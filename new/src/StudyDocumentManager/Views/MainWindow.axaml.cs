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
        if (!e.Data.Contains(DataFormats.Files)) return;

        var vm = DataContext as MainWindowViewModel;
        if (vm == null) return;

        var files = e.Data.GetFiles()?.ToList();
        if (files == null || files.Count == 0) return;

        var filePaths = new List<string>();
        foreach (var item in files)
        {
            if (item is Avalonia.Platform.Storage.IStorageFile file)
            {
                var path = file.Path.LocalPath;
                if (!string.IsNullOrEmpty(path))
                    filePaths.Add(path);
            }
        }

        if (filePaths.Count == 0) return;

        var repo = App.Services?.GetService(typeof(StudyDocumentManager.Core.Interfaces.IDocumentRepository))
            as StudyDocumentManager.Core.Interfaces.IDocumentRepository;
        if (repo == null) return;

        int imported = 0;

        if (filePaths.Count == 1)
        {
            // Single file: show modal dialog for user to fill metadata
            var dialog = new AddDocumentDialog(filePaths[0]);
            var result = await dialog.ShowDialog<bool?>(this);
            if (result == true && dialog.Result != null)
            {
                if (repo.Add(dialog.Result))
                {
                    imported++;
                    // Sync to lookup tables
                    if (!string.IsNullOrWhiteSpace(dialog.Result.MonHoc))
                        StudyDocumentManager.Data.Helpers.DatabaseHelper.AddSubject(dialog.Result.MonHoc);
                    if (!string.IsNullOrWhiteSpace(dialog.Result.Loai))
                        StudyDocumentManager.Data.Helpers.DatabaseHelper.AddType(dialog.Result.Loai);
                }
            }
        }
        else
        {
            // Multiple files: bulk import directly
            foreach (var path in filePaths)
            {
                var info = new System.IO.FileInfo(path);
                var doc = new StudyDocumentManager.Core.Entities.StudyDocument
                {
                    Ten = System.IO.Path.GetFileNameWithoutExtension(path),
                    DuongDan = path,
                    Loai = Services.FileTypeDetector.DetectFromPath(path),
                    KichThuoc = info.Length / (1024.0 * 1024.0)
                };
                if (repo.Add(doc))
                {
                    imported++;
                    StudyDocumentManager.Data.Helpers.DatabaseHelper.AddType(doc.Loai);
                }
            }
        }

        if (imported > 0 && vm.CurrentView is DashboardViewModel dashboard)
        {
            dashboard.RefreshCommand.Execute(null);
        }

        e.Handled = true;
    }
}
