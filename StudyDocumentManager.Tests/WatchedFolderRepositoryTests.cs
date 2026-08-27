using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;
using Microsoft.Data.Sqlite;
using Xunit;

namespace StudyDocumentManager.Tests;

public class WatchedFolderRepositoryTests : DatabaseTestBase
{
    private readonly WatchedFolderRepository _folders;

    public WatchedFolderRepositoryTests()
    {
        _folders = new WatchedFolderRepository(Db);
    }

    [Fact]
    public void Schema_ContainsWatchedFoldersTableAndIndex()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='watched_folders'";
        Assert.NotNull(cmd.ExecuteScalar());
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name='ux_watched_folders_path'";
        Assert.NotNull(cmd.ExecuteScalar());
    }

    [Fact]
    public void Add_ReturnsPositiveId_AndGetByPathIsCaseInsensitive()
    {
        var item = new WatchedFolder { FolderPath = @"C:\Watch\A", Enabled = true, IncludeSubdirectories = false };
        _folders.Add(item);
        Assert.True(item.Id > 0);

        var byLower = _folders.GetByPath(@"c:\watch\a");
        Assert.NotNull(byLower);
        Assert.Equal(item.Id, byLower!.Id);

        var all = _folders.GetAll();
        Assert.Contains(all, f => f.Id == item.Id);
    }

    [Fact]
    public void Update_PersistsChanges()
    {
        var item = new WatchedFolder { FolderPath = @"C:\Watch\B", Enabled = true };
        _folders.Add(item);
        item.Enabled = false;
        item.IncludeSubdirectories = true;
        Assert.True(_folders.Update(item));

        var stored = _folders.GetByPath(@"C:\Watch\B");
        Assert.False(stored!.Enabled);
        Assert.True(stored.IncludeSubdirectories);
    }

    [Fact]
    public void SetEnabled_TogglesValue()
    {
        var item = new WatchedFolder { FolderPath = @"C:\Watch\C", Enabled = true };
        _folders.Add(item);
        _folders.SetEnabled(item.Id, false);
        Assert.False(_folders.GetByPath(@"C:\Watch\C")!.Enabled);
    }

    [Fact]
    public void RecordScan_UpdatesLastScanAt()
    {
        var item = new WatchedFolder { FolderPath = @"C:\Watch\D", Enabled = true };
        _folders.Add(item);
        var scan = new DateTime(2026, 1, 2, 3, 4, 5);
        _folders.RecordScan(item.Id, scan);

        var stored = _folders.GetByPath(@"C:\Watch\D");
        Assert.NotNull(stored!.LastScanAt);
        Assert.Equal(scan, stored.LastScanAt!.Value, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetEnabled_ReturnsOnlyEnabled()
    {
        _folders.Add(new WatchedFolder { FolderPath = @"C:\Watch\E1", Enabled = true });
        _folders.Add(new WatchedFolder { FolderPath = @"C:\Watch\E2", Enabled = false });
        var enabled = _folders.GetEnabled();
        Assert.Single(enabled);
        Assert.Equal(@"C:\Watch\E1", enabled[0].FolderPath);
    }

    [Fact]
    public void Delete_RemovesRow()
    {
        var item = new WatchedFolder { FolderPath = @"C:\Watch\F", Enabled = true };
        _folders.Add(item);
        Assert.True(_folders.Delete(item.Id));
        Assert.Null(_folders.GetByPath(@"C:\Watch\F"));
    }
}
