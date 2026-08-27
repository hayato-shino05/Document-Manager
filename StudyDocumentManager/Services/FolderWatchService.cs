using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

/// <summary>
/// 実行中のフォルダ監視のシングルトン所有者。監視はアプリ起動時に開始され（App 起動処理参照）、
/// どの画面がアクティブでも監視を継続する。トランジエントな
/// <see cref="StudyDocumentManager.Models.WatchedFolderModel"/> は UI ファサードにすぎない。
/// 新たに発見されたファイルは Import Inbox へ Pending として引き渡され、元ファイルの移動・削除は
/// 行わない。本サービスはアプリケーション終了時に一度だけ破棄される。
/// </summary>
public sealed class FolderWatchService : IFolderWatchService
{
    private readonly IWatchedFolderRepository _folders;
    private readonly IWatchedFolderWatcherFactory _watcherFactory;
    private readonly ILog _log;
    private readonly ILocalizationService _loc;
    private readonly Dictionary<int, IWatchedFolderWatcher> _active = new();
    private readonly Dictionary<int, (string Path, bool IncludeSubdirectories)> _activeConfig = new();
    private readonly HashSet<int> _stopping = new();
    private readonly object _sync = new();
    private bool _started;
    private bool _disposed;
    private bool _isStopped;

    public ObservableCollection<WatchedFolder> Folders { get; } = new();
    public bool IsWatching => _active.Count > 0;
    public bool IsStopped => _isStopped;
    public int WatchingCount => _active.Count;
    public event EventHandler? StateChanged;

    public FolderWatchService(
        IWatchedFolderRepository folders,
        IWatchedFolderWatcherFactory watcherFactory,
        ILog log,
        ILocalizationService loc)
    {
        _folders = folders ?? throw new ArgumentNullException(nameof(folders));
        _watcherFactory = watcherFactory ?? throw new ArgumentNullException(nameof(watcherFactory));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _loc = loc ?? throw new ArgumentNullException(nameof(loc));
    }

    /// <summary>
    /// 任意の UI スレッド マーシャラ。ウォッチャのバックグラウンド コールバック（致命的なアダプタ
    /// エラーなど）から発生したステータス／エラー通知を UI スレッドへ到達させるために使う。
    /// アプリは <c>Dispatcher.UIThread.Post</c> を設定する。テストは null のままとし、
    /// アクションは呼び出しスレッド上でそのまま実行される。
    /// </summary>
    public Action<Action>? UiThreadMarshal { get; set; }

    private void RunMarshaled(Action action) => (UiThreadMarshal ?? (a => a()))(action);

    public void Start()
    {
        if (_disposed || _started)
            return;
        _started = true;
        ReloadConfig();
    }

