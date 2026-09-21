using System.Collections.ObjectModel;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.DTOs;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Models.Items;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class DashboardPersonalizationFeatureTests
{
    [Fact]
    public void Dashboard_Initialize_SetsGreetingAndRecentQuickAccess()
    {
        var doc1 = new StudyDocument { Id = 1, Name = "Doc 1", Subject = "Math", IsImportant = true };
        var doc2 = new StudyDocument { Id = 2, Name = "Doc 2", Subject = "Science", Deadline = DateTime.Now.AddDays(1) };
        var repo = new MockDocRepo([doc1, doc2]);
        var recentRepo = new MockRecentRepo([(1, "Doc 1", "Math", "Document", "C:\\doc1.pdf", DateTime.Now)]);
        var loc = new MockLoc();

        var dashboard = CreateDashboard(repo, recentRepo, loc);
        dashboard.Initialize();

        Assert.False(string.IsNullOrEmpty(dashboard.GreetingTitle));
        Assert.False(string.IsNullOrEmpty(dashboard.GreetingSubtitle));
        Assert.Single(dashboard.RecentQuickAccessDocuments);
        Assert.Equal(1, dashboard.RecentQuickAccessDocuments[0].Id);
    }

    [Fact]
    public async Task Dashboard_OpenRecentDocumentCommand_ExecutesSuccessfully()
    {
        var doc1 = new StudyDocument { Id = 1, Name = "Doc 1", Subject = "Math", FilePath = "test.pdf" };
        var repo = new MockDocRepo([doc1]);
        var launcher = new MockLauncher();
        var dashboard = CreateDashboard(repo, processLauncher: launcher);
        dashboard.Initialize();

        await dashboard.OpenRecentDocumentCommand.ExecuteAsync(doc1);

        Assert.Equal(doc1, dashboard.SelectedDocument);
    }

    [Fact]
    public void ToastService_ResolveVisuals_WorksForAllToastTypes()
    {
        var toast = new ToastService();
        toast.Show("Test Success", ToastType.Success);
        toast.Show("Test Error", ToastType.Error);
        toast.Show("Test Warning", ToastType.Warning);
        toast.Show("Test Info", ToastType.Info);
    }

    [Fact]
    public async Task MainWindow_HandleDroppedFiles_ShowsToastOnDashboard()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var doc = new StudyDocument { Id = 1, Name = Path.GetFileName(tempFile), FilePath = tempFile };
            var repo = new MockDocRepo([doc]);
            var loc = new MockLoc();
            var toast = new MockToastService();
            var droppedService = new MockDroppedImportService();
            var dashboard = CreateDashboard(repo, loc: loc);
            var nav = new MockNavigation();
            var mainModel = new MainWindowModel(
                dashboard, nav, new MockDialog(), new MockCustomDialog(),
                droppedService, new MockLifecycle(), loc, new MockSettings(),
                new MockUpdateService(), null, null, toast);

            await mainModel.HandleDroppedFilesAsync([tempFile]);

            Assert.Single(toast.ShownMessages);
            Assert.Equal(ToastType.Success, toast.ShownTypes[0]);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private static DashboardModel CreateDashboard(
        IDocumentRepository? docRepo = null,
        IRecentFileRepository? recentRepo = null,
        ILocalizationService? loc = null,
        IProcessLauncherService? processLauncher = null)
    {
        return new DashboardModel(
            docRepo ?? new MockDocRepo([]),
            new MockRecycleRepo(),
            new MockCategoryRepo(),
            new MockCollectionRepo(),
            recentRepo ?? new MockRecentRepo([]),
            new MockDialog(),
            new MockFileDialog(),
            new MockCustomDialog(),
            new MockNavigation(),
            new MockClipboard(),
            processLauncher ?? new MockLauncher(),
            new MockExport(),
            new MockBackup(),
            new NoopArchiveService(),
            loc ?? new MockLoc(),
            null);
    }

    private sealed class MockToastService : IToastService
    {
        public List<string> ShownMessages { get; } = [];
        public List<ToastType> ShownTypes { get; } = [];
        public void Show(string message, ToastType type = ToastType.Info, int durationMs = 3000)
        {
            ShownMessages.Add(message);
            ShownTypes.Add(type);
        }
    }

    private sealed class MockDroppedImportService : IDroppedFileImportService
    {
        public List<string> GetAvailableSubjects(IReadOnlyList<string> defaults) => ["Math"];
        public List<string> GetAvailableTypes(IReadOnlyList<string> defaults) => ["PDF"];
        public StudyDocument BuildDocumentFromPath(string filePath) => new() { Name = Path.GetFileName(filePath), FilePath = filePath };
        public DocumentImportOutcome SaveDocument(StudyDocument document) => DocumentImportOutcome.Imported;
        public IReadOnlyList<StudyDocument> FindAmbiguousMatches(StudyDocument document) => [];
        public StudyDocument? FindExistingByFilePath(string filePath) => null;
    }

    private sealed class MockDocRepo(List<StudyDocument> documents) : IDocumentRepository
    {
        private readonly List<StudyDocument> _documents = documents;
        public List<StudyDocument> GetAll() => [.._documents];
        public StudyDocument? GetById(int id) => _documents.FirstOrDefault(d => d.Id == id);
        public List<StudyDocument> Search(string keyword) => [];
        public List<StudyDocument> Filter(string subject, string type) => [];
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => [.._documents];
        public List<StudyDocument> SearchAdvancedWithNotes(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => [.._documents];
        public List<StudyDocument> SearchAdvancedWithStatus(string? keyword, string? subject, string? type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant, string? status) => [.._documents];
        public bool Add(StudyDocument document) => true;
        public bool AddWithCatalogs(StudyDocument document) => true;
        public bool Update(StudyDocument document) => true;
        public bool Delete(int id) => true;
        public List<string> GetDistinctSubjects() => [];
        public List<string> GetDistinctTypes() => [];
        public List<string> GetDistinctTags() => [];
        public List<StudyDocument> GetUpcomingDeadlines(int days) => [];
        public List<StudyDocument> GetOverdueDocuments() => [];
        public List<StudyDocument> GetUncategorizedDocuments() => [];
        public List<StudyDocument> GetDocumentsWithMissingMetadata() => [];
        public int GetTotalCount() => _documents.Count;
        public (string? Category, string? Type, string? Tags) GetHiddenFields(int id) => (null, null, null);
        public bool UpdateHiddenFields(int id, string? category, string? type, string? tags) => true;
        public void EnsureSubjectExists(string subject) { }
        public void EnsureTypeExists(string type) { }
    }

    private sealed class MockRecentRepo(params (int Id, string Name, string? Subject, string? Type, string? FilePath, DateTime OpenedAt)[] items) : IRecentFileRepository
    {
        public List<(int Id, string Name, string? Subject, string? Type, string? FilePath, DateTime OpenedAt)> GetAll() => [..items];
        public bool Add(int documentId) => true;
        public void Clear() { }
    }

    private sealed class MockCategoryRepo : ICategoryRepository
    {
        public List<string> GetAllSubjects() => ["Math"];
        public List<string> GetAllTypes() => ["PDF"];
        public List<(string Name, int Count)> GetSubjectsWithCount() => [("Math", 1)];
        public List<(string Name, int Count)> GetTypesWithCount() => [("PDF", 1)];
        public bool AddSubject(string name) => true;
        public bool AddType(string name) => true;
        public bool UpdateSubjectName(string oldName, string newName) => true;
        public bool UpdateTypeName(string oldName, string newName) => true;
        public bool DeleteDocumentsBySubject(string subjectName) => true;
        public bool DeleteDocumentsByType(string typeName) => true;
        public int GetTotalDocumentCount() => 0;
    }

    private sealed class MockCollectionRepo : ICollectionRepository
    {
        public List<(int Id, string Name, string? Description, DateTime CreatedAt, int ItemCount)> GetAll() => [];
        public int Create(string name, string? description = null) => 1;
        public bool Update(int id, string name, string? description = null) => true;
        public bool Delete(int id) => true;
        public List<StudyDocument> GetDocuments(int collectionId) => [];
        public bool AddDocument(int collectionId, int documentId) => true;
        public bool RemoveDocument(int collectionId, int documentId) => true;
    }

    private sealed class MockRecycleRepo : IRecycleBinRepository
    {
        public List<StudyDocument> GetDeletedDocuments() => [];
        public int GetDeletedDocumentCount() => 0;
        public bool RestoreDocument(int id) => true;
        public bool PermanentDeleteDocument(int id) => true;
        public int EmptyRecycleBin() => 0;
    }

    private sealed class MockDialog : IDialogService
    {
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<bool> ShowConfirmAsync(string title, string message, string? okText = null, bool isDanger = false) => Task.FromResult(true);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
    }

    private sealed class MockFileDialog : IFileDialogService
    {
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
    }

    private sealed class MockCustomDialog : ICustomDialogService
    {
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowChangeCategoryAsync(string documentName, IList<string> existingCategories, string currentCategory) => Task.FromResult<string?>(null);
        public Task<AddDocumentDraft?> ShowAddDocumentAsync(string filePath, IList<string> subjects, IList<string> types) => Task.FromResult<AddDocumentDraft?>(new AddDocumentDraft { Name = Path.GetFileName(filePath), FilePath = filePath, Subject = "Math", Type = "PDF" });
        public Task<List<StudyDocument>?> ShowDocumentPickerAsync(string collectionName, IEnumerable<StudyDocument> allDocuments, IEnumerable<int> alreadyInCollection) => Task.FromResult<List<StudyDocument>?>(null);
        public Task<int> ShowSelectCollectionAsync(string documentName, IList<(int Id, string Name, int DocCount)> collections) => Task.FromResult(-1);
    }

    private sealed class MockNavigation : INavigationService
    {
        public bool CanGoBack => true;
        public void NavigateTo(string viewKey) { }
        public void NavigateTo(string viewKey, object? parameter) { }
        public void GoBack() { }
    }

    private sealed class MockClipboard : IClipboardService { public Task SetTextAsync(string text) => Task.CompletedTask; }
    private sealed class MockLauncher : IProcessLauncherService
    {
        public List<string> OpenedFiles { get; } = [];
        public void OpenFile(string filePath) => OpenedFiles.Add(filePath);
        public void OpenFolder(string folderPath) { }
        public void RevealInExplorer(string filePath) { }
        public void OpenUrl(string url) { }
    }
    private sealed class MockExport : IExportService { public Task<ExportResult> ExportCsvAsync(IReadOnlyList<StudyDocument> documents, string? suggestedFileName) => Task.FromResult(new ExportResult(false)); }
    private sealed class MockBackup : IBackupService
    {
        public Task<(bool Success, string? Path, string? Error)> BackupAsync(CancellationToken cancellationToken = default) => Task.FromResult((false, (string?)null, (string?)null));
        public Task<(bool Success, string? Error)> RestoreAsync(CancellationToken cancellationToken = default) => Task.FromResult((false, (string?)null));
        public Task<(bool Success, string? Path, string? Error)> BackupAsync() => Task.FromResult((false, (string?)null, (string?)null));
        public Task<(bool Success, string? Error)> RestoreAsync() => Task.FromResult((false, (string?)null));
    }

    private sealed class MockLifecycle : IApplicationLifecycleService
    {
        public void Shutdown(int exitCode = 0) { }
        public void Shutdown() { }
    }
    private sealed class MockSettings : ISettingsService
    {
        public string? GetSetting(string key) => null;
        public void SetSetting(string key, string? value) { }
        public string GetTheme() => "system";
        public void SetTheme(string theme) { }
        public string GetLanguage() => "ja";
        public void SetLanguage(string language) { }
    }
    private sealed class MockUpdateService : IUpdateService
    {
        public Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default) => Task.FromResult<UpdateInfo?>(null);
        public Task<UpdateInfo?> CheckForUpdateAsync() => Task.FromResult<UpdateInfo?>(null);
        public Task CheckSilentlyAsync() => Task.CompletedTask;
        public Task<string> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<double>? progress = null, CancellationToken cancellationToken = default) => Task.FromResult("");
        public Task HandleUpdateAsync(UpdateInfo updateInfo) => Task.CompletedTask;
        public void ApplyUpdateAndRestart(string installerPath) { }
    }

    private sealed class MockLoc : ILocalizationService
    {
        public string this[string key] => key switch
        {
            "Dashboard_GreetingMorning" => "Good morning",
            "Dashboard_GreetingAfternoon" => "Good afternoon",
            "Dashboard_GreetingEvening" => "Good evening",
            "Dashboard_GreetingSummary" => "{0} important, {1} due soon",
            "Dashboard_DragDropToastSingle" => "Added: {0}",
            "Dashboard_DragDropToastMultiple" => "Added {0} docs",
            _ => key
        };
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public IReadOnlyList<SupportedLanguage> AvailableLanguages => [SupportedLanguage.Japanese, SupportedLanguage.English];
        public event EventHandler? LanguageChanged;
        public void SetLanguage(SupportedLanguage language) => LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
