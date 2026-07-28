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
    public static void RunMigrations(string connectionString)
    {
        const string createTablesQuery = """
            CREATE TABLE IF NOT EXISTS documents (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
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
                deleted_at DATETIME
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
                content TEXT,
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

        using (var cmd = new SqliteCommand(createTablesQuery, conn))
        {
            cmd.ExecuteNonQuery();
        }

        MigrateAddColumn(conn, "documents", "is_deleted", "INTEGER DEFAULT 0");
        MigrateAddColumn(conn, "documents", "deleted_at", "DATETIME");
        MigrateSeedCategories(conn);
        MigrateNormalizeFileTypes(conn);
        MigrateNeutralizeLabels(conn);
    }

    private static void MigrateAddColumn(SqliteConnection conn, string table, string column, string type)
    {
        try
        {
            using var cmd = new SqliteCommand($"ALTER TABLE {table} ADD COLUMN {column} {type}", conn);
            cmd.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
        }
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
}
