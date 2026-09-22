using System;
using System.IO;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public sealed class FileSystemWatcherAdapter : IFileSystemWatcherAdapter
{
    private readonly FileSystemWatcher _inner;

    public event EventHandler<FileSystemWatcherActivityEventArgs>? FileCreated;
    public event EventHandler<FileSystemWatcherErrorEventArgs>? WatcherError;

    public string FolderPath { get; }
    public bool IncludeSubdirectories { get; }

    public bool EnableRaisingEvents
    {
        get => _inner.EnableRaisingEvents;
        set => _inner.EnableRaisingEvents = value;
    }

    public FileSystemWatcherAdapter(string folderPath, bool includeSubdirectories)
    {
        FolderPath = folderPath;
        IncludeSubdirectories = includeSubdirectories;
        _inner = new FileSystemWatcher(folderPath)
        {
            IncludeSubdirectories = includeSubdirectories,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            Filter = "*"
        };
        _inner.Created += OnCreated;
        _inner.Changed += OnChanged;
        _inner.Renamed += OnRenamed;
        _inner.Error += OnError;
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
        => FileCreated?.Invoke(this, new FileSystemWatcherActivityEventArgs { FullPath = e.FullPath });

    private void OnChanged(object sender, FileSystemEventArgs e)
        => FileCreated?.Invoke(this, new FileSystemWatcherActivityEventArgs { FullPath = e.FullPath });

    private void OnRenamed(object sender, RenamedEventArgs e)
        => FileCreated?.Invoke(this, new FileSystemWatcherActivityEventArgs { FullPath = e.FullPath });

    private void OnError(object sender, ErrorEventArgs e)
        => WatcherError?.Invoke(this, new FileSystemWatcherErrorEventArgs { Exception = e.GetException() });

    public void Start() => _inner.EnableRaisingEvents = true;
    public void Stop() => _inner.EnableRaisingEvents = false;

    public void Dispose()
    {
        _inner.Created -= OnCreated;
        _inner.Changed -= OnChanged;
        _inner.Renamed -= OnRenamed;
        _inner.Error -= OnError;
        _inner.Dispose();
    }
}
