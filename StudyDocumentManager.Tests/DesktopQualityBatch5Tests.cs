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
