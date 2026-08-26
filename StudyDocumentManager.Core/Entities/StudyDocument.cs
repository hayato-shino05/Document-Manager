namespace StudyDocumentManager.Core.Entities;

/// <summary>
/// Main entity representing a study document.
/// Maps to the 'documents' table in SQLite.
/// </summary>
public class StudyDocument
{
    public static string NormalizeName(string? name)
        => (name ?? string.Empty).Trim().ToLowerInvariant();
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public double? FileSize { get; set; }
    public string Author { get; set; } = string.Empty;
    public bool IsImportant { get; set; }
    public string Tags { get; set; } = string.Empty;
    public DateTime? Deadline { get; set; }
    public string Status { get; set; } = DocumentStatus.Unread;

    public StudyDocument()
    {
        CreatedAt = DateTime.Now;
        IsImportant = false;
    }
}
