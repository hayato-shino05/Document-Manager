using System.Reflection;
using System.Text.Json;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.DTOs;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class TreeMapModelTests
{
    [Fact]
    public void SubjectMode_GroupsItemsAndCalculatesTotalsAndPercentages()
    {
        var reports = new ReportStub
        {
            Subjects = [("Math", 2), ("Science", 1)]
        };
        var model = new TreeMapModel(new NavigationStub(), new DocumentRepositoryStub(), reports);

        Assert.Equal("subject", model.SelectedMode);
        Assert.Equal(3, model.TotalDocuments);
        Assert.Equal(2, model.Items.Count);
        Assert.Equal("Math", model.Items[0].Label);
        Assert.Equal(66.666, model.Items[0].Percentage, 2);
        Assert.Contains("66.7%", model.Items[0].DisplayText);
        Assert.Equal("#1D4ED8", model.Items[0].Color);
    }

    [Fact]
    public void TypeMode_UsesTypeReportAndRotatesColors()
    {
        var reports = new ReportStub
        {
            Types = Enumerable.Range(0, 16).Select(i => ($"Type{i}", 1)).ToList()
        };
        var model = new TreeMapModel(new NavigationStub(), new DocumentRepositoryStub(), reports);

        model.ShowByTypeCommand.Execute(null);

        Assert.Equal("type", model.SelectedMode);
        Assert.Equal(16, model.TotalDocuments);
        Assert.Equal(16, model.Items.Count);
        Assert.Equal("Type0", model.Items[0].Label);
        Assert.Equal("#1D4ED8", model.Items[0].Color);
        Assert.Equal(model.Items[0].Color, model.Items[15].Color);
        Assert.Equal(6.25, model.Items[0].Percentage, 2);
        Assert.Equal(1, reports.TypeCalls);
    }

    [Fact]
    public void AllMode_ShowsEveryDocumentAndPreservesGroupedModes()
    {
        var reports = new ReportStub
        {
            Subjects = [("Math", 2)]
        };
        var documents = new DocumentRepositoryStub(
            new StudyDocument { Id = 1, Name = "Guide" },
            new StudyDocument { Id = 2, Name = "Guide" });
        var model = new TreeMapModel(new NavigationStub(), documents, reports);

        model.ShowAllCommand.Execute(null);

        Assert.Equal("all", model.SelectedMode);
        Assert.Equal(2, model.TotalDocuments);
        Assert.Equal(["Guide", "Guide"], model.Items.Select(item => item.Label));
        Assert.All(model.Items, item =>
        {
            Assert.Equal(1, item.Count);
            Assert.Equal(50, item.Percentage);
        });

        model.ShowBySubjectCommand.Execute(null);

        Assert.Equal("subject", model.SelectedMode);
        Assert.Equal(2, model.TotalDocuments);
        Assert.Equal("Math", model.Items[0].Label);
    }

    [Fact]
    public void EmptyReport_UsesZeroPercentagesAndBackNavigates()
    {
        var navigation = new NavigationStub();
        var model = new TreeMapModel(navigation, new DocumentRepositoryStub(), new ReportStub());

        Assert.Empty(model.Items);
        Assert.Equal(0, model.TotalDocuments);
        model.GoBackCommand.Execute(null);

        Assert.Equal(["dashboard"], navigation.Routes);
    }

    [Fact]
    public void PaletteKeepsWhiteTextAtAccessibleContrast()
    {
        var reports = new ReportStub
        {
            Types = Enumerable.Range(0, 15).Select(i => ($"Type{i}", 1)).ToList()
        };
        var model = new TreeMapModel(new NavigationStub(), new DocumentRepositoryStub(), reports);

        model.ShowByTypeCommand.Execute(null);

        Assert.All(model.Items, item =>
            Assert.True(ContrastRatio(item.Color, "#FFFFFF") >= 4.5, item.Color));
    }

    private static double ContrastRatio(string foreground, string background)
    {
        var foregroundColor = Avalonia.Media.Color.Parse(foreground);
        var backgroundColor = Avalonia.Media.Color.Parse(background);
        var foregroundLuminance = RelativeLuminance(foregroundColor);
        var backgroundLuminance = RelativeLuminance(backgroundColor);
        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(Avalonia.Media.Color color)
    {
        static double Normalize(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Normalize(color.R))
            + (0.7152 * Normalize(color.G))
            + (0.0722 * Normalize(color.B));
    }

    private sealed class ReportStub : IReportRepository
    {
        public List<(string Label, int Count)> Subjects { get; set; } = [];
        public List<(string Label, int Count)> Types { get; set; } = [];
        public int TypeCalls { get; private set; }
        public List<(string Label, int Count)> GetBySubject() => Subjects;
        public List<(string Label, int Count)> GetByType() { TypeCalls++; return Types; }
        public List<(string Label, int Count)> GetByDay(int days = 7) => [];
        public List<(string Label, int Count)> GetByMonth(int months = 12) => [];
    }

    private sealed class NavigationStub : INavigationService
    {
        public List<string> Routes { get; } = [];
        public bool CanGoBack => false;
        public void NavigateTo(string viewKey) => Routes.Add(viewKey);
        public void NavigateTo(string viewKey, object? parameter) => Routes.Add(viewKey);
        public void GoBack() { }
    }
}

public sealed class UpdateServiceProofTests
{
    [Fact]
    public void ParseResponse_MapsReleaseAndDetectsCurrentOrNewVersion()
    {
        var current = Parse("{\"tag_name\":\"v4.1.0\",\"body\":\"notes\",\"html_url\":\"https://example.test/release\",\"assets\":[{\"name\":\"Study_Setup.exe\",\"browser_download_url\":\"https://example.test/setup.exe\"}]}");
        var newer = Parse("{\"tag_name\":\"v4.2.0\",\"body\":\"notes\",\"html_url\":\"https://example.test/release\",\"assets\":[]}");

        Assert.NotNull(current);
        Assert.False(current!.HasUpdate);
        Assert.Equal("https://example.test/setup.exe", current.DownloadUrl);
        Assert.NotNull(newer);
        Assert.True(newer!.HasUpdate);
        Assert.Equal("https://example.test/release", newer.ReleasePageUrl);
    }

    [Fact]
    public void ParseResponse_MalformedJson_ReturnsNull()
    {
        Assert.Null(Parse("not-json"));
        Assert.Null(Parse("{\"tag_name\": [1]}"));
    }

    [Fact]
    public async Task HandleUpdate_WhenNotConfirmed_DoesNotOpenBrowser()
    {
        var dialog = new DialogStub { ConfirmResult = false };
        var service = new UpdateService(dialog, new LocalizationStub(), new ToastStub());

        await service.HandleUpdateAsync(new UpdateInfo { HasUpdate = true, NewVersion = "v4.1.0" });

        Assert.NotNull(dialog.LastConfirmMessage);
        Assert.Null(dialog.LastErrorMessage);
    }

    [Fact]
    public async Task HandleUpdate_WhenDialogCancelled_PropagatesCancellation()
    {
        var dialog = new DialogStub { CancelConfirmation = true };
        var service = new UpdateService(dialog, new LocalizationStub(), new ToastStub());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.HandleUpdateAsync(new UpdateInfo { HasUpdate = true, NewVersion = "v4.1.0" }));
    }

    [Fact]
    public async Task HandleUpdate_WhenNoUpdate_SkipsDialog()
    {
        var dialog = new DialogStub();
        var service = new UpdateService(dialog, new LocalizationStub(), new ToastStub());

        await service.HandleUpdateAsync(new UpdateInfo { HasUpdate = false });

        Assert.Null(dialog.LastConfirmMessage);
    }

    private static UpdateInfo? Parse(string json)
    {
        var method = typeof(UpdateService).GetMethod("ParseResponse", BindingFlags.Static | BindingFlags.NonPublic);
        return (UpdateInfo?)method!.Invoke(null, [json]);
    }

    private sealed class DialogStub : IDialogService
    {
        public bool ConfirmResult { get; init; } = true;
        public bool CancelConfirmation { get; init; }
        public string? LastConfirmMessage { get; private set; }
        public string? LastErrorMessage { get; private set; }
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) { LastErrorMessage = message; return Task.CompletedTask; }
        public Task<bool> ShowConfirmAsync(string title, string message)
        {
            LastConfirmMessage = message;
            return CancelConfirmation ? Task.FromCanceled<bool>(new CancellationToken(true)) : Task.FromResult(ConfirmResult);
        }
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
            => ShowConfirmAsync(title, message);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "")
            => Task.FromResult<string?>(null);
    }

    private sealed class LocalizationStub : ILocalizationService
    {
        public string this[string key] => key;
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public void SetLanguage(SupportedLanguage language)
            => LanguageChanged?.Invoke(this, EventArgs.Empty);
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = Enum.GetValues<SupportedLanguage>();
        public event EventHandler? LanguageChanged;
    }

    private sealed class ToastStub : IToastService
    {
        public void Show(string message, ToastType type = ToastType.Info, int durationMs = 3000) { }
    }
}

