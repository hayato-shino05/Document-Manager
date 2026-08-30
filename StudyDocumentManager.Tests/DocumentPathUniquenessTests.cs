using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class DocumentPathUniquenessTests : DatabaseTestBase
{
    [Fact]
    public void InsertAndUpdate_EnforceExactNonEmptyPathAcrossActiveAndDeletedDocuments()
    {
        const string path = "C:/Study/Document.pdf";
        Assert.True(Db.InsertDocumentWithCatalogs(new StudyDocument { Name = "First", FilePath = path }));
        var first = Assert.Single(Repo.GetAll());
        Assert.True(Db.DeleteDocument(first.Id));

        var duplicate = new StudyDocument { Name = "Duplicate", FilePath = path };
        var insertException = Assert.Throws<SqliteException>(() => Db.InsertDocumentWithCatalogs(duplicate));
        Assert.Equal(2067, insertException.SqliteExtendedErrorCode);

        Assert.True(Db.InsertDocumentWithCatalogs(new StudyDocument { Name = "Second", FilePath = "C:/Study/Second.pdf" }));
        var second = Assert.Single(Repo.GetAll());
        second.FilePath = path;
        var updateException = Assert.Throws<SqliteException>(() => Db.UpdateDocument(second));
        Assert.Equal(2067, updateException.SqliteExtendedErrorCode);
        Assert.Equal("C:/Study/Second.pdf", Db.GetDocumentById(second.Id)!.FilePath);

        Assert.True(Db.RestoreDocument(first.Id));
        Assert.Equal(path, Db.GetDocumentById(first.Id)!.FilePath);
    }

    [Fact]
    public void InsertDocument_AllowsNullEmptyAndBinaryDistinctPaths()
    {
        Assert.True(Db.InsertDocument(new StudyDocument { Name = "Null 1", FilePath = null! }));
        Assert.True(Db.InsertDocument(new StudyDocument { Name = "Null 2", FilePath = null! }));
        Assert.True(Db.InsertDocument(new StudyDocument { Name = "Empty 1", FilePath = string.Empty }));
        Assert.True(Db.InsertDocument(new StudyDocument { Name = "Empty 2", FilePath = string.Empty }));
        Assert.True(Db.InsertDocument(new StudyDocument { Name = "Upper", FilePath = "C:/Study/Case.pdf" }));
        Assert.True(Db.InsertDocument(new StudyDocument { Name = "Lower", FilePath = "c:/study/case.pdf" }));

        Assert.Equal(6, Repo.GetAll().Count);
    }

    [Fact]
    public void DroppedFileImportService_ReturnsDuplicateOutcomeForDeletedPath()
    {
        const string path = "C:/Study/Deleted.pdf";
        Assert.True(Db.InsertDocumentWithCatalogs(new StudyDocument { Name = "Deleted", FilePath = path }));
        var document = Assert.Single(Repo.GetAll());
        Assert.True(Db.DeleteDocument(document.Id));
        var service = new DroppedFileImportService(Repo);

        var outcome = service.SaveDocument(new StudyDocument { Name = "Duplicate", FilePath = path });

        Assert.Equal(DocumentImportOutcome.SkippedDuplicate, outcome);
    }

    [Fact]
    public void BackupDatabase_RejectsMalformedNamedPathIndex()
    {
        using (var connection = new SqliteConnection(Db.ConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DROP INDEX idx_documents_file_path_unique; CREATE INDEX idx_documents_file_path_unique ON documents(file_path)";
            command.ExecuteNonQuery();
        }

        var destination = Path.Combine(Path.GetTempPath(), $"sdm_bad_index_{Guid.NewGuid():N}.db");
        try
        {
            Assert.False(Db.BackupDatabase(destination));
            Assert.False(File.Exists(destination));
        }
        finally
        {
            if (File.Exists(destination))
                File.Delete(destination);
        }
    }

    [Fact]
    public void RestoreDatabase_MigratesLegacyVersion3BackupWithoutPathIndex()
    {
        var backupPath = Path.Combine(Path.GetTempPath(), $"sdm_legacy_v3_{Guid.NewGuid():N}.db");
        try
        {
            Assert.True(Db.BackupDatabase(backupPath));
            ExecuteNonQuery(backupPath, "DROP INDEX idx_documents_file_path_unique");

            Assert.True(Db.CanRestoreDatabase(backupPath));
            Assert.True(Db.RestoreDatabase(backupPath));

            using var connection = new SqliteConnection(Db.ConnectionString);
            connection.Open();
            Assert.Equal(
                "CREATE UNIQUE INDEX idx_documents_file_path_unique ON documents(file_path COLLATE BINARY) WHERE file_path IS NOT NULL AND file_path <> ''",
                Scalar(connection, "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = 'idx_documents_file_path_unique'"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
    }

    [Fact]
    public void RestoreDatabase_MigratesDocumentPathNoCaseAutoIndex()
    {
        var backupPath = Path.Combine(Path.GetTempPath(), $"sdm_nocase_autoindex_{Guid.NewGuid():N}.db");
        try
        {
            Assert.True(Db.BackupDatabase(backupPath));
            RebuildDocumentsWithNoCaseUniquePath(backupPath);

            Assert.True(Db.CanRestoreDatabase(backupPath));
            Assert.True(Db.RestoreDatabase(backupPath));

            using var connection = new SqliteConnection(Db.ConnectionString);
            connection.Open();
            Assert.Equal(
                "CREATE UNIQUE INDEX idx_documents_file_path_unique ON documents(file_path COLLATE BINARY) WHERE file_path IS NOT NULL AND file_path <> ''",
                Scalar(connection, "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = 'idx_documents_file_path_unique'"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
    }

    [Fact]
    public void RestoreDatabase_MigratesNoCaseAutoIndexAndPreservesCurrentDocumentContract()
    {
        var backupPath = Path.Combine(Path.GetTempPath(), $"sdm_nocase_contract_{Guid.NewGuid():N}.db");
        try
        {
            Assert.True(Db.InsertDocumentWithCatalogs(new StudyDocument { Name = "Legacy source", FilePath = "C:/same.pdf" }));
            Assert.True(Db.BackupDatabase(backupPath));
            RebuildDocumentsWithNoCaseUniquePath(backupPath);

            Assert.True(Db.CanRestoreDatabase(backupPath));
            Assert.True(Db.RestoreDatabase(backupPath));

            using var connection = new SqliteConnection(Db.ConnectionString);
            connection.Open();
            Assert.Equal(1L, Convert.ToInt64(Scalar(connection, "SELECT COUNT(*) FROM documents")));
            Assert.Equal("Legacy source", Scalar(connection, "SELECT name FROM documents WHERE id = 1"));
            Assert.Equal("unread", Scalar(connection, "SELECT status FROM documents WHERE id = 1"));
            Assert.NotEqual(string.Empty, Scalar(connection, "SELECT archive_export_key FROM documents WHERE id = 1"));
            Assert.Equal(
                "CREATE UNIQUE INDEX idx_documents_file_path_unique ON documents(file_path COLLATE BINARY) WHERE file_path IS NOT NULL AND file_path <> ''",
                Scalar(connection, "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = 'idx_documents_file_path_unique'"));
            Assert.Equal(0L, Convert.ToInt64(Scalar(connection, "SELECT COUNT(*) FROM pragma_index_list('documents') AS idx JOIN pragma_index_info(idx.name) AS col WHERE idx.name LIKE 'sqlite_autoindex_documents_%' AND col.name = 'file_path'")));

            ExecuteNonQuery(connection, "INSERT INTO documents (name, file_path) VALUES ('Case distinct', 'c:/SAME.pdf'), ('Null 1', NULL), ('Null 2', NULL), ('Empty 1', ''), ('Empty 2', '')");
            Assert.Throws<SqliteException>(() => ExecuteNonQuery(connection, "INSERT INTO documents (name, file_path) VALUES ('Duplicate', 'C:/same.pdf')"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
    }

    [Fact]
    public void RestoreDatabase_MigratesNamedDocumentPathNoCaseIndex()
    {
        var backupPath = Path.Combine(Path.GetTempPath(), $"sdm_nocase_named_{Guid.NewGuid():N}.db");
        try
        {
            Assert.True(Db.BackupDatabase(backupPath));
            ExecuteNonQuery(backupPath, "DROP INDEX idx_documents_file_path_unique; CREATE UNIQUE INDEX legacy_documents_file_path_unique ON documents(file_path COLLATE NOCASE)");

            Assert.True(Db.CanRestoreDatabase(backupPath));
            Assert.True(Db.RestoreDatabase(backupPath));

            using var connection = new SqliteConnection(Db.ConnectionString);
            connection.Open();
            Assert.Equal(
                "CREATE UNIQUE INDEX idx_documents_file_path_unique ON documents(file_path COLLATE BINARY) WHERE file_path IS NOT NULL AND file_path <> ''",
                Scalar(connection, "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = 'idx_documents_file_path_unique'"));
            Assert.Equal(0L, Convert.ToInt64(Scalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'legacy_documents_file_path_unique'")));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
    }

    private static object Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar()!;
    }

    private static void ExecuteNonQuery(string databasePath, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Foreign Keys=True");
        connection.Open();
        ExecuteNonQuery(connection, sql);
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void RebuildDocumentsWithNoCaseUniquePath(string databasePath)
    {
        ExecuteNonQuery(databasePath, """
            PRAGMA foreign_keys = OFF;
            CREATE TABLE documents_rebuild (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                subject TEXT,
                type TEXT,
                file_path TEXT COLLATE NOCASE UNIQUE,
                notes TEXT,
                created_at DATETIME DEFAULT (datetime('now', 'localtime')),
                file_size REAL,
                author TEXT,
                is_important INTEGER DEFAULT 0,
                tags TEXT,
                deadline DATETIME,
                is_deleted INTEGER DEFAULT 0,
                deleted_at DATETIME
            );
            INSERT INTO documents_rebuild (
                id, name, subject, type, file_path, notes, created_at, file_size,
                author, is_important, tags, deadline, is_deleted, deleted_at)
            SELECT
                id, name, subject, type, file_path, notes, created_at, file_size,
                author, is_important, tags, deadline, is_deleted, deleted_at
            FROM documents;
            DROP TABLE documents;
            ALTER TABLE documents_rebuild RENAME TO documents;
            CREATE UNIQUE INDEX idx_documents_file_path_unique ON documents(file_path COLLATE BINARY) WHERE file_path IS NOT NULL AND file_path <> '';
            PRAGMA foreign_keys = ON;
            """);
    }

    [Fact]
    public void DroppedFileImportService_DoesNotMisclassifyOtherUniqueConstraint()
    {
        using (var connection = new SqliteConnection(Db.ConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE UNIQUE INDEX idx_documents_name_test ON documents(name)";
            command.ExecuteNonQuery();
        }

        var service = new DroppedFileImportService(Repo);
        Assert.Equal(
            DocumentImportOutcome.Imported,
            service.SaveDocument(new StudyDocument { Name = "Same name", FilePath = "C:/first.pdf" }));

        Assert.Throws<SqliteException>(() =>
            service.SaveDocument(new StudyDocument { Name = "Same name", FilePath = "C:/second.pdf" }));
    }
}

public sealed class DocumentPathUniquenessMigrationTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"sdm_path_migration_{Guid.NewGuid():N}.db");

    [Fact]
    public void InitializeDatabase_RebuildsHistoricalDocumentPathNoCaseAutoIndex()
    {
        CreateHistoricalDatabaseWithNoCaseUniquePath();
        var database = CreateDatabaseHelper();

        database.InitializeDatabase();

        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        ExecuteNonQuery(connection, "INSERT INTO documents (name, file_path) VALUES ('Case distinct', 'c:/SAME.pdf'), ('Empty 1', ''), ('Empty 2', '')");
        Assert.Equal(
            "CREATE UNIQUE INDEX idx_documents_file_path_unique ON documents(file_path COLLATE BINARY) WHERE file_path IS NOT NULL AND file_path <> ''",
            Scalar(connection, "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = 'idx_documents_file_path_unique'"));
    }

    [Fact]
    public void InitializeDatabase_RemovesHistoricalNamedNoCasePathIndex()
    {
        CreateHistoricalDatabase("TEXT", "C:/other.pdf");
        using (var connection = new SqliteConnection($"Data Source={_databasePath};Foreign Keys=True"))
        {
            connection.Open();
            ExecuteNonQuery(connection, "DELETE FROM documents WHERE id = 4");
            ExecuteNonQuery(connection, "CREATE UNIQUE INDEX legacy_documents_file_path_unique ON documents(file_path COLLATE NOCASE)");
        }

        var database = CreateDatabaseHelper();
        database.InitializeDatabase();

        using var migratedConnection = new SqliteConnection(database.ConnectionString);
        migratedConnection.Open();
        Assert.Equal(0L, Convert.ToInt64(Scalar(migratedConnection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'legacy_documents_file_path_unique'")));
        Assert.Equal("CREATE UNIQUE INDEX idx_documents_file_path_unique ON documents(file_path COLLATE BINARY) WHERE file_path IS NOT NULL AND file_path <> ''", Scalar(migratedConnection, "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = 'idx_documents_file_path_unique'"));
        ExecuteNonQuery(migratedConnection, "INSERT INTO documents (name, file_path) VALUES ('Case distinct', 'c:/SAME.pdf')");
        ExecuteNonQuery(migratedConnection, "INSERT INTO documents (name, file_path) VALUES ('Empty 1', '')");
        ExecuteNonQuery(migratedConnection, "INSERT INTO documents (name, file_path) VALUES ('Empty 2', '')");
    }

    [Fact]
    public void InitializeDatabase_DeduplicatesHistoricalPathsAndCreatesIndexIdempotently()
    {
        CreateHistoricalDatabase("TEXT");
        var database = CreateDatabaseHelper();

        database.InitializeDatabase();
        database.InitializeDatabase();

        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        Assert.Equal("C:/same.pdf", Scalar(connection, "SELECT file_path FROM documents WHERE id = 1"));
        Assert.Equal(DBNull.Value, Scalar(connection, "SELECT file_path FROM documents WHERE id = 2"));
        Assert.Equal(string.Empty, Scalar(connection, "SELECT file_path FROM documents WHERE id = 3"));
        Assert.Equal(string.Empty, Scalar(connection, "SELECT file_path FROM documents WHERE id = 4"));
        Assert.Equal(1L, Convert.ToInt64(Scalar(connection, "SELECT COUNT(*) FROM documents WHERE file_path IS NULL")));
        Assert.Equal(
            "CREATE UNIQUE INDEX idx_documents_file_path_unique ON documents(file_path COLLATE BINARY) WHERE file_path IS NOT NULL AND file_path <> ''",
            Scalar(connection, "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = 'idx_documents_file_path_unique'"));
    }

    [Fact]
    public void InitializeDatabase_UsesBinaryEqualityForHistoricalNoCaseColumn()
    {
        CreateHistoricalDatabase("TEXT COLLATE NOCASE", "c:/SAME.pdf");
        var database = CreateDatabaseHelper();

        database.InitializeDatabase();

        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        Assert.Equal("C:/same.pdf", Scalar(connection, "SELECT file_path FROM documents WHERE id = 1"));
        Assert.Equal("c:/SAME.pdf", Scalar(connection, "SELECT file_path FROM documents WHERE id = 2"));
    }

    [Fact]
    public void InitializeDatabase_WhenHistoricalDedupeFails_RollsBackDataAndIndex()
    {
        CreateHistoricalDatabase("TEXT NOT NULL");
        var database = CreateDatabaseHelper();

        Assert.Throws<SqliteException>(database.InitializeDatabase);

        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        Assert.Equal(2L, Convert.ToInt64(Scalar(connection, "SELECT COUNT(*) FROM documents WHERE file_path = 'C:/same.pdf'")));
        Assert.Equal(0L, Convert.ToInt64(Scalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'idx_documents_file_path_unique'")));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_databasePath))
                File.Delete(_databasePath);
        }
        catch
        {
        }
    }

    private DatabaseHelper CreateDatabaseHelper()
    {
        var database = new DatabaseHelper();
        database.SetDatabasePath(_databasePath);
        return database;
    }

    private void CreateHistoricalDatabase(string filePathType, string secondPath = "C:/same.pdf")
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath};Foreign Keys=True");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE documents (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                subject TEXT,
                type TEXT,
                file_path {filePathType},
                notes TEXT,
                created_at DATETIME,
                file_size REAL,
                author TEXT,
                is_important INTEGER DEFAULT 0,
                tags TEXT,
                deadline DATETIME,
                is_deleted INTEGER DEFAULT 0,
                deleted_at DATETIME
            );
            INSERT INTO documents (id, name, file_path, is_deleted) VALUES
                (1, 'First', 'C:/same.pdf', 0),
                (2, 'Second', @secondPath, 1),
                (3, 'Empty 1', '', 0),
                (4, 'Empty 2', '', 1);
            """;
        command.Parameters.AddWithValue("@secondPath", secondPath);
        command.ExecuteNonQuery();
    }

    private void CreateHistoricalDatabaseWithNoCaseUniquePath()
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath};Foreign Keys=True");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE documents (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                subject TEXT,
                type TEXT,
                file_path TEXT COLLATE NOCASE UNIQUE,
                notes TEXT,
                created_at DATETIME,
                file_size REAL,
                author TEXT,
                is_important INTEGER DEFAULT 0,
                tags TEXT,
                deadline DATETIME,
                is_deleted INTEGER DEFAULT 0,
                deleted_at DATETIME
            );
            INSERT INTO documents (id, name, file_path, is_deleted) VALUES
                (1, 'First', 'C:/same.pdf', 0),
                (2, 'Second', 'C:/other.pdf', 1);
            """;
        command.ExecuteNonQuery();
    }

    private static object Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar()!;
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
