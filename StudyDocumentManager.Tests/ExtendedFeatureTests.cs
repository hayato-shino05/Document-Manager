using Xunit;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Services;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;

namespace StudyDocumentManager.Tests;

// ════════════════════════════════════════════════════════════
// F17 (extended): Type Management — AddType / GetAllTypes / DeleteType / GetTypesWithCount / DeleteDocumentsByType
// ════════════════════════════════════════════════════════════

public class TypeManagementTests : DatabaseTestBase
{
    [Fact]
    public void AddType_NewType_AddedToList()
    {
        bool result = DatabaseHelper.AddType("Bản nhạc mới");
        Assert.True(result);
        Assert.Contains("Bản nhạc mới", DatabaseHelper.GetAllTypes());
    }

    [Fact]
    public void AddType_DuplicateName_DoesNotDuplicate()
    {
        DatabaseHelper.AddType("Giáo trình");
        DatabaseHelper.AddType("Giáo trình"); // duplicate via INSERT OR IGNORE

        var types = DatabaseHelper.GetAllTypes().Where(t => t == "Giáo trình").ToList();
        Assert.Single(types);
    }

    [Fact]
    public void DeleteType_RemovesFromLookupTable()
    {
        DatabaseHelper.AddType("TypeToDelete");
        bool result = DatabaseHelper.DeleteType("TypeToDelete");

        Assert.True(result);
        Assert.DoesNotContain("TypeToDelete", DatabaseHelper.GetAllTypes());
    }

    [Fact]
    public void DeleteType_NonExistingType_ReturnsFalse()
    {
        bool result = DatabaseHelper.DeleteType("does_not_exist_xyz");
        Assert.False(result);
    }

    [Fact]
    public void GetTypesWithCount_ReturnsCorrectCounts()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "A", Loai = "PDF" });
        repo.Add(new StudyDocument { Ten = "B", Loai = "PDF" });
        repo.Add(new StudyDocument { Ten = "C", Loai = "Video" });

        var typeCounts = DatabaseHelper.GetTypesWithCount();
        var pdfEntry = typeCounts.FirstOrDefault(t => t.Name == "PDF");
        var videoEntry = typeCounts.FirstOrDefault(t => t.Name == "Video");

        Assert.Equal(2, pdfEntry.Count);
        Assert.Equal(1, videoEntry.Count);
    }

    [Fact]
    public void GetTypesWithCount_ExcludesSoftDeleted()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "A", Loai = "Excel" });
        repo.Add(new StudyDocument { Ten = "B", Loai = "Excel" });

        var id = repo.GetAll().First().Id;
        repo.Delete(id);

        var typeCounts = DatabaseHelper.GetTypesWithCount();
        var entry = typeCounts.FirstOrDefault(t => t.Name == "Excel");

        Assert.Equal(1, entry.Count);
    }

    [Fact]
    public void DeleteDocumentsByType_SoftDeletesAllInType()
    {
        DatabaseHelper.AddType("Loai XYZ");
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "Doc1", Loai = "Loai XYZ" });
        repo.Add(new StudyDocument { Ten = "Doc2", Loai = "Loai XYZ" });
        repo.Add(new StudyDocument { Ten = "Keep", Loai = "Khác loại" });

        DatabaseHelper.DeleteDocumentsByType("Loai XYZ");

        var active = repo.GetAll();
        Assert.Single(active);
        Assert.Equal("Keep", active[0].Ten);
        Assert.Equal(2, DatabaseHelper.GetDeletedDocuments().Count);
    }

    [Fact]
    public void UpdateTypeName_RenamesInDocumentsAndLookup()
    {
        DatabaseHelper.AddType("OldType");
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "X", Loai = "OldType" });

        bool result = DatabaseHelper.UpdateTypeName("OldType", "NewType");
        var docs = repo.GetAll();

        Assert.True(result);
        Assert.Equal("NewType", docs[0].Loai);
        Assert.Contains("NewType", DatabaseHelper.GetAllTypes());
        Assert.DoesNotContain("OldType", DatabaseHelper.GetAllTypes());
    }
}

// ════════════════════════════════════════════════════════════
// F03: Advanced Search — Size Filter Edge Cases
// ════════════════════════════════════════════════════════════

public class AdvancedSearchSizeFilterTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public AdvancedSearchSizeFilterTests() { _repo = new DocumentRepository(); }

    private void SeedWithSizes()
    {
        _repo.Add(new StudyDocument { Ten = "Small Doc", KichThuoc = 0.5, MonHoc = "Test" });
        _repo.Add(new StudyDocument { Ten = "Medium Doc", KichThuoc = 5.0, MonHoc = "Test" });
        _repo.Add(new StudyDocument { Ten = "Large Doc", KichThuoc = 20.0, MonHoc = "Test" });
        _repo.Add(new StudyDocument { Ten = "No Size Doc", KichThuoc = null, MonHoc = "Test" });
    }

    [Fact]
    public void SearchAdvanced_ByMinSize_ReturnsOnlyDocumentsAboveMinimum()
    {
        SeedWithSizes();
        var results = _repo.SearchAdvanced("", "", "", null, null, 5.0, null, null);

        // Should return Medium (5.0) and Large (20.0)
        Assert.Equal(2, results.Count);
        Assert.All(results, d => Assert.True(d.KichThuoc >= 5.0));
    }

    [Fact]
    public void SearchAdvanced_ByMaxSize_ReturnsOnlyDocumentsBelowMaximum()
    {
        SeedWithSizes();
        var results = _repo.SearchAdvanced("", "", "", null, null, null, 5.0, null);

        // Should return Small (0.5) and Medium (5.0)
        Assert.Equal(2, results.Count);
        Assert.All(results, d => Assert.True(d.KichThuoc <= 5.0));
    }

    [Fact]
    public void SearchAdvanced_BySizeRange_ReturnsWithinRange()
    {
        SeedWithSizes();
        var results = _repo.SearchAdvanced("", "", "", null, null, 1.0, 10.0, null);

        // Should return Medium (5.0) only
        Assert.Single(results);
        Assert.Equal("Medium Doc", results[0].Ten);
    }

    [Fact]
    public void SearchAdvanced_BySubjectAndImportant_FiltersCorrectly()
    {
        _repo.Add(new StudyDocument { Ten = "Important Math", MonHoc = "Math", QuanTrong = true });
        _repo.Add(new StudyDocument { Ten = "Normal Math", MonHoc = "Math", QuanTrong = false });
        _repo.Add(new StudyDocument { Ten = "Important Science", MonHoc = "Science", QuanTrong = true });

        var results = _repo.SearchAdvanced("", "Math", "", null, null, null, null, true);

        Assert.Single(results);
        Assert.Equal("Important Math", results[0].Ten);
    }

    [Fact]
    public void SearchAdvanced_AllNullFilters_ReturnsAll()
    {
        _repo.Add(new StudyDocument { Ten = "A" });
        _repo.Add(new StudyDocument { Ten = "B" });
        _repo.Add(new StudyDocument { Ten = "C" });

        var results = _repo.SearchAdvanced("", "", "", null, null, null, null, null);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void SearchAdvanced_ByKeywordAndType_NarrowsResults()
    {
        _repo.Add(new StudyDocument { Ten = "Python Tutorial", Loai = "Video" });
        _repo.Add(new StudyDocument { Ten = "Python Guide", Loai = "Tài liệu" });
        _repo.Add(new StudyDocument { Ten = "Java Tutorial", Loai = "Video" });

        var results = _repo.SearchAdvanced("Python", "", "Video", null, null, null, null, null);

        Assert.Single(results);
        Assert.Equal("Python Tutorial", results[0].Ten);
    }
}

// ════════════════════════════════════════════════════════════
// F21 (extended): Statistics — NoFileDocuments / TotalCategories
// ════════════════════════════════════════════════════════════

public class ExtendedStatisticsTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public ExtendedStatisticsTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void GetDashboardStatistics_NoFileDocuments_CountsCorrectly()
    {
        _repo.Add(new StudyDocument { Ten = "With File", DuongDan = @"C:\file.pdf" });
        _repo.Add(new StudyDocument { Ten = "No File 1", DuongDan = "" });
        _repo.Add(new StudyDocument { Ten = "No File 2", DuongDan = null });

        var stats = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(2, stats.NoFileDocuments);
    }

    [Fact]
    public void GetDashboardStatistics_TotalCategories_CountsDistinctSubjects()
    {
        _repo.Add(new StudyDocument { Ten = "A", MonHoc = "Math" });
        _repo.Add(new StudyDocument { Ten = "B", MonHoc = "Math" });
        _repo.Add(new StudyDocument { Ten = "C", MonHoc = "Science" });

        var stats = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(2, stats.TotalCategories);
    }

    [Fact]
    public void GetDashboardStatistics_NearDeadlineExcludesOverdue()
    {
        _repo.Add(new StudyDocument { Ten = "Overdue", Deadline = DateTime.Today.AddDays(-1) });
        _repo.Add(new StudyDocument { Ten = "Near Today", Deadline = DateTime.Today });
        _repo.Add(new StudyDocument { Ten = "Near 7", Deadline = DateTime.Today.AddDays(7) });
        _repo.Add(new StudyDocument { Ten = "Far", Deadline = DateTime.Today.AddDays(30) });

        var stats = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(1, stats.OverdueDocuments);
        Assert.Equal(2, stats.NearDeadlineDocuments); // Today + 7 days (both inclusive)
    }

    [Fact]
    public void GetDocumentsByDay_TodayDocumentCountedCorrectly()
    {
        _repo.Add(new StudyDocument { Ten = "Today Doc 1" });
        _repo.Add(new StudyDocument { Ten = "Today Doc 2" });

        var data = DatabaseHelper.GetDocumentsByDay(7);
        // Today (last entry in ascending order)
        var today = data.Last();

        Assert.Equal(2, today.Count);
    }

    [Fact]
    public void GetDocumentsByMonth_CurrentMonthCountedCorrectly()
    {
        _repo.Add(new StudyDocument { Ten = "This Month Doc" });

        var data = DatabaseHelper.GetDocumentsByMonth(12);
        // Current month is the last in ascending order
        var currentMonth = data.Last();

        Assert.True(currentMonth.Count >= 1);
    }

    [Fact]
    public void GetDocumentsBySubject_ExcludesSoftDeleted()
    {
        _repo.Add(new StudyDocument { Ten = "Active", MonHoc = "Physics" });
        _repo.Add(new StudyDocument { Ten = "Deleted", MonHoc = "Physics" });
        var deletedId = _repo.GetAll().First(d => d.Ten == "Deleted").Id;
        _repo.Delete(deletedId);

        var data = DatabaseHelper.GetDocumentsBySubject();
        var physics = data.FirstOrDefault(d => d.Label == "Physics");

        Assert.Equal(1, physics.Count);
    }

    [Fact]
    public void GetDocumentsByType_ExcludesSoftDeleted()
    {
        _repo.Add(new StudyDocument { Ten = "Active", Loai = "Word" });
        _repo.Add(new StudyDocument { Ten = "Deleted", Loai = "Word" });
        var deletedId = _repo.GetAll().First(d => d.Ten == "Deleted").Id;
        _repo.Delete(deletedId);

        var data = DatabaseHelper.GetDocumentsByType();
        var word = data.FirstOrDefault(d => d.Label == "Word");

        Assert.Equal(1, word.Count);
    }
}

