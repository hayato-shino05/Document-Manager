using StudyDocumentManager.Data.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace StudyDocumentManager.Tests;

/// <summary>
/// Targeted debug tests for the "Cannot create collection" bug.
/// Run with:  dotnet test --filter "FullyQualifiedName~CollectionDebug" -v normal
/// </summary>
public class CollectionDebugTests : DatabaseTestBase
{
    private readonly ITestOutputHelper _out;

    public CollectionDebugTests(ITestOutputHelper output)
    {
        _out = output;
        _out.WriteLine($"[Setup] DB path: {DbPath}");
        _out.WriteLine($"[Setup] DB file exists: {File.Exists(DbPath)}");
        _out.WriteLine($"[Setup] ConnectionString: {Db.ConnectionString}");
    }

    // ─────────────────────────────────────────────────────────────
    // 1. Basic: can we INSERT then SELECT a collection?
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public void CreateCollection_BasicInsert_ReturnsPositiveId()
    {
        _out.WriteLine("[Test] CreateCollection_BasicInsert_ReturnsPositiveId");

        int id = Db.CreateCollection("Test Collection");
        _out.WriteLine($"[Result] returned id = {id}");

        Assert.True(id > 0, $"Expected id > 0 but got {id}");
    }

    [Fact]
    public void CreateCollection_ThenGetCollections_ReturnsIt()
    {
        _out.WriteLine("[Test] CreateCollection_ThenGetCollections_ReturnsIt");

        int id = Db.CreateCollection("My Collection");
        _out.WriteLine($"[Result] CreateCollection id = {id}");

        var list = Db.GetCollections();
        _out.WriteLine($"[Result] GetCollections count = {list.Count}");
        foreach (var c in list)
            _out.WriteLine($"  → id={c.Id} name='{c.Name}' desc='{c.Description}' count={c.ItemCount}");

        Assert.Single(list);
        Assert.Equal("My Collection", list[0].Name);
        Assert.Equal(id, list[0].Id);
    }

    [Fact]
    public void CreateCollection_EmptyDb_GetCollectionsReturnsEmpty()
    {
        _out.WriteLine("[Test] CreateCollection_EmptyDb_GetCollectionsReturnsEmpty");

        var list = Db.GetCollections();
        _out.WriteLine($"[Result] GetCollections count = {list.Count}");

        Assert.Empty(list);
    }

    // ─────────────────────────────────────────────────────────────
    // 2. Concurrency: does the static ConnectionString change between tests?
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public void ConnectionString_IsSetCorrectly_ForThisTestInstance()
    {
        _out.WriteLine("[Test] ConnectionString_IsSetCorrectly_ForThisTestInstance");
        _out.WriteLine($"[Result] DbPath = {DbPath}");
        _out.WriteLine($"[Result] Db.DatabasePath = {Db.DatabasePath}");
        _out.WriteLine($"[Result] Match = {Db.DatabasePath == DbPath}");

        // If this fails → SetDatabasePath() is not thread-safe (parallel tests clobber each other)
        Assert.Equal(DbPath, Db.DatabasePath);
    }

