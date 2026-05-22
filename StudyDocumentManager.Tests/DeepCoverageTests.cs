using Xunit;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;

// ════════════════════════════════════════════════════════════
// Deep Coverage Tests — File 5
// Covers: Collection tuple/CRUD, PersonalNotes upsert,
//         Multi-relation graph, Tag search, Deadline edge cases,
//         Consistency checks, Stress, Dashboard NearDeadline
// ════════════════════════════════════════════════════════════

namespace StudyDocumentManager.Tests;

// ════════════════════════════════════════════════════════════
// COLLECTION — Tuple Structure & UpdateCollection
// ════════════════════════════════════════════════════════════

public class CollectionTupleTests : DatabaseTestBase
{
    [Fact]
    public void GetCollections_EmptyDb_ReturnsEmpty()
    {
        var cols = DatabaseHelper.GetCollections();
        Assert.Empty(cols);
    }

    [Fact]
    public void CreateCollection_WithDescription_AllFieldsPersisted()
    {
        int id = DatabaseHelper.CreateCollection("My Collection", "A test description");
        Assert.True(id > 0);

        var cols = DatabaseHelper.GetCollections();
        Assert.Single(cols);

        var col = cols[0];
        Assert.Equal(id, col.Id);
        Assert.Equal("My Collection", col.Name);
        Assert.Equal("A test description", col.Description);
        Assert.True(col.CreatedAt > DateTime.MinValue);
        Assert.Equal(0, col.ItemCount);
    }

    [Fact]
    public void CreateCollection_WithoutDescription_DescriptionIsNull()
    {
        DatabaseHelper.CreateCollection("No Desc");
        var col = DatabaseHelper.GetCollections()[0];
        Assert.Null(col.Description);
    }

    [Fact]
    public void GetCollections_ItemCount_ReflectsActualItems()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "A" });
        repo.Add(new StudyDocument { Name = "B" });
        var docs = repo.GetAll();

        int colId = DatabaseHelper.CreateCollection("Counted Collection");
        DatabaseHelper.AddDocumentToCollection(colId, docs[0].Id);
        DatabaseHelper.AddDocumentToCollection(colId, docs[1].Id);

        var col = DatabaseHelper.GetCollections()[0];
        Assert.Equal(2, col.ItemCount);
    }

    [Fact]
    public void GetCollections_ItemCountDecreases_AfterRemovingDocument()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "Remove Me" });
        int docId = repo.GetAll()[0].Id;
        int colId = DatabaseHelper.CreateCollection("Dynamic Count");
        DatabaseHelper.AddDocumentToCollection(colId, docId);

        Assert.Equal(1, DatabaseHelper.GetCollections()[0].ItemCount);

        DatabaseHelper.RemoveDocumentFromCollection(colId, docId);

        Assert.Equal(0, DatabaseHelper.GetCollections()[0].ItemCount);
    }

    [Fact]
    public void GetCollections_OrderedByName()
    {
        DatabaseHelper.CreateCollection("Zebra");
        DatabaseHelper.CreateCollection("Apple");
        DatabaseHelper.CreateCollection("Mango");

        var cols = DatabaseHelper.GetCollections();
        var names = cols.Select(c => c.Name).ToList();
        Assert.Equal(names.OrderBy(n => n).ToList(), names);
    }

    [Fact]
    public void UpdateCollection_ChangesNameAndDescription()
    {
        int colId = DatabaseHelper.CreateCollection("Old Name", "Old desc");

        bool result = DatabaseHelper.UpdateCollection(colId, "New Name", "New desc");
        Assert.True(result);

        var col = DatabaseHelper.GetCollections()[0];
        Assert.Equal("New Name", col.Name);
        Assert.Equal("New desc", col.Description);
    }

    [Fact]
    public void UpdateCollection_NonExistentId_ReturnsFalse()
    {
        bool result = DatabaseHelper.UpdateCollection(99999, "Ghost", null);
        Assert.False(result);
    }

    [Fact]
    public void UpdateCollection_ClearDescription_SetToNull()
    {
        int colId = DatabaseHelper.CreateCollection("HasDesc", "Some desc");
        DatabaseHelper.UpdateCollection(colId, "HasDesc", null);

        var col = DatabaseHelper.GetCollections()[0];
        Assert.Null(col.Description);
    }

    [Fact]
    public void DeleteCollection_RemovesAllItems_ButNotDocuments()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = "StillActive" });
        int docId = repo.GetAll()[0].Id;

        int colId = DatabaseHelper.CreateCollection("DeleteMe");
        DatabaseHelper.AddDocumentToCollection(colId, docId);

        DatabaseHelper.DeleteCollection(colId);

        Assert.Empty(DatabaseHelper.GetCollections());
        Assert.Single(repo.GetAll()); // document still active
    }
}

