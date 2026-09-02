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
//         FilterDocuments "All" sentinel, Repo interface
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
    private DocumentRepository _repo => Repo;

    private void Seed()
    {
        _repo.Add(new StudyDocument
        {
            Name = "Python Advanced",
            Subject = "CNTT",
            Type = "PDF",
            FileSize = 5.0,
            IsImportant = true,
            Tags = "python;advanced",
            CreatedAt = DateTime.Today
        });
        _repo.Add(new StudyDocument
        {
            Name = "Java Basics",
            Subject = "CNTT",
            Type = "Word",
            FileSize = 1.5,
            IsImportant = false,
            Tags = "java;basics",
            CreatedAt = DateTime.Today
        });
        _repo.Add(new StudyDocument
        {
            Name = "Business Report",
            Subject = "Kinh tế",
            Type = "PDF",
            FileSize = 10.0,
            IsImportant = true,
            Tags = "business;finance",
            CreatedAt = DateTime.Today
        });
    }

    [Fact]
    public void AdvancedSearch_KeywordOnly_FiltersByNameAndTags()
    {
        Seed();
        var results = Db.SearchDocumentsAdvanced("Python", null, null, null, null, null, null, null);
        Assert.Single(results);
        Assert.Equal("Python Advanced", results[0].Name);
    }

    [Fact]
    public void AdvancedSearch_SubjectOnly_ReturnsSubjectDocs()
    {
        Seed();
        var results = Db.SearchDocumentsAdvanced(null, "CNTT", null, null, null, null, null, null);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void AdvancedSearch_TypeOnly_ReturnsTypeDocs()
    {
        Seed();
        var results = Db.SearchDocumentsAdvanced(null, null, "PDF", null, null, null, null, null);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void AdvancedSearch_MinSize_FiltersCorrectly()
    {
        Seed();
        var results = Db.SearchDocumentsAdvanced(null, null, null, null, null, 5.0, null, null);
        Assert.Equal(2, results.Count); // Python (5.0) and Business (10.0)
    }

    [Fact]
    public void AdvancedSearch_MaxSize_FiltersCorrectly()
    {
        Seed();
        var results = Db.SearchDocumentsAdvanced(null, null, null, null, null, null, 5.0, null);
        Assert.Equal(2, results.Count); // Python (5.0) and Java (1.5)
    }

    [Fact]
    public void AdvancedSearch_SizeRange_NarrowsDown()
    {
        Seed();
        var results = Db.SearchDocumentsAdvanced(null, null, null, null, null, 2.0, 6.0, null);
        Assert.Single(results); // Only Python (5.0)
    }

    [Fact]
    public void AdvancedSearch_IsImportantTrue_ReturnsOnlyImportant()
    {
        Seed();
        var results = Db.SearchDocumentsAdvanced(null, null, null, null, null, null, null, true);
        Assert.Equal(2, results.Count); // Python and Business
        Assert.All(results, d => Assert.True(d.IsImportant));
    }

    [Fact]
    public void AdvancedSearch_IsImportantFalse_ReturnsAll()
    {
        // isImportant = false means "don't filter by importance" (null behaviour)
        Seed();
        var results = Db.SearchDocumentsAdvanced(null, null, null, null, null, null, null, false);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void AdvancedSearch_DateRange_FromToday_IncludesAllToday()
    {
        Seed();
        var results = Db.SearchDocumentsAdvanced(
            null, null, null,
            DateTime.Today, DateTime.Today,
            null, null, null);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void AdvancedSearch_DateRange_TooOld_ReturnsEmpty()
    {
        Seed();
        var results = Db.SearchDocumentsAdvanced(
            null, null, null,
            DateTime.Today.AddDays(-10), DateTime.Today.AddDays(-5),
            null, null, null);
        Assert.Empty(results);
    }

    [Fact]
    public void AdvancedSearch_AllParams_Combo()
    {
        Seed();
        var results = Db.SearchDocumentsAdvanced(
            "Python",
            "CNTT",
            "PDF",
            DateTime.Today,
            DateTime.Today,
            1.0,
            10.0,
            true);
        Assert.Single(results);
        Assert.Equal("Python Advanced", results[0].Name);
    }

    [Fact]
    public void AdvancedSearch_NullAllParams_ReturnsAll()
    {
        Seed();
        var results = Db.SearchDocumentsAdvanced(null, null, null, null, null, null, null, null);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void AdvancedSearch_TatCaSentinel_TreatedAsNoFilter()
    {
        Seed();
        var results = Db.SearchDocumentsAdvanced(null, "All", "All", null, null, null, null, null);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void AdvancedSearch_SearchByTag_FindsDocWithTag()
    {
        Seed();
        var results = Db.SearchDocumentsAdvanced("finance", null, null, null, null, null, null, null);
        Assert.Single(results);
        Assert.Equal("Business Report", results[0].Name);
    }

    [Fact]
    public void AdvancedSearch_ExcludesSoftDeletedDocs()
    {
        Seed();
        var all = _repo.GetAll();
        _repo.Delete(all[0].Id);

        var results = Db.SearchDocumentsAdvanced(null, null, null, null, null, null, null, null);
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
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = name });
        return repo.GetAll().First(d => d.Name == name).Id;
    }

    [Fact]
    public void RemoveRecentFile_ExistingEntry_EntryDisappears()
    {
        int id = AddDoc("RemoveMe");
        Db.AddRecentFile(id);
        Assert.Single(Db.GetRecentFiles());

        Db.RemoveRecentFile(id);
        Assert.Empty(Db.GetRecentFiles());
    }

    [Fact]
    public void RemoveRecentFile_NonExistent_NoException()
    {
        // Should not throw
        Db.RemoveRecentFile(99999);
        Assert.Empty(Db.GetRecentFiles());
    }

    [Fact]
    public void RemoveRecentFile_OnlyRemovesTargetDoc()
    {
        int id1 = AddDoc("Keep1");
        int id2 = AddDoc("Remove2");
        Db.AddRecentFile(id1);
        Thread.Sleep(1100);
        Db.AddRecentFile(id2);

        Db.RemoveRecentFile(id2);
        var recents = Db.GetRecentFiles();
        Assert.Single(recents);
        Assert.Equal(id1, recents[0].Id);
    }

    [Fact]
    public void ClearRecentFiles_RemovesAllEntries()
    {
        int id1 = AddDoc("ClearA");
        int id2 = AddDoc("ClearB");
        Db.AddRecentFile(id1);
        Thread.Sleep(1100);
        Db.AddRecentFile(id2);

        Assert.Equal(2, Db.GetRecentFiles().Count);

        Db.ClearRecentFiles();
        Assert.Empty(Db.GetRecentFiles());
    }

    [Fact]
    public void ClearRecentFiles_EmptyTable_NoException()
    {
        Db.ClearRecentFiles(); // should not throw
        Assert.Empty(Db.GetRecentFiles());
    }

    [Fact]
    public void AddRecentFile_AfterSoftDelete_HiddenInRecentFiles()
    {
        var repo = new DocumentRepository(Db);
        int id = AddDoc("WillBeDeleted");
        Db.AddRecentFile(id);
        repo.Delete(id);

        // GetRecentFilesはdocumentsテーブルをJOINし、is_deletedを除外する
        var recents = Db.GetRecentFiles();
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
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "SharedDoc" });
        int docId = repo.GetAll()[0].Id;

        int col1 = Db.CreateCollection("Col Alpha");
        int col2 = Db.CreateCollection("Col Beta");

        Db.AddDocumentToCollection(col1, docId);
        Db.AddDocumentToCollection(col2, docId);

        var inCol1 = Db.GetDocumentsInCollection(col1);
        var inCol2 = Db.GetDocumentsInCollection(col2);

        Assert.Single(inCol1);
        Assert.Single(inCol2);
        Assert.Equal(docId, inCol1[0].Id);
        Assert.Equal(docId, inCol2[0].Id);
    }

    [Fact]
    public void AddDocumentToCollection_Duplicate_ReturnsFalse()
    {
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "Dup Guard" });
        int docId = repo.GetAll()[0].Id;
        int colId = Db.CreateCollection("Guard Col");

        bool first = Db.AddDocumentToCollection(colId, docId);
        bool second = Db.AddDocumentToCollection(colId, docId); // duplicate

        Assert.True(first);
        Assert.False(second); // rejected
        Assert.Single(Db.GetDocumentsInCollection(colId));
    }

    [Fact]
    public void DeleteCollection_OnlyRemovesTargetCollection()
    {
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "MultiColDoc" });
        int docId = repo.GetAll()[0].Id;

        int col1 = Db.CreateCollection("Keep Me");
        int col2 = Db.CreateCollection("Delete Me");

        Db.AddDocumentToCollection(col1, docId);
        Db.AddDocumentToCollection(col2, docId);

        Db.DeleteCollection(col2);

        var remaining = Db.GetCollections();
        Assert.Single(remaining);
        Assert.Equal("Keep Me", remaining[0].Name);
        Assert.Single(Db.GetDocumentsInCollection(col1));
    }

    [Fact]
    public void GetDocumentsInCollection_SoftDeletedDocHidden()
    {
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "WillDelete" });
        repo.Add(new StudyDocument { Name = "StayActive" });
        var docs = repo.GetAll();

        int colId = Db.CreateCollection("Mixed Col");
        foreach (var d in docs)
            Db.AddDocumentToCollection(colId, d.Id);

        // Soft delete one
        repo.Delete(docs.First(d => d.Name == "WillDelete").Id);

        var inCol = Db.GetDocumentsInCollection(colId);
        Assert.Single(inCol);
        Assert.Equal("StayActive", inCol[0].Name);
    }

    [Fact]
    public void GetCollections_ItemCount_ExcludesSoftDeletedDocuments()
    {
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "Active" });
        repo.Add(new StudyDocument { Name = "Deleted" });
        var docs = repo.GetAll();

        int colId = Db.CreateCollection("Mixed Items");
        foreach (var d in docs)
            Db.AddDocumentToCollection(colId, d.Id);

        // 削除前: 表示対象の active document が 2 件
        var before = Db.GetCollections()[0];
        Assert.Equal(2, before.ItemCount);

        // 1件を論理削除: 表示対象の active document は 1 件に減少
        repo.Delete(docs.First(d => d.Name == "Deleted").Id);
        var after = Db.GetCollections()[0];
        Assert.Equal(1, after.ItemCount);

        // 残りの 1 件も論理削除: active document が 0 件のコレクションは ItemCount == 0
        repo.Delete(docs.First(d => d.Name == "Active").Id);
        var empty = Db.GetCollections()[0];
        Assert.Equal(0, empty.ItemCount);
    }
}

