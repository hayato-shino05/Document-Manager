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
                // このバックグラウンドコールバックが実行される前にコントロールが
                // アンロードされていた場合は、（破棄済みの可能性がある）ViewModel に
                // 触らない。また vm.Load() 内でも _disposed を確認する。
                if (!IsLoaded)
                    return;
                if (DataContext is WatchedFolderModel vm)
                    vm.Load();
            }, DispatcherPriority.Background);
        };
    }
}
