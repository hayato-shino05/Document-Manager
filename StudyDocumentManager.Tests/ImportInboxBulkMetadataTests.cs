using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Repositories;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class ImportInboxBulkMetadataTests : DatabaseTestBase
{
    [Fact]
    public void BulkMetadataUpdatesOnlyInboxItemsWithDocumentIds()
    {
        var document = new StudyDocument { Name = "ready", FilePath = "ready.pdf" };
        Assert.True(Repo.AddWithCatalogs(document));
        var inbox = new ImportInboxRepository(Db);
        var withDocument = new ImportInboxItem { DocumentId = document.Id, SourcePath = "ready.pdf", DisplayName = "ready", State = ImportInboxState.Held };
        var withoutDocument = new ImportInboxItem { SourcePath = "failed.pdf", DisplayName = "failed", State = ImportInboxState.Failed };
        inbox.Add(withDocument);
        inbox.Add(withoutDocument);

        var outcome = Repo.BulkEditMetadata([document.Id], new BulkEditChanges { Subject = "Physics" });
        Assert.Equal(1, outcome.Succeeded);
        Assert.Equal("Physics", Repo.GetById(document.Id)!.Subject);
        Assert.True(inbox.UpdateState(withDocument.Id, ImportInboxState.Processed));
        Assert.Equal(ImportInboxState.Processed, inbox.GetById(withDocument.Id)!.State);
        Assert.Equal(ImportInboxState.Failed, inbox.GetById(withoutDocument.Id)!.State);
    }
}
