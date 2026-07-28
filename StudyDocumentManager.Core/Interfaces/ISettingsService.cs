namespace StudyDocumentManager.Core.Interfaces;

public interface ISettingsService
{
    string? GetSetting(string key);
    void SetSetting(string key, string value);
}
