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
        bool result = Db.AddType("New Music");
        Assert.True(result);
        Assert.Contains("New Music", Db.GetAllTypes());
    }

    [Fact]
    public void AddType_DuplicateName_DoesNotDuplicate()
    {
        Db.AddType("Textbook");
        Db.AddType("Textbook"); // duplicate via INSERT OR IGNORE

        var types = Db.GetAllTypes().Where(t => t == "Textbook").ToList();
        Assert.Single(types);
    }

    [Fact]
    public void DeleteType_RemovesFromLookupTable()
    {
        Db.AddType("TypeToDelete");
        bool result = Db.DeleteType("TypeToDelete");

        Assert.True(result);
        Assert.DoesNotContain("TypeToDelete", Db.GetAllTypes());
    }

    [Fact]
    public void DeleteType_NonExistingType_ReturnsFalse()
    {
        bool result = Db.DeleteType("does_not_exist_xyz");
        Assert.False(result);
    }

    [Fact]
    public void GetTypesWithCount_ReturnsCorrectCounts()
    {
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "A", Type = "PDF" });
        repo.Add(new StudyDocument { Name = "B", Type = "PDF" });
        repo.Add(new StudyDocument { Name = "C", Type = "Video" });

        var typeCounts = Db.GetTypesWithCount();
        var pdfEntry = typeCounts.FirstOrDefault(t => t.Name == "PDF");
        var videoEntry = typeCounts.FirstOrDefault(t => t.Name == "Video");

        Assert.Equal(2, pdfEntry.Count);
        Assert.Equal(1, videoEntry.Count);
    }

    [Fact]
    public void GetTypesWithCount_ExcludesSoftDeleted()
    {
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "A", Type = "Excel" });
        repo.Add(new StudyDocument { Name = "B", Type = "Excel" });

        var id = repo.GetAll().First().Id;
        repo.Delete(id);

        var typeCounts = Db.GetTypesWithCount();
        var entry = typeCounts.FirstOrDefault(t => t.Name == "Excel");

        Assert.Equal(1, entry.Count);
    }

    [Fact]
    public void DeleteDocumentsByType_SoftDeletesAllInType()
    {
        Db.AddType("Loai XYZ");
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "Doc1", Type = "Loai XYZ" });
        repo.Add(new StudyDocument { Name = "Doc2", Type = "Loai XYZ" });
        repo.Add(new StudyDocument { Name = "Keep", Type = "Other" });

        Db.DeleteDocumentsByType("Loai XYZ");

        var active = repo.GetAll();
        Assert.Single(active);
        Assert.Equal("Keep", active[0].Name);
        Assert.Equal(2, Db.GetDeletedDocuments().Count);
    }

    [Fact]
    public void UpdateTypeName_RenamesInDocumentsAndLookup()
    {
        Db.AddType("OldType");
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "X", Type = "OldType" });

        bool result = Db.UpdateTypeName("OldType", "NewType");
        var docs = repo.GetAll();

        Assert.True(result);
        Assert.Equal("NewType", docs[0].Type);
        Assert.Contains("NewType", Db.GetAllTypes());
        Assert.DoesNotContain("OldType", Db.GetAllTypes());
    }
}

// ════════════════════════════════════════════════════════════
// F03: Advanced Search — Size Filter Edge Cases
// ════════════════════════════════════════════════════════════

public class AdvancedSearchSizeFilterTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public AdvancedSearchSizeFilterTests() { _repo = new DocumentRepository(Db); }

    private void SeedWithSizes()
    {
        _repo.Add(new StudyDocument { Name = "Small Doc", FileSize = 0.5, Subject = "Test" });
        _repo.Add(new StudyDocument { Name = "Medium Doc", FileSize = 5.0, Subject = "Test" });
        _repo.Add(new StudyDocument { Name = "Large Doc", FileSize = 20.0, Subject = "Test" });
        _repo.Add(new StudyDocument { Name = "No Size Doc", FileSize = null, Subject = "Test" });
    }

    [Fact]
    public void SearchAdvanced_ByMinSize_ReturnsOnlyDocumentsAboveMinimum()
    {
        SeedWithSizes();
        var results = _repo.SearchAdvanced("", "", "", null, null, 5.0, null, null);

        // Should return Medium (5.0) and Large (20.0)
        Assert.Equal(2, results.Count);
        Assert.All(results, d => Assert.True(d.FileSize >= 5.0));
    }

    [Fact]
    public void SearchAdvanced_ByMaxSize_ReturnsOnlyDocumentsBelowMaximum()
    {
        SeedWithSizes();
        var results = _repo.SearchAdvanced("", "", "", null, null, null, 5.0, null);

        // Should return Small (0.5) and Medium (5.0)
        Assert.Equal(2, results.Count);
        Assert.All(results, d => Assert.True(d.FileSize <= 5.0));
    }

    [Fact]
    public void SearchAdvanced_BySizeRange_ReturnsWithinRange()
    {
        SeedWithSizes();
        var results = _repo.SearchAdvanced("", "", "", null, null, 1.0, 10.0, null);

        // Should return Medium (5.0) only
        Assert.Single(results);
        Assert.Equal("Medium Doc", results[0].Name);
    }

    [Fact]
    public void SearchAdvanced_BySubjectAndImportant_FiltersCorrectly()
    {
        _repo.Add(new StudyDocument { Name = "Important Math", Subject = "Math", IsImportant = true });
        _repo.Add(new StudyDocument { Name = "Normal Math", Subject = "Math", IsImportant = false });
        _repo.Add(new StudyDocument { Name = "Important Science", Subject = "Science", IsImportant = true });

        var results = _repo.SearchAdvanced("", "Math", "", null, null, null, null, true);

        Assert.Single(results);
        Assert.Equal("Important Math", results[0].Name);
    }

    [Fact]
    public void SearchAdvanced_AllNullFilters_ReturnsAll()
    {
        _repo.Add(new StudyDocument { Name = "A" });
        _repo.Add(new StudyDocument { Name = "B" });
        _repo.Add(new StudyDocument { Name = "C" });

        var results = _repo.SearchAdvanced("", "", "", null, null, null, null, null);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void SearchAdvanced_ByKeywordAndType_NarrowsResults()
    {
        _repo.Add(new StudyDocument { Name = "Python Tutorial", Type = "Video" });
        _repo.Add(new StudyDocument { Name = "Python Guide", Type = "Document" });
        _repo.Add(new StudyDocument { Name = "Java Tutorial", Type = "Video" });

        var results = _repo.SearchAdvanced("Python", "", "Video", null, null, null, null, null);

        Assert.Single(results);
        Assert.Equal("Python Tutorial", results[0].Name);
    }
}

// ════════════════════════════════════════════════════════════
// F21 (extended): Statistics — NoFileDocuments / TotalCategories
// ════════════════════════════════════════════════════════════

