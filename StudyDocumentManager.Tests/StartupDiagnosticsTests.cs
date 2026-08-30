using Microsoft.Data.Sqlite;
using Xunit;
using StudyDocumentManager;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Tests;

public sealed class StartupDiagnosticsTests
{
    [Fact]
    public void FileStartupDiagnostics_AppendsOnlySafeEventFields()
    {
        var directory = CreateTempDirectory();
        var logPath = Path.Combine(directory, "startup.log");
        var diagnostics = new FileStartupDiagnostics(logPath);

        diagnostics.RecordDatabaseInitializationSucceeded();
        diagnostics.RecordDatabaseInitializationFailed(new InvalidOperationException("secret message; /raw/path; SELECT * FROM documents"));

        var lines = File.ReadAllLines(logPath);
        Assert.Equal(2, lines.Length);
        Assert.Contains("event=database_initialization_succeeded stage=database_initialization severity=information", lines[0]);
        Assert.Contains("event=database_initialization_failed stage=database_initialization severity=error diagnostic_category=unexpected_database_initialization_failure exception_type=InvalidOperationException", lines[1]);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z ", lines[0]);
        Assert.DoesNotContain("secret message", lines[1]);
        Assert.DoesNotContain("/raw/path", lines[1]);
        Assert.DoesNotContain("SELECT", lines[1]);

        DeleteDirectory(directory);
    }

