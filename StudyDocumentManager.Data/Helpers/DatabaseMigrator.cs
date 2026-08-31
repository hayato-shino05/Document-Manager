using System.Linq;
using Microsoft.Data.Sqlite;

namespace StudyDocumentManager.Data.Helpers;

/// <summary>
/// Schema creation and idempotent migrations for the SQLite database.
/// Extracted from DatabaseHelper to separate migration concerns from runtime data access.
/// </summary>
public static class DatabaseMigrator
{
    /// <summary>
    /// Run all schema creation and migrations against the configured database.
    /// </summary>
    private static void EnsureArchiveExportKeyUniqueness(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var duplicateCommand = connection.CreateCommand();
        duplicateCommand.Transaction = transaction;
        duplicateCommand.CommandText = "SELECT 1 FROM documents WHERE archive_export_key IS NOT NULL AND archive_export_key <> '' GROUP BY archive_export_key COLLATE NOCASE HAVING COUNT(*) > 1 LIMIT 1";
        if (duplicateCommand.ExecuteScalar() is not null)
            throw new InvalidOperationException("Duplicate archive export keys prevent migration.");

        ExecuteSql(connection, transaction, "CREATE UNIQUE INDEX IF NOT EXISTS ux_documents_archive_export_key ON documents(archive_export_key COLLATE BINARY) WHERE archive_export_key IS NOT NULL AND archive_export_key <> ''");
    }

    public static void RunMigrations(string connectionString)
    {
        const string createTablesQuery = """
            CREATE TABLE IF NOT EXISTS documents (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                archive_export_key TEXT UNIQUE,
                name TEXT NOT NULL,
                subject TEXT,
                type TEXT,
                file_path TEXT,
                notes TEXT,
                created_at DATETIME DEFAULT (datetime('now', 'localtime')),
                file_size REAL,
                author TEXT,
                is_important INTEGER DEFAULT 0,
                tags TEXT,
                deadline DATETIME,
                is_deleted INTEGER DEFAULT 0,
                deleted_at DATETIME,
                status TEXT NOT NULL DEFAULT 'unread'
            );

            CREATE TABLE IF NOT EXISTS collections (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                description TEXT,
                created_at DATETIME DEFAULT (datetime('now', 'localtime'))
            );

            CREATE TABLE IF NOT EXISTS collection_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                collection_id INTEGER NOT NULL,
                document_id INTEGER NOT NULL,
                added_at DATETIME DEFAULT (datetime('now', 'localtime')),
                FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE,
                FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
                UNIQUE(collection_id, document_id)
            );

            CREATE TABLE IF NOT EXISTS personal_notes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                document_id INTEGER NOT NULL,
                note_type TEXT NOT NULL DEFAULT 'general',
                content TEXT,
                is_pinned INTEGER NOT NULL DEFAULT 0,
                is_deleted INTEGER NOT NULL DEFAULT 0,
                created_at DATETIME DEFAULT (datetime('now', 'localtime')),
                updated_at DATETIME DEFAULT (datetime('now', 'localtime')),
                FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS recent_files (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                document_id INTEGER NOT NULL UNIQUE,
                opened_at DATETIME DEFAULT (datetime('now','localtime')),
                FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS document_relations (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                doc_id_1 INTEGER NOT NULL,
                doc_id_2 INTEGER NOT NULL,
                relation_type TEXT DEFAULT 'related',
                created_at DATETIME DEFAULT (datetime('now','localtime')),
                FOREIGN KEY (doc_id_1) REFERENCES documents(id) ON DELETE CASCADE,
                FOREIGN KEY (doc_id_2) REFERENCES documents(id) ON DELETE CASCADE,
                UNIQUE(doc_id_1, doc_id_2)
            );

            CREATE TABLE IF NOT EXISTS categories (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                created_at DATETIME DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS document_types (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                created_at DATETIME DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS app_settings (
                key TEXT PRIMARY KEY,
                value TEXT
            );

            CREATE TABLE IF NOT EXISTS import_inbox (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                document_id INTEGER,
                source_path TEXT NOT NULL,
                display_name TEXT NOT NULL,
                failure_code TEXT,
                duplicate_candidate TEXT,
                subject TEXT,
                type TEXT,
                state TEXT NOT NULL DEFAULT 'Pending',
                created_at DATETIME DEFAULT (datetime('now', 'localtime')),
                updated_at DATETIME DEFAULT (datetime('now', 'localtime')),
                FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE SET NULL
            );
            CREATE TABLE IF NOT EXISTS watched_folders (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                folder_path TEXT NOT NULL,
                enabled INTEGER NOT NULL DEFAULT 1,
                include_subdirectories INTEGER NOT NULL DEFAULT 0,
                last_scan_at DATETIME,
                created_at DATETIME DEFAULT (datetime('now', 'localtime'))
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ux_watched_folders_path ON watched_folders(folder_path COLLATE NOCASE);
            CREATE TABLE IF NOT EXISTS saved_searches (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE COLLATE NOCASE,
                criteria_json TEXT NOT NULL,
                created_at DATETIME DEFAULT (datetime('now', 'localtime'))
            );

            CREATE TABLE IF NOT EXISTS student_context (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                academic_year TEXT NOT NULL DEFAULT '',
                semester TEXT NOT NULL DEFAULT '',
                course TEXT NOT NULL DEFAULT '',
                module TEXT NOT NULL DEFAULT '',
                owner TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS courses (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                code TEXT NOT NULL DEFAULT '',
                UNIQUE(name, code)
            );

            CREATE TABLE IF NOT EXISTS semesters (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                starts_on DATETIME,
                ends_on DATETIME,
                is_active INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS assignments (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                course_id INTEGER,
                semester_id INTEGER,
                official_deadline DATETIME,
                personal_deadline DATETIME,
                status TEXT NOT NULL DEFAULT 'planned',
                priority TEXT NOT NULL DEFAULT 'normal',
                milestone TEXT NOT NULL DEFAULT '',
                notes TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (course_id) REFERENCES courses(id) ON DELETE SET NULL,
                FOREIGN KEY (semester_id) REFERENCES semesters(id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS assignment_documents (
                assignment_id INTEGER NOT NULL,
                document_id INTEGER NOT NULL,
                PRIMARY KEY (assignment_id, document_id),
                FOREIGN KEY (assignment_id) REFERENCES assignments(id) ON DELETE CASCADE,
                FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_documents_subject ON documents(subject);
            CREATE INDEX IF NOT EXISTS idx_documents_type ON documents(type);
            CREATE INDEX IF NOT EXISTS idx_documents_created_at ON documents(created_at);
            CREATE INDEX IF NOT EXISTS idx_documents_deadline ON documents(deadline);
            CREATE INDEX IF NOT EXISTS idx_collection_items_collection ON collection_items(collection_id);
            CREATE INDEX IF NOT EXISTS idx_collection_items_document ON collection_items(document_id);
            CREATE INDEX IF NOT EXISTS idx_documents_deleted ON documents(is_deleted);
            CREATE INDEX IF NOT EXISTS idx_documents_important ON documents(is_important);
            """;

        using var conn = new SqliteConnection(connectionString);
        conn.Open();

        if (HasAnyLegacyVietnameseTable(conn))
            MigrateLegacyVietnameseSchema(conn, createTablesQuery);

        var preflight = Preflight(conn);

        using (var transaction = conn.BeginTransaction())
        {
            ExecuteSql(conn, transaction, createTablesQuery);
            MigrateAddColumn(conn, transaction, "documents", "is_deleted", "INTEGER DEFAULT 0");
            MigrateAddColumn(conn, transaction, "documents", "deleted_at", "DATETIME");
            MigrateAddColumn(conn, transaction, "documents", "status", "TEXT NOT NULL DEFAULT 'unread'");
            MigrateAddColumn(conn, transaction, "documents", "archive_export_key", "TEXT");
            ExecuteSql(conn, transaction, "UPDATE documents SET archive_export_key = lower(hex(randomblob(16))) WHERE archive_export_key IS NULL OR archive_export_key = ''");
            EnsureArchiveExportKeyUniqueness(conn, transaction);
            MigrateAddColumn(conn, transaction, "personal_notes", "note_type", "TEXT NOT NULL DEFAULT 'general'");
            MigrateAddColumn(conn, transaction, "personal_notes", "is_pinned", "INTEGER NOT NULL DEFAULT 0");
            MigrateAddColumn(conn, transaction, "personal_notes", "is_deleted", "INTEGER NOT NULL DEFAULT 0");
            MigrateAddColumn(conn, transaction, "import_inbox", "subject", "TEXT");
            MigrateAddColumn(conn, transaction, "import_inbox", "type", "TEXT");
            MigrateImportInboxSourceUniqueness(conn, transaction);
            ExecuteSql(conn, transaction, "PRAGMA defer_foreign_keys = ON");

            if (preflight.RebuildDocuments)
                RebuildDocumentsTable(conn, transaction);

            foreach (var tableName in preflight.TablesToRebuild)
                RebuildChildTable(conn, transaction, tableName);

            ExecuteSql(conn, transaction, createTablesQuery);
            DropLegacyDocumentPathIndexes(conn, transaction);
            ExecuteSql(conn, transaction, "UPDATE documents AS duplicate SET file_path = NULL WHERE file_path IS NOT NULL AND file_path <> '' AND EXISTS (SELECT 1 FROM documents AS original WHERE original.file_path = duplicate.file_path COLLATE BINARY AND original.id < duplicate.id)");
            ExecuteSql(conn, transaction, "DROP INDEX IF EXISTS idx_documents_file_path_unique");
            ExecuteSql(conn, transaction, "CREATE UNIQUE INDEX idx_documents_file_path_unique ON documents(file_path COLLATE BINARY) WHERE file_path IS NOT NULL AND file_path <> ''");
            EnsureForeignKeyCheckIsClean(conn, transaction);
            transaction.Commit();
        }

        MigrateSeedCategories(conn);
        MigrateNormalizeFileTypes(conn);
        MigrateNeutralizeLabels(conn);
        MigrateWriteSchemaVersion4(conn);
    }

