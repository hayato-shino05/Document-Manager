using Xunit;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Core.Services;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;

namespace StudyDocumentManager.Tests;

// ════════════════════════════════════════════════════════════
// AppVersion — Semver edge cases & Compare logic
// ════════════════════════════════════════════════════════════

public class AppVersionSemverTests
{
    [Theory]
    [InlineData("5.0.0", true)]    // major bump
    [InlineData("4.1.0", true)]    // minor bump (current 4.0.0)
    [InlineData("4.0.1", true)]    // patch bump
    [InlineData("4.0.0", false)]   // equal
    [InlineData("3.99.99", false)] // older major
    [InlineData("0.0.1", false)]   // very old
    public void IsNewer_VariousVersions_ReturnsExpected(string candidateVersion, bool expected)
    {
        Assert.Equal(expected, AppVersion.IsNewer(candidateVersion));
    }

    [Theory]
    [InlineData("4.0.0", "4.0.0", 0)]   // equal
    [InlineData("3.0.0", "4.0.0", -1)]  // current older
    [InlineData("5.0.0", "4.0.0", 1)]   // current newer
    [InlineData("4.0.0", "4.0.1", -1)]  // patch diff
    [InlineData("4.1.0", "4.0.9", 1)]   // minor beats patch
    public void Compare_Semver_ReturnsCorrectValue(string current, string latest, int expected)
    {
        Assert.Equal(expected, AppVersion.Compare(current, latest));
    }

    [Fact]
    public void ParseVersion_WithVPrefix_Handled()
    {
        // Version strings prefixed with 'v' (GitHub releases style)
        int result = AppVersion.Compare("v4.0.0", "v4.0.0");
        Assert.Equal(0, result);
    }

    [Fact]
    public void Compare_TwoDigitVersionParts_ParsedCorrectly()
    {
        // 4.10.0 > 4.9.0 (not string-compared!)
        int result = AppVersion.Compare("4.10.0", "4.9.0");
        Assert.Equal(1, result); // 4.10.0 is newer than 4.9.0
    }
}

// ════════════════════════════════════════════════════════════
// Database Schema — Default Category Seeding
// ════════════════════════════════════════════════════════════

public class DatabaseSchemaSeedingTests : DatabaseTestBase
{
    [Fact]
    public void InitializeDatabase_SeedsDefaultSubjects()
    {
        // Fresh DB should have default "danh_muc" entries
        var subjects = DatabaseHelper.GetAllSubjects();
        Assert.Contains("Học tập", subjects);
        Assert.Contains("Công việc", subjects);
        Assert.Contains("Cá nhân", subjects);
        Assert.Contains("Khác", subjects);
    }

    [Fact]
    public void InitializeDatabase_SeedsDefaultTypes()
    {
        // Fresh DB should have default "loai_tai_lieu" entries
        var types = DatabaseHelper.GetAllTypes();
        Assert.Contains("Tài liệu", types);
        Assert.Contains("Báo cáo", types);
        Assert.Contains("Hình ảnh", types);
        Assert.Contains("Video", types);
        Assert.Contains("Khác", types);
    }

    [Fact]
    public void InitializeDatabase_DefaultSubjectCount_AtLeast8()
    {
        // FEATURES_OLD.md specifies 8 default subject categories
        var subjects = DatabaseHelper.GetAllSubjects();
        Assert.True(subjects.Count >= 8, $"Expected >= 8 default subjects, got {subjects.Count}");
    }

    [Fact]
    public void InitializeDatabase_DefaultTypeCount_AtLeast7()
    {
        // FEATURES_OLD.md specifies Tài liệu, Báo cáo, Hướng dẫn, Biểu mẫu, Hình ảnh, Video, Khác
        var types = DatabaseHelper.GetAllTypes();
        Assert.True(types.Count >= 7, $"Expected >= 7 default types, got {types.Count}");
    }

    [Fact]
    public void InitializeDatabase_AllTablesCreated()
    {
        // Verify all 7 tables exist by performing lightweight queries
        // If tables didn't exist these would throw SqliteException
        var ex = Record.Exception(() =>
        {
            DatabaseHelper.GetAllDocuments();       // tai_lieu
            DatabaseHelper.GetCollections();        // collections + collection_items
            DatabaseHelper.GetRecentFiles();        // recent_files
            DatabaseHelper.GetDeletedDocuments();   // tai_lieu (is_deleted)
            DatabaseHelper.GetAllSubjects();        // danh_muc
            DatabaseHelper.GetAllTypes();           // loai_tai_lieu
        });
        Assert.Null(ex);
    }
}

// ════════════════════════════════════════════════════════════
// Integration Flow #1: Full Document Lifecycle
// ════════════════════════════════════════════════════════════