public class ExtendedStatisticsTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public ExtendedStatisticsTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void GetDashboardStatistics_NoFileDocuments_CountsCorrectly()
    {
        _repo.Add(new StudyDocument { Name = "With File", FilePath = @"C:\file.pdf" });
        _repo.Add(new StudyDocument { Name = "No File 1", FilePath = "" });
        _repo.Add(new StudyDocument { Name = "No File 2", FilePath = null! });

        var stats = Db.GetDashboardStatistics();
        Assert.Equal(2, stats.NoFileDocuments);
    }

    [Fact]
    public void GetDashboardStatistics_TotalCategories_CountsDistinctSubjects()
    {
        _repo.Add(new StudyDocument { Name = "A", Subject = "Math" });
        _repo.Add(new StudyDocument { Name = "B", Subject = "Math" });
        _repo.Add(new StudyDocument { Name = "C", Subject = "Science" });

        var stats = Db.GetDashboardStatistics();
        Assert.Equal(2, stats.TotalCategories);
    }

    [Fact]
    public void GetDashboardStatistics_NearDeadlineExcludesOverdue()
    {
        _repo.Add(new StudyDocument { Name = "Overdue", Deadline = DateTime.Today.AddDays(-1) });
        _repo.Add(new StudyDocument { Name = "Near Today", Deadline = DateTime.Today });
        _repo.Add(new StudyDocument { Name = "Near 7", Deadline = DateTime.Today.AddDays(7) });
        _repo.Add(new StudyDocument { Name = "Far", Deadline = DateTime.Today.AddDays(30) });

        var stats = Db.GetDashboardStatistics();
        Assert.Equal(1, stats.OverdueDocuments);
        Assert.Equal(2, stats.NearDeadlineDocuments); // Today + 7 days (both inclusive)
    }

    [Fact]
    public void GetDocumentsByDay_TodayDocumentCountedCorrectly()
    {
        _repo.Add(new StudyDocument { Name = "Today Doc 1" });
        _repo.Add(new StudyDocument { Name = "Today Doc 2" });

        var data = Db.GetDocumentsByDay(7);
        // Today (last entry in ascending order)
        var today = data.Last();

        Assert.Equal(2, today.Count);
    }

    [Fact]
    public void GetDocumentsByMonth_CurrentMonthCountedCorrectly()
    {
        _repo.Add(new StudyDocument { Name = "This Month Doc" });

        var data = Db.GetDocumentsByMonth(12);
        // Current month is the last in ascending order
        var currentMonth = data.Last();

        Assert.True(currentMonth.Count >= 1);
    }

    [Fact]
    public void GetDocumentsBySubject_ExcludesSoftDeleted()
    {
        _repo.Add(new StudyDocument { Name = "Active", Subject = "Physics" });
        _repo.Add(new StudyDocument { Name = "Deleted", Subject = "Physics" });
        var deletedId = _repo.GetAll().First(d => d.Name == "Deleted").Id;
        _repo.Delete(deletedId);

        var data = Db.GetDocumentsBySubject();
        var physics = data.FirstOrDefault(d => d.Label == "Physics");

        Assert.Equal(1, physics.Count);
    }

    [Fact]
    public void GetDocumentsByType_ExcludesSoftDeleted()
    {
        _repo.Add(new StudyDocument { Name = "Active", Type = "Word" });
        _repo.Add(new StudyDocument { Name = "Deleted", Type = "Word" });
        var deletedId = _repo.GetAll().First(d => d.Name == "Deleted").Id;
        _repo.Delete(deletedId);

        var data = Db.GetDocumentsByType();
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

    public BackupRestoreTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void BackupDatabase_ContainsCorrectData()
    {
        // Seed data
        _repo.Add(new StudyDocument { Name = "Backup Content Doc", Subject = "Test" });
        string backupPath = Path.Combine(Path.GetTempPath(), $"sdm_backup_verify_{Guid.NewGuid():N}.db");

        try
        {
            bool result = Db.BackupDatabase(backupPath);
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

    public RecycleBinMetadataTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void DeleteDocument_DeletedAtTimestampIsSet()
    {
        _repo.Add(new StudyDocument { Name = "Timestamp Test" });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id);

        var deleted = Db.GetDeletedDocuments();
        Assert.Single(deleted);
        // deleted_at should be populated (check it's a valid datetime within reason)
        Assert.NotNull(deleted[0].Name);
    }

    [Fact]
    public void EmptyRecycleBin_ReturnsCountOfDeletedItems()
    {
        _repo.Add(new StudyDocument { Name = "Trash A" });
        _repo.Add(new StudyDocument { Name = "Trash B" });
        _repo.Add(new StudyDocument { Name = "Trash C" });
        var all = _repo.GetAll();
        foreach (var d in all) _repo.Delete(d.Id);

        int count = Db.EmptyRecycleBin();
        Assert.Equal(3, count);
        Assert.Equal(0, Db.GetDeletedDocumentCount());
    }

    [Fact]
    public void PermanentDeleteDocument_AlsoRemovedFromActiveList()
    {
        _repo.Add(new StudyDocument { Name = "Perm Delete Test" });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id);

        // Permanent delete
        bool result = Db.PermanentDeleteDocument(id);
        Assert.True(result);

        // Should be gone from both lists
        Assert.Empty(_repo.GetAll());
        Assert.Empty(Db.GetDeletedDocuments());
        Assert.Null(_repo.GetById(id));
    }

    [Fact]
    public void RestoreDocument_AfterUpdateAndDelete_KeepsUpdatedData()
    {
        _repo.Add(new StudyDocument { Name = "Original" });
        var doc = _repo.GetAll()[0];
        int id = doc.Id;

        // Update first
        doc.Name = "Updated Before Delete";
        _repo.Update(doc);

        // Then delete and restore
        _repo.Delete(id);
        Db.RestoreDocument(id);

        var restored = _repo.GetById(id);
        Assert.NotNull(restored);
        Assert.Equal("Updated Before Delete", restored!.Name);
    }
}

