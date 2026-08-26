using System;
using System.IO;
using System.Threading;
using Xunit;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Tests;

public sealed class FakeFileSystemWatcherAdapter : IFileSystemWatcherAdapter
{
    public string FolderPath { get; }
    public bool IncludeSubdirectories { get; }
    public bool EnableRaisingEvents { get; set; }
    public bool Disposed { get; private set; }

    public event EventHandler<FileSystemWatcherActivityEventArgs>? FileCreated;
    public event EventHandler<FileSystemWatcherErrorEventArgs>? WatcherError;

    public FakeFileSystemWatcherAdapter(string folderPath, bool includeSubdirectories)
    {
        FolderPath = folderPath;
        IncludeSubdirectories = includeSubdirectories;
    }

    public void Start() => EnableRaisingEvents = true;
    public void Stop() => EnableRaisingEvents = false;
    public void Dispose() => Disposed = true;

    public void RaiseCreated(string path)
        => FileCreated?.Invoke(this, new FileSystemWatcherActivityEventArgs { FullPath = path });

    public void RaiseError(Exception ex)
        => WatcherError?.Invoke(this, new FileSystemWatcherErrorEventArgs { Exception = ex });
}

public sealed class FakeFileSystemWatcherAdapterFactory : IFileSystemWatcherAdapterFactory
{
    public FakeFileSystemWatcherAdapter? LastAdapter;
    public IFileSystemWatcherAdapter Create(string folderPath, bool includeSubdirectories)
    {
        LastAdapter = new FakeFileSystemWatcherAdapter(folderPath, includeSubdirectories);
        return LastAdapter;
    }
}

public sealed class RecordingLog : ILog
{
    public int Errors;
    public int Warnings;
    public void Information(string message) { }
    public void Warning(string message, Exception? exception = null) => Warnings++;
    public void Error(string message, Exception? exception = null) => Errors++;
}

public class WatchedFolderWatcherTests : DatabaseTestBase
{
    private readonly string _watchDir;
    private readonly ImportInboxRepository _inbox;
    private readonly WatchedFolderRepository _folders;
    private readonly FakeFileSystemWatcherAdapterFactory _adapterFactory = new();
    private readonly RecordingLog _log = new();

    public WatchedFolderWatcherTests()
    {
        _watchDir = Path.Combine(Path.GetTempPath(), $"sdm_watch_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_watchDir);
        _inbox = new ImportInboxRepository(Db);
        _folders = new WatchedFolderRepository(Db);
    }

    private WatchedFolder SeedFolder(bool includeSubdirectories = false)
    {
        var folder = new WatchedFolder
        {
            FolderPath = _watchDir,
            Enabled = true,
            IncludeSubdirectories = includeSubdirectories
        };
        _folders.Add(folder);
        return folder;
    }

    [Fact]
    public void Start_CreatesAdapter_AndScanNowEnqueuesExistingFiles()
    {
        var file = Path.Combine(_watchDir, "existing.pdf");
        File.WriteAllText(file, "x");

        var folder = SeedFolder();
        using var watcher = new WatchedFolderWatcher(folder, _inbox, _folders, _adapterFactory, _log, TimeSpan.FromHours(1));
        watcher.Start();

        Assert.True(watcher.IsRunning);
        Assert.NotNull(_adapterFactory.LastAdapter);
        Assert.True(_adapterFactory.LastAdapter!.EnableRaisingEvents);
        var inboxItems = _inbox.GetAll();
        Assert.Contains(inboxItems, i => i.SourcePath == file && i.State == ImportInboxState.Pending);
        Assert.True(File.Exists(file)); // never moved/deleted
    }

    [Fact]
    public void FileCreated_Event_HandsOffToInbox_WithoutMovingSource()
    {
        var folder = SeedFolder();
        using var watcher = new WatchedFolderWatcher(folder, _inbox, _folders, _adapterFactory, _log, TimeSpan.FromHours(1));
        watcher.Start();

        var file = Path.Combine(_watchDir, "new.txt");
        File.WriteAllText(file, "data");
        _adapterFactory.LastAdapter!.RaiseCreated(file);
        watcher.ProcessBufferedChanges();

        var inboxItems = _inbox.GetAll();
        Assert.Contains(inboxItems, i => i.SourcePath == file);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void Coalescing_SamePathTwice_EnqueuesOnce()
    {
        var folder = SeedFolder();
        using var watcher = new WatchedFolderWatcher(folder, _inbox, _folders, _adapterFactory, _log, TimeSpan.FromHours(1));
        watcher.Start();

        var file = Path.Combine(_watchDir, "dup.txt");
        File.WriteAllText(file, "data");
        _adapterFactory.LastAdapter!.RaiseCreated(file);
        _adapterFactory.LastAdapter.RaiseCreated(file);
        watcher.ProcessBufferedChanges();

        Assert.Single(_inbox.GetAll(), i => i.SourcePath == file);
    }

    [Fact]
    public void ScanNow_WithSubdirectories_IncludesNestedFiles()
    {
        var sub = Path.Combine(_watchDir, "sub");
        Directory.CreateDirectory(sub);
        var nested = Path.Combine(sub, "nested.pdf");
        File.WriteAllText(nested, "y");

        var folder = SeedFolder(includeSubdirectories: true);
        using var watcher = new WatchedFolderWatcher(folder, _inbox, _folders, _adapterFactory, _log, TimeSpan.FromHours(1));
        watcher.ScanNow();

        Assert.Contains(_inbox.GetAll(), i => i.SourcePath == nested);
    }

    [Fact]
    public void MissingSourceFile_IsSkipped_NotEnqueued()
    {
        var folder = SeedFolder();
        using var watcher = new WatchedFolderWatcher(folder, _inbox, _folders, _adapterFactory, _log, TimeSpan.FromHours(1));
        watcher.Start();

        _adapterFactory.LastAdapter!.RaiseCreated(Path.Combine(_watchDir, "ghost.txt"));
        watcher.ProcessBufferedChanges();

        Assert.DoesNotContain(_inbox.GetAll(), i => i.SourcePath!.Contains("ghost"));
    }

    [Fact]
    public void AdapterError_IsLogged_AndDoesNotThrow()
    {
        var folder = SeedFolder();
        using var watcher = new WatchedFolderWatcher(folder, _inbox, _folders, _adapterFactory, _log, TimeSpan.FromHours(1));
        watcher.Start();

        var ex = new IOException("boom");
        _adapterFactory.LastAdapter!.RaiseError(ex);
        Assert.Equal(1, _log.Errors);
        Assert.True(watcher.IsRunning);
    }

    [Fact]
    public void Stop_DisposesAdapter_AndClearsRunning()
    {
        var folder = SeedFolder();
        var watcher = new WatchedFolderWatcher(folder, _inbox, _folders, _adapterFactory, _log, TimeSpan.FromHours(1));
        watcher.Start();
        watcher.Stop();

        Assert.False(watcher.IsRunning);
        Assert.True(_adapterFactory.LastAdapter!.Disposed);
    }
}
