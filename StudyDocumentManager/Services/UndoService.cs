namespace StudyDocumentManager.Services;

public sealed class UndoService : IUndoService
{
    private const int MaxEntries = 10;

    private readonly object _gate = new();
    private readonly List<UndoEntry> _entries = [];

    public event Action? StackChanged;

    public bool CanUndo
    {
        get { lock (_gate) return _entries.Count > 0; }
    }

    public void Push(UndoEntry entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
            if (_entries.Count > MaxEntries)
                _entries.RemoveAt(0);
        }
        StackChanged?.Invoke();
    }

    public UndoEntry? Peek()
    {
        lock (_gate)
            return _entries.Count > 0 ? _entries[^1] : null;
    }

    public UndoEntry? Pop()
    {
        UndoEntry? entry;
        lock (_gate)
        {
            if (_entries.Count == 0)
                return null;

            entry = _entries[^1];
            _entries.RemoveAt(_entries.Count - 1);
        }
        StackChanged?.Invoke();
        return entry;
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (_entries.Count == 0)
                return;
            _entries.Clear();
        }
        StackChanged?.Invoke();
    }
}
