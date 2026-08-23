namespace StudyDocumentManager.Services;

public sealed record UndoApplyResult(bool Success, int AffectedCount, string DescriptionKey);

public interface IUndoApplier
{
    bool CanUndo { get; }

    void ApplyLast();
}

public sealed class UndoPartialRestoreException(int restoredCount, int requestedCount) : InvalidOperationException($"Undo restored {restoredCount} of {requestedCount} documents; the rest were permanently deleted.")
{
    public int RestoredCount { get; } = restoredCount;
    public int RequestedCount { get; } = requestedCount;
}
