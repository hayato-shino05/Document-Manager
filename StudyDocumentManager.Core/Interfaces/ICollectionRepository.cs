using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

public interface ICollectionRepository
{
    List<(int Id, string Name, string? Description, DateTime CreatedAt, int ItemCount)> GetAll();
    int Create(string name, string? description = null);
    bool Update(int id, string name, string? description = null);
    bool Delete(int id);
    List<StudyDocument> GetDocuments(int collectionId);
    bool AddDocument(int collectionId, int documentId);
    bool RemoveDocument(int collectionId, int documentId);
}
