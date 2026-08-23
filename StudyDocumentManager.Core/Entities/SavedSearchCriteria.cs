using System.Text.Json;

namespace StudyDocumentManager.Core.Entities;

public sealed class SavedSearchCriteria
{
    public string Kind { get; set; } = SavedSearchKinds.Standard;
    public string? Keyword { get; set; }
    public string? Subject { get; set; }
    public string? Type { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public double? MinSize { get; set; }
    public double? MaxSize { get; set; }
    public bool? IsImportant { get; set; }
    public int RecentDays { get; set; } = 7;
    public int DeadlineDays { get; set; } = 7;

    public string ToJson() => JsonSerializer.Serialize(this);

    public static SavedSearchCriteria? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SavedSearchCriteria>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public static class SavedSearchKinds
{
    public const string Standard = "standard";
    public const string Uncategorized = "uncategorized";
    public const string MissingMetadata = "missing-metadata";
    public const string MissingFile = "missing-file";
    public const string RecentlyAdded = "recently-added";
    public const string Important = "important";
    public const string DueSoon = "due-soon";
}
