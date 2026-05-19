using Avalonia.Controls;
using Avalonia.Interactivity;

namespace StudyDocumentManager.Views;

/// <summary>
/// Dialog đổi danh mục (MonHoc) cho tài liệu.
/// Hiển thị danh sách danh mục hiện có dạng chip (binding-driven) và AutoCompleteBox để nhập mới.
/// </summary>
public partial class ChangeCategoryDialog : Window
{
    public string? Result { get; private set; }

    private AutoCompleteBox? _categoryInput;

    public ChangeCategoryDialog() { } // XAML loader

    public ChangeCategoryDialog(string documentName, IList<string> existingCategories, string currentCategory)
    {
        InitializeComponent();

        // Gán nhãn tài liệu
        var nameLabel = this.FindControl<TextBlock>("DocNameLabel")!;
        nameLabel.Text = $"Tài liệu: \"{documentName}\"";

        _categoryInput = this.FindControl<AutoCompleteBox>("CategoryInput")!;
        _categoryInput.ItemsSource = existingCategories;
        _categoryInput.Text = currentCategory;

        // Binding-driven chip list — không cần tạo Button thủ công
        var chipsPanel = this.FindControl<ItemsControl>("ChipsPanel")!;
        chipsPanel.ItemsSource = existingCategories;

        // Hiện empty state nếu không có danh mục
        var emptyState = this.FindControl<TextBlock>("EmptyStateText")!;
        emptyState.IsVisible = existingCategories.Count == 0;
        chipsPanel.IsVisible  = existingCategories.Count > 0;

        // Wire buttons
        this.FindControl<Button>("OkButton")!.Click     += OkClicked;
        this.FindControl<Button>("CancelButton")!.Click += CancelClicked;

        // Focus input khi mở
        this.Opened += (_, _) => _categoryInput.Focus();
    }

    // Được gọi từ AXAML ItemTemplate DataTemplate Click="OnChipClicked"
    private void OnChipClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: string category })
        {
            _categoryInput!.Text = category;
            _categoryInput.Focus();
        }
    }

    private void OkClicked(object? sender, RoutedEventArgs e)
    {
        Result = _categoryInput?.Text?.Trim();
        Close();
    }

    private void CancelClicked(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }
}
