using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

public interface IBulkOperationRepository
{
    int BulkSoftDelete(List<int> ids);
    int BulkUpdateSubject(List<int> ids, string subject);
    int BulkToggleImportant(List<int> ids, bool important);

    int BulkUpdateStatus(List<int> ids, string status)
        => throw new NotSupportedException($"{nameof(BulkUpdateStatus)} is not implemented by this repository.");

    BulkEditOutcome BulkEditMetadata(IReadOnlyList<int> documentIds, BulkEditChanges changes)
        => throw new NotSupportedException($"{nameof(BulkEditMetadata)} is not implemented by this repository.");
}
