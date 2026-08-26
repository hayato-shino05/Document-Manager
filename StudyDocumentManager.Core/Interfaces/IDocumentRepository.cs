using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

/// <summary>
/// Repository interface for document CRUD and query operations.
/// </summary>
public interface IDocumentRepository
{
    List<StudyDocument> GetAll();
    StudyDocument? GetById(int id);
    StudyDocument? GetByFilePath(string filePath) => null;
    IReadOnlyList<StudyDocument> FindActiveByName(string name) => [];
    List<StudyDocument> Search(string keyword);
    List<StudyDocument> Filter(string subject, string type);
    List<StudyDocument> SearchAdvanced(
        string keyword, string subject, string type,
        DateTime? fromDate, DateTime? toDate,
        double? minSize, double? maxSize, bool? isImportant);

    List<StudyDocument> SearchAdvancedWithStatus(
        string? keyword, string? subject, string? type,
        DateTime? fromDate, DateTime? toDate,
        double? minSize, double? maxSize, bool? isImportant, string? status)
        => throw new NotSupportedException($"{nameof(SearchAdvancedWithStatus)} is not implemented by this repository.");

    Dictionary<string, int> GetStatusCounts()
        => throw new NotSupportedException($"{nameof(GetStatusCounts)} is not implemented by this repository.");

    bool Add(StudyDocument document);

    bool MergeDocuments(int survivorId, IReadOnlyList<int> duplicateIds)
        => throw new NotSupportedException($"{nameof(MergeDocuments)} is not implemented by this repository.");

    bool AddWithCatalogs(StudyDocument document);
    bool Update(StudyDocument document);
    bool Delete(int id);

    List<string> GetDistinctSubjects();
    List<string> GetDistinctTypes();
    List<string> GetDistinctTags();
    List<StudyDocument> GetUpcomingDeadlines(int days);
    List<StudyDocument> GetOverdueDocuments();

    List<StudyDocument> GetUncategorizedDocuments()
        => throw new NotSupportedException($"{nameof(GetUncategorizedDocuments)} is not implemented by this repository.");

    List<StudyDocument> GetDocumentsWithMissingMetadata()
        => throw new NotSupportedException($"{nameof(GetDocumentsWithMissingMetadata)} is not implemented by this repository.");

    void EnsureSubjectExists(string subject);
    void EnsureTypeExists(string type);
}
