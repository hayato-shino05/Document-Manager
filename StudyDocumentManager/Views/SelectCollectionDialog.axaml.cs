using Avalonia.Controls;
using Avalonia.Interactivity;
using StudyDocumentManager.Models.Items;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Views;

/// <summary>
/// Collection selection dialog with chip-based UI
/// </summary>
public partial class SelectCollectionDialog : Window
{
    public int Result { get; private set; } = -1;

    private readonly SelectCollectionDialogState _state;
    private readonly ILocalizationService _loc;

    private Button? _activeChip;

    public SelectCollectionDialog()
    {
        _loc = null!;
        _state = new SelectCollectionDialogState([], _loc);
    }

    public SelectCollectionDialog(
        string documentName,
        IList<(int Id, string Name, int DocCount)> collections,
        ILocalizationService loc)
    {
        InitializeComponent();
        _loc = loc;
        _state = new SelectCollectionDialogState(collections, loc);

        this.FindControl<TextBlock>("DocNameLabel")!.Text = $"{_loc["Field_Name"]}: \"{documentName}\"";

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

        this.FindControl<Button>("OkButton")!.Click     += OkClicked;
        this.FindControl<Button>("CancelButton")!.Click += CancelClicked;
    }

    private void OnChipClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button chip || chip.DataContext is not CollectionChipItem item)
            return;

        if (_activeChip != null)
        {
            _activeChip.Classes.Remove("chip-selected");
            _activeChip.Classes.Add("chip");
        }

        chip.Classes.Remove("chip");
        chip.Classes.Add("chip-selected");
        _activeChip = chip;

        _state.Select(item.Id);

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
