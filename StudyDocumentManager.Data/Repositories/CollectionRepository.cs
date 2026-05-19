using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public class CollectionRepository : ICollection
{
    public List<(int Id, string Name, string? Description, DateTime CreatedAt, int ItemCount)> GetAll()
        => DatabaseHelper.GetCollections();

    public int Create(string name, string? description = null)
        => DatabaseHelper.CreateCollection(name, description);

    public bool Update(int id, string name, string? description = null)
        => DatabaseHelper.UpdateCollection(id, name, description);

    public bool Delete(int id)
        => DatabaseHelper.DeleteCollection(id);

    public List<StudyDocument> GetDocuments(int collectionId)
        => DatabaseHelper.GetDocumentsInCollection(collectionId);

    public bool AddDocument(int collectionId, int documentId)
        => DatabaseHelper.AddDocumentToCollection(collectionId, documentId);

    public bool RemoveDocument(int collectionId, int documentId)
        => DatabaseHelper.RemoveDocumentFromCollection(collectionId, documentId);
}
