using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

public interface IUndoRepository
{
    void ApplyMetadataUndo(
        IReadOnlyList<StudyDocument> originals,
        IReadOnlyList<(int CollectionId, int DocumentId)> addedCollectionMemberships);

    MergeUndoSnapshot CaptureMergeUndo(int survivorId, IReadOnlyList<int> duplicateIds);

    void ApplyMergeUndo(MergeUndoSnapshot snapshot);
}
