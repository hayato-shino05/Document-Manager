using System.Globalization;
using System.Resources;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public class LocalizationService : ILocalizationService
{
    private readonly ResourceManager _resourceManager;
    private CultureInfo _culture;

    private static readonly Dictionary<SupportedLanguage, string> CultureMap = new()
    {
        { SupportedLanguage.Japanese, "" },
        { SupportedLanguage.English, "en" },
        { SupportedLanguage.Vietnamese, "vi" },
        { SupportedLanguage.Chinese, "zh" }
    };

    public LocalizationService()
    {
        _resourceManager = new ResourceManager(
            "StudyDocumentManager.Resources.Strings",
            typeof(LocalizationService).Assembly);

        _culture = CultureInfo.InvariantCulture;
        CurrentLanguage = SupportedLanguage.Japanese;
    }

    public string this[string key]
    {
        get
        {
            var value = _resourceManager.GetString(key, _culture);
            return value ?? $"[{key}]";
        }
    }

    public SupportedLanguage CurrentLanguage { get; private set; }

    public void SetLanguage(SupportedLanguage language)
    {
        if (CurrentLanguage == language) return;

        CurrentLanguage = language;
        var cultureCode = CultureMap[language];
        _culture = string.IsNullOrEmpty(cultureCode)
            ? CultureInfo.InvariantCulture
            : new CultureInfo(cultureCode);

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } =
        Enum.GetValues<SupportedLanguage>().ToList().AsReadOnly();

    public event EventHandler? LanguageChanged;
}
