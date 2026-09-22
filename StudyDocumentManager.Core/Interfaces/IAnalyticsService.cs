namespace StudyDocumentManager.Core.Interfaces;

public interface IAnalyticsService
{
    Task CaptureAsync(string eventName, CancellationToken cancellationToken = default);
}
