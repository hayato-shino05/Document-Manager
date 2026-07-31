using Avalonia.Controls;
using Avalonia.Interactivity;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Views;

/// <summary>
/// カテゴリ変更ダイアログ（MonHoc）。
/// 既存カテゴリをチップ表示し、AutoCompleteBoxで新規入力可能
/// </summary>
public partial class ChangeCategoryDialog : Window
{
    public string? Result { get; private set; }

    private readonly ILocalizationService? _loc;
    private readonly string _documentName = string.Empty;
    private AutoCompleteBox? _categoryInput;

    public ChangeCategoryDialog() { } // XAML loader

    public ChangeCategoryDialog(string documentName, IList<string> existingCategories, string currentCategory, ILocalizationService? loc = null)
    {
        InitializeComponent();

        _loc = loc;
        _documentName = documentName;
        UpdateDocumentLabel();
        if (_loc != null)
        {
            _loc.LanguageChanged += OnLanguageChanged;
            Closed += (_, _) => _loc.LanguageChanged -= OnLanguageChanged;
        }

        _categoryInput = this.FindControl<AutoCompleteBox>("CategoryInput")!;
        _categoryInput.ItemsSource = existingCategories;
        _categoryInput.Text = currentCategory;

        // チップリスト（バインディング駆動）
        var chipsPanel = this.FindControl<ItemsControl>("ChipsPanel")!;
        chipsPanel.ItemsSource = existingCategories;

        // カテゴリ空の場合はエンプティステートを表示
        var emptyState = this.FindControl<TextBlock>("EmptyStateText")!;
        emptyState.IsVisible = existingCategories.Count == 0;
        chipsPanel.IsVisible  = existingCategories.Count > 0;

        // Wire buttons
        this.FindControl<Button>("OkButton")!.Click     += OkClicked;
        this.FindControl<Button>("CancelButton")!.Click += CancelClicked;

        // ダイアログ表示時にインプットへフォーカス
        this.Opened += (_, _) => _categoryInput.Focus();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
        => UpdateDocumentLabel();

    private void UpdateDocumentLabel()
    {
        var nameLabel = this.FindControl<TextBlock>("DocNameLabel");
        if (nameLabel == null)
            return;

        nameLabel.Text = _loc == null
            ? $"Document: \"{_documentName}\""
            : string.Format(_loc["ChangeCategory_DocumentLabel"], _documentName);
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
