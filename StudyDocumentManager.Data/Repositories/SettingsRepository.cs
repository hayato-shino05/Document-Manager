using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public class SettingsRepository : ISettingsService
{
    private readonly DatabaseHelper _db;

    public SettingsRepository(DatabaseHelper db) => _db = db;

    public string? GetSetting(string key) => _db.GetSetting(key);

    public void SetSetting(string key, string value) => _db.SetSetting(key, value);
}
