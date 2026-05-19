using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

/// <summary>
/// Repository interface for document CRUD and queries.
/// Mirrors the WinForms IDocumentRepository contract exactly.
/// </summary>
public interface IDocument
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
    /// カテゴリがlookupテーブルに存在しない場合、自動追加する
    /// </summary>
    void EnsureSubjectExists(string subject);

    /// <summary>
    /// ドキュメントタイプがlookupテーブルに存在しない場合、自動追加する
    /// </summary>
    void EnsureTypeExists(string type);

    // ─── RecycleBin ───────────────────────────────────────────────────────
    List<StudyDocument> GetDeletedDocuments();
    bool RestoreDocument(int id);
    bool PermanentDeleteDocument(int id);
    int EmptyRecycleBin();
    int GetDeletedDocumentCount();

    // ─── Bulk operations ──────────────────────────────────────────────────
    int BulkSoftDelete(List<int> ids);
    int BulkUpdateSubject(List<int> ids, string subject);
    int BulkToggleImportant(List<int> ids, bool important);

    // ─── Backup & Restore ─────────────────────────────────────────────────
    bool BackupDatabase(string destPath);
    string DatabasePath { get; }

    // ─── File integrity ───────────────────────────────────────────────────
    bool UpdateDocumentPath(int id, string newPath);
    bool ClearDocumentPath(int id);
}