public class FullDocumentLifecycleTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public FullDocumentLifecycleTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void FullLifecycle_CreateUpdateSearchDeleteRestore()
    {
        // 1. ADD
        var doc = new StudyDocument
        {
            Ten = "Lifecycle Test",
            MonHoc = "Học tập",
            Loai = "Tài liệu",
            Tags = "test;lifecycle",
            QuanTrong = false,
            Deadline = DateTime.Today.AddDays(5)
        };
        bool added = _repo.Add(doc);
        Assert.True(added);
        Assert.Equal(1, _repo.GetAll().Count);

        // 2. GETBYID
        int id = _repo.GetAll()[0].Id;
        var fetched = _repo.GetById(id);
        Assert.NotNull(fetched);
        Assert.Equal("Lifecycle Test", fetched!.Ten);

        // 3. SEARCH
        var searchResults = _repo.Search("Lifecycle");
        Assert.Single(searchResults);

        // 4. UPDATE
        fetched.Ten = "Updated Lifecycle";
        fetched.QuanTrong = true;
        fetched.Deadline = DateTime.Today.AddDays(10);
        bool updated = _repo.Update(fetched);
        Assert.True(updated);

        var afterUpdate = _repo.GetById(id)!;
        Assert.Equal("Updated Lifecycle", afterUpdate.Ten);
        Assert.True(afterUpdate.QuanTrong);
        Assert.Equal(DateTime.Today.AddDays(10).Date, afterUpdate.Deadline!.Value.Date);

        // 5. SOFT DELETE
        bool deleted = _repo.Delete(id);
        Assert.True(deleted);
        Assert.Empty(_repo.GetAll());

        // 6. VERIFY IN RECYCLE BIN
        var bin = DatabaseHelper.GetDeletedDocuments();
        Assert.Single(bin);
        Assert.Equal("Updated Lifecycle", bin[0].Ten);
        Assert.Equal(1, DatabaseHelper.GetDeletedDocumentCount());

        // 7. RESTORE
        DatabaseHelper.RestoreDocument(id);
        Assert.Single(_repo.GetAll());
        Assert.Empty(DatabaseHelper.GetDeletedDocuments());

        // 8. PERMANENT DELETE
        _repo.Delete(id);
        DatabaseHelper.PermanentDeleteDocument(id);
        Assert.Empty(_repo.GetAll());
        Assert.Empty(DatabaseHelper.GetDeletedDocuments());
    }

    [Fact]
    public void FullLifecycle_WithCollection_AndRelations_AndNotes()
    {
        // Multi-feature integration

        // Create docs
        _repo.Add(new StudyDocument { Ten = "Main Doc", MonHoc = "Học tập" });
        _repo.Add(new StudyDocument { Ten = "Related Doc", MonHoc = "Học tập" });
        var docs = _repo.GetAll().OrderBy(d => d.Id).ToList();
        int mainId = docs[0].Id;
        int relatedId = docs[1].Id;

        // Add personal note to main doc
        DatabaseHelper.SavePersonalNote(mainId, "Ghi chú cho tài liệu chính");
        Assert.Equal("Ghi chú cho tài liệu chính", DatabaseHelper.GetPersonalNote(mainId));

        // Link as related documents
        DatabaseHelper.AddDocumentRelation(mainId, relatedId, "Bài tập");
        var related = DatabaseHelper.GetRelatedDocuments(mainId);
        Assert.Single(related);
        Assert.Equal("Bài tập", related[0].RelationType);

        // Create collection and add both
        int colId = DatabaseHelper.CreateCollection("My Study Collection");
        DatabaseHelper.AddDocumentToCollection(colId, mainId);
        DatabaseHelper.AddDocumentToCollection(colId, relatedId);
        Assert.Equal(2, DatabaseHelper.GetDocumentsInCollection(colId).Count);

        // Track as recently opened
        DatabaseHelper.AddRecentFile(mainId);
        Assert.Single(DatabaseHelper.GetRecentFiles());

        // Now soft-delete the related doc
        _repo.Delete(relatedId);

        // Recent files only shows active docs
        Assert.Single(DatabaseHelper.GetRecentFiles());

        // Related docs also excludes soft-deleted
        var stillRelated = DatabaseHelper.GetRelatedDocuments(mainId);
        Assert.Empty(stillRelated); // related doc was deleted

        // Collection also excludes soft-deleted
        Assert.Single(DatabaseHelper.GetDocumentsInCollection(colId));

        // Stats reflect changes
        var stats = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(1, stats.TotalDocuments);
    }
}

