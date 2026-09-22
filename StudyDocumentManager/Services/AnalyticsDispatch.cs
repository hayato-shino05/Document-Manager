using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public static class AnalyticsDispatch
{
    public static void Capture(IAnalyticsService analytics, string eventName)
    {
        ArgumentNullException.ThrowIfNull(analytics);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        _ = CaptureAsync(analytics, eventName);
    }

    private static async Task CaptureAsync(IAnalyticsService analytics, string eventName)
    {
        try
        {
            await analytics.CaptureAsync(eventName).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.WriteLine(
                $"Analytics capture failed. Event={eventName}; Exception={exception.GetType().Name}");
        }
    }
}
