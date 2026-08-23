using StudyDocumentManager.Core;
using StudyDocumentManager.Core.DTOs;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public class SmartViewsModelTests : DatabaseTestBase
{
    private SavedSearchRepository CreateSavedSearchRepo() => new(Db);

    private SmartViewsModel CreateModel(
        ISavedSearchRepository? savedSearchRepo = null,
        RecordingDialogService? dialog = null,
        RecordingNavigationService? navigation = null)
    {
        return new SmartViewsModel(
            savedSearchRepo ?? CreateSavedSearchRepo(),
            new CategoryRepository(Db),
            dialog ?? new RecordingDialogService(),
            navigation ?? new RecordingNavigationService(),
            new StubLocalizationService());
    }

    private DashboardModel CreateDashboard(
        RecordingDialogService? dialog = null,
        RecordingNavigationService? navigation = null)
    {
        var repository = new DocumentRepository(Db);
        return new DashboardModel(
            repository,
            repository,
            new CategoryRepository(Db),
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

    private static SavedSearchCriteria BuildCriteria(
        string kind = SavedSearchKinds.Standard,
        string? keyword = null,
        bool important = false,
        int recentDays = 7,
        int deadlineDays = 7)
        => new()
        {
            Kind = kind,
            Keyword = keyword,
            IsImportant = important ? true : null,
            RecentDays = recentDays,
            DeadlineDays = deadlineDays
        };

    [Fact]
    public void Save_HappyPath_PersistsRowWithRoundtrippableCriteria()
    {
        var repo = CreateSavedSearchRepo();
        var model = CreateModel(repo);

        model.NewCommand.Execute(null);
        model.EditorName = "  Calculus set  ";
        model.EditorKeyword = "calculus";
        model.EditorSubject = "Math";
        model.EditorIsImportantOnly = true;
        model.SaveCommand.Execute(null);

        var all = repo.GetAll();
        Assert.Single(all);
        Assert.Equal("Calculus set", all[0].Name);

        var restored = SavedSearchCriteria.FromJson(repo.GetById(all[0].Id)!.CriteriaJson);
        Assert.NotNull(restored);
        Assert.Equal(SavedSearchKinds.Standard, restored!.Kind);
        Assert.Equal("calculus", restored.Keyword);
        Assert.Equal("Math", restored.Subject);
        Assert.True(restored.IsImportant);

        Assert.Single(model.SavedViews);
        Assert.False(model.IsEditing);
        Assert.Equal("SV_Saved", model.StatusText);
    }

    [Fact]
    public void Save_DuplicateName_BlockedWithStatusErrorAndNoNewRow()
    {
        var repo = CreateSavedSearchRepo();
        repo.Add(new SavedSearch { Name = "Work", CriteriaJson = BuildCriteria().ToJson(), CreatedAt = DateTime.Now });
        var model = CreateModel(repo);

        model.NewCommand.Execute(null);
        model.EditorName = "Work";
        model.SaveCommand.Execute(null);

        Assert.Single(repo.GetAll());
        Assert.Equal("SV_NameExists", model.StatusText);
        Assert.True(model.IsEditing);
    }

    [Fact]
    public void Duplicate_CreatesSuffixedCopy_PreservingCriteriaJson()
    {
        var repo = CreateSavedSearchRepo();
        var json = BuildCriteria(keyword: "physics").ToJson();
        var sourceId = repo.Add(new SavedSearch { Name = "Set", CriteriaJson = json, CreatedAt = DateTime.Now });
        var model = CreateModel(repo);
        model.SelectedSavedSearch = model.SavedViews.First(v => v.Id == sourceId);

        model.DuplicateCommand.Execute(null);

        Assert.Equal(2, repo.GetAll().Count);
        var copy = repo.GetAll().First(s => s.Id != sourceId);
        Assert.Equal("Set (2)", copy.Name);
        Assert.Equal(json, copy.CriteriaJson);
        Assert.Equal(2, model.SavedViews.Count);
    }

    [Fact]
    public async Task Delete_Confirmed_RemovesRowWithDangerConfirmation()
    {
        var repo = CreateSavedSearchRepo();
        var dialog = new RecordingDialogService { ConfirmResult = true };
        var id = repo.Add(new SavedSearch { Name = "Old", CriteriaJson = BuildCriteria().ToJson(), CreatedAt = DateTime.Now });
        var model = CreateModel(repo, dialog: dialog);
        model.SelectedSavedSearch = model.SavedViews.First(v => v.Id == id);

        await model.DeleteCommand.ExecuteAsync(null);

        Assert.Null(repo.GetById(id));
        Assert.Empty(model.SavedViews);
        Assert.Equal("SV_Deleted", model.StatusText);
        Assert.True(dialog.LastConfirmWasDanger);
    }

    [Fact]
    public async Task Delete_Dismissed_KeepsRow()
    {
        var repo = CreateSavedSearchRepo();
        var dialog = new RecordingDialogService { ConfirmResult = false };
        var id = repo.Add(new SavedSearch { Name = "Keep", CriteriaJson = BuildCriteria().ToJson(), CreatedAt = DateTime.Now });
        var model = CreateModel(repo, dialog: dialog);
        model.SelectedSavedSearch = model.SavedViews.First(v => v.Id == id);

        await model.DeleteCommand.ExecuteAsync(null);

        Assert.NotNull(repo.GetById(id));
        Assert.Single(model.SavedViews);
    }

    [Fact]
    public void Open_NavigatesToRunSmartViewWithSelectedId()
    {
        var repo = CreateSavedSearchRepo();
        var navigation = new RecordingNavigationService();
        var id = repo.Add(new SavedSearch { Name = "Run", CriteriaJson = BuildCriteria().ToJson(), CreatedAt = DateTime.Now });
        var model = CreateModel(repo, navigation: navigation);
        model.SelectedSavedSearch = model.SavedViews.First(v => v.Id == id);

        model.OpenCommand.Execute(null);

        Assert.Equal([("run-smartview", id)], navigation.RoutesWithParameter);
    }

    [Fact]
    public void ApplySavedSearch_Standard_StoresPendingDuringDeferredInit_AppliesOnInitialize()
    {
        Repo.Add(new StudyDocument { Name = "Alpha calculus", Subject = "Math", Type = "PDF" });
        Repo.Add(new StudyDocument { Name = "Beta physics", Subject = "Physics", Type = "PDF" });
        var dashboard = CreateDashboard();

        dashboard.ApplySavedSearch(BuildCriteria(keyword: "calculus"));

        dashboard.Initialize();

        Assert.Single(dashboard.Documents);
        Assert.Equal("Alpha calculus", dashboard.Documents[0].Name);
        Assert.Equal("calculus", dashboard.SearchKeyword);
    }

    [Fact]
    public void ApplySavedSearch_DueSoon_SetsUpcomingDeadlineList()
    {
        Repo.Add(new StudyDocument { Name = "Near", Deadline = DateTime.Now.AddDays(3) });
        Repo.Add(new StudyDocument { Name = "Far", Deadline = DateTime.Now.AddDays(30) });
        var dashboard = CreateDashboard();

        dashboard.ApplySavedSearch(BuildCriteria(kind: SavedSearchKinds.DueSoon, deadlineDays: 7));
        dashboard.Initialize();

        var names = dashboard.Documents.Select(d => d.Name).ToList();
        Assert.Contains("Near", names);
        Assert.DoesNotContain("Far", names);
        Assert.Equal("Status_UpcomingDeadlines", dashboard.StatusText);
    }

    [Fact]
    public void ApplySavedSearch_MissingFile_FiltersByDiskExistence_ClientSide()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sdm_sv_{Guid.NewGuid():N}.txt");
        File.WriteAllBytes(tempFile, [1, 2, 3]);
        try
        {
            Repo.Add(new StudyDocument { Name = "HasFile", FilePath = tempFile });
            Repo.Add(new StudyDocument { Name = "LostFile", FilePath = @"Z:\definitely_missing_svt.pdf" });
            Repo.Add(new StudyDocument { Name = "NoFilePath" });
            var dashboard = CreateDashboard();

            dashboard.ApplySavedSearch(BuildCriteria(kind: SavedSearchKinds.MissingFile));
            dashboard.Initialize();

            var names = dashboard.Documents.Select(d => d.Name).ToList();
            Assert.Single(names);
            Assert.Contains("LostFile", names);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ApplySavedSearch_RecentlyAdded_EnablesDateFilterWithWindow()
    {
        Repo.Add(new StudyDocument { Name = "Fresh one" });
        Repo.Add(new StudyDocument { Name = "Fresh two" });
        var dashboard = CreateDashboard();

        dashboard.ApplySavedSearch(BuildCriteria(kind: SavedSearchKinds.RecentlyAdded, recentDays: 7));
        dashboard.Initialize();

        Assert.True(dashboard.IsDateFilterEnabled);
        Assert.NotNull(dashboard.FilterFromDate);
        var from = dashboard.FilterFromDate!.Value.DateTime;
        Assert.True(from <= DateTime.Now.AddDays(-6));
        Assert.True(from >= DateTime.Now.AddDays(-8));
        Assert.Equal(2, dashboard.Documents.Count);
    }

    [Fact]
    public void FromJson_InvalidJson_ReturnsNull()
    {
        Assert.Null(SavedSearchCriteria.FromJson("{not valid json"));
    }

    private sealed class RecordingDialogService : IDialogService
    {
        public bool ConfirmResult { get; set; }
        public bool LastConfirmWasDanger { get; private set; }

        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => ShowConfirmCore(false);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
            => ShowConfirmCore(isDanger);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "")
            => Task.FromResult<string?>(null);

        private Task<bool> ShowConfirmCore(bool isDanger)
        {
            LastConfirmWasDanger = isDanger;
            return Task.FromResult(ConfirmResult);
        }
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
