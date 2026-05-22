using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Services;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Services;

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

    public IReadOnlyList<SupportedLanguage> AvailableLanguages => _loc.AvailableLanguages;

    public bool CanGoBack => _navigationService.CanGoBack;

    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly ICustomDialogService _customDialogService;
    private readonly IDroppedFileImportService _droppedFileImportService;
    private readonly IApplicationLifecycleService _lifecycleService;
    private readonly ILocalizationService _loc;

    public MainWindowModel(
        DashboardModel dashboardModel,
        INavigationService navigationService,
        IDialogService dialogService,
        ICustomDialogService customDialogService,
        IDroppedFileImportService droppedFileImportService,
        IApplicationLifecycleService lifecycleService,
        ILocalizationService loc)
    {
        _navigationService = navigationService;
        _dialogService = dialogService;
        _customDialogService = customDialogService;
        _droppedFileImportService = droppedFileImportService;
        _lifecycleService = lifecycleService;
        _loc = loc;
        _currentView = dashboardModel;
        _statusText = string.Format(_loc["Status_TotalDocs"], 0);

        // DB保存済み言語をロード
        LoadLanguageFromSettings();

        // 起動後3秒でサイレント更新チェック
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);
            await UpdateService.CheckSilentlyAsync(_dialogService, _loc);
        });
    }

    [RelayCommand]
    private async Task CheckForUpdateAsync()
    {
        StatusText = _loc["Status_CheckingUpdate"];
        var info = await UpdateChecker.CheckForUpdateAsync();
        if (info == null)
        {
            await _dialogService.ShowMessageAsync(_loc["Main_UpdateTitle"], _loc["Main_CannotConnect"]);
            StatusText = _loc["Status_CannotCheckUpdate"];
        }
        else if (!info.HasUpdate)
        {
            await _dialogService.ShowMessageAsync(_loc["Main_UpdateTitle"],
                string.Format(_loc["Main_AlreadyLatest"], Core.Services.AppVersion.Current));
            StatusText = _loc["Status_UpToDate"];
        }
        else
        {
            await UpdateService.HandleUpdateAsync(info, _dialogService, _loc);
            StatusText = string.Format(_loc["Status_NewVersionAvailable"], info.NewVersion);
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
        StatusText = dashboard.StatusText;
    }

    partial void OnCurrentViewChanged(ModelBase value)
    {
        StatusText = value switch
        {
            DashboardModel dashboard => dashboard.StatusText,
            CategoryManagementModel catMgmt => catMgmt.StatusText,
            _ => string.Empty
        };
        OnPropertyChanged(nameof(CanGoBack));
    }

    public async Task HandleDroppedFilesAsync(IReadOnlyList<string> filePaths)
    {
        if (filePaths.Count == 0)
            return;

        var subjects = _droppedFileImportService.GetAvailableSubjects([]);
        var types = _droppedFileImportService.GetAvailableTypes([]);

        int imported = filePaths.Count == 1
            ? await ImportSingleFileAsync(filePaths[0], subjects, types)
            : ImportMultipleFiles(filePaths);

        if (imported == 0)
            return;

        if (CurrentView is DashboardModel dashboard)
        {
            dashboard.RefreshCommand.Execute(null);
            UpdateStatusFromDashboard(dashboard);
        }
    }

    private async Task<int> ImportSingleFileAsync(string filePath, IList<string> subjects, IList<string> types)
    {
        var draft = await _customDialogService.ShowAddDocumentAsync(filePath, subjects, types);
        if (draft == null)
            return 0;

        var document = draft.ToStudyDocument();
        return _droppedFileImportService.SaveDocument(document) ? 1 : 0;
    }

    private int ImportMultipleFiles(IEnumerable<string> filePaths)
    {
        int imported = 0;

        foreach (var filePath in filePaths)
        {
            var document = _droppedFileImportService.BuildDocumentFromPath(filePath);
            if (!_droppedFileImportService.SaveDocument(document))
                continue;

            imported++;
        }

        return imported;
    }

    private void LoadLanguageFromSettings()
    {
        var saved = DatabaseHelper.GetSetting("language");
        if (Enum.TryParse<SupportedLanguage>(saved, out var lang))
        {
            _loc.SetLanguage(lang);
            _selectedLanguage = lang;
        }
        else
        {
            _selectedLanguage = _loc.CurrentLanguage;
        }
    }

    partial void OnSelectedLanguageChanged(SupportedLanguage value)
    {
        System.Diagnostics.Debug.WriteLine($"[LANG-DEBUG] OnSelectedLanguageChanged fired: value={value}");
        _loc.SetLanguage(value);
        DatabaseHelper.SetSetting("language", value.ToString());
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