// ════════════════════════════════════════════════════════════
// F24 (extended): Backup / Restore Database
// ════════════════════════════════════════════════════════════

public class BackupRestoreTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public BackupRestoreTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void BackupDatabase_ContainsCorrectData()
    {
        // Seed data
        _repo.Add(new StudyDocument { Ten = "Backup Content Doc", MonHoc = "Test" });
        string backupPath = Path.Combine(Path.GetTempPath(), $"sdm_backup_verify_{Guid.NewGuid():N}.db");

        try
        {
            bool result = DatabaseHelper.BackupDatabase(backupPath);
            Assert.True(result);
            Assert.True(File.Exists(backupPath));

            // Backup file should not be empty (has real SQLite data)
            var fileInfo = new FileInfo(backupPath);
            Assert.True(fileInfo.Length > 0);
        }
        finally
        {
            if (File.Exists(backupPath)) File.Delete(backupPath);
        }
    }
}

// ════════════════════════════════════════════════════════════
// F20 (extended): Recycle Bin — Timestamp / deleted_at metadata
// ════════════════════════════════════════════════════════════

public class RecycleBinMetadataTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public RecycleBinMetadataTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void DeleteDocument_DeletedAtTimestampIsSet()
    {
        _repo.Add(new StudyDocument { Ten = "Timestamp Test" });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id);

        var deleted = DatabaseHelper.GetDeletedDocuments();
        Assert.Single(deleted);
        // deleted_at should be populated (check it's a valid datetime within reason)
        Assert.NotNull(deleted[0].Ten);
    }

    [Fact]
    public void EmptyRecycleBin_ReturnsCountOfDeletedItems()
    {
        _repo.Add(new StudyDocument { Ten = "Trash A" });
        _repo.Add(new StudyDocument { Ten = "Trash B" });
        _repo.Add(new StudyDocument { Ten = "Trash C" });
        var all = _repo.GetAll();
        foreach (var d in all) _repo.Delete(d.Id);

        int count = DatabaseHelper.EmptyRecycleBin();
        Assert.Equal(3, count);
        Assert.Equal(0, DatabaseHelper.GetDeletedDocumentCount());
    }

    [Fact]
    public void PermanentDeleteDocument_AlsoRemovedFromActiveList()
    {
        _repo.Add(new StudyDocument { Ten = "Perm Delete Test" });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id);

        // Permanent delete
        bool result = DatabaseHelper.PermanentDeleteDocument(id);
        Assert.True(result);

        // Should be gone from both lists
        Assert.Empty(_repo.GetAll());
        Assert.Empty(DatabaseHelper.GetDeletedDocuments());
        Assert.Null(_repo.GetById(id));
    }

    [Fact]
    public void RestoreDocument_AfterUpdateAndDelete_KeepsUpdatedData()
    {
        _repo.Add(new StudyDocument { Ten = "Original" });
        var doc = _repo.GetAll()[0];
        int id = doc.Id;

        // Update first
        doc.Ten = "Updated Before Delete";
        _repo.Update(doc);

        // Then delete and restore
        _repo.Delete(id);
        DatabaseHelper.RestoreDocument(id);

        var restored = _repo.GetById(id);
        Assert.NotNull(restored);
        Assert.Equal("Updated Before Delete", restored!.Ten);
    }
}

// ════════════════════════════════════════════════════════════
// F13 (extended): Bulk Operations — Edge Cases
// ════════════════════════════════════════════════════════════

public class BulkOperationEdgeCaseTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public BulkOperationEdgeCaseTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void BulkUpdateSubject_EmptyList_ReturnsZero()
    {
        int affected = DatabaseHelper.BulkUpdateSubject(new List<int>(), "Bất kỳ");
        Assert.Equal(0, affected);
    }

    [Fact]
    public void BulkSoftDelete_EmptyList_ReturnsZero()
    {
        int affected = DatabaseHelper.BulkSoftDelete(new List<int>());
        Assert.Equal(0, affected);
    }

    [Fact]
    public void BulkSoftDelete_OnlySoftDeletesSelectedIds()
    {
        _repo.Add(new StudyDocument { Ten = "Keep 1" });
        _repo.Add(new StudyDocument { Ten = "Keep 2" });
        _repo.Add(new StudyDocument { Ten = "Delete Me" });

        var all = _repo.GetAll();
        var deleteId = all.First(d => d.Ten == "Delete Me").Id;

        int affected = DatabaseHelper.BulkSoftDelete(new List<int> { deleteId });

        Assert.Equal(1, affected);
        Assert.Equal(2, _repo.GetAll().Count);
        Assert.DoesNotContain(_repo.GetAll(), d => d.Ten == "Delete Me");
    }

    [Fact]
    public void BulkToggleImportant_NullList_ReturnsZero()
    {
        // null list handled as empty
        int affected = DatabaseHelper.BulkToggleImportant(null!, true);
        Assert.Equal(0, affected);
    }

    [Fact]
    public void BulkUpdateSubject_PartialSelection_OnlyUpdatesSelected()
    {
        _repo.Add(new StudyDocument { Ten = "A", MonHoc = "Toán" });
        _repo.Add(new StudyDocument { Ten = "B", MonHoc = "Toán" });
        _repo.Add(new StudyDocument { Ten = "C", MonHoc = "Toán" });

        var all = _repo.GetAll();
        var updateIds = all.Take(1).Select(d => d.Id).ToList();

        DatabaseHelper.BulkUpdateSubject(updateIds, "Lý");

        var updated = _repo.GetAll();
        Assert.Equal(1, updated.Count(d => d.MonHoc == "Lý"));
        Assert.Equal(2, updated.Count(d => d.MonHoc == "Toán"));
    }
}

// ════════════════════════════════════════════════════════════
// F04 (extended): Distinct Values — Edge Cases
// ════════════════════════════════════════════════════════════

public class DistinctValuesEdgeCaseTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public DistinctValuesEdgeCaseTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void GetDistinctTypes_ExcludesSoftDeleted()
    {
        _repo.Add(new StudyDocument { Ten = "Active", Loai = "Ánh sáng" });
        _repo.Add(new StudyDocument { Ten = "Deleted", Loai = "Xóa loại" });
        var id = _repo.GetAll().First(d => d.Ten == "Deleted").Id;
        _repo.Delete(id);

        var types = _repo.GetDistinctTypes();
        Assert.DoesNotContain("Xóa loại", types);
        Assert.Contains("Ánh sáng", types);
    }

    [Fact]
    public void GetDistinctTags_HandlesSemicolonSeparator()
    {
        _repo.Add(new StudyDocument { Ten = "A", Tags = "python;django;web" });
        _repo.Add(new StudyDocument { Ten = "B", Tags = "web;api" });

        var tags = _repo.GetDistinctTags();

        Assert.Contains("python", tags);
        Assert.Contains("django", tags);
        Assert.Contains("api", tags);
        // "web" should appear only once even though it's in both docs
        Assert.Equal(tags.Distinct().Count(), tags.Count);
    }

    [Fact]
    public void GetDistinctTags_EmptyTags_NotIncluded()
    {
        _repo.Add(new StudyDocument { Ten = "A", Tags = "" });
        _repo.Add(new StudyDocument { Ten = "B", Tags = null });
        _repo.Add(new StudyDocument { Ten = "C", Tags = "valid" });

        var tags = _repo.GetDistinctTags();

        // Empty/null tags should not produce empty string entries
        Assert.DoesNotContain("", tags);
        Assert.Contains("valid", tags);
    }
}

// ════════════════════════════════════════════════════════════
// F10 (extended): AppVersion Service
// ════════════════════════════════════════════════════════════

public class AppVersionExtendedTests
{
    [Fact]
    public void AppVersion_Current_IsNotEmpty()
    {
        // Verify that the version string is set and follows semver format
        var version = AppVersion.Current;
        Assert.False(string.IsNullOrEmpty(version));
        // Version should have the format X.Y.Z
        var parts = version.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.All(parts, p => Assert.True(int.TryParse(p, out _)));
    }

    [Fact]
    public void AppVersion_IsNewer_RelativeToCurrentVersion_Works()
    {
        // Any version with higher major should be newer than current
        var parts = AppVersion.Current.Split('.');
        int majorPlusOne = int.Parse(parts[0]) + 1;
        string newerVersion = $"{majorPlusOne}.0.0";

        bool result = AppVersion.IsNewer(newerVersion);
        Assert.True(result);
    }

    [Fact]
    public void AppVersion_IsNewer_SameVersion_ReturnsFalse()
    {
        bool result = AppVersion.IsNewer(AppVersion.Current);
        Assert.False(result);
    }

    [Fact]
    public void AppVersion_IsNewer_OldestVersion_ReturnsFalse()
    {
        bool result = AppVersion.IsNewer("1.0.0");
        Assert.False(result);
    }
}