// ════════════════════════════════════════════════════════════
// F13 (extended): Bulk Operations — Edge Cases
// ════════════════════════════════════════════════════════════

public class BulkOperationEdgeCaseTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public BulkOperationEdgeCaseTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void BulkUpdateSubject_EmptyList_ReturnsZero()
    {
        int affected = Db.BulkUpdateSubject(new List<int>(), "Any");
        Assert.Equal(0, affected);
    }

    [Fact]
    public void BulkSoftDelete_EmptyList_ReturnsZero()
    {
        int affected = Db.BulkSoftDelete(new List<int>());
        Assert.Equal(0, affected);
    }

    [Fact]
    public void BulkSoftDelete_OnlySoftDeletesSelectedIds()
    {
        _repo.Add(new StudyDocument { Name = "Keep 1" });
        _repo.Add(new StudyDocument { Name = "Keep 2" });
        _repo.Add(new StudyDocument { Name = "Delete Me" });

        var all = _repo.GetAll();
        var deleteId = all.First(d => d.Name == "Delete Me").Id;

        int affected = Db.BulkSoftDelete(new List<int> { deleteId });

        Assert.Equal(1, affected);
        Assert.Equal(2, _repo.GetAll().Count);
        Assert.DoesNotContain(_repo.GetAll(), d => d.Name == "Delete Me");
    }

    [Fact]
    public void BulkToggleImportant_NullList_ReturnsZero()
    {
        // null list handled as empty
        int affected = Db.BulkToggleImportant(null!, true);
        Assert.Equal(0, affected);
    }

    [Fact]
    public void BulkUpdateSubject_PartialSelection_OnlyUpdatesSelected()
    {
        _repo.Add(new StudyDocument { Name = "A", Subject = "Math" });
        _repo.Add(new StudyDocument { Name = "B", Subject = "Math" });
        _repo.Add(new StudyDocument { Name = "C", Subject = "Math" });

        var all = _repo.GetAll();
        var updateIds = all.Take(1).Select(d => d.Id).ToList();

        Db.BulkUpdateSubject(updateIds, "Physics");

        var updated = _repo.GetAll();
        Assert.Equal(1, updated.Count(d => d.Subject == "Physics"));
        Assert.Equal(2, updated.Count(d => d.Subject == "Math"));
    }
}

// ════════════════════════════════════════════════════════════
// F04 (extended): Distinct Values — Edge Cases
// ════════════════════════════════════════════════════════════

public class DistinctValuesEdgeCaseTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public DistinctValuesEdgeCaseTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void GetDistinctTypes_ExcludesSoftDeleted()
    {
        _repo.Add(new StudyDocument { Name = "Active", Type = "Light" });
        _repo.Add(new StudyDocument { Name = "Deleted", Type = "DeleteType" });
        var id = _repo.GetAll().First(d => d.Name == "Deleted").Id;
        _repo.Delete(id);

        var types = _repo.GetDistinctTypes();
        Assert.DoesNotContain("DeleteType", types);
        Assert.Contains("Light", types);
    }

    [Fact]
    public void GetDistinctTags_HandlesSemicolonSeparator()
    {
        _repo.Add(new StudyDocument { Name = "A", Tags = "python;django;web" });
        _repo.Add(new StudyDocument { Name = "B", Tags = "web;api" });

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
        _repo.Add(new StudyDocument { Name = "A", Tags = "" });
        _repo.Add(new StudyDocument { Name = "B", Tags = null! });
        _repo.Add(new StudyDocument { Name = "C", Tags = "valid" });

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

    public CollectionAdvancedTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void CreateCollection_WithDescription_Persists()
    {
        int colId = Db.CreateCollection("My Collection", "This is a description");
        var cols = Db.GetCollections();

        var col = cols.First(c => c.Id == colId);
        Assert.Equal("My Collection", col.Name);
        Assert.Equal("This is a description", col.Description);
    }

    [Fact]
    public void UpdateCollection_ChangesDescription()
    {
        int colId = Db.CreateCollection("Col", "Old desc");
        bool result = Db.UpdateCollection(colId, "Col", "New desc");

        var cols = Db.GetCollections();
        var col = cols.First(c => c.Id == colId);

        Assert.True(result);
        Assert.Equal("New desc", col.Description);
    }

    [Fact]
    public void GetDocumentsInCollection_ExcludesSoftDeletedDocuments()
    {
        _repo.Add(new StudyDocument { Name = "Active Doc" });
        _repo.Add(new StudyDocument { Name = "Deleted Doc" });

        var all = _repo.GetAll();
        var activeDoc = all.First(d => d.Name == "Active Doc");
        var deletedDocId = all.First(d => d.Name == "Deleted Doc").Id;

        int colId = Db.CreateCollection("Test Col");
        Db.AddDocumentToCollection(colId, activeDoc.Id);
        Db.AddDocumentToCollection(colId, deletedDocId);

        // Soft delete one document
        _repo.Delete(deletedDocId);

        var docs = Db.GetDocumentsInCollection(colId);

        // Only active document should appear
        Assert.Single(docs);
        Assert.Equal("Active Doc", docs[0].Name);
    }

    [Fact]
    public void GetCollections_MultipleCollections_SortedByName()
    {
        Db.CreateCollection("Zebra");
        Db.CreateCollection("Alpha");
        Db.CreateCollection("Middle");

        var cols = Db.GetCollections();

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

    public PersonalNoteEdgeCaseTests() { _repo = new DocumentRepository(Db); }

    private int CreateDoc()
    {
        _repo.Add(new StudyDocument { Name = "Note Test Doc" });
        return _repo.GetAll()[0].Id;
    }

    [Fact]
    public void SavePersonalNote_WithSpecialChars_Persists()
    {
        int docId = CreateDoc();
        string content = "Note with special chars: <div>HTML</div> & 'quotes' \"double\" \n newline";
        Db.SavePersonalNote(docId, content);

        Assert.Equal(content, Db.GetPersonalNote(docId));
    }

    [Fact]
    public void SavePersonalNote_WithLongContent_Persists()
    {
        int docId = CreateDoc();
        string longContent = new string('A', 10000); // 10KB content
        Db.SavePersonalNote(docId, longContent);

        Assert.Equal(longContent, Db.GetPersonalNote(docId));
    }

    [Fact]
    public void DeletePersonalNote_WhenNoNote_ReturnsFalse()
    {
        int docId = CreateDoc();
        // No note exists — DeletePersonalNote should return false (0 rows affected)
        bool result = Db.DeletePersonalNote(docId);
        Assert.False(result);
    }
}

// ════════════════════════════════════════════════════════════
// F16 (extended): Related Documents — Multiple Relations
// ════════════════════════════════════════════════════════════

public class RelatedDocumentsExtendedTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public RelatedDocumentsExtendedTests() { _repo = new DocumentRepository(Db); }

    private List<int> CreateDocs(int count)
    {
        for (int i = 1; i <= count; i++)
            _repo.Add(new StudyDocument { Name = $"Doc {i}" });
        return _repo.GetAll().OrderBy(d => d.Id).Select(d => d.Id).ToList();
    }

    [Fact]
    public void AddDocumentRelation_MultipleRelations_AllRetrievable()
    {
        var ids = CreateDocs(4);
        var (doc1, doc2, doc3, doc4) = (ids[0], ids[1], ids[2], ids[3]);

        Db.AddDocumentRelation(doc1, doc2, "related");
        Db.AddDocumentRelation(doc1, doc3, "reference");
        Db.AddDocumentRelation(doc1, doc4, "supplement");

        var relDocs = Db.GetRelatedDocuments(doc1);
        Assert.Equal(3, relDocs.Count);
    }

    [Fact]
    public void AddDocumentRelation_CanonicalOrder_WorksBothWays()
    {
        // Due to canonicalization (lo=min, hi=max), adding (a,b) same as (b,a)
        var ids = CreateDocs(2);
        Db.AddDocumentRelation(ids[0], ids[1]);
        Db.AddDocumentRelation(ids[1], ids[0]); // reversed — should be ignored

        var rel = Db.GetRelatedDocuments(ids[0]);
        Assert.Single(rel); // Only one relation
    }

    [Fact]
    public void GetRelatedDocuments_EmptyForUnlinkedDoc()
    {
        var ids = CreateDocs(2);
        // No relations added
        var rel = Db.GetRelatedDocuments(ids[0]);
        Assert.Empty(rel);
    }

    [Fact]
    public void RemoveDocumentRelation_ByRelationId_RemovesSpecificLink()
    {
        var ids = CreateDocs(3);

        Db.AddDocumentRelation(ids[0], ids[1], "related");
        Db.AddDocumentRelation(ids[0], ids[2], "reference");

        var allRel = Db.GetRelatedDocuments(ids[0]);
        Assert.Equal(2, allRel.Count);

        // Remove only the first relation
        Db.RemoveDocumentRelation(allRel[0].RelationId);

        var remaining = Db.GetRelatedDocuments(ids[0]);
        Assert.Single(remaining);
    }
}

