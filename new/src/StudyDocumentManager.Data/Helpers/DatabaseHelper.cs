using System.Data;
using System.Linq;
using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core.DTOs;
using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Data.Helpers;

/// <summary>
/// Static helper for all SQLite database operations.
/// Ported from WinForms System.Data.SQLite to Microsoft.Data.Sqlite.
/// SQL queries and schema preserved for backward compatibility.
/// </summary>
public static class DatabaseHelper
{
    private static string? _databasePath;
    private static string? _connectionString;

    /// <summary>
    /// Path to SQLite database file
    /// </summary>
    public static string DatabasePath
    {
        get
        {
            if (string.IsNullOrEmpty(_databasePath))
            {
                string appFolder = AppDomain.CurrentDomain.BaseDirectory;
                _databasePath = Path.Combine(appFolder, "data", "study_documents.db");
            }
            return _databasePath;
        }
    }

    /// <summary>
    /// SQLite connection string (Microsoft.Data.Sqlite format)
    /// </summary>
    public static string ConnectionString
    {
        get
        {
            _connectionString ??= $"Data Source={DatabasePath}";
            return _connectionString;
        }
    }

    /// <summary>
    /// Override the database path (useful for testing with isolated temp files).
    /// Must be called BEFORE InitializeDatabase().
    /// </summary>
    public static void SetDatabasePath(string path)
    {
        _databasePath = path;
        _connectionString = $"Data Source={path}";
    }

    // ═══════════════════════════════════════════════════
    // Initialization & Migration
    // ═══════════════════════════════════════════════════

