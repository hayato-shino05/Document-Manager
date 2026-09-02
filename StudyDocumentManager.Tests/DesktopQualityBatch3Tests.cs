using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using StudyDocumentManager.Views;
using Xunit;
using CoreWatchedFolder = StudyDocumentManager.Core.Entities.WatchedFolder;

namespace StudyDocumentManager.Tests;

public sealed class DesktopQualityBatch3Tests
{
    private sealed class FakeFileDialogService(string? result = null) : IFileDialogService
    {
        public string? Result { get; set; } = result;
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult(Result);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
    }

    private sealed class StubNavigationService : INavigationService
    {
        public bool CanGoBack => true;
        public void NavigateTo(string viewKey) { }
        public void NavigateTo(string viewKey, object? parameter) { }
        public void GoBack() { }
    }

    private sealed class StubDocRepo(params StudyDocument[] docs) : IDocumentRepository
    {
        public List<StudyDocument> GetAll() => docs.ToList();
        public StudyDocument? GetById(int id) => docs.FirstOrDefault(d => d.Id == id);
        public bool Add(StudyDocument document) => true;
        public bool Update(StudyDocument document) => true;
        public bool Delete(int id) => true;
        public List<StudyDocument> Search(string keyword) => docs.ToList();
        public List<StudyDocument> GetFiltered(string? subject, string? type) => docs.ToList();
        public List<StudyDocument> Filter(string? subject, string? type) => docs.ToList();
        public List<StudyDocument> SearchAdvanced(string? keyword, string? subject, string? type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => docs.ToList();
        public bool AddWithCatalogs(StudyDocument doc) => true;
        public List<string> GetDistinctSubjects() => new();
        public List<string> GetDistinctTypes() => new();
        public List<string> GetDistinctTags() => new();
        public List<StudyDocument> GetUpcomingDeadlines(int daysAhead) => new();
        public List<StudyDocument> GetOverdueDocuments() => new();
        public void EnsureSubjectExists(string subject) { }
        public void EnsureTypeExists(string type) { }
    }

    private sealed class StubReportRepo : IReportRepository
    {
        public List<(string Label, int Count)> GetByType() => [("PDF", 10)];
        public List<(string Label, int Count)> GetBySubject() => [("Math", 20)];
        public List<(string Label, int Count)> GetByDay(int days = 7) => [("Today", 2)];
        public List<(string Label, int Count)> GetByMonth(int months = 12) => [("Jan", 15)];
    }

    private sealed class StubFolderWatchService : IFolderWatchService
    {
        public ObservableCollection<CoreWatchedFolder> Folders { get; } = new();
        public bool IsWatching => false;
        public bool IsStopped => true;
        public int WatchingCount => 0;
        public Action<Action>? UiThreadMarshal { get; set; }
        public event EventHandler? StateChanged;
        public string? AddFolder(string folderPath, bool includeSubdirectories = false) => null;
        public void RemoveFolder(int id) { }
        public void ToggleEnabled(int id, bool enabled) { }
        public void RetryFolder(int id) { }
        public void Start() { }
        public void StartWatching() { }
        public void Stop() { }
        public void StopWatching() { }
        public void ReloadConfig() { }
        public void Dispose() { }
    }

    [Fact]
    public async Task WatchedFolderModel_BrowseFolderAsync_SetsNewFolderPath()
    {
        var service = new StubFolderWatchService();
        var nav = new StubNavigationService();
        var loc = new FakeLocalizationService();
        var log = new FakeLog();
        var fileDialog = new FakeFileDialogService(@"C:\Users\Test\Documents");

        var model = new WatchedFolderModel(service, nav, loc, log, fileDialog);

        await model.BrowseFolderCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\Users\Test\Documents", model.NewFolderPath);
    }

    [Fact]
    public void WatchedFolderView_HasBrowseButtonWiring()
    {
        var xaml = File.ReadAllText(Path.Combine("..", "..", "..", "..", "StudyDocumentManager", "Views", "WatchedFolder.axaml"));
        Assert.Contains("AutomationProperties.AutomationId=\"WatchedFolder_Browse\"", xaml);
        Assert.Contains("Command=\"{Binding BrowseFolderCommand}\"", xaml);
    }

