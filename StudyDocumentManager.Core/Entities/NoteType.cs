namespace StudyDocumentManager.Core.Entities;

public sealed record NoteType
{
    private NoteType(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static IReadOnlyList<string> All { get; } =
    [
        "general",
        "summary",
        "action",
        "quote",
        "lecture",
        "meeting"
    ];

    public static bool TryParse(string? value, out NoteType noteType)
    {
        var normalizedValue = value?.Trim().ToLowerInvariant();
        if (normalizedValue is not null && All.Contains(normalizedValue, StringComparer.Ordinal))
        {
            noteType = new NoteType(normalizedValue);
            return true;
        }

        noteType = null!;
        return false;
    }
}
