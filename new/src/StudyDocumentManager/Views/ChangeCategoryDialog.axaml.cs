using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace StudyDocumentManager.Views;

/// <summary>
/// Dialog for changing a document's category (MonHoc).
/// Shows existing categories as chip pills for quick-select and an AutoCompleteBox for new input.
/// </summary>
public partial class ChangeCategoryDialog : Window
{
    public string? Result { get; private set; }

    private readonly AutoCompleteBox _categoryInput;
    private readonly WrapPanel _chipsPanel;

    public ChangeCategoryDialog() { } // XAML loader

    public ChangeCategoryDialog(string documentName, IList<string> existingCategories, string currentCategory)
    {
        InitializeComponent();

        // Set doc name label
        var nameLabel = this.FindControl<TextBlock>("DocNameLabel")!;
        nameLabel.Text = $"Tài liệu: \"{documentName}\"";

        _chipsPanel = this.FindControl<WrapPanel>("ChipsPanel")!;
        _categoryInput = this.FindControl<AutoCompleteBox>("CategoryInput")!;

        // Populate AutoComplete items
        _categoryInput.ItemsSource = existingCategories;
        _categoryInput.Text = currentCategory;

        // Build chip pills for each existing category
        foreach (var cat in existingCategories)
        {
            var chip = new Button
            {
                Content = cat,
                Classes = { "chip" }
            };
            chip.Click += (_, _) =>
            {
                _categoryInput.Text = cat;
                _categoryInput.Focus();
            };
            _chipsPanel.Children.Add(chip);
        }

        // Show a placeholder message if no categories exist
        if (existingCategories.Count == 0)
        {
            _chipsPanel.Children.Add(new TextBlock
            {
                Text = "(Chưa có danh mục nào — nhập tên mới bên dưới)",
                Classes = { "empty-state-text" }
            });
        }

        // Wire buttons
        this.FindControl<Button>("OkButton")!.Click += OkClicked;
        this.FindControl<Button>("CancelButton")!.Click += CancelClicked;

        // Focus input after window opens
        this.Opened += (_, _) =>
        {
            _categoryInput.Focus();
        };
    }

    private void OkClicked(object? sender, RoutedEventArgs e)
    {
        Result = _categoryInput.Text?.Trim();
        Close();
    }

    private void CancelClicked(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }
}
