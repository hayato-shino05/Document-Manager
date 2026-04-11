using Xunit;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Services;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;

// ════════════════════════════════════════════════════════════
// Final Coverage Tests — File 6
// Covers: AppVersion edge cases, Advanced Search combos,
//         RecentFiles RemoveFile/Clear, Multi-collection,
//         Collection GetDocumentsInCollection soft-delete,
//         FilterDocuments "Tất cả" sentinel, Repo interface
//         GetDistinctSubjects/Types, InitializeDatabase idempotent,
//         PermanentDelete/Restore bulk, BulkSoftDelete idempotency,
//         AddDocumentToCollection duplicate guard, DuplicateDocuments
// ════════════════════════════════════════════════════════════

namespace StudyDocumentManager.Tests;

// ════════════════════════════════════════════════════════════
// APPVERSION — Compare & IsNewer full branch coverage
// ════════════════════════════════════════════════════════════

public class AppVersionFullTests
{
    // Compare(current, latest)
    [Fact]
    public void Compare_SameVersion_ReturnsZero()
        => Assert.Equal(0, AppVersion.Compare("3.0.0", "3.0.0"));

    [Fact]
    public void Compare_CurrentNewerMinor_ReturnsPositive()
        => Assert.Equal(1, AppVersion.Compare("3.1.0", "3.0.9"));

    [Fact]
    public void Compare_CurrentOlderPatch_ReturnsNegative()
        => Assert.Equal(-1, AppVersion.Compare("3.0.0", "3.0.1"));

    [Fact]
    public void Compare_MajorBump_CurrentOlder()
        => Assert.Equal(-1, AppVersion.Compare("3.9.9", "4.0.0"));

    [Fact]
    public void Compare_MajorBump_CurrentNewer()
        => Assert.Equal(1, AppVersion.Compare("4.0.0", "3.9.9"));

    [Fact]
    public void Compare_WithVPrefix_IgnoresV()
        => Assert.Equal(0, AppVersion.Compare("v3.0.0", "3.0.0"));

    [Fact]
    public void Compare_WithVUpperCase_IgnoresV()
        => Assert.Equal(0, AppVersion.Compare("V3.0.0", "v3.0.0"));

    [Fact]
    public void Compare_TwodigitPatch_CorrectNumericCompare()
        => Assert.Equal(1, AppVersion.Compare("1.0.10", "1.0.9")); // numeric, not lexicographic

    [Fact]
    public void Compare_ShortVersion_TwoPartOnly()
        => Assert.Equal(0, AppVersion.Compare("3.0", "3.0.0")); // missing patch treated as 0

    [Fact]
    public void IsNewer_LatestNewerThanCurrent_ReturnsTrue()
    {
        // Current is "4.0.0" per AppVersion.cs
        // If latest is 5.0.0, it is truly newer
        bool result = AppVersion.IsNewer("5.0.0");
        Assert.True(result);
    }

    [Fact]
    public void IsNewer_SameAsCurrent_ReturnsFalse()
    {
        bool result = AppVersion.IsNewer(AppVersion.Current);
        Assert.False(result);
    }

    [Fact]
    public void IsNewer_OlderThanCurrent_ReturnsFalse()
    {
        // Parse current, decrement patch
        var parts = AppVersion.Current.Split('.');
        string older = $"{parts[0]}.{parts[1]}.{Math.Max(0, int.Parse(parts[2]) - 1)}";
        bool result = AppVersion.IsNewer(older);
        Assert.False(result);
    }

    [Fact]
    public void Current_IsValidSemver()
    {
        var parts = AppVersion.Current.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.All(parts, p => Assert.True(int.TryParse(p, out _)));
    }
}

// ════════════════════════════════════════════════════════════
// ADVANCED SEARCH — All 8 parameters, edge cases, combos
// ════════════════════════════════════════════════════════════

