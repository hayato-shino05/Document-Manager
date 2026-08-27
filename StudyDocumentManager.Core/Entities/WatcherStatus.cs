namespace StudyDocumentManager.Core.Entities;

/// <summary>
/// Transient runtime state of a watched folder, set by the model while it is
/// managing watchers. Not persisted to the database.
/// </summary>
public enum WatcherStatus
{
    Unknown,
    Running,
    Disabled,
    Error,
    Stopped
}
