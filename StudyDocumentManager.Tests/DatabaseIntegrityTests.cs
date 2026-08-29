using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public class DatabaseIntegrityTests : DatabaseTestBase
{
    private readonly DocumentRepository _repository;

    public DatabaseIntegrityTests()
    {
        _repository = new DocumentRepository(Db);
    }

    [Fact]
    public void ConnectionString_EnforcesForeignKeysForIndependentConnections()
    {
        using var connection = new SqliteConnection(Db.ConnectionString);
        connection.Open();

        using var foreignKeyCommand = connection.CreateCommand();
        foreignKeyCommand.CommandText = "PRAGMA foreign_keys";
        Assert.Equal(1L, foreignKeyCommand.ExecuteScalar());

        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = "INSERT INTO recent_files (document_id, opened_at) VALUES (99999, datetime('now'))";
        Assert.Throws<SqliteException>(() => insertCommand.ExecuteNonQuery());
    }

    [Fact]
    public void RestoreDocument_ActiveDocument_ReturnsFalseAndPreservesDocument()
    {
        _repository.Add(new StudyDocument { Name = "Active" });
        var document = Assert.Single(_repository.GetAll());

        Assert.False(Db.RestoreDocument(document.Id));
        Assert.Equal("Active", Assert.Single(_repository.GetAll()).Name);
    }

    [Fact]
    public void PermanentDeleteDocument_ActiveDocument_ReturnsFalseAndPreservesDocument()
    {
        _repository.Add(new StudyDocument { Name = "Active" });
        var document = Assert.Single(_repository.GetAll());

        Assert.False(Db.PermanentDeleteDocument(document.Id));
        Assert.Equal("Active", Assert.Single(_repository.GetAll()).Name);
    }

    [Fact]
    public void RestoreDocument_RecreatesMissingTaxonomy()
    {
        _repository.Add(new StudyDocument { Name = "Restore", Subject = "Algorithms", Type = "PDF" });
        var document = Assert.Single(_repository.GetAll());
        _repository.Delete(document.Id);
        Db.DeleteSubject("Algorithms");
        Db.DeleteType("PDF");

        Assert.True(Db.RestoreDocument(document.Id));
        Assert.Contains("Algorithms", Db.GetAllSubjects());
        Assert.Contains("PDF", Db.GetAllTypes());
        Assert.Equal("Restore", Assert.Single(_repository.GetAll()).Name);
    }

    [Fact]
    public void AddRecentFile_MissingOrDeletedDocument_ReturnsFalseWithoutOrphan()
    {
        Assert.False(Db.AddRecentFile(99999));

        _repository.Add(new StudyDocument { Name = "Deleted" });
        var document = Assert.Single(_repository.GetAll());
        _repository.Delete(document.Id);

        Assert.False(Db.AddRecentFile(document.Id));
        Assert.Empty(Db.GetRecentFiles());
    }


    [Fact]
    public void InsertDocumentWithCatalogs_RollsBackDocumentAndCatalogsWhenCatalogWriteFails()
    {
        const string subject = "Transactional Subject";
        const string type = "Transactional Type";

        using (var connection = new SqliteConnection(Db.ConnectionString))
        {
            connection.Open();
            Execute(connection, $"CREATE TRIGGER fail_document_insert AFTER INSERT ON documents WHEN NEW.name = 'Atomic import' BEGIN SELECT RAISE(ABORT, 'document failure'); END");
        }

        try
        {
            var document = new StudyDocument { Name = "Atomic import", Subject = subject, Type = type };

            Assert.Throws<SqliteException>(() => Db.InsertDocumentWithCatalogs(document));
            Assert.Empty(_repository.GetAll());
            using var verificationConnection = new SqliteConnection(Db.ConnectionString);
            verificationConnection.Open();
            Assert.Equal(0, GetNamedCount(verificationConnection, "categories", subject));
            Assert.Equal(0, GetNamedCount(verificationConnection, "document_types", type));
        }
        finally
        {
            using var connection = new SqliteConnection(Db.ConnectionString);
            connection.Open();
            Execute(connection, "DROP TRIGGER fail_document_insert");
        }
    }


    [Fact]
    public void InsertDocumentWithCatalogs_CommitsDocumentAndCatalogsTogether()
    {
        var document = new StudyDocument
        {
            Name = "Atomic import",
            Subject = "Atomic Subject",
            Type = "Atomic Type"
        };

        Assert.True(Db.InsertDocumentWithCatalogs(document));
        Assert.Equal("Atomic import", Assert.Single(_repository.GetAll()).Name);
        Assert.Contains("Atomic Subject", Db.GetAllSubjects());
        Assert.Contains("Atomic Type", Db.GetAllTypes());
    }


    [Fact]
    public void SaveDocument_RollsBackDocumentAndCatalogsWhenCatalogWriteFails()
    {
        const string subject = "Service Transactional Subject";
        const string type = "Service Transactional Type";

        using (var connection = new SqliteConnection(Db.ConnectionString))
        {
            connection.Open();
            Execute(connection, $"CREATE TRIGGER fail_service_document_insert AFTER INSERT ON documents WHEN NEW.name = 'Atomic service import' BEGIN SELECT RAISE(ABORT, 'document failure'); END");
        }

        try
        {
            var service = new DroppedFileImportService(_repository);
            var document = new StudyDocument { Name = "Atomic service import", Subject = subject, Type = type };

            Assert.Throws<SqliteException>(() => service.SaveDocument(document));
            Assert.Empty(_repository.GetAll());
            using var verificationConnection = new SqliteConnection(Db.ConnectionString);
            verificationConnection.Open();
            Assert.Equal(0, GetNamedCount(verificationConnection, "categories", subject));
            Assert.Equal(0, GetNamedCount(verificationConnection, "document_types", type));
        }
        finally
        {
            using var connection = new SqliteConnection(Db.ConnectionString);
            connection.Open();
            Execute(connection, "DROP TRIGGER fail_service_document_insert");
        }
    }

    [Fact]
    public void UpdateDocument_RollsBackDocumentWhenCatalogWriteFails()
    {
        _repository.Add(new StudyDocument { Name = "Original", Subject = "OriginalSubject", Type = "OriginalType" });
        var document = Assert.Single(_repository.GetAll());

        using var connection = new SqliteConnection(Db.ConnectionString);
        connection.Open();
        Execute(connection, "CREATE TRIGGER fail_add_subject AFTER INSERT ON categories BEGIN SELECT RAISE(FAIL, 'category write failed'); END;");

        var updated = new StudyDocument
        {
            Id = document.Id,
            Name = "Updated",
            Subject = "BrokenSubject",
            Type = "OriginalType",
            FilePath = document.FilePath,
            Notes = document.Notes,
            Author = document.Author,
            Tags = document.Tags,
            IsImportant = document.IsImportant,
            Deadline = document.Deadline,
            FileSize = document.FileSize
        };

        Assert.Throws<SqliteException>(() => Db.UpdateDocument(updated));

        var reloaded = Assert.Single(_repository.GetAll());
        Assert.Equal("Original", reloaded.Name);
        Assert.Equal("OriginalSubject", reloaded.Subject);
        Assert.Equal(0L, GetNamedCount(connection, "categories", "BrokenSubject"));
    }

    [Fact]
    public void UpdateDocument_RollsBackCatalogsWhenTargetDocumentDoesNotExist()
    {
        _repository.Add(new StudyDocument { Name = "Original", Subject = "OriginalSubject", Type = "OriginalType" });
        var document = Assert.Single(_repository.GetAll());

        var updated = new StudyDocument
        {
            Id = document.Id + 1000,
            Name = "Missing",
            Subject = "RollbackMissingSubject",
            Type = "RollbackMissingType",
            FilePath = document.FilePath,
            Notes = document.Notes,
            Author = document.Author,
            Tags = document.Tags,
            IsImportant = document.IsImportant,
            Deadline = document.Deadline,
            FileSize = document.FileSize
        };

        Assert.False(Db.UpdateDocument(updated));

        using var connection = new SqliteConnection(Db.ConnectionString);
        connection.Open();

        var reloaded = Assert.Single(_repository.GetAll());
        Assert.Equal("Original", reloaded.Name);
        Assert.Equal("OriginalSubject", reloaded.Subject);
        Assert.Equal("OriginalType", reloaded.Type);
        Assert.Equal(0L, GetNamedCount(connection, "categories", "RollbackMissingSubject"));
        Assert.Equal(0L, GetNamedCount(connection, "document_types", "RollbackMissingType"));
    }

    [Fact]
    public void PermanentDeleteDocument_CascadesDependentRows()
    {
        _repository.Add(new StudyDocument { Name = "Primary" });
        _repository.Add(new StudyDocument { Name = "Related" });
        var documents = _repository.GetAll();
        var primary = documents.Single(document => document.Name == "Primary");
        var related = documents.Single(document => document.Name == "Related");
        var collectionId = Db.CreateCollection("Collection");

        Assert.True(Db.AddDocumentToCollection(collectionId, primary.Id));
        Assert.True(Db.SavePersonalNote(primary.Id, "Note"));
        Assert.True(Db.AddRecentFile(primary.Id));
        Db.AddDocumentRelation(primary.Id, related.Id);
        _repository.Delete(primary.Id);

        Assert.True(Db.PermanentDeleteDocument(primary.Id));

        using var connection = new SqliteConnection(Db.ConnectionString);
        connection.Open();
        Assert.Equal(0L, GetCount(connection, "collection_items"));
        Assert.Equal(0L, GetCount(connection, "personal_notes"));
        Assert.Equal(0L, GetCount(connection, "recent_files"));
        Assert.Equal(0L, GetCount(connection, "document_relations"));
    }

    [Fact]
    public void InitializeDatabase_RebuildsRecognizedLegacyChildTableWithCascade()
    {
        _repository.Add(new StudyDocument { Name = "Legacy" });
        var document = Assert.Single(_repository.GetAll());

        using (var connection = new SqliteConnection(Db.ConnectionString))
        {
            connection.Open();
            Execute(connection, "DROP TABLE recent_files");
            Execute(connection, "CREATE TABLE recent_files (id INTEGER PRIMARY KEY AUTOINCREMENT, document_id INTEGER NOT NULL UNIQUE, opened_at DATETIME DEFAULT (datetime('now','localtime')))" );
            Execute(connection, $"INSERT INTO recent_files (document_id) VALUES ({document.Id})");
        }

        Db.CloseAllConnections();
        Db.InitializeDatabase();

        using var verificationConnection = new SqliteConnection(Db.ConnectionString);
        verificationConnection.Open();
        using var foreignKeyCommand = verificationConnection.CreateCommand();
        foreignKeyCommand.CommandText = "PRAGMA foreign_key_list(recent_files)";
        using var reader = foreignKeyCommand.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("document_id", reader.GetString(3));
        Assert.Equal("CASCADE", reader.GetString(6));
    }

    [Fact]
    public void InitializeDatabase_OrphanedLegacyChildRowPreservesDatabase()
    {
        using (var connection = new SqliteConnection(Db.ConnectionString))
        {
            connection.Open();
            Execute(connection, "DROP TABLE recent_files");
            Execute(connection, "CREATE TABLE recent_files (id INTEGER PRIMARY KEY AUTOINCREMENT, document_id INTEGER NOT NULL UNIQUE, opened_at DATETIME DEFAULT (datetime('now','localtime')))" );
            Execute(connection, "INSERT INTO recent_files (document_id) VALUES (99999)");
        }

        Db.CloseAllConnections();
        var before = File.ReadAllBytes(DbPath);

        Assert.Throws<InvalidOperationException>(() => Db.InitializeDatabase());

        Db.CloseAllConnections();
        Assert.Equal(before, File.ReadAllBytes(DbPath));
    }

    [Fact]
    public void InitializeDatabase_LegacyPersonalNotes_PreservesRowsWithContractDefaults()
    {
        _repository.Add(new StudyDocument { Name = "Legacy note" });
        var document = Assert.Single(_repository.GetAll());

        using (var connection = new SqliteConnection(Db.ConnectionString))
        {
            connection.Open();
            Execute(connection, "DROP TABLE personal_notes");
            Execute(connection, "CREATE TABLE personal_notes (id INTEGER PRIMARY KEY AUTOINCREMENT, document_id INTEGER NOT NULL, content TEXT, created_at DATETIME DEFAULT (datetime('now', 'localtime')), updated_at DATETIME DEFAULT (datetime('now', 'localtime')), FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE)");
            Execute(connection, $"INSERT INTO personal_notes (document_id, content) VALUES ({document.Id}, 'Legacy content')");
        }

        Db.CloseAllConnections();
        Db.InitializeDatabase();

        using var verificationConnection = new SqliteConnection(Db.ConnectionString);
        verificationConnection.Open();
        using var command = verificationConnection.CreateCommand();
        command.CommandText = "SELECT note_type, is_pinned, is_deleted, content FROM personal_notes";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("general", reader.GetString(0));
        Assert.Equal(0L, reader.GetInt64(1));
        Assert.Equal(0L, reader.GetInt64(2));
        Assert.Equal("Legacy content", reader.GetString(3));
    }

    private static long GetNamedCount(SqliteConnection connection, string tableName, string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName} WHERE name = @name";
        command.Parameters.AddWithValue("@name", name);
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static long GetCount(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName}";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
