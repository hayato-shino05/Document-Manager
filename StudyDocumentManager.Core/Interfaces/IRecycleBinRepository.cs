using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

public interface IRecycleBinRepository
{
    List<StudyDocument> GetDeletedDocuments();
    bool RestoreDocument(int id);
    bool PermanentDeleteDocument(int id);
    int EmptyRecycleBin();
    int GetDeletedDocumentCount();

    int RestoreDocuments(IReadOnlyList<int> ids)
        => throw new NotSupportedException($"{nameof(RestoreDocuments)} is not implemented by this repository.");

    int PermanentlyDeleteDocuments(IReadOnlyList<int> ids)
        => throw new NotSupportedException($"{nameof(PermanentlyDeleteDocuments)} is not implemented by this repository.");
}