public sealed class MainWindowUpdateProofTests
{
    [Fact]
    public async Task CheckForUpdate_NoRelease_ShowsConnectionErrorAndStatus()
    {
        var dialog = new WindowDialogStub();
        var update = new FakeUpdateService { Result = null };
        var model = CreateModel(dialog, update);

        await model.CheckForUpdateCommand.ExecuteAsync(null);

        Assert.Equal("Main_CannotConnect", dialog.LastMessage);
        Assert.Equal("Status_CannotCheckUpdate", model.StatusText);
    }

    [Fact]
    public async Task CheckForUpdate_CurrentRelease_ShowsLatestStatus()
    {
        var dialog = new WindowDialogStub();
        var update = new FakeUpdateService { Result = new UpdateInfo { HasUpdate = false } };
        var model = CreateModel(dialog, update);

        await model.CheckForUpdateCommand.ExecuteAsync(null);

        Assert.Contains("4.1.0", dialog.LastMessage);
        Assert.Equal("Status_UpToDate", model.StatusText);
    }

    [Fact]
    public async Task CheckForUpdate_NewRelease_DelegatesAndUpdatesStatus()
    {
        var update = new FakeUpdateService { Result = new UpdateInfo { HasUpdate = true, NewVersion = "v4.1.0" } };
        var model = CreateModel(new WindowDialogStub(), update);

        await model.CheckForUpdateCommand.ExecuteAsync(null);

        Assert.Equal("v4.1.0", update.Handled?.NewVersion);
        Assert.Contains("v4.1.0", model.StatusText);
    }

