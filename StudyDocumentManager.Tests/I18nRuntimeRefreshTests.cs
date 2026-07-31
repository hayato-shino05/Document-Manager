using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public class I18nRuntimeRefreshTests
{
    [Fact]
    public async Task FileIntegrityCheckModel_LanguageChange_RefreshesStatusTextAndResultStatuses()
    {
        var localization = new ToggleLocalizationService();
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.pdf");
        var document = new StudyDocument { Id = 5, Name = "Missing file", FilePath = missingPath };
        var model = new FileIntegrityCheckModel(
            new DocumentRepositoryStub(document),
            new FileIntegrityRepositoryStub(),
            new DialogServiceStub(),
            new FileDialogServiceStub(),
            localization);

        model.CheckIntegrityCommand.Execute(null);
        await model.CheckIntegrityCommand.ExecutionTask!;

        Assert.Equal("scan-complete:1/1", model.StatusText);
        Assert.Single(model.Results);
        Assert.Equal("missing-jp", model.Results[0].Status);

        localization.SetLanguage(SupportedLanguage.English);

        Assert.Equal("scan-complete-en:1/1", model.StatusText);
        Assert.Equal("missing-en", model.Results[0].Status);
    }

    [Fact]
    public void RecentFilesModel_LanguageChange_ReloadsStatusLabels()
    {
        var localization = new ToggleLocalizationService();
        var missingPath = Path.Combine(Path.GetTempPath(), $"recent_missing_{Guid.NewGuid():N}.pdf");
        var model = new RecentFilesModel(
            new DialogServiceStub(),
            new NavigationServiceStub(),
            new RecentFileRepositoryStub((7, "Notes", "Math", "PDF", missingPath, new DateTime(2026, 7, 30, 9, 0, 0))),
            new ProcessLauncherStub(),
            localization);

        var item = Assert.Single(model.RecentFiles);
        Assert.Equal("missing-jp", item.StatusDisplay);

        localization.SetLanguage(SupportedLanguage.English);

        item = Assert.Single(model.RecentFiles);
        Assert.Equal("missing-en", item.StatusDisplay);
    }

    [Fact]
    public void RelatedDocumentsModel_LanguageChange_LocalizesRelationLabels()
    {
        var localization = new ToggleLocalizationService();
        var relatedDocument = new StudyDocument { Id = 2, Name = "Reference", Subject = "Math" };
        var model = new RelatedDocumentsModel(
            new DocumentRepositoryStub(new StudyDocument { Id = 1, Name = "Main" }, relatedDocument),
            new RelatedDocumentRepositoryStub((relatedDocument, 17, "reference")),
            new DialogServiceStub(),
            new NavigationServiceStub(),
            localization);

        model.Load(1, "Main");

        Assert.Equal("reference-jp", Assert.Single(model.RelatedDocuments).RelationType);
        Assert.Contains("related-jp", model.RelationTypes.Select(option => option.ToString()));
        Assert.Contains("reference-jp", model.RelationTypes.Select(option => option.ToString()));

        localization.SetLanguage(SupportedLanguage.English);

        Assert.Equal("reference-en", Assert.Single(model.RelatedDocuments).RelationType);
        Assert.Contains("related-en", model.RelationTypes.Select(option => option.ToString()));
        Assert.Contains("reference-en", model.RelationTypes.Select(option => option.ToString()));
    }

    private sealed class ToggleLocalizationService : ILocalizationService
    {
        private SupportedLanguage _currentLanguage = SupportedLanguage.Japanese;

        public string this[string key] => (_currentLanguage, key) switch
        {
            (SupportedLanguage.Japanese, "Status_ScanPrompt") => "scan-prompt",
            (SupportedLanguage.Japanese, "Status_ScanComplete") => "scan-complete:{0}/{1}",
            (SupportedLanguage.Japanese, "Status_MissingFiles") => "missing-count:{0}",
            (SupportedLanguage.Japanese, "Integrity_FileNotExist") => "missing-jp",
            (SupportedLanguage.Japanese, "Dialog_Result") => "result",
            (SupportedLanguage.Japanese, "Integrity_AllFilesOk") => "ok:{0}",
            (SupportedLanguage.Japanese, "Recent_FileExists") => "exists-jp",
            (SupportedLanguage.Japanese, "Recent_FileMissing") => "missing-jp",
            (SupportedLanguage.English, "Status_ScanPrompt") => "scan-prompt-en",
            (SupportedLanguage.English, "Status_ScanComplete") => "scan-complete-en:{0}/{1}",
            (SupportedLanguage.English, "Status_MissingFiles") => "missing-count-en:{0}",
            (SupportedLanguage.English, "Integrity_FileNotExist") => "missing-en",
            (SupportedLanguage.English, "Dialog_Result") => "result-en",
            (SupportedLanguage.English, "Integrity_AllFilesOk") => "ok-en:{0}",
            (SupportedLanguage.English, "Recent_FileExists") => "exists-en",
            (SupportedLanguage.English, "Recent_FileMissing") => "missing-en",
            (SupportedLanguage.Japanese, "RelatedDocs_RelationType_related") => "related-jp",
            (SupportedLanguage.Japanese, "RelatedDocs_RelationType_reference") => "reference-jp",
            (SupportedLanguage.Japanese, "RelatedDocs_RelationType_supplement") => "supplement-jp",
            (SupportedLanguage.Japanese, "RelatedDocs_RelationType_prerequisite") => "prerequisite-jp",
            (SupportedLanguage.Japanese, "RelatedDocs_RelationType_sequel") => "sequel-jp",
            (SupportedLanguage.English, "RelatedDocs_RelationType_related") => "related-en",
            (SupportedLanguage.English, "RelatedDocs_RelationType_reference") => "reference-en",
            (SupportedLanguage.English, "RelatedDocs_RelationType_supplement") => "supplement-en",
            (SupportedLanguage.English, "RelatedDocs_RelationType_prerequisite") => "prerequisite-en",
            (SupportedLanguage.English, "RelatedDocs_RelationType_sequel") => "sequel-en",
            _ => key
        };

        public SupportedLanguage CurrentLanguage => _currentLanguage;
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = Enum.GetValues<SupportedLanguage>();
        public event EventHandler? LanguageChanged;

        public void SetLanguage(SupportedLanguage language)
        {
            if (_currentLanguage == language)
                return;

            _currentLanguage = language;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class DocumentRepositoryStub(params StudyDocument[] documents) : IDocumentRepository
    {
        public List<StudyDocument> GetAll() => [..documents];
        public StudyDocument? GetById(int id) => documents.FirstOrDefault(document => document.Id == id);
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

    private sealed class RelatedDocumentRepositoryStub((StudyDocument Doc, int RelationId, string RelationType) relation) : IRelatedDocumentRepository
    {
        public List<(StudyDocument Doc, int RelationId, string RelationType)> GetRelated(int docId) => [relation];
        public void AddRelation(int docId1, int docId2, string relationType = "related") { }
        public void RemoveRelation(int relationId) { }
    }

    private sealed class FileIntegrityRepositoryStub : IFileIntegrityRepository
    {
        public string DatabasePath => string.Empty;
        public bool UpdateDocumentPath(int id, string newPath) => true;
        public bool ClearDocumentPath(int id) => true;
        public bool BackupDatabase(string destPath, bool overwrite = false) => false;
        public bool CanRestoreDatabase(string sourcePath) => false;
        public bool RestoreDatabase(string sourcePath) => false;
    }

    private sealed class RecentFileRepositoryStub((int Id, string Name, string? Subject, string? Type, string? FilePath, DateTime OpenedAt) item) : IRecentFileRepository
    {
        public List<(int Id, string Name, string? Subject, string? Type, string? FilePath, DateTime OpenedAt)> GetAll() => [item];
        public bool Add(int documentId) => true;
        public void Clear() { }
    }

    private sealed class DialogServiceStub : IDialogService
    {
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(true);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
    }

    private sealed class FileDialogServiceStub : IFileDialogService
    {
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
    }

    private sealed class NavigationServiceStub : INavigationService
    {
        public bool CanGoBack => true;
        public void NavigateTo(string viewKey) { }
        public void NavigateTo(string viewKey, object? parameter) { }
        public void GoBack() { }
    }

    private sealed class ProcessLauncherStub : IProcessLauncherService
    {
        public void OpenFile(string filePath) { }
        public void RevealInExplorer(string filePath) { }
        public void OpenUrl(string url) { }
    }
}
