namespace StudyDocumentManager.Core.Entities;

public sealed record BulkItemResult(int DocumentId, bool Success);

public sealed class BulkEditOutcome
{
    public int Requested { get; init; }
    public int Succeeded { get; init; }
    public IReadOnlyList<BulkItemResult> Items { get; init; } = Array.Empty<BulkItemResult>();

    public IReadOnlyList<int> FailedIds => Items.Where(i => !i.Success).Select(i => i.DocumentId).ToList();
}
