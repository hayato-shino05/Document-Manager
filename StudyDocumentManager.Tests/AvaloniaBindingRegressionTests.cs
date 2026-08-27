using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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
    public void AddEdit_EditModeUsesEditHeaderIcon()
    {
        var localization = GetLocalization();
        var document = new StudyDocument
        {
            Id = 42,
            Name = "Algorithms notes",
            Subject = "Computer Science",
            Type = "PDF",
            FilePath = "C:/study/algorithms.pdf"
        };
        var model = new AddEditModel(
            new DocumentRepositoryStub(document, returnDocument: true),
            new CategoryRepositoryStub(),
            null!, null!, null!, localization);
        var view = new AddEdit { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            model.LoadDocument(document.Id);
            window.Show();
            FlushAvaloniaBindings();

            Assert.True(model.IsEditing);
            var addHeaderIcon = view.FindControl<Image>("addHeaderIcon")!;
            var editHeaderIcon = view.FindControl<Image>("editHeaderIcon")!;
            Assert.False(addHeaderIcon.IsVisible);
            Assert.True(editHeaderIcon.IsVisible);
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
    public void AddEdit_UsesWorkflowTabOrderAndValidationDescription()
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
            var expected = new Control[]
            {
                nameBox,
                view.FindControl<TextBox>("txtFilePath")!,
                view.FindControl<Button>("btnBrowse")!,
                view.FindControl<ComboBox>("cmbCategory")!,
                view.FindControl<ComboBox>("cmbType")!,
                view.FindControl<TextBox>("txtAuthor")!,
                view.FindControl<TextBox>("txtTags")!,
                view.FindControl<DatePicker>("dateDeadline")!,
                view.FindControl<CheckBox>("chkImportant")!,
                view.FindControl<TextBox>("txtNotes")!,
                view.FindControl<Button>("btnSave")!,
                view.FindControl<Button>("btnCancel")!
            };

            var browseButton = view.FindControl<Button>("btnBrowse")!;
            Assert.Same(model.BrowseFileCommand, browseButton.Command);

            Assert.True(nameBox.Focus());
            var topLevel = TopLevel.GetTopLevel(view)!;
            foreach (var expectedControl in expected)
            {
                Assert.Same(expectedControl, topLevel.FocusManager?.GetFocusedElement());
                topLevel.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.None, null);
            }

            model.NameValidationMessage = "Document name is required";
            model.HasNameValidationError = true;
            FlushAvaloniaBindings();

            var nameError = view.FindControl<TextBlock>("txtNameError")!;
            Assert.Equal(model.NameValidationMessage,
                nameBox.GetValue(AutomationProperties.HelpTextProperty));
            Assert.Equal(model.NameValidationMessage,
                nameError.GetValue(AutomationProperties.NameProperty));
            Assert.Equal(AutomationLiveSetting.Polite,
                nameError.GetValue(AutomationProperties.LiveSettingProperty));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AddEdit_LocalizedMarkupRefreshesAfterLanguageChanges()
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

            var title = Assert.Single(view.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == localization["AddEdit_PageTitleAdd"]);
            var nameLabel = Assert.Single(view.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == localization["AddEdit_LblDocName"]);
            var nameBox = view.FindControl<TextBox>("txtName")!;
            var browseButton = view.FindControl<Button>("btnBrowse")!;
            var saveButton = view.FindControl<Button>("btnSave")!;
            var cancelButton = view.FindControl<Button>("btnCancel")!;
            var datePicker = view.FindControl<DatePicker>("dateDeadline")!;
            var browseText = Assert.Single(browseButton.GetVisualDescendants().OfType<TextBlock>());
            var saveText = Assert.Single(saveButton.GetVisualDescendants().OfType<TextBlock>());
            var cancelText = Assert.Single(cancelButton.GetVisualDescendants().OfType<TextBlock>());

            Assert.Equal(localization["AddEdit_LblDocName"], nameBox.GetValue(AutomationProperties.NameProperty));
            Assert.Equal(localization["AddEdit_BtnBrowse"], browseText.Text);
            Assert.Equal(localization["AddEdit_BtnSave"], saveText.Text);
            Assert.Equal(localization["AddEdit_BtnCancel"], cancelText.Text);
            Assert.Equal(localization["AddEdit_DateYearFormat"], datePicker.YearFormat);
            Assert.Equal(localization["AddEdit_DateMonthFormat"], datePicker.MonthFormat);
            Assert.Equal(localization["AddEdit_DateDayFormat"], datePicker.DayFormat);

            localization.SetLanguage(Core.SupportedLanguage.English);
            FlushAvaloniaBindings();

            Assert.Equal(localization["AddEdit_PageTitleAdd"], title.Text);
            Assert.Equal(localization["AddEdit_LblDocName"], nameLabel.Text);
            Assert.Equal(localization["AddEdit_LblDocName"], nameBox.GetValue(AutomationProperties.NameProperty));
            Assert.Equal(localization["AddEdit_BtnBrowse"], browseText.Text);
            Assert.Equal(localization["AddEdit_BtnSave"], saveText.Text);
            Assert.Equal(localization["AddEdit_BtnCancel"], cancelText.Text);
            Assert.Equal(localization["AddEdit_DateYearFormat"], datePicker.YearFormat);
            Assert.Equal(localization["AddEdit_DateMonthFormat"], datePicker.MonthFormat);
            Assert.Equal(localization["AddEdit_DateDayFormat"], datePicker.DayFormat);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AddEdit_NarrowWidthKeepsFormControlsInsideArrangedView()
    {
        var localization = GetLocalization();
        var model = new AddEditModel(
            null!, new CategoryRepositoryStub(), null!, null!, null!, localization)
        {
            Name = "日本語の長い文書名と学習ノート",
            FilePath = "C:/study/非常に長いフォルダー名/アルゴリズムとデータ構造の講義資料.pdf",
            Author = "著者名",
            Tags = "アルゴリズム,データ構造",
            Notes = "狭い画面でも入力欄が縦方向に積み重なり、長い文字列が別の項目へ重ならないことを確認します。"
        };
        var view = new AddEdit { DataContext = model };
        var window = new Window { Width = 520, Height = 720, Content = view };

        try
        {
            window.Show();
            FlushAvaloniaBindings();

            var formGrid = view.FindControl<Grid>("formGrid")!;
            Assert.Single(formGrid.ColumnDefinitions);
            Assert.Equal(6, formGrid.RowDefinitions.Count);
            Assert.Equal(0, Grid.GetColumn(view.FindControl<StackPanel>("nameField")!));
            Assert.Equal(1, Grid.GetRow(view.FindControl<StackPanel>("filePathField")!));
            Assert.Equal(2, Grid.GetRow(view.FindControl<Grid>("categoryTypeFields")!));
            Assert.Equal(3, Grid.GetRow(view.FindControl<Grid>("authorTagsFields")!));
            Assert.Equal(4, Grid.GetRow(view.FindControl<Grid>("deadlineImportantFields")!));
            Assert.Equal(5, Grid.GetRow(view.FindControl<StackPanel>("notesField")!));

            Assert.True(view.Bounds.Width > 0);
            Assert.True(view.Bounds.Height > 0);

            var controls = new Control[]
            {
                FindControl<TextBox>(view, "txtName"),
                FindControl<TextBox>(view, "txtFilePath"),
                FindControl<TextBox>(view, "txtAuthor"),
                FindControl<TextBox>(view, "txtTags"),
                FindControl<TextBox>(view, "txtNotes"),
                FindControl<Button>(view, "btnBrowse"),
                FindControl<Button>(view, "btnSave"),
                FindControl<Button>(view, "btnCancel"),
                FindControl<CheckBox>(view, "chkImportant")
            };

            foreach (var control in controls)
            {
                AssertInsideView(view, control);
            }

            var scrollViewer = view.GetVisualDescendants().OfType<ScrollViewer>()
                .OrderByDescending(candidate => candidate.Bounds.Width * candidate.Bounds.Height)
                .First();
            Assert.True(scrollViewer.Bounds.Width > 0);
            Assert.True(scrollViewer.Bounds.Height > 0);
            AssertInsideView(view, scrollViewer);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AddEdit_ResponsiveWidthsKeepGridAndControlsInBoundsAcrossTransitions()
    {
        var localization = GetLocalization();
        var model = new AddEditModel(
            null!, new CategoryRepositoryStub(), null!, null!, null!, localization)
        {
            Name = "Algorithms notes",
            FilePath = "C:/study/algorithms.pdf",
            Author = "Ada",
            Tags = "algorithms",
            Notes = "Responsive layout proof"
        };
        var view = new AddEdit { DataContext = model };
        var window = new Window { Width = 1024, Height = 900, Content = view };

        try
        {
            window.Show();
            FlushAvaloniaBindings();

            void AssertLayout(int width, bool narrow)
            {
                window.Width = width;
                FlushAvaloniaBindings();

                var formGrid = FindControl<Grid>(view, "formGrid");
                Assert.Equal(narrow ? 1 : 2, formGrid.ColumnDefinitions.Count);
                Assert.Equal(narrow ? 6 : 4, formGrid.RowDefinitions.Count);

                var nameField = FindControl<StackPanel>(view, "nameField");
                var filePathField = FindControl<StackPanel>(view, "filePathField");
                var categoryTypeFields = FindControl<Grid>(view, "categoryTypeFields");
                var authorTagsFields = FindControl<Grid>(view, "authorTagsFields");
                var deadlineImportantFields = FindControl<Grid>(view, "deadlineImportantFields");
                var notesField = FindControl<StackPanel>(view, "notesField");

                Assert.Equal(0, Grid.GetColumn(nameField));
                Assert.Equal(0, Grid.GetColumn(filePathField));
                Assert.Equal(narrow ? 0 : 1, Grid.GetColumn(categoryTypeFields));
                Assert.Equal(narrow ? 0 : 1, Grid.GetColumn(authorTagsFields));
                Assert.Equal(narrow ? 0 : 1, Grid.GetColumn(deadlineImportantFields));
                Assert.Equal(0, Grid.GetColumn(notesField));

                Assert.Equal(0, Grid.GetRow(nameField));
                Assert.Equal(1, Grid.GetRow(filePathField));
                Assert.Equal(narrow ? 2 : 0, Grid.GetRow(categoryTypeFields));
                Assert.Equal(narrow ? 3 : 1, Grid.GetRow(authorTagsFields));
                Assert.Equal(narrow ? 4 : 2, Grid.GetRow(deadlineImportantFields));
                Assert.Equal(narrow ? 5 : 2, Grid.GetRow(notesField));
                Assert.Equal(narrow ? 1 : 2, Grid.GetRowSpan(notesField));

                foreach (var name in new[]
                {
                    "txtName", "txtFilePath", "txtAuthor", "txtTags", "txtNotes",
                    "btnBrowse", "btnSave", "btnCancel", "chkImportant"
                })
                    AssertInsideView(view, FindControl<Control>(view, name));
            }

            AssertLayout(759, true);
            AssertLayout(760, false);
            AssertLayout(1024, false);

            AssertLayout(760, false);
            AssertLayout(520, true);
            AssertLayout(1024, false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AddEdit_LongMultilingualValuesKeepActionsReachableAtSmallestWidth()
    {
        var localization = GetLocalization();
        var model = new AddEditModel(
            null!, new CategoryRepositoryStub(), null!, null!, null!, localization)
        {
            Name = "日本語の非常に長い文書名と学習ノートを使った境界テストです",
            FilePath = "C:/study/非常に長いフォルダー名/算法与数据结构/" + new string('文', 80) + ".pdf",
            Author = "Tác giả Việt Nam với tên và mô tả dài để kiểm tra bố cục",
            Tags = "学习资料,数据结构,算法,边界测试",
            Notes = "日本語、Tiếng Việt、中文の長い入力が狭い画面で折り返されてもフォームを壊さないことを確認します。"
        };
        var view = new AddEdit { DataContext = model };
        var window = new Window { Width = 520, Height = 720, Content = view };

        try
        {
            window.Show();
            FlushAvaloniaBindings();

            var scrollViewer = view.GetVisualDescendants().OfType<ScrollViewer>()
                .OrderByDescending(candidate => candidate.Bounds.Width * candidate.Bounds.Height)
                .First();
            Assert.True(scrollViewer.Bounds.Width > 0);
            Assert.True(scrollViewer.Bounds.Height > 0);
            Assert.Equal(Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                scrollViewer.VerticalScrollBarVisibility);
            AssertInsideView(view, scrollViewer);

            foreach (var language in new[]
            {
                Core.SupportedLanguage.Japanese,
                Core.SupportedLanguage.Vietnamese,
                Core.SupportedLanguage.Chinese
            })
            {
                localization.SetLanguage(language);
                FlushAvaloniaBindings();
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(),
                    text => text.Text == localization["AddEdit_LblDocName"]);
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(),
                    text => text.Text == localization["AddEdit_LblFilePath"]);
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(),
                    text => text.Text == localization["AddEdit_LblNotes"]);
            }

            foreach (var name in new[]
            {
                "txtName", "txtFilePath", "txtAuthor", "txtTags", "txtNotes",
                "btnBrowse", "btnSave", "btnCancel", "chkImportant"
            })
                AssertInsideView(view, FindControl<Control>(view, name));

            Assert.Equal(model.FilePath, FindControl<TextBox>(view, "txtFilePath").Text);
            Assert.False(string.IsNullOrWhiteSpace(FindControl<Button>(view, "btnBrowse")
                .GetVisualDescendants().OfType<TextBlock>().Single().Text));
            Assert.False(string.IsNullOrWhiteSpace(FindControl<Button>(view, "btnSave")
                .GetVisualDescendants().OfType<TextBlock>().Single().Text));
            Assert.False(string.IsNullOrWhiteSpace(FindControl<Button>(view, "btnCancel")
                .GetVisualDescendants().OfType<TextBlock>().Single().Text));

            window.Height = 420;
            FlushAvaloniaBindings();
            Assert.True(scrollViewer.Extent.Height > scrollViewer.Viewport.Height);
            scrollViewer.Offset = new Vector(0, 1);
            FlushAvaloniaBindings();
            Assert.True(scrollViewer.Offset.Y > 0);
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
    public void AddDocumentDialog_LongPathStaysInsideViewport()
    {
        GetLocalization();
        var longPath = "C:/Users/ADMIN/Downloads/" + new string('長', 80) + ".docx";
        var dialog = new AddDocumentDialog(longPath, ["Study"], ["Word"]);
        dialog.Show();

        try
        {
            var pathText = dialog.FindControl<TextBlock>("txtFilePath");

            Assert.NotNull(pathText);
            Assert.True(pathText!.Bounds.Right <= dialog.Bounds.Width + 1);
        }
        finally
        {
            dialog.Close();
        }
    }


    [AvaloniaFact]
    public void AddDocumentDialog_DeadlineStaysInsideViewport()
    {
        GetLocalization();
        var dialog = new AddDocumentDialog("C:/drop/test.pdf", ["Study"], ["PDF"]);
        dialog.Show();

        try
        {
            var deadlinePicker = dialog.FindControl<DatePicker>("dpDeadline");

            Assert.NotNull(deadlinePicker);
            Assert.True(deadlinePicker!.Bounds.Bottom <= dialog.Bounds.Height + 1);
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
    public void AddDocumentDialog_PopulatesSubjectCatalog()
    {
        GetLocalization();
        var dialog = new AddDocumentDialog("C:/drop/test.pdf", ["Study", "Finance"], ["PDF"]);
        dialog.Show();

        try
        {
            var subjectBox = dialog.FindControl<ComboBox>("cboMonHoc");

            Assert.NotNull(subjectBox);
            Assert.Equal(["Study", "Finance"], subjectBox!.Items.Cast<string>().ToArray());
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void ChangeCategoryDialog_WrapsCategoriesAndKeepsActionsVisible()
    {
        var localization = GetLocalization();
        var categories = Enumerable.Range(1, 12)
            .Select(index => $"Category {index}")
            .ToList();
        var dialog = new ChangeCategoryDialog("Algorithms notes", categories, "Category 1", localization);
        dialog.Show();

        try
        {
            var saveButton = dialog.FindControl<Button>("OkButton");
            var cancelButton = dialog.FindControl<Button>("CancelButton");

            Assert.NotNull(saveButton);
            Assert.NotNull(cancelButton);
            Assert.True(saveButton!.Bounds.Right <= dialog.Bounds.Width + 1);
            Assert.True(cancelButton!.Bounds.Right <= dialog.Bounds.Width + 1);
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
        var subjectColumn = Assert.IsType<DataGridTemplateColumn>(grid.Columns[1]);
        Assert.Equal("Subject", subjectColumn.SortMemberPath);
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

            var selectionButtons = view.GetVisualDescendants().OfType<Button>().ToList();
            var selectAllButton = selectionButtons.Single(button =>
                AutomationProperties.GetAutomationId(button) == "CollectionManagement_SelectAllDocuments");
            var deselectAllButton = selectionButtons.Single(button =>
                AutomationProperties.GetAutomationId(button) == "CollectionManagement_DeselectAllDocuments");

            selectAllButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            FlushAvaloniaBindings();
            Assert.Equal(model.DocumentsInCollection.Count, model.SelectedDocumentsInCollection.Count);

            deselectAllButton!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            FlushAvaloniaBindings();
            Assert.Empty(model.SelectedDocumentsInCollection.Cast<StudyDocument>());

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
    public void AddToCollectionDialog_ItemCheckboxUpdatesConfirmState()
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
            FlushAvaloniaBindings();
            var checkbox = dialog.GetVisualDescendants().OfType<CheckBox>()
                .Single(control => AutomationProperties.GetName(control) == "Guide");
            var confirmButton = dialog.FindControl<Button>("ConfirmButton");

            Assert.NotNull(confirmButton);
            Assert.False(confirmButton!.IsEnabled);

            checkbox.IsChecked = true;
            FlushAvaloniaBindings();

            Assert.True(confirmButton.IsEnabled);
        }
        finally
        {
            dialog.Close();
        }
    }


    [AvaloniaFact]
    public async Task FileIntegrityCheck_ScanCommandAndResultsBinding_RenderMissingFile()
    {
        var localization = GetLocalization();
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.pdf");
        var document = new StudyDocument { Id = 17, Name = "Missing syllabus", FilePath = missingPath };
        var model = new FileIntegrityCheckModel(
            new DocumentRepositoryStub(document, returnDocument: false),
            null!,
            null!,
            null!,
            localization);
        var view = new FileIntegrityCheck { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();

            var scanButton = view.GetVisualDescendants().OfType<Button>()
                .Single(button => AutomationProperties.GetAutomationId(button) == "FileIntegrity_Scan");
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

    private static T FindControl<T>(AddEdit view, string name)
        where T : Control
    {
        var control = view.FindControl<T>(name);
        Assert.NotNull(control);
        return control;
    }

    private static void AssertInsideView(AddEdit view, Control control)
    {
        Assert.True(control.Bounds.Width > 0);
        Assert.True(control.Bounds.Height > 0);

        var origin = control.TranslatePoint(new Point(0, 0), view);
        Assert.True(origin.HasValue);

        var bounds = new Rect(origin!.Value, control.Bounds.Size);
        Assert.True(bounds.X >= -0.5);
        Assert.True(bounds.Y >= -0.5);
        Assert.True(bounds.Right <= view.Bounds.Width + 0.5);
        Assert.True(bounds.Bottom <= view.Bounds.Height + 0.5);
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


    private sealed class DocumentRepositoryStub(StudyDocument document, bool returnDocument) : IDocumentRepository
    {
        public List<StudyDocument> GetAll() => [document];
        public StudyDocument? GetById(int id) => returnDocument && id == document.Id ? document : null;
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
