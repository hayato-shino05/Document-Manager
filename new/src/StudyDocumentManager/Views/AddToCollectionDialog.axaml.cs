using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Models.Items;

namespace StudyDocumentManager.Views;

/// <summary>
/// Document picker dialog — shows searchable checklist of documents.
/// Returns the list of selected documents, or null if cancelled.
/// </summary>
public partial class AddToCollectionDialog : Window
{
    // ─── Data ───────────────────────────────────────────────────────
    private readonly AddToCollectionDialogState _state;
    private bool _updatingHeader;

    /// <summary>Documents selected by the user. Null = cancelled.</summary>
    public List<StudyDocument>? Result { get; private set; }

    // ────────────────────────────────────────────────────────────────
    /// <summary>Parameterless constructor required by Avalonia XAML loader (AVLN3001).</summary>
    public AddToCollectionDialog()
    {
        InitializeComponent();
        _state = new AddToCollectionDialogState([]);
    }

    public AddToCollectionDialog(
        IEnumerable<StudyDocument> allDocuments,
        IEnumerable<int> alreadyInCollection,
        string collectionName) : this()
    {

        // Update title with collection name
        TitleLabel.Text = $"Thêm tài liệu vào \"{collectionName}\"";

        // Build items, excluding docs already in the collection
        var excluded = alreadyInCollection.ToHashSet();
        var items = allDocuments
            .Where(d => !excluded.Contains(d.Id))
            .OrderBy(d => d.Ten)
            .Select(d => new SelectableDocumentItem(d))
            .ToList();

        _state = new AddToCollectionDialogState(items);
        DocumentList.ItemsSource = _state.VisibleItems;

        // Wire up events
        SearchBox.TextChanged += OnSearchChanged;
        SelectAllBtn.Click    += OnSelectAllClicked;
        ConfirmButton.Click   += OnConfirmClicked;
        CancelButton.Click    += OnCancelClicked;
        HeaderCheckBox.IsCheckedChanged += OnHeaderCheckChanged;

        // Focus search box on open
        Opened += (_, _) =>
        {
            SearchBox.Focus();
            _state.ApplyFilter(string.Empty);
            SyncStateToView();
        };
    }


    // ─── Search / Filter ────────────────────────────────────────────
    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _state.ApplyFilter(SearchBox.Text ?? string.Empty);
        SyncStateToView();
    }

    // ─── Select All (header checkbox + button) ──────────────────────
    private void OnSelectAllClicked(object? sender, RoutedEventArgs e)
    {
        _state.ToggleSelectAllVisible();
        SyncStateToView();
    }

    private void OnHeaderCheckChanged(object? sender, RoutedEventArgs e)
    {
        if (_updatingHeader)
            return;

        _state.SetHeaderSelection(HeaderCheckBox.IsChecked == true);
        SyncStateToView();
    }

    // ─── Footer / Confirm button ─────────────────────────────────────
    private void OnItemSelectionChanged(object? sender, EventArgs e)
    {
        SyncStateToView();
    }

    // ─── Confirm / Cancel ───────────────────────────────────────────
    private void OnConfirmClicked(object? sender, RoutedEventArgs e)
    {
        Result = _state.GetSelectedDocuments();
        Close();
    }

    private void SyncStateToView()
    {
        CountLabel.Text = _state.CountText;
        SelectedCountBadge.Text = _state.SelectedCountText;
        ConfirmButton.IsEnabled = _state.CanConfirm;
        SelectAllBtn.Content = _state.SelectAllButtonText;

        _updatingHeader = true;
        HeaderCheckBox.IsChecked = _state.HeaderCheckState;
        _updatingHeader = false;
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }
}
