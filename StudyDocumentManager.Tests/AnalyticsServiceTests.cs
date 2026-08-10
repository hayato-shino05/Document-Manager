using System.Net;
using System.Text;
using System.Text.Json;
using StudyDocumentManager.Core.Analytics;
using Xunit;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Tests;

public sealed class AnalyticsServiceTests
{
    [Fact]
    public async Task CaptureAsync_AllowedEvent_SendsMinimalPayload()
    {
        var handler = new RecordingHandler(HttpStatusCode.Accepted);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://analytics.example/")
        };
        var service = new AnalyticsService(client, new InstallationIdentityStub("installation-123"));

        await service.CaptureAsync("app_opened");

        Assert.Equal("/api/events", handler.RequestPath);

        var requestBody = handler.RequestBody;
        Assert.NotNull(requestBody);
        using var payload = JsonDocument.Parse(requestBody!);
        var root = payload.RootElement;
        Assert.Equal("installation-123", root.GetProperty("installation_id").GetString());
        Assert.Equal("app_opened", root.GetProperty("event").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("app_version").GetString()));
        Assert.Equal("windows", root.GetProperty("platform").GetString());
        Assert.Equal(JsonValueKind.String, root.GetProperty("occurred_at").ValueKind);
        Assert.Equal(
            ["app_version", "event", "installation_id", "occurred_at", "platform"],
            root.EnumerateObject().Select(property => property.Name).OrderBy(name => name));
    }

    [Fact]
    public async Task CaptureAsync_UnknownEvent_DoesNotSendRequest()
    {
        var handler = new RecordingHandler(HttpStatusCode.Accepted);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://analytics.example/")
        };
        var service = new AnalyticsService(client, new InstallationIdentityStub("installation-123"));

        await service.CaptureAsync("document_path_copied");

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task CaptureAsync_RequestFailure_IsIgnored()
    {
        using var client = new HttpClient(new FailingHandler())
        {
            BaseAddress = new Uri("https://analytics.example/")
        };
        var service = new AnalyticsService(client, new InstallationIdentityStub("installation-123"));

        await service.CaptureAsync("document_opened");
    }

    [Fact]
    public async Task CaptureAsync_TimedOutRequest_IsIgnored()
    {
        using var client = new HttpClient(new DelayedHandler())
        {
            BaseAddress = new Uri("https://analytics.example/"),
            Timeout = TimeSpan.FromMilliseconds(50)
        };
        var service = new AnalyticsService(client, new InstallationIdentityStub("installation-123"));

        await service.CaptureAsync("session_started");
    }

    [Fact]
    public async Task CaptureAsync_IdentityFailure_IsIgnored()
    {
        var handler = new RecordingHandler(HttpStatusCode.Accepted);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://analytics.example/")
        };
        var service = new AnalyticsService(client, new ThrowingInstallationIdentityStub());

        await service.CaptureAsync("app_opened");

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task CaptureAsync_CallerCancellation_IsPropagated()
    {
        var handler = new RecordingHandler(HttpStatusCode.Accepted);
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://analytics.example/")
        };
        var service = new AnalyticsService(client, new InstallationIdentityStub("installation-123"));
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CaptureAsync("app_opened", cancellationTokenSource.Token));

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task CaptureAsync_UsesServiceOwnedTimeout()
    {
        using var client = new HttpClient(new DelayedHandler())
        {
            BaseAddress = new Uri("https://analytics.example/"),
            Timeout = Timeout.InfiniteTimeSpan
        };
        var service = new AnalyticsService(client, new InstallationIdentityStub("installation-123"));
        var deadline = DateTimeOffset.UtcNow.AddSeconds(12);

        await service.CaptureAsync("session_started");

        Assert.True(DateTimeOffset.UtcNow < deadline);
    }

    private sealed class InstallationIdentityStub(string installationId) : IInstallationIdentityService
    {
        public string GetInstallationId() => installationId;

        public void DeleteInstallationId()
        {
        }
    }


    private sealed class ThrowingInstallationIdentityStub : IInstallationIdentityService
    {
        public string GetInstallationId() => throw new InvalidOperationException("Registry identity is unavailable.");

        public void DeleteInstallationId()
        {
        }
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        public string? RequestBody { get; private set; }

        public string? RequestPath { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestPath = request.RequestUri?.AbsolutePath;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode);
        }
    }

    private sealed class DelayedHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage();
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Analytics endpoint is unavailable.");
    }
}
