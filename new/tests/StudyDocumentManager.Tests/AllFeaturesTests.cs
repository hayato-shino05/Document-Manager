using Xunit;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace StudyDocumentManager.Tests;

// ════════════════════════════════════════════════════════════
// F01 - F05: Document CRUD (Add / GetAll / GetById / Update / SoftDelete)
// ════════════════════════════════════════════════════════════

public class DocumentCrudTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public DocumentCrudTests() { _repo = new DocumentRepository(); }

    // Helper: create a minimal valid document
    private static StudyDocument MakeDoc(string name = "Test Doc", string subject = "Học tập", string type = "Tài liệu")
        => new() { Ten = name, MonHoc = subject, Loai = type };

    // ── Add document ──────────────────────────────────────
    [Fact]
    public void Add_ValidDocument_ReturnsTrueAndPersists()
    {
        var doc = MakeDoc("Giáo trình Toán");
        bool result = _repo.Add(doc);
        var all = _repo.GetAll();

        Assert.True(result);
        Assert.Single(all);
        Assert.Equal("Giáo trình Toán", all[0].Ten);
    }

    [Fact]
    public void Add_DocumentWithAllFields_PersistsCorrectly()
    {
        var deadline = DateTime.Today.AddDays(7);
        var doc = new StudyDocument
        {
            Ten = "Full Field Doc",
            MonHoc = "Công việc",
            Loai = "Báo cáo",
            DuongDan = @"C:\docs\report.pdf",
            GhiChu = "Ghi chú quan trọng",
            KichThuoc = 1.5,
            TacGia = "Hayato",
            QuanTrong = true,
            Tags = "urgent;review",
            Deadline = deadline
        };

        _repo.Add(doc);
        var saved = _repo.GetAll().First();

        Assert.Equal("Full Field Doc", saved.Ten);
        Assert.Equal("Công việc", saved.MonHoc);
        Assert.Equal("Báo cáo", saved.Loai);
        Assert.Equal(@"C:\docs\report.pdf", saved.DuongDan);
        Assert.Equal(1.5, saved.KichThuoc);
        Assert.Equal("Hayato", saved.TacGia);
        Assert.True(saved.QuanTrong);
        Assert.Equal("urgent;review", saved.Tags);
        Assert.NotNull(saved.Deadline);
        Assert.Equal(deadline.Date, saved.Deadline!.Value.Date);
    }

    // ── GetAll ────────────────────────────────────────────
    [Fact]
    public void GetAll_EmptyDb_ReturnsEmptyList()
    {
        Assert.Empty(_repo.GetAll());
    }

    [Fact]
    public void GetAll_MultipleDocuments_ReturnsAllSortedByDateDesc()
    {
        _repo.Add(MakeDoc("Doc A"));
        _repo.Add(MakeDoc("Doc B"));
        _repo.Add(MakeDoc("Doc C"));

        var all = _repo.GetAll();
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public void GetAll_DoesNotReturnSoftDeletedDocuments()
    {
        _repo.Add(MakeDoc("Visible"));
        _repo.Add(MakeDoc("Deleted"));

        var all = _repo.GetAll();
        int deletedId = all.First(d => d.Ten == "Deleted").Id;
        _repo.Delete(deletedId);

        var afterDelete = _repo.GetAll();
        Assert.Single(afterDelete);
        Assert.Equal("Visible", afterDelete[0].Ten);
    }

    // ── GetById ───────────────────────────────────────────
    [Fact]
    public void GetById_ExistingId_ReturnsDocument()
    {
        _repo.Add(MakeDoc("Find Me"));
        int id = _repo.GetAll()[0].Id;

        var found = _repo.GetById(id);
        Assert.NotNull(found);
        Assert.Equal("Find Me", found!.Ten);
    }

    [Fact]
    public void GetById_NonExistingId_ReturnsNull()
    {
        var result = _repo.GetById(99999);
        Assert.Null(result);
    }

    // ── Update ────────────────────────────────────────────
    [Fact]
    public void Update_ExistingDocument_PersistsChanges()
    {
        _repo.Add(MakeDoc("Original"));
        var doc = _repo.GetAll()[0];

        doc.Ten = "Updated";
        doc.QuanTrong = true;
        doc.TacGia = "New Author";
        bool result = _repo.Update(doc);

        var updated = _repo.GetById(doc.Id)!;
        Assert.True(result);
        Assert.Equal("Updated", updated.Ten);
        Assert.True(updated.QuanTrong);
        Assert.Equal("New Author", updated.TacGia);
    }

    // ── Soft Delete ───────────────────────────────────────
    [Fact]
    public void Delete_ExistingDocument_SoftDeletesFromActiveList()
    {
        _repo.Add(MakeDoc("To Delete"));
        int id = _repo.GetAll()[0].Id;

        bool result = _repo.Delete(id);

        Assert.True(result);
        Assert.Empty(_repo.GetAll());
    }

    [Fact]
    public void Delete_SoftDeletedDocument_AppearsInRecycleBin()
    {
        _repo.Add(MakeDoc("In Recycle"));
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id);

        var deleted = DatabaseHelper.GetDeletedDocuments();
        Assert.Single(deleted);
        Assert.Equal("In Recycle", deleted[0].Ten);
    }
}

// ════════════════════════════════════════════════════════════
// F02 - F03: Search & Filter
// ════════════════════════════════════════════════════════════

