using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

public class ModernizedAllViewsScreenshotTests
{
    private static readonly string[] ScreenshotDirs =
    [
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "screenshots"),
        Path.Combine(Directory.GetCurrentDirectory(), "screenshots"),
        @"C:\Users\ADMIN\.gemini\antigravity-cli\brain\c4c022e8-7b3f-4664-ba3f-f2151f7b7645\screenshots",
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
            try
            {
                Directory.CreateDirectory(dir);
                var filePath = Path.Combine(dir, filename);
                bitmap.Save(filePath);
            }
            catch
            {
            }
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

    [AvaloniaFact]
    public void Capture_PersonalNote()
    {
        var services = App.Services!;
        var model = services.GetRequiredService<PersonalNoteModel>();
        model.Load(1, "Deep Learning & Neural Networks Guide.pdf");
        if (model.Notes.Count == 0)
        {
            model.Notes.Add(new Core.Entities.PersonalNote(1, 1, "summary", "Chapter 4 covers backpropagation and gradient descent optimization strategies with Adam & RMSprop.", true) { CreatedAt = DateTime.Now.AddDays(-2), UpdatedAt = DateTime.Now.AddHours(-3) });
            model.Notes.Add(new Core.Entities.PersonalNote(2, 1, "action", "Implement custom loss function in PyTorch before Friday's lab session.", false) { CreatedAt = DateTime.Now.AddDays(-1), UpdatedAt = DateTime.Now.AddHours(-1) });
            model.Notes.Add(new Core.Entities.PersonalNote(3, 1, "quote", "Optimization is not about finding the best possible solution, but finding an acceptable solution quickly.", false) { CreatedAt = DateTime.Now.AddDays(-3), UpdatedAt = DateTime.Now.AddDays(-1) });
            model.SelectedNote = model.Notes[0];
        }
        SaveRenderedView(new PersonalNote { DataContext = model }, 1280, 800, "personal_note_current.png");
    }

    [AvaloniaFact]
    public void Capture_OnboardingDialog()
    {
        var services = App.Services!;
        var settings = services.GetRequiredService<ISettingsService>();
        var loc = services.GetService<StudyDocumentManager.Core.Interfaces.ILocalizationService>();
        var model = new OnboardingModel(settings, loc);

        var dialog = new OnboardingDialog { DataContext = model, Width = 860, Height = 640 };
        dialog.Show();
        Dispatcher.UIThread.RunJobs();

        var pixelSize = new PixelSize(860, 640);
        var dpi = new Vector(96, 96);
        using var bitmap = new RenderTargetBitmap(pixelSize, dpi);
        bitmap.Render(dialog);

        foreach (var dir in ScreenshotDirs)
        {
            try
            {
                Directory.CreateDirectory(dir);
                var filePath = Path.Combine(dir, "fresh_onboarding.png");
                bitmap.Save(filePath);
            }
            catch
            {
            }
        }

        // Tab 2: Catalog
        model.SelectedTabIndex = 1;
        Dispatcher.UIThread.RunJobs();
        using var bitmapTab2 = new RenderTargetBitmap(pixelSize, dpi);
        bitmapTab2.Render(dialog);
        foreach (var dir in ScreenshotDirs)
        {
            try { bitmapTab2.Save(Path.Combine(dir, "fresh_onboarding_tab2.png")); } catch { }
        }

        // Tab 3: Shortcuts
        model.SelectedTabIndex = 2;
        Dispatcher.UIThread.RunJobs();
        using var bitmapTab3 = new RenderTargetBitmap(pixelSize, dpi);
        bitmapTab3.Render(dialog);
        foreach (var dir in ScreenshotDirs)
        {
            try { bitmapTab3.Save(Path.Combine(dir, "fresh_onboarding_tab3.png")); } catch { }
        }

        dialog.Close();
        Dispatcher.UIThread.RunJobs();
    }
}
