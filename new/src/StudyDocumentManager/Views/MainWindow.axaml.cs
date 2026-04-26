using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using StudyDocumentManager.ViewModels;

namespace StudyDocumentManager.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Enable drag & drop
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        DragDrop.SetAllowDrop(this, true);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var vm = DataContext as MainWindowViewModel;
        if (vm?.CurrentView is not DashboardViewModel dashboard) return;

        // Keyboard shortcuts — only active when on Dashboard
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.N: // Ctrl+N → Add new document
                    dashboard.AddDocumentCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.F: // Ctrl+F → Focus search (toggle advanced filter as placeholder)
                    dashboard.ToggleAdvancedFilterCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.E: // Ctrl+E → Export CSV
                    dashboard.ExportCsvCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.O: // Ctrl+O → Open selected file
                    dashboard.OpenFileCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
        else if (e.KeyModifiers == KeyModifiers.None)
        {
            switch (e.Key)
            {
                case Key.F5: // F5 → Refresh
                    dashboard.RefreshCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Delete: // Del → Delete selected
                    dashboard.DeleteDocumentCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        // Accept file drops
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files))
            return;

        if (DataContext is not MainWindowViewModel vm)
            return;

        var filePaths = e.Data.GetFiles()?
            .OfType<Avalonia.Platform.Storage.IStorageFile>()
            .Select(file => file.Path.LocalPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .Cast<string>()
            .ToList();

        if (filePaths == null || filePaths.Count == 0)
            return;

        await vm.HandleDroppedFilesAsync(filePaths);
        e.Handled = true;
    }

}