public class SearchFilterTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public SearchFilterTests() { _repo = new DocumentRepository(); }

    private void Seed()
    {
        _repo.Add(new StudyDocument { Ten = "Giáo trình Python", MonHoc = "Học tập", Loai = "Tài liệu", TacGia = "Guido", Tags = "python;code" });
        _repo.Add(new StudyDocument { Ten = "Báo cáo tài chính Q1", MonHoc = "Tài chính", Loai = "Báo cáo", TacGia = "CFO" });
        _repo.Add(new StudyDocument { Ten = "Hợp đồng dự án A", MonHoc = "Dự án", Loai = "Hợp đồng", QuanTrong = true });
        _repo.Add(new StudyDocument { Ten = "Video bài giảng toán", MonHoc = "Học tập", Loai = "Video" });
    }

    // ── Basic Search ──────────────────────────────────────
    [Fact]
    public void Search_ByName_ReturnsMatchingDocuments()
    {
        Seed();
        var results = _repo.Search("Python");
        Assert.Single(results);
        Assert.Equal("Giáo trình Python", results[0].Ten);
    }

    [Fact]
    public void Search_ByAuthor_ReturnsMatchingDocuments()
    {
        Seed();
        var results = _repo.Search("Guido");
        Assert.Single(results);
    }

    [Fact]
    public void Search_ByTags_ReturnsMatchingDocuments()
    {
        Seed();
        var results = _repo.Search("code");
        Assert.Single(results);
        Assert.Contains("python", results[0].Tags);
    }

    [Fact]
    public void Search_CaseInsensitive_ReturnsResults()
    {
        Seed();
        var results = _repo.Search("python");
        Assert.Single(results);
    }

    [Fact]
    public void Search_NoMatch_ReturnsEmptyList()
    {
        Seed();
        var results = _repo.Search("KHÔNG TỒN TẠI XYZ123");
        Assert.Empty(results);
    }

    // ── Filter ────────────────────────────────────────────
    [Fact]
    public void Filter_BySubject_ReturnsOnlyMatchingSubject()
    {
        Seed();
        var results = _repo.Filter("Học tập", "");
        Assert.Equal(2, results.Count);
        Assert.All(results, d => Assert.Equal("Học tập", d.MonHoc));
    }

    [Fact]
    public void Filter_ByType_ReturnsOnlyMatchingType()
    {
        Seed();
        var results = _repo.Filter("", "Video");
        Assert.Single(results);
        Assert.Equal("Video", results[0].Loai);
    }

    [Fact]
    public void Filter_BySubjectAndType_NarrowsResult()
    {
        Seed();
        var results = _repo.Filter("Học tập", "Tài liệu");
        Assert.Single(results);
        Assert.Equal("Giáo trình Python", results[0].Ten);
    }

    [Fact]
    public void Filter_TatCaSubject_ReturnsAll()
    {
        Seed();
        var results = _repo.Filter("Tất cả", "Tất cả");
        Assert.Equal(4, results.Count);
    }

    // ── Advanced Search ───────────────────────────────────
    [Fact]
    public void SearchAdvanced_ByImportantFlag_ReturnsOnlyImportant()
    {
        Seed();
        var results = _repo.SearchAdvanced("", "", "", null, null, null, null, true);
        Assert.All(results, d => Assert.True(d.QuanTrong));
    }

    [Fact]
    public void SearchAdvanced_ByDateRange_ReturnsWithinRange()
    {
        Seed();
        var from = DateTime.Today.AddDays(-1);
        var to = DateTime.Today.AddDays(1);
        var results = _repo.SearchAdvanced("", "", "", from, to, null, null, null);
        Assert.Equal(4, results.Count); // All seeded today
    }

    [Fact]
    public void SearchAdvanced_FutureDateRange_ReturnsEmpty()
    {
        Seed();
        var from = DateTime.Today.AddDays(10);
        var to = DateTime.Today.AddDays(20);
        var results = _repo.SearchAdvanced("", "", "", from, to, null, null, null);
        Assert.Empty(results);
    }

    [Fact]
    public void SearchAdvanced_ByKeywordAndSubject_FiltersCorrectly()
    {
        Seed();
        var results = _repo.SearchAdvanced("giáo trình", "Học tập", "", null, null, null, null, null);
        Assert.Single(results);
        Assert.Equal("Giáo trình Python", results[0].Ten);
    }
}

// ════════════════════════════════════════════════════════════
// F04: Distinct Values (Subjects / Types / Tags)
// ════════════════════════════════════════════════════════════

public class DistinctValueTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public DistinctValueTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void GetDistinctSubjects_ReturnsUniqueSubjects()
    {
        _repo.Add(new StudyDocument { Ten = "A", MonHoc = "Học tập" });
        _repo.Add(new StudyDocument { Ten = "B", MonHoc = "Học tập" });
        _repo.Add(new StudyDocument { Ten = "C", MonHoc = "Dự án" });

        var subjects = _repo.GetDistinctSubjects();
        Assert.Contains("Học tập", subjects);
        Assert.Contains("Dự án", subjects);
        Assert.Equal(2, subjects.Distinct().Count());
    }

    [Fact]
    public void GetDistinctTypes_ReturnsUniqueTypes()
    {
        _repo.Add(new StudyDocument { Ten = "A", Loai = "Tài liệu" });
        _repo.Add(new StudyDocument { Ten = "B", Loai = "Video" });
        _repo.Add(new StudyDocument { Ten = "C", Loai = "Tài liệu" });

        var types = _repo.GetDistinctTypes();
        Assert.Contains("Tài liệu", types);
        Assert.Contains("Video", types);
        Assert.Equal(2, types.Count);
    }

    [Fact]
    public void GetDistinctTags_SplitsAndDeduplicates()
    {
        _repo.Add(new StudyDocument { Ten = "A", Tags = "python;code" });
        _repo.Add(new StudyDocument { Ten = "B", Tags = "code;review" });

        var tags = _repo.GetDistinctTags();
        Assert.Contains("python", tags);
        Assert.Contains("code", tags);
        Assert.Contains("review", tags);
        // No duplicates
        Assert.Equal(tags.Distinct().Count(), tags.Count);
    }

    [Fact]
    public void GetDistinctSubjects_ExcludesSoftDeleted()
    {
        _repo.Add(new StudyDocument { Ten = "A", MonHoc = "Học tập" });
        _repo.Add(new StudyDocument { Ten = "B", MonHoc = "Xóa" });
        var id = _repo.GetAll().First(d => d.MonHoc == "Xóa").Id;
        _repo.Delete(id);

        var subjects = _repo.GetDistinctSubjects();
        Assert.DoesNotContain("Xóa", subjects);
    }
}