// ════════════════════════════════════════════════════════════
// Integration Flow #2: Category Management End-to-End
// ════════════════════════════════════════════════════════════

public class CategoryManagementE2ETests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public CategoryManagementE2ETests() { _repo = new DocumentRepository(); }

    [Fact]
    public void RenameSubject_UpdatesAllDocuments_AndLookupTable()
    {
        // Setup: add subject and assign to docs
        DatabaseHelper.AddSubject("Old Category");
        _repo.Add(new StudyDocument { Ten = "Doc A", MonHoc = "Old Category" });
        _repo.Add(new StudyDocument { Ten = "Doc B", MonHoc = "Old Category" });
        _repo.Add(new StudyDocument { Ten = "Doc C", MonHoc = "Different" });

        // Action: rename
        DatabaseHelper.UpdateSubjectName("Old Category", "New Category");

        // Verify: all docs with old name updated
        var docs = _repo.GetAll();
        Assert.Equal(2, docs.Count(d => d.MonHoc == "New Category"));
        Assert.Equal(1, docs.Count(d => d.MonHoc == "Different"));

        // Lookup table updated
        var subjects = DatabaseHelper.GetAllSubjects();
        Assert.Contains("New Category", subjects);
        Assert.DoesNotContain("Old Category", subjects);

        // GetDistinctSubjects also reflects change
        var distinct = _repo.GetDistinctSubjects();
        Assert.Contains("New Category", distinct);
    }

    [Fact]
    public void DeleteDocumentsBySubject_MovesToRecycleBin_NotPermanent()
    {
        DatabaseHelper.AddSubject("Xóa danh mục này");
        _repo.Add(new StudyDocument { Ten = "Subject Doc 1", MonHoc = "Xóa danh mục này" });
        _repo.Add(new StudyDocument { Ten = "Subject Doc 2", MonHoc = "Xóa danh mục này" });
        _repo.Add(new StudyDocument { Ten = "Keep This", MonHoc = "Giữ lại" });

        DatabaseHelper.DeleteDocumentsBySubject("Xóa danh mục này");

        // Active docs: only "Keep This"
        var active = _repo.GetAll();
        Assert.Single(active);
        Assert.Equal("Keep This", active[0].Ten);

        // Deleted docs in bin: the 2 subject docs
        var bin = DatabaseHelper.GetDeletedDocuments();
        Assert.Equal(2, bin.Count);
    }

    [Fact]
    public void BulkOperations_ChainedFlow()
    {
        // Create mixed docs
        _repo.Add(new StudyDocument { Ten = "A", MonHoc = "Math", QuanTrong = false });
        _repo.Add(new StudyDocument { Ten = "B", MonHoc = "Math", QuanTrong = false });
        _repo.Add(new StudyDocument { Ten = "C", MonHoc = "Science", QuanTrong = false });

        var all = _repo.GetAll().OrderBy(d => d.Ten).ToList();
        var mathIds = all.Where(d => d.MonHoc == "Math").Select(d => d.Id).ToList();

        // Step 1: Mark math docs as important
        int marked = DatabaseHelper.BulkToggleImportant(mathIds, true);
        Assert.Equal(2, marked);
        Assert.Equal(2, _repo.GetAll().Count(d => d.QuanTrong));

        // Step 2: Move math docs to different subject
        DatabaseHelper.BulkUpdateSubject(mathIds, "Physics");
        Assert.Equal(2, _repo.GetAll().Count(d => d.MonHoc == "Physics"));
        Assert.Equal(0, _repo.GetAll().Count(d => d.MonHoc == "Math"));

        // Step 3: Bulk soft delete the physics docs
        int deleted = DatabaseHelper.BulkSoftDelete(mathIds);
        Assert.Equal(2, deleted);
        Assert.Single(_repo.GetAll());
        Assert.Equal("C", _repo.GetAll()[0].Ten);
    }
}

// ════════════════════════════════════════════════════════════
// Integration Flow #3: Duplicate Detection Logic
// (Tests the grouping/detection algorithm without MD5 — data layer)
// ════════════════════════════════════════════════════════════

