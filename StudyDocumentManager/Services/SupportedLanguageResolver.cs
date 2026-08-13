using System.Globalization;
using StudyDocumentManager.Core;

namespace StudyDocumentManager.Services;

public readonly record struct SupportedLanguageResolution(
    SupportedLanguage Language,
    bool UsedSavedLanguage);

public static class SupportedLanguageResolver
{
    private const string InstallerLanguageFileName = "installer-language.ini";
    private const string InstallerLanguageMutexName = @"Local\StudyDocumentManager.InstallerLanguageHandoff.v1";

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

    public static InstallerLanguageHandoff? TryClaimInstallerLanguage(
        string? localAppData = null,
        TimeSpan? mutexTimeout = null)
    {
        localAppData ??= Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(localAppData))
            return null;

        var mutex = new Mutex(initiallyOwned: false, InstallerLanguageMutexName);
        try
        {
            try
            {
                if (!mutex.WaitOne(mutexTimeout ?? TimeSpan.FromSeconds(5)))
                {
                    mutex.Dispose();
                    return null;
                }
            }
            catch (AbandonedMutexException)
            {
            }

            var filePath = Path.Combine(localAppData, "StudyDocumentManager", InstallerLanguageFileName);
            var consumingPath = filePath + ".consuming";
            RecoverStaleHandoffFile(consumingPath, filePath);
            if (!File.Exists(filePath))
                return new InstallerLanguageHandoff(null, filePath, consumingPath, mutex);

            try
            {
                File.Move(filePath, consumingPath);
                var language = ReadLanguage(consumingPath);
                if (TryResolveSavedLanguage(language) is not null)
                    return new InstallerLanguageHandoff(language, filePath, consumingPath, mutex);

                RestoreHandoffFile(consumingPath, filePath);
            }
            catch (IOException)
            {
                RestoreHandoffFile(consumingPath, filePath);
            }
            catch (UnauthorizedAccessException)
            {
                RestoreHandoffFile(consumingPath, filePath);
            }

            return new InstallerLanguageHandoff(null, filePath, consumingPath, mutex);
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    private static string? ReadLanguage(string filePath)
        => File.ReadAllLines(filePath)
            .Select(line => line.Trim())
            .SkipWhile(line => !line.Equals("[Installer]", StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .TakeWhile(line => !line.StartsWith("[", StringComparison.Ordinal))
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2
                && parts[0].Trim().Equals("Language", StringComparison.OrdinalIgnoreCase))
            .Select(parts => parts[1].Trim())
            .FirstOrDefault();

    private static void RecoverStaleHandoffFile(string consumingPath, string filePath)
    {
        try
        {
            if (!File.Exists(consumingPath))
                return;

            if (File.Exists(filePath))
            {
                File.Delete(consumingPath);
                return;
            }

            File.Move(consumingPath, filePath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void RestoreHandoffFile(string consumingPath, string filePath)
        => RecoverStaleHandoffFile(consumingPath, filePath);

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

    public sealed class InstallerLanguageHandoff : IDisposable
    {
        private readonly string _filePath;
        private readonly string _consumingPath;
        private readonly Mutex _mutex;
        private bool _completed;

        internal InstallerLanguageHandoff(string? language, string filePath, string consumingPath, Mutex mutex)
        {
            Language = language;
            _filePath = filePath;
            _consumingPath = consumingPath;
            _mutex = mutex;
        }

        public string? Language { get; }

        public void Complete()
        {
            if (_completed)
                return;

            try
            {
                File.Delete(_consumingPath);
                _completed = true;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public void Dispose()
        {
            if (!_completed)
                RestoreHandoffFile(_consumingPath, _filePath);

            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