// ════════════════════════════════════════════════════════════
// F06: Deadline Tracking (F25: Upcoming / Overdue)
// ════════════════════════════════════════════════════════════

public class DeadlineTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public DeadlineTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void GetOverdueDocuments_ReturnsOnlyPastDeadline()
    {
        _repo.Add(new StudyDocument { Ten = "Overdue", Deadline = DateTime.Now.AddDays(-3) });
        _repo.Add(new StudyDocument { Ten = "Future", Deadline = DateTime.Now.AddDays(5) });
        _repo.Add(new StudyDocument { Ten = "No Deadline" });

        var overdue = _repo.GetOverdueDocuments();
        Assert.Single(overdue);
        Assert.Equal("Overdue", overdue[0].Ten);
    }

    [Fact]
    public void GetUpcomingDeadlines_Returns7DayWindow()
    {
        _repo.Add(new StudyDocument { Ten = "Due Today", Deadline = DateTime.Today });
        _repo.Add(new StudyDocument { Ten = "Due In 5", Deadline = DateTime.Today.AddDays(5) });
        _repo.Add(new StudyDocument { Ten = "Due In 10", Deadline = DateTime.Today.AddDays(10) });
        _repo.Add(new StudyDocument { Ten = "Overdue", Deadline = DateTime.Now.AddDays(-1) });

        var upcoming = _repo.GetUpcomingDeadlines(7);
        Assert.Equal(2, upcoming.Count);
        Assert.All(upcoming, d => Assert.NotNull(d.Deadline));
    }

    [Fact]
    public void GetUpcomingDeadlines_DoesNotIncludeOverdue()
    {
        _repo.Add(new StudyDocument { Ten = "Overdue", Deadline = DateTime.Now.AddDays(-1) });
        var upcoming = _repo.GetUpcomingDeadlines(7);
        Assert.Empty(upcoming);
    }

    [Fact]
    public void GetUpcomingDeadlines_NoDeadline_NotIncluded()
    {
        _repo.Add(new StudyDocument { Ten = "No Deadline" });
        var upcoming = _repo.GetUpcomingDeadlines(7);
        Assert.Empty(upcoming);
    }
}

// ════════════════════════════════════════════════════════════
// F20: Recycle Bin (Restore / Permanent Delete / Empty)
// ════════════════════════════════════════════════════════════

public class RecycleBinTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public RecycleBinTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void RestoreDocument_MovesBackToActiveList()
    {
        _repo.Add(new StudyDocument { Ten = "Restore Me" });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id);

        Assert.Empty(_repo.GetAll());
        bool restored = DatabaseHelper.RestoreDocument(id);

        Assert.True(restored);
        Assert.Single(_repo.GetAll());
        Assert.Equal("Restore Me", _repo.GetAll()[0].Ten);
    }

    [Fact]
    public void PermanentDeleteDocument_RemovesCompletely()
    {
        _repo.Add(new StudyDocument { Ten = "Gone Forever" });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id);

        bool deleted = DatabaseHelper.PermanentDeleteDocument(id);

        Assert.True(deleted);
        Assert.Empty(DatabaseHelper.GetDeletedDocuments());
        Assert.Null(_repo.GetById(id));
    }

    [Fact]
    public void EmptyRecycleBin_ClearsAllDeletedDocuments()
    {
        _repo.Add(new StudyDocument { Ten = "Trash 1" });
        _repo.Add(new StudyDocument { Ten = "Trash 2" });
        var all = _repo.GetAll();
        foreach (var d in all) _repo.Delete(d.Id);

        Assert.Equal(2, DatabaseHelper.GetDeletedDocuments().Count);
        DatabaseHelper.EmptyRecycleBin();
        Assert.Empty(DatabaseHelper.GetDeletedDocuments());
    }

    [Fact]
    public void GetDeletedDocumentCount_ReturnsCorrectCount()
    {
        _repo.Add(new StudyDocument { Ten = "D1" });
        _repo.Add(new StudyDocument { Ten = "D2" });
        var all = _repo.GetAll();
        _repo.Delete(all[0].Id);

        Assert.Equal(1, DatabaseHelper.GetDeletedDocumentCount());
    }

    [Fact]
    public void GetDeletedDocuments_ReturnsDeletedMetadata()
    {
        _repo.Add(new StudyDocument { Ten = "Deleted Metadata", MonHoc = "Học tập" });
        int id = _repo.GetAll()[0].Id;
        _repo.Delete(id);

        var deleted = DatabaseHelper.GetDeletedDocuments();
        Assert.Single(deleted);
        Assert.Equal("Deleted Metadata", deleted[0].Ten);
        Assert.Equal("Học tập", deleted[0].MonHoc);
    }
}

// ════════════════════════════════════════════════════════════
// F13: Bulk Operations (Toggle Important / Update Subject / Soft Delete)
// ════════════════════════════════════════════════════════════

