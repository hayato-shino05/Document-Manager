using System;
using System.Collections.Generic;

namespace StudyDocumentManager.Core.Interfaces;

/// <summary>
/// Lifecycle contract for a single-folder watcher, decoupled from the OS
/// file-watcher so the model can be tested with a fake.
/// </summary>
public interface IWatchedFolderWatcher : IDisposable
{
    bool IsRunning { get; }

    /// <summary>投入に失敗し、再試行を待っているパスの一覧。</summary>
    IReadOnlyList<string> FailedPaths { get; }

    /// <summary>基盤となる OS のウォッチャが致命的なエラーを報告したときに発生する。</summary>
    event EventHandler? AdapterError;

    void Start();
    void Stop();

    /// <summary>
    /// 過去に失敗したパスの再投入を試みる（ロック中／不在／権限不足）。成功した
    /// パスは削除し、それでも失敗するパスは後の再試行のために保持する。例外は投げない。
    /// </summary>
    void RetryEnqueues();
}