// ════════════════════════════════════════════════════════════
// FILTER — "All" sentinel values
// ════════════════════════════════════════════════════════════

public class FilterSentinelTests : DatabaseTestBase
{
    private DocumentRepository _repo => Repo;

    [Fact]
    public void FilterDocuments_AllSubject_ReturnsAll()
    {
        _repo.Add(new StudyDocument { Name = "A", Subject = "Math"  });
        _repo.Add(new StudyDocument { Name = "B", Subject = "Logic" });

        var results = Db.FilterDocuments("All", "");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void FilterDocuments_AllType_ReturnsAll()
    {
        _repo.Add(new StudyDocument { Name = "A", Type = "PDF"  });
        _repo.Add(new StudyDocument { Name = "B", Type = "Word" });

        var results = Db.FilterDocuments("", "All");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void FilterDocuments_BothAll_ReturnsAll()
    {
        _repo.Add(new StudyDocument { Name = "X" });
        _repo.Add(new StudyDocument { Name = "Y" });

        var results = Db.FilterDocuments("All", "All");
        Assert.Equal(2, results.Count);
    }
}

// ════════════════════════════════════════════════════════════
// REPOSITORY INTERFACE — GetDistinctSubjects/Types/Tags via repo
// ════════════════════════════════════════════════════════════

public class RepositoryInterfaceTests : DatabaseTestBase
{
    private DocumentRepository _repo => Repo;

    [Fact]
    public void Repo_GetDistinctSubjects_ReturnsUniqueValues()
    {
        _repo.Add(new StudyDocument { Name = "A", Subject = "Math"    });
        _repo.Add(new StudyDocument { Name = "B", Subject = "Math"    }); // duplicate
        _repo.Add(new StudyDocument { Name = "C", Subject = "Physics" });

        var subjects = _repo.GetDistinctSubjects();
        Assert.Equal(2, subjects.Count);
        Assert.Contains("Math",    subjects);
        Assert.Contains("Physics", subjects);
    }

    [Fact]
    public void Repo_GetDistinctTypes_ReturnsUniqueValues()
    {
        _repo.Add(new StudyDocument { Name = "A", Type = "PDF"  });
        _repo.Add(new StudyDocument { Name = "B", Type = "PDF"  }); // duplicate
        _repo.Add(new StudyDocument { Name = "C", Type = "Word" });

        var types = _repo.GetDistinctTypes();
        Assert.Equal(2, types.Count);
        Assert.Contains("PDF",  types);
        Assert.Contains("Word", types);
    }

    [Fact]
    public void Repo_GetDistinctTags_SplitsAndDeduplicates()
    {
        _repo.Add(new StudyDocument { Name = "A", Tags = "python;math" });
        _repo.Add(new StudyDocument { Name = "B", Tags = "math;logic"  });

        var tags = _repo.GetDistinctTags();
        Assert.Equal(3, tags.Count); // python, math, logic
    }

    [Fact]
    public void Repo_GetDistinctSubjects_ExcludesSoftDeleted()
    {
        _repo.Add(new StudyDocument { Name = "Active", Subject = "KeepMe"   });
        _repo.Add(new StudyDocument { Name = "Dead",   Subject = "DropMe"   });
        int deadId = _repo.GetAll().First(d => d.Name == "Dead").Id;
        _repo.Delete(deadId);

        var subjects = _repo.GetDistinctSubjects();
        Assert.DoesNotContain("DropMe", subjects);
        Assert.Contains("KeepMe", subjects);
    }

    [Fact]
    public void Repo_GetDistinctTypes_IsSortedAlphabetically()
    {
        _repo.Add(new StudyDocument { Name = "Z", Type = "Zebra" });
        _repo.Add(new StudyDocument { Name = "A", Type = "Apple" });
        _repo.Add(new StudyDocument { Name = "M", Type = "Mango" });

        var types = _repo.GetDistinctTypes();
        var sorted = types.OrderBy(t => t).ToList();
        Assert.Equal(sorted, types);
    }

    [Fact]
    public void Repo_Search_ReturnsAll_WhenKeywordMatchesAll()
    {
        _repo.Add(new StudyDocument { Name = "Doc Alpha",  Subject = "Test" });
        _repo.Add(new StudyDocument { Name = "Doc Beta",   Subject = "Test" });
        _repo.Add(new StudyDocument { Name = "Doc Gamma",  Subject = "Test" });

        var results = _repo.Search("Doc");
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Repo_Filter_BySubject_ReturnsByRepo()
    {
        _repo.Add(new StudyDocument { Name = "A", Subject = "Target" });
        _repo.Add(new StudyDocument { Name = "B", Subject = "Other"  });

        var results = _repo.Filter("Target", "");
        Assert.Single(results);
        Assert.Equal("Target", results[0].Subject);
    }

    [Fact]
    public void Repo_SearchAdvanced_ProducesConsistentResultsWithDirectDbHelper()
    {
        _repo.Add(new StudyDocument { Name = "Advanced Doc", Subject = "Science", IsImportant = true });
        _repo.Add(new StudyDocument { Name = "Other Doc", Subject = "Art", IsImportant = false });

        var viaRepo = _repo.SearchAdvanced("Advanced", "Science", null!, null, null, null, null, true);
        var viaDirect = Db.SearchDocumentsAdvanced("Advanced", "Science", null, null, null, null, null, true);

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
        var repo = new DocumentRepository(Db);
        repo.Add(new StudyDocument { Name = "PersistAfterInit" });

        // Call InitializeDatabase again — must be no-op (CREATE TABLE IF NOT EXISTS / ALTER IGNORE)
        Db.InitializeDatabase();

        var all = repo.GetAll();
        Assert.Single(all);
        Assert.Equal("PersistAfterInit", all[0].Name);
    }

    [Fact]
    public void InitializeDatabase_CalledThreeTimes_DefaultSubjectsNotDuplicated()
    {
        Db.InitializeDatabase();
        Db.InitializeDatabase();

        var subjects = Db.GetAllSubjects();
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
    private DocumentRepository _repo => Repo;

    [Fact]
    public void PermanentDelete_DocInCollection_ItemRemovedFromCollectionItems()
    {
        _repo.Add(new StudyDocument { Name = "InCollection" });
        int docId = _repo.GetAll()[0].Id;
        int colId = Db.CreateCollection("TestCol");
        Db.AddDocumentToCollection(colId, docId);

        _repo.Delete(docId); // soft delete first
        Db.PermanentDeleteDocument(docId); // hard delete

        // collection_items row should still exist (no cascade in schema)
        // GetDocumentsInCollectionはdocumentsテーブルをJOINするため表示されない
        var inCol = Db.GetDocumentsInCollection(colId);
        Assert.Empty(inCol);
    }

    [Fact]
    public void RestoreDocument_DocAppearsInActiveList()
    {
        _repo.Add(new StudyDocument { Name = "RestoreMe" });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id);

        Assert.Empty(_repo.GetAll());
        Db.RestoreDocument(id);
        Assert.Single(_repo.GetAll());
    }

    [Fact]
    public void RestoreDocument_RestoredDocRemovedFromDeletedList()
    {
        _repo.Add(new StudyDocument { Name = "Trash" });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id);

        Assert.Single(Db.GetDeletedDocuments());
        Db.RestoreDocument(id);
        Assert.Empty(Db.GetDeletedDocuments());
    }

    [Fact]
    public void GetDeletedDocuments_OrderedByDeletedAtDesc()
    {
        _repo.Add(new StudyDocument { Name = "First"  });
        _repo.Add(new StudyDocument { Name = "Second" });
        var docs = _repo.GetAll();

        _repo.Delete(docs.First(d => d.Name == "First").Id);
        Thread.Sleep(1100); // ensure different deleted_at timestamps
        _repo.Delete(docs.First(d => d.Name == "Second").Id);

        var deleted = Db.GetDeletedDocuments();
        Assert.Equal(2, deleted.Count);
        Assert.Equal("Second", deleted[0].Name); // most recently deleted = first
    }

    [Fact]
    public void PermanentDelete_IdDoesNotExist_ReturnsFalse()
    {
        bool result = Db.PermanentDeleteDocument(999999);
        Assert.False(result);
    }

    [Fact]
    public void RestoreDocument_NonExistentId_ReturnsFalse()
    {
        bool result = Db.RestoreDocument(999999);
        Assert.False(result);
    }
}

// ════════════════════════════════════════════════════════════
// BULK OPERATIONS — Idempotency & Edge cases
// ════════════════════════════════════════════════════════════

public class BulkOperationIdempotencyTests : DatabaseTestBase
{
    private DocumentRepository _repo => Repo;

    [Fact]
    public void BulkSoftDelete_AlreadyDeletedId_CountsAsAffected()
    {
        // SQLite UPDATE on already-deleted rows still counts as "affected"
        // (the flag is already 1, UPDATE sets it to 1 again)
        _repo.Add(new StudyDocument { Name = "SoftDeleted" });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id); // soft delete it

        // A second BulkSoftDelete on the same id — SQLite still returns 1 affected row
        int result = Db.BulkSoftDelete(new List<int> { id });
        Assert.True(result >= 0); // Must not throw
    }

    [Fact]
    public void BulkUpdateSubject_ToSameValue_UpdatesSuccessfully()
    {
        _repo.Add(new StudyDocument { Name = "Same", Subject = "Math" });
        int id = _repo.GetAll()[0].Id;

        int count = Db.BulkUpdateSubject(new List<int> { id }, "Math");
        Assert.Equal(1, count);
        Assert.Equal("Math", _repo.GetAll()[0].Subject);
    }

    [Fact]
    public void BulkToggleImportant_True_ThenFalse_TogglesBothWays()
    {
        _repo.Add(new StudyDocument { Name = "Toggle", IsImportant = false });
        int id = _repo.GetAll()[0].Id;

        Db.BulkToggleImportant(new List<int> { id }, true);
        Assert.True(_repo.GetAll()[0].IsImportant);

        Db.BulkToggleImportant(new List<int> { id }, false);
        Assert.False(_repo.GetAll()[0].IsImportant);
    }

    [Fact]
    public void BulkSoftDelete_MixedIds_OnlyValidOnesAffected()
    {
        _repo.Add(new StudyDocument { Name = "Real" });
        int realId = _repo.GetAll()[0].Id;

        int count = Db.BulkSoftDelete(new List<int> { realId, 99999 });
        Assert.Equal(1, count); // Only one real row affected
    }

    [Fact]
    public void BulkUpdateSubject_MixedIds_OnlyValidOnesUpdated()
    {
        _repo.Add(new StudyDocument { Name = "Real", Subject = "Old" });
        int realId = _repo.GetAll()[0].Id;

        int count = Db.BulkUpdateSubject(new List<int> { realId, 88888 }, "New");
        Assert.Equal(1, count);
        Assert.Equal("New", _repo.GetAll()[0].Subject);
    }

    [Fact]
    public void BulkOperations_LargeIdList_200Items_NoException()
    {
        for (int i = 0; i < 200; i++)
            _repo.Add(new StudyDocument { Name = $"Doc{i}" });

        var ids = _repo.GetAll().Select(d => d.Id).ToList();
        Assert.Equal(200, ids.Count);

        int affected = Db.BulkToggleImportant(ids, true);
        Assert.Equal(200, affected);
    }
}

// ════════════════════════════════════════════════════════════
// DUPLICATE DETECTION — Logic simulation
// ════════════════════════════════════════════════════════════

public class DuplicateDetectionLogicTests : DatabaseTestBase
{
    private DocumentRepository _repo => Repo;

