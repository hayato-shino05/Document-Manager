using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

public interface IRelatedDocumentRepository
{
    List<(StudyDocument Doc, int RelationId, string RelationType)> GetRelated(int docId);
    void AddRelation(int docId1, int docId2, string relationType = "related");
    void RemoveRelation(int relationId);
}
