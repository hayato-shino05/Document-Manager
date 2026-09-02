using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public class DesktopQualityBatch2Tests
{
    [Fact]
    public void Dashboard_EmptyState_HasAddButton()
    {
        var dashboard = File.ReadAllText(
            GetSourceFilePath("StudyDocumentManager", "Views", "Dashboard.axaml"));

        Assert.Contains("AutomationProperties.AutomationId=\"Dashboard_EmptyState_AddButton\"", dashboard);
        Assert.Contains("Command=\"{Binding AddDocumentCommand}\"", dashboard);
    }

    [Fact]
    public void DuplicateDetection_View_HasInitialAndCleanStates()
    {
        var view = File.ReadAllText(
            GetSourceFilePath("StudyDocumentManager", "Views", "DuplicateDetection.axaml"));

        Assert.Contains("IsVisible=\"{Binding IsInitialState}\"", view);
        Assert.Contains("IsVisible=\"{Binding IsCleanState}\"", view);
        Assert.Contains("AutomationProperties.AutomationId=\"DuplicateDetection_EmptyState_ScanButton\"", view);
        Assert.Contains("AutomationProperties.AutomationId=\"DuplicateDetection_CleanState\"", view);
    }

    [Fact]
    public async Task DuplicateDetectionModel_TracksInitialAndCleanState()
    {
        var repo = new EmptyTestDocumentRepository();
        var loc = new CountingLocalizationService();
        var dialog = new TestDialogService();
        var model = new DuplicateDetectionModel(repo, dialog, loc);

        Assert.True(model.IsInitialState);
        Assert.False(model.IsCleanState);
        Assert.False(model.HasResults);

        await model.ScanDuplicatesCommand.ExecuteAsync(null);

        Assert.False(model.IsInitialState);
        Assert.True(model.IsCleanState);
        Assert.False(model.HasResults);
        Assert.False(string.IsNullOrWhiteSpace(model.CleanSummaryText));
    }

    [Fact]
    public async Task DuplicateDetectionModel_OnException_DoesNotShowCleanState()
    {
        var repo = new ThrowingTestDocumentRepository();
        var loc = new CountingLocalizationService();
        var dialog = new TestDialogService();
        var model = new DuplicateDetectionModel(repo, dialog, loc);

        await model.ScanDuplicatesCommand.ExecuteAsync(null);

        Assert.True(model.IsInitialState);
        Assert.False(model.IsCleanState);
        Assert.False(model.HasResults);
    }

    [Fact]
    public void FileIntegrityCheck_View_HasInitialAndHealthyStates()
    {
        var view = File.ReadAllText(
            GetSourceFilePath("StudyDocumentManager", "Views", "FileIntegrityCheck.axaml"));

        Assert.Contains("IsVisible=\"{Binding IsInitialState}\"", view);
        Assert.Contains("IsVisible=\"{Binding IsHealthyState}\"", view);
        Assert.Contains("AutomationProperties.AutomationId=\"FileIntegrity_EmptyState_ScanButton\"", view);
        Assert.Contains("AutomationProperties.AutomationId=\"FileIntegrity_HealthyState\"", view);
    }

    [Fact]
    public async Task FileIntegrityCheckModel_TracksInitialAndHealthyState()
    {
        var repo = new EmptyTestDocumentRepository();
        var loc = new CountingLocalizationService();
        var dialog = new TestDialogService();
        var fileDialog = new TestFileDialogService();
        var model = new FileIntegrityCheckModel(repo, null, dialog, fileDialog, loc);

        Assert.True(model.IsInitialState);
        Assert.False(model.IsHealthyState);

        await model.CheckIntegrityCommand.ExecuteAsync(null);

        Assert.False(model.IsInitialState);
        Assert.True(model.IsHealthyState);
        Assert.False(string.IsNullOrWhiteSpace(model.HealthySummaryText));
    }

    [Fact]
    public async Task FileIntegrityCheckModel_OnException_DoesNotShowHealthyState()
    {
        var repo = new ThrowingTestDocumentRepository();
        var loc = new CountingLocalizationService();
        var dialog = new TestDialogService();
        var fileDialog = new TestFileDialogService();
        var model = new FileIntegrityCheckModel(repo, null, dialog, fileDialog, loc);

        await model.CheckIntegrityCommand.ExecuteAsync(null);

        Assert.True(model.IsInitialState);
        Assert.False(model.IsHealthyState);
    }

    [Fact]
    public void FileIntegrityCheckModel_TransitionToHealthyState_WhenMissingCountDecrementsToZero()
    {
        var repo = new EmptyTestDocumentRepository();
        var loc = new CountingLocalizationService();
        var dialog = new TestDialogService();
        var fileDialog = new TestFileDialogService();
        var model = new FileIntegrityCheckModel(repo, null, dialog, fileDialog, loc);

        model.HasScanned = true;
        model.MissingCount = 1;
        Assert.False(model.IsHealthyState);

        var notified = false;
        model.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(model.IsHealthyState))
                notified = true;
        };

        model.MissingCount = 0;
        Assert.True(notified);
        Assert.True(model.IsHealthyState);
    }

    [Fact]
    public void Views_EllipsisColumns_HaveToolTips()
    {
        var recycleBin = File.ReadAllText(
            GetSourceFilePath("StudyDocumentManager", "Views", "RecycleBin.axaml"));
        var recentFiles = File.ReadAllText(
            GetSourceFilePath("StudyDocumentManager", "Views", "RecentFiles.axaml"));

        Assert.Contains("TextTrimming=\"CharacterEllipsis\" ToolTip.Tip=\"{Binding Name}\"", recycleBin);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\" ToolTip.Tip=\"{Binding DocumentName}\"", recentFiles);
    }

    private static string GetSourceFilePath(params string[] pathSegments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StudyDocumentManager.sln")))
                return Path.Combine(directory.FullName, Path.Combine(pathSegments));
        }

        throw new DirectoryNotFoundException("Could not locate the solution root.");
    }

    private sealed class EmptyTestDocumentRepository : IDocumentRepository
    {
        public List<StudyDocument> GetAll() => new();
        public StudyDocument? GetById(int id) => null;
        public List<StudyDocument> Search(string keyword) => new();
        public List<StudyDocument> Filter(string subject, string type) => new();
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => new();
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

    private sealed class ThrowingTestDocumentRepository : IDocumentRepository
    {
        public List<StudyDocument> GetAll() => throw new InvalidOperationException("Simulated database failure");
        public StudyDocument? GetById(int id) => null;
        public List<StudyDocument> Search(string keyword) => new();
        public List<StudyDocument> Filter(string subject, string type) => new();
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => new();
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

    private sealed class TestDialogService : IDialogService
    {
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(true);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
    }

    private sealed class TestFileDialogService : IFileDialogService
    {
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
    }
}
