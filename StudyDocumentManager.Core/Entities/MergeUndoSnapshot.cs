namespace StudyDocumentManager.Core.Entities;

public sealed record MergeUndoSnapshot(
    StudyDocument Survivor,
    IReadOnlyList<int> DuplicateIds,
    IReadOnlyList<PersonalNote> Notes,
    IReadOnlyList<CollectionMembershipSnapshot> CollectionMemberships,
    IReadOnlyList<DocumentRelationSnapshot> Relations);

public sealed record CollectionMembershipSnapshot(int CollectionId, int DocumentId);

public sealed record DocumentRelationSnapshot(int DocumentId1, int DocumentId2, string RelationType);
