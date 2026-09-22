using Avalonia.Controls;
using Avalonia.Interactivity;
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


    private void OnSelectAllDocumentsClicked(object? sender, RoutedEventArgs e)
        => DocumentGrid.SelectAll();

    private void OnDeselectAllDocumentsClicked(object? sender, RoutedEventArgs e)
        => DocumentGrid.UnselectAll();
}
