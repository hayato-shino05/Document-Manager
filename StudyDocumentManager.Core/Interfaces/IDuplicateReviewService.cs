namespace StudyDocumentManager.Core.Interfaces;

using StudyDocumentManager.Core.Entities;

/// <summary>
/// 重複文書の検出および統合プレビュー生成サービス契約。
/// </summary>
public interface IDuplicateReviewService
{
    /// <summary>
    /// 文書一覧から重複候補グループを検出する。
    /// </summary>
    IReadOnlyList<DuplicateReviewGroup> DetectDuplicates(IReadOnlyList<StudyDocument> documents);

    /// <summary>
    /// 代表文書と統合対象文書から影響範囲プレビューを生成する。
    /// </summary>
    DuplicateMergePreview BuildMergePreview(int survivorId, IReadOnlyList<int> duplicateIds);
}
