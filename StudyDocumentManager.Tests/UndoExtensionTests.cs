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
    public void ApplyLast_EmptyStack_Throws()
    {
        var applier = new UndoApplier(new UndoService(), new RecordingDocuments(), new RecordingRecycleBin(), new RecordingCollections());

        Assert.Throws<InvalidOperationException>(applier.ApplyLast);
    }

    private sealed class RecordingRecycleBin : IRecycleBinRepository
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

    private sealed class RecordingCollections : ICollectionRepository
    {
        public int NextId { get; set; } = 42;
        public List<(string Name, string? Description)> Created { get; } = [];
        public List<(int CollectionId, int DocumentId)> AddedDocs { get; } = [];

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

        public bool RemoveDocument(int collectionId, int documentId) => true;
    }

    private sealed class RecordingDocuments : IDocumentRepository
    {
        public List<StudyDocument> Updated { get; } = [];

        public List<StudyDocument> GetAll() => [];
        public StudyDocument? GetById(int id) => null;
        public List<StudyDocument> Search(string keyword) => [];
        public List<StudyDocument> Filter(string subject, string type) => [];
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => [];
        public bool Add(StudyDocument document) => true;
        public bool AddWithCatalogs(StudyDocument document) => true;

        public bool Update(StudyDocument document)
        {
            Updated.Add(document);
            return true;
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
