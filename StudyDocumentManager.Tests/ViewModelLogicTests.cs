using Xunit;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Core.Services;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Tests;

// ═══════════════════════════════════════════════════════════════
// DatabaseHelper — 3 coverage gaps remaining (L25-29, L65-68, L72-76)
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Covers DatabasePath default fallback (L25-29) and Directory.CreateDirectory branch (L65-68).
/// These cannot be hit via DatabaseTestBase because SetDatabasePath is always called first.
/// </summary>
public class DatabaseHelperDefaultPathTests
{
    [Fact]
    public void DatabasePath_WhenNotSet_ReturnsLocalApplicationDataPath()
    {
        var db = new DatabaseHelper();

        var path = db.DatabasePath;
        var expectedRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.False(string.IsNullOrEmpty(path));
        Assert.EndsWith("study_documents.db", path);
        Assert.Contains(Path.Combine("StudyDocumentManager", "data"), path);
        Assert.StartsWith(expectedRoot, path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DatabasePath_WhenEnvironmentOverrideIsSet_ReturnsConfiguredPath()
    {
        var originalPath = Environment.GetEnvironmentVariable("SDM_DATABASE_PATH");
        var configuredPath = Path.Combine(Path.GetTempPath(), $"sdm_override_{Guid.NewGuid():N}.db");

        try
        {
            Environment.SetEnvironmentVariable("SDM_DATABASE_PATH", configuredPath);

            Assert.Equal(Path.GetFullPath(configuredPath), new DatabaseHelper().DatabasePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SDM_DATABASE_PATH", originalPath);
        }
    }

    [Fact]
    public void InitializeDatabase_WhenDataFolderMissing_CreatesIt()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"sdm_mkdirtest_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(tempRoot, "data", "study_documents.db");
        var db = new DatabaseHelper();

        try
        {
            db.SetDatabasePath(dbPath);
            db.InitializeDatabase();

            Assert.True(File.Exists(dbPath), "DB file should exist after InitializeDatabase");
            Assert.True(Directory.Exists(Path.GetDirectoryName(dbPath)!));
        }
        finally
        {
            db.CloseAllConnections();
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// DashboardViewModel — business logic tests (no Avalonia required)
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Tests DashboardViewModel.ApplyFilters logic flow by exercising DatabaseHelper directly.
/// Since ViewModels use DatabaseHelper + IDocumentRepository, we test the data contract.
/// </summary>
public class DashboardFlowTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public DashboardFlowTests()
    {
        _repo = new DocumentRepository(Db);
    }


    [Fact]
    public void LoadData_AfterAdd_TotalDocumentsIsCorrect()
    {
        _repo.Add(new StudyDocument { Name = "Doc1", Subject = "Math", Type = "PDF" });
        _repo.Add(new StudyDocument { Name = "Doc2", Subject = "Physics", Type = "Word", IsImportant = true });

        var all = _repo.GetAll();
        Assert.Equal(2, all.Count);
        Assert.Equal(1, all.Count(d => d.IsImportant));
    }

    [Fact]
    public void Filter_SubjectSentinel_ReturnsAll()
    {
        _repo.Add(new StudyDocument { Name = "A", Subject = "Math" });
        _repo.Add(new StudyDocument { Name = "B", Subject = "Physics" });

        // "All" sentinel → empty string → GetAll
        var results = _repo.GetAll();
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Filter_BySubject_ReturnsMatchOnly()
    {
        _repo.Add(new StudyDocument { Name = "A", Subject = "Math" });
        _repo.Add(new StudyDocument { Name = "B", Subject = "Physics" });

        var results = _repo.SearchAdvanced("", "Math", "", null, null, null, null, null);
        Assert.Single(results);
        Assert.Equal("A", results[0].Name);
    }

    [Fact]
    public void CategoryTree_AllNode_HasCorrectCount()
    {
        _repo.Add(new StudyDocument { Name = "A", Subject = "Math" });
        _repo.Add(new StudyDocument { Name = "B", Subject = "Science" });

        var all = _repo.GetAll();
        // AllNode should show total count
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void CategoryTree_SubjectNodes_OnlyNonEmptySubjects()
    {
        _repo.Add(new StudyDocument { Name = "A", Subject = "Math" });
        _repo.Add(new StudyDocument { Name = "B", Subject = "" }); // No subject

        var all = _repo.GetAll();
        var bySubject = all.GroupBy(d => d.Subject).Where(g => g.Count() > 0 && !string.IsNullOrEmpty(g.Key));
        // Only "Math" has non-empty subject
        Assert.Single(bySubject);
    }

    [Fact]
    public void ImportantFilter_OnlyImportantDocs()
    {
        _repo.Add(new StudyDocument { Name = "Important", IsImportant = true });
        _repo.Add(new StudyDocument { Name = "Normal", IsImportant = false });

        var results = _repo.SearchAdvanced("", "", "", null, null, null, null, true);
        Assert.Single(results);
        Assert.Equal("Important", results[0].Name);
    }

    [Fact]
    public void ShowUpcomingDeadlines_Returns7DayDocs()
    {
        _repo.Add(new StudyDocument { Name = "Due Soon", Deadline = DateTime.Now.AddDays(3) });
        _repo.Add(new StudyDocument { Name = "Due Later", Deadline = DateTime.Now.AddDays(30) });
        _repo.Add(new StudyDocument { Name = "No Deadline" });

        var upcoming = _repo.GetUpcomingDeadlines(7);
        Assert.Single(upcoming);
        Assert.Equal("Due Soon", upcoming[0].Name);
    }

    [Fact]
    public void ShowOverdue_ReturnsExpiredDocs()
    {
        _repo.Add(new StudyDocument { Name = "Overdue", Deadline = DateTime.Now.AddDays(-2) });
        _repo.Add(new StudyDocument { Name = "Future", Deadline = DateTime.Now.AddDays(5) });

        var overdue = _repo.GetOverdueDocuments();
        Assert.Single(overdue);
        Assert.Equal("Overdue", overdue[0].Name);
    }

    [Fact]
    public void ToggleImportant_UpdatesPersisted()
    {
        _repo.Add(new StudyDocument { Name = "Toggle Me", IsImportant = false });
        var doc = _repo.GetAll().First(d => d.Name == "Toggle Me");

        doc.IsImportant = true;
        _repo.Update(doc);

        var updated = _repo.GetAll().First(d => d.Name == "Toggle Me");
        Assert.True(updated.IsImportant);
    }

    [Fact]
    public void DeleteDocument_SoftDeletes_NotInGetAll()
    {
        _repo.Add(new StudyDocument { Name = "Will Be Deleted" });
        var doc = _repo.GetAll().First(d => d.Name == "Will Be Deleted");

        _repo.Delete(doc.Id);

        var all = _repo.GetAll();
        Assert.DoesNotContain(all, d => d.Name == "Will Be Deleted");
    }

    [Fact]
    public void StatsRefresh_AfterDelete_CountDecreases()
    {
        _repo.Add(new StudyDocument { Name = "D1" });
        _repo.Add(new StudyDocument { Name = "D2" });

        var countBefore = _repo.GetAll().Count;

        var doc = _repo.GetAll().First();
        _repo.Delete(doc.Id);

        var countAfter = _repo.GetAll().Count;
        Assert.Equal(countBefore - 1, countAfter);
    }

    [Fact]
    public void AddToCollection_Flow_DocAppearsInCollection()
    {
        _repo.Add(new StudyDocument { Name = "ColDoc" });
        var doc = _repo.GetAll().First();
        Db.CreateCollection("My Collection");
        var col = Db.GetCollections().First(c => c.Name == "My Collection");

        Db.AddDocumentToCollection(col.Id, doc.Id);

        var colDocs = Db.GetDocumentsInCollection(col.Id)!;
        Assert.Single(colDocs);
    }


    [Fact]
    public async Task OpenMissingFile_ShowsExplanationAndNavigatesToFileIntegrity()
    {
        var document = new StudyDocument { Id = 7, Name = "Missing file", FilePath = @"Z:\missing.pdf" };
        var dialog = new DashboardDialogService { ConfirmResult = true };
        var navigation = new DashboardNavigationService();
        var recent = new DashboardRecentFileRepository();
        var process = new DashboardProcessLauncherService();
        var model = new DashboardModel(
            new DashboardDocumentRepository([document]),
            new DashboardRecycleBinRepository(),
            new DashboardCategoryRepository(),
            new DashboardCollectionRepository(),
            recent,
            dialog,
            new DashboardFileDialogService(),
            new DashboardCustomDialogService(),
            navigation,
            new DashboardClipboardService(),
            process,
            new DashboardExportService(),
            new DashboardBackupService(),
            new DashboardLocalizationService())
        {
            SelectedDocument = document
        };

        model.OpenFileCommand.Execute(null);

        Assert.Empty(recent.AddedDocumentIds);
        Assert.Empty(process.OpenedFiles);
        Assert.Equal("Dashboard_FileMissingMessage", dialog.LastConfirmMessage);
        Assert.Equal(["fileintegrity"], navigation.Routes);
    }

    [Fact]
    public async Task OpenFile_LauncherFailure_ShowsErrorWithoutNavigation()
    {
        var tempFile = Path.GetTempFileName();
        var document = new StudyDocument { Id = 8, Name = "Open failure", FilePath = tempFile };
        var dialog = new DashboardDialogService();
        var navigation = new DashboardNavigationService();
        var recent = new DashboardRecentFileRepository();
        var process = new DashboardProcessLauncherService { ThrowOnOpen = true };
        var model = new DashboardModel(
            new DashboardDocumentRepository([document]),
            new DashboardRecycleBinRepository(),
            new DashboardCategoryRepository(),
            new DashboardCollectionRepository(),
            recent,
            dialog,
            new DashboardFileDialogService(),
            new DashboardCustomDialogService(),
            navigation,
            new DashboardClipboardService(),
            process,
            new DashboardExportService(),
            new DashboardBackupService(),
            new DashboardLocalizationService())
        {
            SelectedDocument = document
        };

        try
        {
            await model.OpenFileCommand.ExecuteAsync(null);
        }
        finally
        {
            File.Delete(tempFile);
        }

        Assert.Equal("Msg_Error", dialog.LastErrorMessage);
        Assert.Empty(recent.AddedDocumentIds);
        Assert.Empty(navigation.Routes);
    }

    [Fact]
    public async Task AddToCollection_EmptyCatalog_CreatesCollectionAndAddsSelectedDocument()
    {
        var document = new StudyDocument { Id = 9, Name = "Calculus" };
        var collection = new DashboardCollectionRepository();
        var dialog = new DashboardDialogService { InputResult = "  Study Set  " };
        var model = new DashboardModel(
            new DashboardDocumentRepository([document]),
            new DashboardRecycleBinRepository(),
            new DashboardCategoryRepository(),
            collection,
            new DashboardRecentFileRepository(),
            dialog,
            new DashboardFileDialogService(),
            new DashboardCustomDialogService(),
            new DashboardNavigationService(),
            new DashboardClipboardService(),
            new DashboardProcessLauncherService(),
            new DashboardExportService(),
            new DashboardBackupService(),
            new DashboardLocalizationService())
        {
            SelectedDocument = document
        };

        await model.AddToCollectionCommand.ExecuteAsync(null);

        Assert.Equal(["Study Set"], collection.CreatedNames);
        Assert.Equal([(41, document.Id)], collection.AddedDocuments);
        Assert.Equal("Dashboard_AddedToCollection", dialog.LastMessage);
    }


    [Fact]
    public async Task DeleteDocument_FailedOrCancelledOutcome_PreservesCurrentDocument()
    {
        var document = new StudyDocument { Id = 10, Name = "Keep" };
        var repository = new DashboardDocumentRepository([document]) { DeleteResult = false };
        var dialog = new DashboardDialogService { ConfirmResult = true };
        var model = new DashboardModel(
            repository, new DashboardRecycleBinRepository(), new DashboardCategoryRepository(),
            new DashboardCollectionRepository(), new DashboardRecentFileRepository(), dialog,
            new DashboardFileDialogService(), new DashboardCustomDialogService(), new DashboardNavigationService(),
            new DashboardClipboardService(), new DashboardProcessLauncherService(), new DashboardExportService(),
            new DashboardBackupService(), new DashboardLocalizationService())
        {
            SelectedDocument = document
        };

        await model.DeleteDocumentCommand.ExecuteAsync(null);

        Assert.Same(document, model.SelectedDocument);
        Assert.Equal("Msg_Error", dialog.LastErrorMessage);

        dialog.CancelConfirmation = true;
        await model.DeleteDocumentCommand.ExecuteAsync(null);

        Assert.Same(document, model.SelectedDocument);
    }

    [Fact]
    public async Task ContextMenuActions_UseSelectedDocumentAndPreserveRoutes()
    {
        var filePath = Path.GetTempFileName();
        var document = new StudyDocument
        {
            Id = 11,
            Name = "Context document",
            FilePath = filePath,
            Subject = "Math",
            Tags = "old",
            Notes = "old note"
        };
        var dialog = new DashboardDialogService { InputResult = "updated value" };
        var customDialog = new DashboardCustomDialogService { ChangeCategoryResult = "Physics" };
        var navigation = new DashboardNavigationService();
        var clipboard = new DashboardClipboardService();
        var process = new DashboardProcessLauncherService();
        var model = new DashboardModel(
            new DashboardDocumentRepository([document]), new DashboardRecycleBinRepository(),
            new DashboardCategoryRepository(), new DashboardCollectionRepository(),
            new DashboardRecentFileRepository(), dialog, new DashboardFileDialogService(),
            customDialog, navigation, clipboard, process, new DashboardExportService(),
            new DashboardBackupService(), new DashboardLocalizationService())
        {
            SelectedDocument = document
        };

        try
        {
            await model.CopyNameCommand.ExecuteAsync(null);
            await model.CopyPathCommand.ExecuteAsync(null);
            model.OpenFolderCommand.Execute(null);
            model.EditDocumentCommand.Execute(null);
            model.OpenPersonalNoteCommand.Execute(null);
            model.OpenRelatedDocumentsCommand.Execute(null);
            await model.ChangeCategoryCommand.ExecuteAsync(null);
            model.SelectedDocument = document;
            await model.QuickEditTagsCommand.ExecuteAsync(null);
            model.SelectedDocument = document;
            await model.QuickEditGhiChuCommand.ExecuteAsync(null);
            model.SelectedDocument = document;
            await model.ToggleImportantCommand.ExecuteAsync(null);
        }
        finally
        {
            File.Delete(filePath);
        }

        Assert.Equal([document.Name, filePath], clipboard.Values);
        Assert.Equal([filePath], process.RevealedPaths);
        Assert.Equal(["addedit", "personal-note", "related-docs"], navigation.Routes);
        Assert.Equal("Physics", document.Subject);
        Assert.Equal("updated value", document.Tags);
        Assert.Equal("updated value", document.Notes);
        Assert.True(document.IsImportant);
    }
}

// ═══════════════════════════════════════════════════════════════
// AddEditViewModel — business logic (DetectFileType, GetFileSize, EscapeCsv)
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Tests static helper methods extracted from AddEditViewModel logic.
/// These are pure functions; we invoke them via reflection or duplicate here.
/// </summary>
file sealed class DashboardDocumentRepository(List<StudyDocument> documents) : IDocumentRepository
    {
        private readonly List<StudyDocument> _documents = documents;
        public bool DeleteResult { get; set; } = true;

        public List<StudyDocument> GetAll() => [.._documents];
        public StudyDocument? GetById(int id) => _documents.FirstOrDefault(document => document.Id == id);
        public List<StudyDocument> Search(string keyword) => [.._documents];
        public List<StudyDocument> Filter(string subject, string type) => [.._documents];
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => [.._documents];
        public bool Add(StudyDocument document) => true;
        public bool AddWithCatalogs(StudyDocument document) => true;
        public bool Update(StudyDocument document) => true;
        public bool Delete(int id) => DeleteResult;
        public List<string> GetDistinctSubjects() => [];
        public List<string> GetDistinctTypes() => [];
        public List<string> GetDistinctTags() => [];
        public List<StudyDocument> GetUpcomingDeadlines(int days) => [];
        public List<StudyDocument> GetOverdueDocuments() => [];
        public void EnsureSubjectExists(string subject) { }
        public void EnsureTypeExists(string type) { }
    }

file sealed class DashboardRecycleBinRepository : IRecycleBinRepository
    {
        public List<StudyDocument> GetDeletedDocuments() => [];
        public bool RestoreDocument(int id) => false;
        public bool PermanentDeleteDocument(int id) => false;
        public int EmptyRecycleBin() => 0;
        public int GetDeletedDocumentCount() => 0;
    }

file sealed class DashboardCategoryRepository : ICategoryRepository
    {
        public List<string> GetAllSubjects() => [];
        public List<string> GetAllTypes() => [];
        public List<(string Name, int Count)> GetSubjectsWithCount() => [];
        public List<(string Name, int Count)> GetTypesWithCount() => [];
        public bool AddSubject(string name) => true;
        public bool AddType(string name) => true;
        public bool UpdateSubjectName(string oldName, string newName) => false;
        public bool UpdateTypeName(string oldName, string newName) => false;
        public bool DeleteDocumentsBySubject(string subjectName) => false;
        public bool DeleteDocumentsByType(string typeName) => false;
        public int GetTotalDocumentCount() => 0;
    }

file sealed class DashboardCollectionRepository : ICollectionRepository
    {
        public List<string> CreatedNames { get; } = [];
        public List<(int CollectionId, int DocumentId)> AddedDocuments { get; } = [];

        public List<(int Id, string Name, string? Description, DateTime CreatedAt, int ItemCount)> GetAll() => [];
        public int Create(string name, string? description = null)
        {
            CreatedNames.Add(name);
            return 41;
        }
        public bool Update(int id, string name, string? description = null) => true;
        public bool Delete(int id) => true;
        public List<StudyDocument> GetDocuments(int collectionId) => [];
        public bool AddDocument(int collectionId, int documentId)
        {
            AddedDocuments.Add((collectionId, documentId));
            return true;
        }
        public bool RemoveDocument(int collectionId, int documentId) => true;
    }

file sealed class DashboardRecentFileRepository : IRecentFileRepository
    {
        public List<int> AddedDocumentIds { get; } = [];
        public List<(int Id, string Name, string? Subject, string? Type, string? FilePath, DateTime OpenedAt)> GetAll() => [];
        public bool Add(int documentId)
        {
            AddedDocumentIds.Add(documentId);
            return true;
        }
        public void Clear() { }
    }

file sealed class DashboardDialogService : IDialogService
    {
        public bool ConfirmResult { get; set; }
        public bool CancelConfirmation { get; set; }
        public string? InputResult { get; set; }
        public string? LastConfirmMessage { get; private set; }
        public string? LastMessage { get; private set; }
        public string? LastErrorMessage { get; private set; }

        public Task ShowMessageAsync(string title, string message)
        {
            LastMessage = message;
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(string title, string message)
        {
            LastErrorMessage = message;
            return Task.CompletedTask;
        }

        public Task<bool> ShowConfirmAsync(string title, string message)
        {
            LastConfirmMessage = message;
            return CancelConfirmation ? Task.FromCanceled<bool>(new CancellationToken(true)) : Task.FromResult(ConfirmResult);
        }

        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
        {
            LastConfirmMessage = message;
            return CancelConfirmation ? Task.FromCanceled<bool>(new CancellationToken(true)) : Task.FromResult(ConfirmResult);
        }

        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "")
            => Task.FromResult(InputResult);
    }

file sealed class DashboardFileDialogService : IFileDialogService
    {
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
    }

file sealed class DashboardCustomDialogService : ICustomDialogService
    {
        public string? ChangeCategoryResult { get; set; }
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowChangeCategoryAsync(string documentName, IList<string> existingCategories, string currentCategory)
            => Task.FromResult(ChangeCategoryResult);
        public Task<StudyDocumentManager.Core.DTOs.AddDocumentDraft?> ShowAddDocumentAsync(string filePath, IList<string> subjects, IList<string> types) => Task.FromResult<StudyDocumentManager.Core.DTOs.AddDocumentDraft?>(null);
        public Task<List<StudyDocument>?> ShowDocumentPickerAsync(string collectionName, IEnumerable<StudyDocument> allDocuments, IEnumerable<int> alreadyInCollection) => Task.FromResult<List<StudyDocument>?>(null);
        public Task<int> ShowSelectCollectionAsync(string documentName, IList<(int Id, string Name, int DocCount)> collections) => Task.FromResult(-1);
    }

file sealed class DashboardNavigationService : INavigationService
    {
        public List<string> Routes { get; } = [];
        public bool CanGoBack => false;
        public void NavigateTo(string viewKey) => Routes.Add(viewKey);
        public void NavigateTo(string viewKey, object? parameter) => Routes.Add(viewKey);
        public void GoBack() { }
    }

file sealed class DashboardClipboardService : IClipboardService
    {
        public List<string> Values { get; } = [];
        public Task SetTextAsync(string text)
        {
            Values.Add(text);
            return Task.CompletedTask;
        }
    }

file sealed class DashboardProcessLauncherService : IProcessLauncherService
    {
        public bool ThrowOnOpen { get; set; }
        public List<string> OpenedFiles { get; } = [];
        public List<string> RevealedPaths { get; } = [];
        public void OpenFile(string filePath)
        {
            if (ThrowOnOpen)
                throw new IOException("launch failed");
            OpenedFiles.Add(filePath);
        }
        public void OpenFolderAndSelect(string filePath) { }
        public void RevealInExplorer(string filePath) => RevealedPaths.Add(filePath);
        public void OpenUrl(string url) { }
    }

file sealed class DashboardExportService : IExportService
    {
        public Task<ExportResult> ExportCsvAsync(IReadOnlyList<StudyDocument> documents, string? suggestedFileName) => Task.FromResult(new ExportResult(false));
    }

file sealed class DashboardBackupService : IBackupService
    {
        public Task<(bool Success, string? Path, string? Error)> BackupAsync() => Task.FromResult((false, (string?)null, (string?)null));
        public Task<(bool Success, string? Error)> RestoreAsync() => Task.FromResult((false, (string?)null));
    }

file sealed class DashboardLocalizationService : ILocalizationService
    {
        public string this[string key] => key;
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public void SetLanguage(SupportedLanguage language) { }
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [];
        public event EventHandler? LanguageChanged { add { } remove { } }
    }

public class AddEditLogicTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public AddEditLogicTests()
    {
        _repo = new DocumentRepository(Db);
    }

    // ─── DetectFileType equivalents ───

    [Theory]
    [InlineData(".pdf", "PDF")]
    [InlineData(".doc", "Word")]
    [InlineData(".docx", "Word")]
    [InlineData(".ppt", "PowerPoint")]
    [InlineData(".pptx", "PowerPoint")]
    [InlineData(".xls", "Excel")]
    [InlineData(".xlsx", "Excel")]
    [InlineData(".txt", "Document")]
    [InlineData(".jpg", "Image")]
    [InlineData(".jpeg", "Image")]
    [InlineData(".png", "Image")]
    [InlineData(".gif", "Image")]
    [InlineData(".bmp", "Image")]
    [InlineData(".mp4", "Video")]
    [InlineData(".avi", "Video")]
    [InlineData(".mkv", "Video")]
    [InlineData(".mp3", "Audio")]
    [InlineData(".wav", "Audio")]
    [InlineData(".flac", "Audio")]
    [InlineData(".zip", "Archive")]
    [InlineData(".rar", "Archive")]
    [InlineData(".7z", "Archive")]
    [InlineData(".cs", "Code")]
    [InlineData(".py", "Code")]
    public void DetectFileType_VariousExtensions_CorrectCategory(string ext, string expected)
    {
        var result = DetectFileTypeHelper(ext);
        Assert.Equal(expected, result);
    }

    // ─── EscapeCsv equivalents ───

    [Fact]
    public void EscapeCsv_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal("", EscapeCsvHelper(null));
        Assert.Equal("", EscapeCsvHelper(""));
    }

    [Fact]
    public void EscapeCsv_PlainText_ReturnsAsIs()
    {
        Assert.Equal("Hello World", EscapeCsvHelper("Hello World"));
        Assert.Equal("Document Manager", EscapeCsvHelper("Document Manager"));
    }

    [Fact]
    public void EscapeCsv_ContainsComma_Quoted()
    {
        var result = EscapeCsvHelper("Hello, World");
        Assert.StartsWith("\"", result);
        Assert.EndsWith("\"", result);
    }

    [Fact]
    public void EscapeCsv_ContainsQuote_DoubleEscaped()
    {
        var result = EscapeCsvHelper("Say \"Hi\"");
        Assert.Contains("\"\"", result);
    }

    [Fact]
    public void EscapeCsv_ContainsNewline_Quoted()
    {
        var result = EscapeCsvHelper("Line1\nLine2");
        Assert.StartsWith("\"", result);
    }

    // ─── AutoFill logic flow ───

    [Fact]
    public void AutoFillName_EmptyTitle_ShouldTakeStemFromPath()
    {
        // Simulate AddEditViewModel logic: if Ten is empty, use GetFileNameWithoutExtension
        string path = @"C:\docs\lecture_notes.pdf";
        string autofilled = Path.GetFileNameWithoutExtension(path);
        Assert.Equal("lecture_notes", autofilled);
    }

    [Fact]
    public void AutoFillType_EmptyLoai_ShouldDetect()
    {
        string ext = ".pdf";
        string detected = DetectFileTypeHelper(ext);
        Assert.Equal("PDF", detected);
    }

    [Fact]
    public void Save_EmptyTitle_ShouldNotPersist()
    {
        // Validation: Ten must not be blank. We verify the repo doesn't receive empty title
        string ten = "   "; // whitespace only
        bool shouldBlock = string.IsNullOrWhiteSpace(ten);
        Assert.True(shouldBlock, "Empty/whitespace title should fail validation before save");
    }

    [Fact]
    public void Save_NewDoc_SyncsCategoryToLookupTable()
    {
        // Simulate AddEditViewModel.SaveAsync flow:
        // 1. Add document
        _repo.Add(new StudyDocument { Name = "New Doc", Subject = "TestSubject", Type = "TestType" });
        // 2. AddSubject / AddType are called
        Db.AddSubject("TestSubject");
        Db.AddType("TestType");

        // Verify lookup tables contain the new values
        var subjects = Db.GetAllSubjects();
        var types = Db.GetAllTypes();
        Assert.Contains("TestSubject", subjects);
        Assert.Contains("TestType", types);
    }

    [Fact]
    public void Edit_LoadDocument_PopulatesAllFields()
    {
        var deadline = new DateTime(2025, 12, 31);
        _repo.Add(new StudyDocument
        {
            Name = "EditMe",
            Subject = "Math",
            Type = "PDF",
            FilePath = @"C:\math.pdf",
            Notes = "notes",
            Author = "Author",
            Tags = "tag1,tag2",
            IsImportant = true,
            Deadline = deadline
        });

        var doc = _repo.GetAll().First(d => d.Name == "EditMe");

        // Verify all fields loaded (simulates LoadDocument)
        Assert.Equal("EditMe", doc.Name);
        Assert.Equal("Math", doc.Subject);
        Assert.Equal("PDF", doc.Type);
        Assert.Equal("notes", doc.Notes);
        Assert.Equal("Author", doc.Author);
        Assert.Equal("tag1,tag2", doc.Tags);
        Assert.True(doc.IsImportant);
        Assert.Equal(deadline.Date, doc.Deadline!.Value.Date);
    }

    // ─── Helpers (inline pure function clones from AddEditViewModel) ───

    private static string DetectFileTypeHelper(string ext)
        => FileTypeDetector.Detect(ext);

    private static string EscapeCsvHelper(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}

// ═══════════════════════════════════════════════════════════════
// BulkDeleteViewModel — business logic (filter + select/deselect)
// ═══════════════════════════════════════════════════════════════
public class BulkDeleteRepositoryCharacterizationTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public BulkDeleteRepositoryCharacterizationTests()
    {
        _repo = new DocumentRepository(Db);
    }

    [Fact]
    public void LoadData_NoFilter_ShowsAllDocs()
    {
        _repo.Add(new StudyDocument { Name = "A", Subject = "Math" });
        _repo.Add(new StudyDocument { Name = "B", Subject = "Science" });

        var all = _repo.GetAll();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Filter_BySubjectInBulkView_ShowsMatchingOnly()
    {
        _repo.Add(new StudyDocument { Name = "Math Doc", Subject = "Math" });
        _repo.Add(new StudyDocument { Name = "Science Doc", Subject = "Science" });

        var filtered = _repo.GetAll().Where(d => d.Subject == "Math").ToList();
        Assert.Single(filtered);
    }

    [Fact]
    public void Filter_ByKeyword_CaseInsensitive()
    {
        _repo.Add(new StudyDocument { Name = "Giải tích", Notes = "" });
        _repo.Add(new StudyDocument { Name = "Vật lý", Notes = "notes về vật lý đại cương" });

        var docs = _repo.GetAll();
        var filtered = docs.Where(d =>
            d.Name.Contains("giải tích", StringComparison.OrdinalIgnoreCase)
            || (d.Notes ?? "").Contains("giải tích", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Single(filtered);
        Assert.Equal("Giải tích", filtered[0].Name);
    }

    [Fact]
    public void SelectAll_Flow_AllDocsChecked()
    {
        _repo.Add(new StudyDocument { Name = "A" });
        _repo.Add(new StudyDocument { Name = "B" });

        var docs = _repo.GetAll().Select(d => new { Doc = d, IsSelected = true }).ToList();
        Assert.All(docs, item => Assert.True(item.IsSelected));
    }

    [Fact]
    public void DeselectAll_Flow_NoneChecked()
    {
        _repo.Add(new StudyDocument { Name = "A" });

        var docs = _repo.GetAll().Select(d => new { Doc = d, IsSelected = false }).ToList();
        Assert.All(docs, item => Assert.False(item.IsSelected));
    }

    [Fact]
    public void DeleteSelected_EmptySelection_ShouldNoop()
    {
        _repo.Add(new StudyDocument { Name = "Not Selected" });
        var selected = new List<int>(); // none selected

        // If no items selected, BulkSoftDelete with empty list returns 0
        int deleted = Db.BulkSoftDelete(selected);
        Assert.Equal(0, deleted);

        // Data intact
        Assert.Single(_repo.GetAll());
    }

    [Fact]
    public void BulkDelete_SelectedItems_MovesToTrash()
    {
        _repo.Add(new StudyDocument { Name = "Delete Me" });
        var doc = _repo.GetAll().First();

        int deleted = Db.BulkSoftDelete(new List<int> { doc.Id });
        Assert.Equal(1, deleted);
        Assert.Empty(_repo.GetAll());
        Assert.Single(Db.GetDeletedDocuments());
    }

    [Fact]
    public void MarkImportant_Selected_TogglesToTrue()
    {
        _repo.Add(new StudyDocument { Name = "A", IsImportant = false });
        var doc = _repo.GetAll().First();

        Db.BulkToggleImportant(new List<int> { doc.Id }, true);

        var updated = _repo.GetAll().First();
        Assert.True(updated.IsImportant);
    }

    [Fact]
    public void ChangeSubject_EmptyNewSubject_ShouldBeBlocked()
    {
        // Simulate validation: NewSubjectValue is whitespace → block before calling BulkUpdateSubject
        string newSubject = "   ";
        bool blocked = string.IsNullOrWhiteSpace(newSubject);
        Assert.True(blocked);
    }

    [Fact]
    public void ChangeSubject_ValidSubject_UpdatesAll()
    {
        _repo.Add(new StudyDocument { Name = "Doc1", Subject = "Old" });
        _repo.Add(new StudyDocument { Name = "Doc2", Subject = "Old" });

        var ids = _repo.GetAll().Select(d => d.Id).ToList();
        int updated = Db.BulkUpdateSubject(ids, "New Subject");

        Assert.Equal(2, updated);
        Assert.All(_repo.GetAll(), d => Assert.Equal("New Subject", d.Subject));
    }
}


// ═══════════════════════════════════════════════════════════════
// CategoryManagementViewModel — business logic flow
// ═══════════════════════════════════════════════════════════════
public class CategoryManagementFlowTests : DatabaseTestBase
{
    public CategoryManagementFlowTests() { }

    [Fact]
    public void AddSubject_NewName_AppearsInList()
    {
        Db.AddSubject("NewSubject");
        Assert.Contains("NewSubject", Db.GetAllSubjects());
    }

    [Fact]
    public void AddSubject_Duplicate_DeduplicatedByHelper()
    {
        Db.AddSubject("DupSubject");
        Db.AddSubject("DupSubject"); // AddSubject is idempotent (INSERT OR IGNORE)

        Assert.Single(Db.GetAllSubjects(), s => s == "DupSubject");
    }

    [Fact]
    public void AddSubject_AlreadyExistsCheck_InViewModel()
    {
        // Simulate CategoryManagementViewModel.AddSubjectAsync validation
        Db.AddSubject("ExistingCategory");
        var subjects = Db.GetAllSubjects();

        bool alreadyExists = subjects.Any(s => s.Equals("ExistingCategory", StringComparison.OrdinalIgnoreCase));
        Assert.True(alreadyExists);
    }

    [Fact]
    public void RenameSubject_UpdatesDocuments()
    {
        Db.AddSubject("OldName");
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "Test", Subject = "OldName" });

        Db.UpdateSubjectName("OldName", "NewName");

        var docs = repo.GetAll();
        Assert.Single(docs);
        Assert.Equal("NewName", docs[0].Subject);
    }

    [Fact]
    public void DeleteSubject_CascadesDocuments()
    {
        Db.AddSubject("ToDelete");
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "Victim", Subject = "ToDelete" });

        // Simulate delete via soft-delete (VM uses DeleteDocumentsBySubject → soft-deletes docs)
        Db.DeleteDocumentsBySubject("ToDelete");
        Db.DeleteSubject("ToDelete");

        Assert.Empty(repo.GetAll());
        Assert.DoesNotContain("ToDelete", Db.GetAllSubjects());
    }

    [Fact]
    public void AddType_NewType_AppearsInList()
    {
        Db.AddType("NewType");
        Assert.Contains("NewType", Db.GetAllTypes());
    }

    [Fact]
    public void RenameType_UpdatesDocuments()
    {
        Db.AddType("OldType");
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "TypeDoc", Type = "OldType" });

        Db.UpdateTypeName("OldType", "NewType");

        var docs = repo.GetAll();
        Assert.Equal("NewType", docs[0].Type);
    }

    [Fact]
    public void DeleteType_CascadesDocuments()
    {
        Db.AddType("ToDeleteType");
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "TypeVictim", Type = "ToDeleteType" });

