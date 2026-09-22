using Avalonia.Controls;
using System.Linq;

namespace StudyDocumentManager.Views;

public partial class ImportInbox : UserControl
{
    public ImportInbox() => InitializeComponent();

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not Models.ImportInboxModel model) return;
        foreach (var item in e.RemovedItems.OfType<Core.Entities.ImportInboxItem>())
            model.RemoveSelected(item);
        foreach (var item in e.AddedItems.OfType<Core.Entities.ImportInboxItem>())
            model.AddSelected(item);
    }
}
