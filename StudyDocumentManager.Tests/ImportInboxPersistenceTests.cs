using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using StudyDocumentManager.Tests.TestDoubles;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class ImportInboxPersistenceTests : DatabaseTestBase
{
    [Fact]
    public void ImportInboxPersistsStateAcrossRepositoryInstances()
    {
        var item = new ImportInboxItem
        {
            SourcePath = "C:\\docs\\pending.pdf",
            DisplayName = "pending",
            State = ImportInboxState.Held,
            DuplicateCandidate = "42"
        };

        var repository = new StudyDocumentManager.Data.Repositories.ImportInboxRepository(Db);
        repository.Add(item);

        var reloadedDb = new StudyDocumentManager.Data.Helpers.DatabaseHelper();
        reloadedDb.SetDatabasePath(DbPath);
        reloadedDb.InitializeDatabase();
        var reloaded = new StudyDocumentManager.Data.Repositories.ImportInboxRepository(reloadedDb);
        var found = reloaded.GetById(item.Id);

        Assert.NotNull(found);
        Assert.Equal(ImportInboxState.Held, found!.State);
        Assert.Equal("42", found.DuplicateCandidate);
    }

    [Fact]
    public void SecondBatchReusesSourceRowAndPersistsOutcome()
    {
        var repository = new StudyDocumentManager.Data.Repositories.ImportInboxRepository(Db);
        var first = new ImportInboxItem { SourcePath = "same.pdf", DisplayName = "same", State = ImportInboxState.Failed };
        repository.Add(first);
        var firstId = first.Id;
        var second = new ImportInboxItem { SourcePath = "same.pdf", DisplayName = "same", State = ImportInboxState.Pending };
        repository.Add(second);
        Assert.Equal(firstId, second.Id);
        repository.UpdateState(second.Id, ImportInboxState.Processed);
        Assert.Equal(ImportInboxState.Processed, repository.GetById(firstId)!.State);
        Assert.Single(repository.GetAll(true));
    }

    [Fact]
    public void ImportInboxSeparatesProcessedFromDefaultListing()
    {
        var repository = new StudyDocumentManager.Data.Repositories.ImportInboxRepository(Db);
        repository.Add(new ImportInboxItem { SourcePath = "a.pdf", DisplayName = "a", State = ImportInboxState.Processed });
        repository.Add(new ImportInboxItem { SourcePath = "b.pdf", DisplayName = "b", State = ImportInboxState.Failed });

        Assert.Single(repository.GetAll());
        Assert.Equal(2, repository.GetAll(true).Count);
    }

    [Fact]
    public void ImportInboxPersistsTypeAndFailureCode()
    {
        var item = new ImportInboxItem
        {
            SourcePath = "C:\\docs\\note.pdf",
            DisplayName = "note",
            State = ImportInboxState.Failed,
            Type = "PDF",
            FailureCode = "ImportFailed"
        };
        var repository = new StudyDocumentManager.Data.Repositories.ImportInboxRepository(Db);
        repository.Add(item);

        var found = repository.GetById(item.Id);
        Assert.Equal("PDF", found!.Type);
        Assert.Equal("ImportFailed", found.FailureCode);
    }

    [Fact]
    public void ImportInboxDeduplicatesSameSourceAcrossRuns()
    {
        var repository = new StudyDocumentManager.Data.Repositories.ImportInboxRepository(Db);
        var firstId = repository.Add(new ImportInboxItem
        {
            SourcePath = "C:\\docs\\dup.pdf",
            DisplayName = "dup",
            State = ImportInboxState.Held,
            DuplicateCandidate = "x"
        });

        var secondId = repository.Add(new ImportInboxItem
        {
            SourcePath = "c:\\DOCS\\dup.pdf",
            DisplayName = "dup2",
            State = ImportInboxState.Held,
            DuplicateCandidate = "y"
        });

        Assert.Equal(firstId, secondId);
        Assert.Single(repository.GetAll(true));
        var loaded = repository.GetById(firstId)!;
        Assert.Equal("dup", loaded.DisplayName);
        Assert.Equal(ImportInboxState.Held, loaded.State);
        Assert.Equal("x", loaded.DuplicateCandidate);
    }

    [Theory]
    [InlineData(ImportInboxState.Processed)]
    [InlineData(ImportInboxState.Held)]
    [InlineData(ImportInboxState.Failed)]
    public void Add_PreservesExistingRow_OnRescan_DoesNotResetToPending(ImportInboxState prior)
    {
        var repository = new StudyDocumentManager.Data.Repositories.ImportInboxRepository(Db);
        var doc = new StudyDocument { Name = "Prior", FilePath = "prior.pdf", Subject = "S", Type = "T" };
        Db.InsertDocument(doc);
        var id = repository.Add(new ImportInboxItem
        {
            SourcePath = "C:\\docs\\rescan.pdf",
            DisplayName = "rescan",
            State = prior,
            DocumentId = doc.Id,
            DuplicateCandidate = "7:Prior",
            FailureCode = prior == ImportInboxState.Failed ? "ImportFailed" : null
        });

        // A rescan (e.g. a watched-folder catch-up) re-submits the same source
        // as Pending; it must NOT overwrite the existing outcome.
        var againId = repository.Add(new ImportInboxItem
        {
            SourcePath = "C:\\docs\\rescan.pdf",
            DisplayName = "rescan2",
            State = ImportInboxState.Pending
        });

        Assert.Equal(id, againId);
        var loaded = repository.GetById(id)!;
        Assert.Equal(prior, loaded.State);
        Assert.Equal(doc.Id, loaded.DocumentId);
        Assert.Equal("7:Prior", loaded.DuplicateCandidate);
        Assert.Equal(prior == ImportInboxState.Failed ? "ImportFailed" : null, loaded.FailureCode);
    }

    [Fact]
    public void FindActiveDocumentsByName_MatchesActiveExcludesDeletedAndIsNormalized()
    {
        Db.InsertDocument(new StudyDocument { Name = "Alpha", FilePath = "a1.pdf", Subject = "S", Type = "T" });
        Db.InsertDocument(new StudyDocument { Name = "ALPHA", FilePath = "a2.pdf", Subject = "S", Type = "T" });
        var deleted = new StudyDocument { Name = "Alpha", FilePath = "a3.pdf", Subject = "S", Type = "T" };
        Db.InsertDocument(deleted);
        Db.BulkSoftDelete(new List<int> { deleted.Id });
        Db.InsertDocument(new StudyDocument { Name = "Beta", FilePath = "b1.pdf", Subject = "S", Type = "T" });

        var matches = Db.FindActiveDocumentsByName("alpha");

        Assert.Equal(2, matches.Count);
        Assert.All(matches, m => Assert.Equal("alpha", StudyDocument.NormalizeName(m.Name)));
        Assert.DoesNotContain(matches, m => m.Id == deleted.Id);
    }

    [Fact]
    public void FindImportInboxIdBySourcePathIsCaseInsensitive()
    {
        var repository = new StudyDocumentManager.Data.Repositories.ImportInboxRepository(Db);
        var id = repository.Add(new ImportInboxItem
        {
            SourcePath = "C:\\docs\\find.pdf",
            DisplayName = "f",
            State = ImportInboxState.Pending
        });

        Assert.Equal(id, Db.FindImportInboxIdBySourcePath("c:\\DOCS\\find.pdf"));
        Assert.Null(Db.FindImportInboxIdBySourcePath("missing.pdf"));
    }

    [Fact]
    public void ImportInbox_StatePersistsAcrossBatchesAndRestarts()
    {
        var first = new StudyDocumentManager.Data.Repositories.ImportInboxRepository(Db);
        var id1 = first.Add(new ImportInboxItem
        {
            SourcePath = "C:\\docs\\a.pdf",
            DisplayName = "a",
            State = ImportInboxState.Held,
            DuplicateCandidate = "5:Alpha"
        });

        var reloaded = new StudyDocumentManager.Data.Repositories.ImportInboxRepository(Db);
        var loaded1 = reloaded.GetById(id1);
        Assert.Equal(ImportInboxState.Held, loaded1!.State);
        Assert.Equal("5:Alpha", loaded1.DuplicateCandidate);

        var id2 = reloaded.Add(new ImportInboxItem
        {
            SourcePath = "C:\\docs\\b.pdf",
            DisplayName = "b",
            State = ImportInboxState.Failed,
            FailureCode = "FileError"
        });

        var reloaded2 = new StudyDocumentManager.Data.Repositories.ImportInboxRepository(Db);
        Assert.Equal(2, reloaded2.GetAll(true).Count);
        Assert.Equal(ImportInboxState.Held, reloaded2.GetById(id1)!.State);
        Assert.Equal(ImportInboxState.Failed, reloaded2.GetById(id2)!.State);
        Assert.Equal("FileError", reloaded2.GetById(id2)!.FailureCode);
    }

    [Fact]
    public async Task BatchImport_FailedImport_CreatesFailedInboxRow()
    {
        var inbox = new StudyDocumentManager.Data.Repositories.ImportInboxRepository(Db);
        var model = new BatchImportModel(
            new RecordingDialogService(),
            new StubFileDialogService(),
            new StubNavigationService(),
            new KeyLocalizationService(),
            new FailedImportService(),
            inbox);
        model.DefaultSubject = "Physics";
        model.Files.Add(new FileImportItem { FileName = "x", FilePath = "x.pdf", FileType = "PDF", IsSelected = true });

        await model.ImportCommand.ExecuteAsync(null);

        var item = inbox.GetAll(true).Single();
        Assert.Equal(ImportInboxState.Failed, item.State);
        Assert.Equal("ImportFailed", item.FailureCode);
    }

    [Fact]
    public async Task BatchImport_Cancellation_LeavesRemainingRowsPending()
    {
        var inbox = new StudyDocumentManager.Data.Repositories.ImportInboxRepository(Db);
        var signaling = new SignalingImportService(new StudyDocumentManager.Services.DroppedFileImportService(Repo));
        var model = new BatchImportModel(
            new RecordingDialogService(),
            new StubFileDialogService(),
            new StubNavigationService(),
            new KeyLocalizationService(),
            signaling,
            inbox);
        model.DefaultSubject = "Physics";
        foreach (var name in new[] { "a", "b", "c" })
            model.Files.Add(new FileImportItem { FileName = name, FilePath = name + ".pdf", FileType = "PDF", IsSelected = true });

        var task = model.ImportCommand.ExecuteAsync(null);
        // Wait until the import has started processing the first file, then
        // cancel deterministically (no production timing hack).
        Assert.True(signaling.EnteredFirst.Wait(TimeSpan.FromSeconds(10)), "first save never started");
        model.CancelCommand.Execute(null);
        signaling.Release.Set();
        await task;

        Assert.True(model.IsImportCancelled);
        var rows = inbox.GetAll(true).ToList();
        Assert.Equal(3, rows.Count);
        Assert.Equal(1, rows.Count(r => r.State == ImportInboxState.Processed));
        Assert.Equal(2, rows.Count(r => r.State == ImportInboxState.Pending));
    }

    [Fact]
    public async Task BatchImport_Duplicate_CreatesHeldRowWithExistingCandidate()
    {
        Db.InsertDocument(new StudyDocument { Name = "Existing Homework", FilePath = "same.pdf", Subject = "S", Type = "T" });
        var importService = new StudyDocumentManager.Services.DroppedFileImportService(Repo);
        var inbox = new StudyDocumentManager.Data.Repositories.ImportInboxRepository(Db);
        var model = new BatchImportModel(
            new RecordingDialogService(),
            new StubFileDialogService(),
            new StubNavigationService(),
            new KeyLocalizationService(),
            importService,
            inbox);
        model.DefaultSubject = "Physics";
        model.Files.Add(new FileImportItem { FileName = "notes", FilePath = "same.pdf", FileType = "PDF", IsSelected = true });

        await model.ImportCommand.ExecuteAsync(null);

        var item = inbox.GetAll(true).Single();
        Assert.Equal(ImportInboxState.Held, item.State);
        Assert.Equal("1:Existing Homework", item.DuplicateCandidate);
        Assert.NotEqual("same.pdf", item.DuplicateCandidate);
    }

    [Fact]
    public void ImportInbox_ReloadAndRetry_Proof()
    {
        var source = Path.Combine(Path.GetTempPath(), $"reload-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(source, "x");
        try
        {
            var inbox = new StudyDocumentManager.Data.Repositories.ImportInboxRepository(Db);
            var id = inbox.Add(new ImportInboxItem
            {
                SourcePath = source,
                DisplayName = "x",
                State = ImportInboxState.Failed,
                FailureCode = "ImportFailed"
            });

            var model = new ImportInboxModel(
                inbox,
                new StubProcessLauncherService(),
                new StubNavigationService(),
                new KeyLocalizationService(),
                new StudyDocumentManager.Services.DroppedFileImportService(Repo));
            var item = model.Items.First(i => i.Id == id);
            Assert.Equal(ImportInboxState.Failed, item.State);

            model.SelectedItem = item;
            model.RetrySelectedCommand.Execute(null);

            var reloaded = new StudyDocumentManager.Data.Repositories.ImportInboxRepository(Db);
            var after = reloaded.GetById(id);
            Assert.Equal(ImportInboxState.Processed, after!.State);
            Assert.True(after.DocumentId.HasValue);
        }
        finally
        {
            if (File.Exists(source))
                File.Delete(source);
        }
    }

    private sealed class FailedImportService : IDroppedFileImportService
    {
        public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects) => fallbackSubjects.ToList();
        public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes) => fallbackTypes.ToList();
        public StudyDocument BuildDocumentFromPath(string filePath) => new() { Name = Path.GetFileNameWithoutExtension(filePath), FilePath = filePath };
        public DocumentImportOutcome SaveDocument(StudyDocument document) => DocumentImportOutcome.Failed;
    }

    /// <summary>
    /// Blocks the first SaveDocument until the test signals it to proceed, so
    /// cancellation can be exercised deterministically after exactly one file
    /// has been processed. This is a test-only synchronization seam.
    /// </summary>
    private sealed class SignalingImportService : IDroppedFileImportService
    {
        private readonly IDroppedFileImportService _inner;
        private int _calls;
        public ManualResetEventSlim EnteredFirst { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);
        public SignalingImportService(IDroppedFileImportService inner) => _inner = inner;
        public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects) => _inner.GetAvailableSubjects(fallbackSubjects);
        public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes) => _inner.GetAvailableTypes(fallbackTypes);
        public StudyDocument BuildDocumentFromPath(string filePath) => _inner.BuildDocumentFromPath(filePath);
        public StudyDocument? FindExistingByFilePath(string filePath) => _inner.FindExistingByFilePath(filePath);
        public IReadOnlyList<StudyDocument> FindExistingByName(string name) => _inner.FindExistingByName(name);
        public DocumentImportOutcome SaveDocument(StudyDocument document)
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                EnteredFirst.Set();
                Release.Wait();
            }
            return _inner.SaveDocument(document);
        }
    }
}
