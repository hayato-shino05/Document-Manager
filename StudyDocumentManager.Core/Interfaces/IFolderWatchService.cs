using System;
using System.Collections.ObjectModel;
using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

/// <summary>
/// 実行中のフォルダ監視のシングルトン所有者。トランジエントな
/// <see cref="StudyDocumentManager.Models.WatchedFolderModel"/> より長く生存し、
/// 画面ナビゲーションを跨いでも監視を継続し、破棄されるのはアプリケーション終了時のみである。
/// モデルは本サービス上の薄い UI ファサードである。
/// </summary>
public interface IFolderWatchService : IDisposable
{
    ObservableCollection<WatchedFolder> Folders { get; }
    bool IsWatching { get; }
    bool IsStopped { get; }
    int WatchingCount { get; }
    event EventHandler? StateChanged;

    /// <summary>
    /// ウォッチャのバックグラウンド コールバック（致命的なアダプタ エラーなど）を UI スレッドへ
    /// 到達させるために使う、任意のホスト スレッド マーシャラ。アプリはこれを
    /// <c>Dispatcher.UIThread.Post</c> に設定する。null の場合はアクションがそのまま実行される。
    /// </summary>
    Action<Action>? UiThreadMarshal { get; set; }

    void Start();
    void ReloadConfig();
    string? AddFolder(string folderPath, bool includeSubdirectories);
    void RemoveFolder(int id);
    void ToggleEnabled(int id, bool enabled);
    void RetryFolder(int id);
    void StartWatching();
    void StopWatching();
}
