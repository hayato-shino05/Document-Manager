using Microsoft.Data.Sqlite;
using StudyDocumentManager.Data.Helpers;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class LegacyVietnameseSchemaMigrationTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"sdm_legacy_{Guid.NewGuid():N}.db");

    [Fact]
    public void InitializeDatabase_MigratesMixedVietnameseAndEnglishSchemaWithoutDataLoss()
    {
        CreateMixedLegacyDatabase();
        var database = new DatabaseHelper();
        database.SetDatabasePath(_databasePath);

        database.InitializeDatabase();

        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();

        Assert.False(TableExists(connection, "tai_lieu"));
        Assert.False(TableExists(connection, "danh_muc"));
        Assert.False(TableExists(connection, "loai_tai_lieu"));
        Assert.Equal(7L, GetCount(connection, "documents"));
        Assert.Equal(5L, GetCount(connection, "documents", "name LIKE 'Legacy %'"));

        Assert.Equal("Current document", GetString(connection, "SELECT name FROM documents WHERE id = 1"));
        Assert.Equal("Legacy image", GetString(connection, "SELECT name FROM documents WHERE subject = 'Work' AND type = 'Image'"));
        Assert.Equal("Legacy notes", GetString(connection, "SELECT notes FROM documents WHERE name = 'Legacy image'"));
        Assert.Equal(1L, GetInt64(connection, "SELECT is_important FROM documents WHERE name = 'Legacy image'"));
        Assert.Equal(1L, GetInt64(connection, "SELECT is_deleted FROM documents WHERE name = 'Legacy spreadsheet'"));

        Assert.Equal(1L, GetCount(connection, "categories", "name = 'Archived'"));
        Assert.Equal(1L, GetCount(connection, "document_types", "name = 'Custom type'"));
        Assert.Equal("Legacy image", GetString(connection, "SELECT d.name FROM collection_items ci INNER JOIN documents d ON d.id = ci.document_id"));
        Assert.Equal("Legacy note", GetString(connection, "SELECT content FROM personal_notes"));
        Assert.Equal("Legacy document 5", GetString(connection, "SELECT d.name FROM recent_files r INNER JOIN documents d ON d.id = r.document_id"));
        Assert.Equal(2L, GetCount(connection, "document_relations"));
        Assert.Equal(3L, GetCount(connection, "collection_items"));
        Assert.Equal(2L, GetCount(connection, "personal_notes"));
        Assert.Equal(1L, GetCount(connection, "recent_files"));
    }

    [Fact]
    public void InitializeDatabase_MigratesLegacySchemaWithoutSoftDeleteColumns()
    {
        CreateLegacyDatabaseWithoutSoftDeleteColumns();
        var database = new DatabaseHelper();
        database.SetDatabasePath(_databasePath);

        database.InitializeDatabase();

        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();

        Assert.Equal("Older legacy document", GetString(connection, "SELECT name FROM documents"));
        Assert.Equal(0L, GetInt64(connection, "SELECT is_deleted FROM documents"));
        Assert.Null(GetNullableValue(connection, "SELECT deleted_at FROM documents"));
    }

    [Fact]
    public void InitializeDatabase_PartialLegacySchema_PreservesDatabase()
    {
        using (var connection = new SqliteConnection($"Data Source={_databasePath};Foreign Keys=True"))
        {
            connection.Open();
            Execute(connection, "CREATE TABLE tai_lieu (id INTEGER PRIMARY KEY, ten TEXT NOT NULL)");
        }

        SqliteConnection.ClearAllPools();
        var before = File.ReadAllBytes(_databasePath);
        var database = new DatabaseHelper();
        database.SetDatabasePath(_databasePath);

        Assert.Throws<InvalidOperationException>(() => database.InitializeDatabase());

        database.CloseAllConnections();
        Assert.Equal(before, File.ReadAllBytes(_databasePath));
    }

    [Fact]
    public void InitializeDatabase_MigratedSchema_IsIdempotentAndUsesCascadingDocumentForeignKeys()
    {
        CreateMixedLegacyDatabase();
        var database = new DatabaseHelper();
        database.SetDatabasePath(_databasePath);

        database.InitializeDatabase();
        database.InitializeDatabase();

        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();

        Assert.Equal(7L, GetCount(connection, "documents"));
        Assert.Equal(0L, GetForeignKeyViolationCount(connection));
        Assert.Equal("documents", GetString(connection, "SELECT \"table\" FROM pragma_foreign_key_list('recent_files')"));

        Execute(connection, "DELETE FROM documents WHERE name = 'Legacy document 5'");
        Assert.Equal(2L, GetCount(connection, "collection_items"));
        Assert.Equal(0L, GetCount(connection, "recent_files"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }

    private void CreateLegacyDatabaseWithoutSoftDeleteColumns()
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath};Foreign Keys=True");
        connection.Open();
        Execute(connection, """
            CREATE TABLE tai_lieu (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                ten TEXT NOT NULL,
                mon_hoc TEXT,
                loai TEXT,
                duong_dan TEXT,
                ghi_chu TEXT,
                ngay_them DATETIME,
                kich_thuoc REAL,
                tac_gia TEXT,
                quan_trong INTEGER DEFAULT 0,
                tags TEXT,
                deadline DATETIME
            );
            INSERT INTO tai_lieu VALUES (1, 'Older legacy document', 'Legacy', 'Document', 'C:/legacy/old.txt', NULL, '2026-03-01 10:00:00', 1.5, NULL, 0, NULL, NULL);
            CREATE TABLE danh_muc (id INTEGER PRIMARY KEY AUTOINCREMENT, ten TEXT NOT NULL UNIQUE, created_at DATETIME);
            INSERT INTO danh_muc VALUES (1, 'Legacy', '2026-03-01 00:00:00');
            CREATE TABLE loai_tai_lieu (id INTEGER PRIMARY KEY AUTOINCREMENT, ten TEXT NOT NULL UNIQUE, created_at DATETIME);
            INSERT INTO loai_tai_lieu VALUES (1, 'Document', '2026-03-01 00:00:00');
            """);
    }

    private void CreateMixedLegacyDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_databasePath};Foreign Keys=True");
        connection.Open();
        Execute(connection, """
            CREATE TABLE documents (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                subject TEXT,
                type TEXT,
                file_path TEXT,
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
            INSERT INTO documents (id, name, type, created_at) VALUES (1, 'Current document', 'PDF', '2026-05-01 10:00:00');
            INSERT INTO documents (id, name, type, created_at) VALUES (2, 'Current document 2', 'Excel', '2026-05-02 10:00:00');

            CREATE TABLE categories (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL UNIQUE, created_at DATETIME);
            CREATE TABLE document_types (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL UNIQUE, created_at DATETIME);
            CREATE TABLE app_settings (key TEXT PRIMARY KEY, value TEXT);
            INSERT INTO app_settings (key, value) VALUES ('schema_version', '3');
            CREATE TABLE collections (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, description TEXT, created_at DATETIME);
            INSERT INTO collections (id, name, created_at) VALUES (1, 'Legacy collection', '2026-04-01 00:00:00');

            CREATE TABLE tai_lieu (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                ten TEXT NOT NULL,
                mon_hoc TEXT,
                loai TEXT,
                duong_dan TEXT,
                ghi_chu TEXT,
                ngay_them DATETIME,
                kich_thuoc REAL,
                tac_gia TEXT,
                quan_trong INTEGER DEFAULT 0,
                tags TEXT,
                deadline DATETIME,
                is_deleted INTEGER DEFAULT 0,
                deleted_at DATETIME
            );
            INSERT INTO tai_lieu VALUES (1, 'Legacy image', 'Công việc', 'Hình ảnh', 'C:/legacy/image.jpg', 'Legacy notes', '2026-04-01 10:00:00', 12.5, 'Author', 1, 'tag-one', '2026-06-01', 0, NULL);
            INSERT INTO tai_lieu VALUES (2, 'Legacy spreadsheet', 'Archived', 'Custom type', 'C:/legacy/sheet.xlsx', NULL, '2026-04-02 10:00:00', 3.5, NULL, 0, NULL, NULL, 1, '2026-04-03 10:00:00');
            INSERT INTO tai_lieu VALUES (3, 'Legacy document 3', 'Archived', 'PDF', 'C:/legacy/document.pdf', NULL, '2026-04-03 10:00:00', 2.5, NULL, 0, NULL, NULL, 0, NULL);
            INSERT INTO tai_lieu VALUES (4, 'Legacy document 4', NULL, 'Video', 'C:/legacy/video.webm', NULL, '2026-04-04 10:00:00', 4.5, NULL, 0, NULL, NULL, 0, NULL);
            INSERT INTO tai_lieu VALUES (5, 'Legacy document 5', NULL, 'Excel', 'C:/legacy/sheet.csv', NULL, '2026-04-05 10:00:00', 5.5, NULL, 0, NULL, NULL, 0, NULL);

            CREATE TABLE danh_muc (id INTEGER PRIMARY KEY AUTOINCREMENT, ten TEXT NOT NULL UNIQUE, created_at DATETIME);
            INSERT INTO danh_muc VALUES (1, 'Công việc', '2026-04-01 00:00:00');
            INSERT INTO danh_muc VALUES (2, 'Archived', '2026-04-02 00:00:00');
            CREATE TABLE loai_tai_lieu (id INTEGER PRIMARY KEY AUTOINCREMENT, ten TEXT NOT NULL UNIQUE, created_at DATETIME);
            INSERT INTO loai_tai_lieu VALUES (1, 'Hình ảnh', '2026-04-01 00:00:00');
            INSERT INTO loai_tai_lieu VALUES (2, 'Custom type', '2026-04-02 00:00:00');

            CREATE TABLE collection_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                collection_id INTEGER NOT NULL,
                document_id INTEGER NOT NULL,
                added_at DATETIME,
                FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE,
                FOREIGN KEY (document_id) REFERENCES tai_lieu(id) ON DELETE CASCADE,
                UNIQUE(collection_id, document_id)
            );
            INSERT INTO collection_items VALUES (1, 1, 1, '2026-04-01 10:01:00');
            INSERT INTO collection_items VALUES (2, 1, 3, '2026-04-03 10:01:00');
            INSERT INTO collection_items VALUES (3, 1, 5, '2026-04-05 10:01:00');
            CREATE TABLE personal_notes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                document_id INTEGER NOT NULL,
                content TEXT,
                created_at DATETIME,
                updated_at DATETIME,
                FOREIGN KEY (document_id) REFERENCES tai_lieu(id) ON DELETE CASCADE
            );
            INSERT INTO personal_notes VALUES (1, 2, 'Legacy note', '2026-04-02 10:01:00', '2026-04-02 10:01:00');
            INSERT INTO personal_notes VALUES (2, 4, 'Legacy note 4', '2026-04-04 10:01:00', '2026-04-04 10:01:00');
            CREATE TABLE recent_files (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                document_id INTEGER NOT NULL UNIQUE,
                opened_at DATETIME,
                FOREIGN KEY (document_id) REFERENCES tai_lieu(id) ON DELETE CASCADE
            );
            INSERT INTO recent_files VALUES (1, 5, '2026-04-05 10:02:00');
            CREATE TABLE document_relations (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                doc_id_1 INTEGER NOT NULL,
                doc_id_2 INTEGER NOT NULL,
                relation_type TEXT,
                created_at DATETIME,
                FOREIGN KEY (doc_id_1) REFERENCES tai_lieu(id) ON DELETE CASCADE,
                FOREIGN KEY (doc_id_2) REFERENCES tai_lieu(id) ON DELETE CASCADE,
                UNIQUE(doc_id_1, doc_id_2)
            );
            INSERT INTO document_relations VALUES (1, 1, 5, 'related', '2026-04-02 10:02:00');
            INSERT INTO document_relations VALUES (2, 2, 4, 'related', '2026-04-04 10:02:00');
            """);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @name";
        command.Parameters.AddWithValue("@name", tableName);
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    private static long GetCount(SqliteConnection connection, string tableName, string? condition = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName}" + (condition is null ? string.Empty : $" WHERE {condition}");
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static long GetInt64(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static long GetForeignKeyViolationCount(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_foreign_key_check";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static object? GetNullableValue(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }

    private static string GetString(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Assert.IsType<string>(command.ExecuteScalar());
    }
}
