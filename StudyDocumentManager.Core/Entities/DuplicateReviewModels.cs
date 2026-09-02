namespace StudyDocumentManager.Core.Entities;

/// <summary>
/// 重複候補の判定理由を表す列挙型。
/// </summary>
public enum DuplicateMatchReason
{
    /// <summary>文書名が完全一致（大文字小文字を区別しない）</summary>
    ExactName,
    /// <summary>ファイルパスが完全一致</summary>
    ExactPath,
    /// <summary>文書名および文書タイプが一致</summary>
    SameNameAndType,
    /// <summary>文書名およびファイルサイズが一致</summary>
    SameNameAndSize
}

/// <summary>
/// 重複候補グループの契約モデル。
/// </summary>
public sealed record DuplicateReviewGroup(
    string GroupKey,
    DuplicateMatchReason Reason,
    string MatchDescription,
    IReadOnlyList<StudyDocument> Candidates);

/// <summary>
/// 重複統合（マージ）実行前の影響範囲プレビュー契約モデル。
/// </summary>
public sealed record DuplicateMergePreview(
    StudyDocument Survivor,
    IReadOnlyList<StudyDocument> DocumentsToMerge,
    int TransferredNotesCount,
    IReadOnlyList<string> MergedTags,
    int TransferredCollectionsCount,
    int TransferredRelationsCount,
    bool WillSoftDeleteDuplicates = true);
