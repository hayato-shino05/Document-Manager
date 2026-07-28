using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public class DatabaseBackupService : IBackupService
{
    private readonly IFileIntegrityRepository _fileIntegrityRepo;
    private readonly IFileDialogService _fileDialogService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _loc;

    public DatabaseBackupService(
        IFileIntegrityRepository fileIntegrityRepo,
        IFileDialogService fileDialogService,
        IDialogService dialogService,
        ILocalizationService localizationService)
    {
        _fileIntegrityRepo = fileIntegrityRepo;
        _fileDialogService = fileDialogService;
        _dialogService = dialogService;
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

        try
        {
            _fileIntegrityRepo.BackupDatabase(path);
            return (true, path, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
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

        var confirmed = await _dialogService.ShowConfirmAsync(
            _loc["Dashboard_ConfirmRestore"],
            _loc["Dashboard_RestoreWarning"],
            _loc["Btn_OverwriteRestore"],
            isDanger: true);

        if (!confirmed)
            return (false, null);

        try
        {
            File.Copy(path, _fileIntegrityRepo.DatabasePath, overwrite: true);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
