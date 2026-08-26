using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

/// <summary>
/// Owns the running folder watchers for the duration the Watched Folder screen
/// is the active view. The model is registered transient and <see cref="NavigationService"/>
/// disposes the previous view on navigation, so watchers are stopped when the
/// user leaves this screen. Background watching across screens is NOT currently
/// supported: issue #50 acceptance does not define off-screen semantics, and
/// moving ownership to a singleton service is a separate product decision.
/// </summary>
public partial class WatchedFolderModel : ModelBase, IDisposable
{
    private readonly IWatchedFolderRepository _folders;
    private readonly IWatchedFolderWatcherFactory _watcherFactory;
    private readonly ILog _log;
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _loc;
    private readonly Dictionary<int, IWatchedFolderWatcher> _active = new();
    private readonly Dictionary<int, (string Path, bool IncludeSubdirectories)> _activeConfig = new();
    private readonly object _sync = new();

    private string? _statusKey;
    private object[] _statusArgs = Array.Empty<object>();
    private string? _errorKey;
    private object[] _errorArgs = Array.Empty<object>();

    [ObservableProperty] private string? _lastError;
    [ObservableProperty] private bool _isWatching;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _newFolderPath;
    [ObservableProperty] private bool _newFolderIncludeSubdirectories;
    [ObservableProperty] private bool _hasFolders;

    public ObservableCollection<WatchedFolder> Folders { get; } = new();

    public WatchedFolderModel(
        IWatchedFolderRepository folders,
        IWatchedFolderWatcherFactory watcherFactory,
        ILog log,
        INavigationService navigationService,
        ILocalizationService loc)
    {
        _folders = folders ?? throw new ArgumentNullException(nameof(folders));
        _watcherFactory = watcherFactory ?? throw new ArgumentNullException(nameof(watcherFactory));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _loc = loc ?? throw new ArgumentNullException(nameof(loc));
        _loc.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (_statusKey is not null)
            StatusMessage = string.Format(_loc[_statusKey], _statusArgs);
        if (_errorKey is not null)
            LastError = string.Format(_loc[_errorKey], _errorArgs);
        // Recompute each folder's error text from its key so the displayed
        // string is never left in the previous language.
        foreach (var folder in Folders)
        {
            if (!string.IsNullOrEmpty(folder.WatcherErrorKey))
                folder.WatcherError = _loc[folder.WatcherErrorKey];
        }
    }

    private void SetItemError(WatchedFolder item, string key)
    {
        item.WatcherErrorKey = key;
        item.WatcherError = _loc[key];
    }

    private void ClearItemError(WatchedFolder item)
    {
        item.WatcherErrorKey = null;
        item.WatcherError = null;
    }

    private static string Format(string localized, object[] args)
        => args.Length == 0 ? localized : string.Format(localized, args);

    private void SetStatus(string key, params object[] args)
    {
        _statusKey = key;
        _statusArgs = args;
        StatusMessage = Format(_loc[key], args);
    }

    private void SetError(string key, params object[] args)
    {
        _errorKey = key;
        _errorArgs = args;
        LastError = Format(_loc[key], args);
    }

    private void ClearError()
    {
        _errorKey = null;
        _errorArgs = Array.Empty<object>();
        LastError = null;
    }

    public void Load()
    {
        Folders.Clear();
        foreach (var folder in _folders.GetAll())
        {
            if (!folder.Enabled)
                folder.WatcherStatus = WatcherStatus.Disabled;
            Folders.Add(folder);
        }
        HasFolders = Folders.Count > 0;
        ReconcileWatchers();
    }

    [RelayCommand]
    public void AddNewFolder()
        => AddFolder(NewFolderPath, NewFolderIncludeSubdirectories);

    public void AddFolder(string? folderPath) => AddFolder(folderPath, false);

    public void AddFolder(string? folderPath, bool includeSubdirectories)
    {
        ClearError();
        var path = folderPath?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            SetError("WF_Error_PathRequired");
            return;
        }
        if (!Directory.Exists(path))
        {
            SetError("WF_Error_NotFound");
            _log.Warning($"Cannot watch missing folder '{path}'.");
            return;
        }
        if (_folders.GetByPath(path) is not null)
        {
            _log.Information($"Folder already watched: {path}");
            return;
        }

