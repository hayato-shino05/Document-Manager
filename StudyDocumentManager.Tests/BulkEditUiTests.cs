using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.DTOs;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public class BulkEditUiTests : DatabaseTestBase
{
    private BulkDeleteModel CreateModel(
        BulkEditMetadataStub bulkRepo,
        RecordingPreviewDialogService? previewDialog = null,
        UndoService? undo = null,
        BulkEditDialogStub? dialogStub = null)
    {
        var documentRepo = new DocumentRepository(Db);
        return new BulkDeleteModel(
            documentRepo,
            bulkRepo,
            new CategoryRepository(Db),
            dialogStub ?? new BulkEditDialogStub(),
            new BulkEditNavigationStub(),
            new BulkEditLocalizationStub(),
            previewDialog ?? new RecordingPreviewDialogService(),
            new CollectionRepository(Db),
            undo ?? new UndoService());
    }

    [Fact]
    public async Task Apply_NoSelection_GatedAndNothingMutated()
    {
        var documentRepo = new DocumentRepository(Db);
        var bulkRepo = new BulkEditMetadataStub(documentRepo);
        var dialog = new RecordingPreviewDialogService();
        Repo.Add(new StudyDocument { Name = "Alpha", Subject = "Math", Type = "PDF" });
        var model = CreateModel(bulkRepo, dialog);
        model.Initialize();

        model.EnableSubject = true;
        model.NewSubject = "Physics";
        Assert.False(model.ApplyBulkEditCommand.CanExecute(null));

        await model.ApplyBulkEditCommand.ExecuteAsync(null);

        Assert.Equal(0, bulkRepo.CallCount);
        Assert.Empty(dialog.PreviewCalls);
        Assert.All(documentRepo.GetAll(), d => Assert.Equal("Math", d.Subject));
    }

    [Fact]
    public void Apply_SelectionWithEnabledField_CanExecute()
    {
        var bulkRepo = new BulkEditMetadataStub(new DocumentRepository(Db));
        Repo.Add(new StudyDocument { Name = "Alpha", Subject = "Math", Type = "PDF" });
        var model = CreateModel(bulkRepo);
        model.Initialize();
        model.Documents[0].IsSelected = true;

        Assert.False(model.HasAnyEnabledChange);
        Assert.False(model.ApplyBulkEditCommand.CanExecute(null));

        model.EnableStatus = true;
        Assert.True(model.HasAnyEnabledChange);
        Assert.True(model.ApplyBulkEditCommand.CanExecute(null));
    }

    [Fact]
    public async Task Apply_HappyPath_UpdatesDb_UndoRestoresAllPriorFields()
    {
        var documentRepo = new DocumentRepository(Db);
        var bulkRepo = new BulkEditMetadataStub(documentRepo);
        var dialog = new RecordingPreviewDialogService { PreviewResult = true };
        var undo = new UndoService();

        Repo.Add(new StudyDocument { Name = "Alpha", Subject = "Math", Type = "PDF", Status = DocumentStatus.Unread, Tags = "", IsImportant = false });
        Repo.Add(new StudyDocument { Name = "Beta", Subject = "Chemistry", Type = "DOCX", Status = DocumentStatus.Read, Tags = "chem", IsImportant = true });
        Repo.Add(new StudyDocument { Name = "Gamma", Subject = "History", Type = "PDF", Status = DocumentStatus.InProgress, Tags = "hist", IsImportant = false });
        var ids = documentRepo.GetAll().Select(d => d.Id).ToList();

        var originals = documentRepo.GetAll().ToDictionary(d => d.Id, Clone);

        var model = CreateModel(bulkRepo, dialog, undo);
        model.Initialize();
        foreach (var row in model.Documents)
            row.IsSelected = true;
        model.EnableSubject = true;
        model.NewSubject = "Physics";
        model.EnableTags = true;
        model.NewTags = "bulk";
        model.EnableStatus = true;
        model.NewStatus = DocumentStatus.Completed;

        await model.ApplyBulkEditCommand.ExecuteAsync(null);

        foreach (var id in ids)
        {
            var updated = documentRepo.GetById(id)!;
            Assert.Equal("Physics", updated.Subject);
            Assert.Equal("bulk", updated.Tags);
            Assert.Equal(DocumentStatus.Completed, updated.Status);
        }
        Assert.True(undo.CanUndo);

        await model.UndoLastCommand.ExecuteAsync(null);

        foreach (var (id, original) in originals)
        {
            var restored = documentRepo.GetById(id)!;
            Assert.Equal(original.Subject, restored.Subject);
            Assert.Equal(original.Tags, restored.Tags);
            Assert.Equal(original.Status, restored.Status);
            Assert.Equal(original.IsImportant, restored.IsImportant);
        }
        Assert.False(model.UndoLastCommand.CanExecute(null));
    }

    [Fact]
    public async Task Apply_PartialFailure_ReportsFailedNames_StillOffersUndo()
    {
        var documentRepo = new DocumentRepository(Db);
        var bulkRepo = new BulkEditMetadataStub(documentRepo);
        var dialog = new RecordingPreviewDialogService { PreviewResult = true };
        var dialogStub = new BulkEditDialogStub();

        Repo.Add(new StudyDocument { Name = "Alpha", Subject = "Math", Type = "PDF", Status = DocumentStatus.Unread });
        Repo.Add(new StudyDocument { Name = "Beta", Subject = "Math", Type = "PDF", Status = DocumentStatus.Read });
        Repo.Add(new StudyDocument { Name = "Gamma", Subject = "Math", Type = "PDF", Status = DocumentStatus.InProgress });
        var (id1, id2, id3) = GetIdsByRow();

        bulkRepo.FailIds.Add(id2);

        var model = CreateModel(bulkRepo, dialog, dialogStub: dialogStub);
        model.Initialize();
        foreach (var row in model.Documents)
            row.IsSelected = true;
        model.EnableStatus = true;
        model.NewStatus = DocumentStatus.Archived;

        await model.ApplyBulkEditCommand.ExecuteAsync(null);

        Assert.Single(dialogStub.Errors);
        Assert.Contains("BE_Result_Partial", dialogStub.Errors[0]);
        Assert.Contains("BE_FailedItemsHeader", dialogStub.Errors[0]);
        Assert.Contains("Beta", dialogStub.Errors[0]);

        Assert.Equal(DocumentStatus.Archived, documentRepo.GetById(id1)!.Status);
        Assert.Equal(DocumentStatus.Read, documentRepo.GetById(id2)!.Status);
        Assert.Equal(DocumentStatus.Archived, documentRepo.GetById(id3)!.Status);

        Assert.True(model.UndoLastCommand.CanExecute(null));
        Assert.True(documentRepo.Delete(id2));
        Assert.True(documentRepo.PermanentDeleteDocument(id2));
        await model.UndoLastCommand.ExecuteAsync(null);
        Assert.Equal(DocumentStatus.Unread, documentRepo.GetById(id1)!.Status);
        Assert.Null(documentRepo.GetById(id2));
        Assert.Equal(DocumentStatus.InProgress, documentRepo.GetById(id3)!.Status);
    }

    [Fact]
    public async Task Preview_PayloadContainsOnlyEnabledFields()
    {
        var documentRepo = new DocumentRepository(Db);
        var bulkRepo = new BulkEditMetadataStub(documentRepo);
        var dialog = new RecordingPreviewDialogService { PreviewResult = false };

        Repo.Add(new StudyDocument { Name = "Alpha", Subject = "Math", Type = "PDF" });
        Repo.Add(new StudyDocument { Name = "Beta", Subject = "Math", Type = "PDF" });
        Repo.Add(new StudyDocument { Name = "Gamma", Subject = "Math", Type = "PDF" });

        var model = CreateModel(bulkRepo, dialog);
        model.Initialize();
        foreach (var row in model.Documents)
            row.IsSelected = true;
        model.EnableSubject = true;
        model.NewSubject = "Physics";

        await model.ApplyBulkEditCommand.ExecuteAsync(null);

        var call = Assert.Single(dialog.PreviewCalls);
        Assert.Equal(3, call.AffectedCount);
        var pair = Assert.Single(call.Changes);
        Assert.Equal("BE_Field_Subject", pair.FieldLabel);
        Assert.Equal("Physics", pair.NewValue);
        Assert.All(documentRepo.GetAll(), d => Assert.Equal("Math", d.Subject));
    }

    [Fact]
    public void UndoService_PushBeyondCap_EvictsOldest()
    {
        var undo = new UndoService();
        for (var i = 1; i <= 11; i++)
        {
            undo.Push(new UndoEntry
            {
                DescriptionKey = "BE_UndoDescription",
                DescriptionArgs = [i],
                Originals = [new StudyDocument { Id = i }],
                CreatedAt = DateTime.Now
            });
        }

        Assert.True(undo.CanUndo);
        Assert.Equal(11, undo.Peek()!.Originals[0].Id);

        for (var expected = 11; expected >= 2; expected--)
            Assert.Equal(expected, undo.Pop()!.Originals[0].Id);

        Assert.False(undo.CanUndo);
        Assert.Null(undo.Peek());
        Assert.Null(undo.Pop());
    }

    private static StudyDocument Clone(StudyDocument source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Subject = source.Subject,
        Type = source.Type,
        FilePath = source.FilePath,
        Notes = source.Notes,
        CreatedAt = source.CreatedAt,
        FileSize = source.FileSize,
        Author = source.Author,
        IsImportant = source.IsImportant,
        Tags = source.Tags,
        Deadline = source.Deadline,
        Status = source.Status
    };

    private (int First, int Second, int Third) GetIdsByRow()
    {
        var ids = Repo.GetAll().OrderBy(d => d.Id).Select(d => d.Id).ToList();
        return (ids[0], ids[1], ids[2]);
    }

    private sealed class BulkEditMetadataStub(DocumentRepository repository) : IBulkOperationRepository
    {
        public HashSet<int> FailIds { get; } = [];
        public int CallCount { get; private set; }

        public BulkEditOutcome BulkEditMetadata(IReadOnlyList<int> documentIds, BulkEditChanges changes)
        {
            CallCount++;
            var items = new List<BulkItemResult>();
            foreach (var id in documentIds)
            {
                if (FailIds.Contains(id))
                {
                    items.Add(new BulkItemResult(id, false));
                    continue;
                }

                var document = repository.GetById(id);
                if (document == null)
                {
                    items.Add(new BulkItemResult(id, false));
                    continue;
                }

                if (changes.Subject != null) document.Subject = changes.Subject;
                if (changes.Type != null) document.Type = changes.Type;
                if (changes.Tags != null) document.Tags = changes.Tags;
                if (changes.IsImportant.HasValue) document.IsImportant = changes.IsImportant.Value;
                if (changes.Deadline.HasValue) document.Deadline = changes.Deadline;
                if (changes.Status != null) document.Status = changes.Status;

                items.Add(new BulkItemResult(id, repository.Update(document)));
            }

            return new BulkEditOutcome
            {
                Requested = documentIds.Count,
                Succeeded = items.Count(i => i.Success),
                Items = items
            };
        }

        public int BulkSoftDelete(List<int> ids) => repository.BulkSoftDelete(ids);
        public int BulkUpdateSubject(List<int> ids, string subject) => repository.BulkUpdateSubject(ids, subject);
        public int BulkToggleImportant(List<int> ids, bool important) => repository.BulkToggleImportant(ids, important);
        public int BulkUpdateStatus(List<int> ids, string status) => repository.BulkUpdateStatus(ids, status);
    }

    private sealed class RecordingPreviewDialogService : ICustomDialogService
    {
        public bool PreviewResult { get; set; }
        public List<(int AffectedCount, List<(string FieldLabel, string NewValue)> Changes)> PreviewCalls { get; } = [];

        public Task<bool> ShowBulkEditPreviewAsync(int affectedCount, IReadOnlyList<(string FieldLabel, string NewValue)> changes)
        {
            PreviewCalls.Add((affectedCount, [.. changes]));
            return Task.FromResult(PreviewResult);
        }

        public Task<string?> ShowChangeCategoryAsync(string documentName, IList<string> existingCategories, string currentCategory)
            => Task.FromResult<string?>(null);

        public Task<int> ShowSelectCollectionAsync(string documentName, IList<(int Id, string Name, int DocCount)> collections)
            => Task.FromResult(-1);

        public Task<List<StudyDocument>?> ShowDocumentPickerAsync(
            string collectionName, IEnumerable<StudyDocument> allDocuments, IEnumerable<int> alreadyInCollection)
            => Task.FromResult<List<StudyDocument>?>(null);

        public Task<AddDocumentDraft?> ShowAddDocumentAsync(string filePath, IList<string> subjects, IList<string> types)
            => Task.FromResult<AddDocumentDraft?>(null);
    }

    private sealed class BulkEditDialogStub : IDialogService
    {
        public List<string> Errors { get; } = [];
        public List<string> Messages { get; } = [];

        public Task ShowMessageAsync(string title, string message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(string title, string message)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }

        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(true);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "")
            => Task.FromResult<string?>(null);
    }

    private sealed class BulkEditNavigationStub : INavigationService
    {
        public bool CanGoBack => false;
        public void NavigateTo(string viewKey) { }
        public void NavigateTo(string viewKey, object? parameter) { }
        public void GoBack() { }
    }

    private sealed class BulkEditLocalizationStub : ILocalizationService
    {
        public string this[string key] => key;
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public void SetLanguage(SupportedLanguage language) { }
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [];
        public event EventHandler? LanguageChanged { add { } remove { } }
    }
}

