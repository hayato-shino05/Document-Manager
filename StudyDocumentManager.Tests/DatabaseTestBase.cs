using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Tests;

/// <summary>
/// Base class for all database-backed tests.
/// Each test CLASS instance gets its own isolated SQLite temp file.
/// The repository is initialized AFTER SetDatabasePath() — use the Repo property in subclasses.
/// 
/// IMPORTANT: Do NOT use field initializers for DocumentRepository in subclasses.
/// Instead, assign in the subclass constructor AFTER calling base().
/// </summary>
public abstract class DatabaseTestBase : IDisposable
{
    protected readonly string DbPath;

    protected DatabaseTestBase()
    {
        // Give each test class instance its own unique temp DB
        DbPath = Path.Combine(Path.GetTempPath(), $"sdm_test_{Guid.NewGuid():N}.db");
        DatabaseHelper.SetDatabasePath(DbPath);
        DatabaseHelper.InitializeDatabase();
    }

    public void Dispose()
    {
        // Close all connections before deleting
        DatabaseHelper.CloseAllConnections();
        try { if (File.Exists(DbPath)) File.Delete(DbPath); }
        catch { /* ignore cleanup errors */ }
    }
}
