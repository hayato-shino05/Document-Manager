using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using StudyDocumentManager.Core.DTOs;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Core.Services;
namespace StudyDocumentManager.Services;

public class UpdateService : IUpdateService
{
    private const string RepoOwner = "hayato-shino05";
    private const string RepoName = "study-document-manager";
    private static readonly string ApiUrl =
        $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _loc;
    private readonly IToastService _toast;
    private readonly IProcessLauncherService? _launcher;

    public UpdateService(IDialogService dialogService, ILocalizationService loc, IToastService toast, IProcessLauncherService? launcher = null)
    {
        _dialogService = dialogService;
        _loc = loc;
        _toast = toast;
        _launcher = launcher;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("StudyDocumentManager", AppVersion.Current));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            var json = await HttpClient.GetStringAsync(ApiUrl);
            return ParseResponse(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task CheckSilentlyAsync()
    {
        var info = await CheckForUpdateAsync();
        if (info is { HasUpdate: true })
        {
            _toast.Show(
                string.Format(_loc["Update_ToastNewVersion"], info.NewVersion),
                ToastType.Info,
                5000);
        }
    }

    public async Task HandleUpdateAsync(UpdateInfo update)
    {
        if (update == null || !update.HasUpdate) return;

        var message = string.Format(_loc["Update_NewVersionReady"], update.NewVersion) + "\n\n";

        if (!string.IsNullOrEmpty(update.ReleaseNotes))
        {
            var notes = update.ReleaseNotes.Length > 200
                ? update.ReleaseNotes[..200] + "..."
                : update.ReleaseNotes;
            message += $"Release Notes:\n{notes}\n\n";
        }

        message += _loc["Update_OpenDownloadPage"];

        var confirmed = await _dialogService.ShowConfirmAsync(_loc["Update_DialogTitle"], message);
        if (!confirmed) return;

        var url = !string.IsNullOrEmpty(update.ReleasePageUrl)
            ? update.ReleasePageUrl
            : $"https://github.com/{RepoOwner}/{RepoName}/releases/latest";

        try
        {
            if (_launcher != null)
            {
                _launcher.OpenUrl(url);
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            await _dialogService.ShowErrorAsync(_loc["Update_ErrorTitle"],
                string.Format(_loc["Update_BrowserError"], url));
        }
    }

    private static UpdateInfo? ParseResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.TryGetProperty("tag_name", out var tagProp)
                ? tagProp.GetString() ?? "" : "";
            var body = root.TryGetProperty("body", out var bodyProp)
                ? bodyProp.GetString() ?? "" : "";
            var htmlUrl = root.TryGetProperty("html_url", out var urlProp)
                ? urlProp.GetString() ?? "" : "";

            string? setupUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var nameProp)
                        ? nameProp.GetString() ?? "" : "";
                    if (name.EndsWith("_Setup.exe", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        setupUrl = asset.TryGetProperty("browser_download_url", out var dlProp)
                            ? dlProp.GetString() : null;
                        break;
                    }
                }
            }

            return new UpdateInfo
            {
                HasUpdate = AppVersion.IsNewer(tagName),
                NewVersion = tagName,
                DownloadUrl = setupUrl ?? "",
                ReleasePageUrl = htmlUrl,
                ReleaseNotes = body
            };
        }
        catch
        {
            return null;
        }
    }
}