public sealed class BulkDeleteApplyButtonBindingTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sdm_test_{Guid.NewGuid():N}.db");
    private readonly DatabaseHelper _db;
    private readonly DocumentRepository _repo;

    public BulkDeleteApplyButtonBindingTests()
    {
        _db = new DatabaseHelper();
        _db.SetDatabasePath(_dbPath);
        _db.InitializeDatabase();
        _repo = new DocumentRepository(_db);
    }

    [AvaloniaFact]
    public void ApplyButton_BindsApplyBulkEditCommand_AndExecutesWhenSelectionAndChangesValid()
    {
        Application.Current!.Resources["Loc"] = new LocalizationService();
        _repo.Add(new StudyDocument { Name = "Alpha", Subject = "Math", Type = "PDF" });

        var model = new BulkDeleteModel(
            _repo,
            _repo,
            new CategoryRepository(_db),
            new ApplyDialogStub(),
            new ApplyNavigationStub(),
            new ApplyLocalizationStub(),
            new ApplyPreviewDialogs { PreviewResult = true },
            new CollectionRepository(_db),
            new UndoService());

        var view = new Views.BulkDelete { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.NotEmpty(model.Documents);
            var expander = view.GetVisualDescendants().OfType<Expander>().Single();
            expander.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();

            foreach (var row in model.Documents)
                row.IsSelected = true;
            model.EnableSubject = true;
            model.NewSubject = "Physics";
            Dispatcher.UIThread.RunJobs();

            var button = Assert.Single(view.GetVisualDescendants().OfType<Button>(),
                b => AutomationProperties.GetAutomationId(b) == "BulkEdit_Apply");

            Assert.Same(model.ApplyBulkEditCommand, button.Command);
            Assert.True(button.IsEnabled);
            Assert.True(button.Command!.CanExecute(null));

            button.Command.Execute(null);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();

            Assert.All(_repo.GetAll(), d => Assert.Equal("Physics", d.Subject));
            Assert.True(model.UndoLastCommand.CanExecute(null));
        }
        finally
        {
            window.Close();
        }
    }

    public void Dispose()
    {
        _db.CloseAllConnections();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); }
        catch { }
    }

    private sealed class ApplyDialogStub : IDialogService
    {
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(true);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
    }

    private sealed class ApplyPreviewDialogs : ICustomDialogService
    {
        public bool PreviewResult { get; set; }

        public Task<bool> ShowBulkEditPreviewAsync(int affectedCount, IReadOnlyList<(string FieldLabel, string NewValue)> changes)
            => Task.FromResult(PreviewResult);

        public Task<string?> ShowChangeCategoryAsync(string documentName, IList<string> existingCategories, string currentCategory)
            => Task.FromResult<string?>(null);

        public Task<int> ShowSelectCollectionAsync(string documentName, IList<(int Id, string Name, int DocCount)> collections)
            => Task.FromResult(-1);

        public Task<List<StudyDocument>?> ShowDocumentPickerAsync(string collectionName, IEnumerable<StudyDocument> allDocuments, IEnumerable<int> alreadyInCollection)
            => Task.FromResult<List<StudyDocument>?>(null);

        public Task<AddDocumentDraft?> ShowAddDocumentAsync(string filePath, IList<string> subjects, IList<string> types)
            => Task.FromResult<AddDocumentDraft?>(null);
    }

    private sealed class ApplyNavigationStub : INavigationService
    {
        public bool CanGoBack => false;
        public void NavigateTo(string viewKey) { }
        public void NavigateTo(string viewKey, object? parameter) { }
        public void GoBack() { }
    }

    private sealed class ApplyLocalizationStub : ILocalizationService
    {
        public string this[string key] => key;
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public void SetLanguage(SupportedLanguage language) { }
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [];
        public event EventHandler? LanguageChanged { add { } remove { } }
    }
}