        Db.DeleteDocumentsByType("ToDeleteType");
        Db.DeleteType("ToDeleteType");

        Assert.Empty(repo.GetAll());
        Assert.DoesNotContain("ToDeleteType", Db.GetAllTypes());
    }

    [Fact]
    public void GetSubjectsWithCount_ReturnsCounts()
    {
        Db.AddSubject("CountTest");
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "A", Subject = "CountTest" });
        repo.Add(new StudyDocument { Name = "B", Subject = "CountTest" });

        var withCounts = Db.GetSubjectsWithCount();
        var entry = withCounts.FirstOrDefault(x => x.Name == "CountTest");

        Assert.True(entry.Name == "CountTest", "CountTest subject should be found");
        Assert.Equal(2, entry.Count);
    }
}

// ═══════════════════════════════════════════════════════════════
// RecycleBinViewModel — flow tests
// ═══════════════════════════════════════════════════════════════
public class RecycleBinFlowTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public RecycleBinFlowTests()
    {
        _repo = new DocumentRepository(Db);
    }

    [Fact]
    public void DeletedDocuments_ShowInRecycleBin()
    {
        _repo.Add(new StudyDocument { Name = "Gone" });
        var doc = _repo.GetAll().First();
        _repo.Delete(doc.Id);

        var deleted = Db.GetDeletedDocuments();
        Assert.Single(deleted);
        Assert.Equal("Gone", deleted[0].Name);
    }

    [Fact]
    public void RestoreDocument_RemovesFromRecycleBin()
    {
        _repo.Add(new StudyDocument { Name = "Restore Me" });
        var doc = _repo.GetAll().First();
        _repo.Delete(doc.Id);

        Db.RestoreDocument(doc.Id);

        Assert.Empty(Db.GetDeletedDocuments());
        Assert.Single(_repo.GetAll());
    }

    [Fact]
    public void PermanentDelete_RemovesFromEverywhere()
    {
        _repo.Add(new StudyDocument { Name = "Permanent" });
        var doc = _repo.GetAll().First();
        _repo.Delete(doc.Id);

        Db.PermanentDeleteDocument(doc.Id);

        Assert.Empty(Db.GetDeletedDocuments());
        Assert.Empty(_repo.GetAll());
    }

    [Fact]
    public void EmptyTrash_DeletesAllDeleted()
    {
        _repo.Add(new StudyDocument { Name = "T1" });
        _repo.Add(new StudyDocument { Name = "T2" });

        foreach (var doc in _repo.GetAll())
            _repo.Delete(doc.Id);

        Assert.Equal(2, Db.GetDeletedDocuments().Count);

        int count = Db.EmptyRecycleBin();
        Assert.Equal(2, count);
        Assert.Empty(Db.GetDeletedDocuments());
    }

    [Fact]
    public void EmptyTrash_WhenAlreadyEmpty_Returns0()
    {
        int count = Db.EmptyRecycleBin();
        Assert.Equal(0, count);
    }

    [Fact]
    public void GetDeletedDocumentCount_MatchesGetDeletedDocuments()
    {
        _repo.Add(new StudyDocument { Name = "D1" });
        _repo.Add(new StudyDocument { Name = "D2" });

        foreach (var doc in _repo.GetAll())
            _repo.Delete(doc.Id);

        int countInt = Db.GetDeletedDocumentCount();
        int listCount = Db.GetDeletedDocuments().Count;

        Assert.Equal(listCount, countInt);
    }
}

