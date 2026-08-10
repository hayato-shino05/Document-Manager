using System.Text.Json.Serialization;

namespace StudyDocumentManager.Core.Analytics;

public sealed record AnalyticsEvent(
    [property: JsonPropertyName("installation_id")] string InstallationId,
    [property: JsonPropertyName("event")] string EventName,
    [property: JsonPropertyName("app_version")] string AppVersion,
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("occurred_at")] DateTimeOffset OccurredAt);
