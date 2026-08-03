using System.ComponentModel;
using Avalonia.Controls;
using StudyDocumentManager.Models;

namespace StudyDocumentManager.Views;

public partial class AddEdit : UserControl
{
    private AddEditModel? _model;
    private bool _propertyChangedSubscribed;

    public AddEdit()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width > 0 && e.NewSize.Width < 760;
        formGrid.ColumnDefinitions = narrow
            ? ColumnDefinitions.Parse("*")
            : ColumnDefinitions.Parse("*,*");
        formGrid.RowDefinitions = narrow
            ? RowDefinitions.Parse("Auto,Auto,Auto,Auto,Auto,Auto")
            : RowDefinitions.Parse("Auto,Auto,Auto,Auto");

        Grid.SetColumn(nameField, 0);
        Grid.SetColumn(filePathField, 0);
        Grid.SetColumn(notesField, 0);
        Grid.SetColumn(categoryTypeFields, narrow ? 0 : 1);
        Grid.SetColumn(authorTagsFields, narrow ? 0 : 1);
        Grid.SetColumn(deadlineImportantFields, narrow ? 0 : 1);

        if (narrow)
        {
            Grid.SetRow(nameField, 0);
            Grid.SetRow(filePathField, 1);
            Grid.SetRow(categoryTypeFields, 2);
            Grid.SetRow(authorTagsFields, 3);
            Grid.SetRow(deadlineImportantFields, 4);
            Grid.SetRow(notesField, 5);
            Grid.SetRowSpan(notesField, 1);
        }
        else
        {
            Grid.SetRow(nameField, 0);
            Grid.SetRow(filePathField, 1);
            Grid.SetRow(categoryTypeFields, 0);
            Grid.SetRow(authorTagsFields, 1);
            Grid.SetRow(deadlineImportantFields, 2);
            Grid.SetRow(notesField, 2);
            Grid.SetRowSpan(notesField, 2);
        }
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        DetachModelHandlers();

        _model = DataContext as AddEditModel;
        if (VisualRoot is not null)
        {
            _model?.AttachLocalization();
            AttachPropertyChangedHandler();
        }
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _model?.AttachLocalization();
        AttachPropertyChangedHandler();
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
        => DetachModelHandlers();

    private void AttachPropertyChangedHandler()
    {
        if (_model is null || _propertyChangedSubscribed)
            return;

        _model.PropertyChanged += OnModelPropertyChanged;
        _propertyChangedSubscribed = true;
    }

    private void DetachModelHandlers()
    {
        if (_model is null)
            return;

        if (!_propertyChangedSubscribed)
            return;

        _model.PropertyChanged -= OnModelPropertyChanged;
        _propertyChangedSubscribed = false;
        _model.DetachLocalization();
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AddEditModel.HasNameValidationError)
            && sender is AddEditModel { HasNameValidationError: true })
        {
            this.FindControl<TextBox>("txtName")?.Focus();
        }
    }
}
