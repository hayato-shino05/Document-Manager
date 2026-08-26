using System.Data;
using System.Linq;
using System.Threading;
using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core.DTOs;
using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Data.Helpers;

/// <summary>
/// SQLiteデータベース操作の静的ヘルパー
/// </summary>
public class DatabaseHelper
{
    private const string DatabasePathEnvironmentVariable = "SDM_DATABASE_PATH";
    private string? _databasePath;
    private string? _connectionString;

    /// <summary>
    /// DBファイルパス
    /// </summary>
    public string DatabasePath
    {
        get
        {
            _databasePath ??= GetDefaultDatabasePath();
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
            _connectionString ??= $"Data Source={DatabasePath};Foreign Keys=True";
            return _connectionString;
        }
    }

    /// <summary>
    /// テスト用パスの差し替え。InitializeDatabase()より前に呼ぶこと
    /// </summary>
    public void SetDatabasePath(string path)
    {
        _databasePath = path;
        _connectionString = $"Data Source={path};Foreign Keys=True";
    }


    private static string GetDefaultDatabasePath()
    {
        var configuredPath = Environment.GetEnvironmentVariable(DatabasePathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (!Path.IsPathFullyQualified(configuredPath))
                throw new InvalidOperationException($"{DatabasePathEnvironmentVariable} must be an absolute path.");

            return Path.GetFullPath(configuredPath);
        }

        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(localAppData) || !Path.IsPathFullyQualified(localAppData))
        {
            localAppData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                "share");
        }

        return Path.Combine(localAppData, "StudyDocumentManager", "data", "study_documents.db");
    }

    private static string GetLegacyDatabasePath()
    {
        string appFolder = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(appFolder, "data", "study_documents.db");
    }

    private void MigrateLegacyDatabaseIfNeeded()
    {
        var targetPath = DatabasePath;
        if (File.Exists(targetPath))
            return;

        var legacyPath = GetLegacyDatabasePath();
        if (!File.Exists(legacyPath) || PathsReferToSameFile(targetPath, legacyPath))
            return;

        using var source = OpenConnection(legacyPath, SqliteOpenMode.ReadOnly, pooling: false, synchronize: false);
        using var destination = OpenConnection(targetPath, pooling: false, synchronize: false);
        source.BackupDatabase(destination);
    }



    public void InitializeDatabase()
    {
        using var operationLock = AcquireDatabaseOperationLock(DatabasePath);
        try
        {
            string? dataFolder = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(dataFolder) && !Directory.Exists(dataFolder))
            {
                Directory.CreateDirectory(dataFolder);
            }

            MigrateLegacyDatabaseIfNeeded();
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
        => SearchDocumentsAdvanced(keyword, subject, type, fromDate, toDate, minSize, maxSize, isImportant, null);

    public List<StudyDocument> SearchDocumentsAdvanced(
        string? keyword, string? subject, string? type,
        DateTime? fromDate, DateTime? toDate,
        double? minSize, double? maxSize, bool? isImportant, string? status)
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

        if (!string.IsNullOrEmpty(status))
        {
            query += " AND (status = @status)";
            parameters.Add(new SqliteParameter("@status", status));
        }

        query += " ORDER BY created_at DESC";
        return ExecuteReader(query, parameters.ToArray());
    }

    public Dictionary<string, int> GetStatusCounts()
    {
        const string query = "SELECT status, COUNT(*) FROM documents WHERE (is_deleted IS NULL OR is_deleted = 0) AND status IS NOT NULL GROUP BY status";
        var counts = new Dictionary<string, int>();
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand(query, conn);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
            counts[reader.GetString(0)] = Convert.ToInt32(reader.GetValue(1));

        return counts;
    }

    public bool InsertDocument(StudyDocument doc)
    {
        const string query = """
            INSERT INTO documents (name, subject, type, file_path, notes, file_size, author, is_important, tags, deadline, status)
            VALUES (@name, @subject, @type, @file_path, @notes, @file_size, @author, @is_important, @tags, @deadline, @status);
            SELECT last_insert_rowid();
            """;

        using var conn = OpenConnection();
        using var cmd = new SqliteCommand(query, conn);

        foreach (var parameter in BuildDocumentParameters(doc))
            cmd.Parameters.Add(parameter);

        var result = cmd.ExecuteScalar();
        if (result == null)
            return false;

        doc.Id = Convert.ToInt32(result);
        return true;
    }


    public bool InsertDocumentWithCatalogs(StudyDocument document)
    {
        const string query = """
            INSERT INTO documents (name, subject, type, file_path, notes, file_size, author, is_important, tags, deadline, status)
            VALUES (@name, @subject, @type, @file_path, @notes, @file_size, @author, @is_important, @tags, @deadline, @status)
            """;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            if (!string.IsNullOrWhiteSpace(document.Subject))
                InsertCatalogValue(connection, transaction, "categories", document.Subject);

            if (!string.IsNullOrWhiteSpace(document.Type))
                InsertCatalogValue(connection, transaction, "document_types", document.Type);

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = query;
            foreach (var parameter in BuildDocumentParameters(document))
                command.Parameters.Add(parameter);

            if (command.ExecuteNonQuery() == 0)
            {
                transaction.Rollback();
                return false;
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public bool UpdateDocument(StudyDocument doc)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var updated = UpdateDocumentCore(connection, transaction, doc);
            if (!updated)
            {
                transaction.Rollback();
                return false;
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private bool UpdateDocumentCore(SqliteConnection connection, SqliteTransaction transaction, StudyDocument doc)
    {
        if (!string.IsNullOrWhiteSpace(doc.Subject))
            InsertCatalogValue(connection, transaction, "categories", doc.Subject);

        if (!string.IsNullOrWhiteSpace(doc.Type))
            InsertCatalogValue(connection, transaction, "document_types", doc.Type);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE documents SET
                name = @name, subject = @subject, type = @type, file_path = @file_path,
                notes = @notes, file_size = @file_size, author = @author,
                is_important = @is_important, tags = @tags, deadline = @deadline,
                status = @status
            WHERE id = @id
            """;
        foreach (var parameter in BuildDocumentParameters(doc))
            command.Parameters.Add(parameter);
        command.Parameters.Add(new SqliteParameter("@id", doc.Id));
        return command.ExecuteNonQuery() > 0;
    }

    public void ApplyMetadataUndo(
        IReadOnlyList<StudyDocument> originals,
        IReadOnlyList<(int CollectionId, int DocumentId)> addedCollectionMemberships)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var original in originals)
            {
                if (!DocumentRowExists(connection, transaction, original.Id))
                    continue;
                if (!UpdateDocumentCore(connection, transaction, original))
                    throw new InvalidOperationException("Undo document restoration failed.");
            }

            foreach (var membership in addedCollectionMemberships)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM collection_items WHERE collection_id = @collectionId AND document_id = @documentId";
                command.Parameters.AddWithValue("@collectionId", membership.CollectionId);
                command.Parameters.AddWithValue("@documentId", membership.DocumentId);
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static bool DocumentRowExists(SqliteConnection connection, SqliteTransaction transaction, int id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM documents WHERE id = @id";
        command.Parameters.AddWithValue("@id", id);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    public bool DeleteDocument(int id)
    {
        const string query = "UPDATE documents SET is_deleted = 1, deleted_at = datetime('now','localtime') WHERE id = @id";
        return ExecuteNonQuery(query, new SqliteParameter("@id", id)) > 0;
    }

    public bool MergeDocuments(int survivorId, IReadOnlyList<int> duplicateIds)
    {
        var duplicates = duplicateIds
            .Where(id => id != survivorId)
            .Distinct()
            .ToArray();
        if (duplicates.Length == 0)
            return false;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            if (!DocumentRowIsActive(connection, transaction, survivorId))
                return false;

            foreach (var duplicateId in duplicates)
            {
                if (!DocumentRowIsActive(connection, transaction, duplicateId))
                    throw new InvalidOperationException($"Duplicate document {duplicateId} is no longer available.");

                MergeCollectionMemberships(connection, transaction, survivorId, duplicateId);
                MergePersonalNotes(connection, transaction, survivorId, duplicateId);
                MergeRecentFile(connection, transaction, survivorId, duplicateId);
                MergeRelations(connection, transaction, survivorId, duplicateId);
                SoftDeleteDocument(connection, transaction, duplicateId);
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static bool DocumentRowIsActive(SqliteConnection connection, SqliteTransaction transaction, int id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM documents WHERE id = @id AND (is_deleted IS NULL OR is_deleted = 0)";
        command.Parameters.AddWithValue("@id", id);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private static void MergeCollectionMemberships(SqliteConnection connection, SqliteTransaction transaction, int survivorId, int duplicateId)
    {
        using var add = connection.CreateCommand();
        add.Transaction = transaction;
        add.CommandText = """
            INSERT OR IGNORE INTO collection_items (collection_id, document_id)
            SELECT collection_id, @survivorId FROM collection_items WHERE document_id = @duplicateId;
            DELETE FROM collection_items WHERE document_id = @duplicateId;
            """;
        add.Parameters.AddWithValue("@survivorId", survivorId);
        add.Parameters.AddWithValue("@duplicateId", duplicateId);
        add.ExecuteNonQuery();
    }

    private static void MergePersonalNotes(SqliteConnection connection, SqliteTransaction transaction, int survivorId, int duplicateId)
    {
        using var read = connection.CreateCommand();
        read.Transaction = transaction;
        read.CommandText = "SELECT content FROM personal_notes WHERE document_id IN (@survivorId, @duplicateId) ORDER BY document_id";
        read.Parameters.AddWithValue("@survivorId", survivorId);
        read.Parameters.AddWithValue("@duplicateId", duplicateId);
        var contents = new List<string>();
        using (var reader = read.ExecuteReader())
        {
            while (reader.Read())
            {
                var content = reader.IsDBNull(0) ? string.Empty : reader.GetString(0).Trim();
                if (content.Length > 0 && !contents.Contains(content, StringComparer.Ordinal))
                    contents.Add(content);
            }
        }

        using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM personal_notes WHERE document_id IN (@survivorId, @duplicateId)";
        delete.Parameters.AddWithValue("@survivorId", survivorId);
        delete.Parameters.AddWithValue("@duplicateId", duplicateId);
        delete.ExecuteNonQuery();

        if (contents.Count == 0)
            return;

        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = "INSERT INTO personal_notes (document_id, content) VALUES (@documentId, @content)";
        insert.Parameters.AddWithValue("@documentId", survivorId);
        insert.Parameters.AddWithValue("@content", string.Join(Environment.NewLine + Environment.NewLine, contents));
        insert.ExecuteNonQuery();
    }

    private static void MergeRecentFile(SqliteConnection connection, SqliteTransaction transaction, int survivorId, int duplicateId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO recent_files (document_id, opened_at)
            SELECT @survivorId, MAX(opened_at) FROM recent_files WHERE document_id IN (@survivorId, @duplicateId);
            UPDATE recent_files SET opened_at = (
                SELECT MAX(opened_at) FROM recent_files WHERE document_id IN (@survivorId, @duplicateId)
            ) WHERE document_id = @survivorId;
            DELETE FROM recent_files WHERE document_id = @duplicateId;
            """;
        command.Parameters.AddWithValue("@survivorId", survivorId);
        command.Parameters.AddWithValue("@duplicateId", duplicateId);
        command.ExecuteNonQuery();
    }

    private static void MergeRelations(SqliteConnection connection, SqliteTransaction transaction, int survivorId, int duplicateId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO document_relations (doc_id_1, doc_id_2, relation_type)
            SELECT
                CASE WHEN doc_id_1 = @duplicateId THEN @survivorId ELSE doc_id_1 END,
                CASE WHEN doc_id_2 = @duplicateId THEN @survivorId ELSE doc_id_2 END,
                relation_type
            FROM document_relations
            WHERE (doc_id_1 = @duplicateId OR doc_id_2 = @duplicateId)
              AND NOT (
                  CASE WHEN doc_id_1 = @duplicateId THEN @survivorId ELSE doc_id_1 END = @survivorId
                  AND CASE WHEN doc_id_2 = @duplicateId THEN @survivorId ELSE doc_id_2 END = @survivorId
              );
            DELETE FROM document_relations WHERE doc_id_1 = @duplicateId OR doc_id_2 = @duplicateId;
            """;
        command.Parameters.AddWithValue("@survivorId", survivorId);
        command.Parameters.AddWithValue("@duplicateId", duplicateId);
        command.ExecuteNonQuery();
    }

    private static void SoftDeleteDocument(SqliteConnection connection, SqliteTransaction transaction, int id)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE documents SET is_deleted = 1, deleted_at = datetime('now','localtime') WHERE id = @id";
        command.Parameters.AddWithValue("@id", id);
        if (command.ExecuteNonQuery() != 1)
            throw new InvalidOperationException($"Failed to merge duplicate document {id}.");
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

        using var conn = OpenConnection();
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

    public List<StudyDocument> GetUncategorizedDocuments()
    {
        const string query = """
            SELECT * FROM documents
            WHERE (is_deleted IS NULL OR is_deleted = 0)
            AND (subject IS NULL OR subject = '')
            ORDER BY created_at DESC
            """;
        return ExecuteReader(query);
    }

    public List<StudyDocument> GetDocumentsWithMissingMetadata()
    {
        const string query = """
            SELECT * FROM documents
            WHERE (is_deleted IS NULL OR is_deleted = 0)
            AND ((subject IS NULL OR subject = '') OR (type IS NULL OR type = '') OR (tags IS NULL OR tags = ''))
            ORDER BY created_at DESC
            """;
        return ExecuteReader(query);
    }

    public DashboardStats GetDashboardStatistics()
    {
        var stats = new DashboardStats();
        using var conn = OpenConnection();

        stats.TotalDocuments = GetScalarInt(conn, "SELECT COUNT(*) FROM documents WHERE (is_deleted IS NULL OR is_deleted = 0)");
        stats.ImportantDocuments = GetScalarInt(conn, "SELECT COUNT(*) FROM documents WHERE is_important = 1 AND (is_deleted IS NULL OR is_deleted = 0)");
        stats.NoFileDocuments = GetScalarInt(conn, "SELECT COUNT(*) FROM documents WHERE (is_deleted IS NULL OR is_deleted = 0) AND (file_path IS NULL OR file_path = '')");
        stats.OverdueDocuments = GetScalarInt(conn, "SELECT COUNT(*) FROM documents WHERE deadline IS NOT NULL AND (is_deleted IS NULL OR is_deleted = 0) AND date(deadline) < date('now', 'localtime')");
        stats.NearDeadlineDocuments = GetScalarInt(conn, "SELECT COUNT(*) FROM documents WHERE deadline IS NOT NULL AND (is_deleted IS NULL OR is_deleted = 0) AND date(deadline) >= date('now', 'localtime') AND date(deadline) <= date('now', 'localtime', '+7 days')");
        stats.TotalCategories = GetScalarInt(conn, "SELECT COUNT(DISTINCT subject) FROM documents WHERE subject IS NOT NULL AND subject != '' AND (is_deleted IS NULL OR is_deleted = 0)");
        stats.TotalCollections = GetScalarInt(conn, "SELECT COUNT(*) FROM collections");

        return stats;
    }




    public int GetDocumentCount()
    {
        using var conn = OpenConnection();
        return GetScalarInt(conn, "SELECT COUNT(*) FROM documents WHERE (is_deleted IS NULL OR is_deleted = 0)");
    }

    public List<StudyDocument> GetDeletedDocuments()
    {
        const string query = "SELECT * FROM documents WHERE is_deleted = 1 ORDER BY deleted_at DESC";
        return ExecuteReader(query);
    }

    public bool RestoreDocument(int id)
    {
        using var conn = OpenConnection();
        using var transaction = conn.BeginTransaction();

        if (!RestoreDocumentCore(conn, transaction, id))
        {
            transaction.Rollback();
            return false;
        }

        transaction.Commit();
        return true;
    }

    public int RestoreDocuments(IReadOnlyList<int> ids)
    {
        if (ids is null || ids.Count == 0)
            return 0;

        var distinctIds = ids.Distinct().ToList();
        using var conn = OpenConnection();
        using var transaction = conn.BeginTransaction();

        var restored = 0;
        foreach (var id in distinctIds)
        {
            if (RestoreDocumentCore(conn, transaction, id))
                restored++;
        }

        transaction.Commit();
        return restored;
    }

    private static bool RestoreDocumentCore(SqliteConnection connection, SqliteTransaction transaction, int id)
    {
        using var documentCommand = connection.CreateCommand();
        documentCommand.Transaction = transaction;
        documentCommand.CommandText = "SELECT subject, type FROM documents WHERE id = @id AND is_deleted = 1";
        documentCommand.Parameters.AddWithValue("@id", id);

        using var reader = documentCommand.ExecuteReader();
        if (!reader.Read())
            return false;

        var subject = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
        var type = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        reader.Close();

        if (!string.IsNullOrWhiteSpace(subject))
            InsertCatalogValue(connection, transaction, "categories", subject);

        if (!string.IsNullOrWhiteSpace(type))
            InsertCatalogValue(connection, transaction, "document_types", type);

        using var restoreCommand = connection.CreateCommand();
        restoreCommand.Transaction = transaction;
        restoreCommand.CommandText = "UPDATE documents SET is_deleted = 0, deleted_at = NULL WHERE id = @id AND is_deleted = 1";
        restoreCommand.Parameters.AddWithValue("@id", id);
        return restoreCommand.ExecuteNonQuery() > 0;
    }

    public bool PermanentDeleteDocument(int id)
    {
        const string query = "DELETE FROM documents WHERE id = @id AND is_deleted = 1";
        return ExecuteNonQuery(query, new SqliteParameter("@id", id)) > 0;
    }



    public bool BackupDatabase(string destPath)
        => BackupDatabase(destPath, overwrite: true);

    public bool BackupDatabase(string destPath, bool overwrite)
        => BackupDatabase(destPath, overwrite, CancellationToken.None);

    public bool BackupDatabase(string destPath, bool overwrite, CancellationToken cancellationToken)
    {
        string? stagingPath = null;
        try
        {
            if (string.IsNullOrWhiteSpace(destPath))
                return false;

            if (cancellationToken.IsCancellationRequested)
                return false;

            var destinationPath = Path.GetFullPath(destPath);
            if (PathsReferToSameFile(DatabasePath, destinationPath))
                return false;

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(destinationDirectory) || !Directory.Exists(destinationDirectory))
                return false;

            if (File.Exists(destinationPath) && !overwrite)
                return false;

            stagingPath = CreateStagingPath(destinationPath);
            using (var source = OpenConnection())
            using (var destination = OpenConnection(stagingPath, pooling: false, synchronize: false))
            {
                source.BackupDatabase(destination);
            }

            ValidateBackupCandidate(stagingPath);

            if (ShouldAbortOperation(cancellationToken))
                return false;

            if (File.Exists(destinationPath))
                File.Replace(stagingPath, destinationPath, null);
            else
                File.Move(stagingPath, destinationPath);

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (stagingPath is not null)
                DeleteFileIfExists(stagingPath);
        }
    }

    public bool CanRestoreDatabase(string sourcePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath) || PathsReferToSameFile(DatabasePath, sourcePath))
                return false;

            ValidateBackupCandidate(sourcePath, requireDocumentPathIndex: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool RestoreDatabase(string sourcePath)
        => RestoreDatabase(sourcePath, CancellationToken.None);

    public bool RestoreDatabase(string sourcePath, CancellationToken cancellationToken)
    {
        using var operationLock = AcquireDatabaseOperationLock(DatabasePath);
        string? stagingPath = null;
        string? rollbackPath = null;
        var swapped = false;

        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath) || PathsReferToSameFile(DatabasePath, sourcePath))
                return false;

            if (cancellationToken.IsCancellationRequested)
                return false;

            stagingPath = CreateStagingPath(DatabasePath);
            rollbackPath = CreateStagingPath(DatabasePath);

            using (var source = OpenConnection(sourcePath, SqliteOpenMode.ReadOnly, pooling: false, synchronize: false))
            using (var destination = OpenConnection(stagingPath, pooling: false, synchronize: false))
            {
                source.BackupDatabase(destination);
            }

            ValidateBackupCandidate(stagingPath, requireDocumentPathIndex: false);
            DatabaseMigrator.RunMigrations($"Data Source={stagingPath};Foreign Keys=True");
            ValidateBackupCandidate(stagingPath);

            using (var source = OpenConnection())
            using (var destination = OpenConnection(rollbackPath, pooling: false, synchronize: false))
            {
                source.BackupDatabase(destination);
            }

            ValidateBackupCandidate(rollbackPath);

            if (ShouldAbortOperation(cancellationToken))
                return false;

            CloseAllConnections();
            DeleteSqliteSidecars(DatabasePath);
            File.Replace(stagingPath, DatabasePath, null);
            swapped = true;
            ValidateBackupCandidate(DatabasePath);
            return true;
        }
        catch
        {
            if (swapped && rollbackPath is not null && File.Exists(rollbackPath))
            {
                try
                {
                    CloseAllConnections();
                    File.Replace(rollbackPath, DatabasePath, null);
                    DeleteFileIfExists($"{DatabasePath}-wal");
                    DeleteFileIfExists($"{DatabasePath}-shm");
                }
                catch
                {
                }
            }

            return false;
        }
        finally
        {
            if (stagingPath is not null)
                DeleteFileIfExists(stagingPath);
            if (rollbackPath is not null)
                DeleteFileIfExists(rollbackPath);
        }
    }

    /// <summary>
    /// Single decision point for whether a backup/restore commit (File.Replace/File.Move)
    /// must be aborted. Checked immediately before the commit so the commit is never started
    /// once cancellation is requested. Overridable for deterministic boundary testing.
    /// </summary>
    protected virtual bool ShouldAbortOperation(CancellationToken cancellationToken)
        => cancellationToken.IsCancellationRequested;

    private static void InsertCatalogValue(SqliteConnection connection, SqliteTransaction transaction, string tableName, string value)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"INSERT OR IGNORE INTO {tableName} (name) VALUES (@name)";
        command.Parameters.AddWithValue("@name", value);
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
        => OpenConnection(DatabasePath);

    private static SqliteConnection OpenConnection(
        string databasePath,
        SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate,
        bool pooling = true,
        bool synchronize = true)
    {
        DatabaseOperationLock? operationLock = synchronize ? AcquireDatabaseOperationLock(databasePath) : null;
        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                ForeignKeys = true,
                Mode = mode,
                Pooling = pooling
            }.ToString();
            var connection = new SqliteConnection(connectionString);
            if (operationLock is not null)
            {
                connection.StateChange += (_, args) =>
                {
                    if (args.CurrentState == System.Data.ConnectionState.Closed)
                        operationLock.Dispose();
                };
            }
            connection.Open();
            return connection;
        }
        catch
        {
            operationLock?.Dispose();
            throw;
        }
    }

    // ponytail: 同一DBを使う協調プロセスのみ直列化する。外部SQLite書き込みの調停が必要になればDBブローカーへ移行する。

    private static DatabaseOperationLock AcquireDatabaseOperationLock(string databasePath)
    {
        var operationLock = new Mutex(false, GetOperationMutexName(databasePath));
        try
        {
            operationLock.WaitOne();
            return new DatabaseOperationLock(operationLock);
        }
        catch (AbandonedMutexException)
        {
            return new DatabaseOperationLock(operationLock);
        }
        catch
        {
            operationLock.Dispose();
            throw;
        }
    }

    private static string GetOperationMutexName(string databasePath)
    {
        var normalizedPath = Path.GetFullPath(databasePath).ToUpperInvariant();
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalizedPath));
        return $"StudyDocumentManager.Database.{Convert.ToHexString(hash)[..16]}";
    }

    private sealed class DatabaseOperationLock(Mutex mutex) : IDisposable
    {
        private Mutex? _mutex = mutex;

        public void Dispose()
        {
            var currentMutex = Interlocked.Exchange(ref _mutex, null);
            if (currentMutex is null)
                return;

            currentMutex.ReleaseMutex();
            currentMutex.Dispose();
        }
    }

    private static bool PathsReferToSameFile(string firstPath, string secondPath)
        => string.Equals(Path.GetFullPath(firstPath), Path.GetFullPath(secondPath), StringComparison.OrdinalIgnoreCase);

    private static string CreateStagingPath(string targetPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(targetPath));
        var fileName = Path.GetFileName(targetPath);
        return Path.Combine(directory!, $".{fileName}.{Guid.NewGuid():N}.tmp");
    }

    private static void DeleteFileIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    protected virtual void DeleteSqliteSidecars(string databasePath)
    {
        DeleteSidecarOrThrow($"{databasePath}-wal");
        DeleteSidecarOrThrow($"{databasePath}-shm");
    }

    private static void DeleteSidecarOrThrow(string path)
    {
        if (!File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                $"Failed to delete SQLite sidecar '{path}' before replacing the live database; " +
                "aborting the replace so the new database is not paired with stale write-ahead logs.",
                ex);
        }
    }

    private static void ValidateBackupCandidate(string databasePath, bool requireDocumentPathIndex = true)
    {
        using var connection = OpenConnection(databasePath, SqliteOpenMode.ReadOnly, pooling: false, synchronize: false);

        var requiredTables = new[]
        {
            "documents", "collections", "collection_items", "personal_notes", "recent_files",
            "document_relations", "categories", "document_types", "app_settings",
            "student_context", "courses", "semesters", "assignments", "assignment_documents"
        };
        using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
        using var tableReader = tableCommand.ExecuteReader();
        var actualTables = new HashSet<string>(StringComparer.Ordinal);
        while (tableReader.Read())
            actualTables.Add(tableReader.GetString(0));

        var hasSavedSearches = actualTables.Contains("saved_searches");
        var metadataTables = new[] { "student_context", "courses", "semesters", "assignments", "assignment_documents" };
        var metadataTableCount = metadataTables.Count(actualTables.Contains);
        var hasStudentMetadata = metadataTableCount == metadataTables.Length;
        if (metadataTableCount != 0 && !hasStudentMetadata)
            throw new InvalidOperationException("Backup database student metadata schema is incomplete.");

        var expectedTables = requiredTables.ToList();
        if (hasSavedSearches)
            expectedTables.Add("saved_searches");
        if (hasStudentMetadata)
            expectedTables.AddRange(metadataTables);

        if (!actualTables.SetEquals(expectedTables))
            throw new InvalidOperationException("Backup database tables are not supported.");

        ValidateRequiredColumns(connection, "documents", ["id", "name", "subject", "type", "file_path", "notes", "created_at", "file_size", "author", "is_important", "tags", "deadline", "is_deleted", "deleted_at", "status"], ["status"]);
        ValidateRequiredColumns(connection, "collections", ["id", "name", "description", "created_at"]);
        ValidateRequiredColumns(connection, "collection_items", ["id", "collection_id", "document_id", "added_at"]);
        ValidateRequiredColumns(connection, "personal_notes", ["id", "document_id", "content", "created_at", "updated_at"]);
        ValidateRequiredColumns(connection, "recent_files", ["id", "document_id", "opened_at"]);
        ValidateRequiredColumns(connection, "document_relations", ["id", "doc_id_1", "doc_id_2", "relation_type", "created_at"]);
        ValidateRequiredColumns(connection, "categories", ["id", "name", "created_at"]);
        ValidateRequiredColumns(connection, "document_types", ["id", "name", "created_at"]);
        ValidateRequiredColumns(connection, "app_settings", ["key", "value"]);
        if (hasSavedSearches)
            ValidateRequiredColumns(connection, "saved_searches", ["id", "name", "criteria_json", "created_at"]);
        if (hasStudentMetadata)
        {
            ValidateRequiredColumns(connection, "student_context", ["id", "academic_year", "semester", "course", "module", "owner"]);
            ValidateRequiredColumns(connection, "courses", ["id", "name", "code"]);
            ValidateRequiredColumns(connection, "semesters", ["id", "name", "starts_on", "ends_on", "is_active"]);
            ValidateRequiredColumns(connection, "assignments", ["id", "title", "course_id", "semester_id", "official_deadline", "personal_deadline", "status", "priority", "milestone", "notes"]);
            ValidateRequiredColumns(connection, "assignment_documents", ["assignment_id", "document_id"]);
        }

        ValidateCascadeForeignKeys(connection, "collection_items", [("collection_id", "collections", "id"), ("document_id", "documents", "id")]);
        ValidateCascadeForeignKeys(connection, "personal_notes", [("document_id", "documents", "id")]);
        ValidateCascadeForeignKeys(connection, "recent_files", [("document_id", "documents", "id")]);
        ValidateCascadeForeignKeys(connection, "document_relations", [("doc_id_1", "documents", "id"), ("doc_id_2", "documents", "id")]);
        if (hasStudentMetadata)
        {
            ValidateForeignKeys(connection, "assignments", [
                ("course_id", "courses", "id", "SET NULL"),
                ("semester_id", "semesters", "id", "SET NULL")]);
            ValidateCascadeForeignKeys(connection, "assignment_documents", [("assignment_id", "assignments", "id"), ("document_id", "documents", "id")]);
        }
        ValidateUniqueConstraint(connection, "collection_items", ["collection_id", "document_id"]);
        ValidateUniqueConstraint(connection, "recent_files", ["document_id"]);
        ValidateUniqueConstraint(connection, "document_relations", ["doc_id_1", "doc_id_2"]);
        if (hasStudentMetadata)
            ValidateUniqueConstraint(connection, "assignment_documents", ["assignment_id", "document_id"]);
        ValidateDocumentPathIndex(connection, requireDocumentPathIndex);

        foreach (var tableName in expectedTables)
            ValidateIndexesAndTriggers(connection, tableName, allowLegacyDocumentPathIndexes: !requireDocumentPathIndex);

        using var schemaVersionCommand = connection.CreateCommand();
        schemaVersionCommand.CommandText = "SELECT value FROM app_settings WHERE key = 'schema_version'";
        var schemaVersion = schemaVersionCommand.ExecuteScalar()?.ToString();
        if (!string.Equals(schemaVersion, "3", StringComparison.Ordinal) && !string.Equals(schemaVersion, "4", StringComparison.Ordinal))
            throw new InvalidOperationException("Backup database schema version is not supported.");

        using (var documentCommand = connection.CreateCommand())
        {
            documentCommand.CommandText = "SELECT * FROM documents";
            using var documentReader = documentCommand.ExecuteReader();
            while (documentReader.Read())
                MapToDocument(documentReader);
        }

        using (var collectionCommand = connection.CreateCommand())
        {
            collectionCommand.CommandText = "SELECT created_at FROM collections";
            using var collectionReader = collectionCommand.ExecuteReader();
            while (collectionReader.Read())
                DateTime.Parse(collectionReader.GetString(0));
        }

        using (var recentFilesCommand = connection.CreateCommand())
        {
            recentFilesCommand.CommandText = "SELECT opened_at FROM recent_files";
            using var recentFilesReader = recentFilesCommand.ExecuteReader();
            while (recentFilesReader.Read())
                DateTime.Parse(recentFilesReader.GetString(0));
        }

        using var integrityCommand = connection.CreateCommand();
        integrityCommand.CommandText = "PRAGMA integrity_check";
        if (!string.Equals(integrityCommand.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Backup database integrity check failed.");

        using var foreignKeyCommand = connection.CreateCommand();
        foreignKeyCommand.CommandText = "PRAGMA foreign_key_check";
        using var foreignKeyReader = foreignKeyCommand.ExecuteReader();
        if (foreignKeyReader.Read())
            throw new InvalidOperationException("Backup database foreign key check failed.");
    }

    private static void ValidateRequiredColumns(SqliteConnection connection, string tableName, IReadOnlyCollection<string> requiredColumns)
        => ValidateRequiredColumns(connection, tableName, requiredColumns, []);

    private static void ValidateRequiredColumns(SqliteConnection connection, string tableName, IReadOnlyCollection<string> requiredColumns, IReadOnlyCollection<string> optionalColumns)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName})";
        using var reader = command.ExecuteReader();
        var actualColumns = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read())
            actualColumns.Add(reader.GetString(1));

        var unsupportedColumns = actualColumns.Where(column => !requiredColumns.Contains(column, StringComparer.Ordinal)).ToList();
        if (unsupportedColumns.Count > 0)
            throw new InvalidOperationException($"Backup database table '{tableName}' is not supported.");

        var missingRequiredColumns = requiredColumns.Except(optionalColumns, StringComparer.Ordinal).Except(actualColumns, StringComparer.Ordinal).ToList();
        if (missingRequiredColumns.Count > 0)
            throw new InvalidOperationException($"Backup database table '{tableName}' is not supported.");
    }


    private static void ValidateCascadeForeignKeys(
        SqliteConnection connection,
        string tableName,
        IReadOnlyCollection<(string From, string ParentTable, string ParentColumn)> expectedForeignKeys)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_key_list({tableName})";
        using var reader = command.ExecuteReader();
        var actualForeignKeys = new List<(string From, string ParentTable, string ParentColumn, string OnDelete)>();
        while (reader.Read())
            actualForeignKeys.Add((reader.GetString(3), reader.GetString(2), reader.GetString(4), reader.GetString(6)));

        if (actualForeignKeys.Count != expectedForeignKeys.Count || actualForeignKeys.Any(foreignKey =>
            !expectedForeignKeys.Contains((foreignKey.From, foreignKey.ParentTable, foreignKey.ParentColumn)) ||
            !string.Equals(foreignKey.OnDelete, "CASCADE", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Backup database foreign key layout for '{tableName}' is not supported.");
        }
    }

    private static void ValidateForeignKeys(
        SqliteConnection connection,
        string tableName,
        IReadOnlyCollection<(string From, string ParentTable, string ParentColumn, string OnDelete)> expectedForeignKeys)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_key_list({tableName})";
        using var reader = command.ExecuteReader();
        var actualForeignKeys = new List<(string From, string ParentTable, string ParentColumn, string OnDelete)>();
        while (reader.Read())
            actualForeignKeys.Add((reader.GetString(3), reader.GetString(2), reader.GetString(4), reader.GetString(6)));

        if (actualForeignKeys.Count != expectedForeignKeys.Count || actualForeignKeys.Any(foreignKey => !expectedForeignKeys.Contains(foreignKey)))
            throw new InvalidOperationException($"Backup database foreign key layout for '{tableName}' is not supported.");
    }

    private static void ValidateUniqueConstraint(SqliteConnection connection, string tableName, IReadOnlyList<string> expectedColumns)
    {
        using var indexesCommand = connection.CreateCommand();
        indexesCommand.CommandText = $"PRAGMA index_list({tableName})";
        using var indexReader = indexesCommand.ExecuteReader();
        var indexNames = new List<string>();
        while (indexReader.Read())
        {
            if (indexReader.GetInt32(2) == 1)
                indexNames.Add(indexReader.GetString(1));
        }
        indexReader.Close();

        var hasConstraint = indexNames.Any(indexName =>
        {
            using var columnsCommand = connection.CreateCommand();
            columnsCommand.CommandText = $"PRAGMA index_info({indexName})";
            using var columnReader = columnsCommand.ExecuteReader();
            var columns = new List<string>();
            while (columnReader.Read())
                columns.Add(columnReader.GetString(2));
            return columns.SequenceEqual(expectedColumns, StringComparer.Ordinal);
        });

        if (!hasConstraint)
            throw new InvalidOperationException($"Backup database unique constraint for '{tableName}' is not supported.");
    }


private static void ValidateDocumentPathIndex(SqliteConnection connection, bool requireDocumentPathIndex)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = 'idx_documents_file_path_unique'";
        var sql = command.ExecuteScalar()?.ToString();
        const string expected = "CREATE UNIQUE INDEX idx_documents_file_path_unique ON documents(file_path COLLATE BINARY) WHERE file_path IS NOT NULL AND file_path <> ''";
        if (sql is null && !requireDocumentPathIndex)
            return;
        if (!string.Equals(sql, expected, StringComparison.Ordinal))
            throw new InvalidOperationException("Backup database document path index is not supported.");
    }

    private static bool IsDocumentPathUniqueIndex(SqliteConnection connection, string indexName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_info(\"{indexName.Replace("\"", "\"\"")}\")";
        using var reader = command.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read())
            columns.Add(reader.GetString(2));
        return columns.SequenceEqual(["file_path"], StringComparer.Ordinal);
    }

    private static void ValidateIndexesAndTriggers(SqliteConnection connection, string tableName, bool allowLegacyDocumentPathIndexes)
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
        var indexes = new List<(string Name, bool IsUnique, string Origin)>();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"PRAGMA index_list({tableName})";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                indexes.Add((reader.GetString(1), reader.GetInt32(2) == 1, reader.GetString(3)));
        }

        foreach (var index in indexes)
        {
            var isLegacyDocumentPathIndex = allowLegacyDocumentPathIndexes && tableName == "documents" && index.IsUnique && IsDocumentPathUniqueIndex(connection, index.Name);
            if (tableName == "documents" && index.Origin == "u" && !isLegacyDocumentPathIndex)
                throw new InvalidOperationException($"Backup database unique constraint on '{tableName}' is not supported.");
            if (index.Origin == "c" && !allowedIndexes.Contains(index.Name) && !isLegacyDocumentPathIndex)
                throw new InvalidOperationException($"Backup database index on '{tableName}' is not supported.");
        }

        using var triggersCommand = connection.CreateCommand();
        triggersCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'trigger' AND tbl_name = @tableName";
        triggersCommand.Parameters.AddWithValue("@tableName", tableName);
        if (Convert.ToInt32(triggersCommand.ExecuteScalar()) > 0)
            throw new InvalidOperationException($"Backup database trigger on '{tableName}' is not supported.");
    }

    private List<StudyDocument> ExecuteReader(string query, params SqliteParameter[] parameters)
    {
        var documents = new List<StudyDocument>();

        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand(query, conn);

        foreach (var param in parameters)
            cmd.Parameters.Add(param);

        return cmd.ExecuteNonQuery();
    }

    private List<string> ExecuteStringList(string query, string columnName)
    {
        var result = new List<string>();
        using var conn = OpenConnection();
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

    private static StudyDocument MapToDocument(SqliteDataReader reader)
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
            Deadline = reader["deadline"] is DBNull ? null : DateTime.Parse(reader["deadline"].ToString()!),
            Status = ReadStatus(reader)
        };
    }

    private static string ReadStatus(SqliteDataReader reader)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (!string.Equals(reader.GetName(i), "status", StringComparison.Ordinal))
                continue;
            return reader.IsDBNull(i) ? DocumentStatus.Unread : reader.GetString(i);
        }

        return DocumentStatus.Unread;
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
            new SqliteParameter("@deadline", doc.Deadline.HasValue ? doc.Deadline.Value.ToString("yyyy-MM-dd HH:mm:ss") : DBNull.Value),
            new SqliteParameter("@status", DocumentStatus.IsValid(doc.Status) ? doc.Status : DocumentStatus.Unread)
        ];
    }



    public string? GetPersonalNote(int documentId)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT content FROM personal_notes WHERE document_id = @documentId";
        cmd.Parameters.AddWithValue("@documentId", documentId);
        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    public bool SavePersonalNote(int documentId, string content)
    {
        using var conn = OpenConnection();

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
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM personal_notes WHERE document_id = @documentId";
        cmd.Parameters.AddWithValue("@documentId", documentId);
        return cmd.ExecuteNonQuery() > 0;
    }



    public void AddDocumentRelation(int docId1, int docId2, string relationType = "related")
    {
        int lo = Math.Min(docId1, docId2);
        int hi = Math.Max(docId1, docId2);
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM document_relations WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", relationId);
        cmd.ExecuteNonQuery();
    }



    public List<(string Label, int Count)> GetDocumentsByDay(int days = 7)
    {
        var results = new List<(string, int)>();
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();

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
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();

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
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM collection_items WHERE collection_id = @colId AND document_id = @docId";
        cmd.Parameters.AddWithValue("@colId", collectionId);
        cmd.Parameters.AddWithValue("@docId", documentId);
        return cmd.ExecuteNonQuery() > 0;
    }



    public List<(string Name, int Count)> GetSubjectsWithCount()
    {
        var results = new List<(string, int)>();
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO categories (name) VALUES (@name)";
        cmd.Parameters.AddWithValue("@name", name);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool AddType(string name)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO document_types (name) VALUES (@name)";
        cmd.Parameters.AddWithValue("@name", name);
        return cmd.ExecuteNonQuery() > 0;
    }

    public List<string> GetAllSubjects()
    {
        var results = new List<string>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM categories ORDER BY name";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) results.Add(reader.GetString(0));
        return results;
    }

    public List<string> GetAllTypes()
    {
        var results = new List<string>();
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM document_types ORDER BY name";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) results.Add(reader.GetString(0));
        return results;
    }

    public bool DeleteSubject(string name)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM categories WHERE name = @name";
        cmd.Parameters.AddWithValue("@name", name);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool DeleteType(string name)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM document_types WHERE name = @name";
        cmd.Parameters.AddWithValue("@name", name);
        return cmd.ExecuteNonQuery() > 0;
    }



    public int BulkSoftDelete(List<int> ids)
    {
        if (ids == null || ids.Count == 0) return 0;
        using var conn = OpenConnection();

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
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
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

    public int BulkUpdateStatus(List<int> ids, string status)
    {
        if (ids == null || ids.Count == 0) return 0;
        if (!DocumentStatus.IsValid(status)) return 0;
        using var conn = OpenConnection();
        var paramNames = new List<string>();
        using var cmd = conn.CreateCommand();
        for (int i = 0; i < ids.Count; i++)
        {
            paramNames.Add($"@id{i}");
            cmd.Parameters.AddWithValue($"@id{i}", ids[i]);
        }
        cmd.Parameters.AddWithValue("@status", status);
        cmd.CommandText = $"UPDATE documents SET status = @status WHERE id IN ({string.Join(",", paramNames)}) AND (is_deleted IS NULL OR is_deleted = 0)";
        return cmd.ExecuteNonQuery();
    }

    public BulkEditOutcome BulkEditMetadata(IReadOnlyList<int> documentIds, BulkEditChanges changes)
    {
        var ids = documentIds ?? Array.Empty<int>();
        var requested = ids.Count;
        if (requested == 0 || changes is null || !changes.HasAnyChange)
            return new BulkEditOutcome { Requested = requested };

        var results = new List<BulkItemResult>(requested);
        using var conn = OpenConnection();
        using var transaction = conn.BeginTransaction();

        try
        {
            var targetCollectionMissing = false;
            if (changes.AddToCollectionId is int requestedCollectionId)
            {
                using var collectionCheck = conn.CreateCommand();
                collectionCheck.Transaction = transaction;
                collectionCheck.CommandText = "SELECT COUNT(*) FROM collections WHERE id = @collectionId";
                collectionCheck.Parameters.AddWithValue("@collectionId", requestedCollectionId);
                targetCollectionMissing = Convert.ToInt32(collectionCheck.ExecuteScalar()) == 0;
            }

            foreach (var id in ids)
            {
                var succeeded = false;
                var statusAllowed = changes.Status is null || DocumentStatus.IsValid(changes.Status);
                if (!targetCollectionMissing && statusAllowed)
                {
                    try
                    {
                        transaction.Save(BulkEditItemSavepoint);
                        try
                        {
                            // whitelist check must precede the UPDATE so a rejected field-set never partially applies
                            succeeded = ExecuteBulkEditMetadataUpdate(conn, transaction, id, changes);
                            if (succeeded && changes.AddToCollectionId is int collectionId)
                                InsertBulkEditCollectionLink(conn, transaction, collectionId, id);
                            transaction.Release(BulkEditItemSavepoint);
                        }
                        catch
                        {
                            transaction.Rollback(BulkEditItemSavepoint);
                            transaction.Release(BulkEditItemSavepoint);
                            throw;
                        }
                    }
                    catch (SqliteException)
                    {
                        succeeded = false;
                    }
                }
                results.Add(new BulkItemResult(id, succeeded));
            }

            SeedBulkEditCatalogValues(conn, transaction, changes, results);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return new BulkEditOutcome
        {
            Requested = requested,
            Succeeded = results.Count(r => r.Success),
            Items = results
        };
    }

    private const string BulkEditItemSavepoint = "bulk_edit_item";

    private static bool ExecuteBulkEditMetadataUpdate(
        SqliteConnection conn, SqliteTransaction transaction, int id, BulkEditChanges changes)
    {
        var sets = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;

        if (changes.Subject is not null)
        {
            sets.Add("subject = @subject");
            cmd.Parameters.AddWithValue("@subject", string.IsNullOrEmpty(changes.Subject) ? DBNull.Value : changes.Subject);
        }
        if (changes.Type is not null)
        {
            sets.Add("type = @type");
            cmd.Parameters.AddWithValue("@type", string.IsNullOrEmpty(changes.Type) ? DBNull.Value : changes.Type);
        }
        if (changes.Tags is not null)
        {
            sets.Add("tags = @tags");
            cmd.Parameters.AddWithValue("@tags", string.IsNullOrEmpty(changes.Tags) ? DBNull.Value : changes.Tags);
        }
        if (changes.IsImportant is not null)
        {
            sets.Add("is_important = @is_important");
            cmd.Parameters.AddWithValue("@is_important", changes.IsImportant.Value ? 1 : 0);
        }
        if (changes.Deadline is not null)
        {
            sets.Add("deadline = @deadline");
            cmd.Parameters.AddWithValue("@deadline", changes.Deadline.Value.ToString("yyyy-MM-dd HH:mm:ss"));
        }
        if (changes.Status is not null)
        {
            sets.Add("status = @status");
            cmd.Parameters.AddWithValue("@status", changes.Status);
        }

        if (sets.Count == 0)
        {
            using var existsCommand = conn.CreateCommand();
            existsCommand.Transaction = transaction;
            existsCommand.CommandText = "SELECT COUNT(*) FROM documents WHERE id = @id AND (is_deleted IS NULL OR is_deleted = 0)";
            existsCommand.Parameters.AddWithValue("@id", id);
            return Convert.ToInt32(existsCommand.ExecuteScalar()) == 1;
        }

        cmd.CommandText = $"UPDATE documents SET {string.Join(", ", sets)} WHERE id = @id AND (is_deleted IS NULL OR is_deleted = 0)";
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() == 1;
    }

    private static void InsertBulkEditCollectionLink(
        SqliteConnection conn, SqliteTransaction transaction, int collectionId, int documentId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "INSERT OR IGNORE INTO collection_items (collection_id, document_id) VALUES (@collectionId, @documentId)";
        cmd.Parameters.AddWithValue("@collectionId", collectionId);
        cmd.Parameters.AddWithValue("@documentId", documentId);
        cmd.ExecuteNonQuery();
    }

    private static void SeedBulkEditCatalogValues(
        SqliteConnection conn, SqliteTransaction transaction, BulkEditChanges changes, IReadOnlyList<BulkItemResult> results)
    {
        if (!results.Any(r => r.Success))
            return;

        if (!string.IsNullOrWhiteSpace(changes.Subject))
            InsertCatalogValue(conn, transaction, "categories", changes.Subject);

        if (!string.IsNullOrWhiteSpace(changes.Type))
            InsertCatalogValue(conn, transaction, "document_types", changes.Type);
    }



    public int EmptyRecycleBin()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM documents WHERE is_deleted = 1";
        return cmd.ExecuteNonQuery();
    }

    public int GetDeletedDocumentCount()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM documents WHERE is_deleted = 1";
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }



    public bool AddRecentFile(int documentId)
    {
        using var conn = OpenConnection();
        using (var exists = conn.CreateCommand())
        {
            exists.CommandText = "SELECT COUNT(*) FROM documents WHERE id = @docId AND (is_deleted IS NULL OR is_deleted = 0)";
            exists.Parameters.AddWithValue("@docId", documentId);
            if (Convert.ToInt32(exists.ExecuteScalar()) == 0)
                return false;
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"INSERT OR REPLACE INTO recent_files (document_id, opened_at)
                                VALUES (@docId, datetime('now','localtime'))";
            cmd.Parameters.AddWithValue("@docId", documentId);
            cmd.ExecuteNonQuery();
        }

        using (var trim = conn.CreateCommand())
        {
            trim.CommandText = @"DELETE FROM recent_files WHERE id NOT IN
                                (SELECT id FROM recent_files ORDER BY opened_at DESC LIMIT 20)";
            trim.ExecuteNonQuery();
        }

        return true;
    }

    public List<(int Id, string Name, string? Subject, string? Type, string? FilePath, DateTime OpenedAt)> GetRecentFiles()
    {
        var results = new List<(int, string, string?, string?, string?, DateTime)>();
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM recent_files WHERE document_id = @docId";
        cmd.Parameters.AddWithValue("@docId", documentId);
        cmd.ExecuteNonQuery();
    }

    public void ClearRecentFiles()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM recent_files";
        cmd.ExecuteNonQuery();
    }



    public int GetTotalDocumentCount()
    {
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
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
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO app_settings (key, value) VALUES (@key, @value)";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.ExecuteNonQuery();
    }

    public List<SavedSearch> GetSavedSearches()
    {
        const string query = "SELECT * FROM saved_searches ORDER BY name COLLATE NOCASE";
        var results = new List<SavedSearch>();
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand(query, conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            results.Add(MapToSavedSearch(reader));
        return results;
    }

    public SavedSearch? GetSavedSearchById(int id)
    {
        const string query = "SELECT * FROM saved_searches WHERE id = @id";
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand(query, conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapToSavedSearch(reader) : null;
    }

    public bool SavedSearchNameExists(string name)
    {
        const string query = "SELECT COUNT(*) FROM saved_searches WHERE name = @name COLLATE NOCASE";
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand(query, conn);
        cmd.Parameters.AddWithValue("@name", name);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public int InsertSavedSearch(SavedSearch savedSearch)
    {
        const string query = """
            INSERT INTO saved_searches (name, criteria_json)
            VALUES (@name, @criteria_json);
            SELECT last_insert_rowid();
            """;
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand(query, conn);
        cmd.Parameters.AddWithValue("@name", savedSearch.Name);
        cmd.Parameters.AddWithValue("@criteria_json", savedSearch.CriteriaJson);
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    public bool UpdateSavedSearch(SavedSearch savedSearch)
    {
        const string query = """
            UPDATE saved_searches SET name = @name, criteria_json = @criteria_json
            WHERE id = @id
            """;
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand(query, conn);
        cmd.Parameters.AddWithValue("@id", savedSearch.Id);
        cmd.Parameters.AddWithValue("@name", savedSearch.Name);
        cmd.Parameters.AddWithValue("@criteria_json", savedSearch.CriteriaJson);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool DeleteSavedSearch(int id)
    {
        const string query = "DELETE FROM saved_searches WHERE id = @id";
        using var conn = OpenConnection();
        using var cmd = new SqliteCommand(query, conn);
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    private static SavedSearch MapToSavedSearch(SqliteDataReader reader)
    {
        return new SavedSearch
        {
            Id = reader.GetInt32(reader.GetOrdinal("id")),
            Name = reader["name"]?.ToString() ?? string.Empty,
            CriteriaJson = reader["criteria_json"]?.ToString() ?? string.Empty,
            CreatedAt = reader["created_at"] is DBNull ? DateTime.Now : DateTime.Parse(reader["created_at"].ToString()!)
        };
    }

    public StudentContext? GetStudentContext()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, academic_year, semester, course, module, owner FROM student_context WHERE id = 1";
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new StudentContext
        {
            Id = reader.GetInt32(0),
            AcademicYear = reader.GetString(1),
            Semester = reader.GetString(2),
            Course = reader.GetString(3),
            Module = reader.GetString(4),
            Owner = reader.GetString(5)
        };
    }

    public bool SaveStudentContext(StudentContext context)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO student_context(id, academic_year, semester, course, module, owner) VALUES(1, @academic_year, @semester, @course, @module, @owner) ON CONFLICT(id) DO UPDATE SET academic_year=excluded.academic_year, semester=excluded.semester, course=excluded.course, module=excluded.module, owner=excluded.owner";
        cmd.Parameters.AddWithValue("@academic_year", context.AcademicYear.Trim());
        cmd.Parameters.AddWithValue("@semester", context.Semester.Trim());
        cmd.Parameters.AddWithValue("@course", context.Course.Trim());
        cmd.Parameters.AddWithValue("@module", context.Module.Trim());
        cmd.Parameters.AddWithValue("@owner", context.Owner.Trim());
        return cmd.ExecuteNonQuery() > 0;
    }

    public List<Course> GetCourses()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, code FROM courses ORDER BY name, code";
        using var reader = cmd.ExecuteReader();
        var result = new List<Course>();
        while (reader.Read())
            result.Add(new Course { Id = reader.GetInt32(0), Name = reader.GetString(1), Code = reader.GetString(2) });
        return result;
    }

    public int AddCourse(Course course)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO courses(name, code) VALUES(@name, @code); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@name", course.Name.Trim());
        cmd.Parameters.AddWithValue("@code", course.Code.Trim());
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public bool UpdateCourse(Course course)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE courses SET name=@name, code=@code WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", course.Id);
        cmd.Parameters.AddWithValue("@name", course.Name.Trim());
        cmd.Parameters.AddWithValue("@code", course.Code.Trim());
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool DeleteCourse(int id)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM courses WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    public List<Semester> GetSemesters()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, starts_on, ends_on, is_active FROM semesters ORDER BY starts_on, name";
        using var reader = cmd.ExecuteReader();
        var result = new List<Semester>();
        while (reader.Read())
        {
            result.Add(new Semester
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                StartsOn = reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2)),
                EndsOn = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                IsActive = reader.GetInt64(4) != 0
            });
        }
        return result;
    }

    public int AddSemester(Semester semester)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO semesters(name, starts_on, ends_on, is_active) VALUES(@name, @starts, @ends, @active); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@name", semester.Name.Trim());
        cmd.Parameters.AddWithValue("@starts", (object?)semester.StartsOn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ends", (object?)semester.EndsOn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@active", semester.IsActive ? 1 : 0);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public bool UpdateSemester(Semester semester)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE semesters SET name=@name, starts_on=@starts, ends_on=@ends, is_active=@active WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", semester.Id);
        cmd.Parameters.AddWithValue("@name", semester.Name.Trim());
        cmd.Parameters.AddWithValue("@starts", (object?)semester.StartsOn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ends", (object?)semester.EndsOn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@active", semester.IsActive ? 1 : 0);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool DeleteSemester(int id)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM semesters WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    public List<Assignment> GetAssignments()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, title, course_id, semester_id, official_deadline, personal_deadline, status, priority, milestone, notes FROM assignments ORDER BY COALESCE(personal_deadline, official_deadline), title";
        using var reader = cmd.ExecuteReader();
        var result = new List<Assignment>();
        while (reader.Read())
            result.Add(MapToAssignment(reader));
        return result;
    }

    public Assignment? GetAssignment(int id)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, title, course_id, semester_id, official_deadline, personal_deadline, status, priority, milestone, notes FROM assignments WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapToAssignment(reader) : null;
    }

    public int AddAssignment(Assignment assignment)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO assignments(title, course_id, semester_id, official_deadline, personal_deadline, status, priority, milestone, notes) VALUES(@title, @course, @semester, @official, @personal, @status, @priority, @milestone, @notes); SELECT last_insert_rowid();";
        BindAssignment(cmd, assignment);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public bool UpdateAssignment(Assignment assignment)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE assignments SET title=@title, course_id=@course, semester_id=@semester, official_deadline=@official, personal_deadline=@personal, status=@status, priority=@priority, milestone=@milestone, notes=@notes WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", assignment.Id);
        BindAssignment(cmd, assignment);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool DeleteAssignment(int id)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM assignments WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool LinkAssignmentDocument(int assignmentId, int documentId)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO assignment_documents(assignment_id, document_id) SELECT @assignment, @document WHERE EXISTS (SELECT 1 FROM assignments WHERE id=@assignment) AND EXISTS (SELECT 1 FROM documents WHERE id=@document)";
        cmd.Parameters.AddWithValue("@assignment", assignmentId);
        cmd.Parameters.AddWithValue("@document", documentId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool UnlinkAssignmentDocument(int assignmentId, int documentId)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM assignment_documents WHERE assignment_id=@assignment AND document_id=@document";
        cmd.Parameters.AddWithValue("@assignment", assignmentId);
        cmd.Parameters.AddWithValue("@document", documentId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool ReplaceAssignmentDocumentLinks(int assignmentId, IReadOnlyList<int> documentIds)
    {
        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            using (var delete = conn.CreateCommand())
            {
                delete.Transaction = tx;
                delete.CommandText = "DELETE FROM assignment_documents WHERE assignment_id=@assignment";
                delete.Parameters.AddWithValue("@assignment", assignmentId);
                delete.ExecuteNonQuery();
            }

            foreach (var documentId in documentIds.Distinct())
            {
                using var insert = conn.CreateCommand();
                insert.Transaction = tx;
                insert.CommandText = "INSERT INTO assignment_documents(assignment_id, document_id) VALUES(@assignment, @document)";
                insert.Parameters.AddWithValue("@assignment", assignmentId);
                insert.Parameters.AddWithValue("@document", documentId);
                insert.ExecuteNonQuery();
            }

            tx.Commit();
            return true;
        }
        catch
        {
            tx.Rollback();
            return false;
        }
    }

    public List<int> GetAssignmentDocumentIds(int assignmentId)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT document_id FROM assignment_documents WHERE assignment_id=@assignment ORDER BY document_id";
        cmd.Parameters.AddWithValue("@assignment", assignmentId);
        using var reader = cmd.ExecuteReader();
        var result = new List<int>();
        while (reader.Read()) result.Add(reader.GetInt32(0));
        return result;
    }

    private static void BindAssignment(SqliteCommand cmd, Assignment assignment)
    {
        cmd.Parameters.AddWithValue("@title", assignment.Title.Trim());
        cmd.Parameters.AddWithValue("@course", (object?)assignment.CourseId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@semester", (object?)assignment.SemesterId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@official", (object?)assignment.OfficialDeadline ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@personal", (object?)assignment.PersonalDeadline ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", assignment.Status.Trim());
        cmd.Parameters.AddWithValue("@priority", assignment.Priority.Trim());
        cmd.Parameters.AddWithValue("@milestone", assignment.Milestone.Trim());
        cmd.Parameters.AddWithValue("@notes", assignment.Notes);
    }

    private static Assignment MapToAssignment(SqliteDataReader reader)
    {
        return new Assignment
        {
            Id = reader.GetInt32(0),
            Title = reader.GetString(1),
            CourseId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            SemesterId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            OfficialDeadline = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
            PersonalDeadline = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5)),
            Status = reader.GetString(6),
            Priority = reader.GetString(7),
            Milestone = reader.GetString(8),
            Notes = reader.GetString(9)
        };
    }

}
