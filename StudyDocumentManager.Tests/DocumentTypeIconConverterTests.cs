using System.Globalization;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using StudyDocumentManager.Converters;
using Xunit;
using Avalonia.Headless.XUnit;

namespace StudyDocumentManager.Tests;

public sealed class DocumentTypeIconConverterTests
{
    [AvaloniaTheory]
    [InlineData("PDF")]
    [InlineData("Word")]
    [InlineData("Excel")]
    [InlineData("PowerPoint")]
    [InlineData("Document")]
    [InlineData("Data")]
    [InlineData("Code")]
    [InlineData("Book")]
    [InlineData("Image")]
    [InlineData("Video")]
    [InlineData("Audio")]
    [InlineData("Archive")]
    [InlineData("Design")]
    [InlineData("Other")]
    public void Convert_KnownDocumentType_ReturnsLoadedIcon(string documentType)
    {
        var icon = DocumentTypeIconConverter.Instance.Convert(
            documentType,
            typeof(object),
            parameter: null,
            CultureInfo.InvariantCulture);

        Assert.NotNull(icon);
        Assert.True(icon is Bitmap or SvgImage);
    }
}
