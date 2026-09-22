using StudyDocumentManager.Converters;
using Xunit;

namespace StudyDocumentManager.Tests;

public class DeadlineBrushConverterTests
{
    [Fact]
    public void DeadlineBrushConverter_UsesNamedSharedBrushKeys()
    {
        Assert.Equal("DeadlineOverdueBrush", DeadlineBrushConverter.DeadlineOverdueBrushKey);
        Assert.Equal("DeadlineUrgentBrush", DeadlineBrushConverter.DeadlineUrgentBrushKey);
        Assert.Equal("DeadlineUpcomingBrush", DeadlineBrushConverter.DeadlineUpcomingBrushKey);
    }

    [Fact]
    public void DeadlineTextConverter_UsesNamedSharedBrushKeys()
    {
        Assert.Equal("DeadlineOverdueTextBrush", DeadlineTextConverter.DeadlineOverdueTextBrushKey);
        Assert.Equal("DeadlineUrgentTextBrush", DeadlineTextConverter.DeadlineUrgentTextBrushKey);
        Assert.Equal("DeadlineUpcomingTextBrush", DeadlineTextConverter.DeadlineUpcomingTextBrushKey);
    }

    [Theory]
    [InlineData(-1, "Dashboard_DeadlineStatusOverdue")]
    [InlineData(0, "Dashboard_DeadlineStatusUrgent")]
    [InlineData(2, "Dashboard_DeadlineStatusUrgent")]
    [InlineData(3, "Dashboard_DeadlineStatusUpcoming")]
    [InlineData(6, "Dashboard_DeadlineStatusUpcoming")]
    [InlineData(7, "Dashboard_DeadlineStatusScheduled")]
    public void DeadlineStatusConverter_ProvidesNonColorStatus(int daysFromToday, string expectedKey)
    {
        Assert.Equal(expectedKey, DeadlineStatusConverter.GetStatusKey(DateTime.Today.AddDays(daysFromToday)));
    }

    [Fact]
    public void DeadlineStatusConverter_TreatsMissingDeadlineAsScheduled()
    {
        Assert.Equal(DeadlineStatusConverter.ScheduledKey, DeadlineStatusConverter.GetStatusKey(null));
    }
}
