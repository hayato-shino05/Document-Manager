using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using Xunit;

namespace StudyDocumentManager.Tests;

public class RecycleBinModelTests
{
    [Fact]
    public async Task RestoreFailure_PreservesSelectionAndShowsError()
    {
        var document = new StudyDocument { Id = 1, Name = "Deleted" };
        var repository = new FakeRecycleBinRepository([document])
        {
            RestoreResult = false
        };
        var dialog = new FakeDialogService { ConfirmResult = true };
        var model = new RecycleBinModel(repository, dialog, new StubLocalizationService())
        {
            SelectedDocument = document
        };

        await ((IAsyncRelayCommand)model.RestoreCommand).ExecuteAsync(null);

        Assert.Same(document, model.SelectedDocument);
        Assert.Single(model.DeletedDocuments);
        Assert.Equal("Recycle_RestoreError", dialog.LastErrorMessage);
        Assert.Null(dialog.LastMessage);
    }

    [Fact]
    public async Task PermanentDeleteFailure_PreservesSelectionAndShowsError()
    {
        var document = new StudyDocument { Id = 1, Name = "Deleted" };
        var repository = new FakeRecycleBinRepository([document])
        {
            PermanentDeleteResult = false
        };
        var dialog = new FakeDialogService { ConfirmResult = true };
        var model = new RecycleBinModel(repository, dialog, new StubLocalizationService())
        {
            SelectedDocument = document
        };

        await ((IAsyncRelayCommand)model.PermanentDeleteCommand).ExecuteAsync(null);

        Assert.Same(document, model.SelectedDocument);
        Assert.Single(model.DeletedDocuments);
        Assert.Equal("Recycle_PermanentDeleteError", dialog.LastErrorMessage);
        Assert.Null(dialog.LastMessage);
    }

    [Fact]
    public void StateFlags_ReflectSelectionAndDeletedDocuments()
    {
        var document = new StudyDocument { Id = 1, Name = "Deleted" };
        var repository = new FakeRecycleBinRepository([document]);
        var model = new RecycleBinModel(repository, new FakeDialogService(), new StubLocalizationService());

        Assert.True(model.HasDeletedDocuments);
        Assert.False(model.HasSelection);

        model.SelectedDocument = document;
        Assert.True(model.HasSelection);
    }


    [Fact]
    public async Task RestoreSuccess_ClearsSelectionAndHasSelection()
    {
        var document = new StudyDocument { Id = 1, Name = "Deleted" };
        var repository = new FakeRecycleBinRepository([document]);
        var dialog = new FakeDialogService { ConfirmResult = true };
        var model = new RecycleBinModel(repository, dialog, new StubLocalizationService())
        {
            SelectedDocument = document
        };

        await ((IAsyncRelayCommand)model.RestoreCommand).ExecuteAsync(null);

        Assert.Null(model.SelectedDocument);
        Assert.False(model.HasSelection);
        Assert.Empty(model.DeletedDocuments);
    }

    [Fact]
    public async Task PermanentDeleteSuccess_ClearsSelectionAndHasSelection()
    {
        var document = new StudyDocument { Id = 1, Name = "Deleted" };
        var repository = new FakeRecycleBinRepository([document]);
        var dialog = new FakeDialogService { ConfirmResult = true };
        var model = new RecycleBinModel(repository, dialog, new StubLocalizationService())
        {
            SelectedDocument = document
        };

        await ((IAsyncRelayCommand)model.PermanentDeleteCommand).ExecuteAsync(null);

        Assert.Null(model.SelectedDocument);
        Assert.False(model.HasSelection);
        Assert.Empty(model.DeletedDocuments);
    }


    [Fact]
    public async Task EmptyTrash_ZeroOrPartialOrCancelled_PreservesItemsAndDoesNotShowSuccess()
    {
        List<StudyDocument> documents = [new StudyDocument { Id = 1, Name = "First" }, new StudyDocument { Id = 2, Name = "Second" }];
        var repository = new FakeRecycleBinRepository(documents) { EmptyTrashCount = 0 };
        var dialog = new FakeDialogService { ConfirmResult = true };
        var model = new RecycleBinModel(repository, dialog, new StubLocalizationService());

        await ((IAsyncRelayCommand)model.EmptyTrashCommand).ExecuteAsync(null);

        Assert.Equal(2, model.DeletedDocuments.Count);
        Assert.Equal("Operation_Partial", dialog.LastErrorMessage);
        Assert.Null(dialog.LastMessage);

        dialog.CancelConfirmation = true;
        await ((IAsyncRelayCommand)model.EmptyTrashCommand).ExecuteAsync(null);

        Assert.Equal(2, model.DeletedDocuments.Count);
        Assert.Equal("Operation_Partial", dialog.LastErrorMessage);
    }


    [Fact]
    public async Task EmptyTrash_PartialResult_ReconcilesAndRetryRemovesRemaining()
    {
        List<StudyDocument> documents = [new StudyDocument { Id = 1, Name = "First" }, new StudyDocument { Id = 2, Name = "Second" }];
        var repository = new FakeRecycleBinRepository(documents) { EmptyTrashCount = 1 };
        var dialog = new FakeDialogService { ConfirmResult = true };
        var model = new RecycleBinModel(repository, dialog, new StubLocalizationService());

        await ((IAsyncRelayCommand)model.EmptyTrashCommand).ExecuteAsync(null);

        Assert.Single(model.DeletedDocuments);
        Assert.Equal(2, model.DeletedDocuments[0].Id);
        Assert.Equal("Operation_Partial", dialog.LastErrorMessage);

        repository.EmptyTrashCount = null;
        await ((IAsyncRelayCommand)model.EmptyTrashCommand).ExecuteAsync(null);

        Assert.Empty(model.DeletedDocuments);
        Assert.Equal("Recycle_EmptyTrashDone", dialog.LastMessage);
    }

