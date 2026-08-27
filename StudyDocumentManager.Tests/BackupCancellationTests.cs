using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

/// <summary>
/// Deterministic proofs that an in-progress backup/restore cancellation does NOT
/// swap the live database, does NOT report success, and does NOT trigger shutdown.
/// Uses a blocking test double instead of timing races.
/// </summary>
public sealed class BackupCancellationServiceTests
{
    [Fact]
    public async Task RestoreAsync_WhenCancelledMidOperation_DoesNotShutdownAndReportsNoSuccess()
    {
        var repo = new BlockingFileIntegrityRepository();
        var lifecycle = new RecordingLifecycleService();
        var restorePath = Path.GetTempFileName();
        var service = new DatabaseBackupService(
            repo,
            new StubFileDialogService(openPath: restorePath),
            new NoOpDialogService(),
            lifecycle,
            new KeyLocalization());

        var cts = new CancellationTokenSource();
        var task = service.RestoreAsync(cts.Token);

        Assert.True(repo.InRestore.Wait(TimeSpan.FromSeconds(5)), "repo entered restore");
        Assert.True(repo.RestoreWasCalledWithToken);
        cts.Cancel();
        var result = await task;

        Assert.False(result.Success);
        Assert.False(lifecycle.ShutdownCalled);
    }

    [Fact]
    public async Task RestoreDatabaseCommand_WhenCancelledMidOperation_ReportsCancelledWithoutShutdown()
    {
        var repo = new BlockingFileIntegrityRepository();
        var lifecycle = new RecordingLifecycleService();
        var restorePath = Path.GetTempFileName();
        var dialog = new NoOpDialogService();
        var fileDialog = new StubFileDialogService(openPath: restorePath);
        var loc = new KeyLocalization();
        var service = new DatabaseBackupService(repo, fileDialog, dialog, lifecycle, loc);
        var model = new DashboardModel(
            null!, null!, null!, null!, null!,
            dialog, fileDialog, null!, null!, null!, null!, null!, service, loc);

        var commandTask = model.RestoreDatabaseCommand.ExecuteAsync(null);
        Assert.True(repo.InRestore.Wait(TimeSpan.FromSeconds(5)), "repo entered restore");

        model.CancelRestoreCommand.Execute(null);
        await commandTask;

        Assert.True(model.RestoreCancelled);
        Assert.False(model.IsRestoring);
        Assert.NotEqual(100, model.RestoreProgress);
        Assert.False(lifecycle.ShutdownCalled);
    }

    [Fact]
    public async Task BackupAsync_WhenCancelledMidOperation_DoesNotWriteDestinationFile()
    {
        var repo = new BlockingFileIntegrityRepository();
        var destPath = Path.Combine(Path.GetTempPath(), $"sdm_backup_{Guid.NewGuid():N}.db");
        var service = new DatabaseBackupService(
            repo,
            new StubFileDialogService(savePath: destPath),
            new NoOpDialogService(),
            new RecordingLifecycleService(),
            new KeyLocalization());

        var cts = new CancellationTokenSource();
        var task = service.BackupAsync(cts.Token);

        Assert.True(repo.InBackup.Wait(TimeSpan.FromSeconds(5)), "repo entered backup");
        cts.Cancel();
        var result = await task;

        Assert.False(result.Success);
        Assert.False(File.Exists(destPath));
    }

    [Fact]
    public async Task RestoreAsync_WhenCancelledInFinalizationWindow_DoesNotShutdown()
    {
        var repo = new StubFileIntegrityRepository();
        var lifecycle = new RecordingLifecycleService();
        var dialog = new BlockingFinalizationDialog();
        var restorePath = Path.GetTempFileName();
        var service = new DatabaseBackupService(
            repo,
            new StubFileDialogService(openPath: restorePath),
            dialog,
            lifecycle,
            new KeyLocalization());

        var cts = new CancellationTokenSource();
        var task = service.RestoreAsync(cts.Token);

        Assert.True(dialog.SuccessShown.Wait(TimeSpan.FromSeconds(5)), "success message reached the finalization window");
        cts.Cancel();
        dialog.Proceed.Set();
        var result = await task;

        Assert.False(result.Success, "success must not be reported when cancel arrives during finalization");
        Assert.False(lifecycle.ShutdownCalled, "shutdown must not occur when cancel arrives during finalization");
    }

    private sealed class BlockingFileIntegrityRepository : IFileIntegrityRepository
    {
        public ManualResetEventSlim InRestore { get; } = new(false);
        public ManualResetEventSlim InBackup { get; } = new(false);
        public bool RestoreWasCalledWithToken { get; private set; }

        public bool UpdateDocumentPath(int id, string newPath) => true;
        public bool ClearDocumentPath(int id) => true;
        public bool BackupDatabase(string destPath, bool overwrite) => false;
        public bool BackupDatabase(string destPath, bool overwrite, CancellationToken cancellationToken)
        {
            InBackup.Set();
            cancellationToken.WaitHandle.WaitOne(Timeout.Infinite);
            return false;
        }

