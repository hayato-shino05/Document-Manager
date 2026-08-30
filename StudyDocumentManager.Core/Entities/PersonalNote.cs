namespace StudyDocumentManager.Core.Entities;

public sealed record PersonalNote(
    int Id,
    int DocumentId,
    string NoteType,
    string Content,
    bool IsPinned)
{
    public bool IsDeleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