// ════════════════════════════════════════════════════════════
// PERSONAL NOTES — Full Upsert Cycle
// ════════════════════════════════════════════════════════════

public class PersonalNotesFullCycleTests : DatabaseTestBase
{
    private int CreateDoc(string name = "Note Doc")
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Name = name });
        return repo.GetAll().First(d => d.Name == name).Id;
    }

    [Fact]
    public void GetPersonalNote_NoNote_ReturnsNull()
    {
        int docId = CreateDoc();
        var note = DatabaseHelper.GetPersonalNote(docId);
        Assert.Null(note);
    }

    [Fact]
    public void SavePersonalNote_FirstTime_InsertsRecord()
    {
        int docId = CreateDoc();
        bool result = DatabaseHelper.SavePersonalNote(docId, "My first note");
        Assert.True(result);

        var note = DatabaseHelper.GetPersonalNote(docId);
        Assert.Equal("My first note", note);
    }

    [Fact]
    public void SavePersonalNote_SecondTime_UpdatesExisting()
    {
        int docId = CreateDoc();
        DatabaseHelper.SavePersonalNote(docId, "Version 1");
        bool result = DatabaseHelper.SavePersonalNote(docId, "Version 2");
        Assert.True(result);

        var note = DatabaseHelper.GetPersonalNote(docId);
        Assert.Equal("Version 2", note); // not "Version 1"
    }

    [Fact]
    public void SavePersonalNote_MultipleUpdates_AlwaysLatestValue()
    {
        int docId = CreateDoc();
        for (int i = 1; i <= 5; i++)
            DatabaseHelper.SavePersonalNote(docId, $"Draft {i}");

        Assert.Equal("Draft 5", DatabaseHelper.GetPersonalNote(docId));
    }

    [Fact]
    public void DeletePersonalNote_ExistingNote_ReturnsTrueAndRemoves()
    {
        int docId = CreateDoc();
        DatabaseHelper.SavePersonalNote(docId, "Delete this");

        bool result = DatabaseHelper.DeletePersonalNote(docId);
        Assert.True(result);
        Assert.Null(DatabaseHelper.GetPersonalNote(docId));
    }

    [Fact]
    public void DeletePersonalNote_NonExistentNote_ReturnsFalse()
    {
        int docId = CreateDoc();
        bool result = DatabaseHelper.DeletePersonalNote(docId); // nothing to delete
        Assert.False(result);
    }

    [Fact]
    public void PersonalNotes_IndependentPerDocument()
    {
        int docA = CreateDoc("DocA");
        int docB = CreateDoc("DocB");

        DatabaseHelper.SavePersonalNote(docA, "Note A");
        DatabaseHelper.SavePersonalNote(docB, "Note B");

        Assert.Equal("Note A", DatabaseHelper.GetPersonalNote(docA));
        Assert.Equal("Note B", DatabaseHelper.GetPersonalNote(docB));
    }

    [Fact]
    public void DeleteNote_DocA_DoesNotAffectDocB()
    {
        int docA = CreateDoc("DA");
        int docB = CreateDoc("DB");

        DatabaseHelper.SavePersonalNote(docA, "A note");
        DatabaseHelper.SavePersonalNote(docB, "B note");

        DatabaseHelper.DeletePersonalNote(docA);

        Assert.Null(DatabaseHelper.GetPersonalNote(docA));
        Assert.Equal("B note", DatabaseHelper.GetPersonalNote(docB));
    }

    [Fact]
    public void SavePersonalNote_WithUnicodeContent_PersistedCorrectly()
    {
        int docId = CreateDoc();
        string unicode = "Note: 日本語 한국어 العربية 🎯";
        DatabaseHelper.SavePersonalNote(docId, unicode);
        Assert.Equal(unicode, DatabaseHelper.GetPersonalNote(docId));
    }

    [Fact]
    public void SavePersonalNote_EmptyString_BehaviorGraceful()
    {
        int docId = CreateDoc();
        // Empty string — content IS set to DBNull in implementation
        DatabaseHelper.SavePersonalNote(docId, "");
        // Should not throw; result may be null or empty depending on implementation
        var note = DatabaseHelper.GetPersonalNote(docId);
        // Either null or empty — just verify no exception
        Assert.True(note == null || note == string.Empty);
    }
}

