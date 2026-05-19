using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Repositories;
using Xunit;
using Xunit.Abstractions;

namespace StudyDocumentManager.Tests;

/// <summary>
/// Tests the full ViewModel logic WITHOUT Avalonia UI.
/// Simulates what CreateCollectionAsync does, replacing the dialog service with a mock.
/// </summary>
public class CollectionViewModelLogicTests : DatabaseTestBase
{
    private readonly ITestOutputHelper _out;
    private readonly DocumentRepository _repo;

    public CollectionViewModelLogicTests(ITestOutputHelper output)
    {
        _out = output;
        _repo = new DocumentRepository();
    }

    // ─────────────────────────────────────────────────────────────
    // Helper: simulate CreateCollectionAsync body without dialog
    // ─────────────────────────────────────────────────────────────
    private (int id, int collectionCount) SimulateCreateCollection(string? dialogResult)
    {
        _out.WriteLine($"[Simulate] dialogResult = '{dialogResult}'");

        if (!string.IsNullOrWhiteSpace(dialogResult))
        {
            _out.WriteLine($"[Simulate] Calling CreateCollection('{dialogResult}')");
            int id = DatabaseHelper.CreateCollection(dialogResult);
            _out.WriteLine($"[Simulate] CreateCollection returned id={id}");

            var collections = DatabaseHelper.GetCollections();
            _out.WriteLine($"[Simulate] GetCollections count={collections.Count}");
            return (id, collections.Count);
        }

        _out.WriteLine("[Simulate] dialogResult was null/empty → no creation");
        return (0, 0);
    }

    [Fact]
    public void SimulateCreate_WithValidName_Succeeds()
    {
        var (id, count) = SimulateCreateCollection("Test BST");
        Assert.True(id > 0);
        Assert.Equal(1, count);
    }

    [Fact]
    public void SimulateCreate_WithNull_NoOp()
    {
        var (id, count) = SimulateCreateCollection(null);
        Assert.Equal(0, id);
        Assert.Equal(0, count);
    }

    [Fact]
    public void SimulateCreate_WithEmptyString_NoOp()
    {
        var (id, count) = SimulateCreateCollection("   ");
        Assert.Equal(0, id);
        Assert.Equal(0, count);
    }

    // ─────────────────────────────────────────────────────────────
    // Test edge cases that could silently fail in production
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void CreateCollection_WithVietnameseName_Succeeds()
    {
        _out.WriteLine("[Test] CreateCollection with Vietnamese chars");
        int id = DatabaseHelper.CreateCollection("Bộ sưu tập học tập 2024");
        _out.WriteLine($"[Result] id={id}");
        Assert.True(id > 0);

        var list = DatabaseHelper.GetCollections();
        _out.WriteLine($"[Result] count={list.Count}, name='{list[0].Name}'");
        Assert.Equal("Bộ sưu tập học tập 2024", list[0].Name);
    }

    [Fact]
    public void CreateCollection_LongName_Succeeds()
    {
        var longName = new string('A', 500);
        _out.WriteLine($"[Test] CreateCollection with 500-char name");
        int id = DatabaseHelper.CreateCollection(longName);
        _out.WriteLine($"[Result] id={id}");
        Assert.True(id > 0);

        var list = DatabaseHelper.GetCollections();
        Assert.Equal(longName, list[0].Name);
    }

    [Fact]
    public void CreateMultipleCollections_AllAppear_InGetCollections()
    {
        _out.WriteLine("[Test] Create 3 collections then list all");
        var names = new[] { "Alpha", "Beta", "Gamma" };

        foreach (var n in names)
        {
            int id = DatabaseHelper.CreateCollection(n);
            _out.WriteLine($"  → Created '{n}' id={id}");
        }

        var list = DatabaseHelper.GetCollections();
        _out.WriteLine($"[Result] count={list.Count}");
        foreach (var c in list)
            _out.WriteLine($"  → id={c.Id} name='{c.Name}'");

        Assert.Equal(3, list.Count);
    }

    // ─────────────────────────────────────────────────────────────
    // Test: ConnectionString không bị đè bởi test parallel nào
    // (xunit.runner.json đã tắt parallel nên test này phải pass)
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public void DatabaseHelper_ConnectionString_MatchesDbPath()
    {
        _out.WriteLine($"[Test] DbPath={DbPath}");
        _out.WriteLine($"[Test] DatabaseHelper.DatabasePath={DatabaseHelper.DatabasePath}");
        Assert.Equal(DbPath, DatabaseHelper.DatabasePath);
        Assert.Contains(DbPath, DatabaseHelper.ConnectionString);
    }

    // ─────────────────────────────────────────────────────────────
    // Test: GetCollections SQL — xác nhận column mapping đúng
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public void GetCollections_ItemCount_IsCorrect()
    {
        _out.WriteLine("[Test] GetCollections ItemCount after adding docs to collection");

        // Create a document
        var doc = new StudyDocument { Ten = "Doc A", MonHoc = "Test", Loai = "PDF" };
        bool inserted = DatabaseHelper.InsertDocument(doc);
        _out.WriteLine($"[Step 1] InsertDocument result={inserted}");

        // Get the inserted doc's ID
        var docs = DatabaseHelper.GetAllDocuments();
        _out.WriteLine($"[Step 1] GetAllDocuments count={docs.Count}");
        Assert.Single(docs);
        int docId = docs[0].Id;

        // Create collection
        int colId = DatabaseHelper.CreateCollection("My Playlist");
        _out.WriteLine($"[Step 2] CreateCollection id={colId}");

        // Add doc to collection
        bool added = DatabaseHelper.AddDocumentToCollection(colId, docId);
        _out.WriteLine($"[Step 3] AddDocumentToCollection result={added}");
        Assert.True(added);

        // Verify ItemCount
        var collections = DatabaseHelper.GetCollections();
        _out.WriteLine($"[Step 4] GetCollections count={collections.Count}");
        _out.WriteLine($"[Step 4] ItemCount={collections[0].ItemCount}");

        Assert.Single(collections);
        Assert.Equal(1, collections[0].ItemCount);
    }
}
