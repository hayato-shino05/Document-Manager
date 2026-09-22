using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;

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
    protected readonly DatabaseHelper Db;
    protected readonly DocumentRepository Repo;

    protected DatabaseTestBase()
    {
        DbPath = Path.Combine(Path.GetTempPath(), $"sdm_test_{Guid.NewGuid():N}.db");
        Db = new DatabaseHelper();
        Db.SetDatabasePath(DbPath);
        Db.InitializeDatabase();
        Repo = new DocumentRepository(Db);
    }

    public void Dispose()
    {
        Db.CloseAllConnections();
        try { if (File.Exists(DbPath)) File.Delete(DbPath); }
        catch { /* ignore cleanup errors */ }
    }
}