    private static MainWindowModel CreateModel(WindowDialogStub dialog, FakeUpdateService update)
    {
        var dashboard = new DashboardModel(null!, null!, null!, null!, null!, dialog, null!, null!, null!, null!, null!, null!, null!, new LocalizationStub());
        return new MainWindowModel(dashboard, new NavigationStub(), dialog, null!, null!, new LifecycleStub(), new LocalizationStub(), new SettingsStub(), update);
    }

    private sealed class FakeUpdateService : IUpdateService
    {
        public UpdateInfo? Result { get; init; }
        public UpdateInfo? Handled { get; private set; }
        public Task<UpdateInfo?> CheckForUpdateAsync() => Task.FromResult(Result);
        public Task CheckSilentlyAsync() => Task.CompletedTask;
        public Task HandleUpdateAsync(UpdateInfo update) { Handled = update; return Task.CompletedTask; }
    }

    private sealed class WindowDialogStub : IDialogService
    {
        public string? LastMessage { get; private set; }
        public Task ShowMessageAsync(string title, string message) { LastMessage = message; return Task.CompletedTask; }
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

    private sealed class LocalizationStub : ILocalizationService
    {
        public string this[string key] => key switch
        {
            "Main_AlreadyLatest" => "latest {0}",
            "Status_NewVersionAvailable" => "new {0}",
            _ => key
        };
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public void SetLanguage(SupportedLanguage language)
            => LanguageChanged?.Invoke(this, EventArgs.Empty);
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = Enum.GetValues<SupportedLanguage>();
        public event EventHandler? LanguageChanged;
    }

    private sealed class NavigationStub : INavigationService
    {
        public bool CanGoBack => false;
        public void NavigateTo(string viewKey) { }
        public void NavigateTo(string viewKey, object? parameter) { }
        public void GoBack() { }
    }

    private sealed class LifecycleStub : IApplicationLifecycleService
    {
        public void Shutdown() { }
    }
}
