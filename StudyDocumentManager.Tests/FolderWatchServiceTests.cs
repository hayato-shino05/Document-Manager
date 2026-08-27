using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Tests;

public sealed class FolderWatchServiceTests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), $"sdm_svc_{Guid.NewGuid():N}");

    private static (FolderWatchService service, FakeWatchedFolderRepository repo, FakeWatchedFolderWatcherFactory factory) Build()
    {
        var repo = new FakeWatchedFolderRepository();
        var factory = new FakeWatchedFolderWatcherFactory();
        var service = new FolderWatchService(repo, factory, new FakeLog(), new FakeLocalizationService());
        return (service, repo, factory);
    }

    [Fact]
    public void Start_LoadsEnabledFolders_AndStartsWatchers()
    {
        var (service, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });

        service.Start();

        Assert.Single(service.Folders);
        Assert.Single(factory.Created);
        Assert.True(factory.Created[0].Started);
        Assert.True(service.IsWatching);
    }

    [Fact]
    public void Start_DisabledFolder_NotStarted()
    {
        var (service, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = false });

        service.Start();

        Assert.Empty(factory.Created);
        Assert.Equal(WatcherStatus.Disabled, service.Folders[0].WatcherStatus);
        Assert.False(service.IsWatching);
    }

    [Fact]
    public void Start_MissingFolder_ReportsError_NotFakeWatching()
    {
        var (service, repo, factory) = Build();
        var missing = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}");
        repo.Add(new WatchedFolder { FolderPath = missing, Enabled = true });

        service.Start();

        Assert.False(service.IsWatching);
        Assert.Empty(factory.Created);
        Assert.Equal(WatcherStatus.Error, service.Folders[0].WatcherStatus);
        Assert.Equal("WF_Error_NotFound", service.Folders[0].WatcherErrorKey);
    }

    [Fact]
    public void ReloadConfig_CalledTwice_DoesNotDuplicateWatchers()
    {
        var (service, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        service.Start();
        var firstWatcher = factory.Created[0];

        service.ReloadConfig();

        Assert.Single(factory.Created);
        Assert.Same(firstWatcher, factory.Created[0]);
        Assert.True(firstWatcher.Started);
        Assert.False(firstWatcher.Stopped);
        Assert.Equal(WatcherStatus.Running, service.Folders[0].WatcherStatus);
    }

    [Fact]
    public void AddFolder_ValidPath_StartsWatcher_AndReturnsNull()
    {
        var (service, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);

        var key = service.AddFolder(dir, false);

        Assert.Null(key);
        Assert.Single(service.Folders);
        Assert.Single(factory.Created);
        Assert.True(factory.Created[0].Started);
    }

    [Fact]
    public void AddFolder_MissingPath_ReturnsErrorKey()
    {
        var (service, _, _) = Build();
        var key = service.AddFolder(Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}"), false);
        Assert.Equal("WF_Error_NotFound", key);
        Assert.Empty(service.Folders);
    }

    [Fact]
    public void RemoveFolder_StopsAndDisposesWatcher()
    {
        var (service, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        service.Start();

        service.RemoveFolder(service.Folders[0].Id);

        Assert.Empty(service.Folders);
        Assert.True(factory.Created[0].Stopped);
        Assert.True(factory.Created[0].Disposed);
    }

    [Fact]
    public void ToggleEnabled_Disable_StopsWatcher()
    {
        var (service, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        service.Start();
        var id = service.Folders[0].Id;

        service.ToggleEnabled(id, false);

        Assert.True(factory.Created[0].Stopped);
        Assert.Equal(WatcherStatus.Disabled, service.Folders[0].WatcherStatus);
        Assert.False(service.IsWatching);
    }

    [Fact]
    public void StopWatching_StopsAllWatchers_AndMarksStopped()
    {
        var (service, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        service.Start();

        service.StopWatching();

        Assert.True(factory.Created[0].Stopped);
        Assert.Equal(WatcherStatus.Stopped, service.Folders[0].WatcherStatus);
        Assert.False(service.IsWatching);
    }

    [Fact]
    public void WatchersSurvive_ModelRecreate_AndDispose()
    {
        var (service, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        service.Start();

        // Simulate navigating to the screen (new model), then away (dispose).
        var model1 = new WatchedFolderModel(service, new FakeNavigationService(), new FakeLocalizationService(), new FakeLog());
        model1.Load();
        model1.Dispose();

        // Background watchers must still be running after the screen is gone.
        Assert.True(factory.Created[0].Started);
        Assert.False(factory.Created[0].Stopped);
        Assert.True(service.IsWatching);

        service.Dispose();
        Assert.True(factory.Created[0].Stopped);
    }

    [Fact]
    public void Start_IsIdempotent_CreatesSingleWatcher()
    {
        var (service, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });

        service.Start();
        service.Start();

        Assert.Single(factory.Created);
        Assert.True(service.IsWatching);
        Assert.Equal(1, service.WatchingCount);
    }

    [Fact]
    public void Dispose_PreventsStartAndReloadFromCreatingWatchers()
    {
        var (context, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        context.Start();

        context.Dispose();
        context.Start();
        context.ReloadConfig();

        // No new watcher may be created after the service is disposed.
        Assert.Single(factory.Created);
        Assert.False(context.IsWatching);
    }

    [Fact]
    public void StopWatching_SetsIsStopped_AndClearsWatching()
    {
        var (service, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        service.Start();

        service.StopWatching();

        Assert.True(service.IsStopped);
        Assert.False(service.IsWatching);
        Assert.Equal(0, service.WatchingCount);
        Assert.True(factory.Created[0].Stopped);
        Assert.Equal(WatcherStatus.Stopped, service.Folders[0].WatcherStatus);
    }

    [Fact]
    public void RetryFolder_WhenWatcherActive_CallsRetryEnqueues()
    {
        var (service, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        service.Start();
        var watcher = factory.Created[0];

        service.RetryFolder(service.Folders[0].Id);

        Assert.True(watcher.RetryEnqueuesCalled);
    }

    [Fact]
    public void AdapterError_UpdatesItemStatus_ToError()
    {
        var (service, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        service.Start();
        var watcher = factory.Created[0];

        watcher.RaiseAdapterError();

        var item = service.Folders[0];
        Assert.Equal(WatcherStatus.Error, item.WatcherStatus);
        Assert.Equal("WF_Error_WatcherFault", item.WatcherErrorKey);
    }

    [Fact]
    public void RemoveFolder_WatcherDisposeThrows_DoesNotCrash_StillRemoves()
    {
        var repo = new FakeWatchedFolderRepository();
        var factory = new FailingDisposeWatcherFactory();
        var service = new FolderWatchService(repo, factory, new FakeLog(), new FakeLocalizationService());
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        service.Start();
        var id = service.Folders[0].Id;

        var ex = Record.Exception(() => service.RemoveFolder(id));

        Assert.Null(ex);
        Assert.Empty(service.Folders);
        Assert.Contains(id, repo.Deleted);
    }

    [Fact]
    public void Dispose_ContinuesCleanup_WhenWatcherStopOrDisposeThrows()
    {
        var repo = new FakeWatchedFolderRepository();
        var factory = new ThrowingWatcherFactory();
        var log = new FakeLog();
        var service = new FolderWatchService(repo, factory, log, new FakeLocalizationService());

        var dir1 = TempDir();
        Directory.CreateDirectory(dir1);
        repo.Add(new WatchedFolder { FolderPath = dir1, Enabled = true });
        var dir2 = Path.Combine(dir1, "b");
        Directory.CreateDirectory(dir2);
        repo.Add(new WatchedFolder { FolderPath = dir2, Enabled = true });
        service.Start();
        Assert.Equal(2, service.WatchingCount);

        var ex = Record.Exception(() => service.Dispose());

        // Dispose はウォッチャの例外を外部へ伝播せず、解体を継続しなければならない。
        Assert.Null(ex);
        Assert.Equal(0, service.WatchingCount);
        Assert.Contains(log.Entries, e => e.Level == "Error");
    }

    [Fact]
    // ReloadConfig はリポジトリから新しいインスタンスを読み直すため、バインド済みの
    // ウォッチャが持つ古い項目参照は Folders から外れる。アダプタエラー発生時に
    // 現在の項目（別インスタンス）へ反映されることを確認する。
    public void AdapterError_AfterReload_UpdatesCurrentItem_NotStale()
    {
        var (service, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        service.Start();

        var stale = service.Folders[0];
        Assert.Equal(WatcherStatus.Running, stale.WatcherStatus);

        // リロードで Folders[0] が作り直される（参照が変わる）。
        service.ReloadConfig();
        Assert.NotSame(stale, service.Folders[0]);
        Assert.Equal(WatcherStatus.Running, service.Folders[0].WatcherStatus);

        // リロード後にエラーが発生しても現在の項目へ反映される。
        factory.Created[0].RaiseAdapterError();
        Assert.Equal(WatcherStatus.Error, service.Folders[0].WatcherStatus);
        Assert.Equal("WF_Error_WatcherFault", service.Folders[0].WatcherErrorKey);
    }

    [Fact]
    // Stop が例外を投げても Dispose は確実に呼ばれ、ステータスのクリーンアップも完了する。
    public void StopThrows_StillDisposesWatcher()
    {
        var repo = new FakeWatchedFolderRepository();
        var factory = new StopThrowingWatcherFactory();
        var service = new FolderWatchService(repo, factory, new FakeLog(), new FakeLocalizationService());
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        service.Start();

        var ex = Record.Exception(() => service.StopWatching());

        Assert.Null(ex);
        var watcher = factory.Created[0];
        Assert.True(watcher.Stopped);
        Assert.True(watcher.Disposed);
        Assert.Equal(0, service.WatchingCount);
        Assert.Equal(WatcherStatus.Stopped, service.Folders[0].WatcherStatus);
    }

    [Fact]
    // 開始に失敗したアダプタは確実に破棄され、リソースリークと状態不整合を起こさない。
    public void Start_Failure_DisposesWatcher()
    {
        var repo = new FakeWatchedFolderRepository();
        var factory = new StartThrowingWatcherFactory();
        var service = new FolderWatchService(repo, factory, new FakeLog(), new FakeLocalizationService());
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });

        var ex = Record.Exception(() => service.Start());

        Assert.Null(ex);
        var watcher = factory.Created[0];
        Assert.True(watcher.Disposed);
        Assert.Equal(0, service.WatchingCount);
        Assert.Equal(WatcherStatus.Error, service.Folders[0].WatcherStatus);
    }

    [Fact]
    // Dispose が Create のブロック中に _disposed をセットしても、ロック内の再判定により
    // ウォッチャは公開（_active への登録）されず、確実に破棄される。Start も例外を外部へ漏らさない。
    public async Task Start_DisposeDuringCreate_DoesNotPublishWatcher()
    {
        var repo = new FakeWatchedFolderRepository();
        var factory = new BlockingCreateWatcherFactory();
        var service = new FolderWatchService(repo, factory, new FakeLog(), new FakeLocalizationService());
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });

        var startTask = Task.Run(() => service.Start());
        Assert.True(factory.Entered.Wait(TimeSpan.FromSeconds(5)));

        var disposeTask = Task.Run(() => service.Dispose());
        // _disposed がセットされ、Dispose がロック取得で待機するのを確実にする。
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        factory.Release();
        var ex = await Record.ExceptionAsync(() => Task.WhenAll(startTask, disposeTask));

        Assert.Null(ex);
        Assert.Equal(0, service.WatchingCount);
        Assert.True(factory.Created[0].Disposed);
    }

    [Fact]
    // Dispose が AddFolder の検査と永続化の間に _disposed をセットしても、ロック内の再判定により
    // 項目は永続化・公開（Folders へ追加）されず、破棄後の状態不整合を起こさない。
    public async Task AddFolder_DisposeBeforePersist_DoesNotAddFolder()
    {
        var repo = new BlockingGetByPathRepo();
        var factory = new FakeWatchedFolderWatcherFactory();
        var service = new FolderWatchService(repo, factory, new FakeLog(), new FakeLocalizationService());
        var dir = TempDir();
        Directory.CreateDirectory(dir);

        var addTask = Task.Run(() => service.AddFolder(dir, false));
        Assert.True(repo.Entered.Wait(TimeSpan.FromSeconds(5)));

        var disposeTask = Task.Run(() => service.Dispose());
        // _disposed がセットされ、Dispose が完了するのを確実にする。
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        repo.Release();
        var ex = await Record.ExceptionAsync(() => Task.WhenAll(addTask, disposeTask));

        Assert.Null(ex);
        Assert.Empty(repo.GetAll());
        Assert.Equal(0, service.Folders.Count);
        Assert.Equal(0, service.WatchingCount);
    }
}

/// <summary>
/// Dispose が例外を投げるウォッチャ。StopWatcher が破棄時のエラーを飲み込み、
/// フォルダの除去／状態更新を中途半端にせず完了することを確認するために使う。
/// </summary>
internal sealed class FailingDisposeWatcher : IWatchedFolderWatcher
{
    public bool IsRunning => true;
    public IReadOnlyList<string> FailedPaths => Array.Empty<string>();
    public event EventHandler? AdapterError;
    public void Start() { }
    public void Stop() { }
    public void RetryEnqueues() { }
    public void Dispose() => throw new InvalidOperationException("dispose boom");
}

internal sealed class FailingDisposeWatcherFactory : IWatchedFolderWatcherFactory
{
    public IWatchedFolderWatcher Create(WatchedFolder config) => new FailingDisposeWatcher();
}

internal sealed class StopThrowingWatcher : IWatchedFolderWatcher
{
    public bool Stopped { get; private set; }
    public bool Disposed { get; private set; }
    public bool IsRunning => true;
    public IReadOnlyList<string> FailedPaths => Array.Empty<string>();
    public event EventHandler? AdapterError;
    public void Start() { }
    public void Stop() { Stopped = true; throw new InvalidOperationException("stop boom"); }
    public void RetryEnqueues() { }
    public void Dispose() => Disposed = true;
}

internal sealed class StopThrowingWatcherFactory : IWatchedFolderWatcherFactory
{
    public List<StopThrowingWatcher> Created { get; } = new();
    public IWatchedFolderWatcher Create(WatchedFolder config)
    {
        var w = new StopThrowingWatcher();
        Created.Add(w);
        return w;
    }
}
internal sealed class StartThrowingWatcher : IWatchedFolderWatcher
{
    public bool Disposed { get; private set; }
    public bool IsRunning => false;
    public IReadOnlyList<string> FailedPaths => Array.Empty<string>();
    public event EventHandler? AdapterError;
    public void Start() => throw new InvalidOperationException("start boom");
    public void Stop() { }
    public void RetryEnqueues() { }
    public void Dispose() => Disposed = true;
}

internal sealed class StartThrowingWatcherFactory : IWatchedFolderWatcherFactory
{
    public List<StartThrowingWatcher> Created { get; } = new();
    public IWatchedFolderWatcher Create(WatchedFolder config)
    {
        var w = new StartThrowingWatcher();
        Created.Add(w);
        return w;
    }
}

/// <summary>
/// Stop と Dispose の両方が例外を投げるウォッチャ。サービスの Dispose が
/// 最初の例外で中断せず、すべてのウォッチャを解体（かつ各失敗をログ出力）し続ける
/// ことを確認するために使う。
/// </summary>
internal sealed class ThrowingWatcher : IWatchedFolderWatcher
{
    public bool IsRunning => true;
    public IReadOnlyList<string> FailedPaths => Array.Empty<string>();
    public event EventHandler? AdapterError;
    public void Start() { }
    public void Stop() => throw new InvalidOperationException("stop boom");
    public void RetryEnqueues() { }
    public void Dispose() => throw new InvalidOperationException("dispose boom");
}

internal sealed class ThrowingWatcherFactory : IWatchedFolderWatcherFactory
{
    public IWatchedFolderWatcher Create(WatchedFolder config) => new ThrowingWatcher();
}

internal sealed class BlockingCreateWatcher : IWatchedFolderWatcher
{
    public bool IsRunning => true;
    public IReadOnlyList<string> FailedPaths => Array.Empty<string>();
    public bool Disposed { get; private set; }
    public event EventHandler? AdapterError;
    public void Start() { }
    public void Stop() { }
    public void RetryEnqueues() { }
    public void Dispose() => Disposed = true;
}

internal sealed class BlockingCreateWatcherFactory : IWatchedFolderWatcherFactory
{
    private readonly ManualResetEventSlim _entered = new(false);
    private readonly ManualResetEventSlim _release = new(false);
    public List<BlockingCreateWatcher> Created { get; } = new();
    public ManualResetEventSlim Entered => _entered;
    public void Release() => _release.Set();
    public IWatchedFolderWatcher Create(WatchedFolder config)
    {
        _entered.Set();
        _release.Wait(TimeSpan.FromSeconds(5));
        var w = new BlockingCreateWatcher();
        Created.Add(w);
        return w;
    }
}

internal sealed class BlockingGetByPathRepo : IWatchedFolderRepository
{
    private readonly FakeWatchedFolderRepository _inner = new();
    private readonly ManualResetEventSlim _entered = new(false);
    private readonly ManualResetEventSlim _release = new(false);
    public ManualResetEventSlim Entered => _entered;
    public void Release() => _release.Set();
    public IReadOnlyList<WatchedFolder> GetAll() => _inner.GetAll();
    public IReadOnlyList<WatchedFolder> GetEnabled() => _inner.GetEnabled();
    public WatchedFolder? GetByPath(string folderPath)
    {
        _entered.Set();
        _release.Wait(TimeSpan.FromSeconds(5));
        return _inner.GetByPath(folderPath);
    }
    public int Add(WatchedFolder item) => _inner.Add(item);
    public bool Update(WatchedFolder item) => _inner.Update(item);
    public bool Delete(int id) => _inner.Delete(id);
    public bool SetEnabled(int id, bool enabled) => _inner.SetEnabled(id, enabled);
    public bool RecordScan(int id, DateTime scannedAt) => _inner.RecordScan(id, scannedAt);
}