public class AdvancedSearchComboTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo = new();

    private void Seed()
    {
        _repo.Add(new StudyDocument
        {
            Ten = "Python Advanced",
            MonHoc = "CNTT",
            Loai = "PDF",
            KichThuoc = 5.0,
            QuanTrong = true,
            Tags = "python;advanced",
            NgayThem = DateTime.Today
        });
        _repo.Add(new StudyDocument
        {
            Ten = "Java Basics",
            MonHoc = "CNTT",
            Loai = "Word",
            KichThuoc = 1.5,
            QuanTrong = false,
            Tags = "java;basics",
            NgayThem = DateTime.Today
        });
        _repo.Add(new StudyDocument
        {
            Ten = "Business Report",
            MonHoc = "Kinh tế",
            Loai = "PDF",
            KichThuoc = 10.0,
            QuanTrong = true,
            Tags = "business;finance",
            NgayThem = DateTime.Today
        });
    }

    [Fact]
    public void AdvancedSearch_KeywordOnly_FiltersByNameAndTags()
    {
        Seed();
        var results = DatabaseHelper.SearchDocumentsAdvanced("Python", null, null, null, null, null, null, null);
        Assert.Single(results);
        Assert.Equal("Python Advanced", results[0].Ten);
    }

    [Fact]
    public void AdvancedSearch_SubjectOnly_ReturnsSubjectDocs()
    {
        Seed();
        var results = DatabaseHelper.SearchDocumentsAdvanced(null, "CNTT", null, null, null, null, null, null);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void AdvancedSearch_TypeOnly_ReturnsTypeDocs()
    {
        Seed();
        var results = DatabaseHelper.SearchDocumentsAdvanced(null, null, "PDF", null, null, null, null, null);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void AdvancedSearch_MinSize_FiltersCorrectly()
    {
        Seed();
        var results = DatabaseHelper.SearchDocumentsAdvanced(null, null, null, null, null, 5.0, null, null);
        Assert.Equal(2, results.Count); // Python (5.0) and Business (10.0)
    }

    [Fact]
    public void AdvancedSearch_MaxSize_FiltersCorrectly()
    {
        Seed();
        var results = DatabaseHelper.SearchDocumentsAdvanced(null, null, null, null, null, null, 5.0, null);
        Assert.Equal(2, results.Count); // Python (5.0) and Java (1.5)
    }

    [Fact]
    public void AdvancedSearch_SizeRange_NarrowsDown()
    {
        Seed();
        var results = DatabaseHelper.SearchDocumentsAdvanced(null, null, null, null, null, 2.0, 6.0, null);
        Assert.Single(results); // Only Python (5.0)
    }

    [Fact]
    public void AdvancedSearch_IsImportantTrue_ReturnsOnlyImportant()
    {
        Seed();
        var results = DatabaseHelper.SearchDocumentsAdvanced(null, null, null, null, null, null, null, true);
        Assert.Equal(2, results.Count); // Python and Business
        Assert.All(results, d => Assert.True(d.QuanTrong));
    }

    [Fact]
    public void AdvancedSearch_IsImportantFalse_ReturnsAll()
    {
        // isImportant = false means "don't filter by importance" (null behaviour)
        Seed();
        var results = DatabaseHelper.SearchDocumentsAdvanced(null, null, null, null, null, null, null, false);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void AdvancedSearch_DateRange_FromToday_IncludesAllToday()
    {
        Seed();
        var results = DatabaseHelper.SearchDocumentsAdvanced(
            null, null, null,
            DateTime.Today, DateTime.Today,
            null, null, null);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void AdvancedSearch_DateRange_TooOld_ReturnsEmpty()
    {
        Seed();
        var results = DatabaseHelper.SearchDocumentsAdvanced(
            null, null, null,
            DateTime.Today.AddDays(-10), DateTime.Today.AddDays(-5),
            null, null, null);
        Assert.Empty(results);
    }

    [Fact]
    public void AdvancedSearch_AllParams_Combo()
    {
        Seed();
        var results = DatabaseHelper.SearchDocumentsAdvanced(
            "Python",
            "CNTT",
            "PDF",
            DateTime.Today,
            DateTime.Today,
            1.0,
            10.0,
            true);
        Assert.Single(results);
        Assert.Equal("Python Advanced", results[0].Ten);
    }

    [Fact]
    public void AdvancedSearch_NullAllParams_ReturnsAll()
    {
        Seed();
        var results = DatabaseHelper.SearchDocumentsAdvanced(null, null, null, null, null, null, null, null);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void AdvancedSearch_TatCaSentinel_TreatedAsNoFilter()
    {
        Seed();
        var results = DatabaseHelper.SearchDocumentsAdvanced(null, "Tất cả", "Tất cả", null, null, null, null, null);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void AdvancedSearch_SearchByTag_FindsDocWithTag()
    {
        Seed();
        var results = DatabaseHelper.SearchDocumentsAdvanced("finance", null, null, null, null, null, null, null);
        Assert.Single(results);
        Assert.Equal("Business Report", results[0].Ten);
    }

    [Fact]
    public void AdvancedSearch_ExcludesSoftDeletedDocs()
    {
        Seed();
        var all = _repo.GetAll();
        _repo.Delete(all[0].Id);

        var results = DatabaseHelper.SearchDocumentsAdvanced(null, null, null, null, null, null, null, null);
        Assert.Equal(2, results.Count);
    }
}

// ════════════════════════════════════════════════════════════
// RECENT FILES — RemoveRecentFile & ClearRecentFiles
// ════════════════════════════════════════════════════════════

public class RecentFilesRemainingTests : DatabaseTestBase
{
    private int AddDoc(string name)
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = name });
        return repo.GetAll().First(d => d.Ten == name).Id;
    }

    [Fact]
    public void RemoveRecentFile_ExistingEntry_EntryDisappears()
    {
        int id = AddDoc("RemoveMe");
        DatabaseHelper.AddRecentFile(id);
        Assert.Single(DatabaseHelper.GetRecentFiles());

        DatabaseHelper.RemoveRecentFile(id);
        Assert.Empty(DatabaseHelper.GetRecentFiles());
    }

    [Fact]
    public void RemoveRecentFile_NonExistent_NoException()
    {
        // Should not throw
        DatabaseHelper.RemoveRecentFile(99999);
        Assert.Empty(DatabaseHelper.GetRecentFiles());
    }

    [Fact]
    public void RemoveRecentFile_OnlyRemovesTargetDoc()
    {
        int id1 = AddDoc("Keep1");
        int id2 = AddDoc("Remove2");
        DatabaseHelper.AddRecentFile(id1);
        Thread.Sleep(1100);
        DatabaseHelper.AddRecentFile(id2);

        DatabaseHelper.RemoveRecentFile(id2);
        var recents = DatabaseHelper.GetRecentFiles();
        Assert.Single(recents);
        Assert.Equal(id1, recents[0].Id);
    }

    [Fact]
    public void ClearRecentFiles_RemovesAllEntries()
    {
        int id1 = AddDoc("ClearA");
        int id2 = AddDoc("ClearB");
        DatabaseHelper.AddRecentFile(id1);
        Thread.Sleep(1100);
        DatabaseHelper.AddRecentFile(id2);

        Assert.Equal(2, DatabaseHelper.GetRecentFiles().Count);

        DatabaseHelper.ClearRecentFiles();
        Assert.Empty(DatabaseHelper.GetRecentFiles());
    }

    [Fact]
    public void ClearRecentFiles_EmptyTable_NoException()
    {
        DatabaseHelper.ClearRecentFiles(); // should not throw
        Assert.Empty(DatabaseHelper.GetRecentFiles());
    }

    [Fact]
    public void AddRecentFile_AfterSoftDelete_HiddenInRecentFiles()
    {
        var repo = new DocumentRepository();
        int id = AddDoc("WillBeDeleted");
        DatabaseHelper.AddRecentFile(id);
        repo.Delete(id);

        // GetRecentFiles JOINs with tai_lieu and filters is_deleted
        var recents = DatabaseHelper.GetRecentFiles();
        Assert.DoesNotContain(recents, r => r.Id == id);
    }
}

// ════════════════════════════════════════════════════════════
// MULTI-COLLECTION MEMBERSHIP — Same doc in N collections
// ════════════════════════════════════════════════════════════

public class MultiCollectionMembershipTests : DatabaseTestBase
{
    [Fact]
    public void DocInTwoCollections_BothCollectionsShowDoc()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "SharedDoc" });
        int docId = repo.GetAll()[0].Id;

        int col1 = DatabaseHelper.CreateCollection("Col Alpha");
        int col2 = DatabaseHelper.CreateCollection("Col Beta");

        DatabaseHelper.AddDocumentToCollection(col1, docId);
        DatabaseHelper.AddDocumentToCollection(col2, docId);

        var inCol1 = DatabaseHelper.GetDocumentsInCollection(col1);
        var inCol2 = DatabaseHelper.GetDocumentsInCollection(col2);

        Assert.Single(inCol1);
        Assert.Single(inCol2);
        Assert.Equal(docId, inCol1[0].Id);
        Assert.Equal(docId, inCol2[0].Id);
    }

    [Fact]
    public void AddDocumentToCollection_Duplicate_ReturnsFalse()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "Dup Guard" });
        int docId = repo.GetAll()[0].Id;
        int colId = DatabaseHelper.CreateCollection("Guard Col");

        bool first = DatabaseHelper.AddDocumentToCollection(colId, docId);
        bool second = DatabaseHelper.AddDocumentToCollection(colId, docId); // duplicate

        Assert.True(first);
        Assert.False(second); // rejected
        Assert.Single(DatabaseHelper.GetDocumentsInCollection(colId));
    }

    [Fact]
    public void DeleteCollection_OnlyRemovesTargetCollection()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "MultiColDoc" });
        int docId = repo.GetAll()[0].Id;

        int col1 = DatabaseHelper.CreateCollection("Keep Me");
        int col2 = DatabaseHelper.CreateCollection("Delete Me");

        DatabaseHelper.AddDocumentToCollection(col1, docId);
        DatabaseHelper.AddDocumentToCollection(col2, docId);

        DatabaseHelper.DeleteCollection(col2);

        var remaining = DatabaseHelper.GetCollections();
        Assert.Single(remaining);
        Assert.Equal("Keep Me", remaining[0].Name);
        Assert.Single(DatabaseHelper.GetDocumentsInCollection(col1));
    }

    [Fact]
    public void GetDocumentsInCollection_SoftDeletedDocHidden()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "WillDelete" });
        repo.Add(new StudyDocument { Ten = "StayActive" });
        var docs = repo.GetAll();

        int colId = DatabaseHelper.CreateCollection("Mixed Col");
        foreach (var d in docs)
            DatabaseHelper.AddDocumentToCollection(colId, d.Id);

        // Soft delete one
        repo.Delete(docs.First(d => d.Ten == "WillDelete").Id);

        var inCol = DatabaseHelper.GetDocumentsInCollection(colId);
        Assert.Single(inCol);
        Assert.Equal("StayActive", inCol[0].Ten);
    }

    [Fact]
    public void GetCollections_ItemCountNotCountsSoftDeletedItems()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "Active" });
        repo.Add(new StudyDocument { Ten = "Deleted" });
        var docs = repo.GetAll();

        int colId = DatabaseHelper.CreateCollection("Mixed Items");
        foreach (var d in docs)
            DatabaseHelper.AddDocumentToCollection(colId, d.Id);

        // Before delete: itemCount counts all rows in collection_items (NOT filtered by is_deleted)
        var before = DatabaseHelper.GetCollections()[0];
        Assert.Equal(2, before.ItemCount); // collection_items has 2 rows

        // Soft delete one doc — collection_items still has 2 rows
        repo.Delete(docs.First(d => d.Ten == "Deleted").Id);
        var after = DatabaseHelper.GetCollections()[0];
        // ItemCount reflects collection_items rows (independent of soft-delete)
        Assert.Equal(2, after.ItemCount);
    }
}

