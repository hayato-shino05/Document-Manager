namespace StudyDocumentManager.Core.Interfaces;

/// <summary>
/// Provides localized string resolution for UI and business layer.
/// </summary>
public interface ILocalizationService
{
    string this[string key] { get; }

    SupportedLanguage CurrentLanguage { get; }

    void SetLanguage(SupportedLanguage language);

    IReadOnlyList<SupportedLanguage> AvailableLanguages { get; }

    event EventHandler? LanguageChanged;
}
