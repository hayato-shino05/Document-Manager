using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Repositories;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class DuplicateMergeTests : DatabaseTestBase
{
    [Fact]
    public void MergeDocuments_PreservesCollectionsNotesAndRelations_AndSoftDeletesDuplicates()
    {
        var survivor = AddDocument("Same name", "survivor.pdf");
        var duplicate = AddDocument("same NAME", "duplicate.pdf");
        var related = AddDocument("Related", "related.pdf");
        var collectionId = Db.CreateCollection("Reading");
        Assert.True(Db.AddDocumentToCollection(collectionId, survivor.Id));
        Assert.True(Db.AddDocumentToCollection(collectionId, duplicate.Id));
        Assert.True(Db.SavePersonalNote(survivor.Id, "Survivor note"));
        Assert.True(Db.SavePersonalNote(duplicate.Id, "Duplicate note"));
        Db.AddDocumentRelation(duplicate.Id, related.Id, "reference");

        Assert.True(Repo.MergeDocuments(survivor.Id, [duplicate.Id]));

        Assert.Contains(Repo.GetAll(), document => document.Id == survivor.Id);
        Assert.DoesNotContain(Repo.GetAll(), document => document.Id == duplicate.Id);
        Assert.Contains(Repo.GetDeletedDocuments(), document => document.Id == duplicate.Id);
        Assert.Contains(new CollectionRepository(Db).GetDocuments(collectionId), document => document.Id == survivor.Id);
        Assert.DoesNotContain(new CollectionRepository(Db).GetDocuments(collectionId), document => document.Id == duplicate.Id);
        Assert.Equal("Survivor note", Db.GetPersonalNote(survivor.Id));
        Assert.Contains(Db.GetRelatedDocuments(survivor.Id), relation => relation.Doc.Id == related.Id);
    }

    [Fact]
    public void MergeDocuments_PreservesNoteIdentityTypePinAndTags()
    {
        var survivor = AddDocument("same", "survivor.pdf");
        survivor.Tags = "alpha";
        Assert.True(Repo.Update(survivor));
        var duplicate = AddDocument("same", "duplicate.pdf");
        duplicate.Tags = "beta;Alpha";
        Assert.True(Repo.Update(duplicate));
        Assert.True(Db.SavePersonalNote(new PersonalNote(0, duplicate.Id, "action", "Follow up", true)));
        var note = Db.GetPersonalNotes(duplicate.Id).Single();

        Assert.True(Repo.MergeDocuments(survivor.Id, [duplicate.Id]));

        var mergedNote = Db.GetPersonalNotes(survivor.Id).Single(item => item.Id == note.Id);
        Assert.Equal("action", mergedNote.NoteType);
        Assert.True(mergedNote.IsPinned);
        Assert.Equal("Follow up", mergedNote.Content);
        Assert.Equal("alpha;beta", Repo.GetById(survivor.Id)!.Tags);
    }

    [Fact]
    public void MergeDocuments_PreservesExistingSurvivorSelfRelation()
    {
        var survivor = AddDocument("Same name", "survivor.pdf");
        var duplicate = AddDocument("Same name", "duplicate.pdf");
        Db.AddDocumentRelation(survivor.Id, survivor.Id, "self");
        Db.AddDocumentRelation(duplicate.Id, survivor.Id, "generated-self");

        Assert.True(Repo.MergeDocuments(survivor.Id, [duplicate.Id]));

        var relations = Db.GetRelatedDocuments(survivor.Id);
        Assert.Contains(relations, relation => relation.RelationType == "self");
        Assert.DoesNotContain(relations, relation => relation.RelationType == "generated-self");
    }

    [Fact]
    public void MergeDocuments_WhenLaterDuplicateIsMissing_RollsBackEarlierChanges()
    {
        var survivor = AddDocument("Same name", "survivor.pdf");
        var duplicate = AddDocument("Same name", "duplicate.pdf");
        var collectionId = Db.CreateCollection("Reading");
        Assert.True(Db.AddDocumentToCollection(collectionId, duplicate.Id));
        Assert.True(Db.SavePersonalNote(duplicate.Id, "Duplicate note"));

        Assert.Throws<InvalidOperationException>(() => Repo.MergeDocuments(survivor.Id, [duplicate.Id, 999999]));

        Assert.Contains(Repo.GetAll(), document => document.Id == duplicate.Id);
        Assert.Contains(new CollectionRepository(Db).GetDocuments(collectionId), document => document.Id == duplicate.Id);
        Assert.Equal("Duplicate note", Db.GetPersonalNote(duplicate.Id));
    }

    [Fact]
    public void ApplyMergeUndo_RestoresNotesCollectionsRelationsAndDuplicate()
    {
        var survivor = AddDocument("same", "survivor.pdf");
        var duplicate = AddDocument("same", "duplicate.pdf");
        var related = AddDocument("related", "related.pdf");
        var collectionId = Db.CreateCollection("Reading");
        Assert.True(Db.AddDocumentToCollection(collectionId, duplicate.Id));
        Assert.True(Db.SavePersonalNote(new PersonalNote(0, duplicate.Id, "action", "Follow up", true)));
        Db.AddDocumentRelation(duplicate.Id, related.Id, "reference");
        var snapshot = Repo.CaptureMergeUndo(survivor.Id, [duplicate.Id]);

        Assert.True(Repo.MergeDocuments(survivor.Id, [duplicate.Id]));
        Repo.ApplyMergeUndo(snapshot);

        Assert.Contains(Repo.GetAll(), item => item.Id == duplicate.Id);
        Assert.Contains(Db.GetPersonalNotes(duplicate.Id), item => item.NoteType == "action" && item.IsPinned);
        Assert.Contains(new CollectionRepository(Db).GetDocuments(collectionId), item => item.Id == duplicate.Id);
        Assert.Contains(Db.GetRelatedDocuments(duplicate.Id), item => item.Doc.Id == related.Id);
    }

    [Fact]
    public void PermanentlyDeleteDocuments_RemovesNotesAndDoesNotRestore()
    {
        var document = AddDocument("delete", "delete.pdf");
        Assert.True(Db.SavePersonalNote(new PersonalNote(0, document.Id, "general", "content", false)));
        Assert.True(Repo.Delete(document.Id));

        Assert.Equal(1, Repo.PermanentlyDeleteDocuments([document.Id]));
        Assert.Null(Repo.GetById(document.Id));
        Assert.Empty(Db.GetPersonalNotes(document.Id, includeDeleted: true));
        Assert.Equal(0, Repo.RestoreDocuments([document.Id]));
    }

    private StudyDocument AddDocument(string name, string path)
    {
        var document = new StudyDocument { Name = name, FilePath = path };
        Assert.True(Repo.Add(document));
        return Repo.GetAll().Single(item => item.FilePath == path);
    }
}