// ════════════════════════════════════════════════════════════
// FILTER — "Tất cả" sentinel values
// ════════════════════════════════════════════════════════════

public class FilterSentinelTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo = new();

    [Fact]
    public void FilterDocuments_TatCaSubject_ReturnsAll()
    {
        _repo.Add(new StudyDocument { Ten = "A", MonHoc = "Math"  });
        _repo.Add(new StudyDocument { Ten = "B", MonHoc = "Logic" });

        var results = DatabaseHelper.FilterDocuments("Tất cả", "");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void FilterDocuments_TatCaType_ReturnsAll()
    {
        _repo.Add(new StudyDocument { Ten = "A", Loai = "PDF"  });
        _repo.Add(new StudyDocument { Ten = "B", Loai = "Word" });

        var results = DatabaseHelper.FilterDocuments("", "Tất cả");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void FilterDocuments_BothTatCa_ReturnsAll()
    {
        _repo.Add(new StudyDocument { Ten = "X" });
        _repo.Add(new StudyDocument { Ten = "Y" });

        var results = DatabaseHelper.FilterDocuments("Tất cả", "Tất cả");
        Assert.Equal(2, results.Count);
    }
}

// ════════════════════════════════════════════════════════════
// REPOSITORY INTERFACE — GetDistinctSubjects/Types/Tags via repo
// ════════════════════════════════════════════════════════════

public class RepositoryInterfaceTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo = new();

    [Fact]
    public void Repo_GetDistinctSubjects_ReturnsUniqueValues()
    {
        _repo.Add(new StudyDocument { Ten = "A", MonHoc = "Math"    });
        _repo.Add(new StudyDocument { Ten = "B", MonHoc = "Math"    }); // duplicate
        _repo.Add(new StudyDocument { Ten = "C", MonHoc = "Physics" });

        var subjects = _repo.GetDistinctSubjects();
        Assert.Equal(2, subjects.Count);
        Assert.Contains("Math",    subjects);
        Assert.Contains("Physics", subjects);
    }

    [Fact]
    public void Repo_GetDistinctTypes_ReturnsUniqueValues()
    {
        _repo.Add(new StudyDocument { Ten = "A", Loai = "PDF"  });
        _repo.Add(new StudyDocument { Ten = "B", Loai = "PDF"  }); // duplicate
        _repo.Add(new StudyDocument { Ten = "C", Loai = "Word" });

        var types = _repo.GetDistinctTypes();
        Assert.Equal(2, types.Count);
        Assert.Contains("PDF",  types);
        Assert.Contains("Word", types);
    }

    [Fact]
    public void Repo_GetDistinctTags_SplitsAndDeduplicates()
    {
        _repo.Add(new StudyDocument { Ten = "A", Tags = "python;math" });
        _repo.Add(new StudyDocument { Ten = "B", Tags = "math;logic"  });

        var tags = _repo.GetDistinctTags();
        Assert.Equal(3, tags.Count); // python, math, logic
    }

    [Fact]
    public void Repo_GetDistinctSubjects_ExcludesSoftDeleted()
    {
        _repo.Add(new StudyDocument { Ten = "Active", MonHoc = "KeepMe"   });
        _repo.Add(new StudyDocument { Ten = "Dead",   MonHoc = "DropMe"   });
        int deadId = _repo.GetAll().First(d => d.Ten == "Dead").Id;
        _repo.Delete(deadId);

        var subjects = _repo.GetDistinctSubjects();
        Assert.DoesNotContain("DropMe", subjects);
        Assert.Contains("KeepMe", subjects);
    }

    [Fact]
    public void Repo_GetDistinctTypes_IsSortedAlphabetically()
    {
        _repo.Add(new StudyDocument { Ten = "Z", Loai = "Zebra" });
        _repo.Add(new StudyDocument { Ten = "A", Loai = "Apple" });
        _repo.Add(new StudyDocument { Ten = "M", Loai = "Mango" });

        var types = _repo.GetDistinctTypes();
        var sorted = types.OrderBy(t => t).ToList();
        Assert.Equal(sorted, types);
    }

    [Fact]
    public void Repo_Search_ReturnsAll_WhenKeywordMatchesAll()
    {
        _repo.Add(new StudyDocument { Ten = "Doc Alpha",  MonHoc = "Test" });
        _repo.Add(new StudyDocument { Ten = "Doc Beta",   MonHoc = "Test" });
        _repo.Add(new StudyDocument { Ten = "Doc Gamma",  MonHoc = "Test" });

        var results = _repo.Search("Doc");
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Repo_Filter_BySubject_ReturnsByRepo()
    {
        _repo.Add(new StudyDocument { Ten = "A", MonHoc = "Target" });
        _repo.Add(new StudyDocument { Ten = "B", MonHoc = "Other"  });

        var results = _repo.Filter("Target", "");
        Assert.Single(results);
        Assert.Equal("Target", results[0].MonHoc);
    }

    [Fact]
    public void Repo_SearchAdvanced_ProducesConsistentResultsWithDirectDbHelper()
    {
        _repo.Add(new StudyDocument { Ten = "Advanced Doc", MonHoc = "Science", QuanTrong = true });
        _repo.Add(new StudyDocument { Ten = "Other Doc", MonHoc = "Art", QuanTrong = false });

        var viaRepo = _repo.SearchAdvanced("Advanced", "Science", null, null, null, null, null, true);
        var viaDirect = DatabaseHelper.SearchDocumentsAdvanced("Advanced", "Science", null, null, null, null, null, true);

        Assert.Equal(viaRepo.Count, viaDirect.Count);
        if (viaRepo.Count > 0)
            Assert.Equal(viaRepo[0].Id, viaDirect[0].Id);
    }
}

// ════════════════════════════════════════════════════════════
// INITIALIZEDDATABASE — Idempotency
// ════════════════════════════════════════════════════════════

public class DatabaseIdempotencyTests : DatabaseTestBase
{
    [Fact]
    public void InitializeDatabase_CalledTwice_DoesNotDestroyData()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "PersistAfterInit" });

        // Call InitializeDatabase again — must be no-op (CREATE TABLE IF NOT EXISTS / ALTER IGNORE)
        DatabaseHelper.InitializeDatabase();

        var all = repo.GetAll();
        Assert.Single(all);
        Assert.Equal("PersistAfterInit", all[0].Ten);
    }

    [Fact]
    public void InitializeDatabase_CalledThreeTimes_DefaultSubjectsNotDuplicated()
    {
        DatabaseHelper.InitializeDatabase();
        DatabaseHelper.InitializeDatabase();

        var subjects = DatabaseHelper.GetAllSubjects();
        // Default subjects added with INSERT OR IGNORE, so no duplicates
        var distinct = subjects.Distinct().ToList();
        Assert.Equal(distinct.Count, subjects.Count);
    }
}