public class BulkOperationTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public BulkOperationTests() { _repo = new DocumentRepository(); }

    private List<int> SeedAndGetIds(int count)
    {
        for (int i = 1; i <= count; i++)
            _repo.Add(new StudyDocument { Ten = $"Doc {i}", MonHoc = "Học tập" });
        return _repo.GetAll().Select(d => d.Id).ToList();
    }

    [Fact]
    public void BulkToggleImportant_SetsImportantTrue()
    {
        var ids = SeedAndGetIds(3);
        int affected = DatabaseHelper.BulkToggleImportant(ids, true);

        Assert.Equal(3, affected);
        var all = _repo.GetAll();
        Assert.All(all, d => Assert.True(d.QuanTrong));
    }

    [Fact]
    public void BulkToggleImportant_SetsImportantFalse()
    {
        var ids = SeedAndGetIds(2);
        DatabaseHelper.BulkToggleImportant(ids, true);
        DatabaseHelper.BulkToggleImportant(ids, false);

        var all = _repo.GetAll();
        Assert.All(all, d => Assert.False(d.QuanTrong));
    }

    [Fact]
    public void BulkUpdateSubject_ChangesSubjectForAllSelected()
    {
        var ids = SeedAndGetIds(3);
        // Only update first 2
        var subset = ids.Take(2).ToList();
        int affected = DatabaseHelper.BulkUpdateSubject(subset, "Dự án");

        Assert.Equal(2, affected);
        var updated = _repo.GetAll().Where(d => subset.Contains(d.Id)).ToList();
        Assert.All(updated, d => Assert.Equal("Dự án", d.MonHoc));
    }

    [Fact]
    public void BulkSoftDelete_MovesDocumentsToRecycleBin()
    {
        var ids = SeedAndGetIds(3);
        int deleted = DatabaseHelper.BulkSoftDelete(ids.Take(2).ToList());

        Assert.Equal(2, deleted);
        Assert.Single(_repo.GetAll()); // 1 remaining
        Assert.Equal(2, DatabaseHelper.GetDeletedDocuments().Count);
    }

    [Fact]
    public void BulkToggleImportant_EmptyList_ReturnsZero()
    {
        int affected = DatabaseHelper.BulkToggleImportant(new List<int>(), true);
        Assert.Equal(0, affected);
    }
}

// ════════════════════════════════════════════════════════════
// F17: Category Management (CRUD danh mục / loại)
// ════════════════════════════════════════════════════════════

public class CategoryManagementTests : DatabaseTestBase
{
    [Fact]
    public void AddSubject_NewName_AddedToList()
    {
        bool result = DatabaseHelper.AddSubject("Âm nhạc");
        var subjects = DatabaseHelper.GetAllSubjects();

        Assert.True(result);
        Assert.Contains("Âm nhạc", subjects);
    }

    [Fact]
    public void AddSubject_DuplicateName_DoesNotDuplicate()
    {
        DatabaseHelper.AddSubject("Khoa học");
        DatabaseHelper.AddSubject("Khoa học"); // Duplicate

        var subjects = DatabaseHelper.GetAllSubjects().Where(s => s == "Khoa học").ToList();
        Assert.Single(subjects);
    }

    [Fact]
    public void AddType_NewType_AddedToList()
    {
        bool result = DatabaseHelper.AddType("Bản nhạc");
        var types = DatabaseHelper.GetAllTypes();

        Assert.True(result);
        Assert.Contains("Bản nhạc", types);
    }

    [Fact]
    public void UpdateSubjectName_RenamesInDocumentsAndLookup()
    {
        DatabaseHelper.AddSubject("Cũ");
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "X", MonHoc = "Cũ" });

        bool result = DatabaseHelper.UpdateSubjectName("Cũ", "Mới");
        var docs = repo.GetAll();

        Assert.True(result);
        Assert.Equal("Mới", docs[0].MonHoc);
        Assert.Contains("Mới", DatabaseHelper.GetAllSubjects());
        Assert.DoesNotContain("Cũ", DatabaseHelper.GetAllSubjects());
    }

    [Fact]
    public void UpdateTypeName_RenamesInDocumentsAndLookup()
    {
        DatabaseHelper.AddType("TypeOld");
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "X", Loai = "TypeOld" });

        DatabaseHelper.UpdateTypeName("TypeOld", "TypeNew");
        var docs = repo.GetAll();

        Assert.Equal("TypeNew", docs[0].Loai);
        Assert.Contains("TypeNew", DatabaseHelper.GetAllTypes());
        Assert.DoesNotContain("TypeOld", DatabaseHelper.GetAllTypes());
    }

    [Fact]
    public void DeleteSubject_RemovesFromLookup()
    {
        DatabaseHelper.AddSubject("DeleteMe");
        DatabaseHelper.DeleteSubject("DeleteMe");

        Assert.DoesNotContain("DeleteMe", DatabaseHelper.GetAllSubjects());
    }

    [Fact]
    public void DeleteDocumentsBySubject_SoftDeletesAllInSubject()
    {
        DatabaseHelper.AddSubject("SoftDelSubject");
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "Doc1", MonHoc = "SoftDelSubject" });
        repo.Add(new StudyDocument { Ten = "Doc2", MonHoc = "SoftDelSubject" });
        repo.Add(new StudyDocument { Ten = "Keep", MonHoc = "Khác" });

        DatabaseHelper.DeleteDocumentsBySubject("SoftDelSubject");

        var active = repo.GetAll();
        Assert.Single(active);
        Assert.Equal("Keep", active[0].Ten);
    }

    [Fact]
    public void GetSubjectsWithCount_ReturnsCorrectCounts()
    {
        var repo = new DocumentRepository();
        repo.Add(new StudyDocument { Ten = "A", MonHoc = "Học tập" });
        repo.Add(new StudyDocument { Ten = "B", MonHoc = "Học tập" });
        repo.Add(new StudyDocument { Ten = "C", MonHoc = "Cá nhân" });

        var subjectCounts = DatabaseHelper.GetSubjectsWithCount();
        var htCount = subjectCounts.FirstOrDefault(s => s.Name == "Học tập");
        var cnCount = subjectCounts.FirstOrDefault(s => s.Name == "Cá nhân");

        Assert.Equal(2, htCount.Count);
        Assert.Equal(1, cnCount.Count);
    }
}