    public void ReloadConfig()
    {
        if (_disposed)
            return;
        _isStopped = false;
        lock (_sync)
        {
            Folders.Clear();
            foreach (var folder in _folders.GetAll())
            {
                if (!folder.Enabled)
                    folder.WatcherStatus = WatcherStatus.Disabled;
                Folders.Add(folder);
            }
        }
        ReconcileWatchers();
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

    public string? AddFolder(string folderPath, bool includeSubdirectories)
    {
        if (_disposed)
            return null;
        var path = folderPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return "WF_Error_PathRequired";
        // 相対パスを絶対パスに正規化し、監視ルートを一意に決定する。
        try { path = Path.GetFullPath(path); }
        catch (Exception ex)
        {
            _log.Warning($"Cannot watch invalid folder path '{folderPath}'.", ex);
            return "WF_Error_PathRequired";
        }
        if (!Directory.Exists(path))
        {
            _log.Warning($"Cannot watch missing folder '{path}'.");
            return "WF_Error_NotFound";
        }
        if (_folders.GetByPath(path) is not null)
        {
            _log.Information($"Folder already watched: {path}");
            return null;
        }

        var item = new WatchedFolder
        {
            FolderPath = path,
            Enabled = true,
            IncludeSubdirectories = includeSubdirectories,
            CreatedAt = DateTime.Now
        };

        // ロック内で検査と永続化・公開をまとめて行う。ロック外の _disposed 検査と本処理の間に
        // 並行する Dispose が _disposed をセットする競合があり得る。ロック内で再判定し、破棄後に
        // 項目を永続化・公開（Folders へ追加）したりウォッチャを開始したりしないよう保証する。
        lock (_sync)
        {
            if (_disposed)
                return null;
            _folders.Add(item);
            Folders.Add(item);
            StartWatcher(item);
        }
        _isStopped = false;
        RaiseStateChanged();
        return null;
    }

    public void RemoveFolder(int id)
    {
        if (_disposed)
            return;
        StopWatcher(id);
        _folders.Delete(id);
        lock (_sync)
        {
            var existing = Folders.FirstOrDefault(f => f.Id == id);
            if (existing is not null)
                Folders.Remove(existing);
        }
        RaiseStateChanged();
    }

    public void ToggleEnabled(int id, bool enabled)
    {
        if (_disposed)
            return;
        var item = Folders.FirstOrDefault(f => f.Id == id);
        if (item is null)
            return;
        lock (_sync)
        {
            item.Enabled = enabled;
            _folders.SetEnabled(id, enabled);
            _folders.Update(item);
            if (enabled)
            {
                item.WatcherStatus = WatcherStatus.Unknown;
                ClearItemError(item);
                StartWatcher(item);
            }
            else
            {
                StopWatcher(id);
                item.WatcherStatus = WatcherStatus.Disabled;
            }
        }
        _isStopped = false;
        RaiseStateChanged();
    }

    public void RetryFolder(int id)
    {
        if (_disposed)
            return;
        var item = Folders.FirstOrDefault(f => f.Id == id);
        if (item is null || !item.Enabled)
            return;
        _isStopped = false;
        lock (_sync)
        {
            item.WatcherStatus = WatcherStatus.Unknown;
            ClearItemError(item);
            if (_active.TryGetValue(id, out var watcher))
                watcher.RetryEnqueues();
            else
                StartWatcher(item);
        }
        RaiseStateChanged();
    }

    public void StartWatching()
    {
        if (_disposed)
            return;
        _isStopped = false;
        ReconcileWatchers();
    }

    public void StopWatching()
    {
        if (_disposed)
            return;
        _isStopped = true;
        foreach (var id in _active.Keys.ToList())
            StopWatcher(id);
        RaiseStateChanged();
    }

    /// <summary>
    /// 実行中のウォッチャと現在の構成を整合させる。削除／無効化された、あるいはパスが変更された
    /// フォルダのウォッチャを停止し、まだ実行されていない有効フォルダのウォッチャを開始する。
    /// 同一構成で既に実行中のフォルダは、再読み込みされた項目を実際の Running 状態へ更新する
    /// （Unknown のままにしない）。IncludeSubdirectories のみ変更された場合は新しい構成で再起動する。
    /// 冪等であり、すでに正しいウォッチャはそのまま残す。
    /// </summary>
    private void ReconcileWatchers()
    {
        if (_disposed)
            return;
        _isStopped = false;
        lock (_sync)
        {
            var desired = Folders.Where(f => f.Enabled).ToList();
            var desiredById = desired.ToDictionary(f => f.Id);

            foreach (var id in _active.Keys.ToList())
            {
                if (!desiredById.TryGetValue(id, out var current) ||
                    !_activeConfig.ContainsKey(id) ||
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
                        folder.WatcherStatus = WatcherStatus.Running;
                        folder.WatcherError = null;
                    }
                }
                else
                {
                    StartWatcher(folder);
                }
            }
        }

        RaiseStateChanged();
    }

