using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Services;

/// <summary>
/// Information about an available update from GitHub Releases.
/// </summary>
public class UpdateInfo
{
    public bool HasUpdate { get; set; }
    public string NewVersion { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleasePageUrl { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
}

/// <summary>
/// Checks GitHub Releases API for new versions.
/// Ported from WinForms (WebClient â†’ HttpClient, JavaScriptSerializer â†’ System.Text.Json).
/// </summary>
public static class UpdateChecker
{
    private const string RepoOwner = "hayato-shino05";
    private const string RepoName = "study-document-manager";
    private static readonly string ApiUrl =
        $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

    private static readonly HttpClient _httpClient = CreateHttpClient();

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

    /// <summary>
    /// Check for updates asynchronously. Returns null on any error (offline, etc.)
    /// </summary>
    public static async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            var json = await _httpClient.GetStringAsync(ApiUrl);
            return ParseResponse(json);
        }
        catch
        {
            // Silently fail if no internet or API error
            return null;
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

            // Find Setup.exe download URL from assets
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