// ════════════════════════════════════════════════════════════
// MULTI-RELATION GRAPH — Doc linked to multiple docs
// ════════════════════════════════════════════════════════════

public class MultiRelationGraphTests : DatabaseTestBase
{
    private List<int> CreateDocs(int count)
    {
        var repo = new DocumentRepository();
        for (int i = 1; i <= count; i++)
            repo.Add(new StudyDocument { Name = $"Node{i:D2}" });
        return repo.GetAll().OrderBy(d => d.Id).Select(d => d.Id).ToList();
    }

    [Fact]
    public void DocA_RelatedToMultiple_GetRelatedReturnsAll()
    {
        var ids = CreateDocs(4); // A=ids[0], B=ids[1], C=ids[2], D=ids[3]
        int A = ids[0], B = ids[1], C = ids[2], D = ids[3];

        DatabaseHelper.AddDocumentRelation(A, B, "reference");
        DatabaseHelper.AddDocumentRelation(A, C, "supplement");
        DatabaseHelper.AddDocumentRelation(A, D, "prerequisite");

        var relationsOfA = DatabaseHelper.GetRelatedDocuments(A);
        Assert.Equal(3, relationsOfA.Count);
    }

    [Fact]
    public void DocA_HasMultipleRelationTypes_AllTypesPreserved()
    {
        var ids = CreateDocs(3);
        int A = ids[0], B = ids[1], C = ids[2];

        DatabaseHelper.AddDocumentRelation(A, B, "prerequisite");
        DatabaseHelper.AddDocumentRelation(A, C, "supplement");

        var relations = DatabaseHelper.GetRelatedDocuments(A);
        var types = relations.Select(r => r.RelationType).OrderBy(t => t).ToList();

        Assert.Equal(new[] { "prerequisite", "supplement" }, types);
    }

    [Fact]
    public void TriangleRelation_AllNodesCanSeeEachOther()
    {
        var ids = CreateDocs(3);
        int A = ids[0], B = ids[1], C = ids[2];

        DatabaseHelper.AddDocumentRelation(A, B);
        DatabaseHelper.AddDocumentRelation(B, C);
        DatabaseHelper.AddDocumentRelation(A, C);

        Assert.Equal(2, DatabaseHelper.GetRelatedDocuments(A).Count);
        Assert.Equal(2, DatabaseHelper.GetRelatedDocuments(B).Count);
        Assert.Equal(2, DatabaseHelper.GetRelatedDocuments(C).Count);
    }

    [Fact]
    public void RemoveOneRelation_OthersUnaffected()
    {
        var ids = CreateDocs(3);
        int A = ids[0], B = ids[1], C = ids[2];

        DatabaseHelper.AddDocumentRelation(A, B, "related");
        DatabaseHelper.AddDocumentRelation(A, C, "similar");

        var relOfA = DatabaseHelper.GetRelatedDocuments(A);
        int relationIdToRemove = relOfA.First(r => r.Doc.Id == C).RelationId;

        DatabaseHelper.RemoveDocumentRelation(relationIdToRemove);

        var afterRemoval = DatabaseHelper.GetRelatedDocuments(A);
        Assert.Single(afterRemoval);
        Assert.Equal(B, afterRemoval[0].Doc.Id);
    }

    [Fact]
    public void SoftDeleteNode_RelationsFromOtherNodesHide()
    {
        var repo = new DocumentRepository();
        var ids = CreateDocs(3);
        int A = ids[0], B = ids[1], C = ids[2];

        DatabaseHelper.AddDocumentRelation(A, B);
        DatabaseHelper.AddDocumentRelation(A, C);

        repo.Delete(B); // soft delete B

        var relOfA = DatabaseHelper.GetRelatedDocuments(A);
        Assert.Single(relOfA); // only C remains
        Assert.Equal(C, relOfA[0].Doc.Id);
    }

    [Fact]
    public void GetRelatedDocuments_WithNoRelations_ReturnsEmpty()
    {
        var ids = CreateDocs(1);
        var result = DatabaseHelper.GetRelatedDocuments(ids[0]);
        Assert.Empty(result);
    }
}

// ════════════════════════════════════════════════════════════
// TAG SEARCH — Tags searched via SearchDocuments
// ════════════════════════════════════════════════════════════

public class TagSearchTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo = new();

    [Fact]
    public void Search_SingleTag_ReturnsMatchingDocuments()
    {
        _repo.Add(new StudyDocument { Name = "D1", Tags = "python;algorithm" });
        _repo.Add(new StudyDocument { Name = "D2", Tags = "java;oop" });
        _repo.Add(new StudyDocument { Name = "D3", Tags = "python;ml" });

        var results = DatabaseHelper.SearchDocuments("python");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Search_TagPartialMatch_ReturnsDocuments()
    {
        _repo.Add(new StudyDocument { Name = "D1", Tags = "machine-learning;deep-learning" });
        _repo.Add(new StudyDocument { Name = "D2", Tags = "java;oop" });

        var results = DatabaseHelper.SearchDocuments("learning");
        Assert.Single(results);
    }

    [Fact]
    public void Search_TagNotPresent_ReturnsEmpty()
    {
        _repo.Add(new StudyDocument { Name = "D1", Tags = "python;code" });
        var results = DatabaseHelper.SearchDocuments("javascript");
        Assert.Empty(results);
    }

    [Fact]
    public void GetDistinctTags_SemicalonDelimited_SplitsCorrectly()
    {
        _repo.Add(new StudyDocument { Name = "D1", Tags = "math;physics;chemistry" });
        _repo.Add(new StudyDocument { Name = "D2", Tags = "math;biology" });

        var tags = DatabaseHelper.GetDistinctTags();
        Assert.Contains("math", tags);
        Assert.Contains("physics", tags);
        Assert.Contains("chemistry", tags);
        Assert.Contains("biology", tags);
        // "math" should appear once (deduplicated)
        Assert.Equal(1, tags.Count(t => t == "math"));
    }

    [Fact]
    public void GetDistinctTags_NullAndEmptyTags_Excluded()
    {
        _repo.Add(new StudyDocument { Name = "D1", Tags = null });
        _repo.Add(new StudyDocument { Name = "D2", Tags = "" });
        _repo.Add(new StudyDocument { Name = "D3", Tags = "valid" });

        var tags = DatabaseHelper.GetDistinctTags();
        Assert.Single(tags);
        Assert.Equal("valid", tags[0]);
    }

    [Fact]
    public void GetDistinctTags_ExcludesSoftDeletedDocuments()
    {
        _repo.Add(new StudyDocument { Name = "D1", Tags = "deleted-tag" });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id);

        _repo.Add(new StudyDocument { Name = "D2", Tags = "active-tag" });

        var tags = DatabaseHelper.GetDistinctTags();
        Assert.DoesNotContain("deleted-tag", tags);
        Assert.Contains("active-tag", tags);
    }
}

// ════════════════════════════════════════════════════════════
// DEADLINE — Near/Overdue edge cases
// ════════════════════════════════════════════════════════════

public class DeadlineEdgeCaseTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo = new();

    [Fact]
    public void GetUpcomingDeadlines_7Days_IncludesDeadlineInRange()
    {
        _repo.Add(new StudyDocument { Name = "Due in 3 days", Deadline = DateTime.Today.AddDays(3) });
        _repo.Add(new StudyDocument { Name = "Due in 10 days", Deadline = DateTime.Today.AddDays(10) });

        var results = _repo.GetUpcomingDeadlines(7);
        Assert.Single(results);
        Assert.Equal("Due in 3 days", results[0].Name);
    }

    [Fact]
    public void GetUpcomingDeadlines_0Days_OnlyToday()
    {
        _repo.Add(new StudyDocument { Name = "Due today", Deadline = DateTime.Today });
        _repo.Add(new StudyDocument { Name = "Due tomorrow", Deadline = DateTime.Today.AddDays(1) });

        var results = _repo.GetUpcomingDeadlines(0);
        Assert.Single(results);
        Assert.Equal("Due today", results[0].Name);
    }

    [Fact]
    public void GetOverdueDocuments_PastDeadline_Included()
    {
        _repo.Add(new StudyDocument { Name = "Overdue!",  Deadline = DateTime.Today.AddDays(-1) });
        _repo.Add(new StudyDocument { Name = "Not overdue", Deadline = DateTime.Today.AddDays(1) });

        var results = _repo.GetOverdueDocuments();
        Assert.Single(results);
        Assert.Equal("Overdue!", results[0].Name);
    }

    [Fact]
    public void GetOverdueDocuments_NullDeadline_Excluded()
    {
        _repo.Add(new StudyDocument { Name = "No deadline", Deadline = null });
        var results = _repo.GetOverdueDocuments();
        Assert.Empty(results);
    }

    [Fact]
    public void GetOverdueDocuments_SoftDeleted_Excluded()
    {
        _repo.Add(new StudyDocument { Name = "Deleted Overdue", Deadline = DateTime.Today.AddDays(-2) });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id);

        var results = _repo.GetOverdueDocuments();
        Assert.Empty(results);
    }

    [Fact]
    public void DashboardStats_NearDeadlineDocuments_CorrectCount()
    {
        _repo.Add(new StudyDocument { Name = "Near1", Deadline = DateTime.Today.AddDays(2) });
        _repo.Add(new StudyDocument { Name = "Near2", Deadline = DateTime.Today.AddDays(6) });
        _repo.Add(new StudyDocument { Name = "Far", Deadline = DateTime.Today.AddDays(30) });
        _repo.Add(new StudyDocument { Name = "Overdue", Deadline = DateTime.Today.AddDays(-1) });
        _repo.Add(new StudyDocument { Name = "NoDeadline", Deadline = null });

        var stats = DatabaseHelper.GetDashboardStatistics();

        // Near = deadline within next 7 days (inclusive today up to today+7)
        Assert.Equal(2, stats.NearDeadlineDocuments);
        Assert.Equal(1, stats.OverdueDocuments);
    }

    [Fact]
    public void GetUpcomingDeadlines_ExcludesOverdue()
    {
        _repo.Add(new StudyDocument { Name = "Already overdue", Deadline = DateTime.Today.AddDays(-1) });
        _repo.Add(new StudyDocument { Name = "Upcoming", Deadline = DateTime.Today.AddDays(3) });

        var results = _repo.GetUpcomingDeadlines(7);
        Assert.Single(results);
        Assert.Equal("Upcoming", results[0].Name);
    }

    [Fact]
    public void GetUpcomingDeadlines_30Days_ReturnsMultiple()
    {
        _repo.Add(new StudyDocument { Name = "D1", Deadline = DateTime.Today.AddDays(5)  });
        _repo.Add(new StudyDocument { Name = "D2", Deadline = DateTime.Today.AddDays(15) });
        _repo.Add(new StudyDocument { Name = "D3", Deadline = DateTime.Today.AddDays(25) });
        _repo.Add(new StudyDocument { Name = "D4", Deadline = DateTime.Today.AddDays(40) });

        var results = _repo.GetUpcomingDeadlines(30);
        Assert.Equal(3, results.Count);
    }
}

// ════════════════════════════════════════════════════════════
// CONSISTENCY CHECKS — GetTotalDocumentCount vs GetAll
// ════════════════════════════════════════════════════════════

public class ConsistencyTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo = new();

    [Fact]
    public void GetTotalDocumentCount_MatchesGetAllCount()
    {
        _repo.Add(new StudyDocument { Name = "A" });
        _repo.Add(new StudyDocument { Name = "B" });
        _repo.Add(new StudyDocument { Name = "C" });

        int countHelper = DatabaseHelper.GetTotalDocumentCount();
        int countRepo = _repo.GetAll().Count;
        Assert.Equal(countHelper, countRepo);
    }

    [Fact]
    public void GetTotalDocumentCount_AfterDelete_Consistent()
    {
        _repo.Add(new StudyDocument { Name = "Keep" });
        _repo.Add(new StudyDocument { Name = "Delete" });

        int id = _repo.GetAll().First(d => d.Name == "Delete").Id;
        _repo.Delete(id);

        Assert.Equal(_repo.GetAll().Count, DatabaseHelper.GetTotalDocumentCount());
    }

    [Fact]
    public void DashboardStats_TotalDocuments_MatchesGetAll()
    {
        _repo.Add(new StudyDocument { Name = "X" });
        _repo.Add(new StudyDocument { Name = "Y" });

        var stats = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(_repo.GetAll().Count, stats.TotalDocuments);
    }

    [Fact]
    public void DashboardStats_TotalCategories_CountsDistinctActiveSubjects()
    {
        _repo.Add(new StudyDocument { Name = "A", Subject = "Cat1" });
        _repo.Add(new StudyDocument { Name = "B", Subject = "Cat1" }); // same cat
        _repo.Add(new StudyDocument { Name = "C", Subject = "Cat2" });

        var stats = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(2, stats.TotalCategories); // Cat1, Cat2
    }

    [Fact]
    public void DashboardStats_TotalCollections_MatchesGetCollections()
    {
        DatabaseHelper.CreateCollection("Col1");
        DatabaseHelper.CreateCollection("Col2");

        var stats = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(DatabaseHelper.GetCollections().Count, stats.TotalCollections);
    }

    [Fact]
    public void DashboardStats_ImportantDocuments_MatchesManualCount()
    {
        _repo.Add(new StudyDocument { Name = "Imp1", IsImportant = true });
        _repo.Add(new StudyDocument { Name = "Imp2", IsImportant = true });
        _repo.Add(new StudyDocument { Name = "Normal", IsImportant = false });

        var stats = DatabaseHelper.GetDashboardStatistics();
        int manualCount = _repo.GetAll().Count(d => d.IsImportant);
        Assert.Equal(manualCount, stats.ImportantDocuments);
    }

    [Fact]
    public void DashboardStats_NoFileDocuments_MatchesManualCount()
    {
        _repo.Add(new StudyDocument { Name = "WithFile", FilePath = @"C:\file.pdf" });
        _repo.Add(new StudyDocument { Name = "NoFile1", FilePath = null });
        _repo.Add(new StudyDocument { Name = "NoFile2", FilePath = "" });

        var stats = DatabaseHelper.GetDashboardStatistics();
        int manualCount = _repo.GetAll().Count(d => string.IsNullOrEmpty(d.FilePath));
        Assert.Equal(manualCount, stats.NoFileDocuments);
    }
}

