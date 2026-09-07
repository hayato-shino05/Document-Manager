using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using StudyDocumentManager.Models;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

public class ModernizedAllViewsScreenshotTests
{
    private static readonly string[] ScreenshotDirs =
    [
        @"C:\Users\ADMIN\.gemini\antigravity-cli\brain\52f12f8f-7bf7-4eb1-87ac-d5a0904b69e0\screenshots"
    ];

    private static void SaveRenderedView(Control control, int width, int height, string filename)
    {
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = control
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var pixelSize = new PixelSize(width, height);
        var dpi = new Vector(96, 96);
        using var bitmap = new RenderTargetBitmap(pixelSize, dpi);
        bitmap.Render(window);

        foreach (var dir in ScreenshotDirs)
        {
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, filename);
            bitmap.Save(filePath);
        }

        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Capture_AllModernizedViews()
    {
        var services = App.Services!;

        // 1. WatchedFolder
        var wfModel = services.GetRequiredService<WatchedFolderModel>();
        SaveRenderedView(new WatchedFolder { DataContext = wfModel }, 1200, 750, "fresh_08_WatchedFolder.png");

        // 2. ImportInbox
        var inboxModel = services.GetRequiredService<ImportInboxModel>();
        SaveRenderedView(new ImportInbox { DataContext = inboxModel }, 1200, 750, "fresh_07_ImportInbox.png");

        // 3. BulkDelete
        var bdModel = services.GetRequiredService<BulkDeleteModel>();
        SaveRenderedView(new BulkDelete { DataContext = bdModel }, 1200, 750, "fresh_21_BulkDelete.png");

        // 4. RecycleBin
        var rbModel = services.GetRequiredService<RecycleBinModel>();
        SaveRenderedView(new RecycleBin { DataContext = rbModel }, 1200, 750, "fresh_16_RecycleBin.png");

        // 5. RecoveryCenterView
        var rcModel = services.GetRequiredService<RecoveryCenterModel>();
        SaveRenderedView(new RecoveryCenterView { DataContext = rcModel }, 1200, 750, "fresh_17_RecoveryCenter.png");

        // 6. Report
        var repModel = services.GetRequiredService<ReportModel>();
        SaveRenderedView(new Report { DataContext = repModel }, 1200, 750, "fresh_22_Report.png");

        // 7. TreeMap
        var tmModel = services.GetRequiredService<TreeMapModel>();
        SaveRenderedView(new TreeMap { DataContext = tmModel }, 1200, 750, "fresh_23_TreeMap.png");

        // 8. BatchImport
        var biModel = services.GetRequiredService<BatchImportModel>();
        SaveRenderedView(new BatchImport { DataContext = biModel }, 1200, 750, "fresh_06_BatchImport.png");
    }
}
