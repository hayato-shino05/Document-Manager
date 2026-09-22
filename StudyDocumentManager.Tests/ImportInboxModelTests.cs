using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using System.Linq;
using StudyDocumentManager.Models;
using StudyDocumentManager.Core;
using StudyDocumentManager.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class ImportInboxModelTests : IDisposable
{
    private readonly string _source = Path.Combine(Path.GetTempPath(), $"inbox-{Guid.NewGuid():N}.pdf");
    private readonly ModelInboxRepository _repository = new();

    public ImportInboxModelTests() => File.WriteAllText(_source, "test");
    public void Dispose() { if (File.Exists(_source)) File.Delete(_source); }

    [Fact]
    public void RetrySuccessPersistsDocumentMetadataAndId()
    {
        var item = new ImportInboxItem { Id = 1, SourcePath = _source, DisplayName = "test", State = ImportInboxState.Failed };
        _repository.Items.Add(item);
        var model = new ImportInboxModel(_repository, new ModelLauncher(), new ModelNavigation(), new ModelLocalization(), new ModelImportService());
        model.SelectedItem = item;

        model.RetrySelectedCommand.Execute(null);

        Assert.Equal(17, _repository.Items[0].DocumentId);
        Assert.Equal("Physics", _repository.Items[0].Subject);
        Assert.Equal("PDF", _repository.Items[0].Type);
        Assert.Equal(ImportInboxState.Processed, _repository.Items[0].State);
    }

    [Fact]
    public void FailureLabel_IsLocalizedFromFailureCode_AndEmptyWhenNone()
    {
        var failed = new ImportInboxItem { Id = 1, SourcePath = _source, DisplayName = "t", State = ImportInboxState.Failed, FailureCode = "FileError" };
        var ok = new ImportInboxItem { Id = 2, SourcePath = _source, DisplayName = "t2", State = ImportInboxState.Held };
        _repository.Items.Add(failed);
        _repository.Items.Add(ok);
        var model = new ImportInboxModel(_repository, new ModelLauncher(), new ModelNavigation(), new ModelLocalization(), new ModelImportService());

        var failedItem = model.Items.First(i => i.Id == 1);
        var okItem = model.Items.First(i => i.Id == 2);
        Assert.Equal("BatchImport_FileError", failedItem.FailureLabel);
        Assert.Equal(string.Empty, okItem.FailureLabel);
    }

    [Fact]
    public void RetryDuplicateKeepsExistingCandidate()
    {
        var item = new ImportInboxItem { Id = 1, SourcePath = _source, DisplayName = "test", State = ImportInboxState.Failed, DuplicateCandidate = "99:Existing Homework" };
        _repository.Items.Add(item);
        var model = new ImportInboxModel(_repository, new ModelLauncher(), new ModelNavigation(), new ModelLocalization(), new DuplicateImportService());
        model.SelectedItem = item;

        model.RetrySelectedCommand.Execute(null);

        Assert.Equal(ImportInboxState.Held, _repository.Items[0].State);
        Assert.Equal("99:Existing Homework", _repository.Items[0].DuplicateCandidate);
    }

    [Fact]
    public void RetryDatabaseErrorMapsToTypedFailure()
    {
        var item = new ImportInboxItem { Id = 1, SourcePath = _source, DisplayName = "test", State = ImportInboxState.Held };
        _repository.Items.Add(item);
        var model = new ImportInboxModel(_repository, new ModelLauncher(), new ModelNavigation(), new ModelLocalization(), new ThrowingImportService());
        model.SelectedItem = item;

        model.RetrySelectedCommand.Execute(null);

        Assert.Equal(ImportInboxState.Failed, _repository.Items[0].State);
        Assert.Equal("DatabaseError", _repository.Items[0].FailureCode);
    }

    [Fact]
    public void SelectionChanged_UpdatesSelectedItemsAndBulkEditEligibility()
    {
        var ok = new ImportInboxItem { Id = 1, SourcePath = _source, DisplayName = "ok", State = ImportInboxState.Held, DocumentId = 10 };
        _repository.Items.Add(ok);
        var model = new ImportInboxModel(_repository, new ModelLauncher(), new ModelNavigation(), new ModelLocalization(), new ModelImportService());
        model.BulkSubject = "Physics";

        Assert.False(model.CanBulkEdit);
        model.AddSelected(ok);
        Assert.Contains(ok, model.SelectedItems);
        Assert.True(model.CanBulkEdit);
        model.RemoveSelected(ok);
        Assert.DoesNotContain(ok, model.SelectedItems);
        Assert.False(model.CanBulkEdit);
    }

    [Fact]
    public void MissingSource_SetsErrorMessage_AndClearsOnRefresh()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pdf");
        var item = new ImportInboxItem { Id = 1, SourcePath = missing, DisplayName = "test", State = ImportInboxState.Failed };
        _repository.Items.Add(item);
        var model = new ImportInboxModel(_repository, new ModelLauncher(), new ModelNavigation(), new ModelLocalization(), new ModelImportService());
        model.SelectedItem = item;

        model.RetrySelectedCommand.Execute(null);

        Assert.Equal("ImportInbox_SourceMissing", model.ErrorMessage);
        Assert.NotEqual("ImportInbox_SourceMissing", model.StatusText);

        model.RefreshCommand.Execute(null);
        Assert.Equal(string.Empty, model.ErrorMessage);
    }

    [Fact]
    public void Refresh_UpdatesStatusTextWithLoadedItemCount()
    {
        _repository.Items.Add(new ImportInboxItem { Id = 1, SourcePath = _source, DisplayName = "t", State = ImportInboxState.Held });
        var model = new ImportInboxModel(_repository, new ModelLauncher(), new ModelNavigation(), new ModelLocalization(), new ModelImportService());
        Assert.Equal("1", model.StatusText);

        _repository.Items.Add(new ImportInboxItem { Id = 2, SourcePath = _source, DisplayName = "t2", State = ImportInboxState.Pending });
        model.RefreshCommand.Execute(null);
        Assert.Equal("2", model.StatusText);
    }

    [Fact]
    public void ApplyBulkMetadata_MixedSuccess_OnlySuccessfulBecomesProcessed()
    {
        var ok = new ImportInboxItem { Id = 1, SourcePath = _source, DisplayName = "ok", State = ImportInboxState.Held, DocumentId = 10, Subject = "Old" };
        var bad = new ImportInboxItem { Id = 2, SourcePath = _source, DisplayName = "bad", State = ImportInboxState.MissingMetadata, DocumentId = 20, Subject = "Old" };
        _repository.Items.Add(ok);
        _repository.Items.Add(bad);
        var bulk = new MixedBulkRepository { FailingIds = new() { 20 } };
        var model = new ImportInboxModel(_repository, new ModelLauncher(), new ModelNavigation(), new ModelLocalization(), new ModelImportService(), bulk);
        model.AddSelected(ok);
        model.AddSelected(bad);
        model.BulkSubject = "Physics";

        model.ApplyBulkMetadataCommand.Execute(null);

        Assert.Equal(ImportInboxState.Processed, _repository.Items[0].State);
        Assert.Equal("Physics", _repository.Items[0].Subject);
        Assert.Equal(ImportInboxState.MissingMetadata, _repository.Items[1].State);
        Assert.Equal("Old", _repository.Items[1].Subject);
    }

    private sealed class ModelInboxRepository : IImportInboxRepository
    {
        public List<ImportInboxItem> Items { get; } = [];
        public IReadOnlyList<ImportInboxItem> GetAll(bool includeProcessed = false) => includeProcessed ? Items : Items.Where(i => i.State != ImportInboxState.Processed).ToList();
        public ImportInboxItem? GetById(int id) => Items.FirstOrDefault(i => i.Id == id);
        public int Add(ImportInboxItem item) { item.Id = Items.Count + 1; Items.Add(item); return item.Id; }
        public bool Update(ImportInboxItem item) => true;
        public bool UpdateState(int id, ImportInboxState state, string? failureCode = null) { var item = GetById(id)!; item.State = state; item.FailureCode = failureCode; return true; }
        public int BulkEditMetadata(IReadOnlyList<int> documentIds, BulkEditChanges changes) => documentIds.Count;
    }

    private sealed class ModelImportService : IDroppedFileImportService
    {
        public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects) => [];
        public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes) => [];
        public DocumentImportOutcome SaveDocument(StudyDocument document) { document.Id = 17; return DocumentImportOutcome.Imported; }
        public StudyDocument BuildDocumentFromPath(string path) => new() { Name = "test", FilePath = path, Subject = "Physics", Type = "PDF" };
    }

    private sealed class DuplicateImportService : IDroppedFileImportService
    {
        public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects) => [];
        public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes) => [];
        public DocumentImportOutcome SaveDocument(StudyDocument document) => DocumentImportOutcome.SkippedDuplicate;
        public StudyDocument BuildDocumentFromPath(string path) => new() { Name = "test", FilePath = path, Subject = "Physics", Type = "PDF" };
        public StudyDocument? FindExistingByFilePath(string filePath) => new() { Id = 99, Name = "Existing Homework" };
    }

    private sealed class ThrowingImportService : IDroppedFileImportService
    {
        public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects) => [];
        public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes) => [];
        public DocumentImportOutcome SaveDocument(StudyDocument document) => throw new SqliteException("db error", 1);
        public StudyDocument BuildDocumentFromPath(string path) => new() { Name = "test", FilePath = path, Subject = "Physics", Type = "PDF" };
    }

    private sealed class MixedBulkRepository : IBulkOperationRepository
    {
        public HashSet<int> FailingIds { get; init; } = new();
        public int BulkSoftDelete(List<int> ids) => ids.Count;
        public int BulkUpdateSubject(List<int> ids, string subject) => ids.Count;
        public int BulkToggleImportant(List<int> ids, bool important) => ids.Count;
        public BulkEditOutcome BulkEditMetadata(IReadOnlyList<int> documentIds, BulkEditChanges changes)
        {
            var items = documentIds.Select(id => new BulkItemResult(id, !FailingIds.Contains(id))).ToList();
            return new BulkEditOutcome { Requested = documentIds.Count, Succeeded = items.Count(i => i.Success), Items = items };
        }
    }

    private sealed class ModelLocalization : ILocalizationService
    {
        public string this[string key] => key == "ImportInbox_Status" ? "{0}" : key;
        public SupportedLanguage CurrentLanguage => SupportedLanguage.English;
        public IReadOnlyList<SupportedLanguage> AvailableLanguages => [SupportedLanguage.English];
        public void SetLanguage(SupportedLanguage language) { }
        public event EventHandler? LanguageChanged { add { } remove { } }
    }
    private sealed class ModelLauncher : IProcessLauncherService { public void OpenFile(string p) { } public void RevealInExplorer(string p) { } public void OpenUrl(string u) { } }
    private sealed class ModelNavigation : INavigationService { public bool CanGoBack => true; public void NavigateTo(string v) { } public void NavigateTo(string v, object? p) { } public void GoBack() { } }
}