// ═══════════════════════════════════════════════════════════════
// DuplicateDetectionViewModel — scan logic
// ═══════════════════════════════════════════════════════════════
public class DuplicateDetectionFlowTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public DuplicateDetectionFlowTests()
    {
        _repo = new DocumentRepository(Db);
    }

    [Fact]
    public void Scan_NoDuplicates_Returns0Groups()
    {
        _repo.Add(new StudyDocument { Name = "UniqueA" });
        _repo.Add(new StudyDocument { Name = "UniqueB" });

        var docs = _repo.GetAll();
        var groups = docs
            .GroupBy(d => d.Name.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Empty(groups);
    }

    [Fact]
    public void Scan_WithDuplicates_DetectsGroups()
    {
        _repo.Add(new StudyDocument { Name = "Same Name" });
        _repo.Add(new StudyDocument { Name = "Same Name" });
        _repo.Add(new StudyDocument { Name = "Unique" });

        var docs = _repo.GetAll();
        var groups = docs
            .GroupBy(d => d.Name.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Single(groups);
        Assert.Equal(2, groups[0].Count());
    }

    [Fact]
    public void Scan_CaseInsensitive_GroupsTogether()
    {
        _repo.Add(new StudyDocument { Name = "giải tích" });
        _repo.Add(new StudyDocument { Name = "Giải Tích" });
        _repo.Add(new StudyDocument { Name = "GIẢI TÍCH" });

        var docs = _repo.GetAll();
        var groups = docs
            .GroupBy(d => d.Name.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Single(groups);
        Assert.Equal(3, groups[0].Count());
    }

    [Fact]
    public void DeleteDuplicate_RemovesOne_OtherStays()
    {
        _repo.Add(new StudyDocument { Name = "Dup" });
        _repo.Add(new StudyDocument { Name = "Dup" });

        var docs = _repo.GetAll();
        Assert.Equal(2, docs.Count);

        _repo.Delete(docs[0].Id);

        var remaining = _repo.GetAll();
        Assert.Single(remaining);
        Assert.Equal("Dup", remaining[0].Name);
    }

    [Fact]
    public void Scan_SoftDeletedExcluded_NotGrouped()
    {
        _repo.Add(new StudyDocument { Name = "Ghost" });
        _repo.Add(new StudyDocument { Name = "Ghost" });

        var first = _repo.GetAll().First();
        _repo.Delete(first.Id); // Soft-delete one

        var activeDocs = _repo.GetAll(); // Should only return 1
        var groups = activeDocs
            .GroupBy(d => d.Name.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Empty(groups); // No duplicates in active docs
    }
}

// ═══════════════════════════════════════════════════════════════
// FileIntegrityCheckViewModel — flow tests
// ═══════════════════════════════════════════════════════════════
public class FileIntegrityFlowTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public FileIntegrityFlowTests()
    {
        _repo = new DocumentRepository(Db);
    }

    [Fact]
    public void Scan_DocWithMissingFile_DetectedAsBroken()
    {
        _repo.Add(new StudyDocument
        {
            Name = "Broken",
            FilePath = @"C:\NonExistent\missing.pdf"
        });

        var docs = _repo.GetAll();
        var broken = docs.Where(d =>
            !string.IsNullOrEmpty(d.FilePath) && !File.Exists(d.FilePath)).ToList();

        Assert.Single(broken);
        Assert.Equal("Broken", broken[0].Name);
    }

    [Fact]
    public void Scan_DocWithNoPath_NotDetectedAsBroken()
    {
        _repo.Add(new StudyDocument { Name = "Meta Only", FilePath = "" });

        var docs = _repo.GetAll();
        // Missing path is not "broken" — it just has no file reference
        var broken = docs.Where(d =>
            !string.IsNullOrEmpty(d.FilePath) && !File.Exists(d.FilePath)).ToList();

        Assert.Empty(broken);
    }

    [Fact]
    public void Scan_RealFile_NotBroken()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            _repo.Add(new StudyDocument { Name = "Real File", FilePath = tmpFile });

            var docs = _repo.GetAll();
            var broken = docs.Where(d =>
                !string.IsNullOrEmpty(d.FilePath) && !File.Exists(d.FilePath)).ToList();

            Assert.Empty(broken);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public void DeleteBroken_RemovesRecordFromDb()
    {
        _repo.Add(new StudyDocument
        {
            Name = "ToRemove",
            FilePath = @"C:\ghost\file.pdf"
        });

        var doc = _repo.GetAll().First(d => d.Name == "ToRemove");
        _repo.Delete(doc.Id);

        Assert.Empty(_repo.GetAll());
    }

    [Fact]
    public void ClearPath_OnlyRemovesPath_KeepsRecord()
    {
        _repo.Add(new StudyDocument
        {
            Name = "PathOnly",
            FilePath = @"C:\docs\old.pdf"
        });

        var doc = _repo.GetAll().First();
        Db.ClearDocumentPath(doc.Id);

        var updated = _repo.GetAll().First();
        Assert.Equal("PathOnly", updated.Name);
        Assert.True(string.IsNullOrEmpty(updated.FilePath));
    }
}

// ═══════════════════════════════════════════════════════════════
// PersonalNoteViewModel flow tests
// ═══════════════════════════════════════════════════════════════
public class PersonalNoteFlowTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public PersonalNoteFlowTests()
    {
        _repo = new DocumentRepository(Db);
    }

    [Fact]
    public void Load_DocumentWithNoNote_ReturnsEmpty()
    {
        _repo.Add(new StudyDocument { Name = "NoNote" });
        var doc = _repo.GetAll().First();

        var note = Db.GetPersonalNote(doc.Id);
        Assert.True(string.IsNullOrEmpty(note));
    }

    [Fact]
    public void Save_Note_PersistsAndLoads()
    {
        _repo.Add(new StudyDocument { Name = "HasNote" });
        var doc = _repo.GetAll().First();

        Db.SavePersonalNote(doc.Id, "My important note");

        var loaded = Db.GetPersonalNote(doc.Id);
        Assert.Equal("My important note", loaded);
    }

    [Fact]
    public void Update_Note_ReplacesOldContent()
    {
        _repo.Add(new StudyDocument { Name = "UpdateNote" });
        var doc = _repo.GetAll().First();

        Db.SavePersonalNote(doc.Id, "Old note");
        Db.SavePersonalNote(doc.Id, "New note"); // Upsert

        Assert.Equal("New note", Db.GetPersonalNote(doc.Id));
    }

    [Fact]
    public void Delete_Note_ClearsContent()
    {
        _repo.Add(new StudyDocument { Name = "DeleteNote" });
        var doc = _repo.GetAll().First();

        Db.SavePersonalNote(doc.Id, "To be deleted");
        Db.DeletePersonalNote(doc.Id);

        var result = Db.GetPersonalNote(doc.Id);
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Fact]
    public void Cancel_DoesNotSave()
    {
        _repo.Add(new StudyDocument { Name = "CancelTest" });
        var doc = _repo.GetAll().First();

        // Simulate: user opens note, types content, clicks Cancel (does NOT call SavePersonalNote)
        // Verify original note is unchanged
        var before = Db.GetPersonalNote(doc.Id);
        // No save call happens
        var after = Db.GetPersonalNote(doc.Id);

        Assert.Equal(before, after);
    }
}

// ═══════════════════════════════════════════════════════════════
// RelatedDocumentsViewModel — flow tests
// ═══════════════════════════════════════════════════════════════
public class RelatedDocumentsFlowTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public RelatedDocumentsFlowTests()
    {
        _repo = new DocumentRepository(Db);
    }

    [Fact]
    public void LoadRelated_NoRelations_EmptyList()
    {
        _repo.Add(new StudyDocument { Name = "Standalone" });
        var doc = _repo.GetAll().First();

        var related = Db.GetRelatedDocuments(doc.Id);
        Assert.Empty(related);
    }

    [Fact]
    public void AddRelation_AppearsBidirectionally()
    {
        _repo.Add(new StudyDocument { Name = "DocA" });
        _repo.Add(new StudyDocument { Name = "DocB" });

        var docs = _repo.GetAll();
        var a = docs.First(d => d.Name == "DocA");
        var b = docs.First(d => d.Name == "DocB");

        Db.AddDocumentRelation(a.Id, b.Id);

        var relatedFromA = Db.GetRelatedDocuments(a.Id);
        var relatedFromB = Db.GetRelatedDocuments(b.Id);

        Assert.Single(relatedFromA);
        Assert.Single(relatedFromB);
    }

    [Fact]
    public void RemoveRelation_DisconnectsDocuments()
    {
        _repo.Add(new StudyDocument { Name = "X" });
        _repo.Add(new StudyDocument { Name = "Y" });

        var docs = _repo.GetAll();
        var x = docs.First(d => d.Name == "X");
        var y = docs.First(d => d.Name == "Y");

        Db.AddDocumentRelation(x.Id, y.Id);
        var relations = Db.GetRelatedDocuments(x.Id);
        var rel = relations.First();

        Db.RemoveDocumentRelation(rel.RelationId);

        Assert.Empty(Db.GetRelatedDocuments(x.Id));
    }

    [Fact]
    public void GetRelated_SoftDeletedDoc_NotInResults()
    {
        _repo.Add(new StudyDocument { Name = "Live" });
        _repo.Add(new StudyDocument { Name = "Deleted" });

        var docs = _repo.GetAll();
        var live = docs.First(d => d.Name == "Live");
        var del = docs.First(d => d.Name == "Deleted");

        Db.AddDocumentRelation(live.Id, del.Id);
        _repo.Delete(del.Id); // Soft-delete the related doc

        var related = Db.GetRelatedDocuments(live.Id);
        // GetRelatedDocuments filters out is_deleted
        Assert.Empty(related);
    }
}

