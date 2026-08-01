using System.Collections.ObjectModel;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.DTOs;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class CollectionManagementModelRegressionTests
{
    [Fact]
    public async Task Create_WhenRepositoryReturnsZero_ShowsErrorWithoutSuccessOrReload()
    {
        var repo = new CollectionRepositoryStub { CreateResult = 0 };
        var dialog = new RegressionDialogService { InputResult = "New collection" };
        var model = CreateModel(repo, dialog);
        var initialLoads = repo.GetAllCalls;

        await model.CreateCollectionCommand.ExecuteAsync(null);

        Assert.NotNull(dialog.LastErrorMessage);
        Assert.Null(dialog.LastMessage);
        Assert.Equal(initialLoads, repo.GetAllCalls);
    }

    [Fact]
    public async Task Rename_WhenRepositoryReturnsFalse_ShowsErrorWithoutReload()
    {
        var repo = new CollectionRepositoryStub();
        var dialog = new RegressionDialogService { InputResult = "Renamed" };
        var model = CreateModel(repo, dialog);
        model.SelectedCollection = Assert.Single(model.Collections);
        repo.UpdateResult = false;
        var initialLoads = repo.GetAllCalls;

        await model.RenameCollectionCommand.ExecuteAsync(null);

        Assert.NotNull(dialog.LastErrorMessage);
        Assert.Null(dialog.LastMessage);
        Assert.Equal(initialLoads, repo.GetAllCalls);
    }

    [Fact]
    public async Task Delete_WhenRepositoryThrows_ShowsErrorAndKeepsSelection()
    {
        var repo = new CollectionRepositoryStub { DeleteException = new InvalidOperationException() };
        var dialog = new RegressionDialogService { ConfirmResult = true };
        var model = CreateModel(repo, dialog);
        model.SelectedCollection = Assert.Single(model.Collections);
        var selectedId = model.SelectedCollection.Id;

        await model.DeleteCollectionCommand.ExecuteAsync(null);

        Assert.NotNull(dialog.LastErrorMessage);
        Assert.Null(dialog.LastMessage);
        Assert.Equal(selectedId, model.SelectedCollection?.Id);
    }

    [Fact]
    public async Task AddDocument_WhenRepositoryReturnsFalse_ShowsErrorWithoutSuccess()
    {
        var repo = new CollectionRepositoryStub();
        var dialog = new RegressionDialogService { ConfirmResult = true };
        var customDialog = new RegressionCustomDialogService
        {
            SelectedDocuments = [new StudyDocument { Id = 20, Name = "Doc" }]
        };
        var model = CreateModel(repo, dialog, customDialog);
        model.SelectedCollection = Assert.Single(model.Collections);
        repo.AddDocumentResult = false;

        await model.AddDocumentToCollectionCommand.ExecuteAsync(null);

        Assert.NotNull(dialog.LastErrorMessage);
        Assert.Null(dialog.LastMessage);
    }

    [Fact]
    public async Task RemoveDocument_WhenRepositoryReturnsFalse_ShowsErrorWithoutSuccess()
    {
        var repo = new CollectionRepositoryStub();
        var dialog = new RegressionDialogService { ConfirmResult = true };
        var model = CreateModel(repo, dialog);
        model.SelectedCollection = Assert.Single(model.Collections);
        var document = Assert.Single(model.DocumentsInCollection);
        repo.RemoveDocumentResult = false;

        await model.RemoveDocumentFromCollectionCommand.ExecuteAsync(document);

        Assert.NotNull(dialog.LastErrorMessage);
        Assert.Null(dialog.LastMessage);
    }

    [Fact]
    public async Task SuccessfulCollectionOperations_ReloadAndPreserveSelectionAndMembers()
    {
        var repo = new CollectionRepositoryStub();
        var dialog = new RegressionDialogService { ConfirmResult = true, InputResult = "Renamed" };
        var customDialog = new RegressionCustomDialogService
        {
            SelectedDocuments = [new StudyDocument { Id = 21, Name = "Added" }]
        };
        var model = CreateModel(repo, dialog, customDialog);
        model.SelectedCollection = Assert.Single(model.Collections);
        var selectedId = model.SelectedCollection.Id;
        Assert.Single(model.DocumentsInCollection);

        await model.RenameCollectionCommand.ExecuteAsync(null);
        Assert.Equal(selectedId, model.SelectedCollection?.Id);
        Assert.Single(model.DocumentsInCollection);

        await model.AddDocumentToCollectionCommand.ExecuteAsync(null);
        Assert.Equal(selectedId, model.SelectedCollection?.Id);
        Assert.Single(model.DocumentsInCollection);

        await model.RemoveDocumentFromCollectionCommand.ExecuteAsync(model.DocumentsInCollection[0]);
        Assert.Equal(selectedId, model.SelectedCollection?.Id);
        Assert.Single(model.DocumentsInCollection);
        Assert.True(repo.GetAllCalls >= 4);
    }

    private static CollectionManagementModel CreateModel(
        CollectionRepositoryStub repo,
        RegressionDialogService dialog,
        RegressionCustomDialogService? customDialog = null)
        => new(new DocumentRepositoryStub(new StudyDocument { Id = 20, Name = "Doc" }, new StudyDocument { Id = 21, Name = "Added" }),
            repo, dialog, customDialog ?? new RegressionCustomDialogService(), new RegressionLocalizationService());
}

