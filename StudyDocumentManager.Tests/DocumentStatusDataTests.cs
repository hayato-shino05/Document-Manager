using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;
using Xunit;

namespace StudyDocumentManager.Tests;

public class DocumentStatusDataTests : DatabaseTestBase
{
    [Fact]
    public void InitializeDatabase_FreshDb_DocumentsTableHasStatusColumnDefaultingToUnread()
    {
        using var connection = new SqliteConnection(Db.ConnectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(documents)";
        using var reader = cmd.ExecuteReader();

        var hasStatusColumn = false;
        while (reader.Read())
        {
            if (!string.Equals(reader.GetString(1), "status", StringComparison.Ordinal))
                continue;

            hasStatusColumn = true;
            Assert.Equal("TEXT", reader.GetString(2));
            Assert.Equal(1, reader.GetInt32(3));
            Assert.Equal("'unread'", reader.GetString(4));
        }

        Assert.True(hasStatusColumn);
    }

    [Fact]
    public void Add_NewDocumentWithoutTouchingStatus_PersistsUnread()
    {
        var doc = new StudyDocument { Name = "Fresh document" };

        Assert.True(Repo.Add(doc));

        var loaded = Repo.GetById(doc.Id);
        Assert.NotNull(loaded);
        Assert.Equal(DocumentStatus.Unread, loaded.Status);
    }

    [Fact]
    public void Add_WithCompletedStatus_RoundtripsThroughGetById()
    {
        var doc = new StudyDocument { Name = "Roundtrip document", Status = DocumentStatus.Completed };

        Assert.True(Repo.Add(doc));

        var loaded = Repo.GetById(doc.Id);
        Assert.NotNull(loaded);
        Assert.Equal(DocumentStatus.Completed, loaded.Status);
    }

    [Fact]
    public void Update_ChangedStatus_Persists()
    {
        var doc = new StudyDocument { Name = "Editable document" };
        Assert.True(Repo.Add(doc));

        var loaded = Repo.GetById(doc.Id);
        Assert.NotNull(loaded);
        loaded.Status = DocumentStatus.InProgress;

        Assert.True(Repo.Update(loaded));
        Assert.Equal(DocumentStatus.InProgress, Repo.GetById(doc.Id)!.Status);
    }

    [Fact]
    public void InitializeDatabase_LegacySchemaWithoutStatusColumn_BackfillsUnreadAndKeepsActiveQuery()
    {
        var legacyPath = Path.Combine(Path.GetTempPath(), $"sdm_status_upgrade_{Guid.NewGuid():N}.db");
        try
        {
            CreateV3StyleDatabaseWithoutStatusColumn(legacyPath);

            var database = new DatabaseHelper();
            database.SetDatabasePath(legacyPath);
            database.InitializeDatabase();

            using var connection = new SqliteConnection(database.ConnectionString);
            connection.Open();

            Assert.Equal(1L, Scalar(connection, "SELECT COUNT(*) FROM pragma_table_info('documents') WHERE name = 'status'"));
            Assert.Equal(DocumentStatus.Unread, ScalarString(connection, "SELECT status FROM documents WHERE id = 1"));
            Assert.Equal(DocumentStatus.Unread, ScalarString(connection, "SELECT status FROM documents WHERE id = 2"));

            var repository = new DocumentRepository(database);
            var active = Assert.Single(repository.GetAll());
            Assert.Equal("Upgrade active", active.Name);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(legacyPath))
                File.Delete(legacyPath);
        }
    }

    [Fact]
    public void InitializeDatabase_CalledTwice_IsIdempotentAndKeepsCustomStatus()
    {
        var doc = new StudyDocument { Name = "Persistent document", Status = DocumentStatus.NeedsAction };
        Assert.True(Repo.Add(doc));

        Db.InitializeDatabase();
        Db.InitializeDatabase();

        Assert.Single(Repo.GetAll());
        var loaded = Repo.GetById(doc.Id);
        Assert.NotNull(loaded);
        Assert.Equal(DocumentStatus.NeedsAction, loaded.Status);
    }

    [Fact]
    public void SearchAdvancedWithStatus_KeywordPlusStatus_NarrowsResults()
    {
        var readDoc = new StudyDocument { Name = "Alpha report", Status = DocumentStatus.Read };
        var completedDoc = new StudyDocument { Name = "Alpha memo", Status = DocumentStatus.Completed };
        Assert.True(Repo.Add(readDoc));
        Assert.True(Repo.Add(completedDoc));

        var combined = Repo.SearchAdvancedWithStatus("Alpha", "", "", null, null, null, null, null, DocumentStatus.Read);

        var narrowed = Assert.Single(combined);
        Assert.Equal(readDoc.Id, narrowed.Id);

        var keywordOnly = Repo.SearchAdvancedWithStatus("Alpha", "", "", null, null, null, null, null, null);
        Assert.Equal(2, keywordOnly.Count);
    }

