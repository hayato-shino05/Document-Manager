using System.Collections.ObjectModel;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public class BatchImportDefaultSubjectGapTests : DatabaseTestBase
{
    [Fact]
    public async Task ImportAsync_WithDefaultSubject_AssignsDefaultSubjectToAllImportedDocuments()
    {
        var dialog = new RecordingDialogService();
        var navigation = new RecordingNavigationService();
        var model = CreateModel(dialog, navigation);
        model.DefaultSubject = "Toán";
        model.Files = new ObservableCollection<FileImportItem>
        {
            new() { FileName = "Algebra", FilePath = "C:/algebra.pdf", FileType = "PDF", FileSizeMB = 1, IsSelected = true },
            new() { FileName = "Geometry", FilePath = "C:/geometry.pdf", FileType = "PDF", FileSizeMB = 1, IsSelected = true }
        };

        await model.ImportCommand.ExecuteAsync(null);

        Assert.Equal(2, model.ImportedCount);
        Assert.All(model.Files, item => Assert.False(item.IsSelected));
        var saved = Repo.GetAll();
        Assert.Equal(2, saved.Count);
        Assert.All(saved, doc => Assert.Equal("Toán", doc.Subject));
        Assert.Contains("Toán", Repo.GetDistinctSubjects());
    }

    [Fact]
    public async Task ImportAsync_WithEmptyDefaultSubject_ImportsDocumentsWithEmptySubject()
    {
        var model = CreateModel();
        model.DefaultSubject = "   ";
        model.Files = new ObservableCollection<FileImportItem>
        {
            new() { FileName = "Notes", FilePath = "C:/notes.txt", FileType = "Text", FileSizeMB = 1, IsSelected = true }
        };

        await model.ImportCommand.ExecuteAsync(null);

        Assert.Equal(1, model.ImportedCount);
        var saved = Assert.Single(Repo.GetAll());
        Assert.Equal(string.Empty, saved.Subject);
    }

    [Fact]
    public async Task ImportAsync_WhenNoFileSelected_ShowsErrorAndImportsNothing()
    {
        var dialog = new RecordingDialogService();
        var navigation = new RecordingNavigationService();
        var model = CreateModel(dialog, navigation);
        model.DefaultSubject = "Toán";
        model.Files = new ObservableCollection<FileImportItem>
        {
            new() { FileName = "Algebra", FilePath = "C:/algebra.pdf", FileType = "PDF", IsSelected = false }
        };

        await model.ImportCommand.ExecuteAsync(null);

        Assert.Contains("Import_NoFileSelected", dialog.Messages);
        Assert.Equal(0, model.ImportedCount);
        Assert.Empty(Repo.GetAll());
        Assert.Empty(navigation.Routes);
    }

    [Fact]
    public async Task ImportAsync_WithDifferentFixedDefaultSubject_SavesDocumentsWithThatSubject()
    {
        var model = CreateModel();
        model.DefaultSubject = "Vật Lý";
        model.Files = new ObservableCollection<FileImportItem>
        {
            new() { FileName = "Mechanics", FilePath = "C:/mechanics.pdf", FileType = "PDF", IsSelected = true },
            new() { FileName = "Optics", FilePath = "C:/optics.pdf", FileType = "PDF", IsSelected = true }
        };

        await model.ImportCommand.ExecuteAsync(null);

        Assert.Equal(2, model.ImportedCount);
        Assert.All(Repo.GetAll(), doc => Assert.Equal("Vật Lý", doc.Subject));
        Assert.Contains("Vật Lý", Repo.GetDistinctSubjects());
    }

    [Fact]
    public async Task ImportAsync_Success_NavigatesToDashboardAndShowsMessage()
    {
        var dialog = new RecordingDialogService();
        var navigation = new RecordingNavigationService();
        var model = CreateModel(dialog, navigation);
        model.Files = new ObservableCollection<FileImportItem>
        {
            new() { FileName = "History", FilePath = "C:/history.pdf", FileType = "PDF", IsSelected = true }
        };

        await model.ImportCommand.ExecuteAsync(null);

        Assert.Single(dialog.Messages);
        Assert.Equal("BatchImport_ResultSummary", model.ImportStatusMessage);
        Assert.Equal(["dashboard"], navigation.Routes);
    }

    private BatchImportModel CreateModel(
        RecordingDialogService? dialogService = null,
        RecordingNavigationService? navigationService = null)
    {
        return new BatchImportModel(
            dialogService ?? new RecordingDialogService(),
            new FakeFileDialogService(),
            navigationService ?? new RecordingNavigationService(),
            new TestLocalizationService(),
            new DroppedFileImportService(Repo));
    }

    private sealed class RecordingDialogService : IDialogService
    {
        public List<string> Messages { get; } = [];

        public Task ShowMessageAsync(string title, string message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(string title, string message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(false);

        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
            => Task.FromResult(false);

        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "")
            => Task.FromResult<string?>(null);
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
        public SupportedLanguage CurrentLanguage { get; private set; } = SupportedLanguage.Japanese;
        public SupportedLanguage? LastSetLanguage { get; private set; }

        public void SetLanguage(SupportedLanguage language)
        {
            CurrentLanguage = language;
            LastSetLanguage = language;
        }

        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [];
        public event EventHandler? LanguageChanged { add { } remove { } }
    }
}