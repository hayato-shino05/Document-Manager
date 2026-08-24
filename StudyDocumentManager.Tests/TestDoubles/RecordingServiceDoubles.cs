using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Tests.TestDoubles;

/// <summary>
/// Records every dialog interaction into a shared timeline so tests can assert
/// message content and ordering against other services (e.g. lifecycle shutdown).
/// </summary>
public sealed class RecordingDialogService(List<string>? timeline = null) : IDialogService
{
    public List<string> Timeline { get; } = timeline ?? new List<string>();

    public bool ConfirmResult { get; set; }

    public string? InputResult { get; set; }

    public Task ShowMessageAsync(string title, string message)
    {
        Timeline.Add($"message|{title}|{message}");
        return Task.CompletedTask;
    }

    public Task ShowErrorAsync(string title, string message)
    {
        Timeline.Add($"error|{title}|{message}");
        return Task.CompletedTask;
    }

    public Task<bool> ShowConfirmAsync(string title, string message)
        => ShowConfirmAsync(title, message, "OK");

    public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
    {
        Timeline.Add($"confirm|{title}|{message}|{confirmText}|danger={isDanger}");
        return Task.FromResult(ConfirmResult);
    }

    public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "")
    {
        Timeline.Add($"input|{title}|{label}");
        return Task.FromResult(InputResult);
    }
}

/// <summary>
/// Records shutdown calls into the shared timeline for ordering assertions.
/// </summary>
public sealed class RecordingLifecycleService(List<string>? timeline = null) : IApplicationLifecycleService
{
    public List<string> Timeline { get; } = timeline ?? new List<string>();

    public int ShutdownCount { get; private set; }

    public void Shutdown()
    {
        ShutdownCount++;
        Timeline.Add("shutdown");
    }
}

public sealed class StubFileDialogService(string? openFileResult = null) : IFileDialogService
{
    public Task<string?> ShowOpenFileAsync(string title, string? filter = null)
        => Task.FromResult(openFileResult);

    public Task<string?> ShowOpenFolderAsync(string title)
        => Task.FromResult<string?>(null);

    public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null)
        => Task.FromResult<string?>(null);
}

public sealed class StubNavigationService : INavigationService
{
    public List<string> Navigated { get; } = new();

    public bool CanGoBack => false;

    public void NavigateTo(string viewKey) => Navigated.Add(viewKey);

    public void NavigateTo(string viewKey, object? parameter) => Navigated.Add(viewKey);

    public void GoBack()
    {
    }
}

public sealed class StubProcessLauncherService : IProcessLauncherService
{
    public bool ThrowOnOpenFolder { get; set; }

    public List<string> OpenedFiles { get; } = new();
    public List<string> OpenedFolders { get; } = new();
    public List<string> OpenedUrls { get; } = new();
    public List<string> Revealed { get; } = new();

    public void OpenFile(string filePath) => OpenedFiles.Add(filePath);

    public void OpenFolder(string folderPath)
    {
        if (ThrowOnOpenFolder)
            throw new IOException("launch failed");
        OpenedFolders.Add(folderPath);
    }

    public void RevealInExplorer(string filePath) => Revealed.Add(filePath);

    public void OpenUrl(string url) => OpenedUrls.Add(url);
}

/// <summary>
/// Returns format templates for keys under assertion and the raw key otherwise,
/// so formatted messages stay deterministic without loading resx resources.
/// </summary>
public sealed class KeyLocalizationService : ILocalizationService
{
    private static readonly Dictionary<string, string> Templates = new(StringComparer.Ordinal)
    {
        ["RC_ConfirmRestoreMessage"] = "restore {0}|count {1}|db {2}",
    };

    public string this[string key]
        => Templates.TryGetValue(key, out var template) ? template : key;

    public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;

    public void SetLanguage(SupportedLanguage language)
    {
    }

    public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = Array.Empty<SupportedLanguage>();

    public event EventHandler? LanguageChanged;
}
