using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public class ScanRecoveryTests
{
    [Fact]
    public async Task CheckIntegrityAsync_GetAllThrows_ResetsStateAndCanRetry()
    {
        var repository = new ThrowingDocumentRepository
        {
            Documents = [new StudyDocument { Name = "Missing", FilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) }]
        };
        var dialogs = new RecordingDialogService();
        var model = new FileIntegrityCheckModel(repository, new FileIntegrityRepositoryStub(), dialogs, new FileDialogServiceStub(), new LocalizationServiceStub());

        await model.CheckIntegrityCommand.ExecuteAsync(null);

        Assert.False(model.IsChecking);
        Assert.Empty(model.Results);
        Assert.Equal(0, model.TotalChecked);
        Assert.Equal(0, model.MissingCount);
        Assert.Single(dialogs.Errors);

        repository.ThrowOnGetAll = false;
        await model.CheckIntegrityCommand.ExecuteAsync(null);

        Assert.False(model.IsChecking);
        Assert.Single(model.Results);
        Assert.Equal(1, model.TotalChecked);
        Assert.Equal(1, model.MissingCount);
    }

    [Fact]
    public async Task ScanDuplicatesAsync_GetAllThrows_ResetsStateAndCanRetry()
    {
        var repository = new ThrowingDocumentRepository
        {
            Documents =
            [
                new StudyDocument { Name = "Duplicate" },
                new StudyDocument { Name = "Duplicate" }
            ]
        };
        var dialogs = new RecordingDialogService();
        var model = new DuplicateDetectionModel(repository, dialogs, new LocalizationServiceStub());

        await model.ScanDuplicatesCommand.ExecuteAsync(null);

        Assert.False(model.IsScanning);
        Assert.Empty(model.DuplicateGroups);
        Assert.Equal(0, model.TotalGroups);
        Assert.Single(dialogs.Errors);

        repository.ThrowOnGetAll = false;
        await model.ScanDuplicatesCommand.ExecuteAsync(null);

        Assert.False(model.IsScanning);
        Assert.Single(model.DuplicateGroups);
        Assert.Equal(1, model.TotalGroups);
    }

    [Fact]
    public async Task CheckIntegrityAsync_SuccessDialogThrows_ResetsCheckingState()
    {
        var repository = new ThrowingDocumentRepository { ThrowOnGetAll = false };
        var dialogs = new RecordingDialogService { ThrowOnMessage = true };
        var model = new FileIntegrityCheckModel(repository, new FileIntegrityRepositoryStub(), dialogs, new FileDialogServiceStub(), new LocalizationServiceStub());

        await model.CheckIntegrityCommand.ExecuteAsync(null);

        Assert.False(model.IsChecking);
        Assert.Empty(model.Results);
        Assert.Equal(0, model.TotalChecked);
        Assert.Equal(0, model.MissingCount);
        Assert.Single(dialogs.Errors);
    }

    [Fact]
    public async Task ScanDuplicatesAsync_SuccessDialogThrows_ResetsScanningState()
    {
        var repository = new ThrowingDocumentRepository { ThrowOnGetAll = false };
        var dialogs = new RecordingDialogService { ThrowOnMessage = true };
        var model = new DuplicateDetectionModel(repository, dialogs, new LocalizationServiceStub());

        await model.ScanDuplicatesCommand.ExecuteAsync(null);

        Assert.False(model.IsScanning);
        Assert.Empty(model.DuplicateGroups);
        Assert.Equal(0, model.TotalGroups);
        Assert.Single(dialogs.Errors);
    }

    [Fact]
    public async Task CheckIntegrityAsync_ErrorDialogThrows_StillResetsCheckingState()
    {
        var dialogs = new RecordingDialogService { ThrowOnError = true };
        var model = new FileIntegrityCheckModel(new ThrowingDocumentRepository(), new FileIntegrityRepositoryStub(), dialogs, new FileDialogServiceStub(), new LocalizationServiceStub());

        await Assert.ThrowsAsync<InvalidOperationException>(() => model.CheckIntegrityCommand.ExecuteAsync(null));

        Assert.False(model.IsChecking);
        Assert.Empty(model.Results);
        Assert.Equal(0, model.TotalChecked);
        Assert.Equal(0, model.MissingCount);
    }

    [Fact]
    public async Task ScanDuplicatesAsync_ErrorDialogThrows_StillResetsScanningState()
    {
        var dialogs = new RecordingDialogService { ThrowOnError = true };
        var model = new DuplicateDetectionModel(new ThrowingDocumentRepository(), dialogs, new LocalizationServiceStub());

        await Assert.ThrowsAsync<InvalidOperationException>(() => model.ScanDuplicatesCommand.ExecuteAsync(null));

        Assert.False(model.IsScanning);
        Assert.Empty(model.DuplicateGroups);
        Assert.Equal(0, model.TotalGroups);
    }

    private sealed class ThrowingDocumentRepository : IDocumentRepository
    {
        public bool ThrowOnGetAll { get; set; } = true;
        public List<StudyDocument> Documents { get; set; } = [];

        public List<StudyDocument> GetAll()
            => ThrowOnGetAll ? throw new InvalidOperationException() : Documents;

        public StudyDocument? GetById(int id) => throw new NotImplementedException();
        public List<StudyDocument> Search(string keyword) => throw new NotImplementedException();
        public List<StudyDocument> Filter(string subject, string type) => throw new NotImplementedException();
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => throw new NotImplementedException();
        public bool Add(StudyDocument document) => throw new NotImplementedException();
        public bool AddWithCatalogs(StudyDocument document) => throw new NotImplementedException();
        public bool Update(StudyDocument document) => throw new NotImplementedException();
        public bool Delete(int id) => throw new NotImplementedException();
        public List<string> GetDistinctSubjects() => throw new NotImplementedException();
        public List<string> GetDistinctTypes() => throw new NotImplementedException();
        public List<string> GetDistinctTags() => throw new NotImplementedException();
        public List<StudyDocument> GetUpcomingDeadlines(int days) => throw new NotImplementedException();
        public List<StudyDocument> GetOverdueDocuments() => throw new NotImplementedException();
        public void EnsureSubjectExists(string subject) => throw new NotImplementedException();
        public void EnsureTypeExists(string type) => throw new NotImplementedException();
    }

    private sealed class FileIntegrityRepositoryStub : IFileIntegrityRepository
    {
        public string DatabasePath => string.Empty;
        public bool UpdateDocumentPath(int id, string newPath) => false;
        public bool ClearDocumentPath(int id) => false;
        public bool BackupDatabase(string destPath, bool overwrite) => false;
        public bool CanRestoreDatabase(string sourcePath) => false;
        public bool RestoreDatabase(string sourcePath) => false;
    }

    private sealed class FileDialogServiceStub : IFileDialogService
    {
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
    }

    private sealed class RecordingDialogService : IDialogService
    {
        public bool ThrowOnMessage { get; set; }
        public bool ThrowOnError { get; set; }
        public List<string> Errors { get; } = [];

        public Task ShowMessageAsync(string title, string message)
            => ThrowOnMessage ? throw new InvalidOperationException() : Task.CompletedTask;

        public Task ShowErrorAsync(string title, string message)
        {
            Errors.Add(message);
            return ThrowOnError ? throw new InvalidOperationException() : Task.CompletedTask;
        }

        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(false);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(false);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
    }

    private sealed class LocalizationServiceStub : ILocalizationService
    {
        public string this[string key] => key;
        public SupportedLanguage CurrentLanguage => SupportedLanguage.English;
        public IReadOnlyList<SupportedLanguage> AvailableLanguages => [SupportedLanguage.English];
        public event EventHandler? LanguageChanged { add { } remove { } }
        public void SetLanguage(SupportedLanguage language) { }
    }
}
