using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace StudyDocumentManager.Converters;

/// <summary>
/// Converts a document type string (e.g., "PDF", "Word", "Excel") to its corresponding icon.
/// </summary>
public class DocumentTypeIconConverter : IValueConverter
{
    public static readonly DocumentTypeIconConverter Instance = new();

    private static Bitmap? _pdfIcon;
    private static Bitmap? _wordIcon;
    private static Bitmap? _excelIcon;
    private static Bitmap? _pptIcon;
    private static Bitmap? _imgIcon;
    private static Bitmap? _videoIcon;
    private static Bitmap? _svgIcon;
    private static Bitmap? _defaultIcon;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string type) return GetOrLoadIcon(ref _defaultIcon, "png");

        var lowerType = type.ToLowerInvariant();
        
        switch (lowerType)
        {
            case "pdf":
                return GetOrLoadIcon(ref _pdfIcon, "pdf");
            case "word":
            case "doc":
            case "docx":
                return GetOrLoadIcon(ref _wordIcon, "word");
            case "excel":
            case "xls":
            case "xlsx":
            case "csv":
                return GetOrLoadIcon(ref _excelIcon, "excel");
            case "powerpoint":
            case "ppt":
            case "pptx":
                return GetOrLoadIcon(ref _pptIcon, "powerpoint");
            case "hình ảnh":
            case "image":
            case "jpg":
            case "png":
            case "jpeg":
            case "gif":
                return GetOrLoadIcon(ref _imgIcon, lowerType.Contains("jpg") ? "jpg" : "png");
            case "video":
            case "mp4":
            case "avi":
            case "mkv":
                return GetOrLoadIcon(ref _videoIcon, "mp4");
            case "svg":
                return GetOrLoadIcon(ref _svgIcon, "svg");
            default:
                return GetOrLoadIcon(ref _defaultIcon, "png"); // Fallback
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static Bitmap? GetOrLoadIcon(ref Bitmap? cachedBitmap, string filename)
    {
        if (cachedBitmap != null) return cachedBitmap;

        try
        {
            var uri = new Uri($"avares://StudyDocumentManager/Assets/Icons/Types/{filename}.png");
            using var stream = AssetLoader.Open(uri);
            cachedBitmap = new Bitmap(stream);
            return cachedBitmap;
        }
        catch
        {
            // If asset not found, swallow and return null or a default
            return null;
        }
    }
}
