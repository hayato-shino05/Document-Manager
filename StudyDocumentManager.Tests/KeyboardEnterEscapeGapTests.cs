using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

public class KeyboardEnterEscapeGapTests
{
    [AvaloniaFact]
    public void AddEdit_SaveButtonIsMarkedDefaultButton()
    {
        var localization = GetLocalization();
        var model = CreateAddEditModel(localization, new RecordingDialogService(), new RecordingNavigationService());
        var view = new AddEdit { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            FlushAvaloniaBindings();

            var saveButton = view.FindControl<Button>("btnSave");
            Assert.NotNull(saveButton);
            Assert.True(saveButton!.IsDefault);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AddEdit_CancelButtonIsMarkedCancelButton()
    {
        var localization = GetLocalization();
        var model = CreateAddEditModel(localization, new RecordingDialogService(), new RecordingNavigationService());
        var view = new AddEdit { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            FlushAvaloniaBindings();

            var cancelButton = view.FindControl<Button>("btnCancel");
            Assert.NotNull(cancelButton);
            Assert.True(cancelButton!.IsCancel);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AddEdit_SaveButtonOffersCtrlSHotKey()
    {
        var localization = GetLocalization();
        var model = CreateAddEditModel(localization, new RecordingDialogService(), new RecordingNavigationService());
        var view = new AddEdit { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            FlushAvaloniaBindings();

            var saveButton = view.FindControl<Button>("btnSave");
            Assert.NotNull(saveButton);
            Assert.NotNull(saveButton!.HotKey);
            Assert.Equal(Key.S, saveButton.HotKey!.Key);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AddEdit_CancelButtonOffersEscapeHotKey()
    {
        var localization = GetLocalization();
        var model = CreateAddEditModel(localization, new RecordingDialogService(), new RecordingNavigationService());
        var view = new AddEdit { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            FlushAvaloniaBindings();

            var cancelButton = view.FindControl<Button>("btnCancel");
            Assert.NotNull(cancelButton);
            Assert.NotNull(cancelButton!.HotKey);
            Assert.Equal(Key.Escape, cancelButton.HotKey!.Key);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AddEdit_EscapeKeyRunsCancelCommand()
    {
        var localization = GetLocalization();
        var navigation = new RecordingNavigationService();
        var dialogs = new RecordingDialogService();
        var model = CreateAddEditModel(localization, dialogs, navigation);
        var view = new AddEdit { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            FlushAvaloniaBindings();

            var nameBox = view.FindControl<TextBox>("txtName");
            Assert.NotNull(nameBox);
            Assert.True(nameBox!.Focus());

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.None, null);
            FlushAvaloniaBindings();

            Assert.Equal("dashboard", Assert.Single(navigation.Navigations));
            Assert.Empty(dialogs.Calls);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AddEdit_EnterKey_TriggersDefaultSaveButton()
    {
        var localization = GetLocalization();
        var navigation = new RecordingNavigationService();
        var dialogs = new RecordingDialogService();
        var model = CreateAddEditModel(localization, dialogs, navigation);
        model.Name = "Algorithms notes";
        var view = new AddEdit { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            FlushAvaloniaBindings();

            var nameBox = view.FindControl<TextBox>("txtName");
            Assert.NotNull(nameBox);
            Assert.True(nameBox!.Focus());

            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
            FlushAvaloniaBindings();

            Assert.Equal("dashboard", Assert.Single(navigation.Navigations));
            var dialogCall = Assert.Single(dialogs.Calls);
            Assert.StartsWith("message:", dialogCall);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AddDocumentDialog_MarksSaveDefaultAndCancelCancel()
    {
        GetLocalization();
        var dialog = new AddDocumentDialog("C:/drop/test.pdf", ["Study"], ["PDF"]);
        dialog.Show();

        try
        {
            var saveButton = dialog.FindControl<Button>("btnSave");
            var cancelButton = dialog.FindControl<Button>("btnCancel");
            Assert.NotNull(saveButton);
            Assert.NotNull(cancelButton);
            Assert.True(saveButton!.IsDefault);
            Assert.True(cancelButton!.IsCancel);
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void ChangeCategoryDialog_MarksOkDefaultAndCancelCancel()
    {
        var localization = GetLocalization();
        var dialog = new ChangeCategoryDialog("Algorithms notes", ["Math"], "Math", localization);
        dialog.Show();

        try
        {
            var okButton = dialog.FindControl<Button>("OkButton");
            var cancelButton = dialog.FindControl<Button>("CancelButton");
            Assert.NotNull(okButton);
            Assert.NotNull(cancelButton);
            Assert.True(okButton!.IsDefault);
            Assert.True(cancelButton!.IsCancel);
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void SelectCollectionDialog_MarksOkDefaultAndCancelCancel()
    {
        var localization = GetLocalization();
        var dialog = new SelectCollectionDialog("Algorithms notes", [(1, "Study", 1)], localization);
        dialog.Show();

        try
        {
            var okButton = dialog.FindControl<Button>("OkButton");
            var cancelButton = dialog.FindControl<Button>("CancelButton");
            Assert.NotNull(okButton);
            Assert.NotNull(cancelButton);
            Assert.True(okButton!.IsDefault);
            Assert.True(cancelButton!.IsCancel);
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void AddToCollectionDialog_MarksConfirmDefaultAndCancelCancel()
    {
        var localization = GetLocalization();
        var dialog = new AddToCollectionDialog(
            [new StudyDocument { Id = 1, Name = "Guide" }],
            [],
            "Collection",
            localization);
        dialog.Show();

        try
        {
            var confirmButton = dialog.FindControl<Button>("ConfirmButton");
            var cancelButton = dialog.FindControl<Button>("CancelButton");
            Assert.NotNull(confirmButton);
            Assert.NotNull(cancelButton);
            Assert.True(confirmButton!.IsDefault);
            Assert.True(cancelButton!.IsCancel);
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void AffectedItemsPreviewDialog_FocusesCancelButtonOnOpen()
    {
        var localization = GetLocalization();
        var dialog = new AffectedItemsPreviewDialog("Title", 1, ["Doc"], "note", localization);
        dialog.Show();

        try
        {
            FlushAvaloniaBindings();

            var cancelButton = dialog.FindControl<Button>("CancelButton");
            Assert.NotNull(cancelButton);
            Assert.Same(cancelButton, dialog.FocusManager?.GetFocusedElement());
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void BulkEditPreviewDialog_FocusesCancelButtonOnOpen()
    {
        var localization = GetLocalization();
        var dialog = new BulkEditPreviewDialog(1, [("Field", "Value")], localization);
        dialog.Show();

        try
        {
            FlushAvaloniaBindings();

            var cancelButton = dialog.FindControl<Button>("CancelButton");
            Assert.NotNull(cancelButton);
            Assert.Same(cancelButton, dialog.FocusManager?.GetFocusedElement());
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void AddDocumentDialog_NameValidationExposesHelpText()
    {
        var localization = GetLocalization();
        var dialog = new AddDocumentDialog("C:/drop/test.pdf", ["Study"], ["PDF"]);
        dialog.Show();

        try
        {
            FlushAvaloniaBindings();

            var saveButton = dialog.FindControl<Button>("btnSave");
            var nameBox = dialog.FindControl<TextBox>("txtTen");
            Assert.NotNull(saveButton);
            Assert.NotNull(nameBox);

            nameBox!.Text = string.Empty;
            saveButton!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            FlushAvaloniaBindings();

            var helpText = nameBox.GetValue(AutomationProperties.HelpTextProperty) as string;
            Assert.Equal(localization["AddEdit_NameRequired"], helpText);
        }
        finally
        {
            dialog.Close();
        }
    }

    private static AddEditModel CreateAddEditModel(
        LocalizationService localization,
        RecordingDialogService dialogs,
        RecordingNavigationService navigation) => new(
            new SavingDocumentRepositoryStub(),
            new CategoryRepositoryStub(),
            dialogs,
            null!,
            navigation,
            localization);

    private static void FlushAvaloniaBindings()
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static LocalizationService GetLocalization()
    {
        var localization = new LocalizationService();
        Application.Current!.Resources["Loc"] = localization;
        return localization;
    }

    private sealed class RecordingDialogService : IDialogService
    {
        public List<string> Calls { get; } = [];

        public Task ShowMessageAsync(string title, string message)
        {
            Calls.Add($"message:{title}");
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(string title, string message)
        {
            Calls.Add($"error:{title}");
            return Task.CompletedTask;
        }

        public Task<bool> ShowConfirmAsync(string title, string message)
        {
            Calls.Add($"confirm:{title}");
            return Task.FromResult(true);
        }

        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
        {
            Calls.Add($"confirm:{title}:{confirmText}:{isDanger}");
            return Task.FromResult(true);
        }

        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "")
        {
            Calls.Add($"input:{title}");
            return Task.FromResult<string?>(defaultValue);
        }
    }

    private sealed class RecordingNavigationService : INavigationService
    {
        public List<string> Navigations { get; } = [];

        public bool CanGoBack => true;

        public void NavigateTo(string viewKey) => Navigations.Add(viewKey);

        public void NavigateTo(string viewKey, object? parameter) => Navigations.Add($"{viewKey}:{parameter}");

        public void GoBack() => Navigations.Add("back");
    }

    private sealed class SavingDocumentRepositoryStub : IDocumentRepository
    {
        public List<StudyDocument> GetAll() => [];
        public StudyDocument? GetById(int id) => null;
        public List<StudyDocument> Search(string keyword) => [];
        public List<StudyDocument> Filter(string subject, string type) => [];
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => [];
        public bool Add(StudyDocument document) => true;
        public bool AddWithCatalogs(StudyDocument document) => true;
        public bool Update(StudyDocument document) => true;
        public bool Delete(int id) => true;
        public List<string> GetDistinctSubjects() => [];
        public List<string> GetDistinctTypes() => [];
        public List<string> GetDistinctTags() => [];
        public List<StudyDocument> GetUpcomingDeadlines(int days) => [];
        public List<StudyDocument> GetOverdueDocuments() => [];
        public void EnsureSubjectExists(string subject) { }
        public void EnsureTypeExists(string type) { }
    }

    private sealed class CategoryRepositoryStub : ICategoryRepository
    {
        public List<string> GetAllSubjects() => ["Computer Science"];
        public List<string> GetAllTypes() => ["PDF"];
        public List<(string Name, int Count)> GetSubjectsWithCount() => [];
        public List<(string Name, int Count)> GetTypesWithCount() => [];
        public bool AddSubject(string name) => throw new NotImplementedException();
        public bool AddType(string name) => throw new NotImplementedException();
        public bool UpdateSubjectName(string oldName, string newName) => throw new NotImplementedException();
        public bool UpdateTypeName(string oldName, string newName) => throw new NotImplementedException();
        public bool DeleteDocumentsBySubject(string subjectName) => throw new NotImplementedException();
        public bool DeleteDocumentsByType(string typeName) => throw new NotImplementedException();
        public int GetTotalDocumentCount() => throw new NotImplementedException();
    }
}