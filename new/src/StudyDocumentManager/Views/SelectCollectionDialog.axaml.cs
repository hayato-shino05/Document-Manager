using Avalonia.Controls;
using Avalonia.Interactivity;
using StudyDocumentManager.ViewModels.Items;

namespace StudyDocumentManager.Views;

/// <summary>
/// Collection-picker dialog.
/// Shows existing collections as chip pills — click to select one, then confirm.
/// Returns the selected collection <c>Id</c> via <see cref="Result"/>, or -1 if cancelled.
/// </summary>
public partial class SelectCollectionDialog : Window
{
    public int Result { get; private set; } = -1;

    private readonly IList<(int Id, string Name, int DocCount)> _collections;
    private readonly WrapPanel _chipsPanel;
    private readonly TextBlock _selectedLabel;
    private readonly Button _okButton;
    private readonly SelectCollectionDialogState _state;

    private Button? _activeChip;

    public SelectCollectionDialog()
    {
        _collections = [];
        _state = new SelectCollectionDialogState([]);
    }

    public SelectCollectionDialog(
        string documentName,
        IList<(int Id, string Name, int DocCount)> collections)
    {
        InitializeComponent();
        _collections = collections;
        _state = new SelectCollectionDialogState(collections);

        this.FindControl<TextBlock>("DocNameLabel")!
            .Text = $"Tài liệu: \"{documentName}\"";

        _chipsPanel    = this.FindControl<WrapPanel>("ChipsPanel")!;
        _selectedLabel = this.FindControl<TextBlock>("SelectedLabel")!;
        _okButton      = this.FindControl<Button>("OkButton")!;

        BuildChips();

        this.FindControl<Button>("CancelButton")!.Click += CancelClicked;
        _okButton.Click += OkClicked;
    }

    // ── Build chip pills ─────────────────────────────────────────────

    private void BuildChips()
    {
        if (_collections.Count == 0)
        {
            _chipsPanel.Children.Add(new TextBlock
            {
                Text = "(Chưa có bộ sưu tập nào — hãy tạo trong menu Bộ sưu tập)",
                FontSize = 11,
                Foreground = Avalonia.Media.Brushes.Gray
            });
            return;
        }

        foreach (var col in _collections)
        {
            var label = SelectCollectionDialogState.BuildChipLabel(col.Name, col.DocCount);

            var chip = new Button
            {
                Content = label,
                Tag = col.Id,
                Classes = { "chip" }
            };
            chip.Click += OnChipClicked;
            _chipsPanel.Children.Add(chip);
        }
    }

    // ── Chip selection ───────────────────────────────────────────────

    private void OnChipClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button chip) return;

        // Deselect previous chip
        if (_activeChip != null)
        {
            _activeChip.Classes.Remove("chip-selected");
            _activeChip.Classes.Add("chip");
        }

        // Activate new chip
        chip.Classes.Remove("chip");
        chip.Classes.Add("chip-selected");
        _activeChip = chip;

        var selectedId = (int)(chip.Tag ?? -1);
        _state.Select(selectedId);

        _selectedLabel.Text = _state.SelectedLabel;
        _okButton.IsEnabled = _state.CanConfirm;
    }

    // ── Confirm / Cancel ────────────────────────────────────────────

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
