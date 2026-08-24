using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class RecoveryCenterModel : ModelBase, IDisposable
{
    private readonly IVersionedBackupService _backupService;
    private readonly IDialogService _dialogService;
    private readonly IFileDialogService _fileDialogService;
    private readonly INavigationService _navigationService;
    private readonly IApplicationLifecycleService _lifecycleService;
    private readonly IProcessLauncherService _processLauncher;
    private readonly ILocalizationService _loc;

    [ObservableProperty] private ObservableCollection<BackupVersionInfo> _versions = new();
    [ObservableProperty] private BackupVersionInfo? _selectedVersion;
    [ObservableProperty] private string _latestBackupText = string.Empty;
    [ObservableProperty] private string _latestStatusText = string.Empty;
    [ObservableProperty] private string _backupLocationText = string.Empty;
    [ObservableProperty] private int _retentionCount;
    [ObservableProperty] private bool _hasVersions;
    [ObservableProperty] private bool _canRestoreSelected;

    public RecoveryCenterModel(
        IVersionedBackupService backupService,
        IDialogService dialogService,
        IFileDialogService fileDialogService,
        INavigationService navigationService,
        IApplicationLifecycleService lifecycleService,
        IProcessLauncherService processLauncher,
        ILocalizationService loc)
    {
        _backupService = backupService;
        _dialogService = dialogService;
        _fileDialogService = fileDialogService;
        _navigationService = navigationService;
        _lifecycleService = lifecycleService;
        _processLauncher = processLauncher;
        _loc = loc;

        _loc.LanguageChanged += OnLanguageChanged;
        RetentionCount = _backupService.RetentionCount;
        LoadData();
    }

    public void Dispose() => _loc.LanguageChanged -= OnLanguageChanged;

    private void OnLanguageChanged(object? sender, EventArgs e) => UpdateLatestSummary(Versions);

    partial void OnSelectedVersionChanged(BackupVersionInfo? value)
        => CanRestoreSelected = value is not null;

    [RelayCommand]
    private void LoadData()
    {
        SelectedVersion = null;
        var versions = _backupService.ListVersions();
        Versions = new ObservableCollection<BackupVersionInfo>(versions);
        HasVersions = versions.Count > 0;
        UpdateLatestSummary(versions);
        BackupLocationText = _backupService.BackupDirectory;
    }

    private void UpdateLatestSummary(IReadOnlyList<BackupVersionInfo> versions)
    {
        var latest = versions.FirstOrDefault();
        LatestBackupText = latest is null
            ? _loc["RC_LatestNone"]
            : latest.CreatedAtLocal.ToString("yyyy-MM-dd HH:mm:ss");
        LatestStatusText = latest is null
            ? _loc["RC_StatusNone"]
            : latest.IsValid ? _loc["RC_StatusValid"] : _loc["RC_StatusInvalid"];
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        var created = await Task.Run(_backupService.CreateVersion);
        if (created is null)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["RC_BackupFailed"]);
            return;
        }

        LoadData();
        await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], _loc["RC_BackupCreated"]);
    }

    /// <summary>
    /// Restore is destructive: it replaces the current database file. The plan (target version,
    /// current database path, current document count) is always confirmed before any write.
    /// On success the app must be restarted, so the steps are shown and the app shuts down.
    /// </summary>
    [RelayCommand]
    private async Task RestoreSelectedAsync()
    {
        if (SelectedVersion is null) return;

        var plan = await Task.Run(() => _backupService.PlanRestore(SelectedVersion.FilePath));
        if (plan is null)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["RC_ErrorInvalidVersion"]);
            return;
        }

        var message = string.Format(
            _loc["RC_ConfirmRestoreMessage"],
            plan.SourceCreatedAtLocal.ToString("yyyy-MM-dd HH:mm:ss"),
            plan.CurrentDocumentCount,
            plan.CurrentDatabasePath);

        var confirmed = await _dialogService.ShowConfirmAsync(
            _loc["RC_ConfirmRestoreTitle"],
            message,
            _loc["Btn_OverwriteRestore"],
            isDanger: true);
        if (!confirmed) return;

        var outcome = await Task.Run(() => _backupService.Restore(plan.SourcePath));
        if (!outcome.Success)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc[outcome.ErrorKey ?? "RC_ErrorRestoreFailed"]);
            return;
        }

        await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], _loc["RC_RestoreRestartMessage"]);
        _lifecycleService.Shutdown();
    }

    [RelayCommand]
    private async Task SaveRetentionAsync()
    {
        _backupService.RetentionCount = RetentionCount;
        RetentionCount = _backupService.RetentionCount;
        LoadData();
        await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], _loc["RC_RetentionSaved"]);
    }

    [RelayCommand]
    private async Task OpenBackupFolderAsync()
    {
        try
        {
            var folder = _backupService.BackupDirectory;
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            _processLauncher.OpenFolder(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private void GoToRecycleBin() => _navigationService.NavigateTo("recycle");

    [RelayCommand]
    private void GoToFileIntegrity() => _navigationService.NavigateTo("integrity");

    [RelayCommand]
    private async Task RestoreFromFileAsync()
    {
        var path = await _fileDialogService.ShowOpenFileAsync(
            _loc["Dashboard_SelectBackup"],
            _loc["Dashboard_BackupOpenFilter"]);
        if (string.IsNullOrWhiteSpace(path)) return;

        if (!File.Exists(path))
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["RC_ErrorInvalidVersion"]);
            return;
        }

        SelectedVersion = new BackupVersionInfo(path, File.GetLastWriteTime(path), 0, IsValid: true, IsLatest: false);
        await RestoreSelectedAsync();
    }
}
