using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

/// <summary>
/// IDocumentRepository implementation using DatabaseHelper.
/// </summary>
public class DocumentRepository : IDocumentRepository
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
}
