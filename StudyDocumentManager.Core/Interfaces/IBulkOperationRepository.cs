namespace StudyDocumentManager.Core.Interfaces;

public interface IBulkOperationRepository
{
    int BulkSoftDelete(List<int> ids);
    int BulkUpdateSubject(List<int> ids, string subject);
    int BulkToggleImportant(List<int> ids, bool important);
}
