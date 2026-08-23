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
    public async Task CheckIntegrityAsync_CancellationPreservesPartialResults()
    {
        var repository = new ThrowingDocumentRepository
        {
            ThrowOnGetAll = false,
            BlockGetAll = true,
            Documents = [new StudyDocument { Name = "Missing", FilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) }]
        };
        var model = new FileIntegrityCheckModel(repository, new FileIntegrityRepositoryStub(), new RecordingDialogService(), new FileDialogServiceStub(), new LocalizationServiceStub());

        var checkTask = model.CheckIntegrityCommand.ExecuteAsync(null);
        Assert.True(repository.GetAllStarted.Wait(TimeSpan.FromSeconds(5)));
        model.CancelCheckCommand.Execute(null);
        repository.ReleaseGetAll.Set();
        await checkTask;

        Assert.True(model.IsCheckCancelled);
        Assert.Equal(0, model.TotalChecked);
        Assert.Empty(model.Results);
        Assert.False(model.IsChecking);
    }

    [Fact]
    public async Task RetryMissingAsync_RechecksOnlyExistingMissingResults()
    {
        var document = new StudyDocument
        {
            Name = "Missing",
            FilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        };
        var repository = new ThrowingDocumentRepository { ThrowOnGetAll = false, Documents = [document] };
        var model = new FileIntegrityCheckModel(repository, new FileIntegrityRepositoryStub(), new RecordingDialogService(), new FileDialogServiceStub(), new LocalizationServiceStub());

        await model.CheckIntegrityCommand.ExecuteAsync(null);
        Assert.Single(model.Results);

        var existingPath = Path.GetTempFileName();
        try
        {
            document.FilePath = existingPath;
            await model.RetryMissingCommand.ExecuteAsync(null);

            Assert.Empty(model.Results);
            Assert.Equal(0, model.MissingCount);
            Assert.Equal(1, model.TotalChecked);
        }
        finally
        {
            File.Delete(existingPath);
        }
    }

    [Fact]
    public async Task RetryMissingAsync_CancelledBeforeWorkerStarts_PreservesResultCountConsistency()
    {
        var documents = Enumerable.Range(1, 100_000)
            .Select(id => new StudyDocument { Id = id, FilePath = $"missing-{id}.pdf" })
            .ToList();
        var model = new FileIntegrityCheckModel(
            new ThrowingDocumentRepository { ThrowOnGetAll = false },
            new FileIntegrityRepositoryStub(),
            new RecordingDialogService(),
            new FileDialogServiceStub(),
            new LocalizationServiceStub());
        foreach (var document in documents)
            model.Results.Add(new IntegrityResult { Document = document, FilePath = document.FilePath });
        model.MissingCount = model.Results.Count;

        var retryTask = model.RetryMissingCommand.ExecuteAsync(null);
        model.CancelCheckCommand.Execute(null);
        await retryTask;

        Assert.False(model.IsChecking);
        Assert.True(model.IsCheckCancelled);
        Assert.Equal(model.Results.Count, model.MissingCount);
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


    [Fact]
    public async Task DeleteDuplicateAsync_FailedOrCancelledDelete_PreservesGroupsAndReportsFailure()
    {
        var repository = new ThrowingDocumentRepository
        {
            ThrowOnGetAll = false,
            DeleteResult = false,
            Documents = [new StudyDocument { Id = 1, Name = "Duplicate" }, new StudyDocument { Id = 2, Name = "Duplicate" }]
        };
        var dialogs = new RecordingDialogService { ConfirmResult = true };
        var model = new DuplicateDetectionModel(repository, dialogs, new LocalizationServiceStub());
        await model.ScanDuplicatesCommand.ExecuteAsync(null);

        await model.DeleteDuplicateCommand.ExecuteAsync(model.DuplicateGroups[0].Documents[0]);

        Assert.Single(model.DuplicateGroups);
        Assert.Single(dialogs.Errors);

        dialogs.ThrowCancellation = true;
        await model.DeleteDuplicateCommand.ExecuteAsync(model.DuplicateGroups[0].Documents[0]);

        Assert.Single(model.DuplicateGroups);
        Assert.Single(dialogs.Errors);
    }

    [Fact]
    public async Task IntegrityMutations_FailedOutcomesPreserveItemsAndReportFailure()
    {
        var repository = new ThrowingDocumentRepository { ThrowOnGetAll = false, DeleteResult = false };
        var integrityRepository = new FileIntegrityRepositoryStub();
        var dialogs = new RecordingDialogService { ConfirmResult = true };
        var fileDialogs = new FileDialogServiceStub { SelectedFile = "C:\\replacement.pdf" };
        var model = new FileIntegrityCheckModel(repository, integrityRepository, dialogs, fileDialogs, new LocalizationServiceStub());
        var item = new IntegrityResult { Document = new StudyDocument { Id = 1, Name = "Missing" } };
        model.Results.Add(item);
        model.MissingCount = 1;

        await model.SelectNewFileCommand.ExecuteAsync(item);
        await model.ClearFilePathCommand.ExecuteAsync(item);
        await model.DeleteDocumentCommand.ExecuteAsync(item);

        Assert.Single(model.Results);
        Assert.Equal(3, dialogs.Errors.Count);

        dialogs.ThrowCancellation = true;
        await model.RemoveMissingCommand.ExecuteAsync(null);

        Assert.Single(model.Results);
        Assert.Equal(3, dialogs.Errors.Count);
    }


    [Fact]
    public async Task IntegrityMutations_DuringScanAreIgnored()
    {
        var repository = new ThrowingDocumentRepository { ThrowOnGetAll = false };
        var dialogs = new RecordingDialogService { ConfirmResult = true };
        var fileDialogs = new FileDialogServiceStub { SelectedFile = "C:\\replacement.pdf" };
        var integrityRepository = new FileIntegrityRepositoryStub();
        var model = new FileIntegrityCheckModel(repository, integrityRepository, dialogs, fileDialogs, new LocalizationServiceStub());
        var item = new IntegrityResult { Document = new StudyDocument { Id = 1, Name = "Missing" } };
        model.Results.Add(item);
        model.MissingCount = 1;
        model.IsChecking = true;

        await model.SelectNewFileCommand.ExecuteAsync(item);
        await model.ClearFilePathCommand.ExecuteAsync(item);
        await model.DeleteDocumentCommand.ExecuteAsync(item);
        await model.RemoveMissingCommand.ExecuteAsync(null);

        Assert.Single(model.Results);
        Assert.Equal(1, model.MissingCount);
        Assert.Empty(dialogs.Errors);
    }

    [Fact]
    public async Task ClearFilePathAsync_ItemRemovedWhileConfirmationOpen_DoesNotMutateRepository()
    {
        var repository = new ThrowingDocumentRepository { ThrowOnGetAll = false };
        var integrityRepository = new FileIntegrityRepositoryStub { UpdateDocumentPathResult = true, ClearDocumentPathResult = true };
        var dialogs = new BlockingConfirmationDialogService { ConfirmResult = true };
        var model = new FileIntegrityCheckModel(repository, integrityRepository, dialogs, new FileDialogServiceStub(), new LocalizationServiceStub());
        var item = new IntegrityResult { Document = new StudyDocument { Id = 1, Name = "Missing" } };
        model.Results.Add(item);
        model.MissingCount = 1;

        var operation = model.ClearFilePathCommand.ExecuteAsync(item);
        Assert.True(dialogs.ConfirmationStarted.Wait(TimeSpan.FromSeconds(5)));
        model.Results.Remove(item);
        dialogs.ReleaseConfirmation.Set();
        await operation;

        Assert.Equal(0, integrityRepository.ClearDocumentPathCalls);
        Assert.Empty(model.Results);
        Assert.Equal(1, model.MissingCount);
    }

    [Fact]
    public async Task SelectNewFileAsync_ItemRemovedWhileFileDialogOpen_DoesNotMutateRepository()
    {
        var repository = new ThrowingDocumentRepository { ThrowOnGetAll = false };
        var integrityRepository = new FileIntegrityRepositoryStub { UpdateDocumentPathResult = true };
        var fileDialogs = new BlockingFileDialogService { SelectedFile = "C:\\replacement.pdf" };
        var model = new FileIntegrityCheckModel(repository, integrityRepository, new RecordingDialogService(), fileDialogs, new LocalizationServiceStub());
        var item = new IntegrityResult { Document = new StudyDocument { Id = 1, Name = "Missing" } };
        model.Results.Add(item);
        model.MissingCount = 1;

        var operation = model.SelectNewFileCommand.ExecuteAsync(item);
        Assert.True(fileDialogs.SelectionStarted.Wait(TimeSpan.FromSeconds(5)));
        model.Results.Remove(item);
        fileDialogs.ReleaseSelection.Set();
        await operation;

        Assert.Empty(model.Results);
        Assert.Equal(1, model.MissingCount);
    }

    [Fact]
    public async Task SelectNewFileAsync_ItemDocumentMutatedWhileFileDialogOpen_DoesNotMutateRepository()
    {
        var repository = new ThrowingDocumentRepository { ThrowOnGetAll = false };
        var integrityRepository = new FileIntegrityRepositoryStub { UpdateDocumentPathResult = true };
        var fileDialogs = new BlockingFileDialogService { SelectedFile = "C:\\replacement.pdf" };
        var model = new FileIntegrityCheckModel(repository, integrityRepository, new RecordingDialogService(), fileDialogs, new LocalizationServiceStub());
        var document = new StudyDocument { Id = 1, Name = "Missing", FilePath = "missing.pdf" };
        var item = new IntegrityResult { Document = document, FilePath = document.FilePath };
        model.Results.Add(item);
        model.MissingCount = 1;

        var operation = model.SelectNewFileCommand.ExecuteAsync(item);
        Assert.True(fileDialogs.SelectionStarted.Wait(TimeSpan.FromSeconds(5)));
        document.Id = 2;
        document.FilePath = "changed.pdf";
        fileDialogs.ReleaseSelection.Set();
        await operation;

        Assert.Equal(0, integrityRepository.UpdateDocumentPathCalls);
        Assert.Single(model.Results);
        Assert.Equal(1, model.MissingCount);
    }

    [Fact]
    public async Task ClearFilePathAsync_ItemDocumentMutatedWhileConfirmationOpen_DoesNotMutateRepository()
    {
        var repository = new ThrowingDocumentRepository { ThrowOnGetAll = false };
        var integrityRepository = new FileIntegrityRepositoryStub { ClearDocumentPathResult = true };
        var dialogs = new BlockingConfirmationDialogService { ConfirmResult = true };
        var model = new FileIntegrityCheckModel(repository, integrityRepository, dialogs, new FileDialogServiceStub(), new LocalizationServiceStub());
        var document = new StudyDocument { Id = 1, Name = "Missing", FilePath = "missing.pdf" };
        var item = new IntegrityResult { Document = document, FilePath = document.FilePath };
        model.Results.Add(item);
        model.MissingCount = 1;

        var operation = model.ClearFilePathCommand.ExecuteAsync(item);
        Assert.True(dialogs.ConfirmationStarted.Wait(TimeSpan.FromSeconds(5)));
        document.Id = 2;
        document.FilePath = "changed.pdf";
        dialogs.ReleaseConfirmation.Set();
        await operation;

        Assert.Equal(0, integrityRepository.ClearDocumentPathCalls);
        Assert.Single(model.Results);
        Assert.Equal(1, model.MissingCount);
    }

    [Fact]
    public async Task DeleteDocumentAsync_ItemDocumentMutatedWhileConfirmationOpen_DoesNotMutateRepository()
    {
        var repository = new ThrowingDocumentRepository { ThrowOnGetAll = false };
        var dialogs = new BlockingConfirmationDialogService { ConfirmResult = true };
        var model = new FileIntegrityCheckModel(repository, new FileIntegrityRepositoryStub(), dialogs, new FileDialogServiceStub(), new LocalizationServiceStub());
        var document = new StudyDocument { Id = 1, Name = "Missing", FilePath = "missing.pdf" };
        var item = new IntegrityResult { Document = document, FilePath = document.FilePath };
        model.Results.Add(item);
        model.MissingCount = 1;

        var operation = model.DeleteDocumentCommand.ExecuteAsync(item);
        Assert.True(dialogs.ConfirmationStarted.Wait(TimeSpan.FromSeconds(5)));
        document.Id = 2;
        document.FilePath = "changed.pdf";
        dialogs.ReleaseConfirmation.Set();
        await operation;

        Assert.Equal(0, repository.DeleteCalls);
        Assert.Single(model.Results);
        Assert.Equal(1, model.MissingCount);
    }

    [Fact]
    public async Task RemoveMissingAsync_ResultsChangedWhileConfirmationOpen_DoesNotMutateRepository()
    {
        var repository = new ThrowingDocumentRepository { ThrowOnGetAll = false };
        var dialogs = new BlockingConfirmationDialogService { ConfirmResult = true };
        var model = new FileIntegrityCheckModel(repository, new FileIntegrityRepositoryStub(), dialogs, new FileDialogServiceStub(), new LocalizationServiceStub());
        model.Results.Add(new IntegrityResult { Document = new StudyDocument { Id = 1, Name = "First" } });
        model.Results.Add(new IntegrityResult { Document = new StudyDocument { Id = 2, Name = "Second" } });
        model.MissingCount = 2;

        var operation = model.RemoveMissingCommand.ExecuteAsync(null);
        Assert.True(dialogs.ConfirmationStarted.Wait(TimeSpan.FromSeconds(5)));
        model.Results.RemoveAt(0);
        dialogs.ReleaseConfirmation.Set();
        await operation;

        Assert.Equal(0, repository.DeleteCalls);
        Assert.Single(model.Results);
        Assert.Equal(2, model.MissingCount);
    }

    [Fact]
    public async Task RemoveMissingAsync_ResultDocumentMutatedWhileConfirmationOpen_DoesNotMutateRepository()
    {
        var repository = new ThrowingDocumentRepository { ThrowOnGetAll = false };
        var dialogs = new BlockingConfirmationDialogService { ConfirmResult = true };
        var model = new FileIntegrityCheckModel(repository, new FileIntegrityRepositoryStub(), dialogs, new FileDialogServiceStub(), new LocalizationServiceStub());
        var document = new StudyDocument { Id = 1, Name = "First", FilePath = "missing-first.pdf" };
        model.Results.Add(new IntegrityResult { Document = document, FilePath = document.FilePath });
        model.MissingCount = 1;

        var operation = model.RemoveMissingCommand.ExecuteAsync(null);
        Assert.True(dialogs.ConfirmationStarted.Wait(TimeSpan.FromSeconds(5)));
        document.Id = 99;
        document.FilePath = "changed-after-confirmation.pdf";
        dialogs.ReleaseConfirmation.Set();
        await operation;

        Assert.Equal(0, repository.DeleteCalls);
        Assert.Single(model.Results);
        Assert.Equal(1, model.MissingCount);
    }

    [Fact]
    public async Task RemoveMissingAsync_ThrowsAfterPartialSuccess_PreservesRetryState()
    {
        var repository = new ThrowingDocumentRepository
        {
            ThrowOnGetAll = false,
            SuccessfulDeletesBeforeThrow = 1
        };
        var dialogs = new RecordingDialogService { ConfirmResult = true };
        var model = new FileIntegrityCheckModel(repository, new FileIntegrityRepositoryStub(), dialogs, new FileDialogServiceStub(), new LocalizationServiceStub());
        model.Results.Add(new IntegrityResult { Document = new StudyDocument { Id = 1, Name = "First" } });
        model.Results.Add(new IntegrityResult { Document = new StudyDocument { Id = 2, Name = "Second" } });
        model.MissingCount = 2;

        await model.RemoveMissingCommand.ExecuteAsync(null);

        Assert.Single(model.Results);
        Assert.Equal(2, model.Results[0].Document.Id);
        Assert.Equal(1, model.MissingCount);
        Assert.Single(dialogs.Errors);

        repository.SuccessfulDeletesBeforeThrow = int.MaxValue;
        await model.RemoveMissingCommand.ExecuteAsync(null);

        Assert.Empty(model.Results);
        Assert.Equal(0, model.MissingCount);
    }

    private sealed class ThrowingDocumentRepository : IDocumentRepository
    {
        public bool ThrowOnGetAll { get; set; } = true;
        public bool DeleteResult { get; set; } = true;
        public int SuccessfulDeletesBeforeThrow { get; set; } = int.MaxValue;
        public bool BlockGetAll { get; set; }
        public ManualResetEventSlim GetAllStarted { get; } = new();
        public ManualResetEventSlim ReleaseGetAll { get; } = new();
        private int _deleteCount;
        public int DeleteCalls => _deleteCount;
        public List<StudyDocument> Documents { get; set; } = [];

        public List<StudyDocument> GetAll()
        {
            if (ThrowOnGetAll)
                throw new InvalidOperationException();
            if (BlockGetAll)
            {
                GetAllStarted.Set();
                ReleaseGetAll.Wait();
            }

            return Documents;
        }

        public StudyDocument? GetById(int id) => throw new NotImplementedException();
        public List<StudyDocument> Search(string keyword) => throw new NotImplementedException();
        public List<StudyDocument> Filter(string subject, string type) => throw new NotImplementedException();
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => throw new NotImplementedException();
        public bool Add(StudyDocument document) => throw new NotImplementedException();
        public bool AddWithCatalogs(StudyDocument document) => throw new NotImplementedException();
        public bool Update(StudyDocument document) => throw new NotImplementedException();
        public bool Delete(int id)
        {
            if (_deleteCount++ >= SuccessfulDeletesBeforeThrow)
                throw new InvalidOperationException();

            return DeleteResult;
        }
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
        public bool UpdateDocumentPathResult { get; set; }
        public bool ClearDocumentPathResult { get; set; }
        public int UpdateDocumentPathCalls { get; private set; }
        public int ClearDocumentPathCalls { get; private set; }
        public bool UpdateDocumentPath(int id, string newPath)
        {
            UpdateDocumentPathCalls++;
            return UpdateDocumentPathResult;
        }
        public bool ClearDocumentPath(int id)
        {
            ClearDocumentPathCalls++;
            return ClearDocumentPathResult;
        }
        public bool BackupDatabase(string destPath, bool overwrite) => false;
        public bool CanRestoreDatabase(string sourcePath) => false;
        public bool RestoreDatabase(string sourcePath) => false;
    }

    private sealed class FileDialogServiceStub : IFileDialogService
    {
        public string? SelectedFile { get; set; }
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult(SelectedFile);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
    }

    private sealed class BlockingFileDialogService : IFileDialogService
    {
        public string? SelectedFile { get; set; }
        public ManualResetEventSlim SelectionStarted { get; } = new();
        public ManualResetEventSlim ReleaseSelection { get; } = new();

        public async Task<string?> ShowOpenFileAsync(string title, string? filter = null)
        {
            SelectionStarted.Set();
            await Task.Run(() => ReleaseSelection.Wait());
            return SelectedFile;
        }

        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
    }

    private sealed class BlockingConfirmationDialogService : IDialogService
    {
        public bool ConfirmResult { get; set; }
        public ManualResetEventSlim ConfirmationStarted { get; } = new();
        public ManualResetEventSlim ReleaseConfirmation { get; } = new();

        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message)
            => WaitForConfirmationAsync();
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
            => WaitForConfirmationAsync();
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "")
            => Task.FromResult<string?>(null);

        private async Task<bool> WaitForConfirmationAsync()
        {
            ConfirmationStarted.Set();
            await Task.Run(() => ReleaseConfirmation.Wait());
            return ConfirmResult;
        }
    }

    private sealed class RecordingDialogService : IDialogService
    {
        public bool ThrowOnMessage { get; set; }
        public bool ThrowOnError { get; set; }
        public bool ThrowCancellation { get; set; }
        public bool ConfirmResult { get; set; }
        public List<string> Errors { get; } = [];

        public Task ShowMessageAsync(string title, string message)
            => ThrowOnMessage ? throw new InvalidOperationException() : Task.CompletedTask;

        public Task ShowErrorAsync(string title, string message)
        {
            Errors.Add(message);
            return ThrowOnError ? throw new InvalidOperationException() : Task.CompletedTask;
        }

        public Task<bool> ShowConfirmAsync(string title, string message)
            => ThrowCancellation ? Task.FromCanceled<bool>(new CancellationToken(true)) : Task.FromResult(ConfirmResult);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
            => ThrowCancellation ? Task.FromCanceled<bool>(new CancellationToken(true)) : Task.FromResult(ConfirmResult);
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
