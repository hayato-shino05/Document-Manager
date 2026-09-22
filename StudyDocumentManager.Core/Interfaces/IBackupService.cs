namespace StudyDocumentManager.Core.Interfaces;

public interface IBackupService
{
    Task<(bool Success, string? Path, string? Error)> BackupAsync();
    Task<(bool Success, string? Error)> RestoreAsync();

    Task<(bool Success, string? Path, string? Error)> BackupAsync(CancellationToken cancellationToken)
        => BackupAsync();

    Task<(bool Success, string? Error)> RestoreAsync(CancellationToken cancellationToken)
        => RestoreAsync();
}
