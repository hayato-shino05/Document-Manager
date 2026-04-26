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
}