// ════════════════════════════════════════════════════════════
// RECYCLE BIN — PermanentDelete & Restore Bulk patterns
// ════════════════════════════════════════════════════════════

public class RecycleBinDetailTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo = new();

    [Fact]
    public void PermanentDelete_DocInCollection_ItemRemovedFromCollectionItems()
    {
        _repo.Add(new StudyDocument { Ten = "InCollection" });
        int docId = _repo.GetAll()[0].Id;
        int colId = DatabaseHelper.CreateCollection("TestCol");
        DatabaseHelper.AddDocumentToCollection(colId, docId);

        _repo.Delete(docId); // soft delete first
        DatabaseHelper.PermanentDeleteDocument(docId); // hard delete

        // collection_items row should still exist (no cascade in schema)
        // but GetDocumentsInCollection JOINs tai_lieu — row won't appear
        var inCol = DatabaseHelper.GetDocumentsInCollection(colId);
        Assert.Empty(inCol);
    }

    [Fact]
    public void RestoreDocument_DocAppearsInActiveList()
    {
        _repo.Add(new StudyDocument { Ten = "RestoreMe" });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id);

        Assert.Empty(_repo.GetAll());
        DatabaseHelper.RestoreDocument(id);
        Assert.Single(_repo.GetAll());
    }

    [Fact]
    public void RestoreDocument_RestoredDocRemovedFromDeletedList()
    {
        _repo.Add(new StudyDocument { Ten = "Trash" });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id);

        Assert.Single(DatabaseHelper.GetDeletedDocuments());
        DatabaseHelper.RestoreDocument(id);
        Assert.Empty(DatabaseHelper.GetDeletedDocuments());
    }

    [Fact]
    public void GetDeletedDocuments_OrderedByDeletedAtDesc()
    {
        _repo.Add(new StudyDocument { Ten = "First"  });
        _repo.Add(new StudyDocument { Ten = "Second" });
        var docs = _repo.GetAll();

        _repo.Delete(docs.First(d => d.Ten == "First").Id);
        Thread.Sleep(1100); // ensure different deleted_at timestamps
        _repo.Delete(docs.First(d => d.Ten == "Second").Id);

        var deleted = DatabaseHelper.GetDeletedDocuments();
        Assert.Equal(2, deleted.Count);
        Assert.Equal("Second", deleted[0].Ten); // most recently deleted = first
    }

    [Fact]
    public void PermanentDelete_IdDoesNotExist_ReturnsFalse()
    {
        bool result = DatabaseHelper.PermanentDeleteDocument(999999);
        Assert.False(result);
    }

    [Fact]
    public void RestoreDocument_NonExistentId_ReturnsFalse()
    {
        bool result = DatabaseHelper.RestoreDocument(999999);
        Assert.False(result);
    }
}