public class DuplicateDetectionDataTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public DuplicateDetectionDataTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void SamePath_MultipleDocuments_CanBeIdentified()
    {
        // Simulate what DuplicateDetectionForm would find:
        // docs with same duong_dan are potential duplicates
        _repo.Add(new StudyDocument { Ten = "Copy 1", DuongDan = @"C:\same_file.pdf" });
        _repo.Add(new StudyDocument { Ten = "Copy 2", DuongDan = @"C:\same_file.pdf" });
        _repo.Add(new StudyDocument { Ten = "Unique", DuongDan = @"C:\different.pdf" });

        var allDocs = _repo.GetAll();

        // Group by path — application logic would do MD5 but here we test the data
        var groupedByPath = allDocs
            .Where(d => !string.IsNullOrEmpty(d.DuongDan))
            .GroupBy(d => d.DuongDan)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Single(groupedByPath); // One group with duplicate
        Assert.Equal(2, groupedByPath[0].Count());
    }

    [Fact]
    public void BulkSoftDelete_DuplicateGroup_RemovesSelectedOnly()
    {
        _repo.Add(new StudyDocument { Ten = "Original", DuongDan = @"C:\dup.pdf" });
        _repo.Add(new StudyDocument { Ten = "Duplicate", DuongDan = @"C:\dup.pdf" });

        var all = _repo.GetAll();
        // Keep the first one, delete the duplicate
        var keepId = all.First(d => d.Ten == "Original").Id;
        var dupId = all.First(d => d.Ten == "Duplicate").Id;

        DatabaseHelper.BulkSoftDelete(new List<int> { dupId });

        var remaining = _repo.GetAll();
        Assert.Single(remaining);
        Assert.Equal(keepId, remaining[0].Id);
    }
}

// ════════════════════════════════════════════════════════════
// Integration Flow #4: Batch Import Simulation
// (Tests the data layer of batch import process)
// ════════════════════════════════════════════════════════════

public class BatchImportSimulationTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public BatchImportSimulationTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void BatchInsert_MultipleDocuments_AllImported()
    {
        // Simulate BatchImportForm inserting multiple docs at once
        var filesToImport = new[]
        {
            new StudyDocument { Ten = "lecture1.pdf", Loai = "Tài liệu", MonHoc = "Học tập", DuongDan = @"C:\docs\lecture1.pdf", KichThuoc = 2.5 },
            new StudyDocument { Ten = "video1.mp4", Loai = "Video", MonHoc = "Học tập", DuongDan = @"C:\docs\video1.mp4", KichThuoc = 150.0 },
            new StudyDocument { Ten = "notes.docx", Loai = "Tài liệu", MonHoc = "Công việc", DuongDan = @"C:\docs\notes.docx", KichThuoc = 0.8 },
        };

        int importedCount = 0;
        foreach (var file in filesToImport)
        {
            if (_repo.Add(file)) importedCount++;
        }

        Assert.Equal(3, importedCount);
        Assert.Equal(3, _repo.GetAll().Count);
    }

    [Fact]
    public void BatchInsert_WithSameSubject_StatsUpdateCorrectly()
    {
        _repo.Add(new StudyDocument { Ten = "F1", MonHoc = "Học tập", Loai = "Tài liệu" });
        _repo.Add(new StudyDocument { Ten = "F2", MonHoc = "Học tập", Loai = "Video" });
        _repo.Add(new StudyDocument { Ten = "F3", MonHoc = "Công việc", Loai = "Tài liệu" });

        var stats = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(3, stats.TotalDocuments);
        Assert.Equal(2, stats.TotalCategories); // Học tập, Công việc

        var bySubject = DatabaseHelper.GetDocumentsBySubject();
        var hocTap = bySubject.FirstOrDefault(s => s.Label == "Học tập");
        Assert.Equal(2, hocTap.Count);
    }

    [Theory]
    [InlineData("file.pdf", "PDF")]
    [InlineData("file.doc", "Word")]
    [InlineData("file.docx", "Word")]
    [InlineData("file.xls", "Excel")]
    [InlineData("file.xlsx", "Excel")]
    [InlineData("file.ppt", "PowerPoint")]
    [InlineData("file.pptx", "PowerPoint")]
    [InlineData("file.jpg", "Hình ảnh")]
    [InlineData("file.png", "Hình ảnh")]
    [InlineData("file.mp4", "Video")]
    [InlineData("file.avi", "Video")]
    [InlineData("file.mp3", "Audio")]
    [InlineData("file.zip", "Nén")]
    [InlineData("file.txt", "Text")]
    [InlineData("file.cs", "Code")]
    [InlineData("file.py", "Code")]
    public void DetectFileType_ByExtension_ReturnsCorrectLabel(string filename, string expectedType)
    {
        // Test the file type detection logic (simulating BatchImportForm.DetectFileType)
        string ext = Path.GetExtension(filename).ToLower();
        string detected = DetectFileType(ext);
        Assert.Equal(expectedType, detected);
    }

    // Replicates the logic from BatchImportForm.cs
    private static string DetectFileType(string ext) => ext switch
    {
        ".pdf" => "PDF",
        ".doc" or ".docx" => "Word",
        ".xls" or ".xlsx" => "Excel",
        ".ppt" or ".pptx" => "PowerPoint",
        ".txt" => "Text",
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "Hình ảnh",
        ".mp4" or ".avi" or ".mkv" or ".mov" => "Video",
        ".mp3" or ".wav" or ".flac" => "Audio",
        ".zip" or ".rar" or ".7z" => "Nén",
        ".html" or ".htm" => "HTML",
        ".cs" or ".java" or ".py" or ".js" or ".ts" => "Code",
        _ => ext.TrimStart('.').ToUpper()
    };
}

