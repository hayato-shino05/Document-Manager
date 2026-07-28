using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

/// <summary>
/// Repository interface for document CRUD and query operations.
/// </summary>
public interface IDocumentRepository
{
    List<StudyDocument> GetAll();
    StudyDocument? GetById(int id);
    List<StudyDocument> Search(string keyword);
    List<StudyDocument> Filter(string subject, string type);
    List<StudyDocument> SearchAdvanced(
        string keyword, string subject, string type,
        DateTime? fromDate, DateTime? toDate,
        double? minSize, double? maxSize, bool? isImportant);
    bool Add(StudyDocument document);
    bool Update(StudyDocument document);
    bool Delete(int id);

    List<string> GetDistinctSubjects();
    List<string> GetDistinctTypes();
    List<string> GetDistinctTags();
    List<StudyDocument> GetUpcomingDeadlines(int days);
    List<StudyDocument> GetOverdueDocuments();

    void EnsureSubjectExists(string subject);
    void EnsureTypeExists(string type);
}