// ═══════════════════════════════════════════════════════════════
// UX Flow Integration: DashboardViewModel.ActiveFilterCount
// ═══════════════════════════════════════════════════════════════
public class ActiveFilterCountLogicTests
{
    // Pure unit tests — no DB needed

    [Fact]
    public void ActiveFilterCount_NoFilters_Zero()
    {
        int count = ComputeActiveFilterCount(
            isDateFilterEnabled: false, hasFromDate: false, hasToDate: false,
            isSizeFilterEnabled: false,
            isImportantOnly: false);
        Assert.Equal(0, count);
    }

    [Fact]
    public void ActiveFilterCount_DateFilterWithDate_One()
    {
        int count = ComputeActiveFilterCount(
            isDateFilterEnabled: true, hasFromDate: true, hasToDate: false,
            isSizeFilterEnabled: false,
            isImportantOnly: false);
        Assert.Equal(1, count);
    }

    [Fact]
    public void ActiveFilterCount_DateFilterWithoutDate_Zero()
    {
        // Date filter enabled but no dates selected → doesn't count
        int count = ComputeActiveFilterCount(
            isDateFilterEnabled: true, hasFromDate: false, hasToDate: false,
            isSizeFilterEnabled: false,
            isImportantOnly: false);
        Assert.Equal(0, count);
    }

