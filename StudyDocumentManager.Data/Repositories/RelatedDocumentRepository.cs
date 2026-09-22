using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public class RelatedDocumentRepository : IRelatedDocumentRepository
{
    private readonly DatabaseHelper _db;

    public RelatedDocumentRepository(DatabaseHelper db) => _db = db;

    public List<(StudyDocument Doc, int RelationId, string RelationType)> GetRelated(int docId)
        => _db.GetRelatedDocuments(docId);

    public void AddRelation(int docId1, int docId2, string relationType = "related")
        => _db.AddDocumentRelation(docId1, docId2, relationType);

    public void RemoveRelation(int relationId)
        => _db.RemoveDocumentRelation(relationId);
}
