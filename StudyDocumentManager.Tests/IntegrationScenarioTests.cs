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
        // 初期化後のデフォルトカテゴリ確認
        var subjects = Db.GetAllSubjects();
        Assert.Contains("Study", subjects);
        Assert.Contains("Work", subjects);
        Assert.Contains("Personal", subjects);
        Assert.Contains("Other", subjects);
    }

    [Fact]
    public void InitializeDatabase_SeedsDefaultTypes()
    {
        // 初期化後のデフォルトタイプ確認
        var types = Db.GetAllTypes();
        Assert.Contains("Document", types);
        Assert.Contains("Report", types);
        Assert.Contains("Image", types);
        Assert.Contains("Video", types);
        Assert.Contains("Other", types);
    }

    [Fact]
    public void InitializeDatabase_DefaultSubjectCount_AtLeast8()
    {
        // FEATURES_OLD.md specifies 8 default subject categories
        var subjects = Db.GetAllSubjects();
        Assert.True(subjects.Count >= 8, $"Expected >= 8 default subjects, got {subjects.Count}");
    }

    [Fact]
    public void InitializeDatabase_DefaultTypeCount_AtLeast7()
    {
        // Default types: Document, Report, Guide, Form, Image, Video, Other
        var types = Db.GetAllTypes();
        Assert.True(types.Count >= 7, $"Expected >= 7 default types, got {types.Count}");
    }

    [Fact]
    public void InitializeDatabase_AllTablesCreated()
    {
        // Verify all 7 tables exist by performing lightweight queries
        // If tables didn't exist these would throw SqliteException
        var ex = Record.Exception(() =>
        {
            Db.GetAllDocuments();
            Db.GetCollections();
            Db.GetRecentFiles();
            Db.GetDeletedDocuments();
            Db.GetAllSubjects();
            Db.GetAllTypes();
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

    public FullDocumentLifecycleTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void FullLifecycle_CreateUpdateSearchDeleteRestore()
    {
        // 1. ADD
        var doc = new StudyDocument
        {
            Name = "Lifecycle Test",
            Subject = "Study",
            Type = "Document",
            Tags = "test;lifecycle",
            IsImportant = false,
            Deadline = DateTime.Today.AddDays(5)
        };
        bool added = _repo.Add(doc);
        Assert.True(added);
        Assert.Equal(1, _repo.GetAll().Count);

        // 2. GETBYID
        int id = _repo.GetAll()[0].Id;
        var fetched = _repo.GetById(id);
        Assert.NotNull(fetched);
        Assert.Equal("Lifecycle Test", fetched!.Name);

        // 3. SEARCH
        var searchResults = _repo.Search("Lifecycle");
        Assert.Single(searchResults);

        // 4. UPDATE
        fetched.Name = "Updated Lifecycle";
        fetched.IsImportant = true;
        fetched.Deadline = DateTime.Today.AddDays(10);
        bool updated = _repo.Update(fetched);
        Assert.True(updated);

        var afterUpdate = _repo.GetById(id)!;
        Assert.Equal("Updated Lifecycle", afterUpdate.Name);
        Assert.True(afterUpdate.IsImportant);
        Assert.Equal(DateTime.Today.AddDays(10).Date, afterUpdate.Deadline!.Value.Date);

        // 5. SOFT DELETE
        bool deleted = _repo.Delete(id);
        Assert.True(deleted);
        Assert.Empty(_repo.GetAll());

        // 6. VERIFY IN RECYCLE BIN
        var bin = Db.GetDeletedDocuments();
        Assert.Single(bin);
        Assert.Equal("Updated Lifecycle", bin[0].Name);
        Assert.Equal(1, Db.GetDeletedDocumentCount());

        // 7. RESTORE
        Db.RestoreDocument(id);
        Assert.Single(_repo.GetAll());
        Assert.Empty(Db.GetDeletedDocuments());

        // 8. PERMANENT DELETE
        _repo.Delete(id);
        Db.PermanentDeleteDocument(id);
        Assert.Empty(_repo.GetAll());
        Assert.Empty(Db.GetDeletedDocuments());
    }

    [Fact]
    public void FullLifecycle_WithCollection_AndRelations_AndNotes()
    {
        // Multi-feature integration

        // Create docs
        _repo.Add(new StudyDocument { Name = "Main Doc", Subject = "Study" });
        _repo.Add(new StudyDocument { Name = "Related Doc", Subject = "Study" });
        var docs = _repo.GetAll().OrderBy(d => d.Id).ToList();
        int mainId = docs[0].Id;
        int relatedId = docs[1].Id;

        // Add personal note to main doc
        Db.SavePersonalNote(mainId, "Note for main document");
        Assert.Equal("Note for main document", Db.GetPersonalNote(mainId));

        // Link as related documents
        Db.AddDocumentRelation(mainId, relatedId, "Exercise");
        var related = Db.GetRelatedDocuments(mainId);
        Assert.Single(related);
        Assert.Equal("Exercise", related[0].RelationType);

        // Create collection and add both
        int colId = Db.CreateCollection("My Study Collection");
        Db.AddDocumentToCollection(colId, mainId);
        Db.AddDocumentToCollection(colId, relatedId);
        Assert.Equal(2, Db.GetDocumentsInCollection(colId).Count);

        // Track as recently opened
        Db.AddRecentFile(mainId);
        Assert.Single(Db.GetRecentFiles());

        // Now soft-delete the related doc
        _repo.Delete(relatedId);

        // Recent files only shows active docs
        Assert.Single(Db.GetRecentFiles());

        // Related docs also excludes soft-deleted
        var stillRelated = Db.GetRelatedDocuments(mainId);
        Assert.Empty(stillRelated); // related doc was deleted

        // Collection also excludes soft-deleted
        Assert.Single(Db.GetDocumentsInCollection(colId));

        // Stats reflect changes
        var stats = Db.GetDashboardStatistics();
        Assert.Equal(1, stats.TotalDocuments);
    }
}

// ════════════════════════════════════════════════════════════
// Integration Flow #2: Category Management End-to-End
// ════════════════════════════════════════════════════════════

public class CategoryManagementE2ETests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public CategoryManagementE2ETests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void RenameSubject_UpdatesAllDocuments_AndLookupTable()
    {
        // Setup: add subject and assign to docs
        Db.AddSubject("Old Category");
        _repo.Add(new StudyDocument { Name = "Doc A", Subject = "Old Category" });
        _repo.Add(new StudyDocument { Name = "Doc B", Subject = "Old Category" });
        _repo.Add(new StudyDocument { Name = "Doc C", Subject = "Different" });

        // Action: rename
        Db.UpdateSubjectName("Old Category", "New Category");

        // Verify: all docs with old name updated
        var docs = _repo.GetAll();
        Assert.Equal(2, docs.Count(d => d.Subject == "New Category"));
        Assert.Equal(1, docs.Count(d => d.Subject == "Different"));

        // Lookup table updated
        var subjects = Db.GetAllSubjects();
        Assert.Contains("New Category", subjects);
        Assert.DoesNotContain("Old Category", subjects);

        // GetDistinctSubjects also reflects change
        var distinct = _repo.GetDistinctSubjects();
        Assert.Contains("New Category", distinct);
    }

    [Fact]
    public void DeleteDocumentsBySubject_MovesToRecycleBin_NotPermanent()
    {
        Db.AddSubject("Xóa danh mục này");
        _repo.Add(new StudyDocument { Name = "Subject Doc 1", Subject = "Xóa danh mục này" });
        _repo.Add(new StudyDocument { Name = "Subject Doc 2", Subject = "Xóa danh mục này" });
        _repo.Add(new StudyDocument { Name = "Keep This", Subject = "Giữ lại" });

        Db.DeleteDocumentsBySubject("Xóa danh mục này");

        // Active docs: only "Keep This"
        var active = _repo.GetAll();
        Assert.Single(active);
        Assert.Equal("Keep This", active[0].Name);

        // Deleted docs in bin: the 2 subject docs
        var bin = Db.GetDeletedDocuments();
        Assert.Equal(2, bin.Count);
    }

    [Fact]
    public void BulkOperations_ChainedFlow()
    {
        // Create mixed docs
        _repo.Add(new StudyDocument { Name = "A", Subject = "Math", IsImportant = false });
        _repo.Add(new StudyDocument { Name = "B", Subject = "Math", IsImportant = false });
        _repo.Add(new StudyDocument { Name = "C", Subject = "Science", IsImportant = false });

        var all = _repo.GetAll().OrderBy(d => d.Name).ToList();
        var mathIds = all.Where(d => d.Subject == "Math").Select(d => d.Id).ToList();

        // Step 1: Mark math docs as important
        int marked = Db.BulkToggleImportant(mathIds, true);
        Assert.Equal(2, marked);
        Assert.Equal(2, _repo.GetAll().Count(d => d.IsImportant));

        // Step 2: Move math docs to different subject
        Db.BulkUpdateSubject(mathIds, "Physics");
        Assert.Equal(2, _repo.GetAll().Count(d => d.Subject == "Physics"));
        Assert.Equal(0, _repo.GetAll().Count(d => d.Subject == "Math"));

        // Step 3: Bulk soft delete the physics docs
        int deleted = Db.BulkSoftDelete(mathIds);
        Assert.Equal(2, deleted);
        Assert.Single(_repo.GetAll());
        Assert.Equal("C", _repo.GetAll()[0].Name);
    }
}

// ════════════════════════════════════════════════════════════
// Integration Flow #3: Duplicate Detection Logic
// (Tests the grouping/detection algorithm without MD5 — data layer)
// ════════════════════════════════════════════════════════════

public class DuplicateDetectionDataTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public DuplicateDetectionDataTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void SamePath_MultipleDocuments_CanBeIdentified()
    {
        // Simulate what DuplicateDetectionForm would find:
        // 同じFilePathを持つ文書は重複候補
        _repo.Add(new StudyDocument { Name = "Copy 1", FilePath = @"C:\same_file.pdf" });
        _repo.Add(new StudyDocument { Name = "Copy 2", FilePath = @"C:\same_file.pdf" });
        _repo.Add(new StudyDocument { Name = "Unique", FilePath = @"C:\different.pdf" });

        var allDocs = _repo.GetAll();

        // Group by path — application logic would do MD5 but here we test the data
        var groupedByPath = allDocs
            .Where(d => !string.IsNullOrEmpty(d.FilePath))
            .GroupBy(d => d.FilePath)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Single(groupedByPath); // One group with duplicate
        Assert.Equal(2, groupedByPath[0].Count());
    }

    [Fact]
    public void BulkSoftDelete_DuplicateGroup_RemovesSelectedOnly()
    {
        _repo.Add(new StudyDocument { Name = "Original", FilePath = @"C:\dup.pdf" });
        _repo.Add(new StudyDocument { Name = "Duplicate", FilePath = @"C:\dup.pdf" });

        var all = _repo.GetAll();
        // Keep the first one, delete the duplicate
        var keepId = all.First(d => d.Name == "Original").Id;
        var dupId = all.First(d => d.Name == "Duplicate").Id;

        Db.BulkSoftDelete(new List<int> { dupId });

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

    public BatchImportSimulationTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void BatchInsert_MultipleDocuments_AllImported()
    {
        // Simulate BatchImportForm inserting multiple docs at once
        var filesToImport = new[]
        {
            new StudyDocument { Name = "lecture1.pdf", Type = "Document", Subject = "Study", FilePath = @"C:\docs\lecture1.pdf", FileSize = 2.5 },
            new StudyDocument { Name = "video1.mp4", Type = "Video", Subject = "Study", FilePath = @"C:\docs\video1.mp4", FileSize = 150.0 },
            new StudyDocument { Name = "notes.docx", Type = "Document", Subject = "Work", FilePath = @"C:\docs\notes.docx", FileSize = 0.8 },
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
        _repo.Add(new StudyDocument { Name = "F1", Subject = "Study", Type = "Document" });
        _repo.Add(new StudyDocument { Name = "F2", Subject = "Study", Type = "Video" });
        _repo.Add(new StudyDocument { Name = "F3", Subject = "Work", Type = "Document" });

        var stats = Db.GetDashboardStatistics();
        Assert.Equal(3, stats.TotalDocuments);
        Assert.Equal(2, stats.TotalCategories); // Study, Work

        var bySubject = Db.GetDocumentsBySubject();
        var studySubject = bySubject.FirstOrDefault(s => s.Label == "Study");
        Assert.Equal(2, studySubject.Count);
    }

    [Theory]
    [InlineData("file.pdf", "PDF")]
    [InlineData("file.doc", "Word")]
    [InlineData("file.docx", "Word")]
    [InlineData("file.xls", "Excel")]
    [InlineData("file.xlsx", "Excel")]
    [InlineData("file.ppt", "PowerPoint")]
    [InlineData("file.pptx", "PowerPoint")]
    [InlineData("file.jpg", "Image")]
    [InlineData("file.png", "Image")]
    [InlineData("file.mp4", "Video")]
    [InlineData("file.avi", "Video")]
    [InlineData("file.mp3", "Audio")]
    [InlineData("file.zip", "Archive")]
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
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "Image",
        ".mp4" or ".avi" or ".mkv" or ".mov" => "Video",
        ".mp3" or ".wav" or ".flac" => "Audio",
        ".zip" or ".rar" or ".7z" => "Archive",
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

    public SearchFilterPipelineTests() { _repo = new DocumentRepository(Db); }

    private void SeedComplexData()
    {
        _repo.Add(new StudyDocument
        {
            Name = "Python Programming Guide",
            Subject = "Technology",
            Type = "Document",
            FileSize = 3.5,
            IsImportant = true,
            Author = "John Doe",
            Tags = "python;programming;guide",
            Deadline = DateTime.Today.AddDays(5)
        });
        _repo.Add(new StudyDocument
        {
            Name = "Java Design Patterns",
            Subject = "Technology",
            Type = "Document",
            FileSize = 5.0,
            IsImportant = false,
            Author = "Jane Smith",
            Tags = "java;patterns;oop"
        });
        _repo.Add(new StudyDocument
        {
            Name = "Machine Learning Video",
            Subject = "AI",
            Type = "Video",
            FileSize = 250.0,
            IsImportant = true,
            Tags = "ml;ai;python"
        });
        _repo.Add(new StudyDocument
        {
            Name = "Data Structures Notes",
            Subject = "Technology",
            Type = "Document",
            FileSize = 1.2,
            IsImportant = false,
            Deadline = DateTime.Today.AddDays(-3) // OVERDUE
        });
    }

    [Fact]
    public void Search_ByAuthor_FindsCorrectDoc()
    {
        SeedComplexData();
        var results = _repo.Search("John Doe");
        Assert.Single(results);
        Assert.Equal("Python Programming Guide", results[0].Name);
    }

    [Fact]
    public void Filter_BySubjectAndType_NarrowsResults()
    {
        SeedComplexData();
        var results = _repo.Filter("Technology", "Document");
        Assert.Equal(3, results.Count);
        Assert.All(results, d => Assert.Equal("Technology", d.Subject));
        Assert.All(results, d => Assert.Equal("Document", d.Type));
    }

    [Fact]
    public void AdvancedSearch_ImportantOnlyAndMinSize_CombinesCorrectly()
    {
        SeedComplexData();
        // Should return: Python Guide (important=true, size=3.5) and ML Video (important=true, size=250)
        var results = _repo.SearchAdvanced("", "", "", null, null, 3.0, null, true);
        Assert.Equal(2, results.Count);
        Assert.All(results, d => Assert.True(d.IsImportant));
        Assert.All(results, d => Assert.True(d.FileSize >= 3.0));
    }

    [Fact]
    public void OverdueDocuments_CorrectlyIdentified()
    {
        SeedComplexData();
        var overdue = _repo.GetOverdueDocuments();
        Assert.Single(overdue);
        Assert.Equal("Data Structures Notes", overdue[0].Name);
    }

    [Fact]
    public void UpcomingDeadlines_Within7Days_IncludesCorrectDoc()
    {
        SeedComplexData();
        var upcoming = _repo.GetUpcomingDeadlines(7);
        Assert.Single(upcoming);
        Assert.Equal("Python Programming Guide", upcoming[0].Name);
    }

    [Fact]
    public void GetDistinctSubjects_ReflectsSeededData()
    {
        SeedComplexData();
        var subjects = _repo.GetDistinctSubjects();
        Assert.Contains("Technology", subjects);
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

    public RecycleBinBulkInteractionTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void BulkDelete_ThenRestoreSelectively_ThenEmptyBin()
    {
        _repo.Add(new StudyDocument { Name = "Keep A" });
        _repo.Add(new StudyDocument { Name = "Delete B" });
        _repo.Add(new StudyDocument { Name = "Delete C" });

        var all = _repo.GetAll().OrderBy(d => d.Name).ToList();
        var deleteIds = all.Where(d => d.Name != "Keep A").Select(d => d.Id).ToList();

        // Bulk delete B, C
        int deleted = Db.BulkSoftDelete(deleteIds);
        Assert.Equal(2, deleted);
        Assert.Single(_repo.GetAll());
        Assert.Equal(2, Db.GetDeletedDocumentCount());

        // Restore B only
        int bId = all.First(d => d.Name == "Delete B").Id;
        Db.RestoreDocument(bId);
        Assert.Equal(2, _repo.GetAll().Count);
        Assert.Equal(1, Db.GetDeletedDocumentCount());

        // Empty remaining bin (only C)
        int emptied = Db.EmptyRecycleBin();
        Assert.Equal(1, emptied);
        Assert.Equal(0, Db.GetDeletedDocumentCount());

        // Final active count
        Assert.Equal(2, _repo.GetAll().Count);
    }

    [Fact]
    public void MultipleSoftDeletes_SameDocument_OnlyOneInBin()
    {
        _repo.Add(new StudyDocument { Name = "Test Double Delete" });
        int id = _repo.GetAll()[0].Id;

        // First soft delete
        _repo.Delete(id);
        Assert.Equal(1, Db.GetDeletedDocumentCount());

        // Second soft delete — should be no-op (already deleted)
        _repo.Delete(id);
        Assert.Equal(1, Db.GetDeletedDocumentCount()); // Still just 1
    }
}

// ════════════════════════════════════════════════════════════
// Integration Flow #7: Collection Isolation between Collections
// ════════════════════════════════════════════════════════════

public class CollectionIsolationTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public CollectionIsolationTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void TwoCollections_DoNotShareDocuments()
    {
        _repo.Add(new StudyDocument { Name = "Shared?" });
        _repo.Add(new StudyDocument { Name = "Exclusive" });
        var docs = _repo.GetAll().OrderBy(d => d.Id).ToList();

        int col1 = Db.CreateCollection("Collection Alpha");
        int col2 = Db.CreateCollection("Collection Beta");

        // Add doc[0] to col1 only, doc[1] to col2 only
        Db.AddDocumentToCollection(col1, docs[0].Id);
        Db.AddDocumentToCollection(col2, docs[1].Id);

        var col1Docs = Db.GetDocumentsInCollection(col1);
        var col2Docs = Db.GetDocumentsInCollection(col2);

        Assert.Single(col1Docs);
        Assert.Single(col2Docs);
        Assert.Equal(docs[0].Id, col1Docs[0].Id);
        Assert.Equal(docs[1].Id, col2Docs[0].Id);
    }

    [Fact]
    public void SameDocument_CanBeInMultipleCollections()
    {
        _repo.Add(new StudyDocument { Name = "Multi-Collection Doc" });
        int docId = _repo.GetAll()[0].Id;

        int col1 = Db.CreateCollection("Alpha");
        int col2 = Db.CreateCollection("Beta");

        Db.AddDocumentToCollection(col1, docId);
        Db.AddDocumentToCollection(col2, docId);

        Assert.Single(Db.GetDocumentsInCollection(col1));
        Assert.Single(Db.GetDocumentsInCollection(col2));
    }

    [Fact]
    public void DeleteCollection_DoesNotDeleteDocuments()
    {
        _repo.Add(new StudyDocument { Name = "Safe Doc" });
        int docId = _repo.GetAll()[0].Id;

        int colId = Db.CreateCollection("To Delete");
        Db.AddDocumentToCollection(colId, docId);

        Db.DeleteCollection(colId);

        // Collection gone
        Assert.Empty(Db.GetCollections());
        // Document still exists
        Assert.Single(_repo.GetAll());
        Assert.Equal("Safe Doc", _repo.GetAll()[0].Name);
    }

    [Fact]
    public void AddDocumentToCollection_Duplicate_ReturnsFalse()
    {
        _repo.Add(new StudyDocument { Name = "Dup Test" });
        int docId = _repo.GetAll()[0].Id;
        int colId = Db.CreateCollection("DupCheck");

        bool first = Db.AddDocumentToCollection(colId, docId);
        bool second = Db.AddDocumentToCollection(colId, docId); // duplicate

        Assert.True(first);
        Assert.False(second); // Duplicate returns false
        Assert.Single(Db.GetDocumentsInCollection(colId));
    }
}

// ════════════════════════════════════════════════════════════
// Integration Flow #8: Statistics Consistency Across Operations
// ════════════════════════════════════════════════════════════

public class StatisticsConsistencyTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public StatisticsConsistencyTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void Stats_ReflectImmediate_AfterEachOperation()
    {
        // Empty state
        var s0 = Db.GetDashboardStatistics();
        Assert.Equal(0, s0.TotalDocuments);
        Assert.Equal(0, s0.ImportantDocuments);
        Assert.Equal(0, s0.NoFileDocuments);

        // Add 3 docs: 2 important, 1 without file
        _repo.Add(new StudyDocument { Name = "A", IsImportant = true, FilePath = @"C:\a.pdf" });
        _repo.Add(new StudyDocument { Name = "B", IsImportant = true, FilePath = @"C:\b.pdf" });
        _repo.Add(new StudyDocument { Name = "C", IsImportant = false, FilePath = "" }); // no file

        var s1 = Db.GetDashboardStatistics();
        Assert.Equal(3, s1.TotalDocuments);
        Assert.Equal(2, s1.ImportantDocuments);
        Assert.Equal(1, s1.NoFileDocuments);

        // Delete one important doc
        int aId = _repo.GetAll().First(d => d.Name == "A").Id;
        _repo.Delete(aId);

        var s2 = Db.GetDashboardStatistics();
        Assert.Equal(2, s2.TotalDocuments);
        Assert.Equal(1, s2.ImportantDocuments);

        // Add collection
        Db.CreateCollection("My Col");
        var s3 = Db.GetDashboardStatistics();
        Assert.Equal(1, s3.TotalCollections);
    }

    [Fact]
    public void TotalDocumentCount_MatchesDashboardStats()
    {
        _repo.Add(new StudyDocument { Name = "X" });
        _repo.Add(new StudyDocument { Name = "Y" });

        var stats = Db.GetDashboardStatistics();
        int countMethod = Db.GetTotalDocumentCount();

        Assert.Equal(stats.TotalDocuments, countMethod);
    }

    [Fact]
    public void Stats_OverdueDocuments_ExcludesRestoredOnes()
    {
        _repo.Add(new StudyDocument { Name = "Overdue", Deadline = DateTime.Today.AddDays(-2) });
        int id = _repo.GetAll()[0].Id;

        var s1 = Db.GetDashboardStatistics();
        Assert.Equal(1, s1.OverdueDocuments);

        // Remove deadline by update
        var doc = _repo.GetAll()[0];
        doc.Deadline = null;
        _repo.Update(doc);

        var s2 = Db.GetDashboardStatistics();
        Assert.Equal(0, s2.OverdueDocuments);
    }
}

// ════════════════════════════════════════════════════════════
// Integration Flow #9: GetDistinctTags case-insensitive dedup
// ════════════════════════════════════════════════════════════

public class TagsNormalizationTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public TagsNormalizationTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void GetDistinctTags_CaseInsensitive_Deduped()
    {
        // "Python" and "python" should deduplicate to 1 entry
        _repo.Add(new StudyDocument { Name = "A", Tags = "Python;AI" });
        _repo.Add(new StudyDocument { Name = "B", Tags = "python;machine-learning" });

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
        _repo.Add(new StudyDocument { Name = "A", Tags = " java ; spring ; web " });
        var tags = _repo.GetDistinctTags();

        Assert.Contains("java", tags);
        Assert.Contains("spring", tags);
        Assert.Contains("web", tags);
        Assert.DoesNotContain(" java ", tags); // No extra spaces
    }

    [Fact]
    public void Tags_SortedAlphabetically()
    {
        _repo.Add(new StudyDocument { Name = "A", Tags = "zebra;apple;mango" });
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

    public RecentFilesLimitTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void RecentFiles_CapAt20_OldestRemoved()
    {
        // Create 25 docs and add all to recent
        for (int i = 1; i <= 25; i++)
            _repo.Add(new StudyDocument { Name = $"Doc {i:D2}" });

        var allDocs = _repo.GetAll().OrderBy(d => d.Id).ToList();

        // Add first 5 docs with a 1.2s gap so they get an older timestamp
        // SQLite datetime resolution is 1 second, so we need >1s between batches
        foreach (var doc in allDocs.Take(5))
            Db.AddRecentFile(doc.Id);

        System.Threading.Thread.Sleep(1200); // Ensure 2nd batch has newer timestamp

        // Add the remaining 20 docs (these should be kept)
        foreach (var doc in allDocs.Skip(5))
            Db.AddRecentFile(doc.Id);

        var recent = Db.GetRecentFiles();

        // Should be capped at 20 (LIMIT 20 in SQL)
        Assert.Equal(20, recent.Count);

        // All returned items should be from the newer batch (docs 06-25)
        // Tuple structure: (int Id, string Ten, ...)
        var recentNames = recent.Select(r => r.Name).ToList();
        Assert.DoesNotContain("Doc 01", recentNames);
        Assert.DoesNotContain("Doc 05", recentNames);
        Assert.Contains("Doc 25", recentNames);
        Assert.Contains("Doc 06", recentNames);
    }

    [Fact]
    public void RecentFiles_GetRecentFiles_ReturnsTupleWithCorrectFields()
    {
        // Verify the tuple structure returned by GetRecentFiles
        _repo.Add(new StudyDocument { Name = "Test File", Subject = "Study", Type = "PDF", FilePath = @"C:\test.pdf" });
        int docId = _repo.GetAll()[0].Id;
        Db.AddRecentFile(docId);

        var recent = Db.GetRecentFiles();
        Assert.Single(recent);

        var item = recent[0];
        Assert.Equal(docId, item.Id);
        Assert.Equal("Test File", item.Name);
        Assert.Equal("Study", item.Subject);
        Assert.Equal("PDF", item.Type);
        Assert.Equal(@"C:\test.pdf", item.FilePath);
        Assert.True(item.OpenedAt > DateTime.MinValue);
    }


}

// ════════════════════════════════════════════════════════════
// IDocumentRepository Contract Tests
// Ensures DocumentRepository implements contract correctly
// ════════════════════════════════════════════════════════════

public class DocumentRepositoryContractTests : DatabaseTestBase
{
    private readonly IDocumentRepository _repo;

    public DocumentRepositoryContractTests()
    {
        _repo = new DocumentRepository(Db);
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
        _repo.Add(new StudyDocument { Name = "Something" });
        var result = _repo.Search("xyzxyzxyz_noexist");
        Assert.Empty(result);
    }

    [Fact]
    public void Add_ReturnsTrue_OnSuccess()
    {
        bool result = _repo.Add(new StudyDocument { Name = "Contract Test" });
        Assert.True(result);
    }

    [Fact]
    public void Update_NonExistentDocument_ReturnsFalse()
    {
        var ghost = new StudyDocument { Id = 99999, Name = "Ghost" };
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
        _repo.Add(new StudyDocument { Name = "No Deadline", Deadline = null });
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
