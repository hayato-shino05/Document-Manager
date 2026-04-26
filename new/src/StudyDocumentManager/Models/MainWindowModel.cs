using StudyDocumentManager.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class MainWindowModel : ModelBase
{
    [ObservableProperty]
    private ModelBase _currentView;

    [ObservableProperty]
    private string _appVersion = Services.AppVersion.Current;

    [ObservableProperty]
    private string _statusText = "Tổng số: 0 tài liệu";

    public bool CanGoBack => _navigationService.CanGoBack;

    private static readonly List<string> DefaultSubjects =
    [
        "Công việc", "Cá nhân", "Học tập", "Dự án", "Tài chính", "Hợp đồng", "Tham khảo", "Khác"
    ];

    private static readonly List<string> DefaultTypes =
    [
        "PDF", "Word", "Excel", "PowerPoint", "Tài liệu", "Báo cáo", "Hướng dẫn", "Biểu mẫu", "Hình ảnh", "Video", "Audio", "Nén", "Khác"
    ];

    private readonly NavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly DroppedFileImportService _droppedFileImportService;

    public MainWindowModel(
        DashboardModel dashboardModel,
        NavigationService navigationService,
        IDialogService dialogService,
        DroppedFileImportService droppedFileImportService)
    {
        _navigationService = navigationService;
        _dialogService = dialogService;
        _droppedFileImportService = droppedFileImportService;
        _currentView = dashboardModel;

        // Wire up navigation service
        _navigationService.SetMainModel(this);

        // Auto-check for updates on startup (silent, non-blocking)
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000); // Wait 3s after startup
            await UpdateService.CheckSilentlyAsync(_dialogService);
        });
    }

    [RelayCommand]
    private async Task CheckForUpdateAsync()
    {
        StatusText = "Đang kiểm tra cập nhật...";
        var info = await UpdateChecker.CheckForUpdateAsync();
        if (info == null)
        {
            await _dialogService.ShowMessageAsync("Cập nhật", "Không thể kết nối đến server.\nVui lòng kiểm tra kết nối mạng.");
            StatusText = "Không thể kiểm tra cập nhật";
        }
        else if (!info.HasUpdate)
        {
            await _dialogService.ShowMessageAsync("Cập nhật", $"Bạn đang sử dụng phiên bản mới nhất (v{Services.AppVersion.Current}).");
            StatusText = "Đã là phiên bản mới nhất";
        }
        else
        {
            await UpdateService.HandleUpdateAsync(info, _dialogService);
            StatusText = $"Phiên bản mới {info.NewVersion} có sẵn";
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

    // ═══ Proxy commands — delegate to active DashboardVM ═══

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
        var version = Services.AppVersion.Current;
        await _dialogService.ShowMessageAsync("Giới thiệu",
            $"Study Document Manager\n" +
            $"Professional Edition\n" +
            $"\n" +
            $"Phiên bản {version}\n" +
            $"\n" +
            $"Ứng dụng quản lý tài liệu học tập\n" +
            $"Kiến trúc MVVM • Avalonia UI • .NET 9.0\n" +
            $"\n" +
            $"Sinh viên thực hiện: Vũ Đức Dũng - TT601-K14\n" +
            $"Cán bộ hướng dẫn: Lê Thị Mai\n" +
            $"\n" +
            $"© 2024-2025 hayato-shino05\n" +
            $"GitHub: hayato-shino05/study-document-manager");
    }

    [RelayCommand]
    private void ExitApp()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
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

        var subjects = _droppedFileImportService.GetAvailableSubjects(DefaultSubjects);
        var types = _droppedFileImportService.GetAvailableTypes(DefaultTypes);

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
        var draft = await _dialogService.ShowAddDocumentAsync(filePath, subjects, types);
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
}