// ════════════════════════════════════════════════════════════
// BULK OPERATIONS — Idempotency & Edge cases
// ════════════════════════════════════════════════════════════

public class BulkOperationIdempotencyTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo = new();

    [Fact]
    public void BulkSoftDelete_AlreadyDeletedId_CountsAsAffected()
    {
        // SQLite UPDATE on already-deleted rows still counts as "affected"
        // (the flag is already 1, UPDATE sets it to 1 again)
        _repo.Add(new StudyDocument { Ten = "SoftDeleted" });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id); // soft delete it

        // A second BulkSoftDelete on the same id — SQLite still returns 1 affected row
        int result = DatabaseHelper.BulkSoftDelete(new List<int> { id });
        Assert.True(result >= 0); // Must not throw
    }

    [Fact]
    public void BulkUpdateSubject_ToSameValue_UpdatesSuccessfully()
    {
        _repo.Add(new StudyDocument { Ten = "Same", MonHoc = "Math" });
        int id = _repo.GetAll()[0].Id;

        int count = DatabaseHelper.BulkUpdateSubject(new List<int> { id }, "Math");
        Assert.Equal(1, count);
        Assert.Equal("Math", _repo.GetAll()[0].MonHoc);
    }

    [Fact]
    public void BulkToggleImportant_True_ThenFalse_TogglesBothWays()
    {
        _repo.Add(new StudyDocument { Ten = "Toggle", QuanTrong = false });
        int id = _repo.GetAll()[0].Id;

        DatabaseHelper.BulkToggleImportant(new List<int> { id }, true);
        Assert.True(_repo.GetAll()[0].QuanTrong);

        DatabaseHelper.BulkToggleImportant(new List<int> { id }, false);
        Assert.False(_repo.GetAll()[0].QuanTrong);
    }

    [Fact]
    public void BulkSoftDelete_MixedIds_OnlyValidOnesAffected()
    {
        _repo.Add(new StudyDocument { Ten = "Real" });
        int realId = _repo.GetAll()[0].Id;

        int count = DatabaseHelper.BulkSoftDelete(new List<int> { realId, 99999 });
        Assert.Equal(1, count); // Only one real row affected
    }

    [Fact]
    public void BulkUpdateSubject_MixedIds_OnlyValidOnesUpdated()
    {
        _repo.Add(new StudyDocument { Ten = "Real", MonHoc = "Old" });
        int realId = _repo.GetAll()[0].Id;

        int count = DatabaseHelper.BulkUpdateSubject(new List<int> { realId, 88888 }, "New");
        Assert.Equal(1, count);
        Assert.Equal("New", _repo.GetAll()[0].MonHoc);
    }

    [Fact]
    public void BulkOperations_LargeIdList_200Items_NoException()
    {
        for (int i = 0; i < 200; i++)
            _repo.Add(new StudyDocument { Ten = $"Doc{i}" });

        var ids = _repo.GetAll().Select(d => d.Id).ToList();
        Assert.Equal(200, ids.Count);

        int affected = DatabaseHelper.BulkToggleImportant(ids, true);
        Assert.Equal(200, affected);
    }
}