    private sealed record MigrationPreflight(IReadOnlyList<string> TablesToRebuild, bool RebuildDocuments);

    private static bool HasAnyLegacyVietnameseTable(SqliteConnection connection)
        => new[] { "tai_lieu", "danh_muc", "loai_tai_lieu" }.Any(tableName => TableExists(connection, tableName));

    private static void ValidateCurrentTablesForLegacyMigration(SqliteConnection connection)
    {
        if (TableExists(connection, "documents"))
        {
            RequireColumns(connection, "documents", ["id", "name", "subject", "type", "file_path", "notes", "created_at", "file_size", "author", "is_important", "tags", "deadline", "is_deleted", "deleted_at", "status"], ["is_deleted", "deleted_at", "status", "archive_export_key"]);
            EnsureNoUnsupportedIndexesOrTriggers(connection, "documents");
        }

        ValidateKnownTable(connection, "collections", ["id", "name", "description", "created_at"]);
        ValidateKnownTable(connection, "categories", ["id", "name", "created_at"]);
        ValidateKnownTable(connection, "document_types", ["id", "name", "created_at"]);
        ValidateKnownTable(connection, "app_settings", ["key", "value"]);
        ValidateKnownTable(connection, "saved_searches", ["id", "name", "criteria_json", "created_at"]);
    }

    private static void MigrateLegacyVietnameseSchema(SqliteConnection connection, string createTablesQuery)
    {
        ValidateLegacyVietnameseSchema(connection);
        ValidateCurrentTablesForLegacyMigration(connection);

        using var transaction = connection.BeginTransaction();
        ExecuteSql(connection, transaction, createTablesQuery);
        MigrateAddColumn(connection, transaction, "documents", "is_deleted", "INTEGER DEFAULT 0");
        MigrateAddColumn(connection, transaction, "documents", "deleted_at", "DATETIME");
        CopyLegacyDocuments(connection, transaction);
        ExecuteSql(connection, transaction, "INSERT INTO categories (name, created_at) SELECT legacy.ten, legacy.created_at FROM danh_muc legacy WHERE NOT EXISTS (SELECT 1 FROM categories current WHERE current.name = legacy.ten)");
        ExecuteSql(connection, transaction, "INSERT INTO document_types (name, created_at) SELECT legacy.ten, legacy.created_at FROM loai_tai_lieu legacy WHERE NOT EXISTS (SELECT 1 FROM document_types current WHERE current.name = legacy.ten)");

        foreach (var tableName in new[] { "collection_items", "personal_notes", "recent_files", "document_relations" })
        {
            if (TableExists(connection, tableName))
                RebuildLegacyChildTable(connection, transaction, tableName);
        }

        ExecuteSql(connection, transaction, "DROP TABLE tai_lieu");
        ExecuteSql(connection, transaction, "DROP TABLE danh_muc");
        ExecuteSql(connection, transaction, "DROP TABLE loai_tai_lieu");
        ExecuteSql(connection, transaction, "INSERT OR REPLACE INTO app_settings (key, value) VALUES ('schema_version', '2')");
        ExecuteSql(connection, transaction, createTablesQuery);
        EnsureForeignKeyCheckIsClean(connection, transaction);
        transaction.Commit();
    }