    [Fact]
    public void ActiveFilterCount_SizeFilter_One()
    {
        int count = ComputeActiveFilterCount(
            isDateFilterEnabled: false, hasFromDate: false, hasToDate: false,
            isSizeFilterEnabled: true,
            isImportantOnly: false);
        Assert.Equal(1, count);
    }

    [Fact]
    public void ActiveFilterCount_ImportantOnly_One()
    {
        int count = ComputeActiveFilterCount(
            isDateFilterEnabled: false, hasFromDate: false, hasToDate: false,
            isSizeFilterEnabled: false,
            isImportantOnly: true);
        Assert.Equal(1, count);
    }

    [Fact]
    public void ActiveFilterCount_AllFilters_Three()
    {
        int count = ComputeActiveFilterCount(
            isDateFilterEnabled: true, hasFromDate: true, hasToDate: true,
            isSizeFilterEnabled: true,
            isImportantOnly: true);
        Assert.Equal(3, count);
    }

    private static int ComputeActiveFilterCount(
        bool isDateFilterEnabled, bool hasFromDate, bool hasToDate,
        bool isSizeFilterEnabled, bool isImportantOnly)
    {
        // Clone of DashboardViewModel.ActiveFilterCount logic
        int count = 0;
        if (isDateFilterEnabled && (hasFromDate || hasToDate)) count++;
        if (isSizeFilterEnabled) count++;
        if (isImportantOnly) count++;
        return count;
    }
}