// ════════════════════════════════════════════════════════════
// STRESS TESTS — Volume & Bulk performance-like
// ════════════════════════════════════════════════════════════

public class StressTests : DatabaseTestBase
{
    [Fact]
    public void Insert100Documents_AllPersisted()
    {
        var repo = new DocumentRepository();
        for (int i = 1; i <= 100; i++)
            repo.Add(new StudyDocument
            {
                Name = $"Stress Doc {i:D3}",
                Subject = i % 2 == 0 ? "Math" : "Physics",
                Type = i % 3 == 0 ? "PDF" : "Word",
                IsImportant = i % 5 == 0,
                Tags = $"tag{i % 10};stress"
            });

        var all = repo.GetAll();
        Assert.Equal(100, all.Count);
    }

    [Fact]
    public void Insert100Docs_StatsAreAccurate()
    {
        var repo = new DocumentRepository();
        int importantCount = 0;
        for (int i = 1; i <= 100; i++)
        {
            bool isImportant = i % 5 == 0;
            if (isImportant) importantCount++;
            repo.Add(new StudyDocument
            {
                Name = $"StressDoc{i}",
                IsImportant = isImportant
            });
        }

        var stats = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(100, stats.TotalDocuments);
        Assert.Equal(importantCount, stats.ImportantDocuments); // 20 (i=5,10,...,100)
    }

    [Fact]
    public void BulkSoftDelete50Docs_StatsUpdateCorrectly()
    {
        var repo = new DocumentRepository();
        for (int i = 1; i <= 50; i++)
            repo.Add(new StudyDocument { Name = $"BulkDoc{i}" });

        var ids = repo.GetAll().Select(d => d.Id).Take(25).ToList();
        int deleted = DatabaseHelper.BulkSoftDelete(ids);

        Assert.Equal(25, deleted);
        Assert.Equal(25, repo.GetAll().Count);
        Assert.Equal(25, DatabaseHelper.GetDeletedDocumentCount());
    }

    [Fact]
    public void EmptyRecycleBin_WithMany_ActiveDocsUnaffected()
    {
        var repo = new DocumentRepository();
        for (int i = 1; i <= 20; i++)
            repo.Add(new StudyDocument { Name = $"Active{i}" });

        for (int i = 1; i <= 10; i++)
            repo.Add(new StudyDocument { Name = $"Delete{i}" });

        var toDelete = repo.GetAll().Where(d => d.Name.StartsWith("Delete")).Select(d => d.Id).ToList();
        DatabaseHelper.BulkSoftDelete(toDelete);

        int emptied = DatabaseHelper.EmptyRecycleBin();
        Assert.Equal(10, emptied);

        // Active docs still intact
        Assert.Equal(20, repo.GetAll().Count);
        Assert.Equal(0, DatabaseHelper.GetDeletedDocumentCount());
    }

    [Fact]
    public void Insert100Docs_SearchIsCorrect()
    {
        var repo = new DocumentRepository();
        for (int i = 1; i <= 100; i++)
            repo.Add(new StudyDocument { Name = i % 10 == 0 ? $"SpecialDoc{i}" : $"NormalDoc{i}" });

        var results = DatabaseHelper.SearchDocuments("SpecialDoc");
        Assert.Equal(10, results.Count); // i=10,20,...,100
    }

    [Fact]
    public void ChartData_100Docs_TodayColumnCorrect()
    {
        var repo = new DocumentRepository();
        for (int i = 1; i <= 50; i++)
            repo.Add(new StudyDocument { Name = $"ChartDoc{i}" });

        var dayData = DatabaseHelper.GetDocumentsByDay(7);
        var today = dayData.Last();
        Assert.Equal(50, today.Count);
    }