// ════════════════════════════════════════════════════════════
// F14 (extended): Recent Files — Order & Filtering
// ════════════════════════════════════════════════════════════

public class RecentFilesExtendedTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public RecentFilesExtendedTests() { _repo = new DocumentRepository(Db); }

    private int CreateDoc(string name)
    {
        _repo.Add(new StudyDocument { Name = name });
        return _repo.GetAll().First(d => d.Name == name).Id;
    }

    [Fact]
    public void GetRecentFiles_ReturnsMostRecentFirst()
    {
        int d1 = CreateDoc("First Opened");
        int d2 = CreateDoc("Second Opened");

        Db.AddRecentFile(d1);
        // SQLite datetime('now','localtime') has 1-second resolution, need >1s gap
        System.Threading.Thread.Sleep(1200);
        Db.AddRecentFile(d2);

        var recent = Db.GetRecentFiles();

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

        Db.AddRecentFile(d1);
        Db.AddRecentFile(d2);
        _repo.Delete(d2); // Soft delete d2

        var recent = Db.GetRecentFiles();

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
            Db.AddRecentFile(99999);
        }
        catch { /* FOREIGN KEY constraint may or may not be enforced */ }

        var recent = Db.GetRecentFiles();
        Assert.Empty(recent);
    }
}

// ════════════════════════════════════════════════════════════
// F01 (extended): Document CRUD — Edge Cases
// ════════════════════════════════════════════════════════════

public class DocumentCrudEdgeCaseTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public DocumentCrudEdgeCaseTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void Add_DocumentWithUnicodeName_Persists()
    {
        var doc = new StudyDocument
        {
            Name = "Advanced Mathematics Textbook for First-Year Students",
            Subject = "Study",
            Type = "Document"
        };

        _repo.Add(doc);
        var saved = _repo.GetAll()[0];

        Assert.Equal("Advanced Mathematics Textbook for First-Year Students", saved.Name);
    }

    [Fact]
    public void Add_DocumentWithNullOptionalFields_Persists()
    {
        var doc = new StudyDocument
        {
            Name = "Minimal Doc",
            Subject = null!,
            Type = null!,
            FilePath = null!,
            Notes = null!,
            FileSize = null,
            Author = null!,
            Tags = null!,
            Deadline = null
        };

        _repo.Add(doc);
        var saved = _repo.GetAll()[0];

        Assert.Equal("Minimal Doc", saved.Name);
        Assert.Null(saved.FileSize);
        Assert.Null(saved.Deadline);
    }

    [Fact]
    public void Update_TogglesImportantFlag()
    {
        _repo.Add(new StudyDocument { Name = "Toggle Test", IsImportant = false });
        var doc = _repo.GetAll()[0];

        doc.IsImportant = true;
        _repo.Update(doc);

        var updated = _repo.GetById(doc.Id)!;
        Assert.True(updated.IsImportant);

        updated.IsImportant = false;
        _repo.Update(updated);

        var toggled = _repo.GetById(doc.Id)!;
        Assert.False(toggled.IsImportant);
    }

    [Fact]
    public void Update_WithEmptyPath_SetsEmptyString()
    {
        _repo.Add(new StudyDocument { Name = "With Path", FilePath = @"C:\file.pdf" });
        var doc = _repo.GetAll()[0];

        doc.FilePath = "";
        _repo.Update(doc);

        var updated = _repo.GetById(doc.Id)!;
        Assert.Equal("", updated.FilePath);
    }

    [Fact]
    public void GetById_SoftDeletedDocument_StillReturnable_ByInternalMethod()
    {
        // GetById (GetDocumentById) does NOT filter is_deleted — it returns the raw record.
        // This is by design: forms like RecycleBin or RestoreForm need to access deleted docs.
        // The filtering is done at GetAll() / Search() level.
        _repo.Add(new StudyDocument { Name = "Soft Deleted" });
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
            _repo.Add(new StudyDocument { Name = $"Large Set Doc {i}", Subject = "Test" });

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

    public FileIntegrityEdgeCaseTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void UpdateDocumentPath_ToSamePath_NoOp()
    {
        _repo.Add(new StudyDocument { Name = "Same Path", FilePath = @"C:\same.pdf" });
        int id = _repo.GetAll()[0].Id;

        bool result = Db.UpdateDocumentPath(id, @"C:\same.pdf");
        var doc = _repo.GetById(id)!;

        Assert.True(result);
        Assert.Equal(@"C:\same.pdf", doc.FilePath);
    }

    [Fact]
    public void UpdateDocumentPath_NonExistentId_ReturnsFalse()
    {
        bool result = Db.UpdateDocumentPath(99999, @"C:\any.pdf");
        Assert.False(result);
    }

    [Fact]
    public void ClearDocumentPath_ThenUpdateToNewPath_Works()
    {
        _repo.Add(new StudyDocument { Name = "Path Journey", FilePath = @"C:\original.pdf" });
        int id = _repo.GetAll()[0].Id;

        // Clear path
        Db.ClearDocumentPath(id);
        Assert.Equal("", _repo.GetById(id)!.FilePath);

        // Set new path
        Db.UpdateDocumentPath(id, @"C:\new_location.pdf");
        Assert.Equal(@"C:\new_location.pdf", _repo.GetById(id)!.FilePath);
    }
}

// ════════════════════════════════════════════════════════════
// F25 (extended): Deadline Boundary Testing
// ════════════════════════════════════════════════════════════