    // ─────────────────────────────────────────────────────────────
    // 3. Duplicate name: what happens?
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public void CreateCollection_DuplicateName_ThrowsOrReturnsZero()
    {
        _out.WriteLine("[Test] CreateCollection_DuplicateName_ThrowsOrReturnsZero");

        int id1 = Db.CreateCollection("Dupe");
        _out.WriteLine($"[Result] First insert id = {id1}");

        try
        {
            int id2 = Db.CreateCollection("Dupe");
            _out.WriteLine($"[Result] Second insert id = {id2}  ← no exception thrown");
            // Either it returned 0 or a new id (depends on DB constraint)
            _out.WriteLine(id2 == 0
                ? "  → returned 0 (handled duplicate gracefully)"
                : $"  → returned {id2} (no UNIQUE constraint on name!)");
        }
        catch (Exception ex)
        {
            _out.WriteLine($"[Result] Exception on duplicate: {ex.GetType().Name}: {ex.Message}");
            // This is acceptable — unique constraint enforcement
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 4. Simulate the exact ViewModel flow
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public void FullViewModelFlow_CreateAndList_Succeeds()
    {
        _out.WriteLine("[Test] FullViewModelFlow_CreateAndList_Succeeds");

        // Simulate: ShowInputAsync returns "New Collection"
        string simulatedDialogResult = "New Collection";
        _out.WriteLine($"[Step 1] Simulated dialog result = '{simulatedDialogResult}'");

        // Guard identical to ViewModel
        if (!string.IsNullOrWhiteSpace(simulatedDialogResult))
        {
            _out.WriteLine("[Step 2] Calling CreateCollection...");
            var newId = Db.CreateCollection(simulatedDialogResult);
            _out.WriteLine($"[Step 2] CreateCollection returned id={newId}");

            _out.WriteLine("[Step 3] Calling GetCollections (LoadCollections equivalent)...");
            var data = Db.GetCollections();
            _out.WriteLine($"[Step 3] GetCollections count={data.Count}");
            foreach (var c in data)
                _out.WriteLine($"  → id={c.Id} name='{c.Name}' itemCount={c.ItemCount}");

            Assert.True(newId > 0, "CreateCollection should return positive ID");
            Assert.Single(data);
            Assert.Equal("New Collection", data[0].Name);
        }
        else
        {
            Assert.Fail("Dialog result was null/empty — simulated dialog bug");
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 5. スキーマ確認: collections テーブルの存在
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public void Schema_CollectionsTable_Exists()
    {
        _out.WriteLine("[Test] Schema_CollectionsTable_Exists");

        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(Db.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='collections'";
        var result = cmd.ExecuteScalar()?.ToString();
        _out.WriteLine($"[Result] sqlite_master lookup: '{result}'");

        Assert.Equal("collections", result);
    }

    [Fact]
    public void Schema_CollectionsTable_HasExpectedColumns()
    {
        _out.WriteLine("[Test] Schema_CollectionsTable_HasExpectedColumns");

        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(Db.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(collections)";
        using var reader = cmd.ExecuteReader();

        var columns = new List<string>();
        while (reader.Read())
        {
            var col = reader["name"]?.ToString() ?? "";
            columns.Add(col);
            _out.WriteLine($"  → column: {col} ({reader["type"]})");
        }

        Assert.Contains("id", columns);
        Assert.Contains("name", columns);
        // description may or may not exist depending on migration
        _out.WriteLine($"[Result] description column exists: {columns.Contains("description")}");
    }

    // ─────────────────────────────────────────────────────────────
    // 6. GetCollections SQL の手動クエリ確認
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public void GetCollections_AfterInsert_RawSqlConfirms()
    {
        _out.WriteLine("[Test] GetCollections_AfterInsert_RawSqlConfirms");

        // Insert via helper
        Db.CreateCollection("Alpha");
        Db.CreateCollection("Beta");

        // Read back directly with raw SQL (bypass helper to isolate)
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection(Db.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name FROM collections ORDER BY name";
        using var reader = cmd.ExecuteReader();

        var rows = new List<(int id, string name)>();
        while (reader.Read())
        {
            rows.Add((reader.GetInt32(0), reader.GetString(1)));
            _out.WriteLine($"  → raw row: id={rows[^1].id} name='{rows[^1].name}'");
        }

        _out.WriteLine($"[Result] Raw row count = {rows.Count}");
        Assert.Equal(2, rows.Count);
        Assert.Equal("Alpha", rows[0].name);
        Assert.Equal("Beta", rows[1].name);

        // Now confirm via helper
        var helperResult = Db.GetCollections();
        _out.WriteLine($"[Result] GetCollections() count = {helperResult.Count}");
        Assert.Equal(2, helperResult.Count);
    }
}