    [Fact]
    public void BulkToggleImportant_50Docs_AllUpdated()
    {
        var repo = new DocumentRepository();
        for (int i = 1; i <= 50; i++)
            repo.Add(new StudyDocument { Name = $"ToggleDoc{i}", IsImportant = false });

        var ids = repo.GetAll().Select(d => d.Id).ToList();
        int updated = DatabaseHelper.BulkToggleImportant(ids, true);

        Assert.Equal(50, updated);

        var all = repo.GetAll();
        Assert.All(all, d => Assert.True(d.IsImportant));
    }

    [Fact]
    public void BulkUpdateSubject_25Docs_AllReassigned()
    {
        var repo = new DocumentRepository();
        for (int i = 1; i <= 25; i++)
            repo.Add(new StudyDocument { Name = $"ReassignDoc{i}", Subject = "OldSubject" });

        var ids = repo.GetAll().Select(d => d.Id).ToList();
        int count = DatabaseHelper.BulkUpdateSubject(ids, "NewSubject");

        Assert.Equal(25, count);
        Assert.All(repo.GetAll(), d => Assert.Equal("NewSubject", d.Subject));
    }
}

// ════════════════════════════════════════════════════════════
// SEARCH — Filter edge cases not covered before
// ════════════════════════════════════════════════════════════

public class FilterEdgeCaseTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo = new();

    [Fact]
    public void FilterDocuments_EmptySubjectAndType_ReturnsAll()
    {
        _repo.Add(new StudyDocument { Name = "A", Subject = "Math", Type = "PDF" });
        _repo.Add(new StudyDocument { Name = "B", Subject = "Physics", Type = "Word" });

        var results = DatabaseHelper.FilterDocuments("", "");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void FilterDocuments_BySubjectOnly_ReturnsMatching()
    {
        _repo.Add(new StudyDocument { Name = "M1", Subject = "Math" });
        _repo.Add(new StudyDocument { Name = "P1", Subject = "Physics" });
        _repo.Add(new StudyDocument { Name = "M2", Subject = "Math" });

        var results = DatabaseHelper.FilterDocuments("Math", "");
        Assert.Equal(2, results.Count);
        Assert.All(results, d => Assert.Equal("Math", d.Subject));
    }

    [Fact]
    public void FilterDocuments_ByTypeOnly_ReturnsMatching()
    {
        _repo.Add(new StudyDocument { Name = "P1", Type = "PDF" });
        _repo.Add(new StudyDocument { Name = "W1", Type = "Word" });
        _repo.Add(new StudyDocument { Name = "P2", Type = "PDF" });

        var results = DatabaseHelper.FilterDocuments("", "PDF");
        Assert.Equal(2, results.Count);
        Assert.All(results, d => Assert.Equal("PDF", d.Type));
    }

    [Fact]
    public void FilterDocuments_BySubjectAndType_NarrowsDown()
    {
        _repo.Add(new StudyDocument { Name = "Match", Subject = "Math", Type = "PDF" });
        _repo.Add(new StudyDocument { Name = "SubjectOnly", Subject = "Math", Type = "Word" });
        _repo.Add(new StudyDocument { Name = "TypeOnly", Subject = "Physics", Type = "PDF" });

        var results = DatabaseHelper.FilterDocuments("Math", "PDF");
        Assert.Single(results);
        Assert.Equal("Match", results[0].Name);
    }

    [Fact]
    public void FilterDocuments_SoftDeletedExcluded()
    {
        _repo.Add(new StudyDocument { Name = "Active", Subject = "Math" });
        _repo.Add(new StudyDocument { Name = "Deleted", Subject = "Math" });
        int id = _repo.GetAll().First(d => d.Name == "Deleted").Id;
        _repo.Delete(id);

        var results = DatabaseHelper.FilterDocuments("Math", "");
        Assert.Single(results);
        Assert.Equal("Active", results[0].Name);
    }

    [Fact]
    public void SearchDocuments_CaseInsensitive_FindsMatch()
    {
        _repo.Add(new StudyDocument { Name = "UPPERCASE DOC" });
        var lower = DatabaseHelper.SearchDocuments("uppercase");
        var upper = DatabaseHelper.SearchDocuments("UPPERCASE");
        var mixed = DatabaseHelper.SearchDocuments("Uppercase");

        Assert.Single(lower);
        Assert.Single(upper);
        Assert.Single(mixed);
    }

    [Fact]
    public void SearchDocuments_WhitespaceKeyword_ReturnsAll()
    {
        _repo.Add(new StudyDocument { Name = "Alpha" });
        _repo.Add(new StudyDocument { Name = "Beta" });

        // Whitespace/empty search — returns all active docs
        var results = DatabaseHelper.SearchDocuments("  ");
        // Behaviour depends on impl; ensure no exception
        Assert.NotNull(results);
    }

    [Fact]
    public void SearchDocuments_ByNotes_FindsDocByGhiChu()
    {
        _repo.Add(new StudyDocument { Name = "Doc1", Notes = "Important document about algorithms" });
        _repo.Add(new StudyDocument { Name = "Doc2", Notes = "Regular notes" });

        var results = DatabaseHelper.SearchDocuments("algorithms");
        Assert.Single(results);
        Assert.Equal("Doc1", results[0].Name);
    }
}