    private static void ValidateLegacyVietnameseSchema(SqliteConnection connection)
    {
        var tables = GetApplicationTables(connection);
        var legacyTables = new[] { "tai_lieu", "danh_muc", "loai_tai_lieu" };
        var foundLegacyTables = legacyTables.Where(tables.Contains).ToList();
        if (foundLegacyTables.Count != legacyTables.Length)
            throw new InvalidOperationException($"Incomplete legacy database tables: {string.Join(", ", foundLegacyTables)}.");

        var supportedTables = new HashSet<string>(StringComparer.Ordinal)
        {
            "documents", "collections", "collection_items", "personal_notes", "recent_files",
            "document_relations", "categories", "document_types", "app_settings",
            "saved_searches", "student_context", "courses", "semesters", "assignments", "assignment_documents", "watched_folders",
            "tai_lieu", "danh_muc", "loai_tai_lieu"
        };
        var unsupportedTables = tables.Where(table => !supportedTables.Contains(table)).ToList();
        if (unsupportedTables.Count > 0)
            throw new InvalidOperationException($"Unsupported database tables: {string.Join(", ", unsupportedTables)}.");

        RequireColumns(connection, "tai_lieu", ["id", "ten", "mon_hoc", "loai", "duong_dan", "ghi_chu", "ngay_them", "kich_thuoc", "tac_gia", "quan_trong", "tags", "deadline", "is_deleted", "deleted_at"], ["is_deleted", "deleted_at"]);
        RequireColumns(connection, "danh_muc", ["id", "ten", "created_at"], []);
        RequireColumns(connection, "loai_tai_lieu", ["id", "ten", "created_at"], []);
        EnsureNoUnsupportedLegacyIndexes(connection, "tai_lieu");
        EnsureNoTriggers(connection, "tai_lieu");
        EnsureNoTriggers(connection, "danh_muc");
        EnsureNoTriggers(connection, "loai_tai_lieu");

        ValidateLegacyChildTable(connection, "collection_items", ["id", "collection_id", "document_id", "added_at"],
            [("collection_id", "collections", "id"), ("document_id", "tai_lieu", "id")], ["collection_id", "document_id"]);
        ValidateLegacyChildTable(connection, "personal_notes", ["id", "document_id", "content", "created_at", "updated_at"],
            [("document_id", "tai_lieu", "id")], null);
        ValidateLegacyChildTable(connection, "recent_files", ["id", "document_id", "opened_at"],
            [("document_id", "tai_lieu", "id")], ["document_id"]);
        ValidateLegacyChildTable(connection, "document_relations", ["id", "doc_id_1", "doc_id_2", "relation_type", "created_at"],
            [("doc_id_1", "tai_lieu", "id"), ("doc_id_2", "tai_lieu", "id")], ["doc_id_1", "doc_id_2"]);
    }

