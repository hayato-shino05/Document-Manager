using System.Data;
using System.Linq;
using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core.DTOs;
using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Data.Helpers;

/// <summary>
/// SQLiteデータベース操作の静的ヘルパー
/// </summary>
public class DatabaseHelper
{
    private string? _databasePath;
    private string? _connectionString;

    /// <summary>
    /// DBファイルパス
    /// </summary>
    public string DatabasePath
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
    /// SQLite接続文字列
    /// </summary>
    public string ConnectionString
    {
        get
        {
            _connectionString ??= $"Data Source={DatabasePath}";
            return _connectionString;
        }
    }

    /// <summary>
    /// テスト用パスの差し替え。InitializeDatabase()より前に呼ぶこと
    /// </summary>
    public void SetDatabasePath(string path)
    {
        _databasePath = path;
        _connectionString = $"Data Source={path}";
    }



    public void InitializeDatabase()
    {
        try
        {
            string? dataFolder = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(dataFolder) && !Directory.Exists(dataFolder))
            {
                Directory.CreateDirectory(dataFolder);
            }

            DatabaseMigrator.RunMigrations(ConnectionString);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Database initialization error: {ex.Message}");
            throw;
        }
    }




    public List<StudyDocument> GetAllDocuments()
    {
        const string query = "SELECT * FROM documents WHERE (is_deleted IS NULL OR is_deleted = 0) ORDER BY created_at DESC";
        return ExecuteReader(query);
    }

    public StudyDocument? GetDocumentById(int id)
    {
        const string query = "SELECT * FROM documents WHERE id = @id";
        var results = ExecuteReader(query, new SqliteParameter("@id", id));
        return results.Count > 0 ? results[0] : null;
    }

    public List<StudyDocument> SearchDocuments(string keyword)
    {
        const string query = """
            SELECT * FROM documents
            WHERE (is_deleted IS NULL OR is_deleted = 0)
            AND (name LIKE @keyword OR subject LIKE @keyword OR notes LIKE @keyword OR author LIKE @keyword OR tags LIKE @keyword)
            ORDER BY created_at DESC
            """;
        return ExecuteReader(query, new SqliteParameter("@keyword", $"%{keyword}%"));
    }

    public List<StudyDocument> FilterDocuments(string subject, string type)
    {
        var query = "SELECT * FROM documents WHERE (is_deleted IS NULL OR is_deleted = 0)";
        var parameters = new List<SqliteParameter>();

        if (!string.IsNullOrEmpty(subject) && subject != "All")
        {
            query += " AND subject = @subject";
            parameters.Add(new SqliteParameter("@subject", subject));
        }

        if (!string.IsNullOrEmpty(type) && type != "All")
        {
            query += " AND type = @type";
            parameters.Add(new SqliteParameter("@type", type));
        }

        query += " ORDER BY created_at DESC";
        return ExecuteReader(query, parameters.ToArray());
    }

    public List<StudyDocument> SearchDocumentsAdvanced(
        string? keyword, string? subject, string? type,
        DateTime? fromDate, DateTime? toDate,
        double? minSize, double? maxSize, bool? isImportant)
    {
        var query = "SELECT * FROM documents WHERE (is_deleted IS NULL OR is_deleted = 0)";
        var parameters = new List<SqliteParameter>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query += " AND (name LIKE @keyword OR subject LIKE @keyword OR notes LIKE @keyword OR tags LIKE @keyword)";
            parameters.Add(new SqliteParameter("@keyword", $"%{keyword}%"));
        }

        if (!string.IsNullOrEmpty(subject) && subject != "All")
        {
            query += " AND subject = @subject";
            parameters.Add(new SqliteParameter("@subject", subject));
        }

        if (!string.IsNullOrEmpty(type) && type != "All")
        {
            query += " AND type = @type";
            parameters.Add(new SqliteParameter("@type", type));
        }

        if (fromDate.HasValue)
        {
            query += " AND date(created_at) >= date(@fromDate)";
            parameters.Add(new SqliteParameter("@fromDate", fromDate.Value.ToString("yyyy-MM-dd")));
        }

        if (toDate.HasValue)
        {
            query += " AND date(created_at) <= date(@toDate)";
            parameters.Add(new SqliteParameter("@toDate", toDate.Value.ToString("yyyy-MM-dd")));
        }

        if (minSize.HasValue)
        {
            query += " AND file_size >= @minSize";
            parameters.Add(new SqliteParameter("@minSize", minSize.Value));
        }

        if (maxSize.HasValue)
        {
            query += " AND file_size <= @maxSize";
            parameters.Add(new SqliteParameter("@maxSize", maxSize.Value));
        }

        if (isImportant is true)
        {
            query += " AND is_important = 1";
        }

        query += " ORDER BY created_at DESC";
        return ExecuteReader(query, parameters.ToArray());
    }

    public bool InsertDocument(StudyDocument doc)
    {
        const string query = """
            INSERT INTO documents (name, subject, type, file_path, notes, file_size, author, is_important, tags, deadline)
            VALUES (@name, @subject, @type, @file_path, @notes, @file_size, @author, @is_important, @tags, @deadline)
            """;
        return ExecuteNonQuery(query, BuildDocumentParameters(doc)) > 0;
    }

    public bool UpdateDocument(StudyDocument doc)
    {
        const string query = """
            UPDATE documents SET
                name = @name, subject = @subject, type = @type, file_path = @file_path,
                notes = @notes, file_size = @file_size, author = @author,
                is_important = @is_important, tags = @tags, deadline = @deadline
            WHERE id = @id
            """;
        var parameters = BuildDocumentParameters(doc).ToList();
        parameters.Add(new SqliteParameter("@id", doc.Id));
        return ExecuteNonQuery(query, parameters.ToArray()) > 0;
    }

    public bool DeleteDocument(int id)
    {
        const string query = "UPDATE documents SET is_deleted = 1, deleted_at = datetime('now','localtime') WHERE id = @id";
        return ExecuteNonQuery(query, new SqliteParameter("@id", id)) > 0;
    }



    public List<string> GetDistinctSubjects()
    {
        const string query = "SELECT DISTINCT subject FROM documents WHERE subject IS NOT NULL AND subject != '' AND (is_deleted IS NULL OR is_deleted = 0) ORDER BY subject";
        return ExecuteStringList(query, "subject");
    }

    public List<string> GetDistinctTypes()
    {
        const string query = "SELECT DISTINCT type FROM documents WHERE type IS NOT NULL AND type != '' AND (is_deleted IS NULL OR is_deleted = 0) ORDER BY type";
        return ExecuteStringList(query, "type");
    }

    public List<string> GetDistinctTags()
    {
        const string query = "SELECT DISTINCT tags FROM documents WHERE tags IS NOT NULL AND tags != '' AND (is_deleted IS NULL OR is_deleted = 0)";
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

    public List<StudyDocument> GetUpcomingDeadlines(int days = 7)
    {
        const string query = """
            SELECT * FROM documents
            WHERE deadline IS NOT NULL
            AND (is_deleted IS NULL OR is_deleted = 0)
            AND date(deadline) >= date('now', 'localtime')
            AND date(deadline) <= date('now', 'localtime', '+' || @days || ' days')
            ORDER BY deadline ASC
            """;
        return ExecuteReader(query, new SqliteParameter("@days", days));
    }

    public List<StudyDocument> GetOverdueDocuments()
    {
        const string query = """
            SELECT * FROM documents
            WHERE deadline IS NOT NULL
            AND (is_deleted IS NULL OR is_deleted = 0)
            AND date(deadline) < date('now', 'localtime')
            ORDER BY deadline ASC
            """;
        return ExecuteReader(query);
    }

    public DashboardStats GetDashboardStatistics()
    {
        var stats = new DashboardStats();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        stats.TotalDocuments = GetScalarInt(conn, "SELECT COUNT(*) FROM documents WHERE (is_deleted IS NULL OR is_deleted = 0)");
        stats.ImportantDocuments = GetScalarInt(conn, "SELECT COUNT(*) FROM documents WHERE is_important = 1 AND (is_deleted IS NULL OR is_deleted = 0)");
        stats.NoFileDocuments = GetScalarInt(conn, "SELECT COUNT(*) FROM documents WHERE (is_deleted IS NULL OR is_deleted = 0) AND (file_path IS NULL OR file_path = '')");
        stats.OverdueDocuments = GetScalarInt(conn, "SELECT COUNT(*) FROM documents WHERE deadline IS NOT NULL AND (is_deleted IS NULL OR is_deleted = 0) AND date(deadline) < date('now', 'localtime')");
        stats.NearDeadlineDocuments = GetScalarInt(conn, "SELECT COUNT(*) FROM documents WHERE deadline IS NOT NULL AND (is_deleted IS NULL OR is_deleted = 0) AND date(deadline) >= date('now', 'localtime') AND date(deadline) <= date('now', 'localtime', '+7 days')");
        stats.TotalCategories = GetScalarInt(conn, "SELECT COUNT(DISTINCT subject) FROM documents WHERE subject IS NOT NULL AND subject != '' AND (is_deleted IS NULL OR is_deleted = 0)");
        stats.TotalCollections = GetScalarInt(conn, "SELECT COUNT(*) FROM collections");

        return stats;
    }




    public List<StudyDocument> GetDeletedDocuments()
    {
        const string query = "SELECT * FROM documents WHERE is_deleted = 1 ORDER BY deleted_at DESC";
        return ExecuteReader(query);
    }

    public bool RestoreDocument(int id)
    {
        const string query = "UPDATE documents SET is_deleted = 0, deleted_at = NULL WHERE id = @id";
        return ExecuteNonQuery(query, new SqliteParameter("@id", id)) > 0;
    }

    public bool PermanentDeleteDocument(int id)
    {
        const string query = "DELETE FROM documents WHERE id = @id";
        return ExecuteNonQuery(query, new SqliteParameter("@id", id)) > 0;
    }



    public bool BackupDatabase(string destPath)
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



    private List<StudyDocument> ExecuteReader(string query, params SqliteParameter[] parameters)
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

    private int ExecuteNonQuery(string query, params SqliteParameter[] parameters)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = new SqliteCommand(query, conn);

        foreach (var param in parameters)
            cmd.Parameters.Add(param);

        return cmd.ExecuteNonQuery();
    }

    private List<string> ExecuteStringList(string query, string columnName)
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

    private int GetScalarInt(SqliteConnection conn, string query)
    {
        using var cmd = new SqliteCommand(query, conn);
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    private StudyDocument MapToDocument(SqliteDataReader reader)
    {
        return new StudyDocument
        {
            Id = reader.GetInt32(reader.GetOrdinal("id")),
            Name = reader["name"]?.ToString() ?? string.Empty,
            Subject = reader["subject"]?.ToString() ?? string.Empty,
            Type = reader["type"]?.ToString() ?? string.Empty,
            FilePath = reader["file_path"]?.ToString() ?? string.Empty,
            Notes = reader["notes"]?.ToString() ?? string.Empty,
            CreatedAt = reader["created_at"] is DBNull ? DateTime.Now : DateTime.Parse(reader["created_at"].ToString()!),
            FileSize = reader["file_size"] is DBNull ? null : Convert.ToDouble(reader["file_size"]),
            Author = reader["author"]?.ToString() ?? string.Empty,
            IsImportant = reader["is_important"] is not DBNull && Convert.ToInt32(reader["is_important"]) == 1,
            Tags = reader["tags"]?.ToString() ?? string.Empty,
            Deadline = reader["deadline"] is DBNull ? null : DateTime.Parse(reader["deadline"].ToString()!)
        };
    }

    private SqliteParameter[] BuildDocumentParameters(StudyDocument doc)
    {
        return
        [
            new SqliteParameter("@name", doc.Name),
            new SqliteParameter("@subject", string.IsNullOrEmpty(doc.Subject) ? DBNull.Value : doc.Subject),
            new SqliteParameter("@type", string.IsNullOrEmpty(doc.Type) ? DBNull.Value : doc.Type),
            new SqliteParameter("@file_path", string.IsNullOrEmpty(doc.FilePath) ? DBNull.Value : doc.FilePath),
            new SqliteParameter("@notes", string.IsNullOrEmpty(doc.Notes) ? DBNull.Value : doc.Notes),
            new SqliteParameter("@file_size", doc.FileSize.HasValue ? doc.FileSize.Value : DBNull.Value),
            new SqliteParameter("@author", string.IsNullOrEmpty(doc.Author) ? DBNull.Value : doc.Author),
            new SqliteParameter("@is_important", doc.IsImportant ? 1 : 0),
            new SqliteParameter("@tags", string.IsNullOrEmpty(doc.Tags) ? DBNull.Value : doc.Tags),
            new SqliteParameter("@deadline", doc.Deadline.HasValue ? doc.Deadline.Value.ToString("yyyy-MM-dd HH:mm:ss") : DBNull.Value)
        ];
    }



    public string? GetPersonalNote(int documentId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT content FROM personal_notes WHERE document_id = @documentId";
        cmd.Parameters.AddWithValue("@documentId", documentId);
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    public bool SavePersonalNote(int documentId, string content)
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

    public bool DeletePersonalNote(int documentId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM personal_notes WHERE document_id = @documentId";
        cmd.Parameters.AddWithValue("@documentId", documentId);
        return cmd.ExecuteNonQuery() > 0;
    }



    public void AddDocumentRelation(int docId1, int docId2, string relationType = "related")
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

    public List<(StudyDocument Doc, int RelationId, string RelationType)> GetRelatedDocuments(int docId)
    {
        var results = new List<(StudyDocument, int, string)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT d.id, d.name, d.subject, d.type, d.file_path, r.relation_type, r.id as relation_id
                            FROM document_relations r
                            INNER JOIN documents d ON (d.id = CASE WHEN r.doc_id_1 = @docId THEN r.doc_id_2 ELSE r.doc_id_1 END)
                            WHERE (r.doc_id_1 = @docId OR r.doc_id_2 = @docId)
                            AND (d.is_deleted IS NULL OR d.is_deleted = 0)
                            ORDER BY d.name";
        cmd.Parameters.AddWithValue("@docId", docId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var doc = new StudyDocument
            {
                Id = reader.GetInt32(0),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Subject = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Type = reader.IsDBNull(3) ? "" : reader.GetString(3),
                FilePath = reader.IsDBNull(4) ? "" : reader.GetString(4)
            };
            var relationType = reader.IsDBNull(5) ? "related" : reader.GetString(5);
            var relationId = reader.GetInt32(6);
            results.Add((doc, relationId, relationType));
        }
        return results;
    }

    public void RemoveDocumentRelation(int relationId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM document_relations WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", relationId);
        cmd.ExecuteNonQuery();
    }



    public List<(string Label, int Count)> GetDocumentsByDay(int days = 7)
    {
        var results = new List<(string, int)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            WITH RECURSIVE DateSeries(d) AS (
                SELECT date('now', 'localtime')
                UNION ALL
                SELECT date(d, '-1 day')
                FROM DateSeries
                WHERE date(d, '-1 day') >= date('now', 'localtime', '-' || (@days - 1) || ' days')
            )
            SELECT
                strftime('%d/%m', ds.d) as date_label,
                COALESCE(COUNT(doc.id), 0) as doc_count
            FROM DateSeries ds
            LEFT JOIN documents doc ON date(doc.created_at) = ds.d
                AND (doc.is_deleted IS NULL OR doc.is_deleted = 0)
            GROUP BY ds.d
            ORDER BY ds.d ASC";
        cmd.Parameters.AddWithValue("@days", days);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add((reader.GetString(0), reader.GetInt32(1)));
        }
        return results;
    }

    public List<(string Label, int Count)> GetDocumentsByMonth(int months = 12)
    {
        var results = new List<(string, int)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            WITH RECURSIVE MonthSeries(m) AS (
                SELECT date('now', 'localtime', 'start of month')
                UNION ALL
                SELECT date(m, '-1 month')
                FROM MonthSeries
                WHERE date(m, '-1 month') >= date('now', 'localtime', 'start of month', '-' || (@months - 1) || ' months')
            )
            SELECT
                strftime('%m/%Y', ms.m) as month_label,
                COALESCE(COUNT(doc.id), 0) as doc_count
            FROM MonthSeries ms
            LEFT JOIN documents doc ON strftime('%Y-%m', doc.created_at) = strftime('%Y-%m', ms.m)
                AND (doc.is_deleted IS NULL OR doc.is_deleted = 0)
            GROUP BY ms.m
            ORDER BY ms.m ASC";
        cmd.Parameters.AddWithValue("@months", months);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add((reader.GetString(0), reader.GetInt32(1)));
        }
        return results;
    }

    public List<(string Label, int Count)> GetDocumentsBySubject()
    {
        var results = new List<(string, int)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT COALESCE(subject, 'Unknown'), COUNT(*)
                            FROM documents
                            WHERE (is_deleted IS NULL OR is_deleted = 0)
                            GROUP BY subject ORDER BY COUNT(*) DESC";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add((reader.GetString(0), reader.GetInt32(1)));
        }
        return results;
    }

    public List<(string Label, int Count)> GetDocumentsByType()
    {
        var results = new List<(string, int)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT COALESCE(type, 'Unknown'), COUNT(*)
                            FROM documents
                            WHERE (is_deleted IS NULL OR is_deleted = 0)
                            GROUP BY type ORDER BY COUNT(*) DESC";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add((reader.GetString(0), reader.GetInt32(1)));
        }
        return results;
    }



    public List<(int Id, string Name, string? Description, DateTime CreatedAt, int ItemCount)> GetCollections()
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

    public int CreateCollection(string name, string? description = null)
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

    public bool UpdateCollection(int collectionId, string name, string? description = null)
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

    public bool DeleteCollection(int collectionId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using (var delItems = conn.CreateCommand())
        {
            delItems.CommandText = "DELETE FROM collection_items WHERE collection_id = @id";
            delItems.Parameters.AddWithValue("@id", collectionId);
            delItems.ExecuteNonQuery();
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM collections WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", collectionId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public List<StudyDocument> GetDocumentsInCollection(int collectionId)
    {
        var results = new List<StudyDocument>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT t.*
                            FROM documents t
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

    public bool AddDocumentToCollection(int collectionId, int documentId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using (var check = conn.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(*) FROM collection_items WHERE collection_id = @colId AND document_id = @docId";
            check.Parameters.AddWithValue("@colId", collectionId);
            check.Parameters.AddWithValue("@docId", documentId);
            var exists = check.ExecuteScalar();
            if (exists != null && Convert.ToInt32(exists) > 0)
                return false;
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO collection_items (collection_id, document_id) VALUES (@colId, @docId)";
        cmd.Parameters.AddWithValue("@colId", collectionId);
        cmd.Parameters.AddWithValue("@docId", documentId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool RemoveDocumentFromCollection(int collectionId, int documentId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM collection_items WHERE collection_id = @colId AND document_id = @docId";
        cmd.Parameters.AddWithValue("@colId", collectionId);
        cmd.Parameters.AddWithValue("@docId", documentId);
        return cmd.ExecuteNonQuery() > 0;
    }



    public List<(string Name, int Count)> GetSubjectsWithCount()
    {
        var results = new List<(string, int)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT c.name, COUNT(d.id) as doc_count
            FROM categories c
            LEFT JOIN documents d ON d.subject = c.name
                AND (d.is_deleted IS NULL OR d.is_deleted = 0)
            GROUP BY c.name
            UNION
            SELECT d2.subject, COUNT(d2.id)
            FROM documents d2
            WHERE d2.subject IS NOT NULL AND d2.subject != ''
              AND (d2.is_deleted IS NULL OR d2.is_deleted = 0)
              AND d2.subject NOT IN (SELECT name FROM categories)
            GROUP BY d2.subject
            ORDER BY 1";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            results.Add((reader.GetString(0), reader.GetInt32(1)));
        return results;
    }

    public List<(string Name, int Count)> GetTypesWithCount()
    {
        var results = new List<(string, int)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT dt.name, COUNT(d.id) as doc_count
            FROM document_types dt
            LEFT JOIN documents d ON d.type = dt.name
                AND (d.is_deleted IS NULL OR d.is_deleted = 0)
            GROUP BY dt.name
            UNION
            SELECT d2.type, COUNT(d2.id)
            FROM documents d2
            WHERE d2.type IS NOT NULL AND d2.type != ''
              AND (d2.is_deleted IS NULL OR d2.is_deleted = 0)
              AND d2.type NOT IN (SELECT name FROM document_types)
            GROUP BY d2.type
            ORDER BY 1";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            results.Add((reader.GetString(0), reader.GetInt32(1)));
        return results;
    }

    public bool UpdateSubjectName(string oldName, string newName)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        using var cmd1 = conn.CreateCommand();
        cmd1.CommandText = "UPDATE documents SET subject = @newName WHERE subject = @oldName AND (is_deleted IS NULL OR is_deleted = 0)";
        cmd1.Parameters.AddWithValue("@oldName", oldName);
        cmd1.Parameters.AddWithValue("@newName", newName);
        cmd1.ExecuteNonQuery();

        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = "UPDATE categories SET name = @newName WHERE name = @oldName";
        cmd2.Parameters.AddWithValue("@oldName", oldName);
        cmd2.Parameters.AddWithValue("@newName", newName);
        cmd2.ExecuteNonQuery();
        tx.Commit();
        return true;
    }

    public bool UpdateTypeName(string oldName, string newName)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        using var cmd1 = conn.CreateCommand();
        cmd1.CommandText = "UPDATE documents SET type = @newName WHERE type = @oldName AND (is_deleted IS NULL OR is_deleted = 0)";
        cmd1.Parameters.AddWithValue("@oldName", oldName);
        cmd1.Parameters.AddWithValue("@newName", newName);
        cmd1.ExecuteNonQuery();
        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = "UPDATE document_types SET name = @newName WHERE name = @oldName";
        cmd2.Parameters.AddWithValue("@oldName", oldName);
        cmd2.Parameters.AddWithValue("@newName", newName);
        cmd2.ExecuteNonQuery();
        tx.Commit();
        return true;
    }

    public bool DeleteDocumentsBySubject(string subjectName)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        using var cmd1 = conn.CreateCommand();
        cmd1.CommandText = "UPDATE documents SET is_deleted = 1, deleted_at = datetime('now','localtime') WHERE subject = @name AND (is_deleted IS NULL OR is_deleted = 0)";
        cmd1.Parameters.AddWithValue("@name", subjectName);
        cmd1.ExecuteNonQuery();
        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = "DELETE FROM categories WHERE name = @name";
        cmd2.Parameters.AddWithValue("@name", subjectName);
        cmd2.ExecuteNonQuery();
        tx.Commit();
        return true;
    }

    public bool DeleteDocumentsByType(string typeName)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        using var cmd1 = conn.CreateCommand();
        cmd1.CommandText = "UPDATE documents SET is_deleted = 1, deleted_at = datetime('now','localtime') WHERE type = @name AND (is_deleted IS NULL OR is_deleted = 0)";
        cmd1.Parameters.AddWithValue("@name", typeName);
        cmd1.ExecuteNonQuery();
        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = "DELETE FROM document_types WHERE name = @name";
        cmd2.Parameters.AddWithValue("@name", typeName);
        cmd2.ExecuteNonQuery();
        tx.Commit();
        return true;
    }



    public bool AddSubject(string name)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO categories (name) VALUES (@name)";
        cmd.Parameters.AddWithValue("@name", name);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool AddType(string name)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO document_types (name) VALUES (@name)";
        cmd.Parameters.AddWithValue("@name", name);
        return cmd.ExecuteNonQuery() > 0;
    }

    public List<string> GetAllSubjects()
    {
        var results = new List<string>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM categories ORDER BY name";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) results.Add(reader.GetString(0));
        return results;
    }

    public List<string> GetAllTypes()
    {
        var results = new List<string>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM document_types ORDER BY name";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) results.Add(reader.GetString(0));
        return results;
    }

    public bool DeleteSubject(string name)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM categories WHERE name = @name";
        cmd.Parameters.AddWithValue("@name", name);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool DeleteType(string name)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM document_types WHERE name = @name";
        cmd.Parameters.AddWithValue("@name", name);
        return cmd.ExecuteNonQuery() > 0;
    }



    public int BulkSoftDelete(List<int> ids)
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
        cmd.CommandText = $"UPDATE documents SET is_deleted = 1, deleted_at = datetime('now','localtime') WHERE id IN ({string.Join(",", paramNames)})";
        return cmd.ExecuteNonQuery();
    }

    public int BulkUpdateSubject(List<int> ids, string subject)
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
        cmd.CommandText = $"UPDATE documents SET subject = @subject WHERE id IN ({string.Join(",", paramNames)})";
        return cmd.ExecuteNonQuery();
    }

    public int BulkToggleImportant(List<int> ids, bool important)
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
        cmd.CommandText = $"UPDATE documents SET is_important = @val WHERE id IN ({string.Join(",", paramNames)})";
        return cmd.ExecuteNonQuery();
    }



    public int EmptyRecycleBin()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM documents WHERE is_deleted = 1";
        return cmd.ExecuteNonQuery();
    }

    public int GetDeletedDocumentCount()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM documents WHERE is_deleted = 1";
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }



    public void AddRecentFile(int documentId)
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
        // 直近20件のみ保持
        using (var trim = conn.CreateCommand())
        {
            trim.CommandText = @"DELETE FROM recent_files WHERE id NOT IN
                                (SELECT id FROM recent_files ORDER BY opened_at DESC LIMIT 20)";
            trim.ExecuteNonQuery();
        }
    }

    public List<(int Id, string Name, string? Subject, string? Type, string? FilePath, DateTime OpenedAt)> GetRecentFiles()
    {
        var results = new List<(int, string, string?, string?, string?, DateTime)>();
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT d.id, d.name, d.subject, d.type, d.file_path, r.opened_at
                             FROM recent_files r
                             INNER JOIN documents d ON r.document_id = d.id
                             WHERE (d.is_deleted IS NULL OR d.is_deleted = 0)
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

    public void RemoveRecentFile(int documentId)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM recent_files WHERE document_id = @docId";
        cmd.Parameters.AddWithValue("@docId", documentId);
        cmd.ExecuteNonQuery();
    }

    public void ClearRecentFiles()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM recent_files";
        cmd.ExecuteNonQuery();
    }



    public int GetTotalDocumentCount()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM documents WHERE (is_deleted IS NULL OR is_deleted = 0)";
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }



    /// <summary>
    /// ファイルパスの差し替え
    /// </summary>
    public bool UpdateDocumentPath(int id, string newPath)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE documents SET file_path = @path WHERE id = @id";
        cmd.Parameters.AddWithValue("@path", newPath);
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// 壊れたパスをクリアしてメタデータのみ残す
    /// </summary>
    public bool ClearDocumentPath(int id)
    {
        return UpdateDocumentPath(id, "");
    }



    /// <summary>
    /// テスト用：接続プールを解放してDBファイルを削除可能にする
    /// </summary>
    public void CloseAllConnections()
    {
        SqliteConnection.ClearAllPools();
    }

    /// <summary>
    /// app_settings テーブルから設定値を取得
    /// </summary>
    public string? GetSetting(string key)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM app_settings WHERE key = @key";
        cmd.Parameters.AddWithValue("@key", key);
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    /// <summary>
    /// app_settings テーブルへ設定値をUPSERT
    /// </summary>
    public void SetSetting(string key, string value)
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO app_settings (key, value) VALUES (@key, @value)";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.ExecuteNonQuery();
    }

}
