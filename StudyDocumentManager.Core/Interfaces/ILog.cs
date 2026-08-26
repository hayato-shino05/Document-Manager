namespace StudyDocumentManager.Core.Interfaces;

/// <summary>
/// Minimal structured logger for non-user-facing diagnostics. Never logs secrets
/// or file contents, only paths and error context.
/// </summary>
public interface ILog
{
    void Information(string message);
    void Warning(string message, Exception? exception = null);
    void Error(string message, Exception? exception = null);
}