public sealed class RelatedDocumentsModelRegressionTests
{
    [Fact]
    public async Task AddRelation_WithNoSelection_DoesNotCallRepository()
    {
        var repo = new RelatedRepositoryStub();
        var model = CreateModel(repo);
        model.Load(1, "Main");

        await model.AddRelationCommand.ExecuteAsync(null);

        Assert.Equal(0, repo.AddCalls);
    }

    [Fact]
    public async Task AddRelation_WhenRepositoryThrows_PreservesSelectionAndAvailableDocuments()
    {
        var repo = new RelatedRepositoryStub { AddException = new InvalidOperationException() };
        var model = CreateModel(repo);
        model.Load(1, "Main");
        var selected = Assert.Single(model.AvailableDocuments);
        model.SelectedAvailableDoc = selected;
        var available = model.AvailableDocuments.ToArray();

        await model.AddRelationCommand.ExecuteAsync(null);

        Assert.Same(selected, model.SelectedAvailableDoc);
        Assert.Equal(available, model.AvailableDocuments);
    }

    [Fact]
    public async Task AddRelation_SuccessResetsSelectionAndPassesCanonicalRelation()
    {
        var repo = new RelatedRepositoryStub();
        var model = CreateModel(repo);
        model.Load(1, "Main");
        model.SelectedAvailableDoc = Assert.Single(model.AvailableDocuments);
        model.SelectedRelationType = "reference-ja";

        await model.AddRelationCommand.ExecuteAsync(null);

        Assert.Null(model.SelectedAvailableDoc);
        Assert.Equal("reference", repo.LastRelationType);
        Assert.Equal(1, repo.AddCalls);
    }

    [Fact]
    public async Task RemoveRelation_WhenRepositoryThrows_PreservesRelatedState()
    {
        var repo = new RelatedRepositoryStub
        {
            Related = [(new StudyDocument { Id = 2, Name = "Related" }, 9, "reference")],
            RemoveException = new InvalidOperationException()
        };
        var dialog = new RegressionDialogService { ConfirmResult = true };
        var model = CreateModel(repo, dialog);
        model.Load(1, "Main");
        var item = Assert.Single(model.RelatedDocuments);
        var available = model.AvailableDocuments.ToArray();

        await model.RemoveRelationCommand.ExecuteAsync(item);

        Assert.Single(model.RelatedDocuments);
        Assert.Equal(available, model.AvailableDocuments);
    }

