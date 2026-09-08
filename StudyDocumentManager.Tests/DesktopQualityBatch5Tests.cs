using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.DTOs;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class DesktopQualityBatch5Tests
{
    [Fact]
    public void MainWindow_DefinesStandardKeyBindings()
    {
        var xaml = File.ReadAllText(Path.Combine("..", "..", "..", "..", "StudyDocumentManager", "Views", "MainWindow.axaml"));

        Assert.Contains("Gesture=\"Ctrl+N\"", xaml);
        Assert.Contains("Gesture=\"Ctrl+Z\"", xaml);
        Assert.Contains("Gesture=\"Ctrl+Shift+I\"", xaml);
        Assert.Contains("Gesture=\"Ctrl+Shift+R\"", xaml);
        Assert.Contains("Gesture=\"Ctrl+D\"", xaml);
        Assert.Contains("Gesture=\"Ctrl+H\"", xaml);
        Assert.Contains("Gesture=\"Ctrl+Shift+C\"", xaml);
        Assert.Contains("Gesture=\"Ctrl+Shift+L\"", xaml);
        Assert.Contains("Gesture=\"Alt+Left\"", xaml);
        Assert.Contains("Gesture=\"F1\"", xaml);
        Assert.Contains("Gesture=\"F5\"", xaml);
        Assert.Contains("Gesture=\"Ctrl+R\"", xaml);
    }

    [Fact]
    public void MainWindow_CanAcceptDroppedFiles_ReflectsActiveView()
    {
        var dialog = new WindowDialogStub();
        var update = new FakeUpdateService();
        var loc = new LocalizationStub();
        var nav = new NavigationStub();
        var settings = new SettingsStub();
        var lifecycle = new LifecycleStub();

        var dashboard = new DashboardModel(null!, null!, null!, null!, null!, dialog, null!, null!, null!, null!, null!, null!, null!, loc);
        var model = new MainWindowModel(dashboard, nav, dialog, null!, null!, lifecycle, loc, settings, update);

        Assert.True(model.CanAcceptDroppedFiles);
    }

    [Fact]
    public void Group5Views_ActionHierarchyAndTokens_AreConsistent()
    {
        var report = File.ReadAllText(Path.Combine("..", "..", "..", "..", "StudyDocumentManager", "Views", "Report.axaml"));
        var treeMap = File.ReadAllText(Path.Combine("..", "..", "..", "..", "StudyDocumentManager", "Views", "TreeMap.axaml"));
        var smartViews = File.ReadAllText(Path.Combine("..", "..", "..", "..", "StudyDocumentManager", "Views", "SmartViews.axaml"));
        var recentFiles = File.ReadAllText(Path.Combine("..", "..", "..", "..", "StudyDocumentManager", "Views", "RecentFiles.axaml"));
        var onboarding = File.ReadAllText(Path.Combine("..", "..", "..", "..", "StudyDocumentManager", "Views", "OnboardingDialog.axaml"));
        var affectedPreview = File.ReadAllText(Path.Combine("..", "..", "..", "..", "StudyDocumentManager", "Views", "AffectedItemsPreviewDialog.axaml"));
        var bulkEditPreview = File.ReadAllText(Path.Combine("..", "..", "..", "..", "StudyDocumentManager", "Views", "BulkEditPreviewDialog.axaml"));

        // Report
        Assert.Contains("Classes=\"page-wrapper\"", report);
        Assert.Contains("Classes=\"secondary\"", report);
        Assert.Contains("IconReport", report);
        Assert.Contains("IconRefresh", report);

        // TreeMap
        Assert.Contains("Classes=\"page-wrapper\"", treeMap);
        Assert.Contains("Classes.primary=\"{Binding IsAllMode}\"", treeMap);
        Assert.Contains("Classes.secondary=\"{Binding !IsAllMode}\"", treeMap);
        Assert.Contains("IconTreeMap", treeMap);

        // SmartViews
        Assert.Contains("Classes=\"page-wrapper\"", smartViews);
        Assert.Contains("Classes=\"primary\" x:Name=\"btnNew\"", smartViews);
        Assert.Contains("Classes=\"primary\" x:Name=\"btnOpen\"", smartViews);
        Assert.Contains("Classes=\"primary\" x:Name=\"btnSave\"", smartViews);
        Assert.Contains("Classes=\"danger\" x:Name=\"btnDelete\"", smartViews);
        Assert.Contains("Classes=\"secondary\" x:Name=\"btnEdit\"", smartViews);
        Assert.Contains("Classes=\"secondary\" x:Name=\"btnDuplicate\"", smartViews);
        Assert.Contains("Classes=\"secondary\" x:Name=\"btnCancelEdit\"", smartViews);
        Assert.Contains("IconSaveWhite", smartViews);
        Assert.Contains("IconDeleteWhite", smartViews);
        Assert.Contains("IconAddWhite", smartViews);

        // RecentFiles
        Assert.Contains("Classes=\"page-wrapper\"", recentFiles);
        Assert.Contains("Classes=\"secondary\" AutomationProperties.AutomationId=\"RecentFiles_Refresh\"", recentFiles);
        Assert.Contains("Classes=\"danger\" AutomationProperties.AutomationId=\"RecentFiles_ClearHistory\"", recentFiles);
        Assert.Contains("IconDeleteWhite", recentFiles);
        Assert.Contains("IconRefresh", recentFiles);
        Assert.Contains("IconOpenFile", recentFiles);

        // OnboardingDialog
        Assert.Matches(@"(?s)<Button[^>]*Name=""SkipButton""[^>]*Classes=""secondary""", onboarding);
        Assert.Matches(@"(?s)<Button[^>]*Name=""PrevButton""[^>]*Classes=""secondary""", onboarding);
        Assert.Matches(@"(?s)<Button[^>]*Name=""NextButton""[^>]*Classes=""primary""", onboarding);
        Assert.Matches(@"(?s)<Button[^>]*Name=""FinishButton""[^>]*Classes=""primary""", onboarding);
        Assert.Contains("AutomationProperties.AutomationId=\"Onboarding_Skip\"", onboarding);
        Assert.Contains("AutomationProperties.AutomationId=\"Onboarding_Prev\"", onboarding);
        Assert.Contains("AutomationProperties.AutomationId=\"Onboarding_Next\"", onboarding);
        Assert.Contains("AutomationProperties.AutomationId=\"Onboarding_Finish\"", onboarding);
        Assert.Contains("IconSearch", onboarding);
        Assert.Contains("IconAdd", onboarding);
        Assert.Contains("IconCollection", onboarding);
        Assert.Contains("IconIntegrity", onboarding);
        Assert.Contains("IconRestore", onboarding);

        // AffectedItemsPreviewDialog
        Assert.Matches(@"(?s)<Button[^>]*x:Name=""CancelButton""[^>]*Classes=""secondary""", affectedPreview);
        Assert.Matches(@"(?s)<Button[^>]*x:Name=""ConfirmButton""[^>]*Classes=""danger""", affectedPreview);
        Assert.Contains("AutomationProperties.AutomationId=\"AffectedPreview_Cancel\"", affectedPreview);
        Assert.Contains("AutomationProperties.AutomationId=\"AffectedPreview_Confirm\"", affectedPreview);

        // BulkEditPreviewDialog
        Assert.Matches(@"(?s)<Button[^>]*x:Name=""CancelButton""[^>]*Classes=""secondary""", bulkEditPreview);
        Assert.Matches(@"(?s)<Button[^>]*x:Name=""ConfirmButton""[^>]*Classes=""primary""", bulkEditPreview);
        Assert.Contains("AutomationProperties.AutomationId=\"BulkEditPreview_Cancel\"", bulkEditPreview);
        Assert.Contains("AutomationProperties.AutomationId=\"BulkEditPreview_Confirm\"", bulkEditPreview);
    }

    private sealed class NavigationStub : INavigationService
    {
        public bool CanGoBack => true;
        public void NavigateTo(string viewKey) { }
        public void NavigateTo(string viewKey, object? parameter) { }
        public void GoBack() { }
    }

    private sealed class WindowDialogStub : IDialogService
    {
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(false);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(false);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "") => Task.FromResult<string?>(null);
    }

    private sealed class SettingsStub : ISettingsService
    {
        public string? GetSetting(string key) => null;
        public void SetSetting(string key, string value) { }
    }

    private sealed class LifecycleStub : IApplicationLifecycleService
    {
        public bool IsExiting { get; set; }
        public void RequestShutdown() { }
        public void Shutdown() { }
    }

    private sealed class LocalizationStub : ILocalizationService
    {
        public string this[string key] => key;
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public void SetLanguage(SupportedLanguage language) => LanguageChanged?.Invoke(this, EventArgs.Empty);
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = Enum.GetValues<SupportedLanguage>();
        public event EventHandler? LanguageChanged;
    }

    private sealed class FakeUpdateService : IUpdateService
    {
        public Task<UpdateInfo?> CheckForUpdateAsync() => Task.FromResult<UpdateInfo?>(null);
        public Task CheckSilentlyAsync() => Task.CompletedTask;
        public Task HandleUpdateAsync(UpdateInfo update) => Task.CompletedTask;
    }
}
