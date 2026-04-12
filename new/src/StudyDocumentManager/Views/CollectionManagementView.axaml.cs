using Avalonia.Controls;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.ViewModels;

namespace StudyDocumentManager.Views;

public partial class CollectionManagementView : UserControl
{
    public CollectionManagementView()
    {
        InitializeComponent();

        // Sync DataGrid multi-selection → ViewModel (SelectedItems not bindable in Avalonia DataGrid)
        DocumentGrid.SelectionChanged += OnDocumentGridSelectionChanged;
    }

    private void OnDocumentGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not CollectionManagementViewModel vm) return;

        // Collect all selected StudyDocument items
        var selected = DocumentGrid.SelectedItems
            .OfType<StudyDocument>()
            .ToList();

        vm.SelectedDocumentsInCollection = selected;
    }
}
