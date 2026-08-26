using Avalonia.Controls;
using Avalonia.Threading;
using StudyDocumentManager.Models;

namespace StudyDocumentManager.Views;

public partial class WatchedFolder : UserControl
{
    public WatchedFolder()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (DataContext is WatchedFolderModel vm)
                    vm.Load();
            }, DispatcherPriority.Background);
        };
    }
}
