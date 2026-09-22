using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Tests.TestDoubles;

/// <summary>
/// Minimal in-memory ISettingsService for isolated tests. Never touches a database.
/// </summary>
public sealed class InMemorySettingsService : ISettingsService
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public string? GetSetting(string key)
        => _values.TryGetValue(key, out var value) ? value : null;

    public void SetSetting(string key, string value) => _values[key] = value;
}
