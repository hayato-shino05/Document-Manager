using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public class RelatedDocumentRepository : IRelatedDocument
{
    public List<(StudyDocument Doc, int RelationId, string RelationType)> GetRelated(int docId)
        => DatabaseHelper.GetRelatedDocuments(docId);

    public void AddRelation(int docId1, int docId2, string relationType = "related")
        => DatabaseHelper.AddDocumentRelation(docId1, docId2, relationType);

    public void RemoveRelation(int relationId)
        => DatabaseHelper.RemoveDocumentRelation(relationId);
}
