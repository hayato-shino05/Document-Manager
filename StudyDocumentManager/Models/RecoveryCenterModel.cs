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
    [ObservableProperty] private bool _isLoading;

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
        BackupLocationText = _backupService.BackupDirectory;
        _ = LoadDataAsync();
    }

    public void Dispose() => _loc.LanguageChanged -= OnLanguageChanged;

    private void OnLanguageChanged(object? sender, EventArgs e) => UpdateLatestSummary(Versions);

    partial void OnSelectedVersionChanged(BackupVersionInfo? value)
        => CanRestoreSelected = value is { IsValid: true };

    [RelayCommand]
    private Task LoadDataAsync() => QueueLoad();

    // Invariant: each refresh runs strictly after the previous one, so a caller that
    // mutates backups then awaits its own load always observes the mutation.
    private Task _loadChain = Task.CompletedTask;

    private async Task<bool> QueueLoad()
    {
        var context = SynchronizationContext.Current;
        var run = RunLoad(_loadChain);
        _loadChain = run;
        _ = run.ContinueWith(_ =>
        {
            if (!ReferenceEquals(_loadChain, run))
                return;

            if (context is not null)
            {
                context.Post(_ =>
                {
                    // Re-check on the target context: a newer load may have started
                    // after this continuation was queued but before it runs here.
                    if (ReferenceEquals(_loadChain, run))
                        IsLoading = false;
                }, null);
            }
            else
            {
                IsLoading = false;
            }
        }, TaskScheduler.Default);
        return await run;
    }

    private async Task<bool> RunLoad(Task previous)
    {
        IsLoading = true;
        try
        {
            await previous;
            var versions = await Task.Run(() => _backupService.ListVersions());
            SelectedVersion = null;
            Versions = new ObservableCollection<BackupVersionInfo>(versions);
            HasVersions = versions.Count > 0;
            UpdateLatestSummary(versions);
            BackupLocationText = _backupService.BackupDirectory;
            return true;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Recovery Center refresh failed.");
            return false;
        }
    }

    public bool ShowEmptyState => !IsLoading && !HasVersions;

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyState));

    partial void OnHasVersionsChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyState));

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

        if (!await QueueLoad())
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
            return;
        }

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
        await RestoreFromPathAsync(SelectedVersion.FilePath);
    }

    private async Task RestoreFromPathAsync(string sourcePath)
    {
        var plan = await Task.Run(() => _backupService.PlanRestore(sourcePath));
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
        if (!await QueueLoad())
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
            return;
        }
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

        await RestoreFromPathAsync(path);
    }
}
