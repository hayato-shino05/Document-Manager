namespace StudyDocumentManager.Core.Entities;

public sealed class OperationProgress
{
    public bool IsRunning { get; private set; }
    public bool IsCancelled { get; private set; }
    public int Processed { get; private set; }
    public int Total { get; private set; }
    public int Succeeded { get; private set; }
    public int Skipped { get; private set; }
    public int Failed { get; private set; }
    public IReadOnlyList<string> FailedItems => _failedItems;

    private readonly List<string> _failedItems = [];

    public void Start(int total)
    {
        IsRunning = true;
        IsCancelled = false;
        Processed = 0;
        Total = total;
        Succeeded = 0;
        Skipped = 0;
        Failed = 0;
        _failedItems.Clear();
    }

    public void RecordSuccess(string item)
    {
        Processed++;
        Succeeded++;
    }

    public void RecordSkipped(string item)
    {
        Processed++;
        Skipped++;
    }

    public void RecordFailure(string item)
    {
        Processed++;
        Failed++;
        if (!_failedItems.Contains(item, StringComparer.OrdinalIgnoreCase))
            _failedItems.Add(item);
    }

    public void Cancel() => IsCancelled = true;

    public void Stop() => IsRunning = false;
}