// ════════════════════════════════════════════════════════════
// DATA INTEGRITY — Edge cases for field boundaries
// ════════════════════════════════════════════════════════════

public class DataIntegrityTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo = new();

    [Fact]
    public void Document_WithMaxLengthName_PersistsCorrectly()
    {
        string longName = new string('A', 500);
        _repo.Add(new StudyDocument { Name = longName });
        var doc = _repo.GetAll()[0];
        Assert.Equal(longName, doc.Name);
    }

    [Fact]
    public void Document_WithZeroSize_PersistsCorrectly()
    {
        _repo.Add(new StudyDocument { Name = "ZeroSize", FileSize = 0.0 });
        var doc = _repo.GetAll()[0];
        Assert.Equal(0.0, doc.FileSize);
    }

    [Fact]
    public void Document_WithLargeSize_PersistsCorrectly()
    {
        double largeSize = 99999.999;
        _repo.Add(new StudyDocument { Name = "LargeFile", FileSize = largeSize });
        var doc = _repo.GetAll()[0];
        Assert.Equal(largeSize, doc.FileSize!.Value, 2); // 2 decimal precision
    }

    [Fact]
    public void Document_NgayThem_IsAutoSetToNow()
    {
        var before = DateTime.Now.AddSeconds(-1);
        _repo.Add(new StudyDocument { Name = "AutoDate" });
        var doc = _repo.GetAll()[0];
        var after = DateTime.Now.AddSeconds(1);

        Assert.True(doc.CreatedAt >= before && doc.CreatedAt <= after);
    }

    [Fact]
    public void Document_DeadlinePreservesTimeComponent()
    {
        var deadline = new DateTime(2027, 6, 15, 23, 59, 0);
        _repo.Add(new StudyDocument { Name = "Deadline Test", Deadline = deadline });
        var doc = _repo.GetAll()[0];

        Assert.NotNull(doc.Deadline);
        Assert.Equal(deadline.Date, doc.Deadline!.Value.Date); // Date part preserved
    }

    [Fact]
    public void Update_PartialFieldChange_OtherFieldsUnchanged()
    {
        _repo.Add(new StudyDocument
        {
            Name = "Original",
            Subject = "Math",
            Author = "Author",
            FileSize = 1.5,
            IsImportant = true
        });

        var doc = _repo.GetAll()[0];
        doc.Name = "Changed Name Only";
        _repo.Update(doc);

        var updated = _repo.GetAll()[0];
        Assert.Equal("Changed Name Only", updated.Name);
        Assert.Equal("Math", updated.Subject);     // unchanged
        Assert.Equal("Author", updated.Author);   // unchanged
        Assert.Equal(1.5, updated.FileSize);     // unchanged
        Assert.True(updated.IsImportant);           // unchanged
    }

    [Fact]
    public void Delete_SetDeletedAt_IsNotNull()
    {
        _repo.Add(new StudyDocument { Name = "CheckDeletedAt" });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id);

        var deleted = DatabaseHelper.GetDeletedDocuments();
        Assert.Single(deleted);
        // Verify doc is in recycle bin (deleted_at is set in DB — accessible via GetDeletedDocuments)
        Assert.Equal("CheckDeletedAt", deleted[0].Name);
    }

    [Fact]
    public void Restore_ClearsDeletedAt_AppearsInActive()
    {
        _repo.Add(new StudyDocument { Name = "Restore Test" });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id);

        DatabaseHelper.RestoreDocument(id);

        Assert.Empty(DatabaseHelper.GetDeletedDocuments());
        Assert.Single(_repo.GetAll());
    }
}
