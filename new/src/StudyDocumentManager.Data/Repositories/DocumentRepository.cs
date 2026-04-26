using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

/// <summary>
/// IDocumentRepository implementation using DatabaseHelper.
/// </summary>
public class DocumentRepository : IDocument
{
    public List<StudyDocument> GetAll() => DatabaseHelper.GetAllDocuments();

    public StudyDocument? GetById(int id) => DatabaseHelper.GetDocumentById(id);

    public List<StudyDocument> Search(string keyword) => DatabaseHelper.SearchDocuments(keyword);

    public List<StudyDocument> Filter(string subject, string type) => DatabaseHelper.FilterDocuments(subject, type);

    public List<StudyDocument> SearchAdvanced(
        string keyword, string subject, string type,
        DateTime? fromDate, DateTime? toDate,
        double? minSize, double? maxSize, bool? isImportant)
        => DatabaseHelper.SearchDocumentsAdvanced(keyword, subject, type, fromDate, toDate, minSize, maxSize, isImportant);

    public bool Add(StudyDocument document) => DatabaseHelper.InsertDocument(document);

    public bool Update(StudyDocument document) => DatabaseHelper.UpdateDocument(document);

    public bool Delete(int id) => DatabaseHelper.DeleteDocument(id);

    public List<string> GetDistinctSubjects() => DatabaseHelper.GetDistinctSubjects();

    public List<string> GetDistinctTypes() => DatabaseHelper.GetDistinctTypes();

    public List<string> GetDistinctTags() => DatabaseHelper.GetDistinctTags();

    public List<StudyDocument> GetUpcomingDeadlines(int days) => DatabaseHelper.GetUpcomingDeadlines(days);

    public List<StudyDocument> GetOverdueDocuments() => DatabaseHelper.GetOverdueDocuments();

    public void EnsureSubjectExists(string subject)
    {
        if (!string.IsNullOrWhiteSpace(subject))
            DatabaseHelper.AddSubject(subject);
    }

    public void EnsureTypeExists(string type)
    {
        if (!string.IsNullOrWhiteSpace(type))
            DatabaseHelper.AddType(type);
    }

    // â”€â”€â”€ RecycleBin â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public List<StudyDocument> GetDeletedDocuments() => DatabaseHelper.GetDeletedDocuments();

    public bool RestoreDocument(int id) => DatabaseHelper.RestoreDocument(id);

    public bool PermanentDeleteDocument(int id) => DatabaseHelper.PermanentDeleteDocument(id);

    public int EmptyRecycleBin() => DatabaseHelper.EmptyRecycleBin();

    public int GetDeletedDocumentCount() => DatabaseHelper.GetDeletedDocumentCount();

    // â”€â”€â”€ Bulk operations â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public int BulkSoftDelete(List<int> ids) => DatabaseHelper.BulkSoftDelete(ids);

    public int BulkUpdateSubject(List<int> ids, string subject) => DatabaseHelper.BulkUpdateSubject(ids, subject);

    public int BulkToggleImportant(List<int> ids, bool important) => DatabaseHelper.BulkToggleImportant(ids, important);

    // â”€â”€â”€ Backup & Restore â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public bool BackupDatabase(string destPath) => DatabaseHelper.BackupDatabase(destPath);

    public string DatabasePath => DatabaseHelper.DatabasePath;

    // â”€â”€â”€ File integrity â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public bool UpdateDocumentPath(int id, string newPath) => DatabaseHelper.UpdateDocumentPath(id, newPath);

    public bool ClearDocumentPath(int id) => DatabaseHelper.ClearDocumentPath(id);
}