    [Fact]
    public async Task RemoveRelation_SuccessRefreshesRelatedAndAvailableDocuments()
    {
        var repo = new RelatedRepositoryStub
        {
            Related = [(new StudyDocument { Id = 2, Name = "Related" }, 9, "reference")]
        };
        var dialog = new RegressionDialogService { ConfirmResult = true };
        var model = CreateModel(repo, dialog);
        model.Load(1, "Main");
        var item = Assert.Single(model.RelatedDocuments);

        await model.RemoveRelationCommand.ExecuteAsync(item);

        Assert.Empty(model.RelatedDocuments);
        Assert.Contains(model.AvailableDocuments, document => document.Id == 2);
        Assert.Equal(1, repo.RemoveCalls);
    }

    private static RelatedDocumentsModel CreateModel(RelatedRepositoryStub relatedRepo, RegressionDialogService? dialog = null)
        => new(new DocumentRepositoryStub(new StudyDocument { Id = 1, Name = "Main" }, new StudyDocument { Id = 2, Name = "Available" }),
            relatedRepo, dialog ?? new RegressionDialogService(), new RegressionNavigationService(), new RegressionLocalizationService());
}

public sealed class CategoryManagementModelRegressionTests
{
    [Fact]
    public async Task AddSubject_WhenRepositoryReturnsFalse_ShowsErrorWithoutSuccessOrReload()
    {
        var repo = new CategoryRepositoryStub { AddSubjectResult = false };
        var dialog = new RegressionDialogService { InputResult = "New" };
        var model = CreateModel(repo, dialog);
        var initialLoads = repo.LoadCalls;

        await model.AddSubjectCommand.ExecuteAsync(null);

        Assert.NotNull(dialog.LastErrorMessage);
        Assert.Null(dialog.LastMessage);
        Assert.Equal(initialLoads, repo.LoadCalls);
    }

    [Fact]
    public async Task RenameSubject_WhenRepositoryThrows_ShowsErrorWithoutSuccess()
    {
        var repo = new CategoryRepositoryStub { UpdateSubjectException = new InvalidOperationException() };
        var dialog = new RegressionDialogService { InputResult = "Renamed" };
        var model = CreateModel(repo, dialog);
        model.SelectedSubject = Assert.Single(model.Subjects);

        await model.RenameSubjectCommand.ExecuteAsync(null);

        Assert.NotNull(dialog.LastErrorMessage);
        Assert.Null(dialog.LastMessage);
    }

    [Fact]
    public async Task DeleteSubject_WhenRepositoryReturnsFalse_ShowsErrorWithoutSuccess()
    {
        var repo = new CategoryRepositoryStub { DeleteSubjectResult = false };
        var dialog = new RegressionDialogService { ConfirmResult = true };
        var model = CreateModel(repo, dialog);
        model.SelectedSubject = Assert.Single(model.Subjects);

        await model.DeleteSubjectCommand.ExecuteAsync(null);

        Assert.NotNull(dialog.LastErrorMessage);
        Assert.Null(dialog.LastMessage);
    }

    [Fact]
    public async Task SuccessfulCategoryOperations_RefreshLists()
    {
        var repo = new CategoryRepositoryStub();
        var dialog = new RegressionDialogService { ConfirmResult = true, InputResult = "Added" };
        var model = CreateModel(repo, dialog);

        await model.AddSubjectCommand.ExecuteAsync(null);
        Assert.Contains(model.Subjects, item => item.Name == "Added");

        dialog.InputResult = "Renamed";
        model.SelectedSubject = model.Subjects.First(item => item.Name == "Existing");
        await model.RenameSubjectCommand.ExecuteAsync(null);
        Assert.Contains(model.Subjects, item => item.Name == "Renamed");

        model.SelectedSubject = model.Subjects.First(item => item.Name == "Renamed");
        await model.DeleteSubjectCommand.ExecuteAsync(null);
        Assert.DoesNotContain(model.Subjects, item => item.Name == "Renamed");
        Assert.NotNull(dialog.LastMessage);
    }