    private static void ValidateLegacyChildTable(
        SqliteConnection connection,
        string tableName,
        string[] expectedColumns,
        (string From, string ParentTable, string ParentColumn)[] expectedForeignKeys,
        string[]? uniqueColumns)
    {
        if (!TableExists(connection, tableName))
            return;

        RequireColumns(connection, tableName, expectedColumns, []);
        EnsureNoUnsupportedIndexesOrTriggers(connection, tableName);
        EnsureNoOrphans(connection, tableName, expectedForeignKeys);

        if (uniqueColumns is not null && !HasUniqueIndex(connection, tableName, uniqueColumns))
            throw new InvalidOperationException($"Missing unique constraint in '{tableName}'.");

        var actualForeignKeys = GetForeignKeys(connection, tableName);
        if (actualForeignKeys.Count != expectedForeignKeys.Length || actualForeignKeys.Any(foreignKey =>
            !expectedForeignKeys.Any(expected => expected.From == foreignKey.From && expected.ParentTable == foreignKey.ParentTable && expected.ParentColumn == foreignKey.ParentColumn) ||
            !string.Equals(foreignKey.OnDelete, "CASCADE", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Unsupported foreign key layout in '{tableName}'.");
        }
    }

    private static void EnsureNoUnsupportedLegacyIndexes(SqliteConnection connection, string tableName)
    {
        var allowedIndexes = new HashSet<string>(StringComparer.Ordinal)
        {
            "idx_tai_lieu_mon_hoc", "idx_tai_lieu_loai", "idx_tai_lieu_ngay_them",
            "idx_tai_lieu_deadline", "idx_tai_lieu_deleted", "idx_tai_lieu_quan_trong"
        };

        using var indexes = connection.CreateCommand();
        indexes.CommandText = $"PRAGMA index_list({tableName})";
        using var indexReader = indexes.ExecuteReader();
        while (indexReader.Read())
        {
            var indexName = indexReader.GetString(1);
            var origin = indexReader.GetString(3);
            if (origin == "c" && !allowedIndexes.Contains(indexName))
                throw new InvalidOperationException($"Unsupported index '{indexName}' on '{tableName}'.");
        }
    }

    private static void EnsureNoTriggers(SqliteConnection connection, string tableName)
    {
        using var triggers = connection.CreateCommand();
        triggers.CommandText = "SELECT name FROM sqlite_master WHERE type = 'trigger' AND tbl_name = @tableName";
        triggers.Parameters.AddWithValue("@tableName", tableName);
        if (triggers.ExecuteScalar() is not null)
            throw new InvalidOperationException($"Unsupported trigger on '{tableName}'.");
    }

    private static void CopyLegacyDocuments(SqliteConnection connection, SqliteTransaction transaction)
    {
        var columns = GetColumns(connection, "tai_lieu");
        var isDeleted = columns.Contains("is_deleted", StringComparer.Ordinal) ? "is_deleted" : "0";
        var deletedAt = columns.Contains("deleted_at", StringComparer.Ordinal) ? "deleted_at" : "NULL";
        var rows = new List<object?[]>();
        using (var source = connection.CreateCommand())
        {
            source.Transaction = transaction;
            source.CommandText = $"SELECT id, ten, mon_hoc, loai, duong_dan, ghi_chu, ngay_them, kich_thuoc, tac_gia, quan_trong, tags, deadline, {isDeleted}, {deletedAt} FROM tai_lieu ORDER BY id";
            using var reader = source.ExecuteReader();
            while (reader.Read())
            {
                var values = new object?[reader.FieldCount];
                reader.GetValues(values);
                rows.Add(values);
            }
        }

        ExecuteSql(connection, transaction, "CREATE TEMP TABLE legacy_document_map (legacy_id INTEGER PRIMARY KEY, document_id INTEGER NOT NULL)");
        foreach (var row in rows)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO documents (name, subject, type, file_path, notes, created_at, file_size, author, is_important, tags, deadline, is_deleted, deleted_at)
                VALUES (@name, @subject, @type, @filePath, @notes, @createdAt, @fileSize, @author, @isImportant, @tags, @deadline, @isDeleted, @deletedAt)
                """;
            insert.Parameters.AddWithValue("@name", row[1]!);
            insert.Parameters.AddWithValue("@subject", row[2]!);
            insert.Parameters.AddWithValue("@type", row[3]!);
            insert.Parameters.AddWithValue("@filePath", row[4]!);
            insert.Parameters.AddWithValue("@notes", row[5]!);
            insert.Parameters.AddWithValue("@createdAt", row[6]!);
            insert.Parameters.AddWithValue("@fileSize", row[7]!);
            insert.Parameters.AddWithValue("@author", row[8]!);
            insert.Parameters.AddWithValue("@isImportant", row[9]!);
            insert.Parameters.AddWithValue("@tags", row[10]!);
            insert.Parameters.AddWithValue("@deadline", row[11]!);
            insert.Parameters.AddWithValue("@isDeleted", row[12] is DBNull ? 0 : row[12]!);
            insert.Parameters.AddWithValue("@deletedAt", row[13]!);
            insert.ExecuteNonQuery();

            using var map = connection.CreateCommand();
            map.Transaction = transaction;
            map.CommandText = "INSERT INTO legacy_document_map (legacy_id, document_id) VALUES (@legacyId, last_insert_rowid())";
            map.Parameters.AddWithValue("@legacyId", row[0]!);
            map.ExecuteNonQuery();
        }
    }

    private static void RebuildLegacyChildTable(SqliteConnection connection, SqliteTransaction transaction, string tableName)
    {
        var (definition, copyQuery) = tableName switch
        {
            "collection_items" => ("CREATE TABLE collection_items_rebuild (id INTEGER PRIMARY KEY AUTOINCREMENT, collection_id INTEGER NOT NULL, document_id INTEGER NOT NULL, added_at DATETIME DEFAULT (datetime('now', 'localtime')), FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE, FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE, UNIQUE(collection_id, document_id))", "INSERT INTO collection_items_rebuild (id, collection_id, document_id, added_at) SELECT child.id, child.collection_id, map.document_id, child.added_at FROM collection_items child INNER JOIN legacy_document_map map ON map.legacy_id = child.document_id"),
            "personal_notes" => ("CREATE TABLE personal_notes_rebuild (id INTEGER PRIMARY KEY AUTOINCREMENT, document_id INTEGER NOT NULL, note_type TEXT NOT NULL DEFAULT 'general', content TEXT, is_pinned INTEGER NOT NULL DEFAULT 0, is_deleted INTEGER NOT NULL DEFAULT 0, created_at DATETIME DEFAULT (datetime('now', 'localtime')), updated_at DATETIME DEFAULT (datetime('now', 'localtime')), FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE)", "INSERT INTO personal_notes_rebuild (id, document_id, note_type, content, is_pinned, is_deleted, created_at, updated_at) SELECT child.id, map.document_id, 'general', child.content, 0, 0, child.created_at, child.updated_at FROM personal_notes child INNER JOIN legacy_document_map map ON map.legacy_id = child.document_id"),
            "recent_files" => ("CREATE TABLE recent_files_rebuild (id INTEGER PRIMARY KEY AUTOINCREMENT, document_id INTEGER NOT NULL UNIQUE, opened_at DATETIME DEFAULT (datetime('now','localtime')), FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE)", "INSERT INTO recent_files_rebuild (id, document_id, opened_at) SELECT child.id, map.document_id, child.opened_at FROM recent_files child INNER JOIN legacy_document_map map ON map.legacy_id = child.document_id"),
            "document_relations" => ("CREATE TABLE document_relations_rebuild (id INTEGER PRIMARY KEY AUTOINCREMENT, doc_id_1 INTEGER NOT NULL, doc_id_2 INTEGER NOT NULL, relation_type TEXT DEFAULT 'related', created_at DATETIME DEFAULT (datetime('now','localtime')), FOREIGN KEY (doc_id_1) REFERENCES documents(id) ON DELETE CASCADE, FOREIGN KEY (doc_id_2) REFERENCES documents(id) ON DELETE CASCADE, UNIQUE(doc_id_1, doc_id_2))", "INSERT INTO document_relations_rebuild (id, doc_id_1, doc_id_2, relation_type, created_at) SELECT child.id, first_map.document_id, second_map.document_id, child.relation_type, child.created_at FROM document_relations child INNER JOIN legacy_document_map first_map ON first_map.legacy_id = child.doc_id_1 INNER JOIN legacy_document_map second_map ON second_map.legacy_id = child.doc_id_2"),
            _ => throw new InvalidOperationException($"Unsupported legacy table '{tableName}'.")
        };

        ExecuteSql(connection, transaction, definition);
        ExecuteSql(connection, transaction, copyQuery);
        ExecuteSql(connection, transaction, $"DROP TABLE {tableName}");
        ExecuteSql(connection, transaction, $"ALTER TABLE {tableName}_rebuild RENAME TO {tableName}");
    }

    private static MigrationPreflight Preflight(SqliteConnection connection)
    {
        var tables = GetApplicationTables(connection);
        if (tables.Count == 0)
            return new MigrationPreflight([], false);

        var supportedTables = new HashSet<string>(StringComparer.Ordinal)
        {
            "documents", "collections", "collection_items", "personal_notes", "recent_files",
            "document_relations", "categories", "document_types", "app_settings", "saved_searches",
            "student_context", "courses", "semesters", "assignments", "assignment_documents", "import_inbox", "watched_folders"
        };
        var unsupportedTables = tables.Where(table => !supportedTables.Contains(table)).ToList();
        if (unsupportedTables.Count > 0)
            throw new InvalidOperationException($"Unsupported database tables: {string.Join(", ", unsupportedTables)}.");

        RequireColumns(connection, "documents", ["id", "name", "subject", "type", "file_path", "notes", "created_at", "file_size", "author", "is_important", "tags", "deadline", "is_deleted", "deleted_at", "status"], ["is_deleted", "deleted_at", "status", "archive_export_key"]);
        var rebuildDocuments = ValidateDocumentIndexesAndTriggers(connection);
        ValidateKnownTable(connection, "collections", ["id", "name", "description", "created_at"]);
        ValidateKnownTable(connection, "categories", ["id", "name", "created_at"]);
        ValidateKnownTable(connection, "document_types", ["id", "name", "created_at"]);
        ValidateKnownTable(connection, "app_settings", ["key", "value"]);
        ValidateKnownTable(connection, "saved_searches", ["id", "name", "criteria_json", "created_at"]);
        ValidateKnownTable(connection, "student_context", ["id", "academic_year", "semester", "course", "module", "owner"]);
        ValidateKnownTable(connection, "courses", ["id", "name", "code"]);
        ValidateKnownTable(connection, "semesters", ["id", "name", "starts_on", "ends_on", "is_active"]);
        ValidateKnownTable(connection, "assignments", ["id", "title", "course_id", "semester_id", "official_deadline", "personal_deadline", "status", "priority", "milestone", "notes"]);
        ValidateKnownTable(connection, "assignment_documents", ["assignment_id", "document_id"]);
        if (TableExists(connection, "import_inbox"))
            RequireColumns(connection, "import_inbox", ["id", "document_id", "source_path", "display_name", "failure_code", "duplicate_candidate", "subject", "type", "state", "created_at", "updated_at"], ["subject", "type"]);
        if (TableExists(connection, "watched_folders"))
            RequireColumns(connection, "watched_folders", ["id", "folder_path", "enabled", "include_subdirectories", "last_scan_at", "created_at"], []);

        var tablesToRebuild = new List<string>();
        ValidateChildTable(connection, "collection_items", ["id", "collection_id", "document_id", "added_at"],
            [("collection_id", "collections", "id"), ("document_id", "documents", "id")], ["collection_id", "document_id"], tablesToRebuild);
        ValidateChildTable(connection, "personal_notes", ["id", "document_id", "content", "created_at", "updated_at"],
            [("document_id", "documents", "id")], null, tablesToRebuild, ["note_type", "is_pinned", "is_deleted"]);
        ValidateChildTable(connection, "recent_files", ["id", "document_id", "opened_at"],
            [("document_id", "documents", "id")], ["document_id"], tablesToRebuild);
        ValidateChildTable(connection, "document_relations", ["id", "doc_id_1", "doc_id_2", "relation_type", "created_at"],
            [("doc_id_1", "documents", "id"), ("doc_id_2", "documents", "id")], ["doc_id_1", "doc_id_2"], tablesToRebuild);

        EnsureForeignKeyCheckIsClean(connection, null);
        return new MigrationPreflight(tablesToRebuild, rebuildDocuments);
    }

    private static List<string> GetApplicationTables(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
        using var reader = command.ExecuteReader();
        var tables = new List<string>();
        while (reader.Read())
            tables.Add(reader.GetString(0));
        return tables;
    }

    private static void ValidateKnownTable(SqliteConnection connection, string tableName, string[] requiredColumns)
    {
        if (!TableExists(connection, tableName))
            return;

        RequireColumns(connection, tableName, requiredColumns, []);
        EnsureNoUnsupportedIndexesOrTriggers(connection, tableName);
    }

    private static void RequireColumns(SqliteConnection connection, string tableName, string[] expectedColumns, string[] optionalColumns)
    {
        if (!TableExists(connection, tableName))
            throw new InvalidOperationException($"Required table '{tableName}' is missing.");

        var actualColumns = GetColumns(connection, tableName);
        var supportedColumns = expectedColumns.Concat(optionalColumns).ToArray();
        var unsupportedColumns = actualColumns.Except(supportedColumns, StringComparer.Ordinal).ToList();
        if (unsupportedColumns.Count > 0)
            throw new InvalidOperationException($"Unsupported columns in '{tableName}': {string.Join(", ", unsupportedColumns)}.");

        var missingRequiredColumns = expectedColumns.Except(optionalColumns, StringComparer.Ordinal).Except(actualColumns, StringComparer.Ordinal).ToList();
        if (missingRequiredColumns.Count > 0)
            throw new InvalidOperationException($"Missing required columns in '{tableName}': {string.Join(", ", missingRequiredColumns)}.");
    }

    private static void ValidateChildTable(
        SqliteConnection connection,
        string tableName,
        string[] expectedColumns,
        (string From, string ParentTable, string ParentColumn)[] expectedForeignKeys,
        string[]? uniqueColumns,
        List<string> tablesToRebuild,
        string[]? optionalColumns = null)
    {
        if (!TableExists(connection, tableName))
            return;

        RequireColumns(connection, tableName, expectedColumns, optionalColumns ?? []);
        EnsureNoUnsupportedIndexesOrTriggers(connection, tableName);
        EnsureNoOrphans(connection, tableName, expectedForeignKeys);

        if (uniqueColumns is not null && !HasUniqueIndex(connection, tableName, uniqueColumns))
            throw new InvalidOperationException($"Missing unique constraint in '{tableName}'.");

        var actualForeignKeys = GetForeignKeys(connection, tableName);
        if (actualForeignKeys.Count == 0)
        {
            tablesToRebuild.Add(tableName);
            return;
        }

        if (actualForeignKeys.Count != expectedForeignKeys.Length || actualForeignKeys.Any(foreignKey =>
            !expectedForeignKeys.Any(expected => expected.From == foreignKey.From && expected.ParentTable == foreignKey.ParentTable && expected.ParentColumn == foreignKey.ParentColumn) ||
            !string.Equals(foreignKey.OnDelete, "CASCADE", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Unsupported foreign key layout in '{tableName}'.");
        }
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @name";
        command.Parameters.AddWithValue("@name", tableName);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static List<string> GetColumns(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName})";
        using var reader = command.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read())
            columns.Add(reader.GetString(1));
        return columns;
    }

    private static List<(string From, string ParentTable, string ParentColumn, string OnDelete)> GetForeignKeys(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_key_list({tableName})";
        using var reader = command.ExecuteReader();
        var foreignKeys = new List<(string, string, string, string)>();
        while (reader.Read())
            foreignKeys.Add((reader.GetString(3), reader.GetString(2), reader.GetString(4), reader.GetString(6)));
        return foreignKeys;
    }

    private static bool HasUniqueIndex(SqliteConnection connection, string tableName, string[] expectedColumns)
    {
        using var indexes = connection.CreateCommand();
        indexes.CommandText = $"PRAGMA index_list({tableName})";
        using var indexReader = indexes.ExecuteReader();
        var indexNames = new List<string>();
        while (indexReader.Read())
        {
            if (indexReader.GetInt32(2) == 1)
                indexNames.Add(indexReader.GetString(1));
        }
        indexReader.Close();

        return indexNames.Any(indexName =>
        {
            using var columns = connection.CreateCommand();
            columns.CommandText = $"PRAGMA index_info({indexName})";
            using var columnReader = columns.ExecuteReader();
            var names = new List<string>();
            while (columnReader.Read())
                names.Add(columnReader.GetString(2));
            return names.SequenceEqual(expectedColumns, StringComparer.Ordinal);
        });
    }

    private static bool HasUniqueIndex(SqliteConnection connection, string tableName, string[] expectedColumns, string indexName)
    {
        using var columns = connection.CreateCommand();
        columns.CommandText = $"PRAGMA index_info(\"{indexName.Replace("\"", "\"\"")}\")";
        using var columnReader = columns.ExecuteReader();
        var names = new List<string>();
        while (columnReader.Read())
            names.Add(columnReader.GetString(2));
        return names.SequenceEqual(expectedColumns, StringComparer.Ordinal);
    }

    private static bool ValidateDocumentIndexesAndTriggers(SqliteConnection connection)
    {
        var allowedIndexes = new HashSet<string>(StringComparer.Ordinal)
        {
            "idx_documents_subject", "idx_documents_type", "idx_documents_created_at", "idx_documents_deadline", "idx_documents_deleted", "idx_documents_important", "idx_documents_file_path_unique", "ux_documents_archive_export_key"
        };
        var rebuildDocuments = false;
        var indexes = new List<(string Name, bool IsUnique, string Origin)>();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA index_list(documents)";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                indexes.Add((reader.GetString(1), reader.GetInt32(2) == 1, reader.GetString(3)));
        }

        foreach (var index in indexes)
        {
            var isDocumentPathIndex = index.IsUnique && HasUniqueIndex(connection, "documents", ["file_path"], index.Name);
            var isArchiveExportKeyIndex = index.IsUnique && HasUniqueIndex(connection, "documents", ["archive_export_key"], index.Name);
            if (index.Origin == "u")
            {
                if (!isDocumentPathIndex && !isArchiveExportKeyIndex)
                    throw new InvalidOperationException($"Unsupported unique constraint '{index.Name}' on 'documents'.");
                if (isDocumentPathIndex)
                    rebuildDocuments = true;
            }
            else if (index.Origin == "c" && !allowedIndexes.Contains(index.Name) && !isDocumentPathIndex && !isArchiveExportKeyIndex)
            {
                throw new InvalidOperationException($"Unsupported index '{index.Name}' on 'documents'.");
            }
        }

        EnsureNoTriggers(connection, "documents");
        return rebuildDocuments;
    }

    private static void DropLegacyDocumentPathIndexes(SqliteConnection connection, SqliteTransaction transaction)
    {
        var indexes = new List<(string Name, bool IsUnique, string Origin)>();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "PRAGMA index_list(documents)";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                indexes.Add((reader.GetString(1), reader.GetInt32(2) == 1, reader.GetString(3)));
        }

        foreach (var index in indexes)
        {
            if (index.Name != "idx_documents_file_path_unique" && index.Origin == "c" && index.IsUnique && HasUniqueIndex(connection, "documents", ["file_path"], index.Name))
                ExecuteSql(connection, transaction, $"DROP INDEX \"{index.Name.Replace("\"", "\"\"")}\"");
        }
    }

    private static void RebuildDocumentsTable(SqliteConnection connection, SqliteTransaction transaction)
    {
        ExecuteSql(connection, transaction, "CREATE TABLE documents_rebuild (id INTEGER PRIMARY KEY AUTOINCREMENT, archive_export_key TEXT UNIQUE, name TEXT NOT NULL, subject TEXT, type TEXT, file_path TEXT, notes TEXT, created_at DATETIME DEFAULT (datetime('now', 'localtime')), file_size REAL, author TEXT, is_important INTEGER DEFAULT 0, tags TEXT, deadline DATETIME, is_deleted INTEGER DEFAULT 0, deleted_at DATETIME, status TEXT NOT NULL DEFAULT 'unread')");
        ExecuteSql(connection, transaction, "INSERT INTO documents_rebuild (id, archive_export_key, name, subject, type, file_path, notes, created_at, file_size, author, is_important, tags, deadline, is_deleted, deleted_at, status) SELECT id, archive_export_key, name, subject, type, file_path, notes, created_at, file_size, author, is_important, tags, deadline, is_deleted, deleted_at, status FROM documents");
        ExecuteSql(connection, transaction, "DROP TABLE documents");
        ExecuteSql(connection, transaction, "ALTER TABLE documents_rebuild RENAME TO documents");
    }

    private static void EnsureNoUnsupportedIndexesOrTriggers(SqliteConnection connection, string tableName)
    {
        var allowedIndexes = tableName switch
        {
            "documents" => new HashSet<string>(StringComparer.Ordinal)
            {
                "idx_documents_subject", "idx_documents_type", "idx_documents_created_at", "idx_documents_deadline", "idx_documents_deleted", "idx_documents_important", "idx_documents_file_path_unique"
            },
            "collection_items" => new HashSet<string>(StringComparer.Ordinal)
            {
                "idx_collection_items_collection", "idx_collection_items_document"
            },
            _ => new HashSet<string>(StringComparer.Ordinal)
        };

        using var indexes = connection.CreateCommand();
        indexes.CommandText = $"PRAGMA index_list({tableName})";
        using var indexReader = indexes.ExecuteReader();
        while (indexReader.Read())
        {
            var indexName = indexReader.GetString(1);
            var origin = indexReader.GetString(3);
            if (tableName == "documents" && origin == "u")
            {
                var isDocumentPathIndex = HasUniqueIndex(connection, "documents", ["file_path"], indexName);
                var isArchiveExportKeyIndex = HasUniqueIndex(connection, "documents", ["archive_export_key"], indexName);
                if (!isDocumentPathIndex && !isArchiveExportKeyIndex)
                    throw new InvalidOperationException($"Unsupported unique constraint '{indexName}' on '{tableName}'.");
            }
            else if (origin == "c" && !allowedIndexes.Contains(indexName))
            {
                throw new InvalidOperationException($"Unsupported index '{indexName}' on '{tableName}'.");
            }
        }

        using var triggers = connection.CreateCommand();
        triggers.CommandText = "SELECT name FROM sqlite_master WHERE type = 'trigger' AND tbl_name = @tableName";
        triggers.Parameters.AddWithValue("@tableName", tableName);
        if (triggers.ExecuteScalar() is not null)
            throw new InvalidOperationException($"Unsupported trigger on '{tableName}'.");
    }

    private static void EnsureNoOrphans(SqliteConnection connection, string tableName, (string From, string ParentTable, string ParentColumn)[] foreignKeys)
    {
        foreach (var foreignKey in foreignKeys)
        {
            if (!TableExists(connection, foreignKey.ParentTable))
                throw new InvalidOperationException($"Table '{tableName}' references missing parent table '{foreignKey.ParentTable}'.");

            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT EXISTS (SELECT 1 FROM {tableName} child LEFT JOIN {foreignKey.ParentTable} parent ON child.{foreignKey.From} = parent.{foreignKey.ParentColumn} WHERE parent.{foreignKey.ParentColumn} IS NULL)";
            if (Convert.ToInt32(command.ExecuteScalar()) == 1)
                throw new InvalidOperationException($"Orphaned records found in '{tableName}'.");
        }
    }

    private static void RebuildChildTable(SqliteConnection connection, SqliteTransaction transaction, string tableName)
    {
        var (definition, columns) = tableName switch
        {
            "collection_items" => ("CREATE TABLE collection_items_rebuild (id INTEGER PRIMARY KEY AUTOINCREMENT, collection_id INTEGER NOT NULL, document_id INTEGER NOT NULL, added_at DATETIME DEFAULT (datetime('now', 'localtime')), FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE, FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE, UNIQUE(collection_id, document_id))", "id, collection_id, document_id, added_at"),
            "personal_notes" => ("CREATE TABLE personal_notes_rebuild (id INTEGER PRIMARY KEY AUTOINCREMENT, document_id INTEGER NOT NULL, note_type TEXT NOT NULL DEFAULT 'general', content TEXT, is_pinned INTEGER NOT NULL DEFAULT 0, is_deleted INTEGER NOT NULL DEFAULT 0, created_at DATETIME DEFAULT (datetime('now', 'localtime')), updated_at DATETIME DEFAULT (datetime('now', 'localtime')), FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE)", "id, document_id, note_type, content, is_pinned, is_deleted, created_at, updated_at"),
            "recent_files" => ("CREATE TABLE recent_files_rebuild (id INTEGER PRIMARY KEY AUTOINCREMENT, document_id INTEGER NOT NULL UNIQUE, opened_at DATETIME DEFAULT (datetime('now','localtime')), FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE)", "id, document_id, opened_at"),
            "document_relations" => ("CREATE TABLE document_relations_rebuild (id INTEGER PRIMARY KEY AUTOINCREMENT, doc_id_1 INTEGER NOT NULL, doc_id_2 INTEGER NOT NULL, relation_type TEXT DEFAULT 'related', created_at DATETIME DEFAULT (datetime('now','localtime')), FOREIGN KEY (doc_id_1) REFERENCES documents(id) ON DELETE CASCADE, FOREIGN KEY (doc_id_2) REFERENCES documents(id) ON DELETE CASCADE, UNIQUE(doc_id_1, doc_id_2))", "id, doc_id_1, doc_id_2, relation_type, created_at"),
            _ => throw new InvalidOperationException($"Unsupported legacy table '{tableName}'.")
        };

        ExecuteSql(connection, transaction, definition);
        ExecuteSql(connection, transaction, $"INSERT INTO {tableName}_rebuild ({columns}) SELECT {columns} FROM {tableName}");
        ExecuteSql(connection, transaction, $"DROP TABLE {tableName}");
        ExecuteSql(connection, transaction, $"ALTER TABLE {tableName}_rebuild RENAME TO {tableName}");
    }

    private static void EnsureForeignKeyCheckIsClean(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA foreign_key_check";
        using var reader = command.ExecuteReader();
        if (reader.Read())
            throw new InvalidOperationException("Foreign key integrity check failed.");
    }

    private static void ExecuteSql(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = new SqliteCommand(sql, connection, transaction);
        command.ExecuteNonQuery();
    }

    private static void MigrateAddColumn(SqliteConnection conn, SqliteTransaction transaction, string table, string column, string type)
    {
        if (GetColumns(conn, table).Contains(column, StringComparer.Ordinal))
            return;

        using var cmd = new SqliteCommand($"ALTER TABLE {table} ADD COLUMN {column} {type}", conn, transaction);
        cmd.ExecuteNonQuery();
    }

    private static void MigrateImportInboxSourceUniqueness(SqliteConnection conn, SqliteTransaction transaction)
    {
        if (!TableExists(conn, "import_inbox"))
            return;

        ExecuteSql(conn, transaction,
            "DELETE FROM import_inbox WHERE id NOT IN (SELECT MAX(id) FROM import_inbox GROUP BY COALESCE(lower(source_path), ''))");
        ExecuteSql(conn, transaction,
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_import_inbox_source ON import_inbox(source_path COLLATE NOCASE)");
    }

    private static void MigrateSeedCategories(SqliteConnection conn)
    {
        using var cmd1 = new SqliteCommand(
            "INSERT OR IGNORE INTO categories (name) SELECT DISTINCT subject FROM documents WHERE subject IS NOT NULL AND subject != ''", conn);
        cmd1.ExecuteNonQuery();

        using var cmd2 = new SqliteCommand(
            "INSERT OR IGNORE INTO document_types (name) SELECT DISTINCT type FROM documents WHERE type IS NOT NULL AND type != ''", conn);
        cmd2.ExecuteNonQuery();

        var defaultSubjects = new[]
        {
            "Work", "Personal", "Study", "Project",
            "Finance", "Contract", "Reference", "Other"
        };
        var defaultTypes = new[]
        {
            "PDF", "Word", "Excel", "PowerPoint",
            "Document", "Report", "Guide", "Form",
            "Data", "Code", "Book", "Design",
            "Image", "Video", "Audio", "Archive",
            "Other"
        };

        foreach (var s in defaultSubjects)
        {
            using var ins = new SqliteCommand("INSERT OR IGNORE INTO categories (name) VALUES (@name)", conn);
            ins.Parameters.AddWithValue("@name", s);
            ins.ExecuteNonQuery();
        }

        foreach (var t in defaultTypes)
        {
            using var ins = new SqliteCommand("INSERT OR IGNORE INTO document_types (name) VALUES (@name)", conn);
            ins.Parameters.AddWithValue("@name", t);
            ins.ExecuteNonQuery();
        }
    }

    private static void MigrateNormalizeFileTypes(SqliteConnection conn)
    {
        var mappings = new (string[] RawExts, string Label)[]
        {
            (new[] { "WEBM", "MP4", "AVI", "MKV", "MOV", "WMV", "FLV", "M4V", "3GP", "MPG", "MPEG", "TS" },
                "Video"),
            (new[] { "MP3", "WAV", "FLAC", "M4A", "AAC", "OGG", "WMA", "OPUS", "APE" },
                "Audio"),

            (new[] { "XLS", "XLSX", "ODS", "CSV" },
                "Excel"),
            (new[] { "DOC", "DOCX", "ODT" },
                "Word"),
            (new[] { "PPT", "PPTX", "ODP" },
                "PowerPoint"),
            (new[] { "JSON", "XML", "YAML", "YML", "TSV" },
                "Data"),
            (new[] { "PY", "IPYNB", "JS", "HTML", "HTM", "CSS", "JAVA", "CS", "GO",
                     "RS", "PHP", "SH", "BAT", "PS1", "SQL", "CPP", "C", "VB", "KT", "RB" },
                "Code"),
            (new[] { "EPUB", "MOBI", "AZW", "AZW3", "FB2" },
                "Book"),
            (new[] { "JPG", "JPEG", "PNG", "GIF", "BMP", "ICO", "TIFF", "TIF", "WEBP", "SVG", "RAW", "HEIC", "HEIF" },
                "Image"),
            (new[] { "ZIP", "RAR", "7Z", "TAR", "GZ", "BZ2", "XZ", "ZST" },
                "Archive"),
            (new[] { "PSD", "AI", "XD", "FIG", "SKETCH", "INDD" },
                "Design"),
        };

        foreach (var (rawExts, label) in mappings)
        {
            var paramNames = rawExts.Select((_, i) => $"@ext{i}").ToList();
            var inClause = string.Join(", ", paramNames);

            var sql = $"""
                UPDATE documents
                SET type = @label
                WHERE (is_deleted IS NULL OR is_deleted = 0)
                  AND (
                    UPPER(type) IN ({inClause})
                    OR LOWER(type) IN ({string.Join(", ", rawExts.Select((e, i) => $"@extL{i}"))})
                  )
                """;

            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@label", label);
            for (int i = 0; i < rawExts.Length; i++)
            {
                cmd.Parameters.AddWithValue($"@ext{i}", rawExts[i]);
                cmd.Parameters.AddWithValue($"@extL{i}", rawExts[i].ToLowerInvariant());
            }
            cmd.ExecuteNonQuery();
        }

        var pathMappings = new (string[] Exts, string Label)[]
        {
            (new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".webm",
                     ".flv", ".m4v", ".3gp", ".mpg", ".mpeg" },       "Video"),
            (new[] { ".mp3", ".wav", ".flac", ".m4a", ".aac",
                     ".ogg", ".wma", ".opus", ".ape" },                "Audio"),
            (new[] { ".pdf" },                                          "PDF"),
            (new[] { ".doc", ".docx", ".odt" },                        "Word"),
            (new[] { ".xls", ".xlsx", ".ods", ".csv" },                "Excel"),
            (new[] { ".ppt", ".pptx", ".odp" },                        "PowerPoint"),
            (new[] { ".txt", ".md", ".rtf" },                          "Document"),
            (new[] { ".json", ".xml", ".yaml", ".yml", ".tsv" },       "Data"),
            (new[] { ".py", ".ipynb", ".js", ".html", ".htm", ".css",
                     ".java", ".cs", ".go", ".rs", ".php",
                     ".sh", ".bat", ".ps1", ".sql", ".cpp", ".c",
                     ".vb", ".kt", ".rb" },                             "Code"),
            (new[] { ".epub", ".mobi", ".azw", ".azw3", ".fb2" },      "Book"),
            (new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico",
                     ".tiff", ".tif", ".webp", ".svg", ".raw",
                     ".heic", ".heif" },                                "Image"),
            (new[] { ".zip", ".rar", ".7z", ".tar", ".gz",
                     ".bz2", ".xz", ".zst" },                           "Archive"),
            (new[] { ".psd", ".ai", ".xd", ".fig", ".sketch", ".indd" }, "Design"),
        };

        int pIdx = 0;
        foreach (var (exts, label) in pathMappings)
        {
            var likeParams = new List<string>();
            var likeValues = new List<string>();
            foreach (var ext in exts)
            {
                likeParams.Add($"@pp{pIdx}");
                likeValues.Add($"%{ext}");
                pIdx++;
            }

            var whereLike = string.Join(" OR ",
                likeParams.Select(p => $"LOWER(file_path) LIKE {p}"));

            var labelParam = $"@plbl{pIdx}";

            var pathSql = $"""
                UPDATE documents
                SET type = {labelParam}
                WHERE (is_deleted IS NULL OR is_deleted = 0)
                  AND file_path IS NOT NULL
                  AND file_path != ''
                  AND ({whereLike})
                """;

            using var pathCmd = new SqliteCommand(pathSql, conn);
            pathCmd.Parameters.AddWithValue(labelParam, label);
            int startIdx = pIdx - exts.Length;
            for (int i = 0; i < exts.Length; i++)
                pathCmd.Parameters.AddWithValue($"@pp{startIdx + i}", likeValues[i]);

            pathCmd.ExecuteNonQuery();
            pIdx++;
        }

        using var reseed = new SqliteCommand(
            "INSERT OR IGNORE INTO document_types (name) SELECT DISTINCT type FROM documents WHERE type IS NOT NULL AND type != ''",
            conn);
        reseed.ExecuteNonQuery();
    }

    private static void MigrateNeutralizeLabels(SqliteConnection conn)
    {
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT value FROM app_settings WHERE key = 'schema_version'";
        var currentVersion = checkCmd.ExecuteScalar()?.ToString();
        if (int.TryParse(currentVersion, out var ver) && ver >= 3)
            return;

        var categoryMap = new (string Old, string New)[]
        {
            ("Công việc", "Work"), ("Cá nhân", "Personal"),
            ("Học tập", "Study"), ("Dự án", "Project"),
            ("Tài chính", "Finance"), ("Hợp đồng", "Contract"),
            ("Tham khảo", "Reference"), ("Khác", "Other")
        };

        var typeMap = new (string Old, string New)[]
        {
            ("Tài liệu", "Document"), ("Báo cáo", "Report"),
            ("Hướng dẫn", "Guide"), ("Biểu mẫu", "Form"),
            ("Dữ liệu", "Data"), ("Sách", "Book"),
            ("Thiết kế", "Design"), ("Hình ảnh", "Image"),
            ("Nén", "Archive"), ("Khác", "Other")
        };

        using var tx = conn.BeginTransaction();
        try
        {
            foreach (var (oldVal, newVal) in categoryMap)
            {
                using var docCmd = conn.CreateCommand();
                docCmd.Transaction = tx;
                docCmd.CommandText = "UPDATE documents SET subject = @new WHERE subject = @old";
                docCmd.Parameters.AddWithValue("@old", oldVal);
                docCmd.Parameters.AddWithValue("@new", newVal);
                docCmd.ExecuteNonQuery();

                using var chk = conn.CreateCommand();
                chk.Transaction = tx;
                chk.CommandText = "SELECT COUNT(*) FROM categories WHERE name = @new";
                chk.Parameters.AddWithValue("@new", newVal);
                var exists = Convert.ToInt64(chk.ExecuteScalar()) > 0;

                if (exists)
                {
                    using var del = conn.CreateCommand();
                    del.Transaction = tx;
                    del.CommandText = "DELETE FROM categories WHERE name = @old";
                    del.Parameters.AddWithValue("@old", oldVal);
                    del.ExecuteNonQuery();
                }
                else
                {
                    using var ren = conn.CreateCommand();
                    ren.Transaction = tx;
                    ren.CommandText = "UPDATE categories SET name = @new WHERE name = @old";
                    ren.Parameters.AddWithValue("@old", oldVal);
                    ren.Parameters.AddWithValue("@new", newVal);
                    ren.ExecuteNonQuery();
                }
            }

            foreach (var (oldVal, newVal) in typeMap)
            {
                using var docCmd = conn.CreateCommand();
                docCmd.Transaction = tx;
                docCmd.CommandText = "UPDATE documents SET type = @new WHERE type = @old";
                docCmd.Parameters.AddWithValue("@old", oldVal);
                docCmd.Parameters.AddWithValue("@new", newVal);
                docCmd.ExecuteNonQuery();

                using var chk = conn.CreateCommand();
                chk.Transaction = tx;
                chk.CommandText = "SELECT COUNT(*) FROM document_types WHERE name = @new";
                chk.Parameters.AddWithValue("@new", newVal);
                var exists = Convert.ToInt64(chk.ExecuteScalar()) > 0;

                if (exists)
                {
                    using var del = conn.CreateCommand();
                    del.Transaction = tx;
                    del.CommandText = "DELETE FROM document_types WHERE name = @old";
                    del.Parameters.AddWithValue("@old", oldVal);
                    del.ExecuteNonQuery();
                }
                else
                {
                    using var ren = conn.CreateCommand();
                    ren.Transaction = tx;
                    ren.CommandText = "UPDATE document_types SET name = @new WHERE name = @old";
                    ren.Parameters.AddWithValue("@old", oldVal);
                    ren.Parameters.AddWithValue("@new", newVal);
                    ren.ExecuteNonQuery();
                }
            }

            using var verCmd = conn.CreateCommand();
            verCmd.Transaction = tx;
            verCmd.CommandText = "INSERT OR REPLACE INTO app_settings (key, value) VALUES ('schema_version', '3')";
            verCmd.ExecuteNonQuery();

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static void MigrateWriteSchemaVersion4(SqliteConnection conn)
    {
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT value FROM app_settings WHERE key = 'schema_version'";
        var currentVersion = checkCmd.ExecuteScalar()?.ToString();
        if (int.TryParse(currentVersion, out var ver) && ver >= 4)
            return;

        using var cmd = new SqliteCommand("INSERT OR REPLACE INTO app_settings (key, value) VALUES ('schema_version', '4')", conn);
        cmd.ExecuteNonQuery();
    }
}
