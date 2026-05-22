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

    private int _indexerCallCount;

    public string this[string key]
    {
        get
        {
            _indexerCallCount++;
            var value = _resourceManager.GetString(key, _culture);
            // 言語切替後のbinding再評価を確認（最初の大量呼び出しはスキップ）
            if (_indexerCallCount > 50)
                System.Diagnostics.Debug.WriteLine($"[LANG-DEBUG] Indexer['{key}'] → '{value}' (culture={_culture.Name})");
            return value ?? $"[{key}]";
        }
    }

    public SupportedLanguage CurrentLanguage { get; private set; }

    public void SetLanguage(SupportedLanguage language)
    {
        System.Diagnostics.Debug.WriteLine($"[LANG-DEBUG] SetLanguage called: requested={language}, current={CurrentLanguage}");
        if (CurrentLanguage == language)
        {
            System.Diagnostics.Debug.WriteLine("[LANG-DEBUG] SetLanguage SKIPPED (same language)");
            return;
        }

        CurrentLanguage = language;
        var cultureCode = CultureMap[language];
        _culture = string.IsNullOrEmpty(cultureCode)
            ? CultureInfo.InvariantCulture
            : new CultureInfo(cultureCode);

        System.Diagnostics.Debug.WriteLine($"[LANG-DEBUG] Culture set to '{_culture.Name}', firing events...");
        LanguageChanged?.Invoke(this, EventArgs.Empty);
        _indexerCallCount = 0;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        System.Diagnostics.Debug.WriteLine($"[LANG-DEBUG] PropertyChanged fired, indexer re-reads={_indexerCallCount}, subscribers={PropertyChanged?.GetInvocationList().Length ?? 0}");
    }

    public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } =
        Enum.GetValues<SupportedLanguage>().ToList().AsReadOnly();

    public event EventHandler? LanguageChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
}