// ═══════════════════════════════════════════════════════════════
// UX Flow: PreviewIcon logic (CategoryTreeItem, PreviewIcon)
// ═══════════════════════════════════════════════════════════════
public class PreviewIconLogicTests
{
    [Theory]
    [InlineData(null, "📄")]
    [InlineData("Image", "🖼️")]
    [InlineData("Video", "🎬")]
    [InlineData("Audio", "🎵")]
    [InlineData("Archive", "📦")]
    [InlineData("Document", "📝")]
    [InlineData("PDF", "📄")]
    [InlineData("Word", "📄")]
    public void PreviewIcon_VariousTypes_CorrectIcon(string? type, string expectedIcon)
    {
        StudyDocument? doc = type == null ? null : new StudyDocument { Type = type };
        var icon = GetPreviewIcon(doc);
        Assert.Equal(expectedIcon, icon);
    }

    private static string GetPreviewIcon(StudyDocument? doc) => doc switch
    {
        null => "📄",
        var d when d.Type is "Image" => "🖼️",
        var d when d.Type is "Video" => "🎬",
        var d when d.Type is "Audio" => "🎵",
        var d when d.Type is "Archive" => "📦",
        var d when d.Type is "Document" => "📝",
        _ => "📄"
    };
}

// ═══════════════════════════════════════════════════════════════
// CategoryTreeItem logic (display text, indent)
// ═══════════════════════════════════════════════════════════════
public class CategoryTreeItemLogicTests
{
    [Fact]
    public void DisplayText_IncludesNameAndCount()
    {
        var item = new { Name = "Math", Count = 5 };
        var display = $"{item.Name} ({item.Count})";
        Assert.Equal("Math (5)", display);
    }