    [Fact]
    public void FileStartupDiagnostics_SwallowsSinkIoFailures()
    {
        var diagnostics = new FileStartupDiagnostics("bad\0path");
        var originalError = Console.Error;
        using var error = new StringWriter();
        Console.SetError(error);
        try
        {
            diagnostics.RecordDatabaseInitializationSucceeded();
            diagnostics.RecordDatabaseInitializationFailed(new InvalidOperationException("secret"));

            Assert.Equal("Startup diagnostics unavailable." + Environment.NewLine + "Startup diagnostics unavailable." + Environment.NewLine, error.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void DatabaseHelper_NotifiesDiagnosticsOnSuccessAndFailure()
    {
        var diagnostics = new RecordingDiagnostics();
        var directory = CreateTempDirectory();
        var database = new DatabaseHelper(diagnostics);
        database.SetDatabasePath(Path.Combine(directory, "database.db"));

        database.InitializeDatabase();

        Assert.Equal(1, diagnostics.SuccessCount);
        Assert.Empty(diagnostics.Failures);
        database.CloseAllConnections();
        DeleteDirectory(directory);

        var failingDirectory = CreateTempDirectory();
        var blockingPath = Path.Combine(failingDirectory, "blocked");
        File.WriteAllText(blockingPath, "not a directory");
        var failingDatabase = new DatabaseHelper(diagnostics);
        failingDatabase.SetDatabasePath(Path.Combine(blockingPath, "database.db"));

        Assert.ThrowsAny<Exception>(() => failingDatabase.InitializeDatabase());
        Assert.Single(diagnostics.Failures);
        DeleteDirectory(failingDirectory);
    }

    [Fact]
    public void DatabaseHelper_DiagnosticFailure_DoesNotMaskOriginalDatabaseException()
    {
        var directory = CreateTempDirectory();
        var blockingPath = Path.Combine(directory, "blocked");
        File.WriteAllText(blockingPath, "not a directory");
        var expectedPath = Path.Combine(blockingPath, "database.db");
        var database = new DatabaseHelper(new ThrowingDiagnostics());
        database.SetDatabasePath(expectedPath);

        var exception = Assert.ThrowsAny<Exception>(() => database.InitializeDatabase());

        Assert.IsType<IOException>(exception);
        DeleteDirectory(directory);
    }

    [Theory]
    [InlineData("Incomplete legacy database tables: tai_lieu.", "incomplete_legacy_schema")]
    [InlineData("Unsupported database tables: secret.", "unsupported_legacy_tables")]
    [InlineData("Unsupported legacy table 'secret'.", "unsupported_legacy_tables")]
    [InlineData("Unsupported columns in 'tai_lieu': secret.", "unsupported_legacy_columns")]
    [InlineData("Missing unique constraint in 'recent_files'.", "incomplete_legacy_schema")]
    [InlineData("Unsupported foreign key layout in 'recent_files'.", "unsupported_legacy_schema")]
    [InlineData("Unsupported index 'idx_secret' on 'documents'.", "unsupported_legacy_schema")]
    [InlineData("Unsupported trigger on 'documents'.", "unsupported_legacy_schema")]
    [InlineData("Required table 'documents' is missing.", "incomplete_legacy_schema")]
    [InlineData("Unsupported unique constraint 'sqlite_autoindex_documents_1' on 'documents'.", "unsupported_legacy_schema")]
    [InlineData("Table 'collection_items' references missing parent table 'collections'.", "incomplete_legacy_schema")]
    [InlineData("Orphaned records found in 'collection_items'.", "incomplete_legacy_schema")]
    [InlineData("Foreign key integrity check failed.", "incomplete_legacy_schema")]
    [InlineData("Unsupported legacy database schema: secret.", "unsupported_legacy_schema")]
    [InlineData("SDM_DATABASE_PATH must be an absolute path.", "invalid_database_path")]
    [InlineData("Unexpected migration failure: secret.", "unexpected_database_initialization_failure")]
    public void ClassifyFailure_UsesSafeFixedCategories(string message, string expectedCategory)
    {
        Assert.Equal(expectedCategory, FileStartupDiagnostics.ClassifyFailure(new InvalidOperationException(message)));
    }

    [Fact]
    public void DatabaseHelper_MigrationFailureLogsSafeCategoryAndRethrowsOriginalException()
    {
        var directory = CreateTempDirectory();
        var databasePath = Path.Combine(directory, "partial.db");
        var logPath = Path.Combine(directory, "startup.log");
        using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE tai_lieu (id INTEGER PRIMARY KEY, ten TEXT NOT NULL)";
            command.ExecuteNonQuery();
        }

        var database = new DatabaseHelper(new FileStartupDiagnostics(logPath));
        database.SetDatabasePath(databasePath);
        var exception = Assert.Throws<InvalidOperationException>(() => database.InitializeDatabase());

        var log = File.ReadAllText(logPath);
        Assert.Contains("diagnostic_category=incomplete_legacy_schema", log);
        Assert.DoesNotContain(exception.Message, log);
        Assert.DoesNotContain(databasePath, log);
        database.CloseAllConnections();
        DeleteDirectory(directory);
    }

    [Fact]
    public void StartupDiagnosticsPath_InvalidConfigurationFallsBackToLocalAppDataLogsWithoutThrowing()
    {
        var original = Environment.GetEnvironmentVariable(FileStartupDiagnostics.EnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(FileStartupDiagnostics.EnvironmentVariableName, "relative/startup.log");

            var path = App.GetStartupDiagnosticsPath();

            Assert.True(Path.IsPathFullyQualified(path));
            Assert.EndsWith(Path.Combine("StudyDocumentManager", "logs", "startup.log"), path, StringComparison.OrdinalIgnoreCase);
            _ = new FileStartupDiagnostics(path);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FileStartupDiagnostics.EnvironmentVariableName, original);
        }
    }

    [Fact]
    public void StartupDiagnosticsPath_DefaultUsesLocalAppDataLogs()
    {
        var original = Environment.GetEnvironmentVariable(FileStartupDiagnostics.EnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(FileStartupDiagnostics.EnvironmentVariableName, null);

            var path = App.GetStartupDiagnosticsPath();

            Assert.EndsWith(Path.Combine("StudyDocumentManager", "logs", "startup.log"), path, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FileStartupDiagnostics.EnvironmentVariableName, original);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sdm_startup_diagnostics_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private sealed class RecordingDiagnostics : IStartupDiagnostics
    {
        public int SuccessCount { get; private set; }
        public List<Exception> Failures { get; } = [];

        public void RecordDatabaseInitializationSucceeded() => SuccessCount++;

        public void RecordDatabaseInitializationFailed(Exception exception) => Failures.Add(exception);
    }

    private sealed class ThrowingDiagnostics : IStartupDiagnostics
    {
        public void RecordDatabaseInitializationSucceeded() => throw new InvalidOperationException("diagnostic failure");

        public void RecordDatabaseInitializationFailed(Exception exception) => throw new InvalidOperationException("diagnostic failure");
    }
}
