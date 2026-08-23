using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

/// <summary>
/// Implements all document-related repository interfaces using DatabaseHelper.
/// </summary>
public class DocumentRepository : IDocumentRepository, IRecycleBinRepository, IBulkOperationRepository, IFileIntegrityRepository, IUndoRepository
{
    private readonly DatabaseHelper _db;

    public DocumentRepository(DatabaseHelper db) => _db = db;

    public List<StudyDocument> GetAll() => _db.GetAllDocuments();

    public StudyDocument? GetById(int id) => _db.GetDocumentById(id);

    public List<StudyDocument> Search(string keyword) => _db.SearchDocuments(keyword);

    public List<StudyDocument> Filter(string subject, string type) => _db.FilterDocuments(subject, type);

    public List<StudyDocument> SearchAdvanced(
        string keyword, string subject, string type,
        DateTime? fromDate, DateTime? toDate,
        double? minSize, double? maxSize, bool? isImportant)
        => _db.SearchDocumentsAdvanced(keyword, subject, type, fromDate, toDate, minSize, maxSize, isImportant);

    public List<StudyDocument> SearchAdvancedWithStatus(
        string? keyword, string? subject, string? type,
        DateTime? fromDate, DateTime? toDate,
        double? minSize, double? maxSize, bool? isImportant, string? status)
        => _db.SearchDocumentsAdvanced(keyword, subject, type, fromDate, toDate, minSize, maxSize, isImportant, status);

    public Dictionary<string, int> GetStatusCounts() => _db.GetStatusCounts();

    public bool Add(StudyDocument document) => _db.InsertDocument(document);


    public bool AddWithCatalogs(StudyDocument document) => _db.InsertDocumentWithCatalogs(document);

    public bool Update(StudyDocument document) => _db.UpdateDocument(document);

    public void ApplyMetadataUndo(
        IReadOnlyList<StudyDocument> originals,
        IReadOnlyList<(int CollectionId, int DocumentId)> addedCollectionMemberships)
        => _db.ApplyMetadataUndo(originals, addedCollectionMemberships);

    public bool Delete(int id) => _db.DeleteDocument(id);

    public List<string> GetDistinctSubjects() => _db.GetDistinctSubjects();

    public List<string> GetDistinctTypes() => _db.GetDistinctTypes();

    public List<string> GetDistinctTags() => _db.GetDistinctTags();

    public List<StudyDocument> GetUpcomingDeadlines(int days) => _db.GetUpcomingDeadlines(days);

    public List<StudyDocument> GetOverdueDocuments() => _db.GetOverdueDocuments();

    public List<StudyDocument> GetUncategorizedDocuments() => _db.GetUncategorizedDocuments();

    public List<StudyDocument> GetDocumentsWithMissingMetadata() => _db.GetDocumentsWithMissingMetadata();

    public void EnsureSubjectExists(string subject)
    {
        if (!string.IsNullOrWhiteSpace(subject))
            _db.AddSubject(subject);
    }

    public void EnsureTypeExists(string type)
    {
        if (!string.IsNullOrWhiteSpace(type))
            _db.AddType(type);
    }

    // ——— RecycleBin ———————————————————————————————————————————
    public List<StudyDocument> GetDeletedDocuments() => _db.GetDeletedDocuments();

    public bool RestoreDocument(int id) => _db.RestoreDocument(id);

    public int RestoreDocuments(IReadOnlyList<int> ids) => _db.RestoreDocuments(ids);

    public bool PermanentDeleteDocument(int id) => _db.PermanentDeleteDocument(id);

    public int EmptyRecycleBin() => _db.EmptyRecycleBin();

    public int GetDeletedDocumentCount() => _db.GetDeletedDocumentCount();

    // ——— Bulk operations ——————————————————————————————————————
    public int BulkSoftDelete(List<int> ids) => _db.BulkSoftDelete(ids);

    public int BulkUpdateSubject(List<int> ids, string subject) => _db.BulkUpdateSubject(ids, subject);

    public int BulkToggleImportant(List<int> ids, bool important) => _db.BulkToggleImportant(ids, important);

    public int BulkUpdateStatus(List<int> ids, string status) => _db.BulkUpdateStatus(ids, status);

    public BulkEditOutcome BulkEditMetadata(IReadOnlyList<int> documentIds, BulkEditChanges changes)
        => _db.BulkEditMetadata(documentIds, changes);

    // ——— File integrity ———————————————————————————————————————
    public bool UpdateDocumentPath(int id, string newPath) => _db.UpdateDocumentPath(id, newPath);

    public bool ClearDocumentPath(int id) => _db.ClearDocumentPath(id);

    public bool BackupDatabase(string destPath, bool overwrite) => _db.BackupDatabase(destPath, overwrite);

    public bool CanRestoreDatabase(string sourcePath) => _db.CanRestoreDatabase(sourcePath);

    public bool RestoreDatabase(string sourcePath) => _db.RestoreDatabase(sourcePath);

    public string DatabasePath => _db.DatabasePath;
}
