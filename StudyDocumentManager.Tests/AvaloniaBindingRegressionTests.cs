using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

public class AvaloniaBindingRegressionTests
{
    [AvaloniaFact]
    public void AddEdit_RendersEnglishModelValues()
    {
        var localization = GetLocalization();
        var model = new AddEditModel(
            null!,
            new CategoryRepositoryStub(),
            null!,
            null!,
            null!,
            localization)
        {
            Name = "Algorithms notes",
            Subject = "Computer Science",
            Type = "PDF",
            FilePath = "C:/study/algorithms.pdf",
            Author = "Ada",
            Notes = "Read chapter three",
            IsImportant = true
        };

        var view = new AddEdit { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();

            Assert.Contains(
                view.GetVisualDescendants().OfType<TextBox>(),
                textBox => textBox.Text == model.Name);
            Assert.Contains(
                view.GetVisualDescendants().OfType<TextBox>(),
                textBox => textBox.Text == model.FilePath);
            Assert.Contains(
                view.GetVisualDescendants().OfType<TextBox>(),
                textBox => textBox.Text == model.Author);
            Assert.Contains(
                view.GetVisualDescendants().OfType<TextBox>(),
                textBox => textBox.Text == model.Notes);
            Assert.Contains(
                view.GetVisualDescendants().OfType<ComboBox>(),
                comboBox => Equals(comboBox.SelectedItem, model.Subject));
            Assert.Contains(
                view.GetVisualDescendants().OfType<ComboBox>(),
                comboBox => Equals(comboBox.SelectedItem, model.Type));
            Assert.Contains(
                view.GetVisualDescendants().OfType<CheckBox>(),
                checkBox => checkBox.IsChecked == model.IsImportant);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AddEdit_EditorialWorkspace_PreservesBindingsAndActions()
    {
        var localization = GetLocalization();
        var model = new AddEditModel(
            null!, new CategoryRepositoryStub(), null!, null!, null!, localization)
        {
            Name = "Algorithms notes",
            Subject = "Computer Science",
            Type = "PDF",
            FilePath = "C:/study/algorithms.pdf",
            Author = "Ada",
            Tags = "algorithms",
            Notes = "Read chapter three",
            IsImportant = true
        };
        var view = new AddEdit { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            FlushAvaloniaBindings();

            Assert.Same(model, view.DataContext);
            Assert.Equal(model.Name, view.FindControl<TextBox>("txtName")!.Text);
            Assert.Equal(model.FilePath, view.FindControl<TextBox>("txtFilePath")!.Text);
            Assert.Equal(model.Author, view.FindControl<TextBox>("txtAuthor")!.Text);
            Assert.Equal(model.Tags, view.FindControl<TextBox>("txtTags")!.Text);
            Assert.Equal(model.Notes, view.FindControl<TextBox>("txtNotes")!.Text);
            Assert.True(view.FindControl<CheckBox>("chkImportant")!.IsChecked);
            Assert.Same(model.SaveCommand, view.FindControl<Button>("btnSave")!.Command);
            Assert.Same(model.CancelCommand, view.FindControl<Button>("btnCancel")!.Command);
            Assert.Same(model.BrowseFileCommand, view.FindControl<Button>("btnBrowse")!.Command);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task AddEdit_EditorialWorkspace_PreservesAccessibilityAndValidationFocus()
    {
        var localization = GetLocalization();
        var model = new AddEditModel(
            null!, new CategoryRepositoryStub(), null!, null!, null!, localization);
        var view = new AddEdit { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            FlushAvaloniaBindings();

            var nameBox = view.FindControl<TextBox>("txtName")!;
            var filePathBox = view.FindControl<TextBox>("txtFilePath")!;
            var browseButton = view.FindControl<Button>("btnBrowse")!;

            Assert.Equal(localization["AddEdit_LblDocName"],
                nameBox.GetValue(AutomationProperties.NameProperty));
            Assert.Equal(localization["AddEdit_LblFilePath"],
                filePathBox.GetValue(AutomationProperties.NameProperty));
            Assert.Equal(localization["AddEdit_BtnBrowse"],
                browseButton.GetValue(AutomationProperties.NameProperty));

            model.Name = string.Empty;
            model.SaveCommand.Execute(null);
            await model.SaveCommand.ExecutionTask!;
            FlushAvaloniaBindings();

            Assert.True(model.HasNameValidationError);
            Assert.Same(nameBox, TopLevel.GetTopLevel(view)?.FocusManager?.GetFocusedElement());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AddEdit_ShowsInlineErrorTextWhenValidationFails()
    {
        var localization = GetLocalization();
        var model = new AddEditModel(
            null!,
            new CategoryRepositoryStub(),
            null!,
            null!,
            null!,
            localization);

        var view = new AddEdit { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            model.NameValidationMessage = "Document name is required";
            model.HasNameValidationError = true;

            var nameBox = view.FindControl<TextBox>("txtName");
            Assert.NotNull(nameBox);
            Assert.Same(nameBox, TopLevel.GetTopLevel(view)?.FocusManager?.GetFocusedElement());
            Assert.Contains(
                view.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == model.NameValidationMessage);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AddDocumentDialog_OpensWithNameFocus()
    {
        GetLocalization();
        var dialog = new AddDocumentDialog("C:/drop/test.pdf", ["Study"], ["PDF"]);
        dialog.Show();

        try
        {
            var nameBox = dialog.FindControl<TextBox>("txtTen");
            Assert.NotNull(nameBox);
            Assert.Same(nameBox, TopLevel.GetTopLevel(dialog)?.FocusManager?.GetFocusedElement());
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void AddDocumentDialog_BlankName_ShowsInlineErrorAndKeepsFocusOnName()
    {
        var localization = GetLocalization();
        var dialog = new AddDocumentDialog("C:/drop/test.pdf", ["Study"], ["PDF"]);
        dialog.Show();

        try
        {
            var nameBox = dialog.FindControl<TextBox>("txtTen");
            Assert.NotNull(nameBox);
            nameBox!.Text = " ";

            var saveMethod = typeof(AddDocumentDialog).GetMethod("OnSaveClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(saveMethod);
            saveMethod!.Invoke(dialog, [null, null!]);

            var error = dialog.FindControl<TextBlock>("txtNameError");
            Assert.NotNull(error);
            Assert.True(error!.IsVisible);
            Assert.Equal(localization["AddEdit_NameRequired"], error.Text);
            Assert.Same(nameBox, TopLevel.GetTopLevel(dialog)?.FocusManager?.GetFocusedElement());
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void ChangeCategoryDialog_RefreshesDocumentLabelWhenLanguageChanges()
    {
        var localization = GetLocalization();
        var dialog = new ChangeCategoryDialog("Algorithms notes", ["Math"], "Math", localization);
        dialog.Show();

        try
        {
            var nameLabel = dialog.FindControl<TextBlock>("DocNameLabel");
            Assert.NotNull(nameLabel);
            Assert.Equal("文書: \"Algorithms notes\"", nameLabel!.Text);

            localization.SetLanguage(Core.SupportedLanguage.English);
            FlushAvaloniaBindings();

            Assert.Equal("Document: \"Algorithms notes\"", nameLabel.Text);
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void RelatedDocuments_RendersEnglishDocumentValues()
    {
        var localization = GetLocalization();
        var document = new StudyDocument
        {
            Id = 2,
            Name = "Related document",
            Subject = "Algorithms"
        };
        var model = new RelatedDocumentsModel(null!, null!, null!, null!, localization)
        {
            DocumentName = "Main document",
            AvailableDocuments = new ObservableCollection<StudyDocument> { document }
        };
        var view = new RelatedDocuments { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();

            var texts = view.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text);
            Assert.Contains("Main document", texts);
            Assert.Contains(document.Name, texts);
            Assert.Contains(document.Subject, texts);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dashboard_DeclaresEnglishDocumentGridBindings()
    {
        GetLocalization();

        var view = new Dashboard();
        var grid = view.FindControl<DataGrid>("dgvDocuments");

        Assert.NotNull(grid);
        var nameColumn = Assert.IsType<DataGridTemplateColumn>(grid.Columns[0]);
        Assert.Equal("Name", nameColumn.SortMemberPath);
        Assert.Equal("Subject", GetPath(Assert.IsType<DataGridTextColumn>(grid.Columns[1])));
        Assert.Equal("Type", GetPath(Assert.IsType<DataGridTextColumn>(grid.Columns[2])));
        Assert.Equal("CreatedAt", GetPath(Assert.IsType<DataGridTextColumn>(grid.Columns[3])));
        Assert.Equal("FileSize", GetPath(Assert.IsType<DataGridTextColumn>(grid.Columns[4])));
    }


    [AvaloniaFact]
    public void Dashboard_SelectionDependentQuickActionsAndSearchLabelsAreAccessible()
    {
        var localization = GetLocalization();
        var model = new DashboardModel(
            null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, localization);
        var view = new Dashboard { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            FlushAvaloniaBindings();

            var copyPathButton = view.FindControl<Button>("StatusCopyPathButton");
            var openFolderButton = view.FindControl<Button>("StatusOpenFolderButton");
            var searchBox = view.GetVisualDescendants().OfType<TextBox>().Single();
            var subjectCombo = view.FindControl<ComboBox>("cboSubject");
            var typeCombo = view.FindControl<ComboBox>("cboType");

            Assert.NotNull(copyPathButton);
            Assert.NotNull(openFolderButton);
            Assert.False(copyPathButton!.IsEnabled);
            Assert.False(openFolderButton!.IsEnabled);
            Assert.Equal(localization["Dashboard_SearchPlaceholder"], searchBox.GetValue(AutomationProperties.NameProperty));
            Assert.Equal(localization["Dashboard_LblCategory"], subjectCombo!.GetValue(AutomationProperties.NameProperty));
            Assert.Equal(localization["Dashboard_LblType"], typeCombo!.GetValue(AutomationProperties.NameProperty));

            model.SelectedDocument = new StudyDocument { Id = 1, Name = "Accessible document" };
            FlushAvaloniaBindings();

            Assert.True(copyPathButton.IsEnabled);
            Assert.True(openFolderButton.IsEnabled);
        }
        finally
        {
            window.Close();
        }
    }


    [AvaloniaFact]
    public void CollectionManagement_RendersCollectionDocuments_AndBindsMembershipActions()
    {
        var localization = GetLocalization();
        var model = new CollectionManagementModel(
            null!,
            new CollectionRepositoryStub(),
            null!,
            null!,
            localization);
        var view = new CollectionManagement { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            model.SelectedCollection = Assert.Single(model.Collections);
            FlushAvaloniaBindings();

            var documentGrid = view.FindControl<ListBox>("DocumentGrid");
            Assert.NotNull(documentGrid);
            Assert.Same(model.DocumentsInCollection, documentGrid!.ItemsSource);
            Assert.Equal(SelectionMode.Multiple, documentGrid.SelectionMode);

            var texts = view.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text);
            Assert.Contains("Calculus", texts);

            var buttons = view.GetVisualDescendants().OfType<Button>().ToList();
            var addButton = Assert.Single(buttons, button => ReferenceEquals(button.Command, model.AddDocumentToCollectionCommand));
            var removeButton = Assert.Single(buttons, button => ReferenceEquals(button.Command, model.RemoveSelectedDocumentsCommand));
            Assert.True(addButton.IsEnabled);
            Assert.True(removeButton.IsEnabled);

            model.SelectedCollection = null;
            FlushAvaloniaBindings();

            Assert.False(addButton.IsEnabled);
            Assert.False(removeButton.IsEnabled);
        }
        finally
        {
            window.Close();
        }
    }


    [AvaloniaFact]
    public async Task FileIntegrityCheck_ScanCommandAndResultsBinding_RenderMissingFile()
    {
        var localization = GetLocalization();
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.pdf");
        var document = new StudyDocument { Id = 17, Name = "Missing syllabus", FilePath = missingPath };
        var model = new FileIntegrityCheckModel(
            new MissingDocumentRepository(document),
            null!,
            null!,
            null!,
            localization);
        var view = new FileIntegrityCheck { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();

            var scanButton = Assert.Single(view.GetVisualDescendants().OfType<Button>());
            Assert.Same(model.CheckIntegrityCommand, scanButton.Command);

            model.CheckIntegrityCommand.Execute(null);
            await model.CheckIntegrityCommand.ExecutionTask!;
            FlushAvaloniaBindings();

            Assert.Equal(1, model.TotalChecked);
            Assert.Equal(1, model.MissingCount);
            Assert.Single(model.Results);

            var texts = view.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text);
            Assert.Contains(document.Name, texts);
            Assert.Contains(missingPath, texts);
            Assert.Contains(model.Results[0].Status, texts);
            Assert.Contains(model.StatusText, texts);
        }
        finally
        {
            window.Close();
        }
    }


    [AvaloniaFact]
    public void AppValidationPluginCleanup_RemovesOnlyDataAnnotationsPlugin()
    {
        var validators = Avalonia.Data.Core.Plugins.BindingPlugins.DataValidators;
        var originalValidators = validators.ToList();
        var testPlugin = new Avalonia.Data.Core.Plugins.DataAnnotationsValidationPlugin();
        validators.Add(testPlugin);

        try
        {
            var cleanupMethod = typeof(StudyDocumentManager.App).GetMethod(
                "RemoveDataAnnotationsValidationPlugin",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(cleanupMethod);

            cleanupMethod!.Invoke(null, null);
            cleanupMethod.Invoke(null, null);

            Assert.DoesNotContain(validators, plugin => ReferenceEquals(plugin, testPlugin));
            foreach (var validator in originalValidators.Where(plugin => plugin is not Avalonia.Data.Core.Plugins.DataAnnotationsValidationPlugin))
                Assert.Contains(validator, validators);
        }
        finally
        {
            validators.Clear();
            foreach (var validator in originalValidators)
                validators.Add(validator);
        }
    }

    private static void FlushAvaloniaBindings()
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static string? GetPath(DataGridTextColumn column)
        => column.Binding is Avalonia.Data.Binding binding ? binding.Path : null;

    private static LocalizationService GetLocalization()
    {
        var localization = new LocalizationService();
        Application.Current!.Resources["Loc"] = localization;
        return localization;
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


    private sealed class CollectionRepositoryStub : ICollectionRepository
    {
        public List<(int Id, string Name, string? Description, DateTime CreatedAt, int ItemCount)> GetAll()
            => [(1, "Study", "Core collection", DateTime.Today, 1)];

        public int Create(string name, string? description = null) => 1;
        public bool Update(int id, string name, string? description = null) => true;
        public bool Delete(int id) => true;
        public List<StudyDocument> GetDocuments(int collectionId)
            => [new StudyDocument { Id = 7, Name = "Calculus", Subject = "Math" }];
        public bool AddDocument(int collectionId, int documentId) => true;
        public bool RemoveDocument(int collectionId, int documentId) => true;
    }


    private sealed class MissingDocumentRepository(StudyDocument document) : IDocumentRepository
    {
        public List<StudyDocument> GetAll() => [document];
        public StudyDocument? GetById(int id) => null;
        public List<StudyDocument> Search(string keyword) => [];
        public List<StudyDocument> Filter(string subject, string type) => [];
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => [];
        public bool Add(StudyDocument document) => false;
        public bool AddWithCatalogs(StudyDocument document) => false;
        public bool Update(StudyDocument document) => false;
        public bool Delete(int id) => false;
        public List<string> GetDistinctSubjects() => [];
        public List<string> GetDistinctTypes() => [];
        public List<string> GetDistinctTags() => [];
        public List<StudyDocument> GetUpcomingDeadlines(int days) => [];
        public List<StudyDocument> GetOverdueDocuments() => [];
        public void EnsureSubjectExists(string subject) { }
        public void EnsureTypeExists(string type) { }
    }
}