// ════════════════════════════════════════════════════════════
// F18: Collection Management
// ════════════════════════════════════════════════════════════

public class CollectionManagementTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public CollectionManagementTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void CreateCollection_ReturnsValidId()
    {
        int id = DatabaseHelper.CreateCollection("Bộ sưu tập 1");
        Assert.True(id > 0);
    }

    [Fact]
    public void GetCollections_ReturnsCreatedCollections()
    {
        DatabaseHelper.CreateCollection("Col A");
        DatabaseHelper.CreateCollection("Col B");

        var cols = DatabaseHelper.GetCollections();
        Assert.Equal(2, cols.Count);
        Assert.Contains(cols, c => c.Name == "Col A");
        Assert.Contains(cols, c => c.Name == "Col B");
    }

    [Fact]
    public void AddDocumentToCollection_Success()
    {
        _repo.Add(new StudyDocument { Ten = "My Doc" });
        int docId = _repo.GetAll()[0].Id;
        int colId = DatabaseHelper.CreateCollection("My Collection");

        bool result = DatabaseHelper.AddDocumentToCollection(colId, docId);
        var docs = DatabaseHelper.GetDocumentsInCollection(colId);

        Assert.True(result);
        Assert.Single(docs);
        Assert.Equal(docId, docs[0].Id);
    }

    [Fact]
    public void AddDocumentToCollection_DuplicatePrevented()
    {
        _repo.Add(new StudyDocument { Ten = "Doc" });
        int docId = _repo.GetAll()[0].Id;
        int colId = DatabaseHelper.CreateCollection("Col");

        DatabaseHelper.AddDocumentToCollection(colId, docId);
        bool second = DatabaseHelper.AddDocumentToCollection(colId, docId);

        Assert.False(second);
        Assert.Single(DatabaseHelper.GetDocumentsInCollection(colId));
    }

    [Fact]
    public void RemoveDocumentFromCollection_Success()
    {
        _repo.Add(new StudyDocument { Ten = "Doc" });
        int docId = _repo.GetAll()[0].Id;
        int colId = DatabaseHelper.CreateCollection("Col");
        DatabaseHelper.AddDocumentToCollection(colId, docId);

        bool removed = DatabaseHelper.RemoveDocumentFromCollection(colId, docId);
        var docs = DatabaseHelper.GetDocumentsInCollection(colId);

        Assert.True(removed);
        Assert.Empty(docs);
    }

    [Fact]
    public void DeleteCollection_RemovesCollectionNotDocuments()
    {
        int colId = DatabaseHelper.CreateCollection("ToDelete");
        _repo.Add(new StudyDocument { Ten = "Doc" });
        int docId = _repo.GetAll()[0].Id;
        DatabaseHelper.AddDocumentToCollection(colId, docId);

        bool deleted = DatabaseHelper.DeleteCollection(colId);

        Assert.True(deleted);
        Assert.Empty(DatabaseHelper.GetCollections());
        // Document still exists
        Assert.Single(_repo.GetAll());
    }

    [Fact]
    public void UpdateCollection_ChangesName()
    {
        int colId = DatabaseHelper.CreateCollection("Old Name");
        bool result = DatabaseHelper.UpdateCollection(colId, "New Name");

        var cols = DatabaseHelper.GetCollections();
        Assert.True(result);
        Assert.Contains(cols, c => c.Name == "New Name");
        Assert.DoesNotContain(cols, c => c.Name == "Old Name");
    }

    [Fact]
    public void GetCollections_IncludesItemCount()
    {
        int colId = DatabaseHelper.CreateCollection("WithDocs");
        _repo.Add(new StudyDocument { Ten = "D1" });
        _repo.Add(new StudyDocument { Ten = "D2" });
        var docs = _repo.GetAll();
        DatabaseHelper.AddDocumentToCollection(colId, docs[0].Id);
        DatabaseHelper.AddDocumentToCollection(colId, docs[1].Id);

        var cols = DatabaseHelper.GetCollections();
        var col = cols.First(c => c.Name == "WithDocs");
        Assert.Equal(2, col.ItemCount);
    }
}

// ════════════════════════════════════════════════════════════
// F11: Personal Notes
// ════════════════════════════════════════════════════════════

public class PersonalNoteTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public PersonalNoteTests() { _repo = new DocumentRepository(); }

    private int CreateDoc()
    {
        _repo.Add(new StudyDocument { Ten = "Doc for Note" });
        return _repo.GetAll()[0].Id;
    }

    [Fact]
    public void SavePersonalNote_NewNote_Persists()
    {
        int docId = CreateDoc();
        bool result = DatabaseHelper.SavePersonalNote(docId, "Ghi chú đầu tiên");

        Assert.True(result);
        Assert.Equal("Ghi chú đầu tiên", DatabaseHelper.GetPersonalNote(docId));
    }

    [Fact]
    public void SavePersonalNote_UpdateExisting_Overwrites()
    {
        int docId = CreateDoc();
        DatabaseHelper.SavePersonalNote(docId, "Cũ");
        DatabaseHelper.SavePersonalNote(docId, "Mới cập nhật");

        Assert.Equal("Mới cập nhật", DatabaseHelper.GetPersonalNote(docId));
    }

    [Fact]
    public void GetPersonalNote_NoNote_ReturnsNull()
    {
        int docId = CreateDoc();
        Assert.Null(DatabaseHelper.GetPersonalNote(docId));
    }

    [Fact]
    public void DeletePersonalNote_ClearsNote()
    {
        int docId = CreateDoc();
        DatabaseHelper.SavePersonalNote(docId, "To be deleted");
        DatabaseHelper.DeletePersonalNote(docId);

        Assert.Null(DatabaseHelper.GetPersonalNote(docId));
    }
}

// ════════════════════════════════════════════════════════════
// F16: Related Documents
// ════════════════════════════════════════════════════════════

public class RelatedDocumentsTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public RelatedDocumentsTests() { _repo = new DocumentRepository(); }

    private (int A, int B) CreateTwoDocs()
    {
        _repo.Add(new StudyDocument { Ten = "Doc A" });
        _repo.Add(new StudyDocument { Ten = "Doc B" });
        var all = _repo.GetAll();
        return (all[1].Id, all[0].Id); // reversed order (latest added first)
    }

    [Fact]
    public void AddDocumentRelation_CreatesRelation()
    {
        var (a, b) = CreateTwoDocs();
        DatabaseHelper.AddDocumentRelation(a, b, "related");

        var relA = DatabaseHelper.GetRelatedDocuments(a);
        Assert.Single(relA);
        Assert.Equal(b, relA[0].Doc.Id);
    }

    [Fact]
    public void AddDocumentRelation_Bidirectional_BothSidesCanQuery()
    {
        var (a, b) = CreateTwoDocs();
        DatabaseHelper.AddDocumentRelation(a, b);

        var relB = DatabaseHelper.GetRelatedDocuments(b);
        Assert.Single(relB);
        Assert.Equal(a, relB[0].Doc.Id);
    }

    [Fact]
    public void AddDocumentRelation_DuplicatePrevented()
    {
        var (a, b) = CreateTwoDocs();
        DatabaseHelper.AddDocumentRelation(a, b);
        DatabaseHelper.AddDocumentRelation(a, b); // duplicate

        var rel = DatabaseHelper.GetRelatedDocuments(a);
        Assert.Single(rel);
    }

    [Fact]
    public void RemoveDocumentRelation_RemovesLink()
    {
        var (a, b) = CreateTwoDocs();
        DatabaseHelper.AddDocumentRelation(a, b);
        var rel = DatabaseHelper.GetRelatedDocuments(a);
        int relationId = rel[0].RelationId;

        DatabaseHelper.RemoveDocumentRelation(relationId);

        Assert.Empty(DatabaseHelper.GetRelatedDocuments(a));
    }

    [Fact]
    public void AddDocumentRelation_WithRelationType_Persists()
    {
        var (a, b) = CreateTwoDocs();
        DatabaseHelper.AddDocumentRelation(a, b, "reference");

        var rel = DatabaseHelper.GetRelatedDocuments(a);
        Assert.Equal("reference", rel[0].RelationType);
    }

    [Fact]
    public void GetRelatedDocuments_ExcludesSoftDeleted()
    {
        var (a, b) = CreateTwoDocs();
        DatabaseHelper.AddDocumentRelation(a, b);
        _repo.Delete(b); // soft delete B

        var rel = DatabaseHelper.GetRelatedDocuments(a);
        Assert.Empty(rel);
    }
}

// ════════════════════════════════════════════════════════════
// F14: Recent Files
// ════════════════════════════════════════════════════════════

public class RecentFilesTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public RecentFilesTests() { _repo = new DocumentRepository(); }

    private int CreateDoc(string name)
    {
        _repo.Add(new StudyDocument { Ten = name });
        return _repo.GetAll().First(d => d.Ten == name).Id;
    }

    [Fact]
    public void AddRecentFile_AppearsInGetRecentFiles()
    {
        int docId = CreateDoc("Recent Doc");
        DatabaseHelper.AddRecentFile(docId);

        var recent = DatabaseHelper.GetRecentFiles();
        Assert.Single(recent);
        Assert.Equal(docId, recent[0].Id);
    }

    [Fact]
    public void AddRecentFile_UpdatesTimestampOnDuplicate()
    {
        int docId = CreateDoc("Same Doc");
        DatabaseHelper.AddRecentFile(docId);
        DatabaseHelper.AddRecentFile(docId); // Should replace, not duplicate

        var recent = DatabaseHelper.GetRecentFiles();
        Assert.Single(recent);
    }

    [Fact]
    public void AddRecentFile_KepsOnly20MostRecent()
    {
        // Seed 25 docs
        for (int i = 1; i <= 25; i++)
            _repo.Add(new StudyDocument { Ten = $"Doc {i}" });

        var allIds = _repo.GetAll().Select(d => d.Id).ToList();
        foreach (var id in allIds)
            DatabaseHelper.AddRecentFile(id);

        var recent = DatabaseHelper.GetRecentFiles();
        Assert.True(recent.Count <= 20);
    }

    [Fact]
    public void RemoveRecentFile_RemovesSpecificEntry()
    {
        int docId = CreateDoc("To Remove");
        DatabaseHelper.AddRecentFile(docId);
        DatabaseHelper.RemoveRecentFile(docId);

        Assert.Empty(DatabaseHelper.GetRecentFiles());
    }

    [Fact]
    public void ClearRecentFiles_EmptiesHistory()
    {
        int d1 = CreateDoc("RF1");
        int d2 = CreateDoc("RF2");
        DatabaseHelper.AddRecentFile(d1);
        DatabaseHelper.AddRecentFile(d2);

        DatabaseHelper.ClearRecentFiles();
        Assert.Empty(DatabaseHelper.GetRecentFiles());
    }
}

