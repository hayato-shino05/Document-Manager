using Avalonia.Controls;
using Avalonia.Interactivity;
using StudyDocumentManager.Models.Items;

namespace StudyDocumentManager.Views;

/// <summary>
/// Dialog chọn bộ sưu tập.
/// Hiển thị danh sách bộ sưu tập dạng chip (binding-driven) — click để chọn, confirm để xác nhận.
/// Trả về Id được chọn qua <see cref="Result"/>, hoặc -1 nếu huỷ.
/// </summary>
public partial class SelectCollectionDialog : Window
{
    public int Result { get; private set; } = -1;

    private readonly SelectCollectionDialogState _state;

    private Button? _activeChip;

    public SelectCollectionDialog()
    {
        _state = new SelectCollectionDialogState([]);
    }

    public SelectCollectionDialog(
        string documentName,
        IList<(int Id, string Name, int DocCount)> collections)
    {
        InitializeComponent();
        _state = new SelectCollectionDialogState(collections);

        // Gán nhãn tài liệu
        this.FindControl<TextBlock>("DocNameLabel")!.Text = $"Tài liệu: \"{documentName}\"";

        // Binding-driven chip list
        var chipsPanel  = this.FindControl<ItemsControl>("ChipsPanel")!;
        var emptyState  = this.FindControl<TextBlock>("EmptyStateText")!;

        if (collections.Count == 0)
        {
            emptyState.IsVisible  = true;
            chipsPanel.IsVisible  = false;
        }
        else
        {
            var items = collections
                .Select(c => new CollectionChipItem(c.Id, c.Name, c.DocCount))
                .ToList();
            chipsPanel.ItemsSource = items;
        }

        // Wire buttons
        this.FindControl<Button>("OkButton")!.Click     += OkClicked;
        this.FindControl<Button>("CancelButton")!.Click += CancelClicked;
    }

    // Được gọi từ AXAML DataTemplate Click="OnChipClicked"
    private void OnChipClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button chip || chip.DataContext is not CollectionChipItem item)
            return;

        // Bỏ chọn chip cũ
        if (_activeChip != null)
        {
            _activeChip.Classes.Remove("chip-selected");
            _activeChip.Classes.Add("chip");
        }

        // Chọn chip mới
        chip.Classes.Remove("chip");
        chip.Classes.Add("chip-selected");
        _activeChip = chip;

        _state.Select(item.Id);

        // Cập nhật footer
        this.FindControl<TextBlock>("SelectedLabel")!.Text   = _state.SelectedLabel;
        this.FindControl<Button>("OkButton")!.IsEnabled      = _state.CanConfirm;
    }

    private void OkClicked(object? sender, RoutedEventArgs e)
    {
        Result = _state.SelectedId;
        Close();
    }

    private void CancelClicked(object? sender, RoutedEventArgs e)
    {
        Result = -1;
        Close();
    }
}
