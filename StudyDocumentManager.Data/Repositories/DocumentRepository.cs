using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

/// <summary>
/// Implements all document-related repository interfaces using DatabaseHelper.
/// </summary>
public class DocumentRepository : IDocumentRepository, IRecycleBinRepository, IBulkOperationRepository, IFileIntegrityRepository
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

    public bool Add(StudyDocument document) => _db.InsertDocument(document);

    public bool Update(StudyDocument document) => _db.UpdateDocument(document);

    public bool Delete(int id) => _db.DeleteDocument(id);

    public List<string> GetDistinctSubjects() => _db.GetDistinctSubjects();

    public List<string> GetDistinctTypes() => _db.GetDistinctTypes();

    public List<string> GetDistinctTags() => _db.GetDistinctTags();

    public List<StudyDocument> GetUpcomingDeadlines(int days) => _db.GetUpcomingDeadlines(days);

    public List<StudyDocument> GetOverdueDocuments() => _db.GetOverdueDocuments();

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

    public bool PermanentDeleteDocument(int id) => _db.PermanentDeleteDocument(id);

    public int EmptyRecycleBin() => _db.EmptyRecycleBin();

    public int GetDeletedDocumentCount() => _db.GetDeletedDocumentCount();

    // ——— Bulk operations ——————————————————————————————————————
    public int BulkSoftDelete(List<int> ids) => _db.BulkSoftDelete(ids);

    public int BulkUpdateSubject(List<int> ids, string subject) => _db.BulkUpdateSubject(ids, subject);

    public int BulkToggleImportant(List<int> ids, bool important) => _db.BulkToggleImportant(ids, important);

    // ——— File integrity ———————————————————————————————————————
    public bool UpdateDocumentPath(int id, string newPath) => _db.UpdateDocumentPath(id, newPath);

    public bool ClearDocumentPath(int id) => _db.ClearDocumentPath(id);

    public bool BackupDatabase(string destPath) => _db.BackupDatabase(destPath);

    public string DatabasePath => _db.DatabasePath;
}