    private static CategoryManagementModel CreateModel(CategoryRepositoryStub repo, RegressionDialogService dialog)
        => new(new DocumentRepositoryStub(), repo, dialog, new RegressionLocalizationService());
}

public sealed class CollectionRepositoryStub : ICollectionRepository
{
    private readonly List<StudyDocument> _documents = [new() { Id = 10, Name = "Member" }];
    public int GetAllCalls { get; private set; }
    public int CreateResult { get; set; } = 41;
    public bool UpdateResult { get; set; } = true;
    public bool DeleteResult { get; set; } = true;
    public bool AddDocumentResult { get; set; } = true;
    public bool RemoveDocumentResult { get; set; } = true;
    public Exception? UpdateException { get; set; }
    public Exception? DeleteException { get; set; }
    public List<(int Id, string Name, string? Description, DateTime CreatedAt, int ItemCount)> GetAll()
    {
        GetAllCalls++;
        return [(7, "Existing", null, DateTime.UtcNow, 1)];
    }
    public int Create(string name, string? description = null) => CreateResult;
    public bool Update(int id, string name, string? description = null)
    {
        if (UpdateException != null) throw UpdateException;
        return UpdateResult;
    }
    public bool Delete(int id)
    {
        if (DeleteException != null) throw DeleteException;
        return DeleteResult;
    }
    public List<StudyDocument> GetDocuments(int collectionId) => [.._documents];
    public bool AddDocument(int collectionId, int documentId) => AddDocumentResult;
    public bool RemoveDocument(int collectionId, int documentId) => RemoveDocumentResult;
}

public sealed class RelatedRepositoryStub : IRelatedDocumentRepository
{
    public List<(StudyDocument Doc, int RelationId, string RelationType)> Related { get; set; } = [];
    public int AddCalls { get; private set; }
    public int RemoveCalls { get; private set; }
    public string? LastRelationType { get; private set; }
    public Exception? AddException { get; set; }
    public Exception? RemoveException { get; set; }
    public List<(StudyDocument Doc, int RelationId, string RelationType)> GetRelated(int docId) => [..Related];
    public void AddRelation(int docId1, int docId2, string relationType = "related")
    {
        AddCalls++;
        if (AddException != null) throw AddException;
        LastRelationType = relationType;
    }
    public void RemoveRelation(int relationId)
    {
        RemoveCalls++;
        if (RemoveException != null) throw RemoveException;
        Related = [];
    }
}

public sealed class CategoryRepositoryStub : ICategoryRepository
{
    private readonly List<string> _subjects = ["Existing"];
    private readonly List<string> _types = ["Type"];
    public int LoadCalls { get; private set; }
    public bool AddSubjectResult { get; set; } = true;
    public bool UpdateSubjectResult { get; set; } = true;
    public bool DeleteSubjectResult { get; set; } = true;
    public Exception? UpdateSubjectException { get; set; }
    public List<string> GetAllSubjects() => [.._subjects];
    public List<string> GetAllTypes() => [.._types];
    public List<(string Name, int Count)> GetSubjectsWithCount()
    {
        LoadCalls++;
        return _subjects.Select(name => (name, 0)).ToList();
    }
    public List<(string Name, int Count)> GetTypesWithCount() => _types.Select(name => (name, 0)).ToList();
    public bool AddSubject(string name)
    {
        if (!AddSubjectResult) return false;
        _subjects.Add(name);
        return true;
    }
    public bool AddType(string name) => true;
    public bool UpdateSubjectName(string oldName, string newName)
    {
        if (UpdateSubjectException != null) throw UpdateSubjectException;
        if (!UpdateSubjectResult) return false;
        var index = _subjects.IndexOf(oldName);
        if (index >= 0) _subjects[index] = newName;
        return true;
    }
    public bool UpdateTypeName(string oldName, string newName) => true;
    public bool DeleteDocumentsBySubject(string subjectName)
    {
        if (!DeleteSubjectResult) return false;
        _subjects.Remove(subjectName);
        return true;
    }
    public bool DeleteDocumentsByType(string typeName) => true;
    public int GetTotalDocumentCount() => 0;
}