    public static void InitializeDatabase()
    {
        try
        {
            string? dataFolder = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(dataFolder) && !Directory.Exists(dataFolder))
            {
                Directory.CreateDirectory(dataFolder);
            }

            CreateTables();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Database initialization error: {ex.Message}");
            throw;
        }
    }

    private static void CreateTables()
    {
        const string createTablesQuery = """
            CREATE TABLE IF NOT EXISTS tai_lieu (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                ten TEXT NOT NULL,
                mon_hoc TEXT,
                loai TEXT,
                duong_dan TEXT,
                ghi_chu TEXT,
                ngay_them DATETIME DEFAULT (datetime('now', 'localtime')),
                kich_thuoc REAL,
                tac_gia TEXT,
                quan_trong INTEGER DEFAULT 0,
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
                FOREIGN KEY (document_id) REFERENCES tai_lieu(id) ON DELETE CASCADE,
                UNIQUE(collection_id, document_id)
            );

            CREATE TABLE IF NOT EXISTS personal_notes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                document_id INTEGER NOT NULL,
                content TEXT,
                created_at DATETIME DEFAULT (datetime('now', 'localtime')),
                updated_at DATETIME DEFAULT (datetime('now', 'localtime')),
                FOREIGN KEY (document_id) REFERENCES tai_lieu(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS recent_files (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                document_id INTEGER NOT NULL UNIQUE,
                opened_at DATETIME DEFAULT (datetime('now','localtime')),
                FOREIGN KEY (document_id) REFERENCES tai_lieu(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS document_relations (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                doc_id_1 INTEGER NOT NULL,
                doc_id_2 INTEGER NOT NULL,
                relation_type TEXT DEFAULT 'related',
                created_at DATETIME DEFAULT (datetime('now','localtime')),
                FOREIGN KEY (doc_id_1) REFERENCES tai_lieu(id) ON DELETE CASCADE,
                FOREIGN KEY (doc_id_2) REFERENCES tai_lieu(id) ON DELETE CASCADE,
                UNIQUE(doc_id_1, doc_id_2)
            );

            CREATE TABLE IF NOT EXISTS danh_muc (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                ten TEXT NOT NULL UNIQUE,
                created_at DATETIME DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS loai_tai_lieu (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                ten TEXT NOT NULL UNIQUE,
                created_at DATETIME DEFAULT (datetime('now','localtime'))
            );

            CREATE INDEX IF NOT EXISTS idx_tai_lieu_mon_hoc ON tai_lieu(mon_hoc);
            CREATE INDEX IF NOT EXISTS idx_tai_lieu_loai ON tai_lieu(loai);
            CREATE INDEX IF NOT EXISTS idx_tai_lieu_ngay_them ON tai_lieu(ngay_them);
            CREATE INDEX IF NOT EXISTS idx_tai_lieu_deadline ON tai_lieu(deadline);
            CREATE INDEX IF NOT EXISTS idx_collection_items_collection ON collection_items(collection_id);
            CREATE INDEX IF NOT EXISTS idx_collection_items_document ON collection_items(document_id);
            CREATE INDEX IF NOT EXISTS idx_tai_lieu_deleted ON tai_lieu(is_deleted);
            CREATE INDEX IF NOT EXISTS idx_tai_lieu_quan_trong ON tai_lieu(quan_trong);
            """;

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using (var cmd = new SqliteCommand(createTablesQuery, conn))
        {
            cmd.ExecuteNonQuery();
        }

        // Migrations
        MigrateAddColumn(conn, "tai_lieu", "is_deleted", "INTEGER DEFAULT 0");
        MigrateAddColumn(conn, "tai_lieu", "deleted_at", "DATETIME");

        // Seed danh_muc and loai_tai_lieu from existing data
        MigrateSeedCategories(conn);

        // Normalize legacy raw-extension loai values (e.g. 'WEBM' → 'Video')
        MigrateNormalizeFileTypes(conn);
    }

    /// <summary>
    /// Auto-seed danh_muc and loai_tai_lieu tables from existing distinct values in tai_lieu.
    /// Safe to call multiple times — uses INSERT OR IGNORE.
    /// </summary>
    private static void MigrateSeedCategories(SqliteConnection conn)
    {
        // Seed from existing document data (migration from old schema)
        using var cmd1 = new SqliteCommand(
            "INSERT OR IGNORE INTO danh_muc (ten) SELECT DISTINCT mon_hoc FROM tai_lieu WHERE mon_hoc IS NOT NULL AND mon_hoc != ''", conn);
        cmd1.ExecuteNonQuery();

        using var cmd2 = new SqliteCommand(
            "INSERT OR IGNORE INTO loai_tai_lieu (ten) SELECT DISTINCT loai FROM tai_lieu WHERE loai IS NOT NULL AND loai != ''", conn);
        cmd2.ExecuteNonQuery();

        // Seed default categories if tables are still empty (fresh install)
        var defaultSubjects = new[]
        {
            "Công việc", "Cá nhân", "Học tập", "Dự án",
            "Tài chính", "Hợp đồng", "Tham khảo", "Khác"
        };
        var defaultTypes = new[]
        {
            // Office
            "PDF", "Word", "Excel", "PowerPoint",
            // Generic document
            "Tài liệu", "Báo cáo", "Hướng dẫn", "Biểu mẫu",
            // New specific categories
            "Dữ liệu", "Code", "Sách", "Thiết kế",
            // Media & archive
            "Hình ảnh", "Video", "Audio", "Nén",
            // Catch-all
            "Khác"
        };

        foreach (var s in defaultSubjects)
        {
            using var ins = new SqliteCommand("INSERT OR IGNORE INTO danh_muc (ten) VALUES (@ten)", conn);
            ins.Parameters.AddWithValue("@ten", s);
            ins.ExecuteNonQuery();
        }

        foreach (var t in defaultTypes)
        {
            using var ins = new SqliteCommand("INSERT OR IGNORE INTO loai_tai_lieu (ten) VALUES (@ten)", conn);
            ins.Parameters.AddWithValue("@ten", t);
            ins.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Normalizes legacy raw-extension values stored in loai column.
    /// Old code stored e.g. 'WEBM', 'CSV', 'XLSX' directly as the type.
    /// This migration converts them to proper labels matching FileTypeDetector output.
    /// Safe to call multiple times — each UPDATE only affects matching rows.
    /// </summary>
    private static void MigrateNormalizeFileTypes(SqliteConnection conn)
    {
        // Map: (SQL IN-list of uppercase raw exts) → proper label
        var mappings = new (string[] RawExts, string Label)[]
        {
            (new[] { "WEBM", "MP4", "AVI", "MKV", "MOV", "WMV", "FLV", "M4V", "3GP", "MPG", "MPEG", "TS" },
                "Video"),
            (new[] { "MP3", "WAV", "FLAC", "M4A", "AAC", "OGG", "WMA", "OPUS", "APE" },
                "Audio"),
            // CSV goes to Excel group (same as FileTypeDetector)
            (new[] { "XLS", "XLSX", "ODS", "CSV" },
                "Excel"),
            (new[] { "DOC", "DOCX", "ODT" },
                "Word"),
            (new[] { "PPT", "PPTX", "ODP" },
                "PowerPoint"),
            (new[] { "JSON", "XML", "YAML", "YML", "TSV" },
                "Dữ liệu"),
            (new[] { "PY", "IPYNB", "JS", "HTML", "HTM", "CSS", "JAVA", "CS", "GO",
                     "RS", "PHP", "SH", "BAT", "PS1", "SQL", "CPP", "C", "VB", "KT", "RB" },
                "Code"),
            (new[] { "EPUB", "MOBI", "AZW", "AZW3", "FB2" },
                "Sách"),
            (new[] { "JPG", "JPEG", "PNG", "GIF", "BMP", "ICO", "TIFF", "TIF", "WEBP", "SVG", "RAW", "HEIC", "HEIF" },
                "Hình ảnh"),
            (new[] { "ZIP", "RAR", "7Z", "TAR", "GZ", "BZ2", "XZ", "ZST" },
                "Nén"),
            (new[] { "PSD", "AI", "XD", "FIG", "SKETCH", "INDD" },
                "Thiết kế"),
        };

        foreach (var (rawExts, label) in mappings)
        {
            // Build parameterized IN clause
            var paramNames = rawExts.Select((_, i) => $"@ext{i}").ToList();
            var inClause = string.Join(", ", paramNames);

            // Match both bare uppercase extensions (e.g. 'WEBM') and
            // dot-prefixed lowercase forms (e.g. '.webm') that may have been stored
            var sql = $"""
                UPDATE tai_lieu
                SET loai = @label
                WHERE (is_deleted IS NULL OR is_deleted = 0)
                  AND (
                    UPPER(loai) IN ({inClause})
                    OR LOWER(loai) IN ({string.Join(", ", rawExts.Select((e, i) => $"@extL{i}"))})
                  )
                """;

            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@label", label);
            for (int i = 0; i < rawExts.Length; i++)
            {
                cmd.Parameters.AddWithValue($"@ext{i}", rawExts[i]);           // UPPERCASE
                cmd.Parameters.AddWithValue($"@extL{i}", rawExts[i].ToLowerInvariant()); // lowercase
            }
            cmd.ExecuteNonQuery();
        }

        // ── Phase 2: Re-detect from duong_dan file path extension ────────────
        // Fixes records where loai is ANY wrong value (e.g. 'Audio' for a .csv or .webm).
        // This is the authoritative pass — file path determines type.
        var pathMappings = new (string[] Exts, string Label)[]
        {
            // Video checked first — .webm never misclassified as audio
            (new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".webm",
                     ".flv", ".m4v", ".3gp", ".mpg", ".mpeg" },       "Video"),
            (new[] { ".mp3", ".wav", ".flac", ".m4a", ".aac",
                     ".ogg", ".wma", ".opus", ".ape" },                "Audio"),
            (new[] { ".pdf" },                                          "PDF"),
            (new[] { ".doc", ".docx", ".odt" },                        "Word"),
            (new[] { ".xls", ".xlsx", ".ods", ".csv" },                "Excel"),
            (new[] { ".ppt", ".pptx", ".odp" },                        "PowerPoint"),
            (new[] { ".txt", ".md", ".rtf" },                          "Tài liệu"),
            (new[] { ".json", ".xml", ".yaml", ".yml", ".tsv" },       "Dữ liệu"),
            (new[] { ".py", ".ipynb", ".js", ".html", ".htm", ".css",
                     ".java", ".cs", ".go", ".rs", ".php",
                     ".sh", ".bat", ".ps1", ".sql", ".cpp", ".c",
                     ".vb", ".kt", ".rb" },                             "Code"),
            (new[] { ".epub", ".mobi", ".azw", ".azw3", ".fb2" },      "Sách"),
            (new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico",
                     ".tiff", ".tif", ".webp", ".svg", ".raw",
                     ".heic", ".heif" },                                "Hình ảnh"),
            (new[] { ".zip", ".rar", ".7z", ".tar", ".gz",
                     ".bz2", ".xz", ".zst" },                           "Nén"),
            (new[] { ".psd", ".ai", ".xd", ".fig", ".sketch", ".indd" }, "Thiết kế"),
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
                likeParams.Select(p => $"LOWER(duong_dan) LIKE {p}"));

            var labelParam = $"@plbl{pIdx}";

            var pathSql = $"""
                UPDATE tai_lieu
                SET loai = {labelParam}
                WHERE (is_deleted IS NULL OR is_deleted = 0)
                  AND duong_dan IS NOT NULL
                  AND duong_dan != ''
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

        // Re-seed lookup table with any newly produced label values
        using var reseed = new SqliteCommand(
            "INSERT OR IGNORE INTO loai_tai_lieu (ten) SELECT DISTINCT loai FROM tai_lieu WHERE loai IS NOT NULL AND loai != ''",
            conn);
        reseed.ExecuteNonQuery();
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
            // Column already exists — ignore
        }
    }

    // ═══════════════════════════════════════════════════
    // Document CRUD
    // ═══════════════════════════════════════════════════

    public static List<StudyDocument> GetAllDocuments()
    {
        const string query = "SELECT * FROM tai_lieu WHERE (is_deleted IS NULL OR is_deleted = 0) ORDER BY ngay_them DESC";
        return ExecuteReader(query);
    }

    public static StudyDocument? GetDocumentById(int id)
    {
        const string query = "SELECT * FROM tai_lieu WHERE id = @id";
        var results = ExecuteReader(query, new SqliteParameter("@id", id));
        return results.Count > 0 ? results[0] : null;
    }

    public static List<StudyDocument> SearchDocuments(string keyword)
    {
        const string query = """
            SELECT * FROM tai_lieu
            WHERE (is_deleted IS NULL OR is_deleted = 0)
            AND (ten LIKE @keyword OR mon_hoc LIKE @keyword OR ghi_chu LIKE @keyword OR tac_gia LIKE @keyword OR tags LIKE @keyword)
            ORDER BY ngay_them DESC
            """;
        return ExecuteReader(query, new SqliteParameter("@keyword", $"%{keyword}%"));
    }

    public static List<StudyDocument> FilterDocuments(string subject, string type)
    {
        var query = "SELECT * FROM tai_lieu WHERE (is_deleted IS NULL OR is_deleted = 0)";
        var parameters = new List<SqliteParameter>();

        if (!string.IsNullOrEmpty(subject) && subject != "Tất cả")
        {
            query += " AND mon_hoc = @mon_hoc";
            parameters.Add(new SqliteParameter("@mon_hoc", subject));
        }

        if (!string.IsNullOrEmpty(type) && type != "Tất cả")
        {
            query += " AND loai = @loai";
            parameters.Add(new SqliteParameter("@loai", type));
        }

        query += " ORDER BY ngay_them DESC";
        return ExecuteReader(query, parameters.ToArray());
    }

    public static List<StudyDocument> SearchDocumentsAdvanced(
        string? keyword, string? subject, string? type,
        DateTime? fromDate, DateTime? toDate,
        double? minSize, double? maxSize, bool? isImportant)
    {
        var query = "SELECT * FROM tai_lieu WHERE (is_deleted IS NULL OR is_deleted = 0)";
        var parameters = new List<SqliteParameter>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query += " AND (ten LIKE @keyword OR mon_hoc LIKE @keyword OR ghi_chu LIKE @keyword OR tags LIKE @keyword)";
            parameters.Add(new SqliteParameter("@keyword", $"%{keyword}%"));
        }

        if (!string.IsNullOrEmpty(subject) && subject != "Tất cả")
        {
            query += " AND mon_hoc = @mon_hoc";
            parameters.Add(new SqliteParameter("@mon_hoc", subject));
        }

        if (!string.IsNullOrEmpty(type) && type != "Tất cả")
        {
            query += " AND loai = @loai";
            parameters.Add(new SqliteParameter("@loai", type));
        }

        if (fromDate.HasValue)
        {
            query += " AND date(ngay_them) >= date(@fromDate)";
            parameters.Add(new SqliteParameter("@fromDate", fromDate.Value.ToString("yyyy-MM-dd")));
        }

        if (toDate.HasValue)
        {
            query += " AND date(ngay_them) <= date(@toDate)";
            parameters.Add(new SqliteParameter("@toDate", toDate.Value.ToString("yyyy-MM-dd")));
        }

        if (minSize.HasValue)
        {
            query += " AND kich_thuoc >= @minSize";
            parameters.Add(new SqliteParameter("@minSize", minSize.Value));
        }

        if (maxSize.HasValue)
        {
            query += " AND kich_thuoc <= @maxSize";
            parameters.Add(new SqliteParameter("@maxSize", maxSize.Value));
        }

        if (isImportant is true)
        {
            query += " AND quan_trong = 1";
        }

        query += " ORDER BY ngay_them DESC";
        return ExecuteReader(query, parameters.ToArray());
    }

    public static bool InsertDocument(StudyDocument doc)
    {
        const string query = """
            INSERT INTO tai_lieu (ten, mon_hoc, loai, duong_dan, ghi_chu, kich_thuoc, tac_gia, quan_trong, tags, deadline)
            VALUES (@ten, @mon_hoc, @loai, @duong_dan, @ghi_chu, @kich_thuoc, @tac_gia, @quan_trong, @tags, @deadline)
            """;
        return ExecuteNonQuery(query, BuildDocumentParameters(doc)) > 0;
    }

    public static bool UpdateDocument(StudyDocument doc)
    {
        const string query = """
            UPDATE tai_lieu SET
                ten = @ten, mon_hoc = @mon_hoc, loai = @loai, duong_dan = @duong_dan,
                ghi_chu = @ghi_chu, kich_thuoc = @kich_thuoc, tac_gia = @tac_gia,
                quan_trong = @quan_trong, tags = @tags, deadline = @deadline
            WHERE id = @id
            """;
        var parameters = BuildDocumentParameters(doc).ToList();
        parameters.Add(new SqliteParameter("@id", doc.Id));
        return ExecuteNonQuery(query, parameters.ToArray()) > 0;
    }

    public static bool DeleteDocument(int id)
    {
        const string query = "UPDATE tai_lieu SET is_deleted = 1, deleted_at = datetime('now','localtime') WHERE id = @id";
        return ExecuteNonQuery(query, new SqliteParameter("@id", id)) > 0;
    }

    // ═══════════════════════════════════════════════════
    // Distinct Values & Statistics
    // ═══════════════════════════════════════════════════

    public static List<string> GetDistinctSubjects()
    {
        const string query = "SELECT DISTINCT mon_hoc FROM tai_lieu WHERE mon_hoc IS NOT NULL AND mon_hoc != '' AND (is_deleted IS NULL OR is_deleted = 0) ORDER BY mon_hoc";
        return ExecuteStringList(query, "mon_hoc");
    }

    public static List<string> GetDistinctTypes()
    {
        const string query = "SELECT DISTINCT loai FROM tai_lieu WHERE loai IS NOT NULL AND loai != '' AND (is_deleted IS NULL OR is_deleted = 0) ORDER BY loai";
        return ExecuteStringList(query, "loai");
    }

    public static List<string> GetDistinctTags()
    {
        const string query = "SELECT DISTINCT tags FROM tai_lieu WHERE tags IS NOT NULL AND tags != '' AND (is_deleted IS NULL OR is_deleted = 0)";
        var allTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = new SqliteCommand(query, conn);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var tagsString = reader.GetString(0);
            foreach (var tag in tagsString.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = tag.Trim().ToLower();
                if (!string.IsNullOrEmpty(trimmed))
                    allTags.Add(trimmed);
            }
        }

        var sortedTags = allTags.ToList();
        sortedTags.Sort();
        return sortedTags;
    }

    public static List<StudyDocument> GetUpcomingDeadlines(int days = 7)
    {
        const string query = """
            SELECT * FROM tai_lieu
            WHERE deadline IS NOT NULL
            AND (is_deleted IS NULL OR is_deleted = 0)
            AND date(deadline) >= date('now', 'localtime')
            AND date(deadline) <= date('now', 'localtime', '+' || @days || ' days')
            ORDER BY deadline ASC
            """;
        return ExecuteReader(query, new SqliteParameter("@days", days));
    }

    public static List<StudyDocument> GetOverdueDocuments()
    {
        const string query = """
            SELECT * FROM tai_lieu
            WHERE deadline IS NOT NULL
            AND (is_deleted IS NULL OR is_deleted = 0)
            AND date(deadline) < date('now', 'localtime')
            ORDER BY deadline ASC
            """;
        return ExecuteReader(query);
    }

    public static DashboardStats GetDashboardStatistics()
    {
        var stats = new DashboardStats();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        stats.TotalDocuments = GetScalarInt(conn, "SELECT COUNT(*) FROM tai_lieu WHERE (is_deleted IS NULL OR is_deleted = 0)");
        stats.ImportantDocuments = GetScalarInt(conn, "SELECT COUNT(*) FROM tai_lieu WHERE quan_trong = 1 AND (is_deleted IS NULL OR is_deleted = 0)");
        stats.NoFileDocuments = GetScalarInt(conn, "SELECT COUNT(*) FROM tai_lieu WHERE (is_deleted IS NULL OR is_deleted = 0) AND (duong_dan IS NULL OR duong_dan = '')");
        stats.OverdueDocuments = GetScalarInt(conn, "SELECT COUNT(*) FROM tai_lieu WHERE deadline IS NOT NULL AND (is_deleted IS NULL OR is_deleted = 0) AND date(deadline) < date('now', 'localtime')");
        stats.NearDeadlineDocuments = GetScalarInt(conn, "SELECT COUNT(*) FROM tai_lieu WHERE deadline IS NOT NULL AND (is_deleted IS NULL OR is_deleted = 0) AND date(deadline) >= date('now', 'localtime') AND date(deadline) <= date('now', 'localtime', '+7 days')");
        stats.TotalCategories = GetScalarInt(conn, "SELECT COUNT(DISTINCT mon_hoc) FROM tai_lieu WHERE mon_hoc IS NOT NULL AND mon_hoc != '' AND (is_deleted IS NULL OR is_deleted = 0)");
        stats.TotalCollections = GetScalarInt(conn, "SELECT COUNT(*) FROM collections");

        return stats;
    }


    // ═══════════════════════════════════════════════════
    // Recycle Bin
    // ═══════════════════════════════════════════════════

    public static List<StudyDocument> GetDeletedDocuments()
    {
        const string query = "SELECT * FROM tai_lieu WHERE is_deleted = 1 ORDER BY deleted_at DESC";
        return ExecuteReader(query);
    }

    public static bool RestoreDocument(int id)
    {
        const string query = "UPDATE tai_lieu SET is_deleted = 0, deleted_at = NULL WHERE id = @id";
        return ExecuteNonQuery(query, new SqliteParameter("@id", id)) > 0;
    }

    public static bool PermanentDeleteDocument(int id)
    {
        const string query = "DELETE FROM tai_lieu WHERE id = @id";
        return ExecuteNonQuery(query, new SqliteParameter("@id", id)) > 0;
    }

    // ═══════════════════════════════════════════════════
    // Backup
    // ═══════════════════════════════════════════════════

    public static bool BackupDatabase(string destPath)
    {
        try
        {
            File.Copy(DatabasePath, destPath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ═══════════════════════════════════════════════════
    // Internal Helpers
    // ═══════════════════════════════════════════════════

    private static List<StudyDocument> ExecuteReader(string query, params SqliteParameter[] parameters)
    {
        var documents = new List<StudyDocument>();

        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = new SqliteCommand(query, conn);

        foreach (var param in parameters)
            cmd.Parameters.Add(param);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            documents.Add(MapToDocument(reader));
        }

        return documents;
    }

    private static int ExecuteNonQuery(string query, params SqliteParameter[] parameters)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = new SqliteCommand(query, conn);

        foreach (var param in parameters)
            cmd.Parameters.Add(param);

        return cmd.ExecuteNonQuery();
    }

    private static List<string> ExecuteStringList(string query, string columnName)
    {
        var result = new List<string>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = new SqliteCommand(query, conn);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var value = reader[columnName]?.ToString();
            if (!string.IsNullOrEmpty(value))
                result.Add(value);
        }

        return result;
    }

    private static int GetScalarInt(SqliteConnection conn, string query)
    {
        using var cmd = new SqliteCommand(query, conn);
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    private static StudyDocument MapToDocument(SqliteDataReader reader)
    {
        return new StudyDocument
        {
            Id = reader.GetInt32(reader.GetOrdinal("id")),
            Ten = reader["ten"]?.ToString() ?? string.Empty,
            MonHoc = reader["mon_hoc"]?.ToString() ?? string.Empty,
            Loai = reader["loai"]?.ToString() ?? string.Empty,
            DuongDan = reader["duong_dan"]?.ToString() ?? string.Empty,
            GhiChu = reader["ghi_chu"]?.ToString() ?? string.Empty,
            NgayThem = reader["ngay_them"] is DBNull ? DateTime.Now : DateTime.Parse(reader["ngay_them"].ToString()!),
            KichThuoc = reader["kich_thuoc"] is DBNull ? null : Convert.ToDouble(reader["kich_thuoc"]),
            TacGia = reader["tac_gia"]?.ToString() ?? string.Empty,
            QuanTrong = reader["quan_trong"] is not DBNull && Convert.ToInt32(reader["quan_trong"]) == 1,
            Tags = reader["tags"]?.ToString() ?? string.Empty,
            Deadline = reader["deadline"] is DBNull ? null : DateTime.Parse(reader["deadline"].ToString()!)
        };
    }

    private static SqliteParameter[] BuildDocumentParameters(StudyDocument doc)
    {
        return
        [
            new SqliteParameter("@ten", doc.Ten),
            new SqliteParameter("@mon_hoc", string.IsNullOrEmpty(doc.MonHoc) ? DBNull.Value : doc.MonHoc),
            new SqliteParameter("@loai", string.IsNullOrEmpty(doc.Loai) ? DBNull.Value : doc.Loai),
            new SqliteParameter("@duong_dan", string.IsNullOrEmpty(doc.DuongDan) ? DBNull.Value : doc.DuongDan),
            new SqliteParameter("@ghi_chu", string.IsNullOrEmpty(doc.GhiChu) ? DBNull.Value : doc.GhiChu),
            new SqliteParameter("@kich_thuoc", doc.KichThuoc.HasValue ? doc.KichThuoc.Value : DBNull.Value),
            new SqliteParameter("@tac_gia", string.IsNullOrEmpty(doc.TacGia) ? DBNull.Value : doc.TacGia),
            new SqliteParameter("@quan_trong", doc.QuanTrong ? 1 : 0),
            new SqliteParameter("@tags", string.IsNullOrEmpty(doc.Tags) ? DBNull.Value : doc.Tags),
            new SqliteParameter("@deadline", doc.Deadline.HasValue ? doc.Deadline.Value.ToString("yyyy-MM-dd HH:mm:ss") : DBNull.Value)
        ];
    }

    // ═══════════════════════════════════════════════════
    // Personal Notes
    // ═══════════════════════════════════════════════════

    public static string? GetPersonalNote(int documentId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT content FROM personal_notes WHERE document_id = @documentId";
        cmd.Parameters.AddWithValue("@documentId", documentId);
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    public static bool SavePersonalNote(int documentId, string content)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM personal_notes WHERE document_id = @documentId";
        checkCmd.Parameters.AddWithValue("@documentId", documentId);
        var count = Convert.ToInt32(checkCmd.ExecuteScalar());

        using var cmd = conn.CreateCommand();
        if (count > 0)
        {
            cmd.CommandText = @"UPDATE personal_notes
                                SET content = @content, updated_at = datetime('now', 'localtime')
                                WHERE document_id = @documentId";
        }
        else
        {
            cmd.CommandText = @"INSERT INTO personal_notes (document_id, content)
                                VALUES (@documentId, @content)";
        }
        cmd.Parameters.AddWithValue("@documentId", documentId);
        cmd.Parameters.AddWithValue("@content", string.IsNullOrEmpty(content) ? DBNull.Value : content);
        return cmd.ExecuteNonQuery() > 0;
    }

    public static bool DeletePersonalNote(int documentId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM personal_notes WHERE document_id = @documentId";
        cmd.Parameters.AddWithValue("@documentId", documentId);
        return cmd.ExecuteNonQuery() > 0;
    }

    // ═══════════════════════════════════════════════════
    // Related Documents
    // ═══════════════════════════════════════════════════

    public static void AddDocumentRelation(int docId1, int docId2, string relationType = "related")
    {
        int lo = Math.Min(docId1, docId2);
        int hi = Math.Max(docId1, docId2);
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT OR IGNORE INTO document_relations (doc_id_1, doc_id_2, relation_type)
                            VALUES (@d1, @d2, @type)";
        cmd.Parameters.AddWithValue("@d1", lo);
        cmd.Parameters.AddWithValue("@d2", hi);
        cmd.Parameters.AddWithValue("@type", relationType);
        cmd.ExecuteNonQuery();
    }

    public static List<(StudyDocument Doc, int RelationId, string RelationType)> GetRelatedDocuments(int docId)
    {
        var results = new List<(StudyDocument, int, string)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT t.id, t.ten, t.mon_hoc, t.loai, t.duong_dan, r.relation_type, r.id as relation_id
                            FROM document_relations r
                            INNER JOIN tai_lieu t ON (t.id = CASE WHEN r.doc_id_1 = @docId THEN r.doc_id_2 ELSE r.doc_id_1 END)
                            WHERE (r.doc_id_1 = @docId OR r.doc_id_2 = @docId)
                            AND (t.is_deleted IS NULL OR t.is_deleted = 0)
                            ORDER BY t.ten";
        cmd.Parameters.AddWithValue("@docId", docId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var doc = new StudyDocument
            {
                Id = reader.GetInt32(0),
                Ten = reader.IsDBNull(1) ? "" : reader.GetString(1),
                MonHoc = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Loai = reader.IsDBNull(3) ? "" : reader.GetString(3),
                DuongDan = reader.IsDBNull(4) ? "" : reader.GetString(4)
            };
            var relationType = reader.IsDBNull(5) ? "related" : reader.GetString(5);
            var relationId = reader.GetInt32(6);
            results.Add((doc, relationId, relationType));
        }
        return results;
    }

    public static void RemoveDocumentRelation(int relationId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM document_relations WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", relationId);
        cmd.ExecuteNonQuery();
    }

    // ═══════════════════════════════════════════════════
    // Report Data — Charts
    // ═══════════════════════════════════════════════════

    public static List<(string Label, int Count)> GetDocumentsByDay(int days = 7)
    {
        var results = new List<(string, int)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            WITH RECURSIVE DateSeries(ngay) AS (
                SELECT date('now', 'localtime')
                UNION ALL
                SELECT date(ngay, '-1 day')
                FROM DateSeries
                WHERE date(ngay, '-1 day') >= date('now', 'localtime', '-' || (@days - 1) || ' days')
            )
            SELECT
                strftime('%d/%m', ds.ngay) as ngay_format,
                COALESCE(COUNT(t.id), 0) as so_luong
            FROM DateSeries ds
            LEFT JOIN tai_lieu t ON date(t.ngay_them) = ds.ngay
                AND (t.is_deleted IS NULL OR t.is_deleted = 0)
            GROUP BY ds.ngay
            ORDER BY ds.ngay ASC";
        cmd.Parameters.AddWithValue("@days", days);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add((reader.GetString(0), reader.GetInt32(1)));
        }
        return results;
    }

    public static List<(string Label, int Count)> GetDocumentsByMonth(int months = 12)
    {
        var results = new List<(string, int)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            WITH RECURSIVE MonthSeries(thang) AS (
                SELECT date('now', 'localtime', 'start of month')
                UNION ALL
                SELECT date(thang, '-1 month')
                FROM MonthSeries
                WHERE date(thang, '-1 month') >= date('now', 'localtime', 'start of month', '-' || (@months - 1) || ' months')
            )
            SELECT
                strftime('%m/%Y', ms.thang) as thang_format,
                COALESCE(COUNT(t.id), 0) as so_luong
            FROM MonthSeries ms
            LEFT JOIN tai_lieu t ON strftime('%Y-%m', t.ngay_them) = strftime('%Y-%m', ms.thang)
                AND (t.is_deleted IS NULL OR t.is_deleted = 0)
            GROUP BY ms.thang
            ORDER BY ms.thang ASC";
        cmd.Parameters.AddWithValue("@months", months);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add((reader.GetString(0), reader.GetInt32(1)));
        }
        return results;
    }

    public static List<(string Label, int Count)> GetDocumentsBySubject()
    {
        var results = new List<(string, int)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT COALESCE(mon_hoc, 'Không rõ'), COUNT(*)
                            FROM tai_lieu
                            WHERE (is_deleted IS NULL OR is_deleted = 0)
                            GROUP BY mon_hoc ORDER BY COUNT(*) DESC";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add((reader.GetString(0), reader.GetInt32(1)));
        }
        return results;
    }

    public static List<(string Label, int Count)> GetDocumentsByType()
    {
        var results = new List<(string, int)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT COALESCE(loai, 'Không rõ'), COUNT(*)
                            FROM tai_lieu
                            WHERE (is_deleted IS NULL OR is_deleted = 0)
                            GROUP BY loai ORDER BY COUNT(*) DESC";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add((reader.GetString(0), reader.GetInt32(1)));
        }
        return results;
    }

    // ═══════════════════════════════════════════════════
    // Collection CRUD
    // ═══════════════════════════════════════════════════

    public static List<(int Id, string Name, string? Description, DateTime CreatedAt, int ItemCount)> GetCollections()
    {
        var results = new List<(int, string, string?, DateTime, int)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT c.id, c.name, c.description, c.created_at,
                            (SELECT COUNT(*) FROM collection_items ci WHERE ci.collection_id = c.id) as item_count
                            FROM collections c
                            ORDER BY c.name";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetDateTime(3),
                reader.GetInt32(4)
            ));
        }
        return results;
    }

    public static int CreateCollection(string name, string? description = null)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO collections (name, description)
                            VALUES (@name, @description);
                            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@description", (object?)description ?? DBNull.Value);
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    public static bool UpdateCollection(int collectionId, string name, string? description = null)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"UPDATE collections SET name = @name, description = @description
                            WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", collectionId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@description", (object?)description ?? DBNull.Value);
        return cmd.ExecuteNonQuery() > 0;
    }

    public static bool DeleteCollection(int collectionId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        // Delete items first
        using (var delItems = conn.CreateCommand())
        {
            delItems.CommandText = "DELETE FROM collection_items WHERE collection_id = @id";
            delItems.Parameters.AddWithValue("@id", collectionId);
            delItems.ExecuteNonQuery();
        }
        // Delete collection
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM collections WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", collectionId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public static List<StudyDocument> GetDocumentsInCollection(int collectionId)
    {
        var results = new List<StudyDocument>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT t.*
                            FROM tai_lieu t
                            INNER JOIN collection_items ci ON t.id = ci.document_id
                            WHERE ci.collection_id = @collectionId
                            AND (t.is_deleted IS NULL OR t.is_deleted = 0)
                            ORDER BY ci.added_at DESC";
        cmd.Parameters.AddWithValue("@collectionId", collectionId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(MapToDocument(reader));
        }
        return results;
    }

    public static bool AddDocumentToCollection(int collectionId, int documentId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        // Check existing
        using (var check = conn.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(*) FROM collection_items WHERE collection_id = @colId AND document_id = @docId";
            check.Parameters.AddWithValue("@colId", collectionId);
            check.Parameters.AddWithValue("@docId", documentId);
            var exists = check.ExecuteScalar();
            if (exists != null && Convert.ToInt32(exists) > 0)
                return false; // Already exists
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO collection_items (collection_id, document_id) VALUES (@colId, @docId)";
        cmd.Parameters.AddWithValue("@colId", collectionId);
        cmd.Parameters.AddWithValue("@docId", documentId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public static bool RemoveDocumentFromCollection(int collectionId, int documentId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM collection_items WHERE collection_id = @colId AND document_id = @docId";
        cmd.Parameters.AddWithValue("@colId", collectionId);
        cmd.Parameters.AddWithValue("@docId", documentId);
        return cmd.ExecuteNonQuery() > 0;
    }

    // ═══════════════════════════════════════════════════
    // Category Management
    // ═══════════════════════════════════════════════════

    public static List<(string Name, int Count)> GetSubjectsWithCount()
    {
        var results = new List<(string, int)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        // UNION: standalone danh_muc table + implicit subjects from tai_lieu not in danh_muc
        cmd.CommandText = @"
            SELECT dm.ten, COUNT(t.id) as so_luong
            FROM danh_muc dm
            LEFT JOIN tai_lieu t ON t.mon_hoc = dm.ten
                AND (t.is_deleted IS NULL OR t.is_deleted = 0)
            GROUP BY dm.ten
            UNION
            SELECT t2.mon_hoc, COUNT(t2.id)
            FROM tai_lieu t2
            WHERE t2.mon_hoc IS NOT NULL AND t2.mon_hoc != ''
              AND (t2.is_deleted IS NULL OR t2.is_deleted = 0)
              AND t2.mon_hoc NOT IN (SELECT ten FROM danh_muc)
            GROUP BY t2.mon_hoc
            ORDER BY 1";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            results.Add((reader.GetString(0), reader.GetInt32(1)));
        return results;
    }

    public static List<(string Name, int Count)> GetTypesWithCount()
    {
        var results = new List<(string, int)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        // UNION: standalone loai_tai_lieu table + implicit types from tai_lieu not in loai_tai_lieu
        cmd.CommandText = @"
            SELECT ltt.ten, COUNT(t.id) as so_luong
            FROM loai_tai_lieu ltt
            LEFT JOIN tai_lieu t ON t.loai = ltt.ten
                AND (t.is_deleted IS NULL OR t.is_deleted = 0)
            GROUP BY ltt.ten
            UNION
            SELECT t2.loai, COUNT(t2.id)
            FROM tai_lieu t2
            WHERE t2.loai IS NOT NULL AND t2.loai != ''
              AND (t2.is_deleted IS NULL OR t2.is_deleted = 0)
              AND t2.loai NOT IN (SELECT ten FROM loai_tai_lieu)
            GROUP BY t2.loai
            ORDER BY 1";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            results.Add((reader.GetString(0), reader.GetInt32(1)));
        return results;
    }

    public static bool UpdateSubjectName(string oldName, string newName)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        // Update documents
        using var cmd1 = conn.CreateCommand();
        cmd1.CommandText = "UPDATE tai_lieu SET mon_hoc = @newName WHERE mon_hoc = @oldName AND (is_deleted IS NULL OR is_deleted = 0)";
        cmd1.Parameters.AddWithValue("@oldName", oldName);
        cmd1.Parameters.AddWithValue("@newName", newName);
        cmd1.ExecuteNonQuery();
        // Update lookup table
        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = "UPDATE danh_muc SET ten = @newName WHERE ten = @oldName";
        cmd2.Parameters.AddWithValue("@oldName", oldName);
        cmd2.Parameters.AddWithValue("@newName", newName);
        cmd2.ExecuteNonQuery();
        tx.Commit();
        return true;
    }

    public static bool UpdateTypeName(string oldName, string newName)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        using var cmd1 = conn.CreateCommand();
        cmd1.CommandText = "UPDATE tai_lieu SET loai = @newName WHERE loai = @oldName AND (is_deleted IS NULL OR is_deleted = 0)";
        cmd1.Parameters.AddWithValue("@oldName", oldName);
        cmd1.Parameters.AddWithValue("@newName", newName);
        cmd1.ExecuteNonQuery();
        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = "UPDATE loai_tai_lieu SET ten = @newName WHERE ten = @oldName";
        cmd2.Parameters.AddWithValue("@oldName", oldName);
        cmd2.Parameters.AddWithValue("@newName", newName);
        cmd2.ExecuteNonQuery();
        tx.Commit();
        return true;
    }

    public static bool DeleteDocumentsBySubject(string subjectName)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        using var cmd1 = conn.CreateCommand();
        cmd1.CommandText = "UPDATE tai_lieu SET is_deleted = 1, deleted_at = datetime('now','localtime') WHERE mon_hoc = @name AND (is_deleted IS NULL OR is_deleted = 0)";
        cmd1.Parameters.AddWithValue("@name", subjectName);
        cmd1.ExecuteNonQuery();
        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = "DELETE FROM danh_muc WHERE ten = @name";
        cmd2.Parameters.AddWithValue("@name", subjectName);
        cmd2.ExecuteNonQuery();
        tx.Commit();
        return true;
    }

    public static bool DeleteDocumentsByType(string typeName)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        using var cmd1 = conn.CreateCommand();
        cmd1.CommandText = "UPDATE tai_lieu SET is_deleted = 1, deleted_at = datetime('now','localtime') WHERE loai = @name AND (is_deleted IS NULL OR is_deleted = 0)";
        cmd1.Parameters.AddWithValue("@name", typeName);
        cmd1.ExecuteNonQuery();
        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = "DELETE FROM loai_tai_lieu WHERE ten = @name";
        cmd2.Parameters.AddWithValue("@name", typeName);
        cmd2.ExecuteNonQuery();
        tx.Commit();
        return true;
    }

    // ═══ Category (danh_muc) CRUD ═══

    public static bool AddSubject(string name)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO danh_muc (ten) VALUES (@name)";
        cmd.Parameters.AddWithValue("@name", name);
        return cmd.ExecuteNonQuery() > 0;
    }

    public static bool AddType(string name)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO loai_tai_lieu (ten) VALUES (@name)";
        cmd.Parameters.AddWithValue("@name", name);
        return cmd.ExecuteNonQuery() > 0;
    }

    public static List<string> GetAllSubjects()
    {
        var results = new List<string>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ten FROM danh_muc ORDER BY ten";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) results.Add(reader.GetString(0));
        return results;
    }

    public static List<string> GetAllTypes()
    {
        var results = new List<string>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ten FROM loai_tai_lieu ORDER BY ten";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) results.Add(reader.GetString(0));
        return results;
    }

    public static bool DeleteSubject(string name)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM danh_muc WHERE ten = @name";
        cmd.Parameters.AddWithValue("@name", name);
        return cmd.ExecuteNonQuery() > 0;
    }

    public static bool DeleteType(string name)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM loai_tai_lieu WHERE ten = @name";
        cmd.Parameters.AddWithValue("@name", name);
        return cmd.ExecuteNonQuery() > 0;
    }

    // ═══════════════════════════════════════════════════
    // Bulk Operations
    // ═══════════════════════════════════════════════════

    public static int BulkSoftDelete(List<int> ids)
    {
        if (ids == null || ids.Count == 0) return 0;
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        // Use parameterized IN clause
        var paramNames = new List<string>();
        using var cmd = conn.CreateCommand();
        for (int i = 0; i < ids.Count; i++)
        {
            paramNames.Add($"@id{i}");
            cmd.Parameters.AddWithValue($"@id{i}", ids[i]);
        }
        cmd.CommandText = $"UPDATE tai_lieu SET is_deleted = 1, deleted_at = datetime('now','localtime') WHERE id IN ({string.Join(",", paramNames)})";
        return cmd.ExecuteNonQuery();
    }

    public static int BulkUpdateSubject(List<int> ids, string subject)
    {
        if (ids == null || ids.Count == 0) return 0;
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        var paramNames = new List<string>();
        using var cmd = conn.CreateCommand();
        for (int i = 0; i < ids.Count; i++)
        {
            paramNames.Add($"@id{i}");
            cmd.Parameters.AddWithValue($"@id{i}", ids[i]);
        }
        cmd.Parameters.AddWithValue("@subject", subject ?? "");
        cmd.CommandText = $"UPDATE tai_lieu SET mon_hoc = @subject WHERE id IN ({string.Join(",", paramNames)})";
        return cmd.ExecuteNonQuery();
    }

    public static int BulkToggleImportant(List<int> ids, bool important)
    {
        if (ids == null || ids.Count == 0) return 0;
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        var paramNames = new List<string>();
        using var cmd = conn.CreateCommand();
        for (int i = 0; i < ids.Count; i++)
        {
            paramNames.Add($"@id{i}");
            cmd.Parameters.AddWithValue($"@id{i}", ids[i]);
        }
        cmd.Parameters.AddWithValue("@val", important ? 1 : 0);
        cmd.CommandText = $"UPDATE tai_lieu SET quan_trong = @val WHERE id IN ({string.Join(",", paramNames)})";
        return cmd.ExecuteNonQuery();
    }

    // ═══════════════════════════════════════════════════
    // Recycle Bin — Extras
    // ═══════════════════════════════════════════════════

    public static int EmptyRecycleBin()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM tai_lieu WHERE is_deleted = 1";
        return cmd.ExecuteNonQuery();
    }

    public static int GetDeletedDocumentCount()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM tai_lieu WHERE is_deleted = 1";
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    // ═══════════════════════════════════════════════════
    // Recent Files
    // ═══════════════════════════════════════════════════

    public static void AddRecentFile(int documentId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"INSERT OR REPLACE INTO recent_files (document_id, opened_at)
                                VALUES (@docId, datetime('now','localtime'))";
            cmd.Parameters.AddWithValue("@docId", documentId);
            cmd.ExecuteNonQuery();
        }
        // Keep only 20 most recent
        using (var trim = conn.CreateCommand())
        {
            trim.CommandText = @"DELETE FROM recent_files WHERE id NOT IN
                                (SELECT id FROM recent_files ORDER BY opened_at DESC LIMIT 20)";
            trim.ExecuteNonQuery();
        }
    }

    public static List<(int Id, string Ten, string? MonHoc, string? Loai, string? DuongDan, DateTime OpenedAt)> GetRecentFiles()
    {
        var results = new List<(int, string, string?, string?, string?, DateTime)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT t.id, t.ten, t.mon_hoc, t.loai, t.duong_dan, r.opened_at
                             FROM recent_files r
                             INNER JOIN tai_lieu t ON r.document_id = t.id
                             WHERE (t.is_deleted IS NULL OR t.is_deleted = 0)
                             ORDER BY r.opened_at DESC
                             LIMIT 20";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetDateTime(5)
            ));
        }
        return results;
    }

    public static void RemoveRecentFile(int documentId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM recent_files WHERE document_id = @docId";
        cmd.Parameters.AddWithValue("@docId", documentId);
        cmd.ExecuteNonQuery();
    }

    public static void ClearRecentFiles()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM recent_files";
        cmd.ExecuteNonQuery();
    }

    // ═══════════════════════════════════════════════════
    // Misc
    // ═══════════════════════════════════════════════════

    public static int GetTotalDocumentCount()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM tai_lieu WHERE (is_deleted IS NULL OR is_deleted = 0)";
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    // ═══════════════════════════════════════════════════
    // File Integrity helpers
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Update the file path for a document (used when replacing a missing file).
    /// </summary>
    public static bool UpdateDocumentPath(int id, string newPath)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE tai_lieu SET duong_dan = @path WHERE id = @id";
        cmd.Parameters.AddWithValue("@path", newPath);
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Clear the file path for a document (keep metadata, remove broken path).
    /// </summary>
    public static bool ClearDocumentPath(int id)
    {
        return UpdateDocumentPath(id, "");
    }

    // ═══════════════════════════════════════════════════
    // Test Helpers
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Clears the SQLite connection pool for the current database path.
    /// Required in unit tests to release file locks before deleting the temp DB file.
    /// </summary>
    public static void CloseAllConnections()
    {
        SqliteConnection.ClearAllPools();
    }
}
