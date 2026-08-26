namespace StudyDocumentManager.Core.Entities;

public enum ImportInboxState
{
    Pending,
    Held,
    MissingMetadata,
    Ambiguous,
    Failed,
    Processed
}

public sealed class ImportInboxItem
{
    public int Id { get; set; }
    public int? DocumentId { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? FailureCode { get; set; }
    public string? DuplicateCandidate { get; set; }
    public string? Subject { get; set; }
    public string? Type { get; set; }
    public ImportInboxState State { get; set; } = ImportInboxState.Pending;
    public string StateLabel { get; set; } = string.Empty;
    public string FailureLabel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
