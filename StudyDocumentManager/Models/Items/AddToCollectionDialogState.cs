using System.Collections.ObjectModel;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Models.Items;

public class AddToCollectionDialogState
{
    private readonly List<SelectableDocumentItem> _allItems;
    private readonly ILocalizationService _loc;

    public AddToCollectionDialogState(IEnumerable<SelectableDocumentItem> items, ILocalizationService loc)
    {
        _allItems = items.ToList();
        _loc = loc;
        VisibleItems = new ObservableCollection<SelectableDocumentItem>();
        foreach (var item in _allItems)
            item.SelectionChanged += (_, _) => RefreshSelectionState();
    }

    public ObservableCollection<SelectableDocumentItem> VisibleItems { get; }
    public string CountText { get; private set; } = string.Empty;
    public string SelectedCountText { get; private set; } = "0";
    public string SelectAllButtonText { get; private set; } = string.Empty;
    public bool CanConfirm { get; private set; }
    public bool? HeaderCheckState { get; private set; }

    public void ApplyFilter(string term)
    {
        VisibleItems.Clear();
        var matches = _allItems.Where(item => item.MatchesSearch(term)).ToList();
        foreach (var item in matches)
            VisibleItems.Add(item);

        CountText = matches.Count == _allItems.Count
            ? string.Format(_loc["Collection_DocCount"], matches.Count)
            : string.Format(_loc["Collection_DocCountFiltered"], matches.Count, _allItems.Count);

        RefreshSelectionState();
    }

    public void ToggleSelectAllVisible()
    {
        bool anyUnselected = VisibleItems.Any(item => !item.IsSelected);
        foreach (var item in VisibleItems)
            item.IsSelected = anyUnselected;

        RefreshSelectionState();
    }

    public void SetHeaderSelection(bool isChecked)
    {
        foreach (var item in VisibleItems)
            item.IsSelected = isChecked;

        RefreshSelectionState();
    }

    public List<StudyDocument> GetSelectedDocuments()
        => _allItems.Where(item => item.IsSelected).Select(item => item.Document).ToList();

    private void RefreshSelectionState()
    {
        int selectedCount = _allItems.Count(item => item.IsSelected);
        SelectedCountText = selectedCount.ToString();
        CanConfirm = selectedCount > 0;
        SelectAllButtonText = VisibleItems.All(item => item.IsSelected) && VisibleItems.Count > 0
            ? _loc["Collection_DeselectAll"]
            : _loc["Collection_SelectAll"];

        if (VisibleItems.Count == 0)
            HeaderCheckState = false;
        else if (VisibleItems.All(item => item.IsSelected))
            HeaderCheckState = true;
        else if (VisibleItems.Any(item => item.IsSelected))
            HeaderCheckState = null;
        else
            HeaderCheckState = false;
    }
}
