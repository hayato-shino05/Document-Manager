using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public sealed class ImportInboxRepository(DatabaseHelper db) : IImportInboxRepository
{
    public IReadOnlyList<ImportInboxItem> GetAll(bool includeProcessed = false) => db.GetImportInboxItems(includeProcessed);
    public ImportInboxItem? GetById(int id) => db.GetImportInboxItem(id);
    public int Add(ImportInboxItem item)
    {
        var existingId = db.FindImportInboxIdBySourcePath(item.SourcePath);
        if (existingId is int id)
        {
            // Preserve the existing row (state, metadata, failure, duplicate
            // candidate, DocumentId). A rescan must never reset a processed,
            // held, or failed entry back to Pending. New Pending rows are only
            // created for genuinely new sources, or via an explicit retry.
            item.Id = id;
            return id;
        }

        return db.InsertImportInboxItem(item);
    }
    public bool Update(ImportInboxItem item) => db.UpdateImportInboxItem(item);
    public bool UpdateState(int id, ImportInboxState state, string? failureCode = null) => db.UpdateImportInboxState(id, state, failureCode);
    public int BulkEditMetadata(IReadOnlyList<int> documentIds, BulkEditChanges changes)
        => db.BulkEditMetadata(documentIds.ToList(), changes).Succeeded;
}
