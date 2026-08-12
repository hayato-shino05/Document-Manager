using System.Globalization;
using StudyDocumentManager.Core;

namespace StudyDocumentManager.Services;

public static class SupportedLanguageResolver
{
    public static SupportedLanguage FromCulture(CultureInfo culture)
        => culture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "en" => SupportedLanguage.English,
            "vi" => SupportedLanguage.Vietnamese,
            "zh" => SupportedLanguage.Chinese,
            "ja" => SupportedLanguage.Japanese,
            _ => SupportedLanguage.Japanese
        };

    public static SupportedLanguage Resolve(string? savedLanguage, CultureInfo culture)
        => Enum.TryParse<SupportedLanguage>(savedLanguage, ignoreCase: true, out var saved)
            ? saved
            : FromCulture(culture);
}