// ════════════════════════════════════════════════════════════
// Integration Flow #5: Search & Filter Pipeline
// ════════════════════════════════════════════════════════════

public class SearchFilterPipelineTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public SearchFilterPipelineTests() { _repo = new DocumentRepository(); }

    private void SeedComplexData()
    {
        _repo.Add(new StudyDocument
        {
            Ten = "Python Programming Guide",
            MonHoc = "Công nghệ",
            Loai = "Tài liệu",
            KichThuoc = 3.5,
            QuanTrong = true,
            TacGia = "John Doe",
            Tags = "python;programming;guide",
            Deadline = DateTime.Today.AddDays(5)
        });
        _repo.Add(new StudyDocument
        {
            Ten = "Java Design Patterns",
            MonHoc = "Công nghệ",
            Loai = "Tài liệu",
            KichThuoc = 5.0,
            QuanTrong = false,
            TacGia = "Jane Smith",
            Tags = "java;patterns;oop"
        });
        _repo.Add(new StudyDocument
        {
            Ten = "Machine Learning Video",
            MonHoc = "AI",
            Loai = "Video",
            KichThuoc = 250.0,
            QuanTrong = true,
            Tags = "ml;ai;python"
        });
        _repo.Add(new StudyDocument
        {
            Ten = "Data Structures Notes",
            MonHoc = "Công nghệ",
            Loai = "Tài liệu",
            KichThuoc = 1.2,
            QuanTrong = false,
            Deadline = DateTime.Today.AddDays(-3) // OVERDUE
        });
    }

    [Fact]
    public void Search_ByAuthor_FindsCorrectDoc()
    {
        SeedComplexData();
        var results = _repo.Search("John Doe");
        Assert.Single(results);
        Assert.Equal("Python Programming Guide", results[0].Ten);
    }

    [Fact]
    public void Filter_BySubjectAndType_NarrowsResults()
    {
        SeedComplexData();
        var results = _repo.Filter("Công nghệ", "Tài liệu");
        Assert.Equal(3, results.Count);
        Assert.All(results, d => Assert.Equal("Công nghệ", d.MonHoc));
        Assert.All(results, d => Assert.Equal("Tài liệu", d.Loai));
    }

    [Fact]
    public void AdvancedSearch_ImportantOnlyAndMinSize_CombinesCorrectly()
    {
        SeedComplexData();
        // Should return: Python Guide (important=true, size=3.5) and ML Video (important=true, size=250)
        var results = _repo.SearchAdvanced("", "", "", null, null, 3.0, null, true);
        Assert.Equal(2, results.Count);
        Assert.All(results, d => Assert.True(d.QuanTrong));
        Assert.All(results, d => Assert.True(d.KichThuoc >= 3.0));
    }

    [Fact]
    public void OverdueDocuments_CorrectlyIdentified()
    {
        SeedComplexData();
        var overdue = _repo.GetOverdueDocuments();
        Assert.Single(overdue);
        Assert.Equal("Data Structures Notes", overdue[0].Ten);
    }

    [Fact]
    public void UpcomingDeadlines_Within7Days_IncludesCorrectDoc()
    {
        SeedComplexData();
        var upcoming = _repo.GetUpcomingDeadlines(7);
        Assert.Single(upcoming);
        Assert.Equal("Python Programming Guide", upcoming[0].Ten);
    }

    [Fact]
    public void GetDistinctSubjects_ReflectsSeededData()
    {
        SeedComplexData();
        var subjects = _repo.GetDistinctSubjects();
        Assert.Contains("Công nghệ", subjects);
        Assert.Contains("AI", subjects);
    }

    [Fact]
    public void GetDistinctTags_SplitsSemicolonSeparated()
    {
        SeedComplexData();
        var tags = _repo.GetDistinctTags();
        Assert.Contains("python", tags);  // appears in 2 docs, but distinct
        Assert.Contains("java", tags);
        Assert.Contains("ml", tags);
        Assert.Contains("patterns", tags);

        // No duplicates
        Assert.Equal(tags.Distinct().Count(), tags.Count);
    }
}

// ════════════════════════════════════════════════════════════
// Integration Flow #6: Recycle Bin + Bulk Ops Interaction
// ════════════════════════════════════════════════════════════

public class RecycleBinBulkInteractionTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public RecycleBinBulkInteractionTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void BulkDelete_ThenRestoreSelectively_ThenEmptyBin()
    {
        _repo.Add(new StudyDocument { Ten = "Keep A" });
        _repo.Add(new StudyDocument { Ten = "Delete B" });
        _repo.Add(new StudyDocument { Ten = "Delete C" });

        var all = _repo.GetAll().OrderBy(d => d.Ten).ToList();
        var deleteIds = all.Where(d => d.Ten != "Keep A").Select(d => d.Id).ToList();

        // Bulk delete B, C
        int deleted = DatabaseHelper.BulkSoftDelete(deleteIds);
        Assert.Equal(2, deleted);
        Assert.Single(_repo.GetAll());
        Assert.Equal(2, DatabaseHelper.GetDeletedDocumentCount());

        // Restore B only
        int bId = all.First(d => d.Ten == "Delete B").Id;
        DatabaseHelper.RestoreDocument(bId);
        Assert.Equal(2, _repo.GetAll().Count);
        Assert.Equal(1, DatabaseHelper.GetDeletedDocumentCount());

        // Empty remaining bin (only C)
        int emptied = DatabaseHelper.EmptyRecycleBin();
        Assert.Equal(1, emptied);
        Assert.Equal(0, DatabaseHelper.GetDeletedDocumentCount());

        // Final active count
        Assert.Equal(2, _repo.GetAll().Count);
    }

    [Fact]
    public void MultipleSoftDeletes_SameDocument_OnlyOneInBin()
    {
        _repo.Add(new StudyDocument { Ten = "Test Double Delete" });
        int id = _repo.GetAll()[0].Id;

        // First soft delete
        _repo.Delete(id);
        Assert.Equal(1, DatabaseHelper.GetDeletedDocumentCount());

        // Second soft delete — should be no-op (already deleted)
        _repo.Delete(id);
        Assert.Equal(1, DatabaseHelper.GetDeletedDocumentCount()); // Still just 1
    }
}

// ════════════════════════════════════════════════════════════
// Integration Flow #7: Collection Isolation between Collections
// ════════════════════════════════════════════════════════════

public class CollectionIsolationTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public CollectionIsolationTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void TwoCollections_DoNotShareDocuments()
    {
        _repo.Add(new StudyDocument { Ten = "Shared?" });
        _repo.Add(new StudyDocument { Ten = "Exclusive" });
        var docs = _repo.GetAll().OrderBy(d => d.Id).ToList();

        int col1 = DatabaseHelper.CreateCollection("Collection Alpha");
        int col2 = DatabaseHelper.CreateCollection("Collection Beta");

        // Add doc[0] to col1 only, doc[1] to col2 only
        DatabaseHelper.AddDocumentToCollection(col1, docs[0].Id);
        DatabaseHelper.AddDocumentToCollection(col2, docs[1].Id);

        var col1Docs = DatabaseHelper.GetDocumentsInCollection(col1);
        var col2Docs = DatabaseHelper.GetDocumentsInCollection(col2);

        Assert.Single(col1Docs);
        Assert.Single(col2Docs);
        Assert.Equal(docs[0].Id, col1Docs[0].Id);
        Assert.Equal(docs[1].Id, col2Docs[0].Id);
    }

    [Fact]
    public void SameDocument_CanBeInMultipleCollections()
    {
        _repo.Add(new StudyDocument { Ten = "Multi-Collection Doc" });
        int docId = _repo.GetAll()[0].Id;

        int col1 = DatabaseHelper.CreateCollection("Alpha");
        int col2 = DatabaseHelper.CreateCollection("Beta");

        DatabaseHelper.AddDocumentToCollection(col1, docId);
        DatabaseHelper.AddDocumentToCollection(col2, docId);

        Assert.Single(DatabaseHelper.GetDocumentsInCollection(col1));
        Assert.Single(DatabaseHelper.GetDocumentsInCollection(col2));
    }

    [Fact]
    public void DeleteCollection_DoesNotDeleteDocuments()
    {
        _repo.Add(new StudyDocument { Ten = "Safe Doc" });
        int docId = _repo.GetAll()[0].Id;

        int colId = DatabaseHelper.CreateCollection("To Delete");
        DatabaseHelper.AddDocumentToCollection(colId, docId);

        DatabaseHelper.DeleteCollection(colId);

        // Collection gone
        Assert.Empty(DatabaseHelper.GetCollections());
        // Document still exists
        Assert.Single(_repo.GetAll());
        Assert.Equal("Safe Doc", _repo.GetAll()[0].Ten);
    }

    [Fact]
    public void AddDocumentToCollection_Duplicate_ReturnsFalse()
    {
        _repo.Add(new StudyDocument { Ten = "Dup Test" });
        int docId = _repo.GetAll()[0].Id;
        int colId = DatabaseHelper.CreateCollection("DupCheck");

        bool first = DatabaseHelper.AddDocumentToCollection(colId, docId);
        bool second = DatabaseHelper.AddDocumentToCollection(colId, docId); // duplicate

        Assert.True(first);
        Assert.False(second); // Duplicate returns false
        Assert.Single(DatabaseHelper.GetDocumentsInCollection(colId));
    }
}

