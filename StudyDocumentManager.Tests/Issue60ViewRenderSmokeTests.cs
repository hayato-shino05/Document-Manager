using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class Issue60ViewRenderSmokeTests
{
    private static T? FindByAutomationId<T>(Control root, string id) where T : Control =>
        root.GetVisualDescendants().OfType<T>()
            .FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == id);

    private static void RenderAt(Window window, Control view, double width, string screenId, string childId)
    {
        window.Width = width;
        window.InvalidateMeasure();
        window.InvalidateVisual();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(screenId, AutomationProperties.GetAutomationId(view));
        Assert.NotNull(FindByAutomationId<Control>(view, childId));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void BatchImport_Renders_AtNarrowAndWide_WithStatusAndGrid()
    {
        var s = App.Services!;
        var model = new BatchImportModel(
            s.GetRequiredService<IDialogService>(),
            s.GetRequiredService<IFileDialogService>(),
            s.GetRequiredService<INavigationService>(),
            s.GetRequiredService<ILocalizationService>(),
            s.GetRequiredService<IDroppedFileImportService>());
        var view = new BatchImport { DataContext = model };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(FindByAutomationId<Button>(view, "BatchImport_Import"));
        Assert.NotNull(FindByAutomationId<Button>(view, "BatchImport_Cancel"));
        Assert.NotNull(FindByAutomationId<TextBlock>(view, "BatchImport_Status"));
        Assert.NotNull(FindByAutomationId<DataGrid>(view, "BatchImport_FileGrid"));

        RenderAt(window, view, 520, "Screen_BatchImport", "BatchImport_Import");
        RenderAt(window, view, 1280, "Screen_BatchImport", "BatchImport_Import");
        window.Close();
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void WatchedFolder_Renders_WithUiaNameStatus_AndReflowsAtNarrow()
    {
        var s = App.Services!;
        var model = new WatchedFolderModel(
            s.GetRequiredService<IFolderWatchService>(),
            s.GetRequiredService<INavigationService>(),
            s.GetRequiredService<ILocalizationService>(),
            s.GetRequiredService<ILog>());
        var view = new StudyDocumentManager.Views.WatchedFolder { DataContext = model };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var path = FindByAutomationId<TextBox>(view, "WatchedFolder_Path");
        Assert.NotNull(path);
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(path!)));

        Assert.NotNull(FindByAutomationId<TextBlock>(view, "WatchedFolder_Status"));
        Assert.NotNull(FindByAutomationId<Button>(view, "WatchedFolder_Add"));

        RenderAt(window, view, 520, "WatchedFolder_Screen", "WatchedFolder_Path");
        RenderAt(window, view, 1280, "WatchedFolder_Screen", "WatchedFolder_Path");
        window.Close();
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void RecoveryCenterView_Renders_WithScrollAndStatus()
    {
        var s = App.Services!;
        var model = new RecoveryCenterModel(
            s.GetRequiredService<IVersionedBackupService>(),
            s.GetRequiredService<IDialogService>(),
            s.GetRequiredService<IFileDialogService>(),
            s.GetRequiredService<INavigationService>(),
            s.GetRequiredService<IApplicationLifecycleService>(),
            s.GetRequiredService<IProcessLauncherService>(),
            s.GetRequiredService<ILocalizationService>());
        var view = new RecoveryCenterView { DataContext = model };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(FindByAutomationId<TextBlock>(view, "Recovery_LatestStatus"));
        Assert.NotNull(FindByAutomationId<DataGrid>(view, "Recovery_VersionsGrid"));
        Assert.True(view.GetVisualDescendants().OfType<ScrollViewer>().Any());

        RenderAt(window, view, 520, "Screen_RecoveryCenter", "Recovery_VersionsGrid");
        RenderAt(window, view, 1280, "Screen_RecoveryCenter", "Recovery_VersionsGrid");
        window.Close();
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task DuplicateDetection_Renders_PerDocumentViewDeleteActions_AfterScan()
    {
        var s = App.Services!;
        var docs = new List<StudyDocument>
        {
            new StudyDocument { Id = 1, Name = "Report.pdf", FilePath = "C:/docs/report.pdf" },
            new StudyDocument { Id = 2, Name = "Report.pdf", FilePath = "C:/docs/report-copy.pdf" },
        };
        var model = new DuplicateDetectionModel(
            new FakeScanRepository(docs),
            s.GetRequiredService<IDialogService>(),
            s.GetRequiredService<ILocalizationService>(),
            s.GetRequiredService<IProcessLauncherService>());
        var view = new DuplicateDetection { DataContext = model };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(FindByAutomationId<Button>(view, "DuplicateDetection_Scan"));

        await model.ScanDuplicatesCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(model.HasResults);
        Assert.NotNull(FindByAutomationId<Button>(view, "DuplicateDetection_View"));
        Assert.NotNull(FindByAutomationId<Button>(view, "DuplicateDetection_Delete"));
        Assert.NotNull(FindByAutomationId<Button>(view, "DuplicateDetection_Merge"));

        RenderAt(window, view, 520, "Screen_DuplicateDetection", "DuplicateDetection_View");
        RenderAt(window, view, 1280, "Screen_DuplicateDetection", "DuplicateDetection_View");
        window.Close();
    }

    private sealed class FakeScanRepository : IDocumentRepository
    {
        private readonly List<StudyDocument> _docs;
        public FakeScanRepository(List<StudyDocument> docs) => _docs = docs;
        public List<StudyDocument> GetAll() => _docs;
        public StudyDocument? GetById(int id) => _docs.FirstOrDefault(d => d.Id == id);
        public List<StudyDocument> Search(string keyword) => _docs;
        public List<StudyDocument> Filter(string subject, string type) => _docs;
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => _docs;
        public bool Add(StudyDocument document) => true;
        public bool AddWithCatalogs(StudyDocument document) => true;
        public bool Update(StudyDocument document) => true;
        public bool Delete(int id) => true;
        public List<string> GetDistinctSubjects() => new();
        public List<string> GetDistinctTypes() => new();
        public List<string> GetDistinctTags() => new();
        public List<StudyDocument> GetUpcomingDeadlines(int days) => new();
        public List<StudyDocument> GetOverdueDocuments() => new();
        public void EnsureSubjectExists(string subject) { }
        public void EnsureTypeExists(string type) { }
    }
}
