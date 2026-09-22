using System;
using System.IO;
using System.Linq;
using Xunit;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Tests;

public class WatchedFolderIntegrationTests : DatabaseTestBase
{
    private readonly string _watchDir;
    private readonly ImportInboxRepository _inbox;
    private readonly WatchedFolderRepository _folders;
    private readonly IFileSystemWatcherAdapterFactory _adapterFactory = new FileSystemWatcherAdapterFactory();
    private readonly ILog _log = new TraceLog();

    public WatchedFolderIntegrationTests()
    {
        _watchDir = Path.Combine(Path.GetTempPath(), $"sdm_wfi_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_watchDir);
        _inbox = new ImportInboxRepository(Db);
        _folders = new WatchedFolderRepository(Db);
    }

    private WatchedFolder Seed()
    {
        var folder = new WatchedFolder { FolderPath = _watchDir, Enabled = true, IncludeSubdirectories = false };
        _folders.Add(folder);
        return folder;
    }

    [Fact]
    public void ScanNow_HandsOff_WithPendingState_AndKeepsSource()
    {
        var file = Path.Combine(_watchDir, "doc.pdf");
        File.WriteAllText(file, "content");
        var folder = Seed();

        using var watcher = new WatchedFolderWatcher(folder, _inbox, _folders, _adapterFactory, _log, TimeSpan.FromHours(1));
        watcher.ScanNow();

        var item = _inbox.GetAll().Single(i => i.SourcePath == file);
        Assert.Equal(ImportInboxState.Pending, item.State);
        Assert.Equal("doc.pdf", item.DisplayName);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void Restart_ScanNowTwice_DeduplicatesToUniqueFiles()
    {
        var f1 = Path.Combine(_watchDir, "a.pdf");
        var f2 = Path.Combine(_watchDir, "b.pdf");
        File.WriteAllText(f1, "1");
        File.WriteAllText(f2, "2");
        var folder = Seed();

        using var watcher = new WatchedFolderWatcher(folder, _inbox, _folders, _adapterFactory, _log, TimeSpan.FromHours(1));
        watcher.ScanNow();
        watcher.ScanNow(); // simulate restart/catch-up

        var entries = _inbox.GetAll();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, i => i.SourcePath == f1);
        Assert.Contains(entries, i => i.SourcePath == f2);
        Assert.All(entries, i => Assert.True(File.Exists(i.SourcePath)));
    }

    [Fact]
    public void NewFileAfterInitialScan_IsAddedWithoutDuplicatingExisting()
    {
        var f1 = Path.Combine(_watchDir, "a.pdf");
        File.WriteAllText(f1, "1");
        var folder = Seed();

        using var watcher = new WatchedFolderWatcher(folder, _inbox, _folders, _adapterFactory, _log, TimeSpan.FromHours(1));
        watcher.ScanNow();

        var f2 = Path.Combine(_watchDir, "b.pdf");
        File.WriteAllText(f2, "2");
        watcher.ScanNow();

        Assert.Equal(2, _inbox.GetAll().Count);
    }

    [Fact]
    public void Rescan_PreservesPreviouslyProcessedRow_InsteadOfRequeuingPending()
    {
        var file = Path.Combine(_watchDir, "done.pdf");
        File.WriteAllText(file, "content");
        var folder = Seed();

        var doc = new StudyDocument { Name = "Prior", FilePath = "prior.pdf", Subject = "S", Type = "T" };
        Db.InsertDocument(doc);

        // Simulate an already-processed inbox entry for this source file.
        _inbox.Add(new ImportInboxItem
        {
            SourcePath = file,
            DisplayName = "done",
            State = ImportInboxState.Processed,
            DocumentId = doc.Id,
            DuplicateCandidate = "99:Prior"
        });

        using var watcher = new WatchedFolderWatcher(folder, _inbox, _folders, _adapterFactory, _log, TimeSpan.FromHours(1));
        watcher.ScanNow();

        var rows = _inbox.GetAll(true).Where(i => i.SourcePath == file).ToList();
        Assert.Single(rows);
        var loaded = rows[0];
        Assert.Equal(ImportInboxState.Processed, loaded.State);
        Assert.Equal(doc.Id, loaded.DocumentId);
        Assert.Equal("99:Prior", loaded.DuplicateCandidate);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void RestartWatcher_Rescan_PreservesProcessedRow_AcrossFreshInstance()
    {
        var file = Path.Combine(_watchDir, "done2.pdf");
        File.WriteAllText(file, "content");
        var folder = Seed();

        var doc = new StudyDocument { Name = "Prior2", FilePath = "prior2.pdf", Subject = "S", Type = "T" };
        Db.InsertDocument(doc);

        _inbox.Add(new ImportInboxItem
        {
            SourcePath = file,
            DisplayName = "done2",
            State = ImportInboxState.Processed,
            DocumentId = doc.Id,
            DuplicateCandidate = "x:Prior2"
        });

        // First watcher instance runs a scan (initial session).
        using (var watcher = new WatchedFolderWatcher(folder, _inbox, _folders, _adapterFactory, _log, TimeSpan.FromHours(1)))
        {
            watcher.ScanNow();
        }

        // App restart: brand-new watcher instance performs catch-up rescan.
        using (var watcher = new WatchedFolderWatcher(folder, _inbox, _folders, _adapterFactory, _log, TimeSpan.FromHours(1)))
        {
            watcher.ScanNow();
        }

        var rows = _inbox.GetAll(true).Where(i => i.SourcePath == file).ToList();
        Assert.Single(rows);
        var loaded = rows[0];
        Assert.Equal(ImportInboxState.Processed, loaded.State);
        Assert.Equal(doc.Id, loaded.DocumentId);
        Assert.Equal("x:Prior2", loaded.DuplicateCandidate);
        Assert.True(File.Exists(file));
    }
}