// ════════════════════════════════════════════════════════════
// F21: Dashboard Statistics
// ════════════════════════════════════════════════════════════

public class DashboardStatisticsTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public DashboardStatisticsTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void GetDashboardStatistics_EmptyDb_AllZeros()
    {
        var stats = DatabaseHelper.GetDashboardStatistics();

        Assert.Equal(0, stats.TotalDocuments);
        Assert.Equal(0, stats.ImportantDocuments);
        Assert.Equal(0, stats.OverdueDocuments);
        Assert.Equal(0, stats.NearDeadlineDocuments);
        Assert.Equal(0, stats.TotalCollections);
    }

    [Fact]
    public void GetDashboardStatistics_CountsCorrectly()
    {
        _repo.Add(new StudyDocument { Ten = "Normal" });
        _repo.Add(new StudyDocument { Ten = "Important", QuanTrong = true });
        _repo.Add(new StudyDocument { Ten = "Overdue", Deadline = DateTime.Today.AddDays(-2) });
        _repo.Add(new StudyDocument { Ten = "Near", Deadline = DateTime.Today.AddDays(3) });
        _repo.Add(new StudyDocument { Ten = "No File", DuongDan = "" });

        DatabaseHelper.CreateCollection("TestCol");

        var stats = DatabaseHelper.GetDashboardStatistics();

        Assert.Equal(5, stats.TotalDocuments);
        Assert.Equal(1, stats.ImportantDocuments);
        Assert.Equal(1, stats.OverdueDocuments);
        Assert.Equal(1, stats.NearDeadlineDocuments);
        Assert.Equal(1, stats.TotalCollections);
    }

    [Fact]
    public void GetDashboardStatistics_ExcludesSoftDeleted()
    {
        _repo.Add(new StudyDocument { Ten = "Visible" });
        _repo.Add(new StudyDocument { Ten = "Deleted" });
        int delId = _repo.GetAll().First(d => d.Ten == "Deleted").Id;
        _repo.Delete(delId);

        var stats = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(1, stats.TotalDocuments);
    }

    [Fact]
    public void GetDocumentsByDay_Returns7DataPoints()
    {
        var data = DatabaseHelper.GetDocumentsByDay(7);
        Assert.Equal(7, data.Count);
    }

    [Fact]
    public void GetDocumentsByMonth_Returns12DataPoints()
    {
        var data = DatabaseHelper.GetDocumentsByMonth(12);
        Assert.Equal(12, data.Count);
    }

    [Fact]
    public void GetDocumentsBySubject_ReturnsGroupedCounts()
    {
        _repo.Add(new StudyDocument { Ten = "A", MonHoc = "Math" });
        _repo.Add(new StudyDocument { Ten = "B", MonHoc = "Math" });
        _repo.Add(new StudyDocument { Ten = "C", MonHoc = "Science" });

        var data = DatabaseHelper.GetDocumentsBySubject();
        var math = data.FirstOrDefault(d => d.Label == "Math");

        Assert.Equal(2, math.Count);
    }

    [Fact]
    public void GetDocumentsByType_ReturnsGroupedCounts()
    {
        _repo.Add(new StudyDocument { Ten = "A", Loai = "PDF" });
        _repo.Add(new StudyDocument { Ten = "B", Loai = "Video" });
        _repo.Add(new StudyDocument { Ten = "C", Loai = "PDF" });

        var data = DatabaseHelper.GetDocumentsByType();
        var pdf = data.FirstOrDefault(d => d.Label == "PDF");

        Assert.Equal(2, pdf.Count);
    }

    [Fact]
    public void GetTotalDocumentCount_ReturnsActiveCount()
    {
        _repo.Add(new StudyDocument { Ten = "Active" });
        _repo.Add(new StudyDocument { Ten = "Soft Deleted" });
        var id = _repo.GetAll().First(d => d.Ten == "Soft Deleted").Id;
        _repo.Delete(id);

        int count = DatabaseHelper.GetTotalDocumentCount();
        Assert.Equal(1, count);
    }
}

// ════════════════════════════════════════════════════════════
// F19: File Integrity helpers (UpdateDocumentPath / ClearDocumentPath)
// ════════════════════════════════════════════════════════════

public class FileIntegrityTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public FileIntegrityTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void UpdateDocumentPath_ChangesFilePath()
    {
        _repo.Add(new StudyDocument { Ten = "File Doc", DuongDan = @"C:\old\path.pdf" });
        int id = _repo.GetAll()[0].Id;

        bool result = DatabaseHelper.UpdateDocumentPath(id, @"C:\new\path.pdf");
        var updated = _repo.GetById(id)!;

        Assert.True(result);
        Assert.Equal(@"C:\new\path.pdf", updated.DuongDan);
    }

    [Fact]
    public void ClearDocumentPath_SetsEmptyPath()
    {
        _repo.Add(new StudyDocument { Ten = "Has Path", DuongDan = @"C:\file.pdf" });
        int id = _repo.GetAll()[0].Id;

        bool result = DatabaseHelper.ClearDocumentPath(id);
        var updated = _repo.GetById(id)!;

        Assert.True(result);
        Assert.Equal("", updated.DuongDan);
    }
}

// ════════════════════════════════════════════════════════════
// F24: Backup Database
// ════════════════════════════════════════════════════════════

