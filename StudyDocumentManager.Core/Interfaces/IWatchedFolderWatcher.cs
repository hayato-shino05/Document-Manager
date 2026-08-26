using System;

namespace StudyDocumentManager.Core.Interfaces;

/// <summary>
/// Lifecycle contract for a single-folder watcher, decoupled from the OS
/// file-watcher so the model can be tested with a fake.
/// </summary>
public interface IWatchedFolderWatcher : IDisposable
{
    bool IsRunning { get; }
    void Start();
    void Stop();
}
