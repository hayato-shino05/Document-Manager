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

    private sealed class FakeRecycleBinRepository(List<StudyDocument> deletedDocuments) : IRecycleBinRepository
    {
        private readonly List<StudyDocument> _deletedDocuments = deletedDocuments;

        public bool RestoreResult { get; set; } = true;
        public bool PermanentDeleteResult { get; set; } = true;
        public int EmptyTrashCount { get; set; }

        public List<StudyDocument> GetDeletedDocuments() => [.._deletedDocuments];

        public bool RestoreDocument(int id)
        {
            if (!RestoreResult)
                return false;

            _deletedDocuments.RemoveAll(document => document.Id == id);
            return true;
        }

        public bool PermanentDeleteDocument(int id)
        {
            if (!PermanentDeleteResult)
                return false;

            _deletedDocuments.RemoveAll(document => document.Id == id);
            return true;
        }

        public int EmptyRecycleBin()
        {
            var count = EmptyTrashCount == 0 ? _deletedDocuments.Count : EmptyTrashCount;
            _deletedDocuments.Clear();
            return count;
        }

        public int GetDeletedDocumentCount() => _deletedDocuments.Count;
    }

    private sealed class FakeDialogService : IDialogService
    {
        public bool ConfirmResult { get; set; }
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
            => Task.FromResult(ConfirmResult);

        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
            => Task.FromResult(ConfirmResult);

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