public sealed class DocumentRepositoryStub(params StudyDocument[] documents) : IDocumentRepository
{
    private readonly List<StudyDocument> _documents = [..documents];
    public List<StudyDocument> GetAll() => [.._documents];
    public StudyDocument? GetById(int id) => _documents.FirstOrDefault(document => document.Id == id);
    public List<StudyDocument> Search(string keyword) => [];
    public List<StudyDocument> Filter(string subject, string type) => [];
    public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => [];
    public bool Add(StudyDocument document) => false;
    public bool AddWithCatalogs(StudyDocument document) => false;
    public bool Update(StudyDocument document) => false;
    public bool Delete(int id) => false;
    public List<string> GetDistinctSubjects() => [];
    public List<string> GetDistinctTypes() => [];
    public List<string> GetDistinctTags() => [];
    public List<StudyDocument> GetUpcomingDeadlines(int days) => [];
    public List<StudyDocument> GetOverdueDocuments() => [];
    public void EnsureSubjectExists(string subject) { }
    public void EnsureTypeExists(string type) { }
}

public sealed class RegressionDialogService : IDialogService
{
    public bool ConfirmResult { get; set; }
    public string? InputResult { get; set; }
    public string? LastMessage { get; private set; }
    public string? LastErrorMessage { get; private set; }
    public Task ShowMessageAsync(string title, string message) { LastMessage = message; return Task.CompletedTask; }
    public Task ShowErrorAsync(string title, string message) { LastErrorMessage = message; return Task.CompletedTask; }
    public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(ConfirmResult);
    public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(ConfirmResult);
    public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult(InputResult);
}

public sealed class RegressionCustomDialogService : ICustomDialogService
{
    public List<StudyDocument>? SelectedDocuments { get; set; }
    public Task<List<StudyDocument>?> ShowDocumentPickerAsync(string collectionName, IEnumerable<StudyDocument> allDocuments, IEnumerable<int> alreadyInCollection)
        => Task.FromResult(SelectedDocuments);
    public Task<string?> ShowChangeCategoryAsync(string documentName, IList<string> existingCategories, string currentCategory)
        => Task.FromResult<string?>(null);
    public Task<AddDocumentDraft?> ShowAddDocumentAsync(string filePath, IList<string> subjects, IList<string> types)
        => Task.FromResult<AddDocumentDraft?>(null);
    public Task<int> ShowSelectCollectionAsync(string documentName, IList<(int Id, string Name, int DocCount)> collections)
        => Task.FromResult(-1);
}

public sealed class RegressionNavigationService : INavigationService
{
    public bool CanGoBack => false;
    public void NavigateTo(string viewKey) { }
    public void NavigateTo(string viewKey, object? parameter) { }
    public void GoBack() { }
}

public sealed class RegressionLocalizationService : ILocalizationService
{
    public string this[string key] => key switch
    {
        "RelatedDocs_RelationType_reference" => "reference-ja",
        "RelatedDocs_RelationType_related" => "related-ja",
        "RelatedDocs_RelationType_supplement" => "supplement-ja",
        "RelatedDocs_RelationType_prerequisite" => "prerequisite-ja",
        "RelatedDocs_RelationType_sequel" => "sequel-ja",
        _ => key
    };
    public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
    public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = Enum.GetValues<SupportedLanguage>();
    public event EventHandler? LanguageChanged { add { } remove { } }
    public void SetLanguage(SupportedLanguage language) { }
}
