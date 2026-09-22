using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using StudyDocumentManager.Tests.TestDoubles;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class ImportInboxRegressionTests
{
    [Fact]
    public async Task BatchImport_PersistsOneInboxRow_ThenUpdatesThatRowToOutcome()
    {
        var inbox = new TrackingInboxRepository();
        var importer = new ImportedDocumentService();
        var model = new BatchImportModel(
            new RecordingDialogService(),
            new StubFileDialogService(),
            new StubNavigationService(),
            new KeyLocalizationService(),
            importer,
            inbox);
        model.DefaultSubject = "Physics";
        model.Files.Add(new FileImportItem
        {
            FileName = "notes",
            FilePath = "notes.pdf",
            FileType = "PDF",
            IsSelected = true
        });
        model.Files.Add(new FileImportItem
        {
            FileName = "notes-copy",
            FilePath = "notes.pdf",
            FileType = "PDF",
            IsSelected = true
        });

        await model.ImportCommand.ExecuteAsync(null);

        var item = Assert.Single(inbox.Items);
        Assert.Equal(1, inbox.AddCount);
        Assert.Equal(1, inbox.UpdateCount);
        Assert.Equal(
            ["Add:1:Pending", "GetById:1:Pending", "Update:1:Processed"],
            inbox.Operations);
        Assert.Equal(ImportInboxState.Pending, inbox.SnapshotsAtLookup[1].State);
        Assert.Equal(ImportInboxState.Processed, item.State);
        Assert.Equal(77, item.DocumentId);
    }

    [Fact]
    public async Task BatchImport_MissingSubjectOrType_ClassifiesMissingMetadata()
    {
        var inbox = new TrackingInboxRepository();
        var model = new BatchImportModel(
            new RecordingDialogService(),
            new StubFileDialogService(),
            new StubNavigationService(),
            new KeyLocalizationService(),
            new ImportedDocumentService(),
            inbox);
        model.DefaultSubject = "";
        model.Files.Add(new FileImportItem
        {
            FileName = "notes",
            FilePath = "notes.pdf",
            FileType = "",
            IsSelected = true
        });

        await model.ImportCommand.ExecuteAsync(null);

        var item = Assert.Single(inbox.Items);
        Assert.Equal(ImportInboxState.MissingMetadata, item.State);
    }

    [Fact]
    public async Task BatchImport_SkippedDuplicate_StoresExistingDocumentIdentity()
    {
        var inbox = new TrackingInboxRepository();
        var model = new BatchImportModel(
            new RecordingDialogService(),
            new StubFileDialogService(),
            new StubNavigationService(),
            new KeyLocalizationService(),
            new DuplicateIdentityImportService(),
            inbox);
        model.DefaultSubject = "Physics";
        model.Files.Add(new FileImportItem
        {
            FileName = "notes",
            FilePath = "same.pdf",
            FileType = "PDF",
            IsSelected = true
        });

        await model.ImportCommand.ExecuteAsync(null);

        var item = Assert.Single(inbox.Items);
        Assert.Equal(ImportInboxState.Held, item.State);
        Assert.Equal("99:Existing Homework", item.DuplicateCandidate);
        Assert.NotEqual("same.pdf", item.DuplicateCandidate);
    }

    [Fact]
    public async Task BatchImport_AmbiguousName_MarksItemAmbiguousAndListsCandidates()
    {
        var inbox = new TrackingInboxRepository();
        var model = new BatchImportModel(
            new RecordingDialogService(),
            new StubFileDialogService(),
            new StubNavigationService(),
            new KeyLocalizationService(),
            new AmbiguousNameImportService(),
            inbox);
        model.DefaultSubject = "Physics";
        model.Files.Add(new FileImportItem
        {
            FileName = "Alpha",
            FilePath = "alpha.pdf",
            FileType = "PDF",
            IsSelected = true
        });

        await model.ImportCommand.ExecuteAsync(null);

        var item = Assert.Single(inbox.Items);
        Assert.Equal(ImportInboxState.Ambiguous, item.State);
        Assert.Contains("5:Alpha", item.DuplicateCandidate);
        Assert.Contains("8:Alpha", item.DuplicateCandidate);
        Assert.Equal(77, item.DocumentId);
    }

    [Fact]
    public async Task BatchImport_SingleNameMatch_IsNotAmbiguous()
    {
        var inbox = new TrackingInboxRepository();
        var model = new BatchImportModel(
            new RecordingDialogService(),
            new StubFileDialogService(),
            new StubNavigationService(),
            new KeyLocalizationService(),
            new SingleNameMatchImportService(),
            inbox);
        model.DefaultSubject = "Physics";
        model.Files.Add(new FileImportItem
        {
            FileName = "Alpha",
            FilePath = "alpha.pdf",
            FileType = "PDF",
            IsSelected = true
        });

        await model.ImportCommand.ExecuteAsync(null);

        var item = Assert.Single(inbox.Items);
        Assert.Equal(ImportInboxState.Processed, item.State);
        Assert.True(string.IsNullOrEmpty(item.DuplicateCandidate));
    }

    [Fact]
    public async Task BatchImport_PersistFailure_MarksFileFailed()
    {
        var inbox = new ThrowingInboxRepository();
        var model = new BatchImportModel(
            new RecordingDialogService(),
            new StubFileDialogService(),
            new StubNavigationService(),
            new KeyLocalizationService(),
            new ImportedDocumentService(),
            inbox);
        model.DefaultSubject = "Physics";
        model.Files.Add(new FileImportItem
        {
            FileName = "notes",
            FilePath = "notes.pdf",
            FileType = "PDF",
            IsSelected = true
        });

        await model.ImportCommand.ExecuteAsync(null);

        var file = Assert.Single(model.Files);
        Assert.True(file.IsFailed);
        Assert.Equal(BatchImportFailureCode.DatabaseError, file.FailureCode);
    }

    [Fact]
    public void ImportInboxModel_LanguageChangedRefreshesLabelsAndItems()
    {
        var localization = new SwitchingInboxLocalizationService();
        var repository = new TrackingInboxRepository();
        repository.Items.Add(new ImportInboxItem
        {
            Id = 1,
            SourcePath = "held.pdf",
            DisplayName = "held",
            State = ImportInboxState.Held
        });
        var model = new ImportInboxModel(
            repository,
            new StubProcessLauncherService(),
            new StubNavigationService(),
            localization);
        var previousItems = model.Items;

        localization.SwitchToEnglish();

        Assert.NotSame(previousItems, model.Items);
        var item = Assert.Single(model.Items);
        Assert.Equal("Held (en)", item.StateLabel);
        Assert.Equal("Held (en)", model.StateOptions.Single(option => option.State == ImportInboxState.Held).Label);
        Assert.Equal(2, repository.GetAllCount);
    }

    private sealed class TrackingInboxRepository : IImportInboxRepository
    {
        public List<ImportInboxItem> Items { get; } = [];
        public int AddCount { get; private set; }
        public int UpdateCount { get; private set; }
        public int GetAllCount { get; private set; }
        public List<string> Operations { get; } = [];
        public Dictionary<int, ImportInboxItem> SnapshotsAtLookup { get; } = [];

        public IReadOnlyList<ImportInboxItem> GetAll(bool includeProcessed = false)
        {
            GetAllCount++;
            return includeProcessed ? Items.ToList() : Items.Where(item => item.State != ImportInboxState.Processed).ToList();
        }

        public ImportInboxItem? GetById(int id)
        {
            var item = Items.FirstOrDefault(candidate => candidate.Id == id);
            if (item is null)
                return null;

            Operations.Add($"GetById:{id}:{item.State}");
            SnapshotsAtLookup[id] = Clone(item);
            return Clone(item);
        }

        public int Add(ImportInboxItem item)
        {
            AddCount++;
            if (Items.Any(candidate => string.Equals(candidate.SourcePath, item.SourcePath, StringComparison.OrdinalIgnoreCase)))
                return Items.First(candidate => string.Equals(candidate.SourcePath, item.SourcePath, StringComparison.OrdinalIgnoreCase)).Id;

            item.Id = Items.Count + 1;
            Items.Add(Clone(item));
            Operations.Add($"Add:{item.Id}:{item.State}");
            return item.Id;
        }

        public bool Update(ImportInboxItem item)
        {
            UpdateCount++;
            var existing = Items.FirstOrDefault(candidate => candidate.Id == item.Id);
            if (existing is null)
                return false;

            if (existing.State != ImportInboxState.Pending)
                return false;
            Operations.Add($"Update:{item.Id}:{item.State}");
            Items[Items.IndexOf(existing)] = Clone(item);
            return true;
        }

        private static ImportInboxItem Clone(ImportInboxItem item) => new()
        {
            Id = item.Id,
            DocumentId = item.DocumentId,
            SourcePath = item.SourcePath,
            DisplayName = item.DisplayName,
            FailureCode = item.FailureCode,
            DuplicateCandidate = item.DuplicateCandidate,
            Subject = item.Subject,
            Type = item.Type,
            State = item.State,
            StateLabel = item.StateLabel,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };

        public bool UpdateState(int id, ImportInboxState state, string? failureCode = null)
        {
            var item = GetById(id);
            if (item is null)
                return false;
            item.State = state;
            item.FailureCode = failureCode;
            return true;
        }

        public int BulkEditMetadata(IReadOnlyList<int> documentIds, BulkEditChanges changes) => documentIds.Count;
    }

    private sealed class ImportedDocumentService : IDroppedFileImportService
    {
        public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects) => fallbackSubjects.ToList();
        public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes) => fallbackTypes.ToList();
        public StudyDocument BuildDocumentFromPath(string filePath) => new() { Name = Path.GetFileNameWithoutExtension(filePath), FilePath = filePath };

        public DocumentImportOutcome SaveDocument(StudyDocument document)
        {
            document.Id = 77;
            return DocumentImportOutcome.Imported;
        }
    }

    private sealed class DuplicateIdentityImportService : IDroppedFileImportService
    {
        public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects) => fallbackSubjects.ToList();
        public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes) => fallbackTypes.ToList();
        public StudyDocument BuildDocumentFromPath(string filePath) => new() { Name = Path.GetFileNameWithoutExtension(filePath), FilePath = filePath };

        public DocumentImportOutcome SaveDocument(StudyDocument document) => DocumentImportOutcome.SkippedDuplicate;

        public StudyDocument? FindExistingByFilePath(string filePath) => new() { Id = 99, Name = "Existing Homework" };
    }

    private sealed class AmbiguousNameImportService : IDroppedFileImportService
    {
        public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects) => fallbackSubjects.ToList();
        public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes) => fallbackTypes.ToList();
        public StudyDocument BuildDocumentFromPath(string filePath) => new() { Name = Path.GetFileNameWithoutExtension(filePath), FilePath = filePath };

        public DocumentImportOutcome SaveDocument(StudyDocument document)
        {
            document.Id = 77;
            return DocumentImportOutcome.Imported;
        }

        public IReadOnlyList<StudyDocument> FindExistingByName(string name) => new List<StudyDocument>
        {
            new() { Id = 5, Name = "Alpha" },
            new() { Id = 8, Name = "Alpha" }
        };
    }

    private sealed class SingleNameMatchImportService : IDroppedFileImportService
    {
        public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects) => fallbackSubjects.ToList();
        public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes) => fallbackTypes.ToList();
        public StudyDocument BuildDocumentFromPath(string filePath) => new() { Name = Path.GetFileNameWithoutExtension(filePath), FilePath = filePath };

        public DocumentImportOutcome SaveDocument(StudyDocument document)
        {
            document.Id = 77;
            return DocumentImportOutcome.Imported;
        }

        public IReadOnlyList<StudyDocument> FindExistingByName(string name) => new List<StudyDocument>
        {
            new() { Id = 5, Name = "Alpha" }
        };
    }

    private sealed class ThrowingInboxRepository : IImportInboxRepository
    {
        public List<ImportInboxItem> Items { get; } = [];
        public IReadOnlyList<ImportInboxItem> GetAll(bool includeProcessed = false) => [];
        public ImportInboxItem? GetById(int id) => null;
        public int Add(ImportInboxItem item) => throw new Microsoft.Data.Sqlite.SqliteException("db", 1);
        public bool Update(ImportInboxItem item) => true;
        public bool UpdateState(int id, ImportInboxState state, string? failureCode = null) => true;
        public int BulkEditMetadata(IReadOnlyList<int> documentIds, BulkEditChanges changes) => documentIds.Count;
    }

    private sealed class SwitchingInboxLocalizationService : ILocalizationService
    {
        private bool _english;

        public string this[string key] => _english
            ? key switch
            {
                "ImportInbox_All" => "All (en)",
                "ImportInbox_State_Held" => "Held (en)",
                "ImportInbox_Status" => "Items: {0} (en)",
                _ => key
            }
            : key switch
            {
                "ImportInbox_All" => "すべて",
                "ImportInbox_State_Held" => "保留",
                "ImportInbox_Status" => "件数: {0}",
                _ => key
            };

        public SupportedLanguage CurrentLanguage => _english ? SupportedLanguage.English : SupportedLanguage.Japanese;
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [SupportedLanguage.Japanese, SupportedLanguage.English];
        public event EventHandler? LanguageChanged;

        public void SetLanguage(SupportedLanguage language)
        {
            _english = language == SupportedLanguage.English;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SwitchToEnglish() => SetLanguage(SupportedLanguage.English);
    }
}
