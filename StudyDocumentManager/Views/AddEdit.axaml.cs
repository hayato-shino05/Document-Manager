using System.ComponentModel;
using Avalonia.Controls;
using StudyDocumentManager.Models;

namespace StudyDocumentManager.Views;

public partial class AddEdit : UserControl
{
    private AddEditModel? _model;

    public AddEdit()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelPropertyChanged;
        }

        _model = DataContext as AddEditModel;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelPropertyChanged;
        }
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