    private void StartWatcher(WatchedFolder item)
    {
        lock (_sync)
        {
            // ロック内で再判定する。ReconcileWatchers 等の呼び出し元は _disposed をロック外で
            // 検査するため、検査と本処理の間に並行する Dispose が _disposed をセットする競合が
            // あり得る。ロック内の判定により、破棄後のウォッチャ生成・公開（_active への登録）を防ぐ。
            if (_disposed || _active.ContainsKey(item.Id) || _stopping.Contains(item.Id))
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

            IWatchedFolderWatcher? watcher = null;
            try
            {
                watcher = _watcherFactory.Create(item);
                watcher.AdapterError += (_, _) => OnWatcherAdapterError(item.Id);
                watcher.Start();
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to start watcher for '{item.FolderPath}'.", ex);
                item.WatcherStatus = WatcherStatus.Error;
                SetItemError(item, "WF_Error_StartFailed");
                // 開始に失敗したアダプタは例外を投げずに確実に破棄する。破棄自体が失敗しても
                // 呼び出し元（Start）へは伝播させず、リソースリークを避ける。
                TryDispose(watcher);
                return;
            }

            // Create のブロック中に並行する Dispose が _disposed をセットした場合、ロック内の再判定
            // によりウォッチャを公開せず、確実に破棄して戻る。これがないと破棄後にアダプタが
            // 生成・公開され、リークと不整合を招く。
            if (_disposed)
            {
                TryDispose(watcher);
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
                TryDispose(watcher);
            }
        }
    }

    // アダプタの破棄中に例外が起きても呼び出し元へ伝播させず、ログに記録するのみとする。
    // 開始失敗時や破棄後の後始末で確実にリソースを解放しつつ、Start 等の処理を例外で中断しない。
    private void TryDispose(IWatchedFolderWatcher? watcher)
    {
        if (watcher is null)
            return;
        try
        {
            watcher.Dispose();
        }
        catch (Exception ex)
        {
            _log.Warning("Error while disposing watcher after failed start.", ex);
        }
    }

    private void OnWatcherAdapterError(int folderId)
    {
        // 致命的なアダプタエラーをバインド中の項目へローカライズ済みエラーとして反映する。
        // 単にログに留めず、UI が失敗を表示できるようにする。ReloadConfig で項目が作り直されても
        // 古いインスタンスを更新しないよう、ここで Id から現在の項目を再解決する。
        RunMarshaled(() =>
        {
            var current = Folders.FirstOrDefault(f => f.Id == folderId);
            if (current is null)
                return;

            current.WatcherStatus = WatcherStatus.Error;
            SetItemError(current, "WF_Error_WatcherFault");
        });
    }

    private void StopWatcher(int id)
    {
        IWatchedFolderWatcher? watcher;
        lock (_sync)
        {
            if (!_active.TryGetValue(id, out watcher))
                return;
            if (_stopping.Contains(id))
                return;
            _stopping.Add(id);
            _active.Remove(id);
            _activeConfig.Remove(id);
        }
        // Stop と Dispose は別の try に分ける。Stop が例外を投げても Dispose を確実に
        // 呼び出し、アダプタのリソース解放を漏らさない。どちらが失敗してもステータスの
        // クリーンアップはこのブロックの直後に完了する。
        try
        {
            watcher?.Stop();
        }
        catch (Exception ex)
        {
            _log.Warning($"Error while stopping watcher for folder id {id}.", ex);
        }
        try
        {
            watcher?.Dispose();
        }
        catch (Exception ex)
        {
            _log.Warning($"Error while disposing watcher for folder id {id}.", ex);
        }
        finally
        {
            lock (_sync) _stopping.Remove(id);
        }
        var item = Folders.FirstOrDefault(f => f.Id == id);
        if (item is not null)
        {
            item.WatcherStatus = WatcherStatus.Stopped;
            ClearItemError(item);
        }
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        IWatchedFolderWatcher[] watchers;
        lock (_sync)
        {
            watchers = _active.Values.ToArray();
            _active.Clear();
            _activeConfig.Clear();
        }
        // 1 件が例外を投げてもすべてのウォッチャを解体する：失敗した Stop/Dispose が
        // 残りのウォッチャの解体を中断してはならない。各ウォッチャは専用の try/catch で扱い、
        // ログには機密を含まない安全なコンテキスト（フォルダパス等）のみを記録する。
        foreach (var watcher in watchers)
        {
            try
            {
                watcher.Stop();
            }
            catch (Exception ex)
            {
                _log.Error($"Error stopping watcher during service dispose.", ex);
            }
            try
            {
                watcher.Dispose();
            }
            catch (Exception ex)
            {
                _log.Error($"Error disposing watcher during service dispose.", ex);
            }
        }
        GC.SuppressFinalize(this);
    }
}
