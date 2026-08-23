using StudyDocumentManager.Core;
using StudyDocumentManager.Core.DTOs;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public class UndoRestoreDocumentsTests : DatabaseTestBase
{
    [Fact]
    public void RestoreDocuments_SoftDeletedDocs_RestoresAllAndReseedsCatalog()
    {
        var physicsA = new StudyDocument { Name = "A", Subject = "Physics", Type = "PDF" };
        var physicsB = new StudyDocument { Name = "B", Subject = "Physics", Type = "PDF" };
        var other = new StudyDocument { Name = "C", Subject = "Other", Type = "DOCX" };
        Assert.True(Repo.Add(physicsA));
        Assert.True(Repo.Add(physicsB));
        Assert.True(Repo.Add(other));

        Assert.True(new CategoryRepository(Db).DeleteDocumentsBySubject("Physics"));
        Assert.Equal(1, Db.BulkSoftDelete([other.Id]));

        Assert.Empty(Repo.GetAll());
        Assert.DoesNotContain("Physics", Db.GetAllSubjects());

        var restored = Repo.RestoreDocuments([physicsA.Id, physicsB.Id, other.Id]);

        Assert.Equal(3, restored);
        Assert.Equal(3, Repo.GetAll().Count);
        Assert.Empty(Repo.GetDeletedDocuments());
        Assert.Contains("Physics", Db.GetAllSubjects());
    }

    [Fact]
    public void RestoreDocuments_ContainsPermanentlyDeletedId_RestoresRecoverableOnesBestEffort()
    {
        var recoverableA = new StudyDocument { Name = "R1", Subject = "Physics", Type = "PDF" };
        var recoverableB = new StudyDocument { Name = "R2", Subject = "Physics", Type = "PDF" };
        var doomed = new StudyDocument { Name = "Gone", Subject = "Other", Type = "DOCX" };
        Assert.True(Repo.Add(recoverableA));
        Assert.True(Repo.Add(recoverableB));
        Assert.True(Repo.Add(doomed));

        Assert.Equal(3, Db.BulkSoftDelete([recoverableA.Id, recoverableB.Id, doomed.Id]));
        Assert.True(Repo.PermanentDeleteDocument(doomed.Id));

        var restored = Repo.RestoreDocuments([recoverableA.Id, doomed.Id, recoverableB.Id]);

        Assert.Equal(2, restored);
        Assert.Equal([recoverableA.Id, recoverableB.Id], Repo.GetAll().Select(d => d.Id).OrderBy(x => x).ToList());
        Assert.Empty(Repo.GetDeletedDocuments());
    }
}

public class UndoApplierRoutingTests
{
    [Fact]
    public void ApplyLast_DeletedIdsEntry_CallsRestoreDocumentsOnceWithExactIds()
    {
        var undo = new UndoService();
        undo.Push(new UndoEntry { DescriptionKey = "UN_DeletedDocuments", DeletedIds = [7, 8, 9] });

        var recycleBin = new RecordingRecycleBin();
        var documents = new RecordingDocuments();
        new UndoApplier(undo, documents, recycleBin, new RecordingCollections()).ApplyLast();

        Assert.Single(recycleBin.RestoreCalls);
        Assert.Equal(new[] { 7, 8, 9 }, recycleBin.RestoreCalls[0]);
        Assert.Empty(documents.Updated);
    }

    [Fact]
    public void ApplyLast_CollectionEntry_CreatesCollectionOnceAndReaddsAllMembers()
    {
        var undo = new UndoService();
        undo.Push(new UndoEntry
        {
            DescriptionKey = "UN_CollectionRestorable",
            Collection = new CollectionSnapshot("Study", "focus", [1, 2])
        });

        var collections = new RecordingCollections { NextId = 42 };
        var recycleBin = new RecordingRecycleBin();
        new UndoApplier(undo, new RecordingDocuments(), recycleBin, collections).ApplyLast();

        Assert.Single(collections.Created);
        Assert.Equal(("Study", "focus"), collections.Created[0]);
        Assert.Equal(new List<(int CollectionId, int DocumentId)> { (42, 1), (42, 2) }, collections.AddedDocs);
        Assert.Empty(recycleBin.RestoreCalls);
    }

    [Fact]
    public void ApplyLast_MetadataEntry_UpdatesOriginals()
    {
        var undo = new UndoService();
        var original = new StudyDocument { Id = 5, Name = "Alpha", Subject = "OldSubject" };
        undo.Push(new UndoEntry
        {
            DescriptionKey = "BE_UndoDescription",
            DescriptionArgs = [1],
            Originals = [original]
        });

        var documents = new RecordingDocuments();
        new UndoApplier(undo, documents, new RecordingRecycleBin(), new RecordingCollections()).ApplyLast();

        Assert.Single(documents.Updated);
        Assert.Same(original, documents.Updated[0]);
    }

