using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public class DatabaseBackupService : IBackupService
{
    private readonly IFileIntegrityRepository _fileIntegrityRepo;
    private readonly IFileDialogService _fileDialogService;
    private readonly IDialogService _dialogService;
    private readonly IApplicationLifecycleService _lifecycleService;
    private readonly ILocalizationService _loc;

    public DatabaseBackupService(
        IFileIntegrityRepository fileIntegrityRepo,
        IFileDialogService fileDialogService,
        IDialogService dialogService,
        IApplicationLifecycleService lifecycleService,
        ILocalizationService localizationService)
    {
        _fileIntegrityRepo = fileIntegrityRepo;
        _fileDialogService = fileDialogService;
        _dialogService = dialogService;
        _lifecycleService = lifecycleService;
        _loc = localizationService;
    }

    public Task<(bool Success, string? Path, string? Error)> BackupAsync()
        => BackupAsync(CancellationToken.None);

    public async Task<(bool Success, string? Path, string? Error)> BackupAsync(CancellationToken cancellationToken)
    {
        var path = await _fileDialogService.ShowSaveFileAsync(
            _loc["Dashboard_BackupTitle"],
            "backup_study_docs.db",
            _loc["Dashboard_BackupFileFilter"]);

        if (string.IsNullOrWhiteSpace(path))
            return (false, null, null);

        if (cancellationToken.IsCancellationRequested)
            return (false, null, null);

        if (File.Exists(path))
        {
            var confirmed = await _dialogService.ShowConfirmAsync(
                _loc["Dashboard_ConfirmOverwriteBackup"],
                _loc["Dashboard_OverwriteBackupWarning"],
                _loc["Dashboard_OverwriteBackup"],
                isDanger: true);
            if (!confirmed)
                return (false, null, null);
        }

        if (cancellationToken.IsCancellationRequested)
            return (false, null, null);

        try
        {
            var succeeded = await Task.Run(
                () => _fileIntegrityRepo.BackupDatabase(path, overwrite: true),
                cancellationToken);
            return succeeded
                ? (true, path, null)
                : (false, null, _loc["Dashboard_BackupFailed"]);
        }
        catch (Exception)
        {
            return (false, null, _loc["Dashboard_BackupFailed"]);
        }
    }

    public Task<(bool Success, string? Error)> RestoreAsync()
        => RestoreAsync(CancellationToken.None);

    public async Task<(bool Success, string? Error)> RestoreAsync(CancellationToken cancellationToken)
    {
        var path = await _fileDialogService.ShowOpenFileAsync(
            _loc["Dashboard_SelectBackup"],
            _loc["Dashboard_BackupOpenFilter"]);

        if (string.IsNullOrWhiteSpace(path))
            return (false, null);

        if (cancellationToken.IsCancellationRequested)
            return (false, null);

        if (!File.Exists(path))
            return (false, _loc["Dashboard_BackupNotExist"]);

        bool canRestore;
        try
        {
            canRestore = await Task.Run(
                () => _fileIntegrityRepo.CanRestoreDatabase(path),
                cancellationToken);
        }
        catch (Exception)
        {
            return (false, _loc["Dashboard_RestoreFailed"]);
        }

        if (!canRestore)
            return (false, _loc["Dashboard_RestoreFailed"]);

        if (cancellationToken.IsCancellationRequested)
            return (false, null);

        var confirmed = await _dialogService.ShowConfirmAsync(
            _loc["Dashboard_ConfirmRestore"],
            _loc["Dashboard_RestoreWarning"],
            _loc["Btn_OverwriteRestore"],
            isDanger: true);

        if (!confirmed)
            return (false, null);

        if (cancellationToken.IsCancellationRequested)
            return (false, null);

        bool restored;
        try
        {
            restored = await Task.Run(
                () => _fileIntegrityRepo.RestoreDatabase(path),
                cancellationToken);
        }
        catch (Exception)
        {
            return (false, _loc["Dashboard_RestoreFailed"]);
        }

        if (!restored)
            return (false, _loc["Dashboard_RestoreFailed"]);

        await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], _loc["Dashboard_RestoreRestartRequired"]);
        _lifecycleService.Shutdown();
        return (true, null);
    }
}
