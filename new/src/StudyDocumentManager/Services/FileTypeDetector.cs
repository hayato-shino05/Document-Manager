namespace StudyDocumentManager.Services;

/// <summary>
/// Single source of truth for mapping file extensions to document type labels.
/// These labels are stored in the DB (loai column) and must align with
/// DocumentTypeIconConverter for correct icon display.
///
/// Label vocabulary:
///   "PDF"         — .pdf
///   "Word"        — .doc .docx .odt (LibreOffice Writer)
///   "Excel"       — .xls .xlsx .ods .csv (LibreOffice Calc + CSV)
///   "Dữ liệu"    — .tsv .json .xml .yaml .yml (structured data, NOT csv)
///   "Code"        — .py .js .ts .html .css .c .cpp .java .rs .go .php .rb .sh .bat .sql
///   "Sách"        — .epub .mobi .azw .fb2 (ebook)
///   "Hình ảnh"   — .jpg .jpeg .png .gif .bmp .ico .tiff .webp .svg .raw .heic
///   "Video"       — .mp4 .avi .mkv .mov .wmv .webm .flv .m4v .3gp .mpg .mpeg
///   "Audio"       — .mp3 .wav .flac .m4a .aac .ogg .wma .opus .ape
///   "Audio"       — .mp3 .wav .flac .m4a .aac .ogg .wma .opus .ape
///   "Nén"         — .zip .rar .7z .tar .gz .bz2 .xz .zst
///   "Tài liệu"   — .txt .md .rtf (plain text)
///   "Thiết kế"   — .psd .ai .xd .fig .sketch .indd
///   "Khác"        — anything not matched
/// </summary>
public static class FileTypeDetector
{
    /// <summary>
    /// Detects document type label from a file extension (e.g. ".pdf" → "PDF").
    /// Extension must include the dot: ".pdf", ".docx".
    /// </summary>
    public static string Detect(string extension) => extension.ToLowerInvariant() switch
    {
        // ── Office documents ──────────────────────────────────────────
        ".pdf"                          => "PDF",
        ".doc"  or ".docx" or ".odt"   => "Word",      // + LibreOffice Writer
        ".ppt"  or ".pptx" or ".odp"   => "PowerPoint", // + LibreOffice Impress
        ".xls"  or ".xlsx" or ".ods" or ".csv" => "Excel", // + LibreOffice Calc + CSV
        ".txt"  or ".md"  or ".rtf"    => "Tài liệu",

        // ── Data / Structured ─────────────────────────────────────────
        ".tsv" or
        ".json" or ".xml" or
        ".yaml" or ".yml"              => "Dữ liệu",

        // ── Code / Source ─────────────────────────────────────────────
        ".py" or ".ipynb" or
        ".js" or ".ts" or
        ".html" or ".htm" or ".css" or
        ".c" or ".h" or ".cpp" or ".cxx" or
        ".java" or ".kt" or
        ".rs" or ".go" or ".rb" or
        ".php" or ".cs" or ".vb" or
        ".sh" or ".bat" or ".ps1" or
        ".sql" or ".r" or ".m"         => "Code",

        // ── eBook ─────────────────────────────────────────────────────
        ".epub" or ".mobi" or
        ".azw" or ".azw3" or ".fb2"   => "Sách",

        // ── Image ─────────────────────────────────────────────────────
        ".jpg"  or ".jpeg" or
        ".png"  or ".gif"  or
        ".bmp"  or ".ico"  or
        ".tiff" or ".tif"  or
        ".webp" or ".svg"  or
        ".raw"  or ".heic" or ".heif" => "Hình ảnh",

        // ── Video  (.ts excluded — TypeScript was matched earlier) ─────
        ".mp4" or ".avi" or ".mkv" or
        ".mov" or ".wmv" or ".webm" or
        ".flv" or ".m4v" or ".3gp" or
        ".mpg" or ".mpeg"              => "Video",

        // ── Audio ─────────────────────────────────────────────────────
        ".mp3" or ".wav" or ".flac" or
        ".m4a" or ".aac" or ".ogg" or
        ".wma" or ".opus" or ".ape"   => "Audio",

        // ── Archive / Compressed ───────────────────────────────────────
        ".zip" or ".rar" or ".7z" or
        ".tar" or ".gz" or ".bz2" or
        ".xz"  or ".zst"              => "Nén",

        // ── Design ────────────────────────────────────────────────────
        ".psd" or ".ai" or ".xd" or
        ".fig" or ".sketch" or ".indd" => "Thiết kế",

        // ── Fallback ──────────────────────────────────────────────────
        _                              => "Khác"
    };

    /// <summary>
    /// Detects document type label from a full file path.
    /// </summary>
    public static string DetectFromPath(string filePath)
        => Detect(Path.GetExtension(filePath));
}
