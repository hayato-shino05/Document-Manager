using StudyDocumentManager.Core;
using StudyDocumentManager.Core.DTOs;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class DocumentStatusUiTests : DatabaseTestBase
{
    private readonly CategoryRepository _categoryRepo;

    public DocumentStatusUiTests()
    {
        _categoryRepo = new CategoryRepository(Db);
    }

    private int SeedDoc(string name, string subject, string status)
    {
        var doc = new StudyDocument
        {
            Name = name,
            Subject = subject,
            Type = "PDF",
            FilePath = $"C:\\seed\\{name}.pdf",
            Status = status
        };
        Assert.True(Repo.Add(doc));
        return doc.Id;
    }

    private DashboardModel CreateDashboard(
        RecordingDialogService? dialog = null,
        RecordingNavigationService? navigation = null)
    {
        var repository = new DocumentRepository(Db);
        return new DashboardModel(
            repository,
            repository,
            _categoryRepo,
            new CollectionRepository(Db),
            new StubRecentFileRepository(),
            dialog ?? new RecordingDialogService(),
            new StubFileDialogService(),
            new StubCustomDialogService(),
            navigation ?? new RecordingNavigationService(),
            new StubClipboardService(),
            new StubProcessLauncherService(),
            new StubExportService(),
            new StubBackupService(),
            new StubLocalizationService());
    }

    private AddEditModel CreateAddEdit()
        => new(
            new DocumentRepository(Db),
            _categoryRepo,
            new RecordingDialogService(),
            new StubFileDialogService(),
            new RecordingNavigationService(),
            new StubLocalizationService());

    private ReportModel CreateReport()
        => new(new ReportRepository(Db), Repo, new StubLocalizationService());

    [Fact]
    public void Dashboard_StatusFilter_NarrowsResultsAndSentinelRestoresAll()
    {
        SeedDoc("Alpha A", "Math", DocumentStatus.Unread);
        SeedDoc("Alpha B", "Math", DocumentStatus.Read);
        SeedDoc("Beta C", "Physics", DocumentStatus.Read);
        var model = CreateDashboard();
        model.Initialize();

        model.SearchKeyword = "Alpha";
        model.SelectedStatus = DocumentStatus.Read;

        var narrowed = model.Documents.Select(d => d.Name).ToList();
        Assert.Equal(["Alpha B"], narrowed);

        model.SelectedStatus = DashboardModel.FILTER_ALL_STATUS_KEY;

        Assert.Equal(["Alpha A", "Alpha B"], [.. model.Documents.Select(d => d.Name)]);
        Assert.Equal(0, model.ActiveFilterCount);
    }

    [Fact]
    public void Dashboard_RefreshCommand_ResetsStatusToSentinelAndReloadsAll()
    {
        SeedDoc("One", "Math", DocumentStatus.Unread);
        SeedDoc("Two", "Math", DocumentStatus.Read);
        SeedDoc("Three", "Physics", DocumentStatus.Archived);
        var model = CreateDashboard();
        model.Initialize();

        model.SelectedStatus = DocumentStatus.Archived;
        Assert.Equal(["Three"], [.. model.Documents.Select(d => d.Name)]);
        Assert.Equal(1, model.ActiveFilterCount);

        model.RefreshCommand.Execute(null);

        Assert.Equal(DashboardModel.FILTER_ALL_STATUS_KEY, model.SelectedStatus);
        Assert.Equal(0, model.ActiveFilterCount);
        Assert.Equal(3, model.Documents.Count);
    }

    [Fact]
    public void Dashboard_StatusOptions_ListSentinelPlusSixCanonicalKinds()
    {
        var model = CreateDashboard();

        Assert.Equal(DashboardModel.FILTER_ALL_STATUS_KEY, model.StatusOptions[0].Value);
        Assert.Equal(DocumentStatus.All, [.. model.StatusOptions.Skip(1).Select(o => o.Value)]);
        Assert.All(model.StatusOptions, o => Assert.False(string.IsNullOrWhiteSpace(o.Display)));
    }

    [Fact]
    public void AddEdit_LoadDocument_MapsStoredStatusIntoPickerState()
    {
        var id = SeedDoc("Stored", "Math", DocumentStatus.NeedsAction);
        var model = CreateAddEdit();

        model.LoadDocument(id);

        Assert.True(model.IsEditing);
        Assert.Equal(DocumentStatus.NeedsAction, model.SelectedStatus);
        Assert.Equal(DocumentStatus.All, [.. model.StatusOptions.Select(o => o.Value)]);
    }

    [Fact]
    public async Task AddEdit_Save_PersistsSelectedStatus()
    {
        var model = CreateAddEdit();
        model.Name = "Fresh";
        model.SelectedStatus = DocumentStatus.Completed;

        await model.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(Repo.GetAll());
        Assert.Equal(DocumentStatus.Completed, saved.Status);
        Assert.False(model.HasStatusValidationError);
    }

    [Fact]
    public async Task AddEdit_Save_InvalidSelection_BlocksSaveWithErrorFlag()
    {
        var model = CreateAddEdit();
        model.Name = "Broken";
        model.SelectedStatus = "bogus-status";

        await model.SaveCommand.ExecuteAsync(null);

        Assert.True(model.HasStatusValidationError);
        Assert.Equal("DS_Error_InvalidStatus", model.StatusValidationMessage);
        Assert.Empty(Repo.GetAll());
    }

    [Fact]
    public async Task AddEdit_Save_ValidSelectionAfterInvalid_ClearsError()
    {
        var model = CreateAddEdit();
        model.Name = "Recover";
        model.SelectedStatus = "bogus-status";
        await model.SaveCommand.ExecuteAsync(null);
        Assert.True(model.HasStatusValidationError);

        model.SelectedStatus = DocumentStatus.InProgress;

        Assert.False(model.HasStatusValidationError);
        Assert.Equal(string.Empty, model.StatusValidationMessage);
    }

    [Fact]
    public void Report_StatusCounts_MergesAllSixKindsInCanonicalOrderWithZerosFilled()
    {
        SeedDoc("A", "Math", DocumentStatus.Unread);
        SeedDoc("B", "Math", DocumentStatus.Unread);
        SeedDoc("C", "Physics", DocumentStatus.Read);
        var model = CreateReport();

        Assert.Equal(DocumentStatus.All, [.. model.ByStatusData.Select(i => i.Kind)]);
        Assert.Equal(2, model.ByStatusData.Single(i => i.Kind == DocumentStatus.Unread).Value);
        Assert.Equal(1, model.ByStatusData.Single(i => i.Kind == DocumentStatus.Read).Value);
        Assert.All(
            model.ByStatusData.Where(i => i.Kind is not (DocumentStatus.Unread or DocumentStatus.Read)),
            i => Assert.Equal(0, i.Value));
        Assert.All(model.ByStatusData, i => Assert.False(string.IsNullOrWhiteSpace(i.Label)));
    }

    [Fact]
    public void Report_DeletedDocuments_AreExcludedFromStatusCounts()
    {
        var id = SeedDoc("Gone", "Math", DocumentStatus.InProgress);
        Assert.True(Repo.Delete(id));

        var model = CreateReport();

        Assert.All(model.ByStatusData, i => Assert.Equal(0, i.Value));
    }

    private sealed class RecordingDialogService : IDialogService
    {
        public bool ConfirmResult { get; set; }
        public List<string> Messages { get; } = [];

        public Task ShowMessageAsync(string title, string message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(ConfirmResult);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
            => Task.FromResult(ConfirmResult);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "")
            => Task.FromResult<string?>(null);
    }

    private sealed class RecordingNavigationService : INavigationService
    {
        public List<(string Route, object? Parameter)> RoutesWithParameter { get; } = [];
        public bool CanGoBack => false;
        public void NavigateTo(string viewKey) => RoutesWithParameter.Add((viewKey, null));
        public void NavigateTo(string viewKey, object? parameter) => RoutesWithParameter.Add((viewKey, parameter));
        public void GoBack() { }
    }

    private sealed class StubRecentFileRepository : IRecentFileRepository
    {
        public List<(int Id, string Name, string? Subject, string? Type, string? FilePath, DateTime OpenedAt)> GetAll() => [];
        public bool Add(int documentId) => true;
        public void Clear() { }
    }

    private sealed class StubFileDialogService : IFileDialogService
    {
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null)
            => Task.FromResult<string?>(null);
    }

    private sealed class StubCustomDialogService : ICustomDialogService
    {
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

    private sealed class StubClipboardService : IClipboardService
    {
        public Task SetTextAsync(string text) => Task.CompletedTask;
    }

    private sealed class StubProcessLauncherService : IProcessLauncherService
    {
        public void OpenFile(string filePath) { }
        public void RevealInExplorer(string filePath) { }
        public void OpenUrl(string url) { }
    }

    private sealed class StubExportService : IExportService
    {
        public Task<ExportResult> ExportCsvAsync(IReadOnlyList<StudyDocument> documents, string? suggestedFileName)
            => Task.FromResult(new ExportResult(true));
    }

    private sealed class StubBackupService : IBackupService
    {
        public Task<(bool Success, string? Path, string? Error)> BackupAsync()
            => Task.FromResult<(bool, string?, string?)>((true, null, null));
        public Task<(bool Success, string? Error)> RestoreAsync()
            => Task.FromResult<(bool, string?)>((true, null));
    }

    private sealed class StubLocalizationService : ILocalizationService
    {
        public string this[string key] => key;
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public void SetLanguage(SupportedLanguage language) { }
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [];
        public event EventHandler? LanguageChanged { add { } remove { } }
    }
}
