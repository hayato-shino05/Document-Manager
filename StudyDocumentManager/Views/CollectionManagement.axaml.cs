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

        // Collect all selected StudyDocument items
        var selected = DocumentGrid.SelectedItems
            .OfType<StudyDocument>()
            .ToList();

        vm.SelectedDocumentsInCollection = selected;
    }
}
