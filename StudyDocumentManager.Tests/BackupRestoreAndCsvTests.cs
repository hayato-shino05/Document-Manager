using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public class BackupRestoreIntegrityTests : DatabaseTestBase
{
    [Fact]
    public void BackupDatabase_CreatesOnlineSnapshotBeforeLaterMutation()
    {
        Repo.Add(new StudyDocument { Name = "Before backup" });
        var backupPath = CreateTemporaryPath("backup.db");

        try
        {
            Assert.True(Db.BackupDatabase(backupPath));

            Repo.Add(new StudyDocument { Name = "After backup" });

            using var connection = new SqliteConnection($"Data Source={backupPath};Mode=ReadOnly;Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM documents WHERE is_deleted = 0 ORDER BY id";
            using var reader = command.ExecuteReader();
            var names = new List<string>();
            while (reader.Read())
                names.Add(reader.GetString(0));

            Assert.Equal(["Before backup"], names);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteFile(backupPath);
        }
    }

    [Fact]
    public void RestoreDatabase_ReplacesLiveDataWithValidatedSnapshot()
    {
        Repo.Add(new StudyDocument { Name = "Snapshot" });
        var backupPath = CreateTemporaryPath("restore.db");

        try
        {
            Assert.True(Db.BackupDatabase(backupPath));
            Repo.Add(new StudyDocument { Name = "Live mutation" });

            Assert.True(Db.RestoreDatabase(backupPath));

            Assert.Equal(["Snapshot"], Repo.GetAll().Select(document => document.Name));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteFile(backupPath);
        }
    }

    [Fact]
    public void RestoreDatabase_InvalidCandidatesPreserveLiveData()
    {
        Repo.Add(new StudyDocument { Name = "Live data" });
        var missingPath = CreateTemporaryPath("missing.db");
        var randomPath = CreateTemporaryPath("random.db");
        var unrelatedPath = CreateTemporaryPath("unrelated.db");
        File.WriteAllText(randomPath, "not a database");

        try
        {
            using (var connection = new SqliteConnection($"Data Source={unrelatedPath};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE unrelated (id INTEGER PRIMARY KEY)";
                command.ExecuteNonQuery();
            }

            Assert.False(Db.RestoreDatabase(missingPath));
            Assert.False(Db.RestoreDatabase(randomPath));
            Assert.False(Db.RestoreDatabase(unrelatedPath));
            Assert.False(Db.RestoreDatabase(DbPath));
            Assert.Equal(["Live data"], Repo.GetAll().Select(document => document.Name));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteFile(randomPath);
            DeleteFile(unrelatedPath);
        }
    }


    [Fact]
    public void RestoreDatabase_UnsupportedSchemaPreservesLiveData()
    {
        Repo.Add(new StudyDocument { Name = "Live data" });
        var candidatePath = CreateTemporaryPath("unsupported.db");

        try
        {
            Assert.True(Db.BackupDatabase(candidatePath));
            using (var connection = new SqliteConnection($"Data Source={candidatePath};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE documents ADD COLUMN unexpected TEXT";
                command.ExecuteNonQuery();
            }

            Assert.False(Db.RestoreDatabase(candidatePath));
            Assert.Equal(["Live data"], Repo.GetAll().Select(document => document.Name));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteFile(candidatePath);
        }
    }


    [Fact]
    public void RestoreDatabase_MissingCascadeConstraintPreservesLiveData()
    {
        Repo.Add(new StudyDocument { Name = "Live data" });
        var candidatePath = CreateTemporaryPath("missing-cascade.db");

        try
        {
            Assert.True(Db.BackupDatabase(candidatePath));
            using (var connection = new SqliteConnection($"Data Source={candidatePath};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "DROP TABLE recent_files; CREATE TABLE recent_files (id INTEGER PRIMARY KEY AUTOINCREMENT, document_id INTEGER NOT NULL UNIQUE, opened_at DATETIME)";
                command.ExecuteNonQuery();
            }

            Assert.False(Db.RestoreDatabase(candidatePath));
            Assert.Equal(["Live data"], Repo.GetAll().Select(document => document.Name));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteFile(candidatePath);
        }
    }


    [Fact]
    public void RestoreDatabase_CustomTriggerPreservesLiveData()
    {
        Repo.Add(new StudyDocument { Name = "Live data" });
        var candidatePath = CreateTemporaryPath("trigger.db");

        try
        {
            Assert.True(Db.BackupDatabase(candidatePath));
            using (var connection = new SqliteConnection($"Data Source={candidatePath};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "CREATE TRIGGER documents_probe AFTER INSERT ON documents BEGIN SELECT 1; END";
                command.ExecuteNonQuery();
            }

            Assert.False(Db.RestoreDatabase(candidatePath));
            Assert.Equal(["Live data"], Repo.GetAll().Select(document => document.Name));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteFile(candidatePath);
        }
    }


    [Fact]
    public void RestoreDatabase_InvalidApplicationRowPreservesLiveData()
    {
        Repo.Add(new StudyDocument { Name = "Live data" });
        var candidatePath = CreateTemporaryPath("invalid-date.db");

        try
        {
            Assert.True(Db.BackupDatabase(candidatePath));
            using (var connection = new SqliteConnection($"Data Source={candidatePath};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE documents SET created_at = 'not-a-date'";
                command.ExecuteNonQuery();
            }

            Assert.False(Db.RestoreDatabase(candidatePath));
            Assert.Equal(["Live data"], Repo.GetAll().Select(document => document.Name));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteFile(candidatePath);
        }
    }


    [Fact]
    public void RestoreDatabase_InvalidCollectionAndRecentTimestampsPreserveLiveData()
    {
        Repo.Add(new StudyDocument { Name = "Live data" });
        var document = Assert.Single(Repo.GetAll());
        Db.CreateCollection("Collection");
        Db.AddRecentFile(document.Id);
        var candidatePath = CreateTemporaryPath("invalid-timestamp.db");

        try
        {
            Assert.True(Db.BackupDatabase(candidatePath));
            using (var connection = new SqliteConnection($"Data Source={candidatePath};Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE collections SET created_at = 'not-a-date'; UPDATE recent_files SET opened_at = 'not-a-date'";
                command.ExecuteNonQuery();
            }

            Assert.False(Db.RestoreDatabase(candidatePath));
            Assert.Equal(["Live data"], Repo.GetAll().Select(document => document.Name));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteFile(candidatePath);
        }
    }

    [Fact]
    public async Task RestoreDatabase_WaitsForDatabaseOperationLock()
    {
        Repo.Add(new StudyDocument { Name = "Snapshot" });
        var backupPath = CreateTemporaryPath("lock.db");

        try
        {
            Assert.True(Db.BackupDatabase(backupPath));
            var method = typeof(DatabaseHelper).GetMethod("GetOperationMutexName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(method);
            var mutexName = Assert.IsType<string>(method.Invoke(null, [DbPath]));
            var lockAcquired = new TaskCompletionSource();
            var releaseLock = new TaskCompletionSource();

            var lockHolder = Task.Run(() =>
            {
                using var mutex = new Mutex(false, mutexName);
                mutex.WaitOne();
                lockAcquired.SetResult();
                releaseLock.Task.GetAwaiter().GetResult();
                mutex.ReleaseMutex();
            });

            await lockAcquired.Task;
            var restoreTask = Task.Run(() => Db.RestoreDatabase(backupPath));
            await Task.Delay(200);
            Assert.False(restoreTask.IsCompleted);
            releaseLock.SetResult();

            await lockHolder;
            Assert.True(await restoreTask);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteFile(backupPath);
        }
    }

    [Fact]
    public async Task InsertDocument_WaitsForDatabaseOperationLock()
    {
        var method = typeof(DatabaseHelper).GetMethod("GetOperationMutexName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        var mutexName = Assert.IsType<string>(method.Invoke(null, [DbPath]));
        var lockAcquired = new TaskCompletionSource();
        var releaseLock = new TaskCompletionSource();

        var lockHolder = Task.Run(() =>
        {
            using var mutex = new Mutex(false, mutexName);
            mutex.WaitOne();
            lockAcquired.SetResult();
            releaseLock.Task.GetAwaiter().GetResult();
            mutex.ReleaseMutex();
        });

        await lockAcquired.Task;
        var secondHelper = new DatabaseHelper();
        secondHelper.SetDatabasePath(DbPath);
        var secondRepository = new DocumentRepository(secondHelper);
        var insertTask = Task.Run(() => secondRepository.Add(new StudyDocument { Name = "Concurrent" }));

        await Task.Delay(200);
        Assert.False(insertTask.IsCompleted);
        releaseLock.SetResult();

        await lockHolder;
        Assert.True(await insertTask);
    }


    [Fact]
    public async Task InitializeDatabase_WaitsForDatabaseOperationLock()
    {
        var method = typeof(DatabaseHelper).GetMethod("GetOperationMutexName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        var mutexName = Assert.IsType<string>(method.Invoke(null, [DbPath]));
        var lockAcquired = new TaskCompletionSource();
        var releaseLock = new TaskCompletionSource();

        var lockHolder = Task.Run(() =>
        {
            using var mutex = new Mutex(false, mutexName);
            mutex.WaitOne();
            lockAcquired.SetResult();
            releaseLock.Task.GetAwaiter().GetResult();
            mutex.ReleaseMutex();
        });

        await lockAcquired.Task;
        var secondHelper = new DatabaseHelper();
        secondHelper.SetDatabasePath(DbPath);
        var initializeTask = Task.Run(secondHelper.InitializeDatabase);

        await Task.Delay(200);
        Assert.False(initializeTask.IsCompleted);
        releaseLock.SetResult();

        await lockHolder;
        await initializeTask;
    }


    [Fact]
    public async Task OppositeDirectionRestores_CompleteWithoutLockInversion()
    {
        var secondaryPath = CreateTemporaryPath("secondary.db");
        var secondary = new DatabaseHelper();
        secondary.SetDatabasePath(secondaryPath);
        secondary.InitializeDatabase();
        var secondaryRepository = new DocumentRepository(secondary);
        Repo.Add(new StudyDocument { Name = "Primary" });
        secondaryRepository.Add(new StudyDocument { Name = "Secondary" });

        try
        {
            var restores = Task.WhenAll(
                Task.Run(() => Db.RestoreDatabase(secondaryPath)),
                Task.Run(() => secondary.RestoreDatabase(DbPath)));

            var completed = await Task.WhenAny(restores, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(restores, completed);
            await restores;
        }
        finally
        {
            secondary.CloseAllConnections();
            SqliteConnection.ClearAllPools();
            DeleteFile(secondaryPath);
        }
    }

    [Fact]
    public void BackupDatabase_RefusesUnconfirmedOverwriteAndPreservesExistingFile()
    {
        Repo.Add(new StudyDocument { Name = "Source" });
        var destinationPath = CreateTemporaryPath("existing.db");
        File.WriteAllText(destinationPath, "existing backup");

        try
        {
            Assert.False(Db.BackupDatabase(destinationPath, overwrite: false));
            Assert.Equal("existing backup", File.ReadAllText(destinationPath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteFile(destinationPath);
        }
    }

    private static string CreateTemporaryPath(string suffix)
        => Path.Combine(Path.GetTempPath(), $"sdm_{Guid.NewGuid():N}_{suffix}");

    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}

public class CsvExportServiceTests
{
    [Fact]
    public async Task ExportCsvAsync_WritesInvariantEscapedUtf8Rows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sdm_{Guid.NewGuid():N}.csv");
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = new CultureInfo("vi-VN");
        CultureInfo.CurrentUICulture = new CultureInfo("vi-VN");

        try
        {
            var service = new CsvExportService(new FileDialogStub(path), new LocalizationStub());
            var document = new StudyDocument
            {
                Id = 7,
                Name = "=SUM(1,2)",
                Subject = "+Subject",
                Type = "-Type",
                FilePath = "C:/notes,final.pdf",
                Author = "Ada \"Lovelace\"",
                Tags = "@formula",
                IsImportant = true,
                FileSize = 3.5,
                CreatedAt = new DateTime(2026, 7, 29),
                Deadline = new DateTime(2026, 8, 1),
                Notes = "@first\r\nsecond"
            };

            var result = await service.ExportCsvAsync([document], "documents.csv");
            var bytes = File.ReadAllBytes(path);
            var csv = File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            Assert.True(result.Success);
            Assert.Equal(path, result.FilePath);
            Assert.Equal(1, result.Count);
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
            Assert.StartsWith("ID,Name,Subject,Type,FilePath,Author,Tags,IsImportant,FileSize (MB),CreatedAt,Deadline,Notes", csv);
            Assert.Contains("7,\"'=SUM(1,2)\",'+Subject,'-Type,\"C:/notes,final.pdf\",\"Ada \"\"Lovelace\"\"\",'@formula,Yes,3.50,2026-07-29,2026-08-01,\"'@first\r\nsecond\"", csv);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportCsvAsync_CancelAndWriteFailureDoNotExposeProviderDetails()
    {
        var localization = new LocalizationStub();
        var cancelled = new CsvExportService(new FileDialogStub(null), localization);
        var failed = new CsvExportService(new FileDialogStub(@"Z:\does-not-exist\documents.csv"), localization);

        var cancelledResult = await cancelled.ExportCsvAsync([], null);
        var failedResult = await failed.ExportCsvAsync([], null);

        Assert.False(cancelledResult.Success);
        Assert.Null(cancelledResult.Error);
        Assert.False(failedResult.Success);
        Assert.Equal("Dashboard_ExportWriteFailed", failedResult.Error);
    }

    private sealed class FileDialogStub(string? savePath) : IFileDialogService
    {
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult(savePath);
    }

    private sealed class LocalizationStub : ILocalizationService
    {
        public string this[string key] => key switch
        {
            "Dashboard_CsvYes" => "Yes",
            "Dashboard_CsvNo" => "No",
            _ => key
        };

        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public void SetLanguage(SupportedLanguage language) { }
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [];
        public event EventHandler? LanguageChanged { add { } remove { } }
    }
}


public class DatabaseBackupServiceTests
{
    [Fact]
    public async Task RestoreAsync_ConfirmedValidatedBackup_ShowsRestartMessageThenShutsDown()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sdm_{Guid.NewGuid():N}.db");
        File.WriteAllText(path, "backup");
        var events = new List<string>();
        var repository = new FileIntegrityRepositoryStub { RestoreResult = true };
        var dialog = new DialogStub(events) { ConfirmResult = true };
        var lifecycle = new LifecycleStub(events);
        var service = new DatabaseBackupService(
            repository,
            new BackupFileDialogStub(path),
            dialog,
            lifecycle,
            new BackupLocalizationStub());

        try
        {
            var result = await service.RestoreAsync();

            Assert.True(result.Success);
            Assert.True(repository.RestoreCalled);
            Assert.Equal(["message", "shutdown"], events);
            Assert.Equal("Dashboard_RestoreRestartRequired", dialog.LastMessage);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task RestoreAsync_CancelledConfirmation_DoesNotRestoreOrShutdown()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sdm_{Guid.NewGuid():N}.db");
        File.WriteAllText(path, "backup");
        var repository = new FileIntegrityRepositoryStub();
        var lifecycle = new LifecycleStub([]);
        var service = new DatabaseBackupService(
            repository,
            new BackupFileDialogStub(path),
            new DialogStub([]),
            lifecycle,
            new BackupLocalizationStub());

        try
        {
            var result = await service.RestoreAsync();

            Assert.False(result.Success);
            Assert.Null(result.Error);
            Assert.False(repository.RestoreCalled);
            Assert.False(lifecycle.ShutdownCalled);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }


    [Fact]
    public async Task RestoreAsync_RepositoryFailure_DoesNotShutdown()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sdm_{Guid.NewGuid():N}.db");
        File.WriteAllText(path, "backup");
        var repository = new FileIntegrityRepositoryStub { RestoreResult = false };
        var lifecycle = new LifecycleStub([]);
        var service = new DatabaseBackupService(
            repository,
            new BackupFileDialogStub(path),
            new DialogStub([]) { ConfirmResult = true },
            lifecycle,
            new BackupLocalizationStub());

        try
        {
            var result = await service.RestoreAsync();

            Assert.False(result.Success);
            Assert.Equal("Dashboard_RestoreFailed", result.Error);
            Assert.True(repository.RestoreCalled);
            Assert.False(lifecycle.ShutdownCalled);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }


    [Fact]
    public async Task RestoreAsync_InvalidCandidate_DoesNotAskConfirmation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sdm_{Guid.NewGuid():N}.db");
        File.WriteAllText(path, "backup");
        var repository = new FileIntegrityRepositoryStub { CanRestoreResult = false };
        var dialog = new DialogStub([]) { ConfirmResult = true };
        var lifecycle = new LifecycleStub([]);
        var service = new DatabaseBackupService(
            repository,
            new BackupFileDialogStub(path),
            dialog,
            lifecycle,
            new BackupLocalizationStub());

        try
        {
            var result = await service.RestoreAsync();

            Assert.False(result.Success);
            Assert.Equal("Dashboard_RestoreFailed", result.Error);
            Assert.False(dialog.ConfirmCalled);
            Assert.False(repository.RestoreCalled);
            Assert.False(lifecycle.ShutdownCalled);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private sealed class FileIntegrityRepositoryStub : IFileIntegrityRepository
    {
        public bool RestoreResult { get; set; }
        public bool CanRestoreResult { get; set; } = true;
        public bool RestoreCalled { get; private set; }
        public bool CanRestoreDatabase(string sourcePath) => CanRestoreResult;
        public string DatabasePath => string.Empty;
        public bool UpdateDocumentPath(int id, string newPath) => false;
        public bool ClearDocumentPath(int id) => false;
        public bool BackupDatabase(string destPath, bool overwrite) => false;
        public bool RestoreDatabase(string sourcePath)
        {
            RestoreCalled = true;
            return RestoreResult;
        }
    }

    private sealed class BackupFileDialogStub(string path) : IFileDialogService
    {
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult<string?>(path);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
    }

    private sealed class DialogStub(List<string> events) : IDialogService
    {
        public bool ConfirmResult { get; set; }
        public bool ConfirmCalled { get; private set; }
        public string? LastMessage { get; private set; }
        public Task ShowMessageAsync(string title, string message)
        {
            LastMessage = message;
            events.Add("message");
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message)
        {
            ConfirmCalled = true;
            return Task.FromResult(ConfirmResult);
        }

        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
        {
            ConfirmCalled = true;
            return Task.FromResult(ConfirmResult);
        }
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
    }

    private sealed class LifecycleStub(List<string> events) : IApplicationLifecycleService
    {
        public bool ShutdownCalled { get; private set; }
        public void Shutdown()
        {
            ShutdownCalled = true;
            events.Add("shutdown");
        }
    }

    private sealed class BackupLocalizationStub : ILocalizationService
    {
        public string this[string key] => key;
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public void SetLanguage(SupportedLanguage language) { }
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [];
        public event EventHandler? LanguageChanged { add { } remove { } }
    }
}
