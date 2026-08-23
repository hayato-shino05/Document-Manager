namespace StudyDocumentManager.Services;

public sealed record UndoApplyResult(bool Success, int AffectedCount, string DescriptionKey);

public interface IUndoApplier
{
    bool CanUndo { get; }

    void ApplyLast();
}