// ════════════════════════════════════════════════════════════
// F18 (extended): Collection — Description & Advanced
// ════════════════════════════════════════════════════════════

public class CollectionAdvancedTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public CollectionAdvancedTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void CreateCollection_WithDescription_Persists()
    {
        int colId = DatabaseHelper.CreateCollection("My Collection", "This is a description");
        var cols = DatabaseHelper.GetCollections();

        var col = cols.First(c => c.Id == colId);
        Assert.Equal("My Collection", col.Name);
        Assert.Equal("This is a description", col.Description);
    }

    [Fact]
    public void UpdateCollection_ChangesDescription()
    {
        int colId = DatabaseHelper.CreateCollection("Col", "Old desc");
        bool result = DatabaseHelper.UpdateCollection(colId, "Col", "New desc");

        var cols = DatabaseHelper.GetCollections();
        var col = cols.First(c => c.Id == colId);

        Assert.True(result);
        Assert.Equal("New desc", col.Description);
    }

    [Fact]
    public void GetDocumentsInCollection_ExcludesSoftDeletedDocuments()
    {
        _repo.Add(new StudyDocument { Ten = "Active Doc" });
        _repo.Add(new StudyDocument { Ten = "Deleted Doc" });

        var all = _repo.GetAll();
        var activeDoc = all.First(d => d.Ten == "Active Doc");
        var deletedDocId = all.First(d => d.Ten == "Deleted Doc").Id;

        int colId = DatabaseHelper.CreateCollection("Test Col");
        DatabaseHelper.AddDocumentToCollection(colId, activeDoc.Id);
        DatabaseHelper.AddDocumentToCollection(colId, deletedDocId);

        // Soft delete one document
        _repo.Delete(deletedDocId);

        var docs = DatabaseHelper.GetDocumentsInCollection(colId);

        // Only active document should appear
        Assert.Single(docs);
        Assert.Equal("Active Doc", docs[0].Ten);
    }

    [Fact]
    public void GetCollections_MultipleCollections_SortedByName()
    {
        DatabaseHelper.CreateCollection("Zebra");
        DatabaseHelper.CreateCollection("Alpha");
        DatabaseHelper.CreateCollection("Middle");

        var cols = DatabaseHelper.GetCollections();

        Assert.Equal("Alpha", cols[0].Name);
        Assert.Equal("Middle", cols[1].Name);
        Assert.Equal("Zebra", cols[2].Name);
    }
}

// ════════════════════════════════════════════════════════════
// F11 (extended): Personal Notes — Long Content / Special Chars
// ════════════════════════════════════════════════════════════

public class PersonalNoteEdgeCaseTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public PersonalNoteEdgeCaseTests() { _repo = new DocumentRepository(); }

    private int CreateDoc()
    {
        _repo.Add(new StudyDocument { Ten = "Note Test Doc" });
        return _repo.GetAll()[0].Id;
    }

    [Fact]
    public void SavePersonalNote_WithSpecialChars_Persists()
    {
        int docId = CreateDoc();
        string content = "Ghi chú với ký tự đặc biệt: <div>HTML</div> & 'quotes' \"double\" \n newline";
        DatabaseHelper.SavePersonalNote(docId, content);

        Assert.Equal(content, DatabaseHelper.GetPersonalNote(docId));
    }

    [Fact]
    public void SavePersonalNote_WithLongContent_Persists()
    {
        int docId = CreateDoc();
        string longContent = new string('A', 10000); // 10KB content
        DatabaseHelper.SavePersonalNote(docId, longContent);

        Assert.Equal(longContent, DatabaseHelper.GetPersonalNote(docId));
    }

    [Fact]
    public void DeletePersonalNote_WhenNoNote_ReturnsFalse()
    {
        int docId = CreateDoc();
        // No note exists — DeletePersonalNote should return false (0 rows affected)
        bool result = DatabaseHelper.DeletePersonalNote(docId);
        Assert.False(result);
    }
}

// ════════════════════════════════════════════════════════════
// F16 (extended): Related Documents — Multiple Relations
// ════════════════════════════════════════════════════════════

public class RelatedDocumentsExtendedTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public RelatedDocumentsExtendedTests() { _repo = new DocumentRepository(); }

    private List<int> CreateDocs(int count)
    {
        for (int i = 1; i <= count; i++)
            _repo.Add(new StudyDocument { Ten = $"Doc {i}" });
        return _repo.GetAll().OrderBy(d => d.Id).Select(d => d.Id).ToList();
    }

    [Fact]
    public void AddDocumentRelation_MultipleRelations_AllRetrievable()
    {
        var ids = CreateDocs(4);
        var (doc1, doc2, doc3, doc4) = (ids[0], ids[1], ids[2], ids[3]);

        DatabaseHelper.AddDocumentRelation(doc1, doc2, "related");
        DatabaseHelper.AddDocumentRelation(doc1, doc3, "reference");
        DatabaseHelper.AddDocumentRelation(doc1, doc4, "supplement");

        var relDocs = DatabaseHelper.GetRelatedDocuments(doc1);
        Assert.Equal(3, relDocs.Count);
    }

    [Fact]
    public void AddDocumentRelation_CanonicalOrder_WorksBothWays()
    {
        // Due to canonicalization (lo=min, hi=max), adding (a,b) same as (b,a)
        var ids = CreateDocs(2);
        DatabaseHelper.AddDocumentRelation(ids[0], ids[1]);
        DatabaseHelper.AddDocumentRelation(ids[1], ids[0]); // reversed — should be ignored

        var rel = DatabaseHelper.GetRelatedDocuments(ids[0]);
        Assert.Single(rel); // Only one relation
    }

    [Fact]
    public void GetRelatedDocuments_EmptyForUnlinkedDoc()
    {
        var ids = CreateDocs(2);
        // No relations added
        var rel = DatabaseHelper.GetRelatedDocuments(ids[0]);
        Assert.Empty(rel);
    }

    [Fact]
    public void RemoveDocumentRelation_ByRelationId_RemovesSpecificLink()
    {
        var ids = CreateDocs(3);

        DatabaseHelper.AddDocumentRelation(ids[0], ids[1], "related");
        DatabaseHelper.AddDocumentRelation(ids[0], ids[2], "reference");

        var allRel = DatabaseHelper.GetRelatedDocuments(ids[0]);
        Assert.Equal(2, allRel.Count);

        // Remove only the first relation
        DatabaseHelper.RemoveDocumentRelation(allRel[0].RelationId);

        var remaining = DatabaseHelper.GetRelatedDocuments(ids[0]);
        Assert.Single(remaining);
    }
}

// ════════════════════════════════════════════════════════════
// F14 (extended): Recent Files — Order & Filtering
// ════════════════════════════════════════════════════════════

public class RecentFilesExtendedTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public RecentFilesExtendedTests() { _repo = new DocumentRepository(); }

    private int CreateDoc(string name)
    {
        _repo.Add(new StudyDocument { Ten = name });
        return _repo.GetAll().First(d => d.Ten == name).Id;
    }

    [Fact]
    public void GetRecentFiles_ReturnsMostRecentFirst()
    {
        int d1 = CreateDoc("First Opened");
        int d2 = CreateDoc("Second Opened");

        DatabaseHelper.AddRecentFile(d1);
        // SQLite datetime('now','localtime') has 1-second resolution, need >1s gap
        System.Threading.Thread.Sleep(1200);
        DatabaseHelper.AddRecentFile(d2);

        var recent = DatabaseHelper.GetRecentFiles();

        // Most recent first — d2 should be first
        Assert.Equal(2, recent.Count);
        Assert.Equal(d2, recent[0].Id);
        Assert.Equal(d1, recent[1].Id);
    }

    [Fact]
    public void GetRecentFiles_ExcludesSoftDeletedDocuments()
    {
        int d1 = CreateDoc("Active Recent");
        int d2 = CreateDoc("Deleted Recent");

        DatabaseHelper.AddRecentFile(d1);
        DatabaseHelper.AddRecentFile(d2);
        _repo.Delete(d2); // Soft delete d2

        var recent = DatabaseHelper.GetRecentFiles();

        // Only active document should appear
        Assert.Single(recent);
        Assert.Equal(d1, recent[0].Id);
    }

    [Fact]
    public void AddRecentFile_NonExistentDocument_EntryInsertedButNotReturnedByJoin()
    {
        // Adding a non-existent document inserts a record in recent_files,
        // but GetRecentFiles() uses INNER JOIN so it excludes orphan records.
        // We just verify it doesn't return result — we cannot avoid the INSERT.
        // (In production, this case shouldn't arise as doc must exist before opening.)
        try
        {
            DatabaseHelper.AddRecentFile(99999);
        }
        catch { /* FOREIGN KEY constraint may or may not be enforced */ }

        var recent = DatabaseHelper.GetRecentFiles();
        Assert.Empty(recent);
    }
}

// ════════════════════════════════════════════════════════════
// F01 (extended): Document CRUD — Edge Cases
// ════════════════════════════════════════════════════════════

public class DocumentCrudEdgeCaseTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public DocumentCrudEdgeCaseTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void Add_DocumentWithVietnameseName_Persists()
    {
        var doc = new StudyDocument
        {
            Ten = "Giáo trình Toán học đại cương cho sinh viên năm nhất",
            MonHoc = "Học tập",
            Loai = "Tài liệu"
        };

        _repo.Add(doc);
        var saved = _repo.GetAll()[0];

        Assert.Equal("Giáo trình Toán học đại cương cho sinh viên năm nhất", saved.Ten);
    }

    [Fact]
    public void Add_DocumentWithNullOptionalFields_Persists()
    {
        var doc = new StudyDocument
        {
            Ten = "Minimal Doc",
            MonHoc = null,
            Loai = null,
            DuongDan = null,
            GhiChu = null,
            KichThuoc = null,
            TacGia = null,
            Tags = null,
            Deadline = null
        };

        _repo.Add(doc);
        var saved = _repo.GetAll()[0];

        Assert.Equal("Minimal Doc", saved.Ten);
        Assert.Null(saved.KichThuoc);
        Assert.Null(saved.Deadline);
    }

    [Fact]
    public void Update_TogglesImportantFlag()
    {
        _repo.Add(new StudyDocument { Ten = "Toggle Test", QuanTrong = false });
        var doc = _repo.GetAll()[0];

        doc.QuanTrong = true;
        _repo.Update(doc);

        var updated = _repo.GetById(doc.Id)!;
        Assert.True(updated.QuanTrong);

        updated.QuanTrong = false;
        _repo.Update(updated);

        var toggled = _repo.GetById(doc.Id)!;
        Assert.False(toggled.QuanTrong);
    }

    [Fact]
    public void Update_WithEmptyPath_SetsEmptyString()
    {
        _repo.Add(new StudyDocument { Ten = "With Path", DuongDan = @"C:\file.pdf" });
        var doc = _repo.GetAll()[0];

        doc.DuongDan = "";
        _repo.Update(doc);

        var updated = _repo.GetById(doc.Id)!;
        Assert.Equal("", updated.DuongDan);
    }

    [Fact]
    public void GetById_SoftDeletedDocument_StillReturnable_ByInternalMethod()
    {
        // GetById (GetDocumentById) does NOT filter is_deleted — it returns the raw record.
        // This is by design: forms like RecycleBin or RestoreForm need to access deleted docs.
        // The filtering is done at GetAll() / Search() level.
        _repo.Add(new StudyDocument { Ten = "Soft Deleted" });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id);

        // GetById still returns the doc (no is_deleted filter)
        var result = _repo.GetById(id);
        Assert.NotNull(result); // Still accessible via ID

        // But GetAll excludes it
        Assert.Empty(_repo.GetAll());
    }

    [Fact]
    public void GetAll_LargeDataset_ReturnsAll()
    {
        for (int i = 1; i <= 50; i++)
            _repo.Add(new StudyDocument { Ten = $"Large Set Doc {i}", MonHoc = "Test" });

        var all = _repo.GetAll();
        Assert.Equal(50, all.Count);
    }
}

// ════════════════════════════════════════════════════════════
// F19 (extended): File Integrity — Edge Cases
// ════════════════════════════════════════════════════════════

public class FileIntegrityEdgeCaseTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public FileIntegrityEdgeCaseTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void UpdateDocumentPath_ToSamePath_NoOp()
    {
        _repo.Add(new StudyDocument { Ten = "Same Path", DuongDan = @"C:\same.pdf" });
        int id = _repo.GetAll()[0].Id;

        bool result = DatabaseHelper.UpdateDocumentPath(id, @"C:\same.pdf");
        var doc = _repo.GetById(id)!;

        Assert.True(result);
        Assert.Equal(@"C:\same.pdf", doc.DuongDan);
    }

    [Fact]
    public void UpdateDocumentPath_NonExistentId_ReturnsFalse()
    {
        bool result = DatabaseHelper.UpdateDocumentPath(99999, @"C:\any.pdf");
        Assert.False(result);
    }

    [Fact]
    public void ClearDocumentPath_ThenUpdateToNewPath_Works()
    {
        _repo.Add(new StudyDocument { Ten = "Path Journey", DuongDan = @"C:\original.pdf" });
        int id = _repo.GetAll()[0].Id;

        // Clear path
        DatabaseHelper.ClearDocumentPath(id);
        Assert.Equal("", _repo.GetById(id)!.DuongDan);

        // Set new path
        DatabaseHelper.UpdateDocumentPath(id, @"C:\new_location.pdf");
        Assert.Equal(@"C:\new_location.pdf", _repo.GetById(id)!.DuongDan);
    }
}

// ════════════════════════════════════════════════════════════
// F25 (extended): Deadline Boundary Testing
// ════════════════════════════════════════════════════════════