        var item = new WatchedFolder
        {
            FolderPath = path,
            Enabled = true,
            IncludeSubdirectories = includeSubdirectories,
            CreatedAt = DateTime.Now
        };
        _folders.Add(item);
        Folders.Add(item);
        HasFolders = true;
        StartWatcher(item);
        IsWatching = _active.Count > 0;
        SetStatus("WF_Status_Watching", _active.Count);
        NewFolderPath = null;
        NewFolderIncludeSubdirectories = false;
    }

    [RelayCommand]
    public void RemoveFolder(int id)
    {
        StopWatcher(id);
        _folders.Delete(id);
        var existing = Folders.FirstOrDefault(f => f.Id == id);
        if (existing is not null)
            Folders.Remove(existing);
        HasFolders = Folders.Count > 0;
        IsWatching = _active.Count > 0;
    }

    [RelayCommand]
    public void ToggleEnabled(WatchedFolder item)
    {
        if (item is null)
            return;
        _folders.SetEnabled(item.Id, item.Enabled);
        if (item.Enabled)
        {
            item.WatcherStatus = WatcherStatus.Unknown;
            ClearItemError(item);
            StartWatcher(item);
        }
        else
        {
            StopWatcher(item.Id);
            item.WatcherStatus = WatcherStatus.Disabled;
        }
        IsWatching = _active.Count > 0;
    }

    [RelayCommand]
    public void RetryFolder(int id)
    {
        var item = Folders.FirstOrDefault(f => f.Id == id);
        if (item is null || !item.Enabled)
            return;
        item.WatcherStatus = WatcherStatus.Unknown;
        ClearItemError(item);
        StartWatcher(item);
        IsWatching = _active.Count > 0;
    }

    [RelayCommand]
    public void GoBack() => _navigationService.NavigateTo("dashboard");

    [RelayCommand]
    public void StartWatching() => ReconcileWatchers();

    /// <summary>
    /// Reconciles the running watchers with the current configuration: stops
    /// watchers for removed/disabled folders or folders whose path changed, and
    /// starts watchers for enabled folders not already running. For folders that
    /// are already running with an identical config, the freshly reloaded item is
    /// updated to reflect the actual Running status (never left at Unknown). If
    /// only IncludeSubdirectories changed, the watcher is restarted with the new
    /// config. Idempotent: an already-correct watcher is left untouched.
    /// </summary>
    private void ReconcileWatchers()
    {
        var desired = Folders.Where(f => f.Enabled).ToList();
        var desiredById = desired.ToDictionary(f => f.Id);

        foreach (var id in _active.Keys.ToList())
        {
            if (!desiredById.TryGetValue(id, out var current) ||
                _activeConfig[id].Path != current.FolderPath)
            {
                StopWatcher(id);
            }
        }

        foreach (var folder in desired)
        {
            if (_active.ContainsKey(folder.Id))
            {
                if (_activeConfig[folder.Id].IncludeSubdirectories != folder.IncludeSubdirectories)
                {
                    StopWatcher(folder.Id);
                    StartWatcher(folder);
                }
                else
                {
                    // Reloaded item must reflect the actually-running watcher.
                    folder.WatcherStatus = WatcherStatus.Running;
                    folder.WatcherError = null;
                }
            }
            else
            {
                StartWatcher(folder);
            }
        }

        IsWatching = _active.Count > 0;
        if (IsWatching)
            SetStatus("WF_Status_Watching", _active.Count);
        else
            SetStatus("WF_Status_NoFolders");
    }

    [RelayCommand]
    public void StopWatching()
    {
        foreach (var id in _active.Keys.ToList())
            StopWatcher(id);
        IsWatching = false;
        SetStatus("WF_Status_Stopped");
    }

    private void StartWatcher(WatchedFolder item)
    {
        lock (_sync)
        {
            if (_active.ContainsKey(item.Id))
                return;
            if (!item.Enabled)
            {
                item.WatcherStatus = WatcherStatus.Disabled;
                return;
            }
            if (!Directory.Exists(item.FolderPath))
            {
                item.WatcherStatus = WatcherStatus.Error;
                SetItemError(item, "WF_Error_NotFound");
                _log.Warning($"Watched folder does not exist: '{item.FolderPath}'.");
                return;
            }

            IWatchedFolderWatcher watcher;
            try
            {
                watcher = _watcherFactory.Create(item);
                watcher.Start();
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to start watcher for '{item.FolderPath}'.", ex);
                item.WatcherStatus = WatcherStatus.Error;
                SetItemError(item, "WF_Error_StartFailed");
                return;
            }

            if (watcher.IsRunning)
            {
                _active[item.Id] = watcher;
                _activeConfig[item.Id] = (item.FolderPath, item.IncludeSubdirectories);
                item.WatcherStatus = WatcherStatus.Running;
                ClearItemError(item);
            }
            else
            {
                item.WatcherStatus = WatcherStatus.Error;
                SetItemError(item, "WF_Error_StartFailed");
                watcher.Dispose();
            }
        }
    }

    private void StopWatcher(int id)
    {
        IWatchedFolderWatcher? watcher;
        lock (_sync)
        {
            if (!_active.TryGetValue(id, out watcher))
                return;
            _active.Remove(id);
            _activeConfig.Remove(id);
        }
        watcher?.Stop();
        watcher?.Dispose();
        // Reflect the stopped state on the bound item so the UI never shows a
        // stale "Running" indicator after the watcher is gone.
        var item = Folders.FirstOrDefault(f => f.Id == id);
        if (item is not null)
        {
            item.WatcherStatus = WatcherStatus.Stopped;
            ClearItemError(item);
        }
    }

    public void Dispose()
    {
        _loc.LanguageChanged -= OnLanguageChanged;
        foreach (var id in _active.Keys.ToList())
            StopWatcher(id);
    }
}
