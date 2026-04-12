using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Views;

/// <summary>
/// Selectable wrapper for StudyDocument — used in AddToCollectionDialog list.
/// </summary>
public class SelectableDocumentItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public StudyDocument Document { get; }
    public bool HasAuthor => !string.IsNullOrWhiteSpace(Document.TacGia);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? SelectionChanged;

    public SelectableDocumentItem(StudyDocument doc)
    {
        Document = doc;
    }

    public bool MatchesSearch(string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return true;
        term = term.Trim();
        return Document.Ten.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (Document.MonHoc?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (Document.Loai?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (Document.TacGia?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (Document.Tags?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Document picker dialog — shows searchable checklist of documents.
/// Returns the list of selected documents, or null if cancelled.
/// </summary>
public partial class AddToCollectionDialog : Window
{
    // ─── Data ───────────────────────────────────────────────────────
    private readonly List<SelectableDocumentItem> _allItems;
    private readonly ObservableCollection<SelectableDocumentItem> _visibleItems = new();
    private string _searchTerm = string.Empty;
    private bool _updatingHeader;

    /// <summary>Documents selected by the user. Null = cancelled.</summary>
    public List<StudyDocument>? Result { get; private set; }

    // ────────────────────────────────────────────────────────────────
    /// <summary>Parameterless constructor required by Avalonia XAML loader (AVLN3001).</summary>
    public AddToCollectionDialog()
    {
        InitializeComponent();
        _allItems = [];
    }

    public AddToCollectionDialog(
        IEnumerable<StudyDocument> allDocuments,
        IEnumerable<int> alreadyInCollection,
        string collectionName)
    {
        InitializeComponent();

        // Update title with collection name
        TitleLabel.Text = $"Thêm tài liệu vào \"{collectionName}\"";

        // Build items, excluding docs already in the collection
        var excluded = alreadyInCollection.ToHashSet();
        _allItems = allDocuments
            .Where(d => !excluded.Contains(d.Id))
            .OrderBy(d => d.Ten)
            .Select(d =>
            {
                var item = new SelectableDocumentItem(d);
                item.SelectionChanged += OnItemSelectionChanged;
                return item;
            })
            .ToList();

        DocumentList.ItemsSource = _visibleItems;

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
            ApplyFilter(string.Empty);
        };
    }


    // ─── Search / Filter ────────────────────────────────────────────
    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _searchTerm = SearchBox.Text ?? string.Empty;
        ApplyFilter(_searchTerm);
    }

    private void ApplyFilter(string term)
    {
        _visibleItems.Clear();
        var matches = _allItems.Where(i => i.MatchesSearch(term)).ToList();
        foreach (var item in matches)
            _visibleItems.Add(item);

        CountLabel.Text = matches.Count == _allItems.Count
            ? $"{matches.Count} tài liệu"
            : $"{matches.Count} / {_allItems.Count} tài liệu";

        UpdateHeaderCheckBox();
        UpdateFooter();
    }

    // ─── Select All (header checkbox + button) ──────────────────────
    private void OnSelectAllClicked(object? sender, RoutedEventArgs e)
    {
        bool anyUnselected = _visibleItems.Any(i => !i.IsSelected);
        foreach (var item in _visibleItems)
            item.IsSelected = anyUnselected;
    }

    private void OnHeaderCheckChanged(object? sender, RoutedEventArgs e)
    {
        if (_updatingHeader) return;
        bool check = HeaderCheckBox.IsChecked == true;
        foreach (var item in _visibleItems)
            item.IsSelected = check;
    }

    private void UpdateHeaderCheckBox()
    {
        _updatingHeader = true;
        if (_visibleItems.Count == 0)
            HeaderCheckBox.IsChecked = false;
        else if (_visibleItems.All(i => i.IsSelected))
            HeaderCheckBox.IsChecked = true;
        else if (_visibleItems.Any(i => i.IsSelected))
            HeaderCheckBox.IsChecked = null; // indeterminate
        else
            HeaderCheckBox.IsChecked = false;
        _updatingHeader = false;
    }

    // ─── Footer / Confirm button ─────────────────────────────────────
    private void OnItemSelectionChanged(object? sender, EventArgs e)
    {
        UpdateFooter();
        UpdateHeaderCheckBox();
    }

    private void UpdateFooter()
    {
        int count = _allItems.Count(i => i.IsSelected);
        SelectedCountBadge.Text = count.ToString();
        ConfirmButton.IsEnabled = count > 0;

        SelectAllBtn.Content = _visibleItems.All(i => i.IsSelected) && _visibleItems.Count > 0
            ? "Bỏ chọn tất cả"
            : "Chọn tất cả";
    }

    // ─── Confirm / Cancel ───────────────────────────────────────────
    private void OnConfirmClicked(object? sender, RoutedEventArgs e)
    {
        Result = _allItems.Where(i => i.IsSelected).Select(i => i.Document).ToList();
        Close();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }
}
