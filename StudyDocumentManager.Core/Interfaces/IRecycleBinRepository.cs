using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

public interface IRecycleBinRepository
{
    List<StudyDocument> GetDeletedDocuments();
    bool RestoreDocument(int id);
    bool PermanentDeleteDocument(int id);
    int EmptyRecycleBin();
    int GetDeletedDocumentCount();
}
