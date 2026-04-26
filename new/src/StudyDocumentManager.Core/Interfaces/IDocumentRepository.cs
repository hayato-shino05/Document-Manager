using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

/// <summary>
/// Repository interface for document CRUD and queries.
/// Mirrors the WinForms IDocumentRepository contract exactly.
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

    /// <summary>
    /// Đảm bảo danh mục tồn tại trong lookup table (nếu chưa có thì thêm).
    /// Gọi sau khi thêm tài liệu có MonHoc mới.
    /// </summary>
    void EnsureSubjectExists(string subject);

    /// <summary>
    /// Đảm bảo loại tài liệu tồn tại trong lookup table (nếu chưa có thì thêm).
    /// Gọi sau khi thêm tài liệu có Loai mới.
    /// </summary>
    void EnsureTypeExists(string type);
}