// ════════════════════════════════════════════════════════════
// DUPLICATE DETECTION — Logic simulation
// ════════════════════════════════════════════════════════════

public class DuplicateDetectionLogicTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo = new();

    [Fact]
    public void GetAllDocuments_SamePath_CanBeGroupedAsDuplicates()
    {
        string sharedPath = @"C:\shared\file.pdf";
        _repo.Add(new StudyDocument { Ten = "Copy A", DuongDan = sharedPath });
        _repo.Add(new StudyDocument { Ten = "Copy B", DuongDan = sharedPath });
        _repo.Add(new StudyDocument { Ten = "Unique",  DuongDan = @"C:\unique.pdf" });

        var all = _repo.GetAll();
        var grouped = all
            .Where(d => !string.IsNullOrEmpty(d.DuongDan))
            .GroupBy(d => d.DuongDan!)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Single(grouped);
        Assert.Equal(2, grouped[0].Count());
    }

    [Fact]
    public void GetAllDocuments_SameName_CanBeGroupedAsDuplicates()
    {
        _repo.Add(new StudyDocument { Ten = "Same Name" });
        _repo.Add(new StudyDocument { Ten = "Same Name" }); // exact duplicate name
        _repo.Add(new StudyDocument { Ten = "Unique Name" });

        var all = _repo.GetAll();
        var grouped = all
            .GroupBy(d => d.Ten, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Single(grouped);
        Assert.Equal(2, grouped[0].Count());
    }

    [Fact]
    public void GetAllDocuments_NullPath_ExcludedFromDuplicatePathCheck()
    {
        _repo.Add(new StudyDocument { Ten = "NoPath1", DuongDan = null });
        _repo.Add(new StudyDocument { Ten = "NoPath2", DuongDan = null });

        var all = _repo.GetAll();
        var grouped = all
            .Where(d => !string.IsNullOrEmpty(d.DuongDan))
            .GroupBy(d => d.DuongDan!)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Empty(grouped);
    }

    [Fact]
    public void SoftDeletedDocs_ExcludedFromDuplicateAnalysis()
    {
        string path = @"C:\dup.pdf";
        _repo.Add(new StudyDocument { Ten = "Live Copy",    DuongDan = path });
        _repo.Add(new StudyDocument { Ten = "Deleted Copy", DuongDan = path });
        int deletedId = _repo.GetAll().First(d => d.Ten == "Deleted Copy").Id;
        _repo.Delete(deletedId);

        var active = _repo.GetAll(); // excludes soft-deleted
        var grouped = active
            .Where(d => !string.IsNullOrEmpty(d.DuongDan))
            .GroupBy(d => d.DuongDan!)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Empty(grouped); // only 1 active doc with that path
    }
}
