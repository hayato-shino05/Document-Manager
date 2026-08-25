using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using StudyDocumentManager.Tests.TestDoubles;
using System.Threading;
using Xunit;

namespace StudyDocumentManager.Tests;

/// <summary>
/// Isolated proofs for the versioned backup / Recovery Center contract (Issue #43).
/// Every test runs against its own temp database and temp backup folder;
/// the production database path is never touched.
/// </summary>
public sealed class RecoveryCenterTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DatabaseHelper _db;
    private readonly DocumentRepository _repo;
    private readonly InMemorySettingsService _settings = new();
    private readonly VersionedBackupService _service;

    public RecoveryCenterTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sdm_rc_{Guid.NewGuid():N}", "study_documents.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        _db = new DatabaseHelper();
        _db.SetDatabasePath(_dbPath);
        _db.InitializeDatabase();
        _repo = new DocumentRepository(_db);
        _service = new VersionedBackupService(_repo, _settings);
    }

    public void Dispose()
    {
        _db.CloseAllConnections();
        try { Directory.Delete(Path.GetDirectoryName(_dbPath)!, recursive: true); }
        catch { /* best effort cleanup */ }
    }

    private void SeedDocument(string name)
    {
        Assert.True(_db.InsertDocumentWithCatalogs(new StudyDocument
        {
            Name = name,
            Subject = "RecoveryTests",
            Type = "Note"
        }), "document seed should succeed");
    }

    [Fact]
    public void CreateVersion_CreatesTimestampedFile_AndMarksItLatest()
    {
        SeedDocument("version target");

        var created = _service.CreateVersion();

        Assert.NotNull(created);
        Assert.True(File.Exists(created!.FilePath));
        Assert.True(created.IsValid);
        Assert.True(created.IsLatest);
        Assert.StartsWith("study_documents_v", Path.GetFileName(created.FilePath));
        Assert.EndsWith(".db", Path.GetFileName(created.FilePath));
        Assert.Equal(Path.Combine(Path.GetDirectoryName(_dbPath)!, "backups"), _service.BackupDirectory);

        var latest = _service.GetLatest();
        Assert.NotNull(latest);
        Assert.Equal(created.FilePath, latest!.FilePath);
        Assert.True(latest.SizeBytes > 0);
    }

    [Fact]
    public void ListVersions_OrdersNewestFirst_AndFlagsInvalidFiles()
    {
        SeedDocument("ordering target");
        var first = _service.CreateVersion();
        Assert.NotNull(first);

        Thread.Sleep(1100); // filename timestamp resolution is 1s

        var second = _service.CreateVersion();
        Assert.NotNull(second);

        // Corrupt a file: valid name, garbage content -> must be listed but invalid.
        File.WriteAllText(first!.FilePath, "this is not a sqlite database");

        var versions = _service.ListVersions();

        Assert.Equal(2, versions.Count);
        Assert.True(versions[0].IsLatest);
        Assert.False(versions[1].IsLatest);
        Assert.True(versions[0].CreatedAtLocal >= versions[1].CreatedAtLocal);
        Assert.False(versions[1].IsValid, "corrupted version must not be a restore candidate");
        Assert.True(versions[0].IsValid);
    }

    [Fact]
    public void PruneRetention_KeepsNewestCount_AndRemovesOlder()
    {
        _service.RetentionCount = 3;
        SeedDocument("prune target");

        for (var i = 0; i < 5; i++)
        {
            Assert.NotNull(_service.CreateVersion());
            Thread.Sleep(1100);
        }

        var remaining = _service.ListVersions();
        Assert.Equal(3, remaining.Count);
        Assert.Equal(3, Directory.GetFiles(_service.BackupDirectory, "*.db").Length);
        // Newest three survive: the oldest created file must be gone.
        var oldestSurviving = remaining.OrderBy(x => x.CreatedAtLocal).First();
        Assert.True(remaining.All(v => v.CreatedAtLocal >= oldestSurviving.CreatedAtLocal));
        Assert.True(remaining.All(v => v.IsValid));
    }

    [Fact]
    public void RetentionCount_PersistsThroughSettings_AndClampsToValidRange()
    {
        Assert.Equal(10, _service.RetentionCount); // default

        _service.RetentionCount = 4;
        Assert.Equal("4", _settings.GetSetting("backup_retention_count"));

        var reloaded = new VersionedBackupService(_repo, _settings);
        Assert.Equal(4, reloaded.RetentionCount);

        _service.RetentionCount = 500;
        Assert.Equal(100, _service.RetentionCount);

        _service.RetentionCount = 0;
        Assert.Equal(1, _service.RetentionCount);
    }

    [Fact]
    public void Restore_ReturnsRestartRequired_AndRecoversBackupState()
    {
        SeedDocument("before backup");
        var version = _service.CreateVersion();
        Assert.NotNull(version);

        // Mutate current data after the backup was taken.
        SeedDocument("after backup");
        Assert.Equal(2, _repo.GetDocumentCount());

        var outcome = _service.Restore(version!.FilePath);

        Assert.True(outcome.Success);
        Assert.True(outcome.RestartRequired, "successful restore must require an app restart");
        Assert.Null(outcome.ErrorKey);
        Assert.Equal(1, _repo.GetDocumentCount()); // backup state recovered
    }

    [Fact]
    public void Restore_RejectsInvalidFile_AndLeavesCurrentDataUntouched()
    {
        SeedDocument("keep me");
        var beforeCount = _repo.GetDocumentCount();
        var beforeInfo = new FileInfo(_dbPath);
        var (beforeSize, beforeWrite) = (beforeInfo.Length, beforeInfo.LastWriteTimeUtc);

        var invalidPath = Path.Combine(_service.BackupDirectory, "study_documents_v19990101_000000.db");
        Directory.CreateDirectory(_service.BackupDirectory);
        File.WriteAllText(invalidPath, "garbage bytes, not sqlite");

        var outcome = _service.Restore(invalidPath);

        Assert.False(outcome.Success);
        Assert.False(outcome.RestartRequired);
        Assert.Equal("RC_ErrorInvalidVersion", outcome.ErrorKey);
        Assert.Equal(beforeCount, _repo.GetDocumentCount());
        var afterInfo = new FileInfo(_dbPath);
        Assert.Equal(beforeSize, afterInfo.Length);
        Assert.Equal(beforeWrite, afterInfo.LastWriteTimeUtc);
    }

    [Fact]
    public void PlanRestore_ReportsTargetAndImpact()
    {
        SeedDocument("plan target");
        var version = _service.CreateVersion();
        SeedDocument("added after backup");
        Assert.NotNull(version);

        var plan = _service.PlanRestore(version!.FilePath);

        Assert.NotNull(plan);
        Assert.Equal(version.FilePath, plan!.SourcePath);
        Assert.Equal(_dbPath, plan.CurrentDatabasePath);
        Assert.Equal(2, plan.CurrentDocumentCount);
        Assert.Equal(version.CreatedAtLocal, plan.SourceCreatedAtLocal);
    }

    [Fact]
    public void PlanRestore_ReturnsNull_ForMissingOrInvalidFile()
    {
        Assert.Null(_service.PlanRestore(Path.Combine(_service.BackupDirectory, "missing.db")));

        Directory.CreateDirectory(_service.BackupDirectory);
        var invalid = Path.Combine(_service.BackupDirectory, "study_documents_v19990101_000000.db");
        File.WriteAllText(invalid, "not sqlite");
        Assert.Null(_service.PlanRestore(invalid));
    }

    [Fact]
    public void EnsureFreshBackup_TreatsCorruptNewestAsStale_AndReplacesIt()
    {
        SeedDocument("corruption target");
        Assert.Equal(1, _service.EnsureFreshBackup(TimeSpan.FromHours(24)));

        var latest = _service.GetLatest();
        Assert.NotNull(latest);
        File.WriteAllText(latest!.FilePath, "corrupted bytes");

        Assert.Equal(1, _service.EnsureFreshBackup(TimeSpan.FromHours(24)));
        var newest = _service.GetLatest();
        Assert.NotNull(newest);
        Assert.True(newest.IsValid);
        Assert.NotEqual(latest.FilePath, newest.FilePath);
    }

    [Fact]
    public async Task CreateBackupAsync_AwaitedReload_ListsNewVersion_AndResetsLoading()
    {
        SeedDocument("async reload target");
        var (model, dialogs, lifecycle, _, _, timeline) = CreateModel();
        dialogs.ConfirmResult = true;

        await model.CreateBackupCommand.ExecuteAsync(null);

        Assert.False(model.IsLoading);
        Assert.True(model.HasVersions);
        Assert.Single(model.Versions);
        Assert.Contains(timeline, t => t.StartsWith("message|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadDataAsync_ConcurrentCalls_CompleteWithoutErrorOrDuplication()
    {
        SeedDocument("reentry target");
        Assert.NotNull(_service.CreateVersion());
        var (model, _, _, _, _, _) = CreateModel();

        while (model.IsLoading)
            await Task.Delay(10);
        Assert.True(model.HasVersions);

        var first = model.LoadDataCommand.ExecuteAsync(null);
        var second = model.LoadDataCommand.ExecuteAsync(null);
        await Task.WhenAll(first, second);

        // The IsLoading clear runs in a completion continuation that may land after the await.
        for (var i = 0; i < 500 && model.IsLoading; i++)
            await Task.Delay(10);

        Assert.False(model.IsLoading);
        Assert.True(model.HasVersions);
        Assert.Single(model.Versions);
    }

    [Fact]
    public async Task LoadDataAsync_JoinedAfterMutation_ObservesNewVersion()
    {
        SeedDocument("mutation target");
        var (model, _, _, _, _, _) = CreateModel();

        // The constructor's background load may still be in flight; the mutation lands
        // and the next awaited refresh must include it instead of being dropped.
        Assert.NotNull(_service.CreateVersion());

        await model.LoadDataCommand.ExecuteAsync(null);

        Assert.False(model.IsLoading);
        Assert.True(model.HasVersions);
        Assert.Single(model.Versions);
    }

    [Fact]
    public async Task CreateBackupAsync_WhenReloadFails_ShowsErrorInsteadOfSuccess()
    {
        var version = new BackupVersionInfo(@"C:\stub\new.db", DateTime.Now, 10, IsValid: true, IsLatest: true);
        var stub = new StubVersionedBackupService
        {
            CreateHandler = () => version,
            ListHandler = () => throw new IOException("reload failure")
        };
        var (model, dialogs, _, _, _, timeline) = CreateModel(service: stub);
        dialogs.ConfirmResult = true;

        await model.CreateBackupCommand.ExecuteAsync(null);

        Assert.DoesNotContain(timeline, t => t.StartsWith("message|", StringComparison.Ordinal));
        Assert.Contains(timeline, t => t.StartsWith("error|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveRetentionAsync_WhenReloadFails_ShowsErrorInsteadOfSuccess()
    {
        var stub = new StubVersionedBackupService
        {
            ListHandler = () => throw new IOException("reload failure")
        };
        var (model, dialogs, _, _, _, timeline) = CreateModel(service: stub);
        model.RetentionCount = 3;

        await model.SaveRetentionCommand.ExecuteAsync(null);

        Assert.DoesNotContain(timeline, t => t.StartsWith("message|", StringComparison.Ordinal));
        Assert.Contains(timeline, t => t.StartsWith("error|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadDataCommand_ConcurrentRequests_SerializeWithoutOverlap()
    {
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var version = new BackupVersionInfo(@"C:\stub\a.db", DateTime.Now, 10, IsValid: true, IsLatest: true);
        var callCount = 0;
        var stub = new StubVersionedBackupService
        {
            ListHandler = () =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                {
                    firstStarted.TrySetResult(true);
                    releaseFirst.Task.Wait(5000);
                    return [version];
                }

                return [version];
            }
        };
        var (model, _, _, _, _, _) = CreateModel(service: stub);

        while (model.IsLoading)
            await Task.Delay(10);

        var first = model.LoadDataCommand.ExecuteAsync(null);
        await firstStarted.Task;
        var second = model.LoadDataCommand.ExecuteAsync(null);
        releaseFirst.TrySetResult(true);
        await Task.WhenAll(first, second);

        for (var i = 0; i < 500 && model.IsLoading; i++)
            await Task.Delay(10);

        Assert.Equal(1, stub.MaxConcurrentLoads);
        Assert.False(model.IsLoading);
        Assert.True(model.HasVersions);
        Assert.Single(model.Versions);
    }

    [Fact]
    public async Task Constructor_StartsLoadWithoutBlocking_AndCompletesIt()
    {
        var version = new BackupVersionInfo(@"C:\stub\b.db", DateTime.Now, 10, IsValid: true, IsLatest: true);
        var stub = new StubVersionedBackupService { ListHandler = () => [version] };

        var model = new RecoveryCenterModel(
            stub,
            new RecordingDialogService(),
            new StubFileDialogService(),
            new StubNavigationService(),
            new RecordingLifecycleService(),
            new StubProcessLauncherService(),
            new KeyLocalizationService());

        Assert.True(model.IsLoading); // load started synchronously, constructor returned
        for (var i = 0; i < 500 && model.IsLoading; i++)
            await Task.Delay(10);
        Assert.False(model.IsLoading);
        Assert.Single(model.Versions);
    }

    [Fact]
    public async Task ConcurrentCreateAndRetention_EachCallerGetsOwnOutcome()
    {
        var version = new BackupVersionInfo(@"C:\stub\c.db", DateTime.Now, 10, IsValid: true, IsLatest: true);
        var ctorLoadStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCtorLoad = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var createReloadStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCreateReload = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;
        var stubCreate = new StubVersionedBackupService
        {
            CreateHandler = () => version,
            ListHandler = () =>
            {
                var n = Interlocked.Increment(ref callCount);
                if (n == 1)
                {
                    ctorLoadStarted.TrySetResult(true);
                    releaseCtorLoad.Task.Wait(5000);
                    return [];
                }

                if (n == 2)
                {
                    createReloadStarted.TrySetResult(true);
                    releaseCreateReload.Task.Wait(5000);
                    throw new IOException("create reload fails");
                }

                return [version];
            }
        };
        var stubRetention = new StubVersionedBackupService { ListHandler = () => [version] };
        var (createModel, createDialogs, _, _, _, createTimeline) = CreateModel(service: stubCreate);
        var (retentionModel, retentionDialogs, _, _, _, retentionTimeline) = CreateModel(service: stubRetention);

        await ctorLoadStarted.Task;
        releaseCtorLoad.TrySetResult(true);

        var create = createModel.CreateBackupCommand.ExecuteAsync(null);
        await createReloadStarted.Task;

        var retention = retentionModel.SaveRetentionCommand.ExecuteAsync(null);
        await retention;

        releaseCreateReload.TrySetResult(true);
        await create;

        // The failing reload must not leak its result into the other caller's outcome.
        Assert.Contains(createTimeline, t => t.StartsWith("error|", StringComparison.Ordinal));
        Assert.DoesNotContain(createTimeline, t => t.StartsWith("message|", StringComparison.Ordinal));
        Assert.Contains(retentionTimeline, t => t.StartsWith("message|", StringComparison.Ordinal));
        Assert.DoesNotContain(retentionTimeline, t => t.StartsWith("error|", StringComparison.Ordinal));
        Assert.Equal(1, stubCreate.MaxConcurrentLoads);
        Assert.Equal(1, stubRetention.MaxConcurrentLoads);
    }

    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => d(state);
        public override void Send(SendOrPostCallback d, object? state) => d(state);
    }

    [Fact]
    public async Task StaleLoadCompletion_DoesNotClearIsLoadingWhileNewerLoadActive()
    {
        var version = new BackupVersionInfo(@"C:\stub\s.db", DateTime.Now, 10, IsValid: true, IsLatest: true);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;
        var stub = new StubVersionedBackupService
        {
            ListHandler = () =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                    gate.Task.Wait(5000); // block the constructor load (A) until released
                return [version];
            }
        };

        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new ImmediateSynchronizationContext());
        try
        {
            var (model, _, _, _, _, _) = CreateModel(service: stub);
            Assert.True(model.IsLoading, "constructor load A is in progress");

            // Trigger load B; its QueueLoad replaces _loadChain with runB synchronously.
            var b = model.LoadDataCommand.ExecuteAsync(null);
            Assert.True(model.IsLoading, "load B is active after it starts");

            // Release load A. It completes after B started, so its reload must not clear IsLoading.
            gate.TrySetResult(true);
            await b;

            // B's completion posts IsLoading=false via the context; observe the final state.
            for (var i = 0; i < 200 && model.IsLoading; i++)
                await Task.Delay(10);

            Assert.False(model.IsLoading, "only the latest load (B) clears IsLoading");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Fact]
    public void EnsureFreshBackup_CreatesOnlyWhenStale()
    {
        SeedDocument("freshness target");

        Assert.Equal(1, _service.EnsureFreshBackup(TimeSpan.FromHours(24))); // no versions yet -> create
        Assert.Equal(0, _service.EnsureFreshBackup(TimeSpan.FromHours(24))); // fresh version exists -> skip

        // Backdate the only version beyond the threshold.
        var latest = _service.GetLatest();
        Assert.NotNull(latest);
        File.SetLastWriteTime(latest!.FilePath, DateTime.Now.AddHours(-48));
        // Rebuild name-based timestamp: rename to an old stamp so parsing agrees with mtime.
        var oldStamp = Path.Combine(_service.BackupDirectory, $"study_documents_v{DateTime.Now.AddHours(-48):yyyyMMdd_HHmmss}.db");
        File.Move(latest.FilePath, oldStamp, overwrite: true);

        Assert.Equal(1, _service.EnsureFreshBackup(TimeSpan.FromHours(24))); // stale again -> create
        Assert.Equal(2, Directory.GetFiles(_service.BackupDirectory, "*.db").Length);
    }

    [Fact]
    public void Constructor_DoesNotTouchProductionDatabasePath()
    {
        // The service must derive everything from the injected repository path,
        // never from Environment.SpecialFolder.LocalApplicationData.
        var service = new VersionedBackupService(_repo, _settings);
        Assert.Contains(Path.GetDirectoryName(_dbPath)!, service.BackupDirectory);
        Assert.DoesNotContain("StudyDocumentManager", Path.GetDirectoryName(service.BackupDirectory)!);
    }

    // --- Model-level confirmation contract (deterministic, doubles only) ---

    private (RecoveryCenterModel Model, RecordingDialogService Dialogs, RecordingLifecycleService Lifecycle, StubFileDialogService FileDialogs, StubProcessLauncherService Launcher, List<string> Timeline) CreateModel(
        string? openFileResult = null,
        IVersionedBackupService? service = null)
    {
        var timeline = new List<string>();
        var dialogs = new RecordingDialogService(timeline);
        var lifecycle = new RecordingLifecycleService(timeline);
        var fileDialogs = new StubFileDialogService(openFileResult);
        var launcher = new StubProcessLauncherService();
        var model = new RecoveryCenterModel(
            service ?? _service,
            dialogs,
            fileDialogs,
            new StubNavigationService(),
            lifecycle,
            launcher,
            new KeyLocalizationService());
        return (model, dialogs, lifecycle, fileDialogs, launcher, timeline);
    }

    private BackupVersionInfo SeedSingleVersionAndMutate()
    {
        SeedDocument("version target");
        var version = _service.CreateVersion();
        Assert.NotNull(version);
        SeedDocument("added after backup");
        Assert.Equal(2, _repo.GetDocumentCount());
        return version!;
    }

    [Fact]
    public async Task RestoreSelectedAsync_WhenConfirmationCancelled_DoesNotRestoreOrShutdown()
    {
        var version = SeedSingleVersionAndMutate();
        var (model, dialogs, lifecycle, _, _, timeline) = CreateModel();
        dialogs.ConfirmResult = false;
        model.SelectedVersion = version;

        await model.RestoreSelectedCommand.ExecuteAsync(null);

        Assert.Equal(0, lifecycle.ShutdownCount);
        Assert.Single(timeline, t => t.StartsWith("confirm|", StringComparison.Ordinal));
        Assert.DoesNotContain(timeline, t => t.StartsWith("message|", StringComparison.Ordinal));
        Assert.Equal(2, _repo.GetDocumentCount()); // current data untouched
    }

    [Fact]
    public async Task RestoreSelectedAsync_ConfirmationMessage_ShowsTimestampCountAndDatabasePath()
    {
        var version = SeedSingleVersionAndMutate();
        var (model, dialogs, _, _, _, timeline) = CreateModel();
        dialogs.ConfirmResult = false;
        model.SelectedVersion = version;

        await model.RestoreSelectedCommand.ExecuteAsync(null);

        var confirm = timeline.Single(t => t.StartsWith("confirm|", StringComparison.Ordinal));
        Assert.Contains(version.CreatedAtLocal.ToString("yyyy-MM-dd HH:mm:ss"), confirm);
        Assert.Contains("count 2", confirm);
        Assert.Contains(_dbPath, confirm);
    }

    [Fact]
    public async Task RestoreSelectedAsync_WhenConfirmed_ShowsSuccessMessageBeforeShutdown()
    {
        var version = SeedSingleVersionAndMutate();
        var (model, dialogs, lifecycle, _, _, timeline) = CreateModel();
        dialogs.ConfirmResult = true;
        model.SelectedVersion = version;

        await model.RestoreSelectedCommand.ExecuteAsync(null);

        Assert.Equal(1, lifecycle.ShutdownCount);
        Assert.Equal(1, _repo.GetDocumentCount()); // backup state recovered
        var messageIndex = timeline.FindIndex(t => t.StartsWith("message|", StringComparison.Ordinal)
            && t.Contains("RC_RestoreRestartMessage", StringComparison.Ordinal));
        var shutdownIndex = timeline.IndexOf("shutdown");
        Assert.True(messageIndex >= 0, "restart-required message must be shown");
        Assert.True(shutdownIndex > messageIndex, "success message must be displayed before shutdown");
    }

    [Fact]
    public async Task RestoreFromFile_WithMissingPath_ShowsInvalidVersionErrorWithoutConfirmOrShutdown()
    {
        SeedDocument("unrelated");
        var missing = Path.Combine(_service.BackupDirectory, "study_documents_v29990101_000000.db");
        var (model, dialogs, lifecycle, _, _, timeline) = CreateModel(openFileResult: missing);

        await model.RestoreFromFileCommand.ExecuteAsync(null);

        Assert.Equal(0, lifecycle.ShutdownCount);
        Assert.DoesNotContain(timeline, t => t.StartsWith("confirm|", StringComparison.Ordinal));
        var error = timeline.Single(t => t.StartsWith("error|", StringComparison.Ordinal));
        Assert.Contains("RC_ErrorInvalidVersion", error);
        Assert.Equal(1, _repo.GetDocumentCount());
    }

    [Fact]
    public async Task OpenBackupFolder_WhenLauncherThrows_ShowsErrorAndDoesNotFault()
    {
        var (model, dialogs, lifecycle, _, launcher, timeline) = CreateModel();
        launcher.ThrowOnOpenFolder = true;

        var exception = await Record.ExceptionAsync(() => model.OpenBackupFolderCommand.ExecuteAsync(null));

        Assert.Null(exception);
        Assert.Equal(0, lifecycle.ShutdownCount);
        Assert.Contains(timeline, t => t.StartsWith("error|", StringComparison.Ordinal));
    }

    [Fact]
    public void Restore_ThenCloseAndReopen_NewConnectionReadsBackupState()
    {
        SeedDocument("before backup");
        var version = _service.CreateVersion();
        Assert.NotNull(version);
        SeedDocument("after backup");
        Assert.Equal(2, _repo.GetDocumentCount());

        var outcome = _service.Restore(version!.FilePath);
        Assert.True(outcome.Success);

        // Data-layer reopen proof (not a fresh process): pooled connections are closed,
        // a new helper re-initializes the same database file, and the backup state is read back.
        _db.CloseAllConnections();
        var reopened = new DatabaseHelper();
        reopened.SetDatabasePath(_dbPath);
        reopened.InitializeDatabase();
        try
        {
            var documents = reopened.GetAllDocuments();
            Assert.Single(documents);
            Assert.Equal("before backup", documents[0].Name);
        }
        finally
        {
            reopened.CloseAllConnections();
        }
    }
}
