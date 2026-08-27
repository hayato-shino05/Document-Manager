using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

/// <summary>
/// <see cref="IFolderWatchService"/> の上に置かれた UI ファサード。実際のウォッチャは
/// シングルトン サービスが所有しており、この画面からナビゲーションで離れても監視を継続する。
/// このモデルはサービスの状態を反映し、ユーザー操作を転送するだけである。
/// モデルはトランジエントとして登録され、<see cref="NavigationService"/> によるナビゲーションで
/// 破棄されるが、その破棄がバックグラウンドのウォッチャを停止してはならない。
/// </summary>
public partial class WatchedFolderModel : ModelBase, IDisposable
{
    private readonly IFolderWatchService _service;
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _loc;
    private readonly ILog _log;
    private string? _lastErrorKey;
    private bool _disposed;

    [ObservableProperty] private string? _lastError;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _newFolderPath;
    [ObservableProperty] private bool _newFolderIncludeSubdirectories;

    public ObservableCollection<WatchedFolder> Folders => _service.Folders;
    public bool IsWatching => _service.IsWatching;
    public bool IsStopped => _service.IsStopped;
    public bool HasFolders => Folders.Count > 0;

    public WatchedFolderModel(
        IFolderWatchService service,
        INavigationService navigationService,
        ILocalizationService loc,
        ILog log)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _loc = loc ?? throw new ArgumentNullException(nameof(loc));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _loc.LanguageChanged += OnLanguageChanged;
        _service.StateChanged += OnServiceStateChanged;
    }

    private void OnServiceStateChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsWatching));
        OnPropertyChanged(nameof(HasFolders));
        RefreshStatus();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RefreshStatus();
        if (_lastErrorKey is not null)
            LastError = _loc[_lastErrorKey];
        // 各フォルダのエラー文字列をキーから再計算し、表示文字列が直前の言語のままにならないようにする。
        // また WatcherStatus プロパティを再通知し、コンバータを経由してバインドされた
        // ローカライズ済みステータス ラベルが新しいカルチャで再評価されるようにする。
        foreach (var folder in Folders)
        {
            if (!string.IsNullOrEmpty(folder.WatcherErrorKey))
                folder.WatcherError = _loc[folder.WatcherErrorKey];
            folder.NotifyWatcherStatusChanged();
        }
    }

    private void RefreshStatus()
    {
        if (_service.IsWatching)
            StatusMessage = string.Format(_loc["WF_Status_Watching"], _service.WatchingCount);
        else if (_service.IsStopped)
            StatusMessage = _loc["WF_Status_Stopped"];
        else
            StatusMessage = _loc["WF_Status_NoFolders"];
    }

    private void SetError(string key)
    {
        _lastErrorKey = key;
        LastError = _loc[key];
    }

    private void ClearError()
    {
        _lastErrorKey = null;
        LastError = null;
    }

    public void Load()
    {
        if (_disposed)
            return;
        _service.ReloadConfig();
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
        var key = _service.AddFolder(path, includeSubdirectories);
        if (key is not null)
        {
            SetError(key);
            return;
        }
        NewFolderPath = null;
        NewFolderIncludeSubdirectories = false;
    }

    [RelayCommand]
    public void RemoveFolder(int id) => _service.RemoveFolder(id);

    [RelayCommand]
    public void ToggleEnabled(WatchedFolder item)
    {
        if (item is not null)
            _service.ToggleEnabled(item.Id, item.Enabled);
    }

    [RelayCommand]
    public void RetryFolder(int id) => _service.RetryFolder(id);

    [RelayCommand]
    public void GoBack() => _navigationService.NavigateTo("dashboard");

    [RelayCommand]
    public void StartWatching() => _service.StartWatching();

    [RelayCommand]
    public void StopWatching()
    {
        // サービス側の停止が例外を投げても UI コマンドがクラッシュしないよう防御する。
        // 停止そのものの成否はサービス層で記録される。
        try { _service.StopWatching(); }
        catch (Exception ex) { _log.Error("Failed to stop watched folder monitoring.", ex); }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        // イベント登録解除は各ハンドラごとに個別の try/catch で扱い、
        // 1 件が失敗しても残りの解除を続行する。
        try { _loc.LanguageChanged -= OnLanguageChanged; }
        catch (Exception ex) { _log.Warning("Failed to detach language handler.", ex); }
        try { _service.StateChanged -= OnServiceStateChanged; }
        catch (Exception ex) { _log.Warning("Failed to detach state handler.", ex); }
        GC.SuppressFinalize(this);
    }
}
