namespace StudyDocumentManager.Core.Entities;

public sealed class BulkEditChanges
{
    public string? Subject { get; set; }
    public string? Type { get; set; }
    public string? Tags { get; set; }
    public bool? IsImportant { get; set; }
    public DateTime? Deadline { get; set; }
    public string? Status { get; set; }

    // membership add, not a document column
    public int? AddToCollectionId { get; set; }

    public bool HasAnyChange =>
        Subject != null || Type != null || Tags != null || IsImportant != null ||
        Deadline != null || Status != null || AddToCollectionId != null;
}
