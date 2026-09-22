using System;

namespace StudyDocumentManager.Core.Interfaces;

public sealed class FileSystemWatcherActivityEventArgs : EventArgs
{
    public string FullPath { get; init; } = string.Empty;
}

public sealed class FileSystemWatcherErrorEventArgs : EventArgs
{
    public Exception Exception { get; init; } = null!;
}

/// <summary>
/// Testable seam around the OS file watcher. The real implementation wraps
/// System.IO.FileSystemWatcher; tests supply a fake that can raise events.
/// </summary>
public interface IFileSystemWatcherAdapter : IDisposable
{
    event EventHandler<FileSystemWatcherActivityEventArgs>? FileCreated;
    event EventHandler<FileSystemWatcherErrorEventArgs>? WatcherError;
    string FolderPath { get; }
    bool IncludeSubdirectories { get; }
    bool EnableRaisingEvents { get; set; }
    void Start();
    void Stop();
}

public interface IFileSystemWatcherAdapterFactory
{
    IFileSystemWatcherAdapter Create(string folderPath, bool includeSubdirectories);
}
