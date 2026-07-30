using Avalonia.Controls;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Models;

namespace StudyDocumentManager.Views;

public partial class CollectionManagement : UserControl
{
    public CollectionManagement()
    {
        InitializeComponent();

        // Sync DataGrid multi-selection → ViewModel (SelectedItems not bindable in Avalonia DataGrid)
        DocumentGrid.SelectionChanged += OnDocumentGridSelectionChanged;
    }

    private void OnDocumentGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not CollectionManagementModel vm) return;

        var selectedItems = DocumentGrid.SelectedItems;
        var selected = selectedItems == null
            ? new List<StudyDocument>()
            : selectedItems.OfType<StudyDocument>().ToList();

        vm.SelectedDocumentsInCollection = selected;
    }
}