// ════════════════════════════════════════════════════════════
// Integration Flow #8: Statistics Consistency Across Operations
// ════════════════════════════════════════════════════════════

public class StatisticsConsistencyTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public StatisticsConsistencyTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void Stats_ReflectImmediate_AfterEachOperation()
    {
        // Empty state
        var s0 = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(0, s0.TotalDocuments);
        Assert.Equal(0, s0.ImportantDocuments);
        Assert.Equal(0, s0.NoFileDocuments);

        // Add 3 docs: 2 important, 1 without file
        _repo.Add(new StudyDocument { Ten = "A", QuanTrong = true, DuongDan = @"C:\a.pdf" });
        _repo.Add(new StudyDocument { Ten = "B", QuanTrong = true, DuongDan = @"C:\b.pdf" });
        _repo.Add(new StudyDocument { Ten = "C", QuanTrong = false, DuongDan = "" }); // no file

        var s1 = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(3, s1.TotalDocuments);
        Assert.Equal(2, s1.ImportantDocuments);
        Assert.Equal(1, s1.NoFileDocuments);

        // Delete one important doc
        int aId = _repo.GetAll().First(d => d.Ten == "A").Id;
        _repo.Delete(aId);

        var s2 = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(2, s2.TotalDocuments);
        Assert.Equal(1, s2.ImportantDocuments);

        // Add collection
        DatabaseHelper.CreateCollection("My Col");
        var s3 = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(1, s3.TotalCollections);
    }

    [Fact]
    public void TotalDocumentCount_MatchesDashboardStats()
    {
        _repo.Add(new StudyDocument { Ten = "X" });
        _repo.Add(new StudyDocument { Ten = "Y" });

        var stats = DatabaseHelper.GetDashboardStatistics();
        int countMethod = DatabaseHelper.GetTotalDocumentCount();

        Assert.Equal(stats.TotalDocuments, countMethod);
    }

    [Fact]
    public void Stats_OverdueDocuments_ExcludesRestoredOnes()
    {
        _repo.Add(new StudyDocument { Ten = "Overdue", Deadline = DateTime.Today.AddDays(-2) });
        int id = _repo.GetAll()[0].Id;

        var s1 = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(1, s1.OverdueDocuments);

        // Remove deadline by update
        var doc = _repo.GetAll()[0];
        doc.Deadline = null;
        _repo.Update(doc);

        var s2 = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(0, s2.OverdueDocuments);
    }
}

// ════════════════════════════════════════════════════════════
// Integration Flow #9: GetDistinctTags case-insensitive dedup
// ════════════════════════════════════════════════════════════

public class TagsNormalizationTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public TagsNormalizationTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void GetDistinctTags_CaseInsensitive_Deduped()
    {
        // "Python" and "python" should deduplicate to 1 entry
        _repo.Add(new StudyDocument { Ten = "A", Tags = "Python;AI" });
        _repo.Add(new StudyDocument { Ten = "B", Tags = "python;machine-learning" });

        var tags = _repo.GetDistinctTags();

        // Tags are lowercased and deduped
        Assert.Contains("python", tags);
        Assert.DoesNotContain("Python", tags); // Should be converted to lowercase

        // No duplicate "python"
        Assert.Equal(1, tags.Count(t => t == "python"));
    }

    [Fact]
    public void GetDistinctTags_TrimsWhitespace()
    {
        _repo.Add(new StudyDocument { Ten = "A", Tags = " java ; spring ; web " });
        var tags = _repo.GetDistinctTags();

        Assert.Contains("java", tags);
        Assert.Contains("spring", tags);
        Assert.Contains("web", tags);
        Assert.DoesNotContain(" java ", tags); // No extra spaces
    }

    [Fact]
    public void Tags_SortedAlphabetically()
    {
        _repo.Add(new StudyDocument { Ten = "A", Tags = "zebra;apple;mango" });
        var tags = _repo.GetDistinctTags();

        // Should be sorted
        Assert.Equal(tags.OrderBy(t => t).ToList(), tags);
    }
}