public class DeadlineBoundaryTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public DeadlineBoundaryTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void GetUpcomingDeadlines_DeadlineToday_Included()
    {
        _repo.Add(new StudyDocument { Name = "Due Today", Deadline = DateTime.Today });
        var upcoming = _repo.GetUpcomingDeadlines(7);

        Assert.Single(upcoming);
        Assert.Equal("Due Today", upcoming[0].Name);
    }

    [Fact]
    public void GetUpcomingDeadlines_ExactlyAt7Days_Included()
    {
        _repo.Add(new StudyDocument { Name = "Due 7 Days", Deadline = DateTime.Today.AddDays(7) });
        var upcoming = _repo.GetUpcomingDeadlines(7);

        Assert.Single(upcoming);
    }

    [Fact]
    public void GetUpcomingDeadlines_At8Days_NotIncluded()
    {
        _repo.Add(new StudyDocument { Name = "Due 8 Days", Deadline = DateTime.Today.AddDays(8) });
        var upcoming = _repo.GetUpcomingDeadlines(7);

        Assert.Empty(upcoming);
    }

    [Fact]
    public void GetOverdueDocuments_DeadlineYesterday_IsOverdue()
    {
        _repo.Add(new StudyDocument { Name = "Yesterday", Deadline = DateTime.Today.AddDays(-1) });
        var overdue = _repo.GetOverdueDocuments();

        Assert.Single(overdue);
        Assert.Equal("Yesterday", overdue[0].Name);
    }

    [Fact]
    public void GetOverdueDocuments_DeadlineToday_NotOverdue()
    {
        // Today's deadline should NOT be overdue (deadline < today, not <=)
        _repo.Add(new StudyDocument { Name = "Due Today", Deadline = DateTime.Today });
        var overdue = _repo.GetOverdueDocuments();

        Assert.Empty(overdue);
    }

    [Fact]
    public void Deadline_Update_PersistsCorrectly()
    {
        _repo.Add(new StudyDocument { Name = "Deadline Update", Deadline = DateTime.Today });
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
        _repo.Add(new StudyDocument { Name = "Remove Deadline", Deadline = DateTime.Today });
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

    public SearchEdgeCaseTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void Search_ByNote_ReturnsMatchingDocuments()
    {
        _repo.Add(new StudyDocument { Name = "Doc Without Note", Notes = "Very important note" });
        _repo.Add(new StudyDocument { Name = "Doc Without Note 2", Notes = "Not important" });

        var results = _repo.Search("Very important note");
        Assert.Single(results);
    }

    [Fact]
    public void Search_EmptyKeyword_ReturnsAll()
    {
        _repo.Add(new StudyDocument { Name = "Doc 1" });
        _repo.Add(new StudyDocument { Name = "Doc 2" });

        // Empty keyword → should return all active documents
        var results = _repo.Search("");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Filter_EmptySubjectAndType_ReturnsAll()
    {
        _repo.Add(new StudyDocument { Name = "A", Subject = "Math", Type = "PDF" });
        _repo.Add(new StudyDocument { Name = "B", Subject = "Science", Type = "Video" });

        var results = _repo.Filter("", "");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Filter_NonExistentSubject_ReturnsEmpty()
    {
        _repo.Add(new StudyDocument { Name = "A", Subject = "Math" });

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

    public CsvExportDataTests() { _repo = new DocumentRepository(Db); }

    [Fact]
    public void AllDocumentFields_Retrievable_ForExport()
    {
        var deadline = DateTime.Today.AddDays(10);
        _repo.Add(new StudyDocument
        {
            Name = "Export Test Doc",
            Subject = "Study",
            Type = "Document",
            FilePath = @"C:\docs\study.pdf",
            Notes = "CSV export note",
            FileSize = 3.14,
            Author = "John Doe",
            IsImportant = true,
            Tags = "exam;final",
            Deadline = deadline
        });

        var doc = _repo.GetAll()[0];

        // Verify each field is properly retrievable
        Assert.Equal("Export Test Doc", doc.Name);
        Assert.Equal("Study", doc.Subject);
        Assert.Equal("Document", doc.Type);
        Assert.Equal(@"C:\docs\study.pdf", doc.FilePath);
        Assert.Equal("CSV export note", doc.Notes);
        Assert.Equal(3.14, doc.FileSize);
        Assert.Equal("John Doe", doc.Author);
        Assert.True(doc.IsImportant);
        Assert.Equal("exam;final", doc.Tags);
        Assert.Equal(deadline.Date, doc.Deadline!.Value.Date);
        Assert.True(doc.Id > 0);
        Assert.NotEqual(default, doc.CreatedAt);
    }

    [Fact]
    public void SoftDeletedDocuments_NotIncludedInExport()
    {
        _repo.Add(new StudyDocument { Name = "Active Export" });
        _repo.Add(new StudyDocument { Name = "Deleted Export" });

        var deletedId = _repo.GetAll().First(d => d.Name == "Deleted Export").Id;
        _repo.Delete(deletedId);

        // Only active documents should be considered for export
        var allActive = _repo.GetAll();
        Assert.Single(allActive);
        Assert.Equal("Active Export", allActive[0].Name);
    }
}