        public bool CanRestoreDatabase(string sourcePath) => true;
        public bool RestoreDatabase(string sourcePath) => false;
        public bool RestoreDatabase(string sourcePath, CancellationToken cancellationToken)
        {
            RestoreWasCalledWithToken = true;
            InRestore.Set();
            cancellationToken.WaitHandle.WaitOne(Timeout.Infinite);
            return false;
        }

        public int GetDocumentCount() => 0;
        public string DatabasePath => "blocked.db";
    }

    private sealed class RecordingLifecycleService : IApplicationLifecycleService
    {
        public bool ShutdownCalled { get; private set; }
        public void Shutdown() => ShutdownCalled = true;
    }

    private sealed class StubFileIntegrityRepository : IFileIntegrityRepository
    {
        public bool UpdateDocumentPath(int id, string newPath) => true;
        public bool ClearDocumentPath(int id) => true;
        public bool BackupDatabase(string destPath, bool overwrite) => true;
        public bool BackupDatabase(string destPath, bool overwrite, CancellationToken cancellationToken) => true;
        public bool CanRestoreDatabase(string sourcePath) => true;
        public bool RestoreDatabase(string sourcePath) => true;
        public bool RestoreDatabase(string sourcePath, CancellationToken cancellationToken) => true;
        public int GetDocumentCount() => 0;
        public string DatabasePath => "stub.db";
    }

    private sealed class BlockingFinalizationDialog : IDialogService
    {
        public ManualResetEventSlim SuccessShown { get; } = new(false);
        public ManualResetEventSlim Proceed { get; } = new(false);
        public string? LastSuccessTitle { get; private set; }

        public Task ShowMessageAsync(string title, string message)
        {
            LastSuccessTitle = title;
            SuccessShown.Set();
            Proceed.Wait(Timeout.Infinite);
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
            => Task.FromResult(true);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "")
            => Task.FromResult<string?>(null);
    }

    private sealed class StubFileDialogService : IFileDialogService
    {
        private readonly string? _openPath;
        private readonly string? _savePath;

        public StubFileDialogService(string? openPath = null, string? savePath = null)
        {
            _openPath = openPath;
            _savePath = savePath;
        }

        public Task<string?> ShowOpenFileAsync(string title, string? filter = null)
            => Task.FromResult<string?>(_openPath);

        public Task<string?> ShowOpenFolderAsync(string title)
            => Task.FromResult<string?>(null);

        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null)
            => Task.FromResult<string?>(_savePath);
    }

    private sealed class NoOpDialogService : IDialogService
    {
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
            => Task.FromResult(true);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "")
            => Task.FromResult<string?>(null);
    }

    private sealed class KeyLocalization : ILocalizationService
    {
        public string this[string key] => key;
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [];
        public event EventHandler? LanguageChanged { add { } remove { } }
        public void SetLanguage(SupportedLanguage language) { }
    }
}

public sealed class BackupCancellationDatabaseTests : DatabaseTestBase
{
    [Fact]
    public void RestoreDatabase_WhenCancelledBeforeStart_DoesNotReplaceLiveDatabase()
    {
        var candidate = Path.Combine(Path.GetTempPath(), $"sdm_can_{Guid.NewGuid():N}.db");
        try
        {
            Repo.Add(new StudyDocument { Name = "Original" });
            Assert.True(Db.BackupDatabase(candidate));

            Repo.Add(new StudyDocument { Name = "NewAfterBackup" });
            Assert.Contains(Repo.GetAll(), d => d.Name == "NewAfterBackup");

            var result = Db.RestoreDatabase(candidate, new CancellationToken(canceled: true));

            Assert.False(result);
            var names = Repo.GetAll().Select(d => d.Name).ToList();
            Assert.Contains("Original", names);
            Assert.Contains("NewAfterBackup", names);
        }
        finally
        {
            if (File.Exists(candidate))
                File.Delete(candidate);
        }
    }

}

/// <summary>
/// Deterministic (no timing race) proof that the commit boundary is atomic:
/// the commit-abort decision is made at the single boundary seam, so a restore
/// or backup that reaches the commit point with abort requested never starts
/// File.Replace/Move and never deletes live sidecars.
/// </summary>
public sealed class BackupCancellationCommitBoundaryTests
{
    private sealed class CommitGateDatabaseHelper : DatabaseHelper
    {
        public bool AbortAtCommit { get; set; }
        public bool CommitGuardReached { get; private set; }

        protected override bool ShouldAbortOperation(CancellationToken cancellationToken)
        {
            CommitGuardReached = true;
            return AbortAtCommit || cancellationToken.IsCancellationRequested;
        }
    }

    private sealed class SidecarDeleteFailingHelper : DatabaseHelper
    {
        public bool SidecarDeleteReached { get; private set; }

        protected override void DeleteSqliteSidecars(string databasePath)
        {
            SidecarDeleteReached = true;
            throw new IOException("Simulated sidecar delete failure (locked -wal).");
        }
    }

