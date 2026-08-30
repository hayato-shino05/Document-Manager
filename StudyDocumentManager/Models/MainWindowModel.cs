using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Services;

namespace StudyDocumentManager.Models;

public partial class MainWindowModel : ModelBase
{
    [ObservableProperty]
    private ModelBase _currentView;

    [ObservableProperty]
    private string _appVersion = Core.Services.AppVersion.Current;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private SupportedLanguage _selectedLanguage;

    [ObservableProperty]
    private bool _canUndo;

    public IReadOnlyList<SupportedLanguage> AvailableLanguages => _loc.AvailableLanguages;

    public bool CanGoBack => _navigationService.CanGoBack;
    public bool CanAcceptDroppedFiles => CurrentView is DashboardModel or AddEditModel or BatchImportModel;

    public event EventHandler? HelpRequested;

    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ICustomDialogService _customDialogService;
    private readonly IDroppedFileImportService _droppedFileImportService;
    private readonly IApplicationLifecycleService _lifecycleService;
    private readonly ILocalizationService _loc;
    private readonly ISettingsService _settingsService;
    private readonly IUpdateService _updateService;
    private readonly IUndoApplier? _undoApplier;
    private readonly IUndoService? _undoService;
    private string? _statusKey = "Status_TotalDocs";
    private object[] _statusArguments = [0];

