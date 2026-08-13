using System.Globalization;
using StudyDocumentManager.Core;

namespace StudyDocumentManager.Services;

public readonly record struct SupportedLanguageResolution(
    SupportedLanguage Language,
    bool UsedSavedLanguage);

public static class SupportedLanguageResolver
{
    private const string InstallerLanguageFileName = "installer-language.ini";

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
        => Resolve(savedLanguage, installerLanguage: null, culture);

    public static SupportedLanguageResolution Resolve(string? savedLanguage, string? installerLanguage, CultureInfo? culture)
        => TryResolveSavedLanguage(savedLanguage) is { } saved
            ? new SupportedLanguageResolution(saved, UsedSavedLanguage: true)
            : TryResolveSavedLanguage(installerLanguage) is { } installed
                ? new SupportedLanguageResolution(installed, UsedSavedLanguage: false)
                : new SupportedLanguageResolution(FromCulture(culture), UsedSavedLanguage: false);

    public static string? ReadInstallerLanguage(string? localAppData = null)
    {
        localAppData ??= Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(localAppData))
            return null;

        var filePath = Path.Combine(localAppData, "StudyDocumentManager", InstallerLanguageFileName);
        if (!File.Exists(filePath))
            return null;

        var consumingPath = filePath + ".consuming";
        try
        {
            File.Move(filePath, consumingPath);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        try
        {
            var language = File.ReadAllLines(consumingPath)
                .Select(line => line.Trim())
                .SkipWhile(line => !line.Equals("[Installer]", StringComparison.OrdinalIgnoreCase))
                .Skip(1)
                .Select(line => line.Split('=', 2))
                .Where(parts => parts.Length == 2
                    && parts[0].Trim().Equals("Language", StringComparison.OrdinalIgnoreCase))
                .Select(parts => parts[1].Trim())
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(language))
            {
                RestoreHandoffFile(consumingPath, filePath);
                return null;
            }

            try
            {
                File.Delete(consumingPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return language;
        }
        catch (IOException)
        {
            RestoreHandoffFile(consumingPath, filePath);
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            RestoreHandoffFile(consumingPath, filePath);
            return null;
        }
    }

    private static void RestoreHandoffFile(string consumingPath, string filePath)
    {
        try
        {
            if (File.Exists(consumingPath) && !File.Exists(filePath))
                File.Move(consumingPath, filePath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

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
