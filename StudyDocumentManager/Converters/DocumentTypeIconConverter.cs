using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Svg.Skia;

namespace StudyDocumentManager.Converters;

/// <summary>
/// Converts a document type string (e.g., "PDF", "Word", "Audio") to its corresponding icon (PNG or SVG).
///
/// Primary labels (from FileTypeDetector):
///   "PDF"        → pdf.png
///   "Word"       → file_type_word_icon_130070.svg
///   "Excel"      → file_type_excel_icon_130611.svg         (incl. csv)
///   "PowerPoint" → file_type_powerpoint_icon_130245.svg
///   "Data"       → data_filetype_icon.svg                  (json, xml, yaml, tsv...)
///   "Code"       → code_filetype_icon.svg
///   "Book"       → ilustracoes_04-10_icon-icons.com_75464.svg
///   "Image"      → jpg.png / png.png
///   "Video"      → file_type_video_icon_130090.svg
///   "Audio"      → ext_audio_generic_filetype_icon_176230.svg
///   "Archive"    → zip_filetype_icon_177508.svg
///   "Design"     → psd_file_design_graphic_digital_artwork_adobe_photoshop_icon_191032.svg
///   "Document", "Report", "Guide", "Form", "Other"
///                → text_filetype_icon_177517.svg
///
/// Backward compat (raw extension strings stored by old documents):
///   "psd"  → psd_file_design_...svg
///   "ai"   → file_type_ai_icon_130757.svg
///   "xd"   → software_adobe_xd_app_computer_icon_191050.svg
///   "webm", "m4v", "3gp"... → video icon
///   "wma", "opus", "ape"... → audio icon
///   "ico", "tiff", "raw"... → image icon
/// </summary>
public class DocumentTypeIconConverter : IValueConverter
{
    public static readonly DocumentTypeIconConverter Instance = new();

    // PNG bitmap cache
    private static Bitmap? _pdfIcon;
    private static Bitmap? _imgJpgIcon;
    private static Bitmap? _imgPngIcon;

    // SVG image cache
    private static SvgImage? _wordIcon;
    private static SvgImage? _excelIcon;
    private static SvgImage? _pptIcon;
    private static SvgImage? _videoIcon;
    private static SvgImage? _audioIcon;
    private static SvgImage? _zipIcon;
    private static SvgImage? _codeIcon;
    private static SvgImage? _bookIcon;
    private static SvgImage? _dataIcon;
    private static SvgImage? _designPsdIcon;   // Photoshop / generic design
    private static SvgImage? _designAiIcon;    // Illustrator
    private static SvgImage? _designXdIcon;    // Adobe XD
    private static SvgImage? _defaultIcon;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var lowerType = (value as string)?.ToLowerInvariant() ?? string.Empty;

