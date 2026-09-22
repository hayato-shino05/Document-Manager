using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

public interface IImportInboxRepository
{
    IReadOnlyList<ImportInboxItem> GetAll(bool includeProcessed = false);
    ImportInboxItem? GetById(int id);
    int Add(ImportInboxItem item);
    bool Update(ImportInboxItem item);
    bool UpdateState(int id, ImportInboxState state, string? failureCode = null);
    int BulkEditMetadata(IReadOnlyList<int> documentIds, BulkEditChanges changes);
}