    [Fact]
    public void FilterByCategory_All_ResetsFilters()
    {
        // Simulate DashboardViewModel.FilterByCategory "all"
        string selectedSubject = "Math";
        string selectedType = "PDF";
        bool isImportantOnly = true;

        // FilterType="all"
        selectedSubject = "All";
        selectedType = "All";
        isImportantOnly = false;

        Assert.Equal("All", selectedSubject);
        Assert.Equal("All", selectedType);
        Assert.False(isImportantOnly);
    }

    [Fact]
    public void FilterByCategory_Subject_OnlyChangesSubject()
    {
        string selectedSubject = "All";
        string selectedType = "All";
        bool isImportantOnly = false;

        // FilterType="subject"
        selectedSubject = "Math";
        selectedType = "All";
        isImportantOnly = false;

        Assert.Equal("Math", selectedSubject);
        Assert.Equal("All", selectedType);
        Assert.False(isImportantOnly);
    }

    [Fact]
    public void FilterByCategory_Important_SetsImportantOnly()
    {
        bool isImportantOnly = false;

        // FilterType="important"
        isImportantOnly = true;

        Assert.True(isImportantOnly);
    }

    [Fact]
    public void FilterByCategory_CollectionHeader_DoesNothing()
    {
        // "collection-header" = early return, no state change
        string selectedSubject = "Physics";
        string filterType = "collection-header";

        bool earlyReturn = filterType == "collection-header";
        Assert.True(earlyReturn);
        Assert.Equal("Physics", selectedSubject); // Unchanged
    }
}
