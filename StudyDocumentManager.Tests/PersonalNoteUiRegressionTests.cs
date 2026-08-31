using Avalonia.Controls;
using StudyDocumentManager.Core.Entities;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class PersonalNoteUiRegressionTests
{
    [AvaloniaFact]
    public void PersonalNote_EditorialLayout_ExposesAccessibleNamedControls()
    {
        var view = new PersonalNote();
        var window = new Window { Content = view };

        try
        {
            window.Show();

            var editor = view.FindControl<TextBox>("txtNoteContent");
            var backButton = view.FindControl<Button>("btnNoteBack");
            var saveButton = view.FindControl<Button>("btnNoteSave");
            var deleteButton = view.FindControl<Button>("btnNoteDelete");

            Assert.NotNull(editor);
            Assert.NotNull(backButton);
            Assert.NotNull(saveButton);
            Assert.NotNull(deleteButton);
            Assert.False(string.IsNullOrWhiteSpace(editor!.Watermark));
            Assert.False(string.IsNullOrWhiteSpace(
                editor.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)));
            Assert.False(string.IsNullOrWhiteSpace(
                saveButton!.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PersonalNote_ShowsSavedNotePreview_WhenNoteExists()
    {
        var model = new PersonalNoteModel(
            new PersonalNoteRepositoryStub(),
            new DialogServiceStub(),
            new NavigationServiceStub(),
            new LocalizationServiceStub())
        {
            DocumentName = "Algebra",
            NoteContent = "Saved note content",
            SavedNoteContent = "Saved note content",
            HasExistingNote = true
        };
        var view = new PersonalNote { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            var preview = view.FindControl<TextBlock>("txtSavedNotePreview");

            Assert.NotNull(preview);
            Assert.Equal("Saved note content", preview!.Text);
            Assert.True(preview.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PersonalNote_HidesSavedNotePreview_WhenSavedNoteIsEmpty()
    {
        var model = new PersonalNoteModel(
            new PersonalNoteRepositoryStub(),
            new DialogServiceStub(),
            new NavigationServiceStub(),
            new LocalizationServiceStub())
        {
            DocumentName = "Algebra",
            NoteContent = string.Empty,
            SavedNoteContent = string.Empty,
            HasExistingNote = true
        };
        var view = new PersonalNote { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            var previewCard = view.FindControl<Border>("savedNotePreviewCard");

            Assert.NotNull(previewCard);
            Assert.False(previewCard!.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PersonalNote_ExposesMultiNoteControlsWithAccessibleNames()
    {
        var view = new PersonalNote();
        var window = new Window { Content = view };

        try
        {
            window.Show();

            var noteList = view.FindControl<ItemsControl>("lstNotes");
            var noteType = view.FindControl<ComboBox>("cmbNoteType");
            var pinned = view.FindControl<Button>("btnNotePinned");

            Assert.NotNull(noteList);
            Assert.NotNull(noteType);
            Assert.NotNull(pinned);
            Assert.False(string.IsNullOrWhiteSpace(noteType!.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)));
            Assert.False(string.IsNullOrWhiteSpace(pinned!.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PersonalNote_EditorRemainsInsideNarrowViewport()
    {
        var view = new PersonalNote();
        var window = new Window { Content = view, Width = 420, Height = 360 };

        try
        {
            window.Show();
            var editor = view.FindControl<TextBox>("txtNoteContent");

            Assert.NotNull(editor);
            Assert.True(editor!.Bounds.Width > 0);
            Assert.True(editor.Bounds.Height > 0);
            Assert.True(editor.Bounds.Right <= view.Bounds.Width + 1);
            Assert.True(editor.Bounds.Bottom <= view.Bounds.Height + 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task PersonalNote_SaveRoundTrip_PreservesEditorContent()
    {
        var repository = new PersonalNoteRepositoryStub();
        var model = new PersonalNoteModel(
            repository,
            new DialogServiceStub(),
            new NavigationServiceStub(),
            new LocalizationServiceStub());
        model.Load(7, "Algebra");
        var view = new PersonalNote { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            var editor = view.FindControl<TextBox>("txtNoteContent");

            Assert.NotNull(editor);
            editor!.Text = "Runtime note";
            await model.SaveNoteCommand.ExecuteAsync(null);

            Assert.Equal("Runtime note", repository.SavedContent);
            Assert.Equal("Runtime note", repository.GetNote(7));
            Assert.True(model.HasExistingNote);
            Assert.True(model.CanSaveNote);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PersonalNote_DoesNotShowUnsavedEditorChangesAsSavedPreview()
    {
        var model = new PersonalNoteModel(
            new PersonalNoteRepositoryStub(),
            new DialogServiceStub(),
            new NavigationServiceStub(),
            new LocalizationServiceStub())
        {
            DocumentName = "Algebra",
            NoteContent = "Draft note",
            SavedNoteContent = "Saved note",
            HasExistingNote = true
        };
        var view = new PersonalNote { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            var preview = view.FindControl<TextBlock>("txtSavedNotePreview");

            Assert.NotNull(preview);
            Assert.Equal("Saved note", preview!.Text);

            model.NoteContent = "Changed draft";

            Assert.Equal("Saved note", preview.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PersonalNote_ChangingSelection_DoesNotDiscardDraft()
    {
        var repository = new PersonalNoteRepositoryStub
        {
            Notes =
            [
                new(1, 7, "general", "Saved general", false),
                new(2, 7, "action", "Saved action", true)
            ]
        };
        var model = new PersonalNoteModel(
            repository,
            new DialogServiceStub(),
            new NavigationServiceStub(),
            new LocalizationServiceStub());
        model.Load(7, "Algebra");
        model.NoteContent = "Unsaved draft";

        model.SelectedNote = repository.Notes[1];

        Assert.Equal(1, model.SelectedNote?.Id);
        Assert.Equal("Unsaved draft", model.NoteContent);
    }

    [Fact]
    public async Task PersonalNote_NewNote_PreservesExistingGeneralNote()
    {
        var repository = new PersonalNoteRepositoryStub
        {
            Notes = [new(1, 7, "general", "Existing", false)]
        };
        var model = new PersonalNoteModel(
            repository,
            new DialogServiceStub(),
            new NavigationServiceStub(),
            new LocalizationServiceStub());
        model.Load(7, "Algebra");

        model.NewNoteCommand.Execute(null);
        model.NoteContent = "Second note";
        await model.SaveNoteCommand.ExecuteAsync(null);

        Assert.Equal(2, repository.Notes.Count);
        Assert.Contains(repository.Notes, note => note.Content == "Existing");
        Assert.Contains(repository.Notes, note => note.Content == "Second note");
    }

    [AvaloniaFact]
    public void PersonalNote_ShowsPinnedState()
    {
        var model = new PersonalNoteModel(
            new PersonalNoteRepositoryStub(),
            new DialogServiceStub(),
            new NavigationServiceStub(),
            new LocalizationServiceStub())
        {
            IsPinned = true
        };
        var view = new PersonalNote { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            var pinnedState = view.FindControl<TextBlock>("txtNotePinnedState");

            Assert.NotNull(pinnedState);
            Assert.True(pinnedState!.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public async Task PersonalNote_DirtyBack_ConfirmsBeforeNavigating()
    {
        var dialog = new DialogServiceStub { ConfirmResult = false };
        var navigation = new NavigationServiceStub();
        var model = new PersonalNoteModel(
            new PersonalNoteRepositoryStub(), dialog, navigation, new LocalizationServiceStub());
        model.Load(7, "Algebra");
        model.NoteContent = "Draft note";

        await model.GoBackCommand.ExecuteAsync(null);

        Assert.Equal(1, dialog.ConfirmCount);
        Assert.False(navigation.WentBack);

        dialog.ConfirmResult = true;
        await model.GoBackCommand.ExecuteAsync(null);

        Assert.Equal(2, dialog.ConfirmCount);
        Assert.True(navigation.WentBack);
    }

    [Fact]
    public async Task PersonalNote_UnchangedBack_NavigatesWithoutConfirmation()
    {
        var dialog = new DialogServiceStub();
        var navigation = new NavigationServiceStub();
        var model = new PersonalNoteModel(
            new PersonalNoteRepositoryStub(), dialog, navigation, new LocalizationServiceStub());
        model.Load(7, "Algebra");

        await model.GoBackCommand.ExecuteAsync(null);

        Assert.Equal(0, dialog.ConfirmCount);
        Assert.True(navigation.WentBack);
    }

    private sealed class PersonalNoteRepositoryStub : IPersonalNoteRepository
    {
        public string? SavedContent { get; private set; }
        public List<PersonalNote> Notes { get; set; } = [];
        public string? GetNote(int documentId) => Notes.FirstOrDefault(note => note.NoteType == "general")?.Content ?? SavedContent;
        public IReadOnlyList<PersonalNote> GetNotes(int documentId, bool includeDeleted = false)
            => Notes.Where(note => note.DocumentId == documentId).ToList();
        public bool SaveNote(PersonalNote note)
        {
            if (note.Id == 0)
            {
                var created = note with { Id = Notes.Count == 0 ? 1 : Notes.Max(item => item.Id) + 1 };
                Notes.Add(created);
            }
            else
            {
                Notes[Notes.FindIndex(item => item.Id == note.Id)] = note;
            }
            SavedContent = note.Content;
            return true;
        }
        public bool SaveNote(int documentId, string content)
        {
            SavedContent = content;
            return true;
        }
        public bool DeleteNote(int documentId) => true;
    }

    private sealed class DialogServiceStub : IDialogService
    {
        public bool ConfirmResult { get; set; }
        public int ConfirmCount { get; private set; }
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(false);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
        {
            ConfirmCount++;
            return Task.FromResult(ConfirmResult);
        }
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
    }

    private sealed class NavigationServiceStub : INavigationService
    {
        public bool CanGoBack => true;
        public bool WentBack { get; private set; }
        public void NavigateTo(string viewKey) { }
        public void NavigateTo(string viewKey, object? parameter) { }
        public void GoBack() => WentBack = true;
    }

    private sealed class LocalizationServiceStub : ILocalizationService
    {
        public string this[string key] => key;
        public StudyDocumentManager.Core.SupportedLanguage CurrentLanguage => StudyDocumentManager.Core.SupportedLanguage.Japanese;
        public IReadOnlyList<StudyDocumentManager.Core.SupportedLanguage> AvailableLanguages => [StudyDocumentManager.Core.SupportedLanguage.Japanese];
        public void SetLanguage(StudyDocumentManager.Core.SupportedLanguage language)
            => LanguageChanged?.Invoke(this, EventArgs.Empty);
        public event EventHandler? LanguageChanged;
    }
}