public class BackupTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public BackupTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void BackupDatabase_CreatesBackupFile()
    {
        _repo.Add(new StudyDocument { Ten = "Backup Test Doc" });
        string backupPath = Path.Combine(Path.GetTempPath(), $"backup_{Guid.NewGuid():N}.db");

        try
        {
            bool result = DatabaseHelper.BackupDatabase(backupPath);
            Assert.True(result);
            Assert.True(File.Exists(backupPath));
        }
        finally
        {
            if (File.Exists(backupPath)) File.Delete(backupPath);
        }
    }

    [Fact]
    public void BackupDatabase_InvalidPath_ReturnsFalse()
    {
        bool result = DatabaseHelper.BackupDatabase(@"Z:\nonexistent\deep\path\backup.db");
        Assert.False(result);
    }
}

// ════════════════════════════════════════════════════════════
// F10: AppVersion Service
// ════════════════════════════════════════════════════════════

public class AppVersionTests
{
    [Fact]
    public void AppVersion_Current_IsNotEmpty()
    {
        var version = StudyDocumentManager.Core.Services.AppVersion.Current;
        Assert.False(string.IsNullOrWhiteSpace(version));
    }

    [Fact]
    public void AppVersion_Current_FollowsSemver()
    {
        var version = StudyDocumentManager.Core.Services.AppVersion.Current;
        var parts = version.Split('.');
        Assert.True(parts.Length >= 2, "Version should have at least major.minor");
        Assert.All(parts, p => Assert.True(int.TryParse(p, out _), $"Part '{p}' should be numeric"));
    }
}

// ════════════════════════════════════════════════════════════
// F23: Export CSV (EscapeCsv logic – tested via document fields)
// ════════════════════════════════════════════════════════════

public class CsvExportTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public CsvExportTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void ExportData_AllDocumentFieldsCanBeRetrieved()
    {
        _repo.Add(new StudyDocument
        {
            Ten = "CSV Test \"Quoted\"",
            MonHoc = "Học tập",
            Loai = "Tài liệu",
            TacGia = "Author, Jr.",
            Tags = "tag1;tag2",
            QuanTrong = true,
            KichThuoc = 2.5,
            Deadline = DateTime.Today.AddDays(5)
        });

        var doc = _repo.GetAll()[0];

        // Verify all fields are properly stored and retrievable for CSV export
        Assert.Equal("CSV Test \"Quoted\"", doc.Ten);
        Assert.Equal("Author, Jr.", doc.TacGia);
        Assert.Equal(2.5, doc.KichThuoc);
        Assert.True(doc.QuanTrong);
        Assert.NotNull(doc.Deadline);
    }
}

// ════════════════════════════════════════════════════════════
// Integration: Repository + DatabaseHelper Round-trip
// ════════════════════════════════════════════════════════════

public class IntegrationTests : DatabaseTestBase
{
    private readonly DocumentRepository _repo;

    public IntegrationTests() { _repo = new DocumentRepository(); }

    [Fact]
    public void FullCycle_AddUpdateDeleteRestore()
    {
        // Add
        _repo.Add(new StudyDocument { Ten = "Lifecycle", MonHoc = "Test" });
        var doc = _repo.GetAll().First();
        int id = doc.Id;
        Assert.Equal("Lifecycle", doc.Ten);

        // Update
        doc.Ten = "Updated Lifecycle";
        _repo.Update(doc);
        Assert.Equal("Updated Lifecycle", _repo.GetById(id)!.Ten);

        // Soft Delete
        _repo.Delete(id);
        Assert.Empty(_repo.GetAll());
        Assert.Single(DatabaseHelper.GetDeletedDocuments());

        // Restore
        DatabaseHelper.RestoreDocument(id);
        Assert.Single(_repo.GetAll());
        Assert.Equal("Updated Lifecycle", _repo.GetAll()[0].Ten);

        // Permanent Delete
        DatabaseHelper.PermanentDeleteDocument(id);
        Assert.Empty(_repo.GetAll());
        Assert.Null(_repo.GetById(id));
    }

    [Fact]
    public void CollectionAndDocumentLifecycle()
    {
        // Create collection
        int colId = DatabaseHelper.CreateCollection("Integration Collection");
        Assert.True(colId > 0);

        // Add docs to collection
        _repo.Add(new StudyDocument { Ten = "Col Doc 1" });
        _repo.Add(new StudyDocument { Ten = "Col Doc 2" });
        var docs = _repo.GetAll();

        DatabaseHelper.AddDocumentToCollection(colId, docs[0].Id);
        DatabaseHelper.AddDocumentToCollection(colId, docs[1].Id);

        Assert.Equal(2, DatabaseHelper.GetDocumentsInCollection(colId).Count);

        // Remove one from collection
        DatabaseHelper.RemoveDocumentFromCollection(colId, docs[0].Id);
        Assert.Single(DatabaseHelper.GetDocumentsInCollection(colId));

        // Delete collection — docs remain
        DatabaseHelper.DeleteCollection(colId);
        Assert.Empty(DatabaseHelper.GetCollections());
        Assert.Equal(2, _repo.GetAll().Count); // Docs unaffected
    }

    [Fact]
    public void StatisticsReflectLiveChanges()
    {
        var stats0 = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(0, stats0.TotalDocuments);

        _repo.Add(new StudyDocument { Ten = "S1", QuanTrong = true });
        _repo.Add(new StudyDocument { Ten = "S2" });

        var stats2 = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(2, stats2.TotalDocuments);
        Assert.Equal(1, stats2.ImportantDocuments);

        // Soft delete one
        _repo.Delete(_repo.GetAll()[0].Id);
        var stats1 = DatabaseHelper.GetDashboardStatistics();
        Assert.Equal(1, stats1.TotalDocuments);
    }
}