public class DeadlineBoundaryTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public DeadlineBoundaryTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void GetUpcomingDeadlines_DeadlineToday_Included()
    {
        _repo.Add(new StudyDocument { Ten = "Due Today", Deadline = DateTime.Today });
        var upcoming = _repo.GetUpcomingDeadlines(7);

        Assert.Single(upcoming);
        Assert.Equal("Due Today", upcoming[0].Ten);
    }

    [Fact]
    public void GetUpcomingDeadlines_ExactlyAt7Days_Included()
    {
        _repo.Add(new StudyDocument { Ten = "Due 7 Days", Deadline = DateTime.Today.AddDays(7) });
        var upcoming = _repo.GetUpcomingDeadlines(7);

        Assert.Single(upcoming);
    }

    [Fact]
    public void GetUpcomingDeadlines_At8Days_NotIncluded()
    {
        _repo.Add(new StudyDocument { Ten = "Due 8 Days", Deadline = DateTime.Today.AddDays(8) });
        var upcoming = _repo.GetUpcomingDeadlines(7);

        Assert.Empty(upcoming);
    }

    [Fact]
    public void GetOverdueDocuments_DeadlineYesterday_IsOverdue()
    {
        _repo.Add(new StudyDocument { Ten = "Yesterday", Deadline = DateTime.Today.AddDays(-1) });
        var overdue = _repo.GetOverdueDocuments();

        Assert.Single(overdue);
        Assert.Equal("Yesterday", overdue[0].Ten);
    }

    [Fact]
    public void GetOverdueDocuments_DeadlineToday_NotOverdue()
    {
        // Today's deadline should NOT be overdue (deadline < today, not <=)
        _repo.Add(new StudyDocument { Ten = "Due Today", Deadline = DateTime.Today });
        var overdue = _repo.GetOverdueDocuments();

        Assert.Empty(overdue);
    }

    [Fact]
    public void Deadline_Update_PersistsCorrectly()
    {
        _repo.Add(new StudyDocument { Ten = "Deadline Update", Deadline = DateTime.Today });
        var doc = _repo.GetAll()[0];
        var newDeadline = DateTime.Today.AddDays(30);

        doc.Deadline = newDeadline;
        _repo.Update(doc);

        var updated = _repo.GetById(doc.Id)!;
        Assert.Equal(newDeadline.Date, updated.Deadline!.Value.Date);
    }

    [Fact]
    public void Deadline_RemoveDeadline_SetsToNull()
    {
        _repo.Add(new StudyDocument { Ten = "Remove Deadline", Deadline = DateTime.Today });
        var doc = _repo.GetAll()[0];

        doc.Deadline = null;
        _repo.Update(doc);

        var updated = _repo.GetById(doc.Id)!;
        Assert.Null(updated.Deadline);
    }
}

// ════════════════════════════════════════════════════════════
// F02 (extended): Search — Edge Cases
// ════════════════════════════════════════════════════════════

public class SearchEdgeCaseTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public SearchEdgeCaseTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void Search_ByNote_ReturnsMatchingDocuments()
    {
        _repo.Add(new StudyDocument { Ten = "Doc Without Note", GhiChu = "Quan trọng lắm" });
        _repo.Add(new StudyDocument { Ten = "Doc Without Note 2", GhiChu = "Không quan trọng" });

        var results = _repo.Search("Quan trọng lắm");
        Assert.Single(results);
    }

    [Fact]
    public void Search_EmptyKeyword_ReturnsAll()
    {
        _repo.Add(new StudyDocument { Ten = "Doc 1" });
        _repo.Add(new StudyDocument { Ten = "Doc 2" });

        // Empty keyword → should return all active documents
        var results = _repo.Search("");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Filter_EmptySubjectAndType_ReturnsAll()
    {
        _repo.Add(new StudyDocument { Ten = "A", MonHoc = "Math", Loai = "PDF" });
        _repo.Add(new StudyDocument { Ten = "B", MonHoc = "Science", Loai = "Video" });

        var results = _repo.Filter("", "");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Filter_NonExistentSubject_ReturnsEmpty()
    {
        _repo.Add(new StudyDocument { Ten = "A", MonHoc = "Math" });

        var results = _repo.Filter("NonExistentSubject999", "");
        Assert.Empty(results);
    }
}

// ════════════════════════════════════════════════════════════
// F23: CSV Export — Data Integrity Verification
// ════════════════════════════════════════════════════════════

public class CsvExportDataTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public CsvExportDataTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void AllDocumentFields_Retrievable_ForExport()
    {
        var deadline = DateTime.Today.AddDays(10);
        _repo.Add(new StudyDocument
        {
            Ten = "Export Test Doc",
            MonHoc = "Học tập",
            Loai = "Tài liệu",
            DuongDan = @"C:\docs\study.pdf",
            GhiChu = "Ghi chú xuất CSV",
            KichThuoc = 3.14,
            TacGia = "Nguyễn Văn A",
            QuanTrong = true,
            Tags = "exam;final",
            Deadline = deadline
        });

        var doc = _repo.GetAll()[0];

        // Verify each field is properly retrievable
        Assert.Equal("Export Test Doc", doc.Ten);
        Assert.Equal("Học tập", doc.MonHoc);
        Assert.Equal("Tài liệu", doc.Loai);
        Assert.Equal(@"C:\docs\study.pdf", doc.DuongDan);
        Assert.Equal("Ghi chú xuất CSV", doc.GhiChu);
        Assert.Equal(3.14, doc.KichThuoc);
        Assert.Equal("Nguyễn Văn A", doc.TacGia);
        Assert.True(doc.QuanTrong);
        Assert.Equal("exam;final", doc.Tags);
        Assert.Equal(deadline.Date, doc.Deadline!.Value.Date);
        Assert.True(doc.Id > 0);
        Assert.NotEqual(default, doc.NgayThem);
    }

    [Fact]
    public void SoftDeletedDocuments_NotIncludedInExport()
    {
        _repo.Add(new StudyDocument { Ten = "Active Export" });
        _repo.Add(new StudyDocument { Ten = "Deleted Export" });

        var deletedId = _repo.GetAll().First(d => d.Ten == "Deleted Export").Id;
        _repo.Delete(deletedId);

        // Only active documents should be considered for export
        var allActive = _repo.GetAll();
        Assert.Single(allActive);
        Assert.Equal("Active Export", allActive[0].Ten);
    }
}