        return lowerType switch
        {
            // ── PDF ──────────────────────────────────────────────────────────
            "pdf"
                => GetOrLoadBitmap(ref _pdfIcon, "pdf.png"),

            // ── Word (+ LibreOffice Writer backward compat) ──────────────────
            "word" or "doc" or "docx" or "odt"
                => GetOrLoadSvg(ref _wordIcon, "file_type_word_icon_130070.svg"),

            // ── Excel (incl. csv, LibreOffice Calc backward compat) ──────────
            "excel" or "xls" or "xlsx" or "ods" or "csv"
                => GetOrLoadSvg(ref _excelIcon, "file_type_excel_icon_130611.svg"),

            // ── PowerPoint (+ LibreOffice Impress) ───────────────────────────
            "powerpoint" or "ppt" or "pptx" or "odp"
                => GetOrLoadSvg(ref _pptIcon, "file_type_powerpoint_icon_130245.svg"),

            // ── Data / Structured (json, xml, yaml, tsv...) ──────────────────
            "data" or "tsv" or "json" or "xml" or "yaml" or "yml"
                => GetOrLoadSvg(ref _dataIcon, "data_filetype_icon.svg"),

            // ── Code ─────────────────────────────────────────────────────────
            "code" or
            "py" or "ipynb" or "js" or "ts" or "html" or "htm" or "css" or
            "c" or "h" or "cpp" or "cxx" or "java" or "kt" or
            "rs" or "go" or "rb" or "php" or "cs" or "vb" or
            "sh" or "bat" or "ps1" or "sql" or "r"
                => GetOrLoadSvg(ref _codeIcon, "code_filetype_icon.svg"),

            // ── eBook ─────────────────────────────────────────────────────────
            "book" or "epub" or "mobi" or "azw" or "azw3" or "fb2"
                => GetOrLoadSvg(ref _bookIcon, "ilustracoes_04-10_icon-icons.com_75464.svg"),

            // ── Image ────────────────────────────────────────────────────────
            "image" or "jpeg" or "jpg"
                => GetOrLoadBitmap(ref _imgJpgIcon, "jpg.png"),
            "png" or "gif" or "bmp" or "webp" or
            "ico" or "tiff" or "tif" or "raw" or "heic" or "heif" or "svg"
                => GetOrLoadBitmap(ref _imgPngIcon, "png.png"),

            // ── Video (+ backward compat: webm, m4v, 3gp, mpg) ──────────────
            "video" or
            "mp4" or "avi" or "mkv" or "mov" or "wmv" or
            "webm" or "flv" or "m4v" or "3gp" or "ts" or "mpg" or "mpeg"
                => GetOrLoadSvg(ref _videoIcon, "file_type_video_icon_130090.svg"),

            // ── Audio (+ backward compat: wma, opus, ape) ────────────────────
            "audio" or
            "mp3" or "wav" or "flac" or "m4a" or "aac" or
            "ogg" or "wma" or "opus" or "ape"
                => GetOrLoadSvg(ref _audioIcon, "ext_audio_generic_filetype_icon_176230.svg"),

            // ── Archive / Compressed (+ backward compat: xz, zst) ───────────
            "archive" or "zip" or "rar" or "7z" or "tar" or "gz" or "bz2" or "xz" or "zst"
                => GetOrLoadSvg(ref _zipIcon, "zip_filetype_icon_177508.svg"),

            // ── Design — Adobe Illustrator ────────────────────────────────────
            "ai"
                => GetOrLoadSvg(ref _designAiIcon, "file_type_ai_icon_130757.svg"),

            // ── Design — Adobe XD ─────────────────────────────────────────────
            "xd"
                => GetOrLoadSvg(ref _designXdIcon, "software_adobe_xd_app_computer_icon_191050.svg"),

            // ── Design — Photoshop / generic design ─────────────────────────
            "design" or "psd" or "fig" or "sketch" or "indd"
                => GetOrLoadSvg(ref _designPsdIcon, "psd_file_design_graphic_digital_artwork_adobe_photoshop_icon_191032.svg"),

            // ── Default (Document, Report, Guide, Form, Other...) ─────────
            _ => GetOrLoadSvg(ref _defaultIcon, "text_filetype_icon_177517.svg"),
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();

    // ── Loaders ──────────────────────────────────────────────────────────────

    private static IImage? GetOrLoadBitmap(ref Bitmap? cache, string filename)
    {
        if (cache != null) return cache;
        try
        {
            var assemblyName = typeof(DocumentTypeIconConverter).Assembly.GetName().Name
                ?? throw new InvalidOperationException("The document manager assembly name is unavailable.");
            var uri = new Uri($"avares://{assemblyName}/Assets/Icons/Types/{filename}");
            var stream = AssetLoader.Open(uri);
            cache = new Bitmap(stream);
            return cache;
        }
        catch
        {
            return null;
        }
    }

    private static IImage? GetOrLoadSvg(ref SvgImage? cache, string filename)
    {
        if (cache != null) return cache;
        try
        {
            var assemblyName = typeof(DocumentTypeIconConverter).Assembly.GetName().Name
                ?? throw new InvalidOperationException("The document manager assembly name is unavailable.");
            var uri = new Uri($"avares://{assemblyName}/Assets/Icons/Types/{filename}");
            var stream = AssetLoader.Open(uri);
            var source = SvgSource.LoadFromStream(stream);
            cache = new SvgImage { Source = source };
            return cache;
        }
        catch
        {
            return null;
        }
    }
}
