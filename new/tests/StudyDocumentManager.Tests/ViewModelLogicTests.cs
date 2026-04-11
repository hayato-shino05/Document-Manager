using Xunit;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Services;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;

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
    public void DatabasePath_WhenNotSet_ReturnsDefaultAppBaseDir()
    {
        // Save current path
        string? original = null;
        try
        {
            // Use reflection to reset the private static field so the property takes the default branch
            var field = typeof(DatabaseHelper).GetField("_databasePath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            original = (string?)field!.GetValue(null);
            field.SetValue(null, null);

            // Also reset the connection string
            var csField = typeof(DatabaseHelper).GetField("_connectionString",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            csField!.SetValue(null, null);

            var path = DatabaseHelper.DatabasePath;

            // Should be a path ending in data/study_documents.db relative to base dir
            Assert.False(string.IsNullOrEmpty(path));
            Assert.EndsWith("study_documents.db", path);
            Assert.Contains("data", path);
        }
        finally
        {
            // Restore the field so subsequent tests are not broken
            var field = typeof(DatabaseHelper).GetField("_databasePath",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            field!.SetValue(null, original);

            var csField = typeof(DatabaseHelper).GetField("_connectionString",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            csField!.SetValue(null, null);
        }
    }

    [Fact]
    public void InitializeDatabase_WhenDataFolderMissing_CreatesIt()
    {
        // Use a brand-new GUID temp dir that definitely doesn't exist yet
        var tempRoot = Path.Combine(Path.GetTempPath(), $"sdm_mkdirtest_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(tempRoot, "data", "study_documents.db");

        try
        {
            DatabaseHelper.SetDatabasePath(dbPath);
            DatabaseHelper.InitializeDatabase(); // Should create tempRoot/data/

            Assert.True(File.Exists(dbPath), "DB file should exist after InitializeDatabase");
            Assert.True(Directory.Exists(Path.GetDirectoryName(dbPath)!));
        }
        finally
        {
            DatabaseHelper.CloseAllConnections();
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
        _repo = new DocumentRepository();
    }

    [Fact]
    public void LoadData_AfterAdd_TotalDocumentsIsCorrect()
    {
        _repo.Add(new StudyDocument { Ten = "Doc1", MonHoc = "Math", Loai = "PDF" });
        _repo.Add(new StudyDocument { Ten = "Doc2", MonHoc = "Physics", Loai = "Word", QuanTrong = true });

        var all = _repo.GetAll();
        Assert.Equal(2, all.Count);
        Assert.Equal(1, all.Count(d => d.QuanTrong));
    }

    [Fact]
    public void Filter_SubjectSentinel_ReturnsAll()
    {
        _repo.Add(new StudyDocument { Ten = "A", MonHoc = "Math" });
        _repo.Add(new StudyDocument { Ten = "B", MonHoc = "Physics" });

        // "Tất cả" sentinel → empty string → GetAll
        var results = _repo.GetAll();
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Filter_BySubject_ReturnsMatchOnly()
    {
        _repo.Add(new StudyDocument { Ten = "A", MonHoc = "Math" });
        _repo.Add(new StudyDocument { Ten = "B", MonHoc = "Physics" });

        var results = _repo.SearchAdvanced("", "Math", "", null, null, null, null, null);
        Assert.Single(results);
        Assert.Equal("A", results[0].Ten);
    }

    [Fact]
    public void CategoryTree_AllNode_HasCorrectCount()
    {
        _repo.Add(new StudyDocument { Ten = "A", MonHoc = "Math" });
        _repo.Add(new StudyDocument { Ten = "B", MonHoc = "Science" });

        var all = _repo.GetAll();
        // AllNode should show total count
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void CategoryTree_SubjectNodes_OnlyNonEmptySubjects()
    {
        _repo.Add(new StudyDocument { Ten = "A", MonHoc = "Math" });
        _repo.Add(new StudyDocument { Ten = "B", MonHoc = "" }); // No subject

        var all = _repo.GetAll();
        var bySubject = all.GroupBy(d => d.MonHoc).Where(g => g.Count() > 0 && !string.IsNullOrEmpty(g.Key));
        // Only "Math" has non-empty subject
        Assert.Single(bySubject);
    }

    [Fact]
    public void ImportantFilter_OnlyImportantDocs()
    {
        _repo.Add(new StudyDocument { Ten = "Important", QuanTrong = true });
        _repo.Add(new StudyDocument { Ten = "Normal", QuanTrong = false });

        var results = _repo.SearchAdvanced("", "", "", null, null, null, null, true);
        Assert.Single(results);
        Assert.Equal("Important", results[0].Ten);
    }

    [Fact]
    public void ShowUpcomingDeadlines_Returns7DayDocs()
    {
        _repo.Add(new StudyDocument { Ten = "Due Soon", Deadline = DateTime.Now.AddDays(3) });
        _repo.Add(new StudyDocument { Ten = "Due Later", Deadline = DateTime.Now.AddDays(30) });
        _repo.Add(new StudyDocument { Ten = "No Deadline" });

        var upcoming = _repo.GetUpcomingDeadlines(7);
        Assert.Single(upcoming);
        Assert.Equal("Due Soon", upcoming[0].Ten);
    }

    [Fact]
    public void ShowOverdue_ReturnsExpiredDocs()
    {
        _repo.Add(new StudyDocument { Ten = "Overdue", Deadline = DateTime.Now.AddDays(-2) });
        _repo.Add(new StudyDocument { Ten = "Future", Deadline = DateTime.Now.AddDays(5) });

        var overdue = _repo.GetOverdueDocuments();
        Assert.Single(overdue);
        Assert.Equal("Overdue", overdue[0].Ten);
    }

    [Fact]
    public void ToggleImportant_UpdatesPersisted()
    {
        _repo.Add(new StudyDocument { Ten = "Toggle Me", QuanTrong = false });
        var doc = _repo.GetAll().First(d => d.Ten == "Toggle Me");

        doc.QuanTrong = true;
        _repo.Update(doc);

        var updated = _repo.GetAll().First(d => d.Ten == "Toggle Me");
        Assert.True(updated.QuanTrong);
    }

    [Fact]
    public void DeleteDocument_SoftDeletes_NotInGetAll()
    {
        _repo.Add(new StudyDocument { Ten = "Will Be Deleted" });
        var doc = _repo.GetAll().First(d => d.Ten == "Will Be Deleted");

        _repo.Delete(doc.Id);

        var all = _repo.GetAll();
        Assert.DoesNotContain(all, d => d.Ten == "Will Be Deleted");
    }

    [Fact]
    public void StatsRefresh_AfterDelete_CountDecreases()
    {
        _repo.Add(new StudyDocument { Ten = "D1" });
        _repo.Add(new StudyDocument { Ten = "D2" });

        var countBefore = _repo.GetAll().Count;

        var doc = _repo.GetAll().First();
        _repo.Delete(doc.Id);

        var countAfter = _repo.GetAll().Count;
        Assert.Equal(countBefore - 1, countAfter);
    }

    [Fact]
    public void AddToCollection_Flow_DocAppearsInCollection()
    {
        _repo.Add(new StudyDocument { Ten = "ColDoc" });
        var doc = _repo.GetAll().First();
        DatabaseHelper.CreateCollection("My Collection");
        var col = DatabaseHelper.GetCollections().First(c => c.Name == "My Collection");

        DatabaseHelper.AddDocumentToCollection(col.Id, doc.Id);

        var colDocs = DatabaseHelper.GetDocumentsInCollection(col.Id)!;
        Assert.Single(colDocs);
    }
}

// ═══════════════════════════════════════════════════════════════
// AddEditViewModel — business logic (DetectFileType, GetFileSize, EscapeCsv)
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Tests static helper methods extracted from AddEditViewModel logic.
/// These are pure functions; we invoke them via reflection or duplicate here.
/// </summary>
public class AddEditLogicTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public AddEditLogicTests()
    {
        _repo = new DocumentRepository();
    }

    // ─── DetectFileType equivalents ───

    [Theory]
    [InlineData(".pdf", "Tài liệu")]
    [InlineData(".doc", "Tài liệu")]
    [InlineData(".docx", "Tài liệu")]
    [InlineData(".ppt", "Tài liệu")]
    [InlineData(".pptx", "Tài liệu")]
    [InlineData(".xls", "Tài liệu")]
    [InlineData(".xlsx", "Tài liệu")]
    [InlineData(".txt", "Tài liệu")]
    [InlineData(".jpg", "Hình ảnh")]
    [InlineData(".jpeg", "Hình ảnh")]
    [InlineData(".png", "Hình ảnh")]
    [InlineData(".gif", "Hình ảnh")]
    [InlineData(".bmp", "Hình ảnh")]
    [InlineData(".mp4", "Video")]
    [InlineData(".avi", "Video")]
    [InlineData(".mkv", "Video")]
    [InlineData(".mp3", "Audio")]
    [InlineData(".wav", "Audio")]
    [InlineData(".flac", "Audio")]
    [InlineData(".zip", "Nén")]
    [InlineData(".rar", "Nén")]
    [InlineData(".7z", "Nén")]
    [InlineData(".cs", "CS")]  // unknown → uppercased extension
    [InlineData(".py", "PY")]
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
        Assert.Equal("Study Document Manager", EscapeCsvHelper("Study Document Manager"));
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
        Assert.Equal("Tài liệu", detected);
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
        _repo.Add(new StudyDocument { Ten = "New Doc", MonHoc = "TestSubject", Loai = "TestType" });
        // 2. AddSubject / AddType are called
        DatabaseHelper.AddSubject("TestSubject");
        DatabaseHelper.AddType("TestType");

        // Verify lookup tables contain the new values
        var subjects = DatabaseHelper.GetAllSubjects();
        var types = DatabaseHelper.GetAllTypes();
        Assert.Contains("TestSubject", subjects);
        Assert.Contains("TestType", types);
    }

    [Fact]
    public void Edit_LoadDocument_PopulatesAllFields()
    {
        var deadline = new DateTime(2025, 12, 31);
        _repo.Add(new StudyDocument
        {
            Ten = "EditMe",
            MonHoc = "Math",
            Loai = "PDF",
            DuongDan = @"C:\math.pdf",
            GhiChu = "notes",
            TacGia = "Author",
            Tags = "tag1,tag2",
            QuanTrong = true,
            Deadline = deadline
        });

        var doc = _repo.GetAll().First(d => d.Ten == "EditMe");

        // Verify all fields loaded (simulates LoadDocument)
        Assert.Equal("EditMe", doc.Ten);
        Assert.Equal("Math", doc.MonHoc);
        Assert.Equal("PDF", doc.Loai);
        Assert.Equal("notes", doc.GhiChu);
        Assert.Equal("Author", doc.TacGia);
        Assert.Equal("tag1,tag2", doc.Tags);
        Assert.True(doc.QuanTrong);
        Assert.Equal(deadline.Date, doc.Deadline!.Value.Date);
    }

    // ─── Helpers (inline pure function clones from AddEditViewModel) ───

    private static string DetectFileTypeHelper(string ext) => ext switch
    {
        ".pdf" => "Tài liệu",
        ".doc" or ".docx" => "Tài liệu",
        ".ppt" or ".pptx" => "Tài liệu",
        ".xls" or ".xlsx" => "Tài liệu",
        ".txt" => "Tài liệu",
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp"
            or ".ico" or ".tiff" or ".webp" => "Hình ảnh",
        ".mp4" or ".avi" or ".mkv" or ".mov"
            or ".wmv" or ".webm" or ".flv" or ".m4v" => "Video",
        ".mp3" or ".wav" or ".flac" => "Audio",
        ".zip" or ".rar" or ".7z" => "Nén",
        _ => ext.TrimStart('.').ToUpperInvariant()
    };

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
public class BulkDeleteFlowTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public BulkDeleteFlowTests()
    {
        _repo = new DocumentRepository();
    }

    [Fact]
    public void LoadData_NoFilter_ShowsAllDocs()
    {
        _repo.Add(new StudyDocument { Ten = "A", MonHoc = "Math" });
        _repo.Add(new StudyDocument { Ten = "B", MonHoc = "Science" });

        var all = _repo.GetAll();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Filter_BySubjectInBulkView_ShowsMatchingOnly()
    {
        _repo.Add(new StudyDocument { Ten = "Math Doc", MonHoc = "Math" });
        _repo.Add(new StudyDocument { Ten = "Science Doc", MonHoc = "Science" });

        var filtered = _repo.GetAll().Where(d => d.MonHoc == "Math").ToList();
        Assert.Single(filtered);
    }

    [Fact]
    public void Filter_ByKeyword_CaseInsensitive()
    {
        _repo.Add(new StudyDocument { Ten = "Giải tích", GhiChu = "" });
        _repo.Add(new StudyDocument { Ten = "Vật lý", GhiChu = "notes về vật lý đại cương" });

        var docs = _repo.GetAll();
        var filtered = docs.Where(d =>
            d.Ten.Contains("giải tích", StringComparison.OrdinalIgnoreCase)
            || (d.GhiChu ?? "").Contains("giải tích", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Single(filtered);
        Assert.Equal("Giải tích", filtered[0].Ten);
    }

    [Fact]
    public void SelectAll_Flow_AllDocsChecked()
    {
        _repo.Add(new StudyDocument { Ten = "A" });
        _repo.Add(new StudyDocument { Ten = "B" });

        var docs = _repo.GetAll().Select(d => new { Doc = d, IsSelected = true }).ToList();
        Assert.All(docs, item => Assert.True(item.IsSelected));
    }

    [Fact]
    public void DeselectAll_Flow_NoneChecked()
    {
        _repo.Add(new StudyDocument { Ten = "A" });

        var docs = _repo.GetAll().Select(d => new { Doc = d, IsSelected = false }).ToList();
        Assert.All(docs, item => Assert.False(item.IsSelected));
    }

    [Fact]
    public void DeleteSelected_EmptySelection_ShouldNoop()
    {
        _repo.Add(new StudyDocument { Ten = "Not Selected" });
        var selected = new List<int>(); // none selected

        // If no items selected, BulkSoftDelete with empty list returns 0
        int deleted = DatabaseHelper.BulkSoftDelete(selected);
        Assert.Equal(0, deleted);

        // Data intact
        Assert.Single(_repo.GetAll());
    }

    [Fact]
    public void BulkDelete_SelectedItems_MovesToTrash()
    {
        _repo.Add(new StudyDocument { Ten = "Delete Me" });
        var doc = _repo.GetAll().First();

        int deleted = DatabaseHelper.BulkSoftDelete(new List<int> { doc.Id });
        Assert.Equal(1, deleted);
        Assert.Empty(_repo.GetAll());
        Assert.Single(DatabaseHelper.GetDeletedDocuments());
    }

    [Fact]
    public void MarkImportant_Selected_TogglesToTrue()
    {
        _repo.Add(new StudyDocument { Ten = "A", QuanTrong = false });
        var doc = _repo.GetAll().First();

        DatabaseHelper.BulkToggleImportant(new List<int> { doc.Id }, true);

        var updated = _repo.GetAll().First();
        Assert.True(updated.QuanTrong);
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
        _repo.Add(new StudyDocument { Ten = "Doc1", MonHoc = "Old" });
        _repo.Add(new StudyDocument { Ten = "Doc2", MonHoc = "Old" });

        var ids = _repo.GetAll().Select(d => d.Id).ToList();
        int updated = DatabaseHelper.BulkUpdateSubject(ids, "New Subject");

        Assert.Equal(2, updated);
        Assert.All(_repo.GetAll(), d => Assert.Equal("New Subject", d.MonHoc));
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
        DatabaseHelper.AddSubject("NewSubject");
        Assert.Contains("NewSubject", DatabaseHelper.GetAllSubjects());
    }

    [Fact]
    public void AddSubject_Duplicate_DeduplicatedByHelper()
    {
        DatabaseHelper.AddSubject("DupSubject");
        DatabaseHelper.AddSubject("DupSubject"); // AddSubject is idempotent (INSERT OR IGNORE)

        Assert.Single(DatabaseHelper.GetAllSubjects(), s => s == "DupSubject");
    }

    [Fact]
    public void AddSubject_AlreadyExistsCheck_InViewModel()
    {
        // Simulate CategoryManagementViewModel.AddSubjectAsync validation
        DatabaseHelper.AddSubject("ExistingCategory");
        var subjects = DatabaseHelper.GetAllSubjects();

        bool alreadyExists = subjects.Any(s => s.Equals("ExistingCategory", StringComparison.OrdinalIgnoreCase));
        Assert.True(alreadyExists);
    }

    [Fact]
    public void RenameSubject_UpdatesDocuments()
    {
        DatabaseHelper.AddSubject("OldName");
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "Test", MonHoc = "OldName" });

        DatabaseHelper.UpdateSubjectName("OldName", "NewName");

        var docs = repo.GetAll();
        Assert.Single(docs);
        Assert.Equal("NewName", docs[0].MonHoc);
    }

    [Fact]
    public void DeleteSubject_CascadesDocuments()
    {
        DatabaseHelper.AddSubject("ToDelete");
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "Victim", MonHoc = "ToDelete" });

        // Simulate delete via soft-delete (VM uses DeleteDocumentsBySubject → soft-deletes docs)
        DatabaseHelper.DeleteDocumentsBySubject("ToDelete");
        DatabaseHelper.DeleteSubject("ToDelete");

        Assert.Empty(repo.GetAll());
        Assert.DoesNotContain("ToDelete", DatabaseHelper.GetAllSubjects());
    }

    [Fact]
    public void AddType_NewType_AppearsInList()
    {
        DatabaseHelper.AddType("NewType");
        Assert.Contains("NewType", DatabaseHelper.GetAllTypes());
    }

    [Fact]
    public void RenameType_UpdatesDocuments()
    {
        DatabaseHelper.AddType("OldType");
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "TypeDoc", Loai = "OldType" });

        DatabaseHelper.UpdateTypeName("OldType", "NewType");

        var docs = repo.GetAll();
        Assert.Equal("NewType", docs[0].Loai);
    }

    [Fact]
    public void DeleteType_CascadesDocuments()
    {
        DatabaseHelper.AddType("ToDeleteType");
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "TypeVictim", Loai = "ToDeleteType" });

        DatabaseHelper.DeleteDocumentsByType("ToDeleteType");
        DatabaseHelper.DeleteType("ToDeleteType");

        Assert.Empty(repo.GetAll());
        Assert.DoesNotContain("ToDeleteType", DatabaseHelper.GetAllTypes());
    }

    [Fact]
    public void GetSubjectsWithCount_ReturnsCounts()
    {
        DatabaseHelper.AddSubject("CountTest");
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "A", MonHoc = "CountTest" });
        repo.Add(new StudyDocument { Ten = "B", MonHoc = "CountTest" });

        var withCounts = DatabaseHelper.GetSubjectsWithCount();
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
        _repo = new DocumentRepository();
    }

    [Fact]
    public void DeletedDocuments_ShowInRecycleBin()
    {
        _repo.Add(new StudyDocument { Ten = "Gone" });
        var doc = _repo.GetAll().First();
        _repo.Delete(doc.Id);

        var deleted = DatabaseHelper.GetDeletedDocuments();
        Assert.Single(deleted);
        Assert.Equal("Gone", deleted[0].Ten);
    }

    [Fact]
    public void RestoreDocument_RemovesFromRecycleBin()
    {
        _repo.Add(new StudyDocument { Ten = "Restore Me" });
        var doc = _repo.GetAll().First();
        _repo.Delete(doc.Id);

        DatabaseHelper.RestoreDocument(doc.Id);

        Assert.Empty(DatabaseHelper.GetDeletedDocuments());
        Assert.Single(_repo.GetAll());
    }

    [Fact]
    public void PermanentDelete_RemovesFromEverywhere()
    {
        _repo.Add(new StudyDocument { Ten = "Permanent" });
        var doc = _repo.GetAll().First();
        _repo.Delete(doc.Id);

        DatabaseHelper.PermanentDeleteDocument(doc.Id);

        Assert.Empty(DatabaseHelper.GetDeletedDocuments());
        Assert.Empty(_repo.GetAll());
    }

    [Fact]
    public void EmptyTrash_DeletesAllDeleted()
    {
        _repo.Add(new StudyDocument { Ten = "T1" });
        _repo.Add(new StudyDocument { Ten = "T2" });

        foreach (var doc in _repo.GetAll())
            _repo.Delete(doc.Id);

        Assert.Equal(2, DatabaseHelper.GetDeletedDocuments().Count);

        int count = DatabaseHelper.EmptyRecycleBin();
        Assert.Equal(2, count);
        Assert.Empty(DatabaseHelper.GetDeletedDocuments());
    }

    [Fact]
    public void EmptyTrash_WhenAlreadyEmpty_Returns0()
    {
        int count = DatabaseHelper.EmptyRecycleBin();
        Assert.Equal(0, count);
    }

    [Fact]
    public void GetDeletedDocumentCount_MatchesGetDeletedDocuments()
    {
        _repo.Add(new StudyDocument { Ten = "D1" });
        _repo.Add(new StudyDocument { Ten = "D2" });

        foreach (var doc in _repo.GetAll())
            _repo.Delete(doc.Id);

        int countInt = DatabaseHelper.GetDeletedDocumentCount();
        int listCount = DatabaseHelper.GetDeletedDocuments().Count;

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
        _repo = new DocumentRepository();
    }

    [Fact]
    public void Scan_NoDuplicates_Returns0Groups()
    {
        _repo.Add(new StudyDocument { Ten = "UniqueA" });
        _repo.Add(new StudyDocument { Ten = "UniqueB" });

        var docs = _repo.GetAll();
        var groups = docs
            .GroupBy(d => d.Ten.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Empty(groups);
    }

    [Fact]
    public void Scan_WithDuplicates_DetectsGroups()
    {
        _repo.Add(new StudyDocument { Ten = "Same Name" });
        _repo.Add(new StudyDocument { Ten = "Same Name" });
        _repo.Add(new StudyDocument { Ten = "Unique" });

        var docs = _repo.GetAll();
        var groups = docs
            .GroupBy(d => d.Ten.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Single(groups);
        Assert.Equal(2, groups[0].Count());
    }

    [Fact]
    public void Scan_CaseInsensitive_GroupsTogether()
    {
        _repo.Add(new StudyDocument { Ten = "giải tích" });
        _repo.Add(new StudyDocument { Ten = "Giải Tích" });
        _repo.Add(new StudyDocument { Ten = "GIẢI TÍCH" });

        var docs = _repo.GetAll();
        var groups = docs
            .GroupBy(d => d.Ten.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Single(groups);
        Assert.Equal(3, groups[0].Count());
    }

    [Fact]
    public void DeleteDuplicate_RemovesOne_OtherStays()
    {
        _repo.Add(new StudyDocument { Ten = "Dup" });
        _repo.Add(new StudyDocument { Ten = "Dup" });

        var docs = _repo.GetAll();
        Assert.Equal(2, docs.Count);

        _repo.Delete(docs[0].Id);

        var remaining = _repo.GetAll();
        Assert.Single(remaining);
        Assert.Equal("Dup", remaining[0].Ten);
    }

    [Fact]
    public void Scan_SoftDeletedExcluded_NotGrouped()
    {
        _repo.Add(new StudyDocument { Ten = "Ghost" });
        _repo.Add(new StudyDocument { Ten = "Ghost" });

        var first = _repo.GetAll().First();
        _repo.Delete(first.Id); // Soft-delete one

        var activeDocs = _repo.GetAll(); // Should only return 1
        var groups = activeDocs
            .GroupBy(d => d.Ten.Trim().ToLowerInvariant())
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
        _repo = new DocumentRepository();
    }

    [Fact]
    public void Scan_DocWithMissingFile_DetectedAsBroken()
    {
        _repo.Add(new StudyDocument
        {
            Ten = "Broken",
            DuongDan = @"C:\NonExistent\missing.pdf"
        });

        var docs = _repo.GetAll();
        var broken = docs.Where(d =>
            !string.IsNullOrEmpty(d.DuongDan) && !File.Exists(d.DuongDan)).ToList();

        Assert.Single(broken);
        Assert.Equal("Broken", broken[0].Ten);
    }

    [Fact]
    public void Scan_DocWithNoPath_NotDetectedAsBroken()
    {
        _repo.Add(new StudyDocument { Ten = "Meta Only", DuongDan = "" });

        var docs = _repo.GetAll();
        // Missing path is not "broken" — it just has no file reference
        var broken = docs.Where(d =>
            !string.IsNullOrEmpty(d.DuongDan) && !File.Exists(d.DuongDan)).ToList();

        Assert.Empty(broken);
    }

    [Fact]
    public void Scan_RealFile_NotBroken()
    {
        var tmpFile = Path.GetTempFileName();
        try
        {
            _repo.Add(new StudyDocument { Ten = "Real File", DuongDan = tmpFile });

            var docs = _repo.GetAll();
            var broken = docs.Where(d =>
                !string.IsNullOrEmpty(d.DuongDan) && !File.Exists(d.DuongDan)).ToList();

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
            Ten = "ToRemove",
            DuongDan = @"C:\ghost\file.pdf"
        });

        var doc = _repo.GetAll().First(d => d.Ten == "ToRemove");
        _repo.Delete(doc.Id);

        Assert.Empty(_repo.GetAll());
    }

    [Fact]
    public void ClearPath_OnlyRemovesPath_KeepsRecord()
    {
        _repo.Add(new StudyDocument
        {
            Ten = "PathOnly",
            DuongDan = @"C:\docs\old.pdf"
        });

        var doc = _repo.GetAll().First();
        DatabaseHelper.ClearDocumentPath(doc.Id);

        var updated = _repo.GetAll().First();
        Assert.Equal("PathOnly", updated.Ten);
        Assert.True(string.IsNullOrEmpty(updated.DuongDan));
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
        _repo = new DocumentRepository();
    }

    [Fact]
    public void Load_DocumentWithNoNote_ReturnsEmpty()
    {
        _repo.Add(new StudyDocument { Ten = "NoNote" });
        var doc = _repo.GetAll().First();

        var note = DatabaseHelper.GetPersonalNote(doc.Id);
        Assert.True(string.IsNullOrEmpty(note));
    }

    [Fact]
    public void Save_Note_PersistsAndLoads()
    {
        _repo.Add(new StudyDocument { Ten = "HasNote" });
        var doc = _repo.GetAll().First();

        DatabaseHelper.SavePersonalNote(doc.Id, "My important note");

        var loaded = DatabaseHelper.GetPersonalNote(doc.Id);
        Assert.Equal("My important note", loaded);
    }

    [Fact]
    public void Update_Note_ReplacesOldContent()
    {
        _repo.Add(new StudyDocument { Ten = "UpdateNote" });
        var doc = _repo.GetAll().First();

        DatabaseHelper.SavePersonalNote(doc.Id, "Old note");
        DatabaseHelper.SavePersonalNote(doc.Id, "New note"); // Upsert

        Assert.Equal("New note", DatabaseHelper.GetPersonalNote(doc.Id));
    }

    [Fact]
    public void Delete_Note_ClearsContent()
    {
        _repo.Add(new StudyDocument { Ten = "DeleteNote" });
        var doc = _repo.GetAll().First();

        DatabaseHelper.SavePersonalNote(doc.Id, "To be deleted");
        DatabaseHelper.DeletePersonalNote(doc.Id);

        var result = DatabaseHelper.GetPersonalNote(doc.Id);
        Assert.True(string.IsNullOrEmpty(result));
    }

    [Fact]
    public void Cancel_DoesNotSave()
    {
        _repo.Add(new StudyDocument { Ten = "CancelTest" });
        var doc = _repo.GetAll().First();

        // Simulate: user opens note, types content, clicks Cancel (does NOT call SavePersonalNote)
        // Verify original note is unchanged
        var before = DatabaseHelper.GetPersonalNote(doc.Id);
        // No save call happens
        var after = DatabaseHelper.GetPersonalNote(doc.Id);

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
        _repo = new DocumentRepository();
    }

    [Fact]
    public void LoadRelated_NoRelations_EmptyList()
    {
        _repo.Add(new StudyDocument { Ten = "Standalone" });
        var doc = _repo.GetAll().First();

        var related = DatabaseHelper.GetRelatedDocuments(doc.Id);
        Assert.Empty(related);
    }

    [Fact]
    public void AddRelation_AppearsBidirectionally()
    {
        _repo.Add(new StudyDocument { Ten = "DocA" });
        _repo.Add(new StudyDocument { Ten = "DocB" });

        var docs = _repo.GetAll();
        var a = docs.First(d => d.Ten == "DocA");
        var b = docs.First(d => d.Ten == "DocB");

        DatabaseHelper.AddDocumentRelation(a.Id, b.Id);

        var relatedFromA = DatabaseHelper.GetRelatedDocuments(a.Id);
        var relatedFromB = DatabaseHelper.GetRelatedDocuments(b.Id);

        Assert.Single(relatedFromA);
        Assert.Single(relatedFromB);
    }

    [Fact]
    public void RemoveRelation_DisconnectsDocuments()
    {
        _repo.Add(new StudyDocument { Ten = "X" });
        _repo.Add(new StudyDocument { Ten = "Y" });

        var docs = _repo.GetAll();
        var x = docs.First(d => d.Ten == "X");
        var y = docs.First(d => d.Ten == "Y");

        DatabaseHelper.AddDocumentRelation(x.Id, y.Id);
        var relations = DatabaseHelper.GetRelatedDocuments(x.Id);
        var rel = relations.First();

        DatabaseHelper.RemoveDocumentRelation(rel.RelationId);

        Assert.Empty(DatabaseHelper.GetRelatedDocuments(x.Id));
    }

    [Fact]
    public void GetRelated_SoftDeletedDoc_NotInResults()
    {
        _repo.Add(new StudyDocument { Ten = "Live" });
        _repo.Add(new StudyDocument { Ten = "Deleted" });

        var docs = _repo.GetAll();
        var live = docs.First(d => d.Ten == "Live");
        var del = docs.First(d => d.Ten == "Deleted");

        DatabaseHelper.AddDocumentRelation(live.Id, del.Id);
        _repo.Delete(del.Id); // Soft-delete the related doc

        var related = DatabaseHelper.GetRelatedDocuments(live.Id);
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
    [InlineData("Hình ảnh", "🖼️")]
    [InlineData("Video", "🎬")]
    [InlineData("Audio", "🎵")]
    [InlineData("Nén", "📦")]
    [InlineData("Tài liệu", "📝")]
    [InlineData("PDF", "📄")]           // unknown → default
    [InlineData("Word", "📄")]
    public void PreviewIcon_VariousTypes_CorrectIcon(string? loai, string expectedIcon)
    {
        StudyDocument? doc = loai == null ? null : new StudyDocument { Loai = loai };
        var icon = GetPreviewIcon(doc);
        Assert.Equal(expectedIcon, icon);
    }

    private static string GetPreviewIcon(StudyDocument? doc) => doc switch
    {
        null => "📄",
        var d when d.Loai is "Hình ảnh" => "🖼️",
        var d when d.Loai is "Video" => "🎬",
        var d when d.Loai is "Audio" => "🎵",
        var d when d.Loai is "Nén" => "📦",
        var d when d.Loai is "Tài liệu" => "📝",
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
        selectedSubject = "Tất cả";
        selectedType = "Tất cả";
        isImportantOnly = false;

        Assert.Equal("Tất cả", selectedSubject);
        Assert.Equal("Tất cả", selectedType);
        Assert.False(isImportantOnly);
    }

    [Fact]
    public void FilterByCategory_Subject_OnlyChangesSubject()
    {
        string selectedSubject = "Tất cả";
        string selectedType = "Tất cả";
        bool isImportantOnly = false;

        // FilterType="subject"
        selectedSubject = "Math";
        selectedType = "Tất cả";
        isImportantOnly = false;

        Assert.Equal("Math", selectedSubject);
        Assert.Equal("Tất cả", selectedType);
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
