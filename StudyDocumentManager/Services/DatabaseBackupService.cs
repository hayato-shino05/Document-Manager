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

    public async Task<(bool Success, string? Path, string? Error)> BackupAsync()
    {
        var path = await _fileDialogService.ShowSaveFileAsync(
            _loc["Dashboard_BackupTitle"],
            "backup_study_docs.db",
            _loc["Dashboard_BackupFileFilter"]);

        if (string.IsNullOrWhiteSpace(path))
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

        return _fileIntegrityRepo.BackupDatabase(path, overwrite: true)
            ? (true, path, null)
            : (false, null, _loc["Dashboard_BackupFailed"]);
    }

    public async Task<(bool Success, string? Error)> RestoreAsync()
    {
        var path = await _fileDialogService.ShowOpenFileAsync(
            _loc["Dashboard_SelectBackup"],
            _loc["Dashboard_BackupOpenFilter"]);

        if (string.IsNullOrWhiteSpace(path))
            return (false, null);

        if (!File.Exists(path))
            return (false, _loc["Dashboard_BackupNotExist"]);

        if (!_fileIntegrityRepo.CanRestoreDatabase(path))
            return (false, _loc["Dashboard_RestoreFailed"]);

        var confirmed = await _dialogService.ShowConfirmAsync(
            _loc["Dashboard_ConfirmRestore"],
            _loc["Dashboard_RestoreWarning"],
            _loc["Btn_OverwriteRestore"],
            isDanger: true);

        if (!confirmed)
            return (false, null);

        if (!_fileIntegrityRepo.RestoreDatabase(path))
            return (false, _loc["Dashboard_RestoreFailed"]);

        await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], _loc["Dashboard_RestoreRestartRequired"]);
        _lifecycleService.Shutdown();
        return (true, null);
    }
}