    public MainWindowModel(
        DashboardModel dashboardModel,
        INavigationService navigationService,
        IDialogService dialogService,
        ICustomDialogService customDialogService,
        IDroppedFileImportService droppedFileImportService,
        IApplicationLifecycleService lifecycleService,
        ILocalizationService loc,
        ISettingsService settingsService,
        IUpdateService updateService,
        IUndoApplier? undoApplier = null,
        IUndoService? undoService = null)
    {
        _navigationService = navigationService;
        _dialogService = dialogService;
        _customDialogService = customDialogService;
        _droppedFileImportService = droppedFileImportService;
        _lifecycleService = lifecycleService;
        _loc = loc;
        _settingsService = settingsService;
        _updateService = updateService;
        _undoApplier = undoApplier;
        _undoService = undoService;
        _currentView = dashboardModel;
        _statusText = FormatLocalizedStatus();
        if (_undoService != null)
        {
            CanUndo = _undoApplier != null && _undoService.CanUndo;
            _undoService.StackChanged += () => CanUndo = _undoApplier != null && _undoApplier.CanUndo;
        }
        _loc.LanguageChanged += (_, _) =>
        {
            if (_statusKey != null)
                StatusText = FormatLocalizedStatus();
            else if (CurrentView is DashboardModel dashboard)
                StatusText = dashboard.StatusText;
            else if (CurrentView is CategoryManagementModel categoryManagement)
                StatusText = categoryManagement.StatusText;
        };

        SelectedLanguage = _loc.CurrentLanguage;

        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);
            await _updateService.CheckSilentlyAsync();
        });
    }

    private string FormatLocalizedStatus()
        => string.Format(_loc[_statusKey!], _statusArguments);

    private void SetLocalizedStatus(string key, params object[] arguments)
    {
        _statusKey = key;
        _statusArguments = arguments;
        StatusText = FormatLocalizedStatus();
    }

    private void SetExternalStatus(string value)
    {
        _statusKey = null;
        StatusText = value;
    }

    [RelayCommand]
    private async Task CheckForUpdateAsync()
    {
        SetLocalizedStatus("Status_CheckingUpdate");
        var info = await _updateService.CheckForUpdateAsync();
        if (info == null)
        {
            await _dialogService.ShowMessageAsync(_loc["Main_UpdateTitle"], _loc["Main_CannotConnect"]);
            SetLocalizedStatus("Status_CannotCheckUpdate");
        }
        else if (!info.HasUpdate)
        {
            await _dialogService.ShowMessageAsync(_loc["Main_UpdateTitle"],
                string.Format(_loc["Main_AlreadyLatest"], Core.Services.AppVersion.Current));
            SetLocalizedStatus("Status_UpToDate");
        }
        else
        {
            await _updateService.HandleUpdateAsync(info);
            SetLocalizedStatus("Status_NewVersionAvailable", info.NewVersion);
        }
    }

    [RelayCommand]
    private void Navigate(string target)
    {
        _navigationService.NavigateTo(target);
        OnPropertyChanged(nameof(CanGoBack));
    }

    [RelayCommand]
    private void GoBack()
    {
        if (_navigationService.CanGoBack)
        {
            _navigationService.GoBack();
            OnPropertyChanged(nameof(CanGoBack));
        }
    }

    [RelayCommand]
    private async Task UndoLastAsync()
    {
        if (!CanUndo || _undoApplier == null) return;

        var entry = _undoService?.Peek();
        try
        {
            _undoApplier.ApplyLast();
        }
        catch (UndoPartialRestoreException ex)
        {
            SetLocalizedStatus("UN_Applied", string.Format(_loc["BE_Result_Partial"], ex.RestoredCount, ex.RequestedCount));
            RefreshCurrentViewAfterUndo();
            return;
        }
        catch (Exception)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
            return;
        }

        if (entry != null)
        {
            var detail = string.Format(_loc[entry.DescriptionKey], entry.DescriptionArgs ?? []);
            SetLocalizedStatus("UN_Applied", detail);
        }

        RefreshCurrentViewAfterUndo();
    }

    private void RefreshCurrentViewAfterUndo()
    {
        switch (CurrentView)
        {
            case DashboardModel dashboard:
                dashboard.RefreshCommand.Execute(null);
                break;
            case CategoryManagementModel categoryManagement:
                categoryManagement.RefreshCommand.Execute(null);
                break;
            case CollectionManagementModel collectionManagement:
                collectionManagement.RefreshCommand.Execute(null);
                break;
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        if (CurrentView is DashboardModel dashboard)
        {
            dashboard.RefreshCommand.Execute(null);
            UpdateStatusFromDashboard(dashboard);
        }
    }

    [RelayCommand]
    private void EditDocument()
    {
        if (CurrentView is DashboardModel dashboard)
            dashboard.EditDocumentCommand.Execute(null);
    }

    [RelayCommand]
    private void DeleteDocument()
    {
        if (CurrentView is DashboardModel dashboard)
            dashboard.DeleteDocumentCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenFile()
    {
        if (CurrentView is DashboardModel dashboard)
            dashboard.OpenFileCommand.Execute(null);
    }

    [RelayCommand]
    private void ExportCsv()
    {
        if (CurrentView is DashboardModel dashboard)
            dashboard.ExportCsvCommand.Execute(null);
    }

    [RelayCommand]
    private void ShowHelp()
    {
        HelpRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ShowAboutAsync()
    {
        var version = Core.Services.AppVersion.Current;
        await _dialogService.ShowMessageAsync(_loc["Dialog_About"],
            string.Format(_loc["Main_About"], version));
    }

    [RelayCommand]
    private void ExitApp()
    {
        _lifecycleService.Shutdown();
    }

    [RelayCommand]
    private void BackupDatabase()
    {
        if (CurrentView is DashboardModel dashboard)
            dashboard.BackupDatabaseCommand.Execute(null);
    }

    [RelayCommand]
    private void RestoreDatabase()
    {
        if (CurrentView is DashboardModel dashboard)
            dashboard.RestoreDatabaseCommand.Execute(null);
    }

    private void UpdateStatusFromDashboard(DashboardModel dashboard)
    {
        SetExternalStatus(dashboard.StatusText);
    }

    partial void OnCurrentViewChanged(ModelBase value)
    {
        switch (value)
        {
            case DashboardModel dashboard:
                SetExternalStatus(dashboard.StatusText);
                break;
            case CategoryManagementModel catMgmt:
                SetExternalStatus(catMgmt.StatusText);
                break;
            case SmartViewsModel smartViews:
                SetExternalStatus(smartViews.StatusText);
                break;
            default:
                SetExternalStatus(string.Empty);
                break;
        }
        OnPropertyChanged(nameof(CanGoBack));
    }

    public async Task HandleDroppedFilesAsync(IReadOnlyList<string> filePaths)
    {
        if (filePaths.Count == 0)
            return;

        var validPaths = filePaths
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (validPaths.Count == 0)
        {
            ShowInvalidDropStatus();
            return;
        }

        try
        {
            switch (CurrentView)
            {
                case AddEditModel addEdit when validPaths.Count == 1:
                    if (addEdit.TryApplyFile(validPaths[0]))
                        SetExternalStatus(string.Empty);
                    else
                        SetLocalizedStatus("BatchImport_InvalidDrop");
                    return;

                case BatchImportModel batchImport:
                    await batchImport.AddDroppedFilesAsync(validPaths);
                    SetExternalStatus(string.Empty);
                    return;

                case DashboardModel dashboard:
                {
                    var subjects = _droppedFileImportService.GetAvailableSubjects([]);
                    var types = _droppedFileImportService.GetAvailableTypes([]);
                    int imported = validPaths.Count == 1
                        ? await ImportSingleFileAsync(validPaths[0], subjects, types)
                        : ImportMultipleFiles(validPaths);

                    if (imported == 0)
                        return;

                    dashboard.RefreshCommand.Execute(null);
                    UpdateStatusFromDashboard(dashboard);
                    return;
                }

                default:
                    ShowInvalidDropStatus();
                    return;
            }
        }
        catch (IOException)
        {
            ShowInvalidDropStatus();
        }
        catch (UnauthorizedAccessException)
        {
            ShowInvalidDropStatus();
        }
    }

    public void ShowInvalidDropStatus()
    {
        SetLocalizedStatus("BatchImport_InvalidDrop");
    }

    private async Task<int> ImportSingleFileAsync(string filePath, IList<string> subjects, IList<string> types)
    {
        var draft = await _customDialogService.ShowAddDocumentAsync(filePath, subjects, types);
        if (draft == null)
            return 0;

        var document = draft.ToStudyDocument();
        return _droppedFileImportService.SaveDocument(document) == DocumentImportOutcome.Imported ? 1 : 0;
    }

    private int ImportMultipleFiles(IEnumerable<string> filePaths)
    {
        int imported = 0;

        foreach (var filePath in filePaths)
        {
            var document = _droppedFileImportService.BuildDocumentFromPath(filePath);
            if (_droppedFileImportService.SaveDocument(document) != DocumentImportOutcome.Imported)
                continue;

            imported++;
        }

        return imported;
    }

    partial void OnSelectedLanguageChanged(SupportedLanguage value)
    {
        System.Diagnostics.Debug.WriteLine($"[LANG-DEBUG] OnSelectedLanguageChanged fired: value={value}");
        _loc.SetLanguage(value);
        _settingsService.SetSetting("language", value.ToString());
        System.Diagnostics.Debug.WriteLine($"[LANG-DEBUG] Language persisted to DB: {value}");
    }

    [RelayCommand]
    private void ChangeLanguage(string langName)
    {
        System.Diagnostics.Debug.WriteLine($"[LANG-DEBUG] ChangeLanguageCommand invoked: langName='{langName}'");
        if (Enum.TryParse<SupportedLanguage>(langName, out var lang))
        {
            System.Diagnostics.Debug.WriteLine($"[LANG-DEBUG] Parsed OK → {lang}, current={SelectedLanguage}");
            SelectedLanguage = lang;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[LANG-DEBUG] PARSE FAILED for '{langName}'");
        }
    }
}
