using System;
using System.Globalization;
using Avalonia.Data;
using StudyDocumentManager.Converters;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class ConverterSafetyTests
{
    [Fact]
    public void FileSizeConverter_Convert_FormatsSizesCorrectly()
    {
        var converter = FileSizeConverter.Instance;
        Assert.Equal("500 B", converter.Convert(500L, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("1.5 KB", converter.Convert(1536L, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("2.0 MB", converter.Convert(2 * 1024 * 1024L, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("1.50 GB", converter.Convert((long)(1.5 * 1024 * 1024 * 1024), typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal(string.Empty, converter.Convert(-1L, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal(string.Empty, converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void AllConverters_ConvertBack_ReturnsDoNothing_NeverThrows()
    {
        // 1. Recovery Converters
        Assert.Same(BindingOperations.DoNothing, FileSizeConverter.Instance.ConvertBack("1.5 KB", typeof(long), null, CultureInfo.InvariantCulture));
        Assert.Same(BindingOperations.DoNothing, BackupStatusConverter.Instance.ConvertBack("Valid", typeof(bool), null, CultureInfo.InvariantCulture));

        // 2. Watcher Status Converter
        Assert.Same(BindingOperations.DoNothing, WatcherStatusLabelConverter.Instance.ConvertBack("Running", typeof(string), null, CultureInfo.InvariantCulture));

        // 3. Lang Display Converter
        Assert.Same(BindingOperations.DoNothing, LangDisplayConverter.Instance.ConvertBack("Japanese", typeof(string), null, CultureInfo.InvariantCulture));

        // 4. Document Type Icon Converter
        Assert.Same(BindingOperations.DoNothing, new DocumentTypeIconConverter().ConvertBack(null, typeof(string), null, CultureInfo.InvariantCulture));

        // 5. Deadline Converters
        Assert.Same(BindingOperations.DoNothing, DeadlineBrushConverter.Instance.ConvertBack(null, typeof(DateTime), null, CultureInfo.InvariantCulture));
        Assert.Same(BindingOperations.DoNothing, DeadlineTextConverter.Instance.ConvertBack(null, typeof(DateTime), null, CultureInfo.InvariantCulture));
        Assert.Same(BindingOperations.DoNothing, DeadlineStatusConverter.Instance.ConvertBack("Overdue", typeof(DateTime), null, CultureInfo.InvariantCulture));

        // 6. Dashboard Filter Label Converter
        Assert.Same(BindingOperations.DoNothing, DashboardFilterLabelConverter.Instance.ConvertBack("Filter", typeof(string), null, CultureInfo.InvariantCulture));
    }
}
