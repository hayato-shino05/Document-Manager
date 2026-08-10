using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Models.Items;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class Task5DashboardRecentFilesTests
{
    [Fact]
    public void Dashboard_FilterWithNoResults_ResetsVisibleStatsAndStatus()
    {
        var repository = new Task5DocumentRepository(
            [new StudyDocument { Id = 1, Name = "Math", Subject = "Math", IsImportant = true }]);
        var model = CreateDashboard(repository);
        model.Initialize();

        model.SearchKeyword = "not found";
        model.SearchCommand.Execute(null);

        Assert.Empty(model.Documents);
        Assert.Equal(0, model.TotalDocuments);
        Assert.Equal(0, model.ImportantDocuments);
        Assert.Contains("0", model.StatusText);
    }

    [Fact]
    public void Dashboard_LoadFailure_CanRetryAfterRepositoryRecovers()
    {
        var repository = new Task5DocumentRepository([new StudyDocument { Id = 1, Name = "Retry" }])
        {
            ThrowOnNextGetAll = true
        };
        var model = CreateDashboard(repository);

        model.Initialize();

        Assert.True(model.HasLoadError);
        Assert.False(model.IsLoading);
        Assert.Equal("load-error", model.StateMessage);

        model.RefreshCommand.Execute(null);

        Assert.False(model.HasLoadError);
        Assert.Single(model.Documents);
        Assert.Contains("1", model.StatusText);
    }

    [Fact]
    public void Dashboard_CollectionFilter_UpdatesStatsAndEmptyState()
    {
        var repository = new Task5DocumentRepository([new StudyDocument { Id = 1, Name = "All" }]);
        var collections = new Task5CollectionRepository
        {
            CollectionDocuments = [new StudyDocument { Id = 2, Name = "Collected", IsImportant = true, FilePath = "" }]
        };
        var model = CreateDashboard(repository, collections);
        model.Initialize();

        model.FilterByCategoryCommand.Execute(new CategoryTreeItem { FilterType = "collection", FilterValue = "41" });

        Assert.Single(model.Documents);
        Assert.Equal(1, model.TotalDocuments);
        Assert.Equal(1, model.ImportantDocuments);
        Assert.Equal(1, model.NoFileDocuments);
        Assert.False(model.IsEmptyState);
        Assert.Contains("1", model.StatusText);

        collections.CollectionDocuments = [];
        model.FilterByCategoryCommand.Execute(new CategoryTreeItem { FilterType = "collection", FilterValue = "41" });

        Assert.Empty(model.Documents);
        Assert.Equal(0, model.TotalDocuments);
        Assert.Equal(0, model.ImportantDocuments);
        Assert.True(model.IsEmptyState);
        Assert.Equal("empty", model.StateMessage);
        Assert.Contains("0", model.StatusText);
    }

    [Fact]
    public void RecentFiles_ExposesHistoryAvailability()
    {
        var withHistory = CreateRecent(
            new Task5RecentRepository((7, "Notes", "Math", "PDF", null, DateTime.Now)),
            new Task5Launcher());
        var withoutHistory = CreateRecent(new Task5RecentRepository(), new Task5Launcher());

        Assert.True(withHistory.HasRecentFiles);
        Assert.False(withoutHistory.HasRecentFiles);
    }

    [Fact]
    public void RecentFiles_ExistingFile_LaunchesThenAddsHistory()
    {
        var path = Path.GetTempFileName();
        try
        {
            var recent = new Task5RecentRepository((7, "Notes", "Math", "PDF", path, DateTime.Now));
            var launcher = new Task5Launcher();
            var model = CreateRecent(recent, launcher);
            var item = Assert.Single(model.RecentFiles);

            model.OpenFileCommand.Execute(item);

            Assert.Equal([path], launcher.OpenedFiles);
            Assert.Equal([7], recent.AddedIds);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RecentFiles_MissingFile_DoesNotAddHistoryOrLaunch()
    {
        var itemPath = Path.Combine(Path.GetTempPath(), $"task5_missing_{Guid.NewGuid():N}.pdf");
        var recent = new Task5RecentRepository((8, "Missing", "Math", "PDF", itemPath, DateTime.Now));
        var launcher = new Task5Launcher();
        var model = CreateRecent(recent, launcher);

        model.OpenFileCommand.Execute(Assert.Single(model.RecentFiles));

        Assert.Empty(launcher.OpenedFiles);
        Assert.Empty(recent.AddedIds);
    }

    [Fact]
    public void RecentFiles_LauncherFailure_DoesNotAddHistory()
    {
        var path = Path.GetTempFileName();
        try
        {
            var recent = new Task5RecentRepository((9, "Failure", "Math", "PDF", path, DateTime.Now));
            var launcher = new Task5Launcher { ThrowOnOpen = true };
            var model = CreateRecent(recent, launcher);

            model.OpenFileCommand.Execute(Assert.Single(model.RecentFiles));

            Assert.Empty(recent.AddedIds);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RecentFiles_InvalidItem_DoesNotAddHistory()
    {
        var recent = new Task5RecentRepository((10, "Invalid", "Math", "PDF", null, DateTime.Now));
        var launcher = new Task5Launcher();
        var model = CreateRecent(recent, launcher);

        model.OpenFileCommand.Execute(Assert.Single(model.RecentFiles));

        Assert.Empty(recent.AddedIds);
        Assert.Empty(launcher.OpenedFiles);
    }

    [Fact]
    public void RecentFiles_ViewBindsOpenActionToItemCommand()
    {
        var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "StudyDocumentManager", "Views", "RecentFiles.axaml"));

        Assert.Contains("OpenFileCommand", xaml);
        Assert.Contains("CommandParameter=\"{Binding}\"", xaml);
    }

    private static DashboardModel CreateDashboard(Task5DocumentRepository repository, Task5CollectionRepository? collections = null) => new(
        repository,
        new Task5RecycleRepository(),
        new Task5CategoryRepository(),
        collections ?? new Task5CollectionRepository(),
        new Task5RecentRepository(),
        new Task5Dialog(),
        new Task5FileDialog(),
        new Task5CustomDialog(),
        new Task5Navigation(),
        new Task5Clipboard(),
        new Task5Launcher(),
        new Task5Export(),
        new Task5Backup(),
        new Task5Localization());

    private static RecentFilesModel CreateRecent(Task5RecentRepository recent, Task5Launcher launcher) => new(
        new Task5Dialog(), new Task5Navigation(), recent, launcher, new Task5Localization());

    private sealed class Task5DocumentRepository(List<StudyDocument> documents) : IDocumentRepository
    {
        private readonly List<StudyDocument> _documents = documents;
        public bool ThrowOnNextGetAll { get; set; }
        public List<StudyDocument> GetAll() { if (ThrowOnNextGetAll) { ThrowOnNextGetAll = false; throw new IOException("load failed"); } return [.._documents]; }
        public StudyDocument? GetById(int id) => _documents.FirstOrDefault(d => d.Id == id);
        public List<StudyDocument> Search(string keyword) => [];
        public List<StudyDocument> Filter(string subject, string type) => [];
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => keyword == "not found" ? [] : [.._documents];
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

    private sealed class Task5RecentRepository(params (int Id, string Name, string? Subject, string? Type, string? FilePath, DateTime OpenedAt)[] items) : IRecentFileRepository
    {
        public List<int> AddedIds { get; } = [];
        public List<(int Id, string Name, string? Subject, string? Type, string? FilePath, DateTime OpenedAt)> GetAll() => [..items];
        public bool Add(int documentId) { AddedIds.Add(documentId); return true; }
        public void Clear() { }
    }

    private sealed class Task5Launcher : IProcessLauncherService
    {
        public bool ThrowOnOpen { get; set; }
        public List<string> OpenedFiles { get; } = [];
        public void OpenFile(string filePath) { if (ThrowOnOpen) throw new IOException("launch failed"); OpenedFiles.Add(filePath); }
        public void OpenFolderAndSelect(string filePath) { }
        public void RevealInExplorer(string filePath) { }
        public void OpenUrl(string url) { }
    }

    private sealed class Task5Localization : ILocalizationService
    {
        public string this[string key] => key switch
        {
            "Status_TotalSummary" => "total:{0}/{1}/{2}",
            "Dashboard_EmptyState" => "empty",
            "Dashboard_LoadError" => "load-error",
            _ => key
        };
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public void SetLanguage(SupportedLanguage language) { }
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [];
        public event EventHandler? LanguageChanged { add { } remove { } }
    }

    private sealed class Task5Dialog : IDialogService
    {
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(false);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(false);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
    }

    private sealed class Task5RecycleRepository : IRecycleBinRepository
    {
        public List<StudyDocument> GetDeletedDocuments() => [];
        public bool RestoreDocument(int id) => false;
        public bool PermanentDeleteDocument(int id) => false;
        public int EmptyRecycleBin() => 0;
        public int GetDeletedDocumentCount() => 0;
    }

    private sealed class Task5CategoryRepository : ICategoryRepository
    {
        public List<string> GetAllSubjects() => [];
        public List<string> GetAllTypes() => [];
        public List<(string Name, int Count)> GetSubjectsWithCount() => [];
        public List<(string Name, int Count)> GetTypesWithCount() => [];
        public bool AddSubject(string name) => true;
        public bool AddType(string name) => true;
        public bool UpdateSubjectName(string oldName, string newName) => true;
        public bool UpdateTypeName(string oldName, string newName) => true;
        public bool DeleteDocumentsBySubject(string subjectName) => true;
        public bool DeleteDocumentsByType(string typeName) => true;
        public int GetTotalDocumentCount() => 0;
    }

    private sealed class Task5CollectionRepository : ICollectionRepository
    {
        public List<StudyDocument> CollectionDocuments { get; set; } = [];
        public List<(int Id, string Name, string? Description, DateTime CreatedAt, int ItemCount)> GetAll() => [];
        public int Create(string name, string? description = null) => 1;
        public bool Update(int id, string name, string? description = null) => true;
        public bool Delete(int id) => true;
        public List<StudyDocument> GetDocuments(int collectionId) => [..CollectionDocuments];
        public bool AddDocument(int collectionId, int documentId) => true;
        public bool RemoveDocument(int collectionId, int documentId) => true;
    }

    private sealed class Task5FileDialog : IFileDialogService
    {
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
    }

    private sealed class Task5CustomDialog : ICustomDialogService
    {
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowChangeCategoryAsync(string documentName, IList<string> existingCategories, string currentCategory) => Task.FromResult<string?>(null);
        public Task<StudyDocumentManager.Core.DTOs.AddDocumentDraft?> ShowAddDocumentAsync(string filePath, IList<string> subjects, IList<string> types) => Task.FromResult<StudyDocumentManager.Core.DTOs.AddDocumentDraft?>(null);
        public Task<List<StudyDocument>?> ShowDocumentPickerAsync(string collectionName, IEnumerable<StudyDocument> allDocuments, IEnumerable<int> alreadyInCollection) => Task.FromResult<List<StudyDocument>?>(null);
        public Task<int> ShowSelectCollectionAsync(string documentName, IList<(int Id, string Name, int DocCount)> collections) => Task.FromResult(-1);
    }

    private sealed class Task5Navigation : INavigationService
    {
        public bool CanGoBack => true;
        public void NavigateTo(string viewKey) { }
        public void NavigateTo(string viewKey, object? parameter) { }
        public void GoBack() { }
    }

    private sealed class Task5Clipboard : IClipboardService { public Task SetTextAsync(string text) => Task.CompletedTask; }
    private sealed class Task5Export : IExportService { public Task<ExportResult> ExportCsvAsync(IReadOnlyList<StudyDocument> documents, string? suggestedFileName) => Task.FromResult(new ExportResult(false)); }
    private sealed class Task5Backup : IBackupService
    {
        public Task<(bool Success, string? Path, string? Error)> BackupAsync() => Task.FromResult((false, (string?)null, (string?)null));
        public Task<(bool Success, string? Error)> RestoreAsync() => Task.FromResult((false, (string?)null));
    }
}
