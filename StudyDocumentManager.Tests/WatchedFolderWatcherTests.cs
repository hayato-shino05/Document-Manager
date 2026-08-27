using System;
using System.Collections.Concurrent;
using Xunit;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Tests;

/// <summary>
/// Regression coverage for the WatchedFolderWatcher timer lifecycle race:
/// BufferPath touches _timer outside any lock while Stop can dispose/null it.
/// The fix captures the timer under _sync, so concurrent event/stop must never
/// throw NRE or ObjectDisposedException.
/// </summary>
public sealed class WatchedFolderWatcherTests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), $"sdm_wfw_{Guid.NewGuid():N}");

    private sealed class FakeAdapter : IFileSystemWatcherAdapter
    {
        public string FolderPath { get; }
        public bool IncludeSubdirectories { get; }
        public bool EnableRaisingEvents { get; set; }
        public bool Started { get; private set; }
        public int StartCount { get; private set; }
        public bool Stopped { get; private set; }
        public bool Disposed { get; private set; }

        public FakeAdapter(string folderPath, bool includeSubdirectories)
        {
            FolderPath = folderPath;
            IncludeSubdirectories = includeSubdirectories;
        }

        public event EventHandler<FileSystemWatcherActivityEventArgs>? FileCreated;
        public event EventHandler<FileSystemWatcherErrorEventArgs>? WatcherError;

        public void Start() { Started = true; StartCount++; }
        public void Stop() => Stopped = true;
        public void Dispose() => Disposed = true;

        public void RaiseFileCreated(string path)
            => FileCreated?.Invoke(this, new FileSystemWatcherActivityEventArgs { FullPath = path });

        public void RaiseWatcherError(Exception exception)
            => WatcherError?.Invoke(this, new FileSystemWatcherErrorEventArgs { Exception = exception });
    }

    // Stop の解体で例外が起きても、残りのリソース解体が続行されることを確認するためのアダプタ。
    private sealed class FaultyAdapter : IFileSystemWatcherAdapter
    {
        public string FolderPath { get; }
        public bool IncludeSubdirectories { get; }
        public bool EnableRaisingEvents { get; set; }
        public bool StopThrows { get; init; }
        public bool DisposeThrows { get; init; }
        public bool StopCalled { get; private set; }
        public bool DisposeCalled { get; private set; }
        public bool DetachedFile { get; private set; }
        public bool DetachedError { get; private set; }

        public FaultyAdapter(string folderPath, bool includeSubdirectories)
        {
            FolderPath = folderPath;
            IncludeSubdirectories = includeSubdirectories;
        }

        public event EventHandler<FileSystemWatcherActivityEventArgs>? FileCreated;
        public event EventHandler<FileSystemWatcherErrorEventArgs>? WatcherError;

        public void Start() { }
        public void Stop()
        {
            StopCalled = true;
            if (StopThrows)
                throw new InvalidOperationException("adapter stop boom");
        }
        public void Dispose()
        {
            DisposeCalled = true;
            if (DisposeThrows)
                throw new InvalidOperationException("adapter dispose boom");
        }
    }

    private sealed class FakeAdapterFactory : IFileSystemWatcherAdapterFactory
    {
        private readonly IFileSystemWatcherAdapter _adapter;
        public FakeAdapterFactory(IFileSystemWatcherAdapter adapter) => _adapter = adapter;
        public IFileSystemWatcherAdapter Create(string folderPath, bool includeSubdirectories) => _adapter;
    }

    private sealed class FakeInbox : IImportInboxRepository
    {
        private readonly object _gate = new();
        private readonly List<ImportInboxItem> _items = new();
        public bool ThrowOnAdd { get; set; }
        public IReadOnlyList<ImportInboxItem> GetAll(bool includeProcessed = false) => _items;
        public List<ImportInboxItem> Added => _items;
        public ImportInboxItem? GetById(int id) => null;
        public int Add(ImportInboxItem item)
        {
            if (ThrowOnAdd)
                throw new InvalidOperationException("inbox add boom");
            lock (_gate) { _items.Add(item); }
            return _items.Count;
        }
        public bool Update(ImportInboxItem item) => true;
        public bool UpdateState(int id, ImportInboxState state, string? failureCode = null) => true;
        public int BulkEditMetadata(IReadOnlyList<int> documentIds, BulkEditChanges changes) => 0;
    }

    private sealed class FakeFolderRepo : IWatchedFolderRepository
    {
        public IReadOnlyList<WatchedFolder> GetAll() => Array.Empty<WatchedFolder>();
        public IReadOnlyList<WatchedFolder> GetEnabled() => Array.Empty<WatchedFolder>();
        public WatchedFolder? GetByPath(string folderPath) => null;
        public int Add(WatchedFolder item) => 0;
        public bool Update(WatchedFolder item) => true;
        public bool Delete(int id) => true;
        public bool SetEnabled(int id, bool enabled) => true;
        public bool RecordScan(int id, DateTime scannedAt) => true;
    }

    private sealed class BlockingAdapter : IFileSystemWatcherAdapter
    {
        public string FolderPath { get; }
        public BlockingAdapter(string folderPath) => FolderPath = folderPath;
        public bool IncludeSubdirectories { get; }
        public bool EnableRaisingEvents { get; set; }
        public bool Started { get; private set; }
        public event EventHandler<FileSystemWatcherActivityEventArgs>? FileCreated;
        public event EventHandler<FileSystemWatcherErrorEventArgs>? WatcherError;
        public void Start() => Started = true;
        public void Stop() { }
        public void Dispose() { }
    }

    private sealed class BlockingAdapterFactory : IFileSystemWatcherAdapterFactory
    {
        private readonly IFileSystemWatcherAdapter _adapter;
        private readonly ManualResetEventSlim _createEntered;
        private readonly ManualResetEventSlim _createRelease;
        public BlockingAdapterFactory(IFileSystemWatcherAdapter adapter, ManualResetEventSlim createEntered, ManualResetEventSlim createRelease)
        {
            _adapter = adapter;
            _createEntered = createEntered;
            _createRelease = createRelease;
        }
        public IFileSystemWatcherAdapter Create(string folderPath, bool includeSubdirectories)
        {
            _createEntered.Set();
            _createRelease.Wait(TimeSpan.FromSeconds(5));
            return _adapter;
        }
    }

    [Fact]
    // Dispose が先に _disposed をセットし、Start のロック内処理の前に並行して走る競合を再現する。
    // 修正後はロック内の _disposed 判定により、破棄後にアダプタが生成・開始されることはない。
    public async Task Start_AfterConcurrentDispose_DoesNotCreateAdapter()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var createEntered = new ManualResetEventSlim(false);
        var createRelease = new ManualResetEventSlim(false);
        var adapter = new BlockingAdapter(dir);
        var watcher = new WatchedFolderWatcher(
            new WatchedFolder { FolderPath = dir, Enabled = true, IncludeSubdirectories = false },
            new FakeInbox(), new FakeFolderRepo(), new BlockingAdapterFactory(adapter, createEntered, createRelease),
            new FakeLog(), TimeSpan.FromMilliseconds(1));

        var startTask = Task.Run(() => watcher.Start());
        Assert.True(createEntered.Wait(TimeSpan.FromSeconds(5)));

        var disposeTask = Task.Run(() => watcher.Dispose());
        // Dispose が _disposed をセットし（Stop は Start が保持するロックで待機する）、
        // Create ブロック中に競合が発生するよう、確実に _disposed が立ってから Create を解放する。
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        createRelease.Set();
        await Task.WhenAll(startTask, disposeTask);

        Assert.False(watcher.IsRunning);
        Assert.False(adapter.Started);
    }

    [Fact]
    public void BufferPath_AfterStop_DoesNotThrow_OnNullTimer()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FakeAdapter(dir, false);
        var watcher = new WatchedFolderWatcher(
            new WatchedFolder { FolderPath = dir, Enabled = true, IncludeSubdirectories = false },
            new FakeInbox(), new FakeFolderRepo(), new FakeAdapterFactory(adapter), new FakeLog(),
            TimeSpan.FromMilliseconds(1));

        watcher.Start();
        watcher.Stop();
        // Timer is now null/disposed; a late event must not NRE.
        var ex = Record.Exception(() => adapter.RaiseFileCreated(Path.Combine(dir, "late.pdf")));
        Assert.Null(ex);
    }

    [Fact]
    public void Stop_Twice_DoesNotThrow()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FakeAdapter(dir, false);
        var watcher = new WatchedFolderWatcher(
            new WatchedFolder { FolderPath = dir, Enabled = true, IncludeSubdirectories = false },
            new FakeInbox(), new FakeFolderRepo(), new FakeAdapterFactory(adapter), new FakeLog(),
            TimeSpan.FromMilliseconds(1));

        watcher.Start();
        watcher.Stop();
        var ex = Record.Exception(() => watcher.Stop());
        Assert.Null(ex);
    }

    [Fact]
    public void BufferPath_ConcurrentWithStop_DoesNotThrow()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FakeAdapter(dir, false);
        var watcher = new WatchedFolderWatcher(
            new WatchedFolder { FolderPath = dir, Enabled = true, IncludeSubdirectories = false },
            new FakeInbox(), new FakeFolderRepo(), new FakeAdapterFactory(adapter), new FakeLog(),
            TimeSpan.FromMilliseconds(1));

        var exceptions = new ConcurrentBag<Exception>();
        watcher.Start();

        const int iterations = 40;
        for (var i = 0; i < iterations; i++)
        {
            var barrier = new Barrier(2);
            var tEvent = new Thread(() =>
            {
                try { barrier.SignalAndWait(); adapter.RaiseFileCreated(Path.Combine(dir, $"f{i}.pdf")); }
                catch (Exception ex) { exceptions.Add(ex); }
            });
            var tStop = new Thread(() =>
            {
                try { barrier.SignalAndWait(); watcher.Stop(); }
                catch (Exception ex) { exceptions.Add(ex); }
            });
            tEvent.Start();
            tStop.Start();
            tEvent.Join();
            tStop.Join();
            try { watcher.Start(); }
            catch (Exception ex) { exceptions.Add(ex); }
        }

        Assert.Empty(exceptions);
    }

    private static WatchedFolderWatcher BuildWatcher(string dir, FakeAdapter adapter, FakeInbox inbox, TimeSpan debounce)
        => new(
            new WatchedFolder { FolderPath = dir, Enabled = true, IncludeSubdirectories = false },
            inbox, new FakeFolderRepo(), new FakeAdapterFactory(adapter), new FakeLog(),
            debounce);

    [Fact]
    public void FileCreated_EnqueuesPendingIntoInbox()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FakeAdapter(dir, false);
        var inbox = new FakeInbox();
        var watcher = BuildWatcher(dir, adapter, inbox, TimeSpan.FromHours(1));

        // Start runs the initial scan (no files yet); the file appears afterwards.
        watcher.Start();
        var file = Path.Combine(dir, "a.pdf");
        File.WriteAllText(file, "x");
        adapter.RaiseFileCreated(file);
        watcher.ProcessBufferedChanges();

        Assert.Single(inbox.Added);
        Assert.Equal(ImportInboxState.Pending, inbox.Added[0].State);
        Assert.Equal(file, inbox.Added[0].SourcePath);
    }

    [Fact]
    public void DuplicateFileCreated_EnqueuesOnce()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FakeAdapter(dir, false);
        var inbox = new FakeInbox();
        var watcher = BuildWatcher(dir, adapter, inbox, TimeSpan.FromHours(1));

        watcher.Start();
        var file = Path.Combine(dir, "dup.pdf");
        File.WriteAllText(file, "x");
        adapter.RaiseFileCreated(file);
        adapter.RaiseFileCreated(file);
        watcher.ProcessBufferedChanges();

        Assert.Single(inbox.Added);
    }

    [Fact]
    public void Start_IncludeSubdirectories_FindsNestedFiles()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var sub = Path.Combine(dir, "sub");
        Directory.CreateDirectory(sub);
        var top = Path.Combine(dir, "top.pdf");
        var nested = Path.Combine(sub, "nested.pdf");
        File.WriteAllText(top, "x");
        File.WriteAllText(nested, "x");
        var adapter = new FakeAdapter(dir, true);
        var inbox = new FakeInbox();
        var watcher = new WatchedFolderWatcher(
            new WatchedFolder { FolderPath = dir, Enabled = true, IncludeSubdirectories = true },
            inbox, new FakeFolderRepo(), new FakeAdapterFactory(adapter), new FakeLog(),
            TimeSpan.FromHours(1));

        // Start triggers the initial nested scan.
        watcher.Start();

        Assert.Equal(2, inbox.Added.Count);
    }

    [Fact]
    public void FileCreated_MissingPath_Skipped_NotEnqueued()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var ghost = Path.Combine(dir, "ghost.pdf");
        var adapter = new FakeAdapter(dir, false);
        var inbox = new FakeInbox();
        var watcher = BuildWatcher(dir, adapter, inbox, TimeSpan.FromHours(1));

        watcher.Start();
        adapter.RaiseFileCreated(ghost);
        watcher.ProcessBufferedChanges();

        Assert.Empty(inbox.Added);
    }

    [Fact]
    public void AdapterError_RaisesEvent()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FakeAdapter(dir, false);
        var inbox = new FakeInbox();
        var watcher = BuildWatcher(dir, adapter, inbox, TimeSpan.FromHours(1));
        watcher.Start();

        var fired = false;
        watcher.AdapterError += (_, _) => fired = true;
        adapter.RaiseWatcherError(new Exception("boom"));

        Assert.True(fired);
    }

    [Fact]
    public void Dispose_StopsAndDisposesAdapter()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FakeAdapter(dir, false);
        var inbox = new FakeInbox();
        var watcher = BuildWatcher(dir, adapter, inbox, TimeSpan.FromHours(1));

        watcher.Start();
        watcher.Dispose();

        Assert.True(adapter.Stopped);
        Assert.True(adapter.Disposed);
        Assert.False(watcher.IsRunning);
    }

    [Fact]
    public void Start_Concurrent_DoesNotDoubleStart()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FakeAdapter(dir, false);
        var inbox = new FakeInbox();
        var watcher = BuildWatcher(dir, adapter, inbox, TimeSpan.FromHours(1));
        var errors = new ConcurrentBag<Exception>();

        var threads = Enumerable.Range(0, 8)
            .Select(_ => new Thread(() =>
            {
                try { watcher.Start(); }
                catch (Exception ex) { errors.Add(ex); }
            }))
            .ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        Assert.Empty(errors);
        Assert.Equal(1, adapter.StartCount);
        Assert.True(watcher.IsRunning);
    }

    [Fact]
    public void StartAndStop_Concurrent_NoLeak()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FakeAdapter(dir, false);
        var inbox = new FakeInbox();
        var watcher = BuildWatcher(dir, adapter, inbox, TimeSpan.FromHours(1));
        var errors = new ConcurrentBag<Exception>();

        var threads = Enumerable.Range(0, 4)
            .SelectMany(_ => new[]
            {
                new Thread(() => { try { watcher.Start(); } catch (Exception ex) { errors.Add(ex); } }),
                new Thread(() => { try { watcher.Stop(); } catch (Exception ex) { errors.Add(ex); } })
            })
            .ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        // 最後の確定的な破棄で、アダプタをリークせずきれいに停止することを確認する。
        watcher.Stop();

        Assert.Empty(errors);
        Assert.False(watcher.IsRunning);
        Assert.True(adapter.Disposed);
    }

    [Fact]
    public void QueuedCallbackAfterStop_DoesNotProcess()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FakeAdapter(dir, false);
        var inbox = new FakeInbox();
        var watcher = BuildWatcher(dir, adapter, inbox, TimeSpan.FromMilliseconds(1));
        watcher.Start();

        watcher.Stop();
        // Stop 完了前にすでにキューされていたタイマコールバックを再現する。
        watcher.ProcessBufferedChanges();

        Assert.False(watcher.IsRunning);
        Assert.Empty(inbox.Added);
    }

    [Fact]
    public void StartStopStart_SameInstance_EnqueuesFileAfterRestart()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FakeAdapter(dir, false);
        var inbox = new FakeInbox();
        var watcher = BuildWatcher(dir, adapter, inbox, TimeSpan.FromHours(1));

        watcher.Start();
        watcher.Stop();
        // 同一インスタンスでの再起動。Stop は可逆なので新しい Start が動作する必要がある
        // （以前は Stop が _disposed をセットし、以降の Start をブロックしていた）。
        watcher.Start();
        Assert.True(watcher.IsRunning);

        var file = Path.Combine(dir, "after_restart.pdf");
        File.WriteAllText(file, "x");
        adapter.RaiseFileCreated(file);
        watcher.ProcessBufferedChanges();

        Assert.Single(inbox.Added);
        Assert.Equal(file, inbox.Added[0].SourcePath);
    }

    [Fact]
    public void TimerCallback_StaleGenerationAfterStop_SkipsProcessing()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FakeAdapter(dir, false);
        var inbox = new FakeInbox();
        var watcher = BuildWatcher(dir, adapter, inbox, TimeSpan.FromHours(1));
        watcher.Start();

        // 実タイマが捕捉したこの世代を Stop が進めるため、無効となる。
        var staleGeneration = watcher.CurrentGeneration;
        watcher.Stop();

        var file = Path.Combine(dir, "stale.pdf");
        File.WriteAllText(file, "x");
        adapter.RaiseFileCreated(file);

        // 古い世代を持つキュー済みコールバックは処理をスキップしなければならない。
        watcher.OnTimerElapsed(staleGeneration);
        Assert.Empty(inbox.Added);
    }

    [Fact]
    public void TimerCallback_CurrentGenerationAfterRestart_Processes()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FakeAdapter(dir, false);
        var inbox = new FakeInbox();
        var watcher = BuildWatcher(dir, adapter, inbox, TimeSpan.FromHours(1));
        watcher.Start();
        watcher.Stop();
        watcher.Start();

        var currentGeneration = watcher.CurrentGeneration;
        var file = Path.Combine(dir, "current.pdf");
        File.WriteAllText(file, "x");
        adapter.RaiseFileCreated(file);

        watcher.OnTimerElapsed(currentGeneration);
        Assert.Single(inbox.Added);
    }

    [Fact]
    public void RetryEnqueues_KeepsFailedPath_Logs_WhenEnqueueThrows()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FakeAdapter(dir, false);
        var inbox = new FakeInbox { ThrowOnAdd = true };
        var log = new FakeLog();
        var watcher = new WatchedFolderWatcher(
            new WatchedFolder { FolderPath = dir, Enabled = true },
            inbox, new FakeFolderRepo(), new FakeAdapterFactory(adapter), log,
            TimeSpan.FromHours(1));

        watcher.Start();
        var file = Path.Combine(dir, "retry.pdf");
        File.WriteAllText(file, "x");
        adapter.RaiseFileCreated(file);
        watcher.ProcessBufferedChanges(); // inbox が例外を投げる -> 失敗として記録される

        Assert.Contains(file, watcher.FailedPaths);

        watcher.RetryEnqueues(); // それでも投げる -> 保持され、飲み込まれない

        Assert.Contains(file, watcher.FailedPaths);
        Assert.Contains(log.Entries, e => e.Level == "Error");
        // 状態は未処理の失敗を隠さず反映する。
        Assert.Equal(WatcherStatus.Error, watcher.CurrentStatus);
    }

    [Fact]
    public void RetryEnqueues_RemovesFailedPath_OnSuccess()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FakeAdapter(dir, false);
        var inbox = new FakeInbox { ThrowOnAdd = true };
        var log = new FakeLog();
        var watcher = new WatchedFolderWatcher(
            new WatchedFolder { FolderPath = dir, Enabled = true },
            inbox, new FakeFolderRepo(), new FakeAdapterFactory(adapter), log,
            TimeSpan.FromHours(1));

        watcher.Start();
        var file = Path.Combine(dir, "retry2.pdf");
        File.WriteAllText(file, "x");
        adapter.RaiseFileCreated(file);
        watcher.ProcessBufferedChanges();
        Assert.Contains(file, watcher.FailedPaths);

        inbox.ThrowOnAdd = false;
        watcher.RetryEnqueues();

        Assert.DoesNotContain(file, watcher.FailedPaths);
        Assert.Single(inbox.Added);
    }

    [Fact]
    public void FileCreated_OutsideConfiguredRoot_IsNotEnqueued()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FakeAdapter(dir, false);
        var inbox = new FakeInbox();
        var watcher = BuildWatcher(dir, adapter, inbox, TimeSpan.FromHours(1));
        watcher.Start();

        var outside = Path.Combine(Path.GetTempPath(), $"sdm_outside_{Guid.NewGuid():N}.pdf");
        File.WriteAllText(outside, "x");
        adapter.RaiseFileCreated(outside);
        watcher.ProcessBufferedChanges();

        Assert.Empty(inbox.Added);
    }

    [Fact]
    public void FileCreated_InsideRoot_IsEnqueued()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FakeAdapter(dir, false);
        var inbox = new FakeInbox();
        var watcher = BuildWatcher(dir, adapter, inbox, TimeSpan.FromHours(1));
        watcher.Start();

        var file = Path.Combine(dir, "inside.pdf");
        File.WriteAllText(file, "x");
        adapter.RaiseFileCreated(file);
        watcher.ProcessBufferedChanges();

        Assert.Single(inbox.Added);
        Assert.Equal(file, inbox.Added[0].SourcePath);
    }

    [Fact]
    public void FileCreated_Subdir_OnlyWhenIncludeSubdirectories()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var sub = Path.Combine(dir, "sub");
        Directory.CreateDirectory(sub);
        var adapter = new FakeAdapter(dir, false);
        var inbox = new FakeInbox();
        var watcher = BuildWatcher(dir, adapter, inbox, TimeSpan.FromHours(1));
        watcher.Start();

        var file = Path.Combine(sub, "deep.pdf");
        File.WriteAllText(file, "x");
        adapter.RaiseFileCreated(file);
        watcher.ProcessBufferedChanges();

        // IncludeSubdirectories=false のため、サブフォルダ配下は投入されない。
        Assert.Empty(inbox.Added);
    }

    [Fact]
    public void Stop_CleanupContinues_WhenAdapterStopThrows()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FaultyAdapter(dir, false) { StopThrows = true };
        var watcher = new WatchedFolderWatcher(
            new WatchedFolder { FolderPath = dir, Enabled = true },
            new FakeInbox(), new FakeFolderRepo(), new FakeAdapterFactory(adapter), new FakeLog(),
            TimeSpan.FromHours(1));
        watcher.Start();

        var ex = Record.Exception(() => watcher.Stop());

        // 例外は外に伝播せず、解体は続行され IsRunning は常に false になる。
        Assert.Null(ex);
        Assert.False(watcher.IsRunning);
        Assert.True(adapter.StopCalled);
        Assert.True(adapter.DisposeCalled);
    }

    [Fact]
    public void Stop_CleanupContinues_WhenAdapterDisposeThrows()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var adapter = new FaultyAdapter(dir, false) { DisposeThrows = true };
        var watcher = new WatchedFolderWatcher(
            new WatchedFolder { FolderPath = dir, Enabled = true },
            new FakeInbox(), new FakeFolderRepo(), new FakeAdapterFactory(adapter), new FakeLog(),
            TimeSpan.FromHours(1));
        watcher.Start();

        var ex = Record.Exception(() => watcher.Stop());

        Assert.Null(ex);
        Assert.False(watcher.IsRunning);
        Assert.True(adapter.StopCalled);
        Assert.True(adapter.DisposeCalled);
    }
}