    [Fact]
    public async Task EmptyTrash_RepositoryThrows_PreservesCurrentStateAndReportsError()
    {
        var repository = new FakeRecycleBinRepository([new StudyDocument { Id = 1, Name = "Deleted" }])
        {
            ThrowOnEmptyTrash = true
        };
        var dialog = new FakeDialogService { ConfirmResult = true };
        var model = new RecycleBinModel(repository, dialog, new StubLocalizationService());

        await ((IAsyncRelayCommand)model.EmptyTrashCommand).ExecuteAsync(null);

        Assert.Single(model.DeletedDocuments);
        Assert.Equal("Msg_Error", dialog.LastErrorMessage);
        Assert.Null(dialog.LastMessage);
    }

    [Fact]
    public async Task RestoreAndPermanentDelete_RepositoryThrows_PreserveSelectionAndReportError()
    {
        var document = new StudyDocument { Id = 1, Name = "Deleted" };
        var repository = new FakeRecycleBinRepository([document])
        {
            ThrowOnRestore = true,
            ThrowOnPermanentDelete = true
        };
        var dialog = new FakeDialogService { ConfirmResult = true };
        var model = new RecycleBinModel(repository, dialog, new StubLocalizationService())
        {
            SelectedDocument = document
        };

        await ((IAsyncRelayCommand)model.RestoreCommand).ExecuteAsync(null);
        Assert.Same(document, model.SelectedDocument);
        Assert.Equal("Msg_Error", dialog.LastErrorMessage);

        await ((IAsyncRelayCommand)model.PermanentDeleteCommand).ExecuteAsync(null);
        Assert.Same(document, model.SelectedDocument);
        Assert.Equal("Msg_Error", dialog.LastErrorMessage);
    }


    [Fact]
    public async Task RestoreAndPermanentDelete_Cancellation_PreserveSelectionWithoutFeedback()
    {
        var document = new StudyDocument { Id = 1, Name = "Deleted" };
        var dialog = new FakeDialogService { ConfirmResult = true, CancelConfirmation = true };
        var model = new RecycleBinModel(new FakeRecycleBinRepository([document]), dialog, new StubLocalizationService())
        {
            SelectedDocument = document
        };

        await ((IAsyncRelayCommand)model.RestoreCommand).ExecuteAsync(null);
        await ((IAsyncRelayCommand)model.PermanentDeleteCommand).ExecuteAsync(null);

        Assert.Same(document, model.SelectedDocument);
        Assert.Null(dialog.LastErrorMessage);
        Assert.Null(dialog.LastMessage);
    }

    private sealed class FakeRecycleBinRepository(List<StudyDocument> deletedDocuments) : IRecycleBinRepository
    {
        private readonly List<StudyDocument> _deletedDocuments = deletedDocuments;

        public bool RestoreResult { get; set; } = true;
        public bool PermanentDeleteResult { get; set; } = true;
        public int? EmptyTrashCount { get; set; }
        public bool ThrowOnEmptyTrash { get; set; }
        public bool ThrowOnRestore { get; set; }
        public bool ThrowOnPermanentDelete { get; set; }

        public List<StudyDocument> GetDeletedDocuments() => [.._deletedDocuments];

        public bool RestoreDocument(int id)
        {
            if (ThrowOnRestore)
                throw new InvalidOperationException();
            if (!RestoreResult)
                return false;

            _deletedDocuments.RemoveAll(document => document.Id == id);
            return true;
        }

        public bool PermanentDeleteDocument(int id)
        {
            if (ThrowOnPermanentDelete)
                throw new InvalidOperationException();
            if (!PermanentDeleteResult)
                return false;

            _deletedDocuments.RemoveAll(document => document.Id == id);
            return true;
        }

        public int EmptyRecycleBin()
        {
            if (ThrowOnEmptyTrash)
                throw new InvalidOperationException();

            var count = EmptyTrashCount ?? _deletedDocuments.Count;
            _deletedDocuments.RemoveRange(0, Math.Min(count, _deletedDocuments.Count));
            return count;
        }

        public int GetDeletedDocumentCount() => _deletedDocuments.Count;
    }

    private sealed class FakeDialogService : IDialogService
    {
        public bool ConfirmResult { get; set; }
        public bool CancelConfirmation { get; set; }
        public string? LastMessage { get; private set; }
        public string? LastErrorMessage { get; private set; }

        public Task ShowMessageAsync(string title, string message)
        {
            LastMessage = message;
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(string title, string message)
        {
            LastErrorMessage = message;
            return Task.CompletedTask;
        }

        public Task<bool> ShowConfirmAsync(string title, string message)
            => CancelConfirmation ? Task.FromCanceled<bool>(new CancellationToken(true)) : Task.FromResult(ConfirmResult);

        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
            => CancelConfirmation ? Task.FromCanceled<bool>(new CancellationToken(true)) : Task.FromResult(ConfirmResult);

        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "")
            => Task.FromResult<string?>(null);
    }

    private sealed class StubLocalizationService : ILocalizationService
    {
        public string this[string key] => key;
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public void SetLanguage(SupportedLanguage language) { }
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [];
        public event EventHandler? LanguageChanged { add { } remove { } }
    }
}
