using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Repositories;
using Xunit;

namespace StudyDocumentManager.Tests;

public class BulkEditDataTests : DatabaseTestBase
{
    [Fact]
    public void BulkEditMetadata_MixedFieldsOnMultipleDocs_AllSucceedAndPersist()
    {
        var docs = new[]
        {
            new StudyDocument { Name = "Doc A" },
            new StudyDocument { Name = "Doc B" },
            new StudyDocument { Name = "Doc C" }
        };
        foreach (var doc in docs)
            Assert.True(Repo.Add(doc));

        var changes = new BulkEditChanges
        {
            Subject = "Physics",
            Type = "Lecture Note",
            Tags = "bulk;edit",
            IsImportant = true,
            Status = DocumentStatus.Completed
        };

        var outcome = Repo.BulkEditMetadata(docs.Select(d => d.Id).ToList(), changes);

        Assert.Equal(3, outcome.Requested);
        Assert.Equal(3, outcome.Succeeded);
        Assert.Empty(outcome.FailedIds);
        foreach (var doc in docs)
        {
            var loaded = Repo.GetById(doc.Id)!;
            Assert.Equal("Physics", loaded.Subject);
            Assert.Equal("Lecture Note", loaded.Type);
            Assert.Equal("bulk;edit", loaded.Tags);
            Assert.True(loaded.IsImportant);
            Assert.Equal(DocumentStatus.Completed, loaded.Status);
        }
    }

    [Fact]
    public void BulkEditMetadata_MissingAndSoftDeletedIds_FailPerItemWhileValidDocsUpdate()
    {
        var activeA = new StudyDocument { Name = "Active A" };
        var activeB = new StudyDocument { Name = "Active B" };
        var softDeleted = new StudyDocument { Name = "Soft deleted" };
        Assert.True(Repo.Add(activeA));
        Assert.True(Repo.Add(activeB));
        Assert.True(Repo.Add(softDeleted));
        Assert.True(Repo.Delete(softDeleted.Id));

        const int missingId = 987654321;
        var outcome = Repo.BulkEditMetadata(
            [missingId, activeA.Id, softDeleted.Id, activeB.Id],
            new BulkEditChanges { Subject = "History", Status = DocumentStatus.Read });

        Assert.Equal(4, outcome.Requested);
        Assert.Equal(2, outcome.Succeeded);
        Assert.Equal([missingId, softDeleted.Id], outcome.FailedIds);
        Assert.False(outcome.Items.Single(r => r.DocumentId == missingId).Success);
        Assert.True(outcome.Items.Single(r => r.DocumentId == activeA.Id).Success);

        Assert.Equal("History", Repo.GetById(activeA.Id)!.Subject);
        Assert.Equal("History", Repo.GetById(activeB.Id)!.Subject);
        var untouched = Repo.GetById(softDeleted.Id)!;
        Assert.Equal(string.Empty, untouched.Subject);
        Assert.Equal(DocumentStatus.Unread, untouched.Status);
    }

    [Fact]
    public void BulkEditMetadata_EmptyIdList_ReturnsZeroOutcomeWithoutWrites()
    {
        var doc = new StudyDocument { Name = "Idle document" };
        Assert.True(Repo.Add(doc));

        var outcome = Repo.BulkEditMetadata([], new BulkEditChanges { Subject = "Ignored" });

        Assert.Equal(0, outcome.Requested);
        Assert.Equal(0, outcome.Succeeded);
        Assert.Empty(outcome.Items);
        Assert.Equal("Idle document", Repo.GetById(doc.Id)!.Name);
    }

    [Fact]
    public void BulkEditMetadata_NoFieldSet_ReturnsRequestedOnlyWithoutException()
    {
        var doc = new StudyDocument { Name = "Untouched", Subject = "Original" };
        Assert.True(Repo.Add(doc));

        var outcome = Repo.BulkEditMetadata([doc.Id], new BulkEditChanges());

        Assert.Equal(1, outcome.Requested);
        Assert.Equal(0, outcome.Succeeded);
        Assert.Empty(outcome.Items);
        var loaded = Repo.GetById(doc.Id)!;
        Assert.Equal("Original", loaded.Subject);
        Assert.Equal(DocumentStatus.Unread, loaded.Status);
    }

    [Fact]
    public void BulkEditMetadata_BogusStatusWithOtherFieldsSet_FailsItemBeforeAnyWrite()
    {
        var doc = new StudyDocument { Name = "Guarded", Subject = "Old subject" };
        Assert.True(Repo.Add(doc));

        var outcome = Repo.BulkEditMetadata(
            [doc.Id],
            new BulkEditChanges { Subject = "New subject", IsImportant = true, Status = "bogus" });

        Assert.Equal(1, outcome.Requested);
        Assert.Equal(0, outcome.Succeeded);
        Assert.Equal([doc.Id], outcome.FailedIds);

        var loaded = Repo.GetById(doc.Id)!;
        Assert.Equal("Old subject", loaded.Subject);
        Assert.False(loaded.IsImportant);
    }

