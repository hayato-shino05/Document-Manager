using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public class CollectionRepository : ICollectionRepository
{
    private readonly DatabaseHelper _db;

    public CollectionRepository(DatabaseHelper db) => _db = db;

    public List<(int Id, string Name, string? Description, DateTime CreatedAt, int ItemCount)> GetAll()
        => _db.GetCollections();

    public int Create(string name, string? description = null)
        => _db.CreateCollection(name, description);

    public bool Update(int id, string name, string? description = null)
        => _db.UpdateCollection(id, name, description);

    public bool Delete(int id)
        => _db.DeleteCollection(id);

    public List<StudyDocument> GetDocuments(int collectionId)
        => _db.GetDocumentsInCollection(collectionId);

    public bool AddDocument(int collectionId, int documentId)
        => _db.AddDocumentToCollection(collectionId, documentId);

    public bool RemoveDocument(int collectionId, int documentId)
        => _db.RemoveDocumentFromCollection(collectionId, documentId);
}
