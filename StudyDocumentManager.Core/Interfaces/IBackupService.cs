namespace StudyDocumentManager.Core.Interfaces;

public interface IBackupService
{
    Task<(bool Success, string? Path, string? Error)> BackupAsync();
    Task<(bool Success, string? Error)> RestoreAsync();
}