    [Fact]
    public void RestoreDatabase_SidecarDeleteFailure_FailsClosedWithoutSwapping()
    {
        var db = new SidecarDeleteFailingHelper();
        var dbPath = Path.Combine(Path.GetTempPath(), $"sdm_sidecar_{Guid.NewGuid():N}.db");
        db.SetDatabasePath(dbPath);
        db.InitializeDatabase();
        var repo = new DocumentRepository(db);
        repo.Add(new StudyDocument { Name = "LiveBefore" });

        var candidate = Path.Combine(Path.GetTempPath(), $"sdm_can_{Guid.NewGuid():N}.db");
        try
        {
            Assert.True(db.BackupDatabase(candidate));
            // Diverge live from candidate so any swap would be detectable by file identity.
            repo.Add(new StudyDocument { Name = "LiveAfter" });
            var liveHashBefore = ComputeFileHash(dbPath);

            var result = db.RestoreDatabase(candidate, CancellationToken.None);

            Assert.False(result, "restore must fail closed when sidecars cannot be deleted");
            Assert.True(db.SidecarDeleteReached, "sidecar delete must be reached before any replace");

            // File identity: the live main database file must not have been replaced.
            db.CloseAllConnections();
            var liveHashAfter = ComputeFileHash(dbPath);
            Assert.Equal(liveHashBefore, liveHashAfter);

            // Sidecar safety / no swap: live keeps its own data and is never paired
            // with the candidate's content.
            var names = repo.GetAll().Select(d => d.Name).ToList();
            Assert.Contains("LiveBefore", names);
            Assert.Contains("LiveAfter", names);
            Assert.True(File.Exists(dbPath), "live database file must remain present and consistent");
        }
        finally
        {
            if (File.Exists(candidate))
                File.Delete(candidate);
            db.CloseAllConnections();
            Cleanup(dbPath);
        }
    }

    private static string ComputeFileHash(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToBase64String(sha.ComputeHash(stream));
    }

    [Fact]
    public void RestoreDatabase_AbortAtCommitBoundary_DoesNotSwapOrDeleteSidecars()
    {
        var db = new CommitGateDatabaseHelper();
        var dbPath = Path.Combine(Path.GetTempPath(), $"sdm_commit_{Guid.NewGuid():N}.db");
        db.SetDatabasePath(dbPath);
        db.InitializeDatabase();
        var repo = new DocumentRepository(db);
        repo.Add(new StudyDocument { Name = "BeforeCancel" });

        var candidate = Path.Combine(Path.GetTempPath(), $"sdm_can_{Guid.NewGuid():N}.db");
        var walConn = new SqliteConnection($"Data Source={dbPath}");
        walConn.Open();
        try
        {
            using (var setWal = walConn.CreateCommand())
            {
                setWal.CommandText = "PRAGMA journal_mode=WAL;";
                setWal.ExecuteNonQuery();
            }

            using (var dirty = walConn.CreateCommand())
            {
                dirty.CommandText = "INSERT INTO app_settings(key, value) VALUES('__wal_probe__', '1');";
                dirty.ExecuteNonQuery();
            }

            Assert.True(File.Exists(dbPath + "-wal"), "WAL sidecar should exist before restore");

            Assert.True(db.BackupDatabase(candidate));

            db.AbortAtCommit = true;
            var result = db.RestoreDatabase(candidate, CancellationToken.None);

            Assert.False(result);
            Assert.True(db.CommitGuardReached, "commit boundary must be reached");
            Assert.Contains(repo.GetAll(), d => d.Name == "BeforeCancel");
            Assert.True(File.Exists(dbPath + "-wal"), "WAL sidecar must not be deleted when commit is aborted");
        }
        finally
        {
            walConn.Dispose();
            db.CloseAllConnections();
            Cleanup(dbPath);
            if (File.Exists(candidate))
                File.Delete(candidate);
        }
    }

    [Fact]
    public void BackupDatabase_AbortAtCommitBoundary_DoesNotWriteDestination()
    {
        var db = new CommitGateDatabaseHelper();
        var dbPath = Path.Combine(Path.GetTempPath(), $"sdm_commit_{Guid.NewGuid():N}.db");
        db.SetDatabasePath(dbPath);
        db.InitializeDatabase();

        var destPath = Path.Combine(Path.GetTempPath(), $"sdm_dest_{Guid.NewGuid():N}.db");
        db.AbortAtCommit = true;
        var result = db.BackupDatabase(destPath, true, CancellationToken.None);

        Assert.False(result);
        Assert.True(db.CommitGuardReached, "commit boundary must be reached");
        Assert.False(File.Exists(destPath), "destination must not be written when commit is aborted");
        db.CloseAllConnections();
        Cleanup(dbPath);
    }

    private static void Cleanup(string dbPath)
    {
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            try { if (File.Exists(dbPath + suffix)) File.Delete(dbPath + suffix); }
            catch { }
        }
    }
}
