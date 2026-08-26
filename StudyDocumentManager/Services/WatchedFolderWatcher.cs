using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

/// <summary>
/// Watches a single configured folder and hands newly discovered files off to
/// the Import Inbox as Pending entries. It never moves or deletes the source
/// files. Discovery is debounced/coalesced: bursts of change events within the
/// debounce window collapse to one enqueue per unique path, and the inbox
/// repository de-duplicates by source path so re-scans are safe.
/// </summary>
public sealed class WatchedFolderWatcher : IWatchedFolderWatcher
{
    private readonly WatchedFolder _config;
    private readonly IImportInboxRepository _inbox;
    private readonly IWatchedFolderRepository _folders;
    private readonly IFileSystemWatcherAdapterFactory _adapterFactory;
    private readonly ILog _log;
    private readonly TimeSpan _debounce;

    private IFileSystemWatcherAdapter? _adapter;
    private System.Timers.Timer? _timer;
    private readonly HashSet<string> _buffer = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public bool IsRunning { get; private set; }

    public WatchedFolderWatcher(
        WatchedFolder config,
        IImportInboxRepository inbox,
        IWatchedFolderRepository folders,
        IFileSystemWatcherAdapterFactory adapterFactory,
        ILog log,
        TimeSpan? debounce = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
        _folders = folders ?? throw new ArgumentNullException(nameof(folders));
        _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _debounce = debounce ?? TimeSpan.FromMilliseconds(800);
    }

    public void Start()
    {
        if (!_config.Enabled)
        {
            _log.Information($"Watched folder '{_config.FolderPath}' is disabled; watcher not started.");
            return;
        }
        if (IsRunning)
            return;
        if (!Directory.Exists(_config.FolderPath))
        {
            _log.Warning($"Watched folder '{_config.FolderPath}' does not exist; watcher not started.");
            return;
        }

        try
        {
            _adapter = _adapterFactory.Create(_config.FolderPath, _config.IncludeSubdirectories);
            _adapter.FileCreated += OnFileCreated;
            _adapter.WatcherError += OnAdapterError;
            _adapter.Start();
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to start watcher for '{_config.FolderPath}'.", ex);
            _adapter?.Dispose();
            _adapter = null;
            return;
        }

        _timer = new System.Timers.Timer(_debounce.TotalMilliseconds) { AutoReset = false };
        _timer.Elapsed += (_, _) => ProcessBufferedChanges();
        _timer.Start();
        IsRunning = true;
        ScanNow();
    }

    private void OnFileCreated(object? sender, FileSystemWatcherActivityEventArgs e) => BufferPath(e.FullPath);

    private void BufferPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        lock (_sync)
        {
            _buffer.Add(path);
        }
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Start();
        }
    }

    /// <summary>
    /// Enqueue every buffered path into the Import Inbox as a Pending entry.
    /// Safe against locked/missing/permission-denied files: each failure is
    /// logged and skipped without aborting the remaining batch.
    /// </summary>
    public void ProcessBufferedChanges()
    {
        List<string> paths;
        lock (_sync)
        {
            paths = new List<string>(_buffer);
            _buffer.Clear();
        }

        foreach (var path in paths)
        {
            try
            {
                if (!File.Exists(path))
                    continue;
                _inbox.Add(new ImportInboxItem
                {
                    SourcePath = path,
                    DisplayName = Path.GetFileName(path),
                    State = ImportInboxState.Pending
                });
            }
            catch (IOException ex)
            {
                _log.Warning($"Watched folder could not read '{path}'.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                _log.Warning($"Watched folder access denied for '{path}'.", ex);
            }
            catch (Exception ex)
            {
                _log.Error($"Watched folder failed to enqueue '{path}'.", ex);
            }
        }

        try
        {
            _folders.RecordScan(_config.Id, DateTime.Now);
        }
        catch (Exception ex)
        {
            _log.Warning("Failed to record watched folder scan time.", ex);
        }
    }

    /// <summary>
    /// One-shot scan of the configured folder, buffering every discovered file
    /// for hand-off. Used for initial catch-up and after a restart.
    /// </summary>
    public void ScanNow()
    {
        if (!Directory.Exists(_config.FolderPath))
            return;
        string[] files;
        try
        {
            var option = _config.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            files = Directory.GetFiles(_config.FolderPath, "*", option);
        }
        catch (Exception ex)
        {
            _log.Warning($"Watched folder scan failed for '{_config.FolderPath}'.", ex);
            return;
        }

        foreach (var file in files)
            BufferPath(file);
        ProcessBufferedChanges();
    }

    private void OnAdapterError(object? sender, FileSystemWatcherErrorEventArgs e)
        => _log.Error($"Watcher error for '{_config.FolderPath}'.", e.Exception);

    public void Stop()
    {
        if (!IsRunning)
            return;
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Dispose();
            _timer = null;
        }
        if (_adapter != null)
        {
            _adapter.FileCreated -= OnFileCreated;
            _adapter.WatcherError -= OnAdapterError;
            _adapter.Stop();
            _adapter.Dispose();
            _adapter = null;
        }
        IsRunning = false;
    }

    public void Dispose() => Stop();
}