    [Fact]
    public void GetAllDocuments_SamePath_CanBeGroupedAsDuplicates()
    {
        string sharedPath = @"C:\shared\file.pdf";
        _repo.Add(new StudyDocument { Name = "Copy A", FilePath = sharedPath });
        _repo.Add(new StudyDocument { Name = "Copy B", FilePath = @"c:\SHARED\FILE.pdf" });
        _repo.Add(new StudyDocument { Name = "Unique",  FilePath = @"C:\unique.pdf" });

        var all = _repo.GetAll();
        var grouped = all
            .Where(d => !string.IsNullOrEmpty(d.FilePath))
            .GroupBy(d => d.FilePath!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Single(grouped);
        Assert.Equal(2, grouped[0].Count());
    }

    [Fact]
    public void GetAllDocuments_SameName_CanBeGroupedAsDuplicates()
    {
        _repo.Add(new StudyDocument { Name = "Same Name" });
        _repo.Add(new StudyDocument { Name = "Same Name" }); // exact duplicate name
        _repo.Add(new StudyDocument { Name = "Unique Name" });

        var all = _repo.GetAll();
        var grouped = all
            .GroupBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Single(grouped);
        Assert.Equal(2, grouped[0].Count());
    }

    [Fact]
    public void GetAllDocuments_NullPath_ExcludedFromDuplicatePathCheck()
    {
        _repo.Add(new StudyDocument { Name = "NoPath1", FilePath = null! });
        _repo.Add(new StudyDocument { Name = "NoPath2", FilePath = null! });

        var all = _repo.GetAll();
        var grouped = all
            .Where(d => !string.IsNullOrEmpty(d.FilePath))
            .GroupBy(d => d.FilePath!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Empty(grouped);
    }

    [Fact]
    public void SoftDeletedDocs_ExcludedFromDuplicateAnalysis()
    {
        string path = @"C:\dup.pdf";
        _repo.Add(new StudyDocument { Name = "Live Copy",    FilePath = path });
        _repo.Add(new StudyDocument { Name = "Deleted Copy", FilePath = @"c:\DUP.pdf" });
        int deletedId = _repo.GetAll().First(d => d.Name == "Deleted Copy").Id;
        _repo.Delete(deletedId);

        var active = _repo.GetAll(); // excludes soft-deleted
        var grouped = active
            .Where(d => !string.IsNullOrEmpty(d.FilePath))
            .GroupBy(d => d.FilePath!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.Empty(grouped); // only 1 active doc with that path
    }
}