    [Fact]
    public void SearchAdvancedWithStatus_NullStatus_MatchesPlainSearchAdvanced()
    {
        var first = new StudyDocument { Name = "Shared report", Status = DocumentStatus.Archived };
        var second = new StudyDocument { Name = "Other notes", Status = DocumentStatus.Unread };
        Assert.True(Repo.Add(first));
        Assert.True(Repo.Add(second));

        var viaPlain = Repo.SearchAdvanced("", "", "", null, null, null, null, null);
        var viaStatus = Repo.SearchAdvancedWithStatus("", "", "", null, null, null, null, null, null);

        Assert.Equal(viaPlain.Select(d => d.Id), viaStatus.Select(d => d.Id));
        Assert.Equal(viaPlain.Select(d => d.Status), viaStatus.Select(d => d.Status));
    }

    [Fact]
    public void GetStatusCounts_ExcludesSoftDeleted_GroupsActiveOnly()
    {
        var readKept = new StudyDocument { Name = "Read kept", Status = DocumentStatus.Read };
        var completed = new StudyDocument { Name = "Completed doc", Status = DocumentStatus.Completed };
        var readDeleted = new StudyDocument { Name = "Read deleted", Status = DocumentStatus.Read };
        Assert.True(Repo.Add(readKept));
        Assert.True(Repo.Add(completed));
        Assert.True(Repo.Add(readDeleted));
        Assert.True(Repo.Delete(readDeleted.Id));

        var counts = Repo.GetStatusCounts();

        Assert.Equal(
            new Dictionary<string, int>
            {
                [DocumentStatus.Read] = 1,
                [DocumentStatus.Completed] = 1
            },
            counts);
    }

    [Fact]
    public void BulkUpdateStatus_ActiveIdsUpdated_SoftDeletedSkipped()
    {
        var activeA = new StudyDocument { Name = "Bulk A" };
        var activeB = new StudyDocument { Name = "Bulk B" };
        var softDeleted = new StudyDocument { Name = "Bulk C" };
        Assert.True(Repo.Add(activeA));
        Assert.True(Repo.Add(activeB));
        Assert.True(Repo.Add(softDeleted));
        Assert.True(Repo.Delete(softDeleted.Id));

        var affected = Repo.BulkUpdateStatus([activeA.Id, activeB.Id, softDeleted.Id], DocumentStatus.Completed);

        Assert.Equal(2, affected);
        Assert.Equal(DocumentStatus.Completed, Repo.GetById(activeA.Id)!.Status);
        Assert.Equal(DocumentStatus.Completed, Repo.GetById(activeB.Id)!.Status);
        Assert.Equal(DocumentStatus.Unread, Repo.GetById(softDeleted.Id)!.Status);
    }

    [Fact]
    public void BulkUpdateStatus_InvalidStatus_AffectsZeroRows()
    {
        var doc = new StudyDocument { Name = "Guarded document", Status = DocumentStatus.Read };
        Assert.True(Repo.Add(doc));

        var affected = Repo.BulkUpdateStatus([doc.Id], "bogus");

        Assert.Equal(0, affected);
        Assert.Equal(DocumentStatus.Read, Repo.GetById(doc.Id)!.Status);
    }

    [Fact]
    public void Add_InvalidOrMissingStatus_IsCoercedToUnreadOnWrite()
    {
        var bogus = new StudyDocument { Name = "Bogus status document", Status = "bogus" };
        var nullStatus = new StudyDocument { Name = "Null status document", Status = null! };

        Assert.True(Repo.Add(bogus));
        Assert.True(Repo.Add(nullStatus));

        Assert.Equal(DocumentStatus.Unread, Repo.GetById(bogus.Id)!.Status);
        Assert.Equal(DocumentStatus.Unread, Repo.GetById(nullStatus.Id)!.Status);
    }

    private static void CreateV3StyleDatabaseWithoutStatusColumn(string databasePath)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Foreign Keys=True");
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
            INSERT INTO documents (id, name, created_at, is_deleted, deleted_at) VALUES (1, 'Upgrade active', '2026-05-01 10:00:00', 0, NULL);
            INSERT INTO documents (id, name, created_at, is_deleted, deleted_at) VALUES (2, 'Upgrade deleted', '2026-05-02 10:00:00', 1, '2026-05-03 10:00:00');
            CREATE TABLE app_settings (key TEXT PRIMARY KEY, value TEXT);
            INSERT INTO app_settings (key, value) VALUES ('schema_version', '3');
            """);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static string ScalarString(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Assert.IsType<string>(command.ExecuteScalar());
    }
}