    [Fact]
    public void StudentWorkspaceView_UsesResponsiveWrapPanels()
    {
        var xaml = File.ReadAllText(Path.Combine("..", "..", "..", "..", "StudyDocumentManager", "Views", "StudentWorkspace.axaml"));
        Assert.Contains("<WrapPanel Orientation=\"Horizontal\"", xaml);
        Assert.Contains("SW_AcademicYear", xaml);
        Assert.Contains("SW_Assignments", xaml);
    }

    [Fact]
    public void TreeMapModel_ModeProperties_NotifyAndReflectSelectedMode()
    {
        var nav = new StubNavigationService();
        var docRepo = new StubDocRepo(new StudyDocument { Id = 1, Name = "Doc1" });
        var reportRepo = new StubReportRepo();

        var model = new TreeMapModel(nav, docRepo, reportRepo);

        model.ShowAllCommand.Execute(null);
        Assert.True(model.IsAllMode);
        Assert.False(model.IsSubjectMode);
        Assert.False(model.IsTypeMode);

        model.ShowBySubjectCommand.Execute(null);
        Assert.False(model.IsAllMode);
        Assert.True(model.IsSubjectMode);
        Assert.False(model.IsTypeMode);

        model.ShowByTypeCommand.Execute(null);
        Assert.False(model.IsAllMode);
        Assert.False(model.IsSubjectMode);
        Assert.True(model.IsTypeMode);
    }

    [Fact]
    public void ReportModel_ChartDataItem_RatioAndPercentage()
    {
        var item = new ChartDataItem
        {
            Label = "Math",
            Value = 25,
            MaxValue = 50
        };

        Assert.Equal(0.5, item.Ratio);
        Assert.Equal(50.0, item.Percentage);
        Assert.Equal(150.0, item.BarWidth);

        var zeroItem = new ChartDataItem
        {
            Label = "None",
            Value = 0,
            MaxValue = 0
        };

        Assert.Equal(0.0, zeroItem.Ratio);
        Assert.Equal(0.0, zeroItem.Percentage);
        Assert.Equal(0.0, zeroItem.BarWidth);
    }

    [Fact]
    public void ReportView_HasToolTipsOnAllCharts()
    {
        var xaml = File.ReadAllText(Path.Combine("..", "..", "..", "..", "StudyDocumentManager", "Views", "Report.axaml"));
        var count = System.Text.RegularExpressions.Regex.Matches(xaml, @"ToolTip\.Tip=""\{Binding Label\}""").Count;
        Assert.True(count >= 5, $"Expected at least 5 ToolTip.Tip bindings in Report.axaml, found {count}");
    }

    [Fact]
    public void AccessibilityAutomationIds_PresentOnStandardizedViews()
    {
        var relDocsXaml = File.ReadAllText(Path.Combine("..", "..", "..", "..", "StudyDocumentManager", "Views", "RelatedDocuments.axaml"));
        Assert.Contains("AutomationProperties.AutomationId=\"Screen_RelatedDocuments\"", relDocsXaml);

        var importInboxXaml = File.ReadAllText(Path.Combine("..", "..", "..", "..", "StudyDocumentManager", "Views", "ImportInbox.axaml"));
        Assert.Contains("AutomationProperties.AutomationId=\"Screen_ImportInbox\"", importInboxXaml);

        var bulkDeleteXaml = File.ReadAllText(Path.Combine("..", "..", "..", "..", "StudyDocumentManager", "Views", "BulkDelete.axaml"));
        Assert.Contains("AutomationProperties.AutomationId=\"BulkDelete_MarkImportant\"", bulkDeleteXaml);
        Assert.Contains("AutomationProperties.AutomationId=\"BulkDelete_ChangeSubject\"", bulkDeleteXaml);
        Assert.Contains("AutomationProperties.AutomationId=\"BulkDelete_Cancel\"", bulkDeleteXaml);
    }
}
