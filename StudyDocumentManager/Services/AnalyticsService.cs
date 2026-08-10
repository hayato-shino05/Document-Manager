using System.Net.Http.Json;
using StudyDocumentManager.Core.Analytics;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Core.Services;

namespace StudyDocumentManager.Services;

public sealed class AnalyticsService : IAnalyticsService
{
    private const string CapturePath = "/api/events";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private static readonly HashSet<string> AllowedEventNames = new(StringComparer.Ordinal)
    {
        "app_opened",
        "session_started",
        "app_closed",
        "document_added",
        "document_opened",
        "batch_import_completed",
        "export_completed",
        "app_updated"
    };

    private readonly HttpClient _httpClient;
    private readonly IInstallationIdentityService _installationIdentityService;

    public AnalyticsService(HttpClient httpClient, IInstallationIdentityService installationIdentityService)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _installationIdentityService = installationIdentityService ?? throw new ArgumentNullException(nameof(installationIdentityService));
    }

    public async Task CaptureAsync(string eventName, CancellationToken cancellationToken = default)
    {
        if (!AllowedEventNames.Contains(eventName))
            return;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var analyticsEvent = new AnalyticsEvent(
                _installationIdentityService.GetInstallationId(),
                eventName,
                AppVersion.Current,
                "windows",
                DateTimeOffset.UtcNow);

            using var requestCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestCancellationTokenSource.CancelAfter(RequestTimeout);

            using var response = await _httpClient.PostAsJsonAsync(
                CapturePath,
                analyticsEvent,
                requestCancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (HttpRequestException)
        {
            return;
        }
        catch (InvalidOperationException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }
}
