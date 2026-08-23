using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Services;

public interface IUndoService
{
    event Action? StackChanged;

    void Push(UndoEntry entry);

    UndoEntry? Peek();

    bool CanUndo { get; }

    UndoEntry? Pop();

    void Clear();
}

public sealed class UndoEntry
{
    public string DescriptionKey { get; set; } = string.Empty;
    public object[]? DescriptionArgs { get; set; }
    public IReadOnlyList<StudyDocument> Originals { get; init; } = Array.Empty<StudyDocument>();
    public IReadOnlyList<int> DeletedIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<CollectionMembership> AddedCollectionMemberships { get; init; } = Array.Empty<CollectionMembership>();
    public CollectionSnapshot? Collection { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record CollectionMembership(int CollectionId, int DocumentId);

public sealed record CollectionSnapshot(string Name, string? Description, IReadOnlyList<int> MemberDocumentIds);