    [Fact]
    public void BulkEditMetadata_DeadlineAndImportant_RoundtripTypesThroughGetById()
    {
        var doc = new StudyDocument { Name = "Dated document", IsImportant = true };
        Assert.True(Repo.Add(doc));
        var deadline = new DateTime(2030, 12, 31, 13, 45, 30);

        var outcome = Repo.BulkEditMetadata(
            [doc.Id],
            new BulkEditChanges { Deadline = deadline, IsImportant = false });

        Assert.Equal(1, outcome.Succeeded);
        var loaded = Repo.GetById(doc.Id)!;
        Assert.NotNull(loaded.Deadline);
        Assert.Equal(deadline, loaded.Deadline.Value);
        Assert.False(loaded.IsImportant);
    }

    [Fact]
    public void BulkEditMetadata_AddToCollection_LinksSucceededIdsOnlyAndIsIdempotent()
    {
        var activeA = new StudyDocument { Name = "Link A" };
        var activeB = new StudyDocument { Name = "Link B" };
        var softDeleted = new StudyDocument { Name = "Link C" };
        Assert.True(Repo.Add(activeA));
        Assert.True(Repo.Add(activeB));
        Assert.True(Repo.Add(softDeleted));
        Assert.True(Repo.Delete(softDeleted.Id));

        var collectionId = Db.CreateCollection("Bulk target");
        var changes = new BulkEditChanges { Status = DocumentStatus.Archived, AddToCollectionId = collectionId };

        var firstRun = Repo.BulkEditMetadata([activeA.Id, softDeleted.Id, activeB.Id], changes);

        Assert.Equal(2, firstRun.Succeeded);
        Assert.Equal(1, CountLinks(collectionId, activeA.Id));
        Assert.Equal(1, CountLinks(collectionId, activeB.Id));
        Assert.Equal(0, CountLinks(collectionId, softDeleted.Id));

        Repo.BulkEditMetadata([activeA.Id, activeB.Id], changes);

        Assert.Equal(1, CountLinks(collectionId, activeA.Id));
        Assert.Equal(1, CountLinks(collectionId, activeB.Id));
    }

    [Fact]
    public void BulkEditMetadata_MembershipOnlyChange_LinksActiveDocsOnly()
    {
        var doc = new StudyDocument { Name = "Solo" };
        var deletedDoc = new StudyDocument { Name = "Gone" };
        Assert.True(Repo.Add(doc));
        Assert.True(Repo.Add(deletedDoc));
        Assert.True(Repo.Delete(deletedDoc.Id));

        var collectionId = Db.CreateCollection("Only link");

        var outcome = Repo.BulkEditMetadata(
            [doc.Id, deletedDoc.Id],
            new BulkEditChanges { AddToCollectionId = collectionId });

        Assert.Equal(2, outcome.Requested);
        Assert.Equal(1, outcome.Succeeded);
        Assert.Equal([deletedDoc.Id], outcome.FailedIds);
        Assert.Equal(1, CountLinks(collectionId, doc.Id));
        Assert.Equal(0, CountLinks(collectionId, deletedDoc.Id));
    }

    [Fact]
    public void BulkEditMetadata_NewSubjectValue_SeedsCategoryCatalog()
    {
        var categoryRepo = new CategoryRepository(Db);
        var doc = new StudyDocument { Name = "Catalog source" };
        Assert.True(Repo.Add(doc));

        Repo.BulkEditMetadata([doc.Id], new BulkEditChanges { Subject = "Brand New Subject" });

        Assert.Contains("Brand New Subject", categoryRepo.GetAllSubjects());
    }

    [Fact]
    public void BulkEditMetadata_PartialFailureLeavesDatabaseConsistent()
    {
        var active = new StudyDocument { Name = "Consistent active", Subject = "Before" };
        var softDeleted = new StudyDocument { Name = "Consistent deleted", Subject = "Before" };
        Assert.True(Repo.Add(active));
        Assert.True(Repo.Add(softDeleted));
        Assert.True(Repo.Delete(softDeleted.Id));

        var outcome = Repo.BulkEditMetadata(
            [999999999, active.Id, softDeleted.Id],
            new BulkEditChanges { Subject = "After", Type = "Essay" });

        Assert.Equal(3, outcome.Requested);
        Assert.Equal(1, outcome.Succeeded);
        Assert.Single(Repo.GetAll());
        Assert.Contains(softDeleted.Id, Repo.GetDeletedDocuments().Select(d => d.Id));
        Assert.Equal("After", Repo.GetById(active.Id)!.Subject);
        Assert.Equal("Before", Repo.GetById(softDeleted.Id)!.Subject);
        using (var connection = new SqliteConnection(Db.ConnectionString))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_key_check";
            Assert.False(cmd.ExecuteReader().Read());
        }
    }

    private int CountLinks(int collectionId, int documentId)
    {
        using var connection = new SqliteConnection(Db.ConnectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM collection_items
            WHERE collection_id = @collectionId AND document_id = @documentId
            """;
        cmd.Parameters.AddWithValue("@collectionId", collectionId);
        cmd.Parameters.AddWithValue("@documentId", documentId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
