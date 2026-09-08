using System.ComponentModel;
using System.Globalization;
using System.Resources;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public class LocalizationService : ILocalizationService, INotifyPropertyChanged
{
    private readonly ResourceManager _resourceManager;
    private CultureInfo _culture;

    private static readonly Dictionary<SupportedLanguage, string> CultureMap = new()
    {
        { SupportedLanguage.Japanese, "ja-JP" },
        { SupportedLanguage.English, "en" },
        { SupportedLanguage.Vietnamese, "vi" },
        { SupportedLanguage.Chinese, "zh" }
    };

    public LocalizationService()
    {
        _resourceManager = new ResourceManager(
            "StudyDocumentManager.Resources.Strings",
            typeof(LocalizationService).Assembly);

        CurrentLanguage = SupportedLanguage.Japanese;
        _culture = new CultureInfo(CultureMap[CurrentLanguage]);
        ApplyCurrentCulture();
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
        if (CurrentLanguage == language)
            return;

        CurrentLanguage = language;
        _culture = new CultureInfo(CultureMap[language]);
        ApplyCurrentCulture();

        LanguageChanged?.Invoke(this, EventArgs.Empty);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    private void ApplyCurrentCulture()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _culture;
    }

    public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } =
        Enum.GetValues<SupportedLanguage>().ToList().AsReadOnly();

    public event EventHandler? LanguageChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
}