    [Fact]
    public void ApplyLast_MetadataEntry_RemovesAddedCollectionMemberships()
    {
        var undo = new UndoService();
        undo.Push(new UndoEntry
        {
            DescriptionKey = "BE_UndoDescription",
            Originals = [new StudyDocument { Id = 5 }],
            AddedCollectionMemberships = [new CollectionMembership(42, 5)]
        });

        var collections = new RecordingCollections();
        new UndoApplier(undo, new RecordingDocuments(), new RecordingRecycleBin(), collections).ApplyLast();

        Assert.Equal([(42, 5)], collections.RemovedDocs);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void ApplyLast_WhenDocumentRestoreFails_KeepsUndoEntry()
    {
        var undo = new UndoService();
        undo.Push(new UndoEntry
        {
            DescriptionKey = "BE_UndoDescription",
            Originals = [new StudyDocument { Id = 5 }]
        });

        var documents = new RecordingDocuments { UpdateResult = false };
        var applier = new UndoApplier(undo, documents, new RecordingRecycleBin(), new RecordingCollections());

        Assert.Throws<InvalidOperationException>(applier.ApplyLast);
        Assert.True(undo.CanUndo);
    }

    [Fact]
    public void ApplyLast_WhenRestoreCountIsPartial_ConsumesEntryAndThrowsPartial()
    {
        var undo = new UndoService();
        undo.Push(new UndoEntry { DescriptionKey = "UN_DeletedDocuments", DeletedIds = [7, 8] });

        var recycleBin = new RecordingRecycleBin { RestoreCount = 1 };
        var applier = new UndoApplier(undo, new RecordingDocuments(), recycleBin, new RecordingCollections());

        var exception = Assert.Throws<UndoPartialRestoreException>(applier.ApplyLast);
        Assert.Equal(1, exception.RestoredCount);
        Assert.Equal(2, exception.RequestedCount);
        Assert.Single(recycleBin.RestoreCalls);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void ApplyLast_WhenMembershipAlreadyGone_SkipsMembershipAndKeepsDocumentRestore()
    {
        var undo = new UndoService();
        var original = new StudyDocument { Id = 5, Name = "Before" };
        undo.Push(new UndoEntry
        {
            DescriptionKey = "BE_UndoDescription",
            Originals = [original],
            AddedCollectionMemberships = [new CollectionMembership(42, 5)]
        });

        var documents = new RecordingDocuments();
        var collections = new RecordingCollections { RemoveResult = false };
        new UndoApplier(undo, documents, new RecordingRecycleBin(), collections).ApplyLast();

        Assert.Single(documents.Updated);
        Assert.Same(original, documents.Updated[0]);
        Assert.Equal([(42, 5)], collections.RemovedDocs);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void ApplyLast_EmptyStack_Throws()
    {
        var applier = new UndoApplier(new UndoService(), new RecordingDocuments(), new RecordingRecycleBin(), new RecordingCollections());

        Assert.Throws<InvalidOperationException>(applier.ApplyLast);
    }

    private sealed class RecordingRecycleBin : IRecycleBinRepository
    {
        public List<IReadOnlyList<int>> RestoreCalls { get; } = [];
        public int? RestoreCount { get; init; }

        public int RestoreDocuments(IReadOnlyList<int> ids)
        {
            RestoreCalls.Add(ids);
            return RestoreCount ?? ids.Count;
        }

        public List<StudyDocument> GetDeletedDocuments() => [];
        public bool RestoreDocument(int id) => true;
        public bool PermanentDeleteDocument(int id) => true;
        public int EmptyRecycleBin() => 0;
        public int GetDeletedDocumentCount() => 0;
    }

    private sealed class RecordingCollections : ICollectionRepository
    {
        public int NextId { get; set; } = 42;
        public List<(string Name, string? Description)> Created { get; } = [];
        public List<(int CollectionId, int DocumentId)> AddedDocs { get; } = [];
        public List<(int CollectionId, int DocumentId)> RemovedDocs { get; } = [];
        public bool RemoveResult { get; init; } = true;

        public List<(int Id, string Name, string? Description, DateTime CreatedAt, int ItemCount)> GetAll() => [];

        public int Create(string name, string? description = null)
        {
            Created.Add((name, description));
            return NextId;
        }

        public bool Update(int id, string name, string? description = null) => true;
        public bool Delete(int id) => true;
        public List<StudyDocument> GetDocuments(int collectionId) => [];

        public bool AddDocument(int collectionId, int documentId)
        {
            AddedDocs.Add((collectionId, documentId));
            return true;
        }

        public bool RemoveDocument(int collectionId, int documentId)
        {
            RemovedDocs.Add((collectionId, documentId));
            return RemoveResult;
        }
    }

    private sealed class RecordingDocuments : IDocumentRepository
    {
        public List<StudyDocument> Updated { get; } = [];
        public bool UpdateResult { get; init; } = true;

        public List<StudyDocument> GetAll() => [];
        public StudyDocument? GetById(int id) => new() { Id = id, Name = "After" };
        public List<StudyDocument> Search(string keyword) => [];
        public List<StudyDocument> Filter(string subject, string type) => [];
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => [];
        public bool Add(StudyDocument document) => true;
        public bool AddWithCatalogs(StudyDocument document) => true;

        public bool Update(StudyDocument document)
        {
            Updated.Add(document);
            return UpdateResult;
        }

        public bool Delete(int id) => true;
        public List<string> GetDistinctSubjects() => [];
        public List<string> GetDistinctTypes() => [];
        public List<string> GetDistinctTags() => [];
        public List<StudyDocument> GetUpcomingDeadlines(int days) => [];
        public List<StudyDocument> GetOverdueDocuments() => [];
        public void EnsureSubjectExists(string subject) { }
        public void EnsureTypeExists(string type) { }
    }
}

public class UndoApplierResilienceTests : DatabaseTestBase
{
    [Fact]
    public void ApplyLast_MetadataEntryWithPermanentlyDeletedOriginal_RestoresSurvivorsAndSkipsStaleMembership()
    {
        var collections = new CollectionRepository(Db);
        var collectionId = collections.Create("Study", "focus");
        var docA = new StudyDocument { Name = "A", Subject = "NewSubject", Type = "PDF" };
        var docB = new StudyDocument { Name = "B", Subject = "NewSubject", Type = "PDF" };
        var docC = new StudyDocument { Name = "C", Subject = "NewSubject", Type = "PDF" };
        Assert.True(Repo.Add(docA));
        Assert.True(Repo.Add(docB));
        Assert.True(Repo.Add(docC));

        foreach (var doc in new[] { docA, docB, docC })
        {
            Assert.True(collections.AddDocument(collectionId, doc.Id));
            Assert.Equal(1, Repo.BulkUpdateSubject([doc.Id], "OldSubject"));
        }

        Assert.True(Repo.Delete(docC.Id));
        Assert.True(Repo.PermanentDeleteDocument(docC.Id));
        _ = collections.RemoveDocument(collectionId, docC.Id);
        Assert.True(collections.RemoveDocument(collectionId, docA.Id));
        Assert.Equal([docB.Id], collections.GetDocuments(collectionId).Select(d => d.Id).ToList());

        var undo = new UndoService();
        undo.Push(new UndoEntry
        {
            DescriptionKey = "BE_UndoDescription",
            DescriptionArgs = [3],
            Originals =
            [
                new StudyDocument { Id = docA.Id, Name = "A", Subject = "NewSubject", Type = "PDF" },
                new StudyDocument { Id = docB.Id, Name = "B", Subject = "NewSubject", Type = "PDF" }
            ],
            AddedCollectionMemberships =
            [
                new CollectionMembership(collectionId, docA.Id),
                new CollectionMembership(collectionId, docB.Id),
                new CollectionMembership(collectionId, docC.Id)
            ],
            CreatedAt = DateTime.Now
        });

        new UndoApplier(undo, Repo, Repo, collections).ApplyLast();

        Assert.Equal("NewSubject", Repo.GetById(docA.Id)!.Subject);
        Assert.Equal("NewSubject", Repo.GetById(docB.Id)!.Subject);
        Assert.Null(Repo.GetById(docC.Id));
        Assert.Empty(collections.GetDocuments(collectionId));
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void ApplyLast_PartialDeleteRestore_ConsumesNewestEntryAndUnclogsStack()
    {
        var docA = new StudyDocument { Name = "A", Subject = "OldSubject", Type = "PDF" };
        var doomed = new StudyDocument { Name = "C", Subject = "S3", Type = "PDF" };
        Assert.True(Repo.Add(docA));
        Assert.True(Repo.Add(doomed));

        var olderMetadata = new UndoEntry
        {
            DescriptionKey = "BE_UndoDescription",
            DescriptionArgs = [1],
            Originals =
            [
                new StudyDocument { Id = docA.Id, Name = "A", Subject = "OldSubject", Type = "PDF" }
            ],
            CreatedAt = DateTime.Now
        };
        var newerDelete = new UndoEntry
        {
            DescriptionKey = "UN_DeletedDocuments",
            DescriptionArgs = [2],
            DeletedIds = [docA.Id, doomed.Id],
            CreatedAt = DateTime.Now
        };

        var undo = new UndoService();
        undo.Push(olderMetadata);
        undo.Push(newerDelete);
        Assert.Equal(1, Repo.BulkUpdateSubject([docA.Id], "ChangedSubject"));
        Assert.Equal(2, Db.BulkSoftDelete([docA.Id, doomed.Id]));
        Assert.True(Repo.PermanentDeleteDocument(doomed.Id));

        var applier = new UndoApplier(undo, Repo, Repo, new CollectionRepository(Db));

        var partial = Assert.Throws<UndoPartialRestoreException>(applier.ApplyLast);

        Assert.Equal(1, partial.RestoredCount);
        Assert.Equal(2, partial.RequestedCount);
        Assert.NotNull(Repo.GetById(docA.Id));
        Assert.Null(Repo.GetById(doomed.Id));
        Assert.Same(olderMetadata, undo.Peek());
        Assert.True(applier.CanUndo);

        applier.ApplyLast();

        Assert.Equal("OldSubject", Repo.GetById(docA.Id)!.Subject);
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void ApplyLast_AllDeletedIdsPermanentlyGone_KeepsEntryForRetry()
    {
        var doomed = new StudyDocument { Name = "Gone", Subject = "S3", Type = "PDF" };
        Assert.True(Repo.Add(doomed));
        Assert.Equal(1, Db.BulkSoftDelete([doomed.Id]));
        Assert.True(Repo.PermanentDeleteDocument(doomed.Id));

        var undo = new UndoService();
        var entry = new UndoEntry
        {
            DescriptionKey = "UN_DeletedDocuments",
            DescriptionArgs = [1],
            DeletedIds = [doomed.Id],
            CreatedAt = DateTime.Now
        };
        undo.Push(entry);
        var applier = new UndoApplier(undo, Repo, Repo, new CollectionRepository(Db));

        var failure = Record.Exception(applier.ApplyLast);

        Assert.IsType<InvalidOperationException>(failure);
        Assert.Same(entry, undo.Peek());
        Assert.True(applier.CanUndo);

        Assert.Throws<InvalidOperationException>(applier.ApplyLast);
        Assert.Same(entry, undo.Peek());
    }

    [Fact]
    public void ApplyLast_FastPathMetadataUndo_PermanentlyDeletedOriginal_SkipsItAndRestoresOthers()
    {
        var docA = new StudyDocument { Name = "A", Subject = "NewSubject", Type = "PDF" };
        var docB = new StudyDocument { Name = "B", Subject = "NewSubject", Type = "PDF" };
        var docC = new StudyDocument { Name = "C", Subject = "NewSubject", Type = "PDF" };
        Assert.True(Repo.Add(docA));
        Assert.True(Repo.Add(docB));
        Assert.True(Repo.Add(docC));

        foreach (var doc in new[] { docA, docB, docC })
            Assert.Equal(1, Repo.BulkUpdateSubject([doc.Id], "OldSubject"));

        Assert.True(Repo.Delete(docC.Id));
        Assert.True(Repo.PermanentDeleteDocument(docC.Id));

        var undo = new UndoService();
        undo.Push(new UndoEntry
        {
            DescriptionKey = "BE_UndoDescription",
            DescriptionArgs = [3],
            Originals =
            [
                new StudyDocument { Id = docA.Id, Name = "A", Subject = "NewSubject", Type = "PDF" },
                new StudyDocument { Id = docB.Id, Name = "B", Subject = "NewSubject", Type = "PDF" },
                new StudyDocument { Id = docC.Id, Name = "C", Subject = "NewSubject", Type = "PDF" }
            ],
            CreatedAt = DateTime.Now
        });

        new UndoApplier(undo, Repo, Repo, new CollectionRepository(Db), Repo).ApplyLast();

        Assert.Equal("NewSubject", Repo.GetById(docA.Id)!.Subject);
        Assert.Equal("NewSubject", Repo.GetById(docB.Id)!.Subject);
        Assert.Null(Repo.GetById(docC.Id));
        Assert.False(undo.CanUndo);
    }
}

public class UndoCollectionIntegrationTests : DatabaseTestBase
{
    [Fact]
    public void ApplyLast_CollectionDelete_RecreatesCollectionWithNewIdAndMembers()
    {
        var collections = new CollectionRepository(Db);
        int originalId = collections.Create("Study", "focus");
        var docA = new StudyDocument { Name = "A", Subject = "S", Type = "T" };
        var docB = new StudyDocument { Name = "B", Subject = "S", Type = "T" };
        Assert.True(Repo.Add(docA));
        Assert.True(Repo.Add(docB));
        Assert.True(collections.AddDocument(originalId, docA.Id));
        Assert.True(collections.AddDocument(originalId, docB.Id));

        var memberIds = new List<int> { docA.Id, docB.Id };
        var undo = new UndoService();
        undo.Push(new UndoEntry
        {
            DescriptionKey = "UN_CollectionRestorable",
            DescriptionArgs = ["Study"],
            Collection = new CollectionSnapshot("Study", "focus", memberIds),
            CreatedAt = DateTime.Now
        });
        Assert.True(collections.Delete(originalId));
        Assert.Empty(collections.GetAll());

        new UndoApplier(undo, Repo, Repo, collections).ApplyLast();

        var all = collections.GetAll();
        Assert.Single(all);
        Assert.NotEqual(originalId, all[0].Id);
        Assert.Equal("Study", all[0].Name);
        Assert.Equal("focus", all[0].Description);

        var restoredMemberIds = collections.GetDocuments(all[0].Id).Select(d => d.Id).OrderBy(x => x).ToList();
        Assert.Equal(memberIds.OrderBy(x => x), restoredMemberIds);
    }
}

public class UndoCategoryCascadeTests : DatabaseTestBase
{
    [Fact]
    public async Task DeleteSubject_WithPreviewConfirm_PushesUndoThatRestoresDocsAndCatalog()
    {
        var docA = new StudyDocument { Name = "Alpha", Subject = "Math101", Type = "PDF" };
        var docB = new StudyDocument { Name = "Beta", Subject = "Math101", Type = "PDF" };
        Assert.True(Repo.AddWithCatalogs(docA));
        Assert.True(Repo.AddWithCatalogs(docB));

        // AddWithCatalogs does not write back generated ids; read them from storage.
        var seededIds = Repo.GetAll().Select(d => d.Id).OrderBy(x => x).ToList();
        Assert.Equal(2, seededIds.Count);

        var undo = new UndoService();
        var applier = new UndoApplier(undo, Repo, Repo, new CollectionRepository(Db));
        var dialogs = new PreviewConfirmTrueDialogs();
        var model = new CategoryManagementModel(Repo, new CategoryRepository(Db), dialogs, new KeyLocalization(), dialogs, undo);

        var target = model.Subjects.First(s => s.Name == "Math101");
        model.SelectedSubjects = new List<CategoryItem> { target };

        await model.DeleteSubjectCommand.ExecuteAsync(null);

        Assert.Empty(Repo.GetAll());
        Assert.DoesNotContain("Math101", Db.GetAllSubjects());
        Assert.True(applier.CanUndo);

        applier.ApplyLast();

        Assert.Equal(
            seededIds,
            Repo.GetAll().Select(d => d.Id).OrderBy(x => x).ToList());
        Assert.Contains("Math101", Db.GetAllSubjects());
        Assert.False(applier.CanUndo);
    }

    private sealed class PreviewConfirmTrueDialogs : IDialogService, ICustomDialogService
    {
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(true);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);

        public Task<bool> ShowAffectedItemsPreviewAsync(string title, int totalCount, IReadOnlyList<string> itemNames, string reversibilityNote)
            => Task.FromResult(true);

        public Task<string?> ShowChangeCategoryAsync(string documentName, IList<string> existingCategories, string currentCategory)
            => Task.FromResult<string?>(null);

        public Task<int> ShowSelectCollectionAsync(string documentName, IList<(int Id, string Name, int DocCount)> collections)
            => Task.FromResult(-1);

        public Task<List<StudyDocument>?> ShowDocumentPickerAsync(string collectionName, IEnumerable<StudyDocument> allDocuments, IEnumerable<int> alreadyInCollection)
            => Task.FromResult<List<StudyDocument>?>(null);

        public Task<AddDocumentDraft?> ShowAddDocumentAsync(string filePath, IList<string> subjects, IList<string> types)
            => Task.FromResult<AddDocumentDraft?>(null);
    }
}

public class MainWindowModelUndoStateTests
{
    [Fact]
    public void CanUndo_TransitionsOnPushAndApply()
    {
        var undo = new UndoService();
        var applier = new UndoApplier(undo, new RoutingFakes.RecordingDocuments(), new RoutingFakes.RecordingRecycleBin(), new RoutingFakes.RecordingCollections());
        var model = CreateModel(applier, undo);

        Assert.False(model.CanUndo);

        undo.Push(new UndoEntry { DescriptionKey = "UN_DeletedDocuments", DeletedIds = [1] });
        Assert.True(model.CanUndo);

        applier.ApplyLast();
        Assert.False(model.CanUndo);
    }

    private static MainWindowModel CreateModel(IUndoApplier applier, IUndoService undo)
    {
        var loc = new KeyLocalization();
        var dashboard = new DashboardModel(null!, null!, null!, null!, null!, new StubDialogService(), null!, null!, null!, null!, null!, null!, null!, loc);
        return new MainWindowModel(dashboard, new StubNavigation(), new StubDialogService(), null!, null!,
            new StubLifecycle(), loc, new StubSettings(), new StubUpdate(), applier, undo);
    }
}

public class BulkDeleteLegacyUndoRegressionTests : DatabaseTestBase
{
    [Fact]
    public async Task MetadataBulkEdit_UndoWithoutApplier_StillRestoresOriginals()
    {
        var doc = new StudyDocument { Name = "Alpha", Subject = "Math", Type = "PDF" };
        Assert.True(Repo.Add(doc));

        var undo = new UndoService();
        var dialog = new StubDialogService { ConfirmResult = true };
        var model = new BulkDeleteModel(Repo, Repo, new CategoryRepository(Db), dialog, new StubNavigation(), new KeyLocalization(),
            null, null, undo);
        model.Initialize();
        foreach (var row in model.Documents)
            row.IsSelected = true;
        model.EnableSubject = true;
        model.NewSubject = "Physics";

        await model.ApplyBulkEditCommand.ExecuteAsync(null);
        Assert.Equal("Physics", Repo.GetById(doc.Id)!.Subject);
        Assert.True(model.UndoLastCommand.CanExecute(null));

        await model.UndoLastCommand.ExecuteAsync(null);

        Assert.Equal("Math", Repo.GetById(doc.Id)!.Subject);
        Assert.False(model.UndoLastCommand.CanExecute(null));
    }

    [Fact]
    public async Task DeleteBulkEdit_UndoWithoutApplier_RestoresSoftDeletedDocs()
    {
        var docA = new StudyDocument { Name = "Alpha", Subject = "Math", Type = "PDF" };
        var docB = new StudyDocument { Name = "Beta", Subject = "Physics", Type = "DOCX" };
        Assert.True(Repo.Add(docA));
        Assert.True(Repo.Add(docB));

        var undo = new UndoService();
        var dialog = new StubDialogService { ConfirmResult = true };
        var model = new BulkDeleteModel(Repo, Repo, new CategoryRepository(Db), dialog, new StubNavigation(), new KeyLocalization(),
            null, null, undo, null, Repo);
        model.Initialize();
        foreach (var row in model.Documents)
            row.IsSelected = true;

        await model.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Empty(Repo.GetAll());
        Assert.Equal(2, Repo.GetDeletedDocuments().Count);
        Assert.True(model.UndoLastCommand.CanExecute(null));

        await model.UndoLastCommand.ExecuteAsync(null);

        Assert.Equal([docA.Id, docB.Id], Repo.GetAll().Select(d => d.Id).OrderBy(x => x).ToList());
        Assert.Empty(Repo.GetDeletedDocuments());
        Assert.False(model.UndoLastCommand.CanExecute(null));
    }
}

public class BulkDeleteFallbackUndoTests : DatabaseTestBase
{
    private static StudyDocument Clone(StudyDocument d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Subject = d.Subject,
        Type = d.Type
    };

    private BulkDeleteModel CreateModel(UndoService undo, IDialogService dialogs, IRecycleBinRepository? recycleBin)
        => new(Repo, Repo, new CategoryRepository(Db), dialogs, new StubNavigation(), new FormatKeyLocalization(),
            null, null, undo, null, recycleBin);

    [Fact]
    public async Task UndoLast_DeletedIdsFallback_RestoresAllPopsAndReportsApplied()
    {
        var docA = new StudyDocument { Name = "Alpha", Subject = "Math", Type = "PDF" };
        var docB = new StudyDocument { Name = "Beta", Subject = "Physics", Type = "DOCX" };
        Assert.True(Repo.Add(docA));
        Assert.True(Repo.Add(docB));

        var originals = Repo.GetAll().Select(Clone).ToList();
        Assert.Equal(2, Db.BulkSoftDelete([docA.Id, docB.Id]));

        var undo = new UndoService();
        undo.Push(new UndoEntry
        {
            DescriptionKey = "UN_DeletedDocuments",
            DescriptionArgs = [2],
            Originals = originals,
            DeletedIds = [docA.Id, docB.Id],
            CreatedAt = DateTime.Now
        });

        var dialogs = new RecordingErrorDialogs();
        var model = CreateModel(undo, dialogs, Repo);

        await model.UndoLastCommand.ExecuteAsync(null);

        Assert.Equal([docA.Id, docB.Id], Repo.GetAll().Select(d => d.Id).OrderBy(x => x).ToList());
        Assert.Empty(Repo.GetDeletedDocuments());
        Assert.False(model.UndoLastCommand.CanExecute(null));
        Assert.Contains("UN_DeletedDocuments", model.StatusText);
        Assert.DoesNotContain("Msg_Error", model.StatusText);
        Assert.Empty(dialogs.Errors);
    }

    [Fact]
    public async Task UndoLast_DeletedIdsPartialRestore_ReportsPartialNotFullSuccess()
    {
        var survivor = new StudyDocument { Name = "Keep", Subject = "Math", Type = "PDF" };
        var doomed = new StudyDocument { Name = "Gone", Subject = "Physics", Type = "PDF" };
        Assert.True(Repo.Add(survivor));
        Assert.True(Repo.Add(doomed));

        var originals = Repo.GetAll().Select(Clone).ToList();
        Assert.Equal(2, Db.BulkSoftDelete([survivor.Id, doomed.Id]));
        Assert.True(Repo.PermanentDeleteDocument(doomed.Id));

        var undo = new UndoService();
        undo.Push(new UndoEntry
        {
            DescriptionKey = "UN_DeletedDocuments",
            DescriptionArgs = [2],
            Originals = originals,
            DeletedIds = [survivor.Id, doomed.Id],
            CreatedAt = DateTime.Now
        });

        var dialogs = new RecordingErrorDialogs();
        var model = CreateModel(undo, dialogs, Repo);

        await model.UndoLastCommand.ExecuteAsync(null);

        Assert.NotNull(Repo.GetById(survivor.Id));
        Assert.Null(Repo.GetById(doomed.Id));
        Assert.False(model.UndoLastCommand.CanExecute(null));
        Assert.Empty(dialogs.Errors);
        Assert.Contains("BE_Result_Partial", model.StatusText);
        Assert.DoesNotContain("UN_DeletedDocuments", model.StatusText);
    }

    [Fact]
    public async Task UndoLast_DeletedIdsWithoutRecycleBin_ShowsErrorKeepsEntryMutatesNothing()
    {
        var doc = new StudyDocument { Name = "Alpha", Subject = "Math", Type = "PDF" };
        Assert.True(Repo.Add(doc));

        var originals = Repo.GetAll().Select(Clone).ToList();
        Assert.Equal(1, Db.BulkSoftDelete([doc.Id]));

        var undo = new UndoService();
        undo.Push(new UndoEntry
        {
            DescriptionKey = "UN_DeletedDocuments",
            Originals = originals,
            DeletedIds = [doc.Id],
            CreatedAt = DateTime.Now
        });

        var dialogs = new RecordingErrorDialogs();
        var model = CreateModel(undo, dialogs, null);

        await model.UndoLastCommand.ExecuteAsync(null);

        Assert.Single(dialogs.Errors);
        Assert.Empty(Repo.GetAll());
        Assert.Single(Repo.GetDeletedDocuments());
        Assert.True(model.UndoLastCommand.CanExecute(null));

        await model.UndoLastCommand.ExecuteAsync(null);
        Assert.Equal(2, dialogs.Errors.Count);
    }

    [Fact]
    public async Task UndoLast_MetadataFallbackWithMemberships_RemovesMembershipsRestoresOriginalsAndPops()
    {
        var collections = new CollectionRepository(Db);
        var collectionId = collections.Create("Study", "focus");
        var doc = new StudyDocument { Name = "Alpha", Subject = "Math", Type = "PDF" };
        Assert.True(Repo.Add(doc));
        Assert.True(collections.AddDocument(collectionId, doc.Id));
        Assert.Single(collections.GetDocuments(collectionId));

        var preEdit = Clone(Repo.GetById(doc.Id)!);
        Assert.True(Repo.Update(new StudyDocument { Id = doc.Id, Name = "Alpha", Subject = "Physics", Type = "PDF" }));

        var undo = new UndoService();
        undo.Push(new UndoEntry
        {
            DescriptionKey = "BE_UndoDescription",
            DescriptionArgs = [1],
            Originals = [preEdit],
            AddedCollectionMemberships = [new CollectionMembership(collectionId, doc.Id)],
            CreatedAt = DateTime.Now
        });

        var dialogs = new RecordingErrorDialogs();
        var model = new BulkDeleteModel(Repo, Repo, new CategoryRepository(Db), dialogs, new StubNavigation(), new FormatKeyLocalization(),
            null, collections, undo);

        await model.UndoLastCommand.ExecuteAsync(null);

        Assert.Equal("Math", Repo.GetById(doc.Id)!.Subject);
        Assert.Empty(collections.GetDocuments(collectionId));
        Assert.False(model.UndoLastCommand.CanExecute(null));
        Assert.Empty(dialogs.Errors);
    }

    [Fact]
    public async Task UndoLast_MetadataFallbackWithMembershipsAndNullCollectionRepo_SkipsMembershipRemovalSilently()
    {
        var doc = new StudyDocument { Name = "Beta", Subject = "Math", Type = "DOCX" };
        Assert.True(Repo.Add(doc));

        var preEdit = Clone(Repo.GetById(doc.Id)!);
        Assert.True(Repo.Update(new StudyDocument { Id = doc.Id, Name = "Beta", Subject = "Physics", Type = "DOCX" }));

        var undo = new UndoService();
        undo.Push(new UndoEntry
        {
            DescriptionKey = "BE_UndoDescription",
            DescriptionArgs = [1],
            Originals = [preEdit],
            AddedCollectionMemberships = [new CollectionMembership(999, doc.Id)],
            CreatedAt = DateTime.Now
        });

        var dialogs = new RecordingErrorDialogs();
        var model = CreateModel(undo, dialogs, null);

        await model.UndoLastCommand.ExecuteAsync(null);

        Assert.Equal("Math", Repo.GetById(doc.Id)!.Subject);
        Assert.False(model.UndoLastCommand.CanExecute(null));
        Assert.Empty(dialogs.Errors);
    }

    private sealed class FormatKeyLocalization : ILocalizationService
    {
        public string this[string key] => $"{key}:{{0}}";
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public void SetLanguage(SupportedLanguage language) { }
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = Enum.GetValues<SupportedLanguage>();
        public event EventHandler? LanguageChanged { add { } remove { } }
    }

    private sealed class RecordingErrorDialogs : IDialogService
    {
        public List<string> Errors { get; } = [];

        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(true);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
    }
}

public class BulkDeleteModelPartialUndoTests : DatabaseTestBase
{
    [Fact]
    public async Task UndoLast_ApplierPartialRestore_ReportsPartialWithoutErrorDialog()
    {
        var survivor = new StudyDocument { Name = "Keep", Subject = "Math", Type = "PDF" };
        Assert.True(Repo.Add(survivor));
        Assert.Equal(1, Db.BulkSoftDelete([survivor.Id]));

        var undo = new UndoService();
        undo.Push(new UndoEntry
        {
            DescriptionKey = "UN_DeletedDocuments",
            DescriptionArgs = [2],
            DeletedIds = [survivor.Id],
            CreatedAt = DateTime.Now
        });

        var dialogs = new RecordingDialogs();
        var applier = new PartialThrowingApplier(Repo, undo, [survivor.Id]);
        var model = new BulkDeleteModel(Repo, Repo, new CategoryRepository(Db), dialogs, new StubNavigation(), new FormatLocalization(),
            null, null, undo, applier, Repo);
        model.Initialize();

        await model.UndoLastCommand.ExecuteAsync(null);

        Assert.Empty(dialogs.Errors);
        Assert.Contains("BE_Result_Partial", model.StatusText);
        Assert.DoesNotContain("UN_DeletedDocuments", model.StatusText);
        Assert.Equal([survivor.Id], model.Documents.Select(d => d.Document.Id).ToList());
        Assert.False(model.UndoLastCommand.CanExecute(null));
    }

    private sealed class PartialThrowingApplier(IRecycleBinRepository recycleBin, IUndoService undo, IReadOnlyList<int> restoreIds) : IUndoApplier
    {
        public bool CanUndo => undo.CanUndo;

        public void ApplyLast()
        {
            recycleBin.RestoreDocuments(restoreIds);
            undo.Pop();
            throw new UndoPartialRestoreException(1, 2);
        }
    }

    private sealed class RecordingDialogs : IDialogService
    {
        public List<string> Errors { get; } = [];

        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(true);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
    }

    private sealed class FormatLocalization : ILocalizationService
    {
        public string this[string key] => $"{key}:{{0}}";
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public void SetLanguage(SupportedLanguage language) { }
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = Enum.GetValues<SupportedLanguage>();
        public event EventHandler? LanguageChanged { add { } remove { } }
    }
}

internal static class RoutingFakes
{
    public sealed class RecordingRecycleBin : IRecycleBinRepository
    {
        public List<IReadOnlyList<int>> RestoreCalls { get; } = [];

        public int RestoreDocuments(IReadOnlyList<int> ids)
        {
            RestoreCalls.Add(ids);
            return ids.Count;
        }

        public List<StudyDocument> GetDeletedDocuments() => [];
        public bool RestoreDocument(int id) => true;
        public bool PermanentDeleteDocument(int id) => true;
        public int EmptyRecycleBin() => 0;
        public int GetDeletedDocumentCount() => 0;
    }

    public sealed class RecordingCollections : ICollectionRepository
    {
        public int NextId { get; set; } = 42;

        public List<(int Id, string Name, string? Description, DateTime CreatedAt, int ItemCount)> GetAll() => [];
        public int Create(string name, string? description = null) => NextId;
        public bool Update(int id, string name, string? description = null) => true;
        public bool Delete(int id) => true;
        public List<StudyDocument> GetDocuments(int collectionId) => [];
        public bool AddDocument(int collectionId, int documentId) => true;
        public bool RemoveDocument(int collectionId, int documentId) => true;
    }

    public sealed class RecordingDocuments : IDocumentRepository
    {
        public List<StudyDocument> GetAll() => [];
        public StudyDocument? GetById(int id) => null;
        public List<StudyDocument> Search(string keyword) => [];
        public List<StudyDocument> Filter(string subject, string type) => [];
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => [];
        public bool Add(StudyDocument document) => true;
        public bool AddWithCatalogs(StudyDocument document) => true;
        public bool Update(StudyDocument document) => true;
        public bool Delete(int id) => true;
        public List<string> GetDistinctSubjects() => [];
        public List<string> GetDistinctTypes() => [];
        public List<string> GetDistinctTags() => [];
        public List<StudyDocument> GetUpcomingDeadlines(int days) => [];
        public List<StudyDocument> GetOverdueDocuments() => [];
        public void EnsureSubjectExists(string subject) { }
        public void EnsureTypeExists(string type) { }
    }
}

file sealed class StubDialogService : IDialogService
{
    public bool ConfirmResult { get; init; }

    public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
    public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
    public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(ConfirmResult);
    public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(ConfirmResult);
    public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
}

file sealed class StubNavigation : INavigationService
{
    public bool CanGoBack => false;
    public void NavigateTo(string viewKey) { }
    public void NavigateTo(string viewKey, object? parameter) { }
    public void GoBack() { }
}

file sealed class StubLifecycle : IApplicationLifecycleService
{
    public void Shutdown() { }
}

file sealed class StubSettings : ISettingsService
{
    public string? GetSetting(string key) => null;
    public void SetSetting(string key, string value) { }
}

file sealed class StubUpdate : IUpdateService
{
    public Task<UpdateInfo?> CheckForUpdateAsync() => Task.FromResult<UpdateInfo?>(null);
    public Task CheckSilentlyAsync() => Task.CompletedTask;
    public Task HandleUpdateAsync(UpdateInfo update) => Task.CompletedTask;
}

file sealed class KeyLocalization : ILocalizationService
{
    public string this[string key] => key;
    public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
    public void SetLanguage(SupportedLanguage language) { }
    public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = Enum.GetValues<SupportedLanguage>();
    public event EventHandler? LanguageChanged { add { } remove { } }
}
