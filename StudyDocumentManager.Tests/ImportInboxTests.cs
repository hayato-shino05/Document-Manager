using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Repositories;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class ImportInboxTests : DatabaseTestBase
{
    [Fact]
    public void StatesCanTransitionAndProcessedCanBeFiltered()
    {
        var repository = new ImportInboxRepository(Db);
        var item = new ImportInboxItem { SourcePath = "source.pdf", DisplayName = "source", State = ImportInboxState.Failed };
        repository.Add(item);

        Assert.Equal(ImportInboxState.Failed, repository.GetById(item.Id)!.State);
        Assert.True(repository.UpdateState(item.Id, ImportInboxState.Pending));
        Assert.Equal(ImportInboxState.Pending, repository.GetById(item.Id)!.State);
        Assert.True(repository.UpdateState(item.Id, ImportInboxState.Processed));
        Assert.Empty(repository.GetAll());
        Assert.Single(repository.GetAll(true));
    }

    [Fact]
    public void DuplicateCandidateAndFailureCodePersist()
    {
        var repository = new ImportInboxRepository(Db);
        var item = new ImportInboxItem
        {
            SourcePath = "duplicate.pdf",
            DisplayName = "duplicate",
            DuplicateCandidate = "12",
            FailureCode = "PermissionError",
            State = ImportInboxState.Held
        };
        repository.Add(item);

        var loaded = repository.GetById(item.Id);
        Assert.Equal("12", loaded!.DuplicateCandidate);
        Assert.Equal("PermissionError", loaded.FailureCode);
        Assert.Equal(ImportInboxState.Held, loaded.State);
    }
}