// ════════════════════════════════════════════════════════════
// Integration Flow #10: Recent Files — 20 Item Limit
// ════════════════════════════════════════════════════════════

public class RecentFilesLimitTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public RecentFilesLimitTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void RecentFiles_CapAt20_OldestRemoved()
    {
        // Create 25 docs and add all to recent
        for (int i = 1; i <= 25; i++)
            _repo.Add(new StudyDocument { Ten = $"Doc {i:D2}" });

        var allDocs = _repo.GetAll().OrderBy(d => d.Id).ToList();

        // Add first 5 docs with a 1.2s gap so they get an older timestamp
        // SQLite datetime resolution is 1 second, so we need >1s between batches
        foreach (var doc in allDocs.Take(5))
            DatabaseHelper.AddRecentFile(doc.Id);

        System.Threading.Thread.Sleep(1200); // Ensure 2nd batch has newer timestamp

        // Add the remaining 20 docs (these should be kept)
        foreach (var doc in allDocs.Skip(5))
            DatabaseHelper.AddRecentFile(doc.Id);

        var recent = DatabaseHelper.GetRecentFiles();

        // Should be capped at 20 (LIMIT 20 in SQL)
        Assert.Equal(20, recent.Count);

        // All returned items should be from the newer batch (docs 06-25)
        // Tuple structure: (int Id, string Ten, ...)
        var recentNames = recent.Select(r => r.Ten).ToList();
        Assert.DoesNotContain("Doc 01", recentNames);
        Assert.DoesNotContain("Doc 05", recentNames);
        Assert.Contains("Doc 25", recentNames);
        Assert.Contains("Doc 06", recentNames);
    }

    [Fact]
    public void RecentFiles_GetRecentFiles_ReturnsTupleWithCorrectFields()
    {
        // Verify the tuple structure returned by GetRecentFiles
        _repo.Add(new StudyDocument { Ten = "Test File", MonHoc = "Học tập", Loai = "PDF", DuongDan = @"C:\test.pdf" });
        int docId = _repo.GetAll()[0].Id;
        DatabaseHelper.AddRecentFile(docId);

        var recent = DatabaseHelper.GetRecentFiles();
        Assert.Single(recent);

        var item = recent[0];
        Assert.Equal(docId, item.Id);
        Assert.Equal("Test File", item.Ten);
        Assert.Equal("Học tập", item.MonHoc);
        Assert.Equal("PDF", item.Loai);
        Assert.Equal(@"C:\test.pdf", item.DuongDan);
        Assert.True(item.OpenedAt > DateTime.MinValue);
    }


}

// ════════════════════════════════════════════════════════════
// IDocumentRepository Contract Tests
// Ensures DocumentRepository implements contract correctly
// ════════════════════════════════════════════════════════════

public class DocumentRepositoryContractTests : DatabaseTestBase
{
    private readonly IDocument _repo;

    public DocumentRepositoryContractTests()
    {
        _repo = new DocumentRepository();
    }

    [Fact]
    public void GetAll_EmptyDb_ReturnsEmptyList()
    {
        var result = _repo.GetAll();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void GetById_NonExistentId_ReturnsNull()
    {
        var result = _repo.GetById(99999);
        Assert.Null(result);
    }

    [Fact]
    public void Search_NoMatches_ReturnsEmptyList()
    {
        _repo.Add(new StudyDocument { Ten = "Something" });
        var result = _repo.Search("xyzxyzxyz_noexist");
        Assert.Empty(result);
    }

    [Fact]
    public void Add_ReturnsTrue_OnSuccess()
    {
        bool result = _repo.Add(new StudyDocument { Ten = "Contract Test" });
        Assert.True(result);
    }

    [Fact]
    public void Update_NonExistentDocument_ReturnsFalse()
    {
        var ghost = new StudyDocument { Id = 99999, Ten = "Ghost" };
        bool result = _repo.Update(ghost);
        Assert.False(result);
    }

    [Fact]
    public void Delete_NonExistentId_ReturnsFalse()
    {
        bool result = _repo.Delete(99999);
        Assert.False(result);
    }

    [Fact]
    public void GetDistinctSubjects_EmptyDb_ReturnsEmptyList()
    {
        var result = _repo.GetDistinctSubjects();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void GetUpcomingDeadlines_NoDeadlines_ReturnsEmpty()
    {
        _repo.Add(new StudyDocument { Ten = "No Deadline", Deadline = null });
        var result = _repo.GetUpcomingDeadlines(7);
        Assert.Empty(result);
    }

    [Fact]
    public void GetOverdueDocuments_EmptyDb_ReturnsEmpty()
    {
        var result = _repo.GetOverdueDocuments();
        Assert.Empty(result);
    }
}
