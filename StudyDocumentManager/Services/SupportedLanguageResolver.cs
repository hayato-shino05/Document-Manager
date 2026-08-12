using System.Globalization;
using StudyDocumentManager.Core;

namespace StudyDocumentManager.Services;

public readonly record struct SupportedLanguageResolution(
    SupportedLanguage Language,
    bool UsedSavedLanguage);

public static class SupportedLanguageResolver
{
    public static SupportedLanguage FromCulture(CultureInfo? culture)
        => culture?.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "en" => SupportedLanguage.English,
            "vi" => SupportedLanguage.Vietnamese,
            "zh" => SupportedLanguage.Chinese,
            "ja" => SupportedLanguage.Japanese,
            _ => SupportedLanguage.Japanese
        };

    public static SupportedLanguageResolution Resolve(string? savedLanguage, CultureInfo? culture)
        => TryResolveSavedLanguage(savedLanguage) is { } saved
            ? new SupportedLanguageResolution(saved, UsedSavedLanguage: true)
            : new SupportedLanguageResolution(FromCulture(culture), UsedSavedLanguage: false);

    private static SupportedLanguage? TryResolveSavedLanguage(string? savedLanguage)
    {
        if (string.IsNullOrWhiteSpace(savedLanguage))
            return null;

        if (!Enum.TryParse<SupportedLanguage>(savedLanguage, ignoreCase: true, out var saved))
            return null;

        if (!Enum.IsDefined(saved))
            return null;

        return saved.ToString().Equals(savedLanguage.Trim(), StringComparison.OrdinalIgnoreCase)
            ? saved
            : null;
    }
}
