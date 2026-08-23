using System.Collections.ObjectModel;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class BatchImportOperationStateTests
{
    [Fact]
    public async Task ImportThenRetry_TracksProgressAndRetriesOnlyFailedItems()
    {
        var importer = new ControlledImportService { FailFirstA = true };
        var navigation = new RecordingNavigationService();
        var model = CreateModel(importer, navigation);
        model.Files = new ObservableCollection<FileImportItem>
        {
            new() { FileName = "A", FilePath = "A.pdf", FileType = "PDF" },
            new() { FileName = "B", FilePath = "B.pdf", FileType = "PDF" },
            new() { FileName = "C", FilePath = "C.pdf", FileType = "PDF" }
        };

        await model.ImportCommand.ExecuteAsync(null);

        Assert.Equal(3, model.TotalCount);
        Assert.Equal(3, model.ProcessedCount);
        Assert.Equal(1, model.ImportedCount);
        Assert.Equal(1, model.SkippedDuplicateCount);
        Assert.Equal(1, model.FailedCount);
        Assert.Equal(["A.pdf"], model.FailedItems);
        Assert.True(model.Files[0].IsFailed);
        Assert.Equal("BatchImport_ItemFailed", model.Files[0].FailureReason);
        Assert.Empty(navigation.Routes);

        await model.RetryFailedCommand.ExecuteAsync(null);

        Assert.Equal(["A.pdf", "B.pdf", "C.pdf", "A.pdf"], importer.AttemptedPaths);
        Assert.Equal(1, model.TotalCount);
        Assert.Equal(1, model.ProcessedCount);
        Assert.Equal(2, model.ImportedCount);
        Assert.Equal(1, model.SkippedDuplicateCount);
        Assert.Equal(0, model.FailedCount);
        Assert.Empty(model.FailedItems);
        Assert.False(model.Files[0].IsFailed);
        Assert.Empty(model.Files[0].FailureReason);
        Assert.Equal(["dashboard"], navigation.Routes);
    }

    [Fact]
    public async Task CancelDuringImport_DoesNotMarkUnprocessedItemsAsFailed()
    {
        var importer = new ControlledImportService { FailFirstA = false };
        var model = CreateModel(importer, new RecordingNavigationService());
        importer.OnFirstSave = () => model.CancelCommand.Execute(null);
        model.Files = new ObservableCollection<FileImportItem>
        {
            new() { FileName = "A", FilePath = "A.pdf", FileType = "PDF" },
            new() { FileName = "B", FilePath = "B.pdf", FileType = "PDF" }
        };

        await model.ImportCommand.ExecuteAsync(null);

        Assert.True(model.IsImportCancelled);
        Assert.Equal(2, model.TotalCount);
        Assert.Equal(1, model.ProcessedCount);
        Assert.Equal(1, model.ImportedCount);
        Assert.Equal(0, model.FailedCount);
        Assert.False(model.Files[0].IsSelected);
        Assert.True(model.Files[1].IsSelected);
        Assert.False(model.Files[1].IsFailed);
        Assert.Equal("BatchImport_Cancelled", model.ImportStatusMessage);
    }

    [Fact]
    public async Task RetryFailed_WhenFailureRepeats_KeepsFailedCountStable()
    {
        var importer = new ControlledImportService { AlwaysFailA = true };
        var model = CreateModel(importer, new RecordingNavigationService());
        model.Files = new ObservableCollection<FileImportItem>
        {
            new() { FileName = "A", FilePath = "A.pdf", FileType = "PDF" }
        };

        await model.ImportCommand.ExecuteAsync(null);
        await model.RetryFailedCommand.ExecuteAsync(null);

        Assert.Equal(1, model.FailedCount);
        Assert.Single(model.FailedItems);
        Assert.True(model.Files[0].IsFailed);
    }

    private static BatchImportModel CreateModel(
        ControlledImportService importer,
        RecordingNavigationService navigation)
        => new(
            new RecordingDialogService(),
            new FakeFileDialogService(),
            navigation,
            new TestLocalizationService(),
            importer);

    private sealed class ControlledImportService : IDroppedFileImportService
    {
        private bool _failedOnce;

        public bool FailFirstA { get; init; }
        public bool AlwaysFailA { get; init; }
        public List<string> AttemptedPaths { get; } = [];
        public Action? OnFirstSave { get; set; }

        public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects) => fallbackSubjects.ToList();
        public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes) => fallbackTypes.ToList();
        public StudyDocument BuildDocumentFromPath(string filePath) => throw new NotSupportedException();

        public DocumentImportOutcome SaveDocument(StudyDocument document)
        {
            AttemptedPaths.Add(document.FilePath);
            if (AttemptedPaths.Count == 1)
                OnFirstSave?.Invoke();
            if (AlwaysFailA && document.FilePath == "A.pdf")
                return DocumentImportOutcome.Failed;

            if (FailFirstA && document.FilePath == "A.pdf" && !_failedOnce)
            {
                _failedOnce = true;
                return DocumentImportOutcome.Failed;
            }

            return document.FilePath == "B.pdf"
                ? DocumentImportOutcome.SkippedDuplicate
                : DocumentImportOutcome.Imported;
        }
    }

    private sealed class RecordingDialogService : IDialogService
    {
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(false);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(false);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
    }

    private sealed class FakeFileDialogService : IFileDialogService
    {
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
    }

    private sealed class RecordingNavigationService : INavigationService
    {
        public List<string> Routes { get; } = [];
        public bool CanGoBack => false;
        public void NavigateTo(string viewKey) => Routes.Add(viewKey);
        public void NavigateTo(string viewKey, object? parameter) => Routes.Add(viewKey);
        public void GoBack() { }
    }

    private sealed class TestLocalizationService : ILocalizationService
    {
        public string this[string key] => key;
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [];
        public event EventHandler? LanguageChanged { add { } remove { } }
        public void SetLanguage(SupportedLanguage language) { }
    }
}
