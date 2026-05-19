namespace StudyDocumentManager.Core.Services;

/// <summary>
/// ファイル拡張子からドキュメント種別ラベルを判定するユーティリティ
/// DB (loai列) に格納する種別名を返す — プラットフォーム非依存
/// </summary>
public static class FileTypeDetector
{
    /// <summary>
    /// 拡張子 → ドキュメント種別ラベル（例: ".pdf" → "PDF"）
    /// </summary>
    public static string Detect(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf"                          => "PDF",
        ".doc"  or ".docx" or ".odt"   => "Word",
        ".ppt"  or ".pptx" or ".odp"   => "PowerPoint",
        ".xls"  or ".xlsx" or ".ods" or ".csv" => "Excel",
        ".txt"  or ".md"  or ".rtf"    => "Tài liệu",

        ".tsv" or
        ".json" or ".xml" or
        ".yaml" or ".yml"              => "Dữ liệu",

        ".py" or ".ipynb" or
        ".js" or ".ts" or
        ".html" or ".htm" or ".css" or
        ".c" or ".h" or ".cpp" or ".cxx" or
        ".java" or ".kt" or
        ".rs" or ".go" or ".rb" or
        ".php" or ".cs" or ".vb" or
        ".sh" or ".bat" or ".ps1" or
        ".sql" or ".r" or ".m"         => "Code",

        ".epub" or ".mobi" or
        ".azw" or ".azw3" or ".fb2"   => "Sách",

        ".jpg"  or ".jpeg" or
        ".png"  or ".gif"  or
        ".bmp"  or ".ico"  or
        ".tiff" or ".tif"  or
        ".webp" or ".svg"  or
        ".raw"  or ".heic" or ".heif" => "Hình ảnh",

        ".mp4" or ".avi" or ".mkv" or
        ".mov" or ".wmv" or ".webm" or
        ".flv" or ".m4v" or ".3gp" or
        ".mpg" or ".mpeg"              => "Video",

        ".mp3" or ".wav" or ".flac" or
        ".m4a" or ".aac" or ".ogg" or
        ".wma" or ".opus" or ".ape"   => "Audio",

        ".zip" or ".rar" or ".7z" or
        ".tar" or ".gz" or ".bz2" or
        ".xz"  or ".zst"              => "Nén",

        ".psd" or ".ai" or ".xd" or
        ".fig" or ".sketch" or ".indd" => "Thiết kế",

        _                              => "Khác"
    };

    /// <summary>
    /// フルパスからドキュメント種別を判定
    /// </summary>
    public static string DetectFromPath(string filePath)
        => Detect(Path.GetExtension(filePath));
}
