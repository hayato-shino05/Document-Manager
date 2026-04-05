using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentView;

    [ObservableProperty]
    private string _appVersion = Core.Services.AppVersion.Current;

    [ObservableProperty]
    private string _statusText = "Tổng số: 0 tài liệu";

    public bool CanGoBack => _navigationService.CanGoBack;

    private readonly NavigationService _navigationService;
    private readonly IDialogService _dialogService;

    public MainWindowViewModel(DashboardViewModel dashboardViewModel, NavigationService navigationService, IDialogService dialogService)
    {
        _navigationService = navigationService;
        _dialogService = dialogService;
        _currentView = dashboardViewModel;

        // Wire up navigation service
        _navigationService.SetMainViewModel(this);

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
            await _dialogService.ShowMessageAsync("Cập nhật", $"Bạn đang sử dụng phiên bản mới nhất (v{Core.Services.AppVersion.Current}).");
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
        if (CurrentView is DashboardViewModel dashboard)
        {
            dashboard.RefreshCommand.Execute(null);
            UpdateStatusFromDashboard(dashboard);
        }
    }

    // ═══ Proxy commands — delegate to active DashboardVM ═══

    [RelayCommand]
    private void EditDocument()
    {
        if (CurrentView is DashboardViewModel dashboard)
            dashboard.EditDocumentCommand.Execute(null);
    }

    [RelayCommand]
    private void DeleteDocument()
    {
        if (CurrentView is DashboardViewModel dashboard)
            dashboard.DeleteDocumentCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenFile()
    {
        if (CurrentView is DashboardViewModel dashboard)
            dashboard.OpenFileCommand.Execute(null);
    }

    [RelayCommand]
    private void ExportCsv()
    {
        if (CurrentView is DashboardViewModel dashboard)
            dashboard.ExportCsvCommand.Execute(null);
    }

    [RelayCommand]
    private async Task ShowAboutAsync()
    {
        var version = Core.Services.AppVersion.Current;
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
        if (CurrentView is DashboardViewModel dashboard)
            dashboard.BackupDatabaseCommand.Execute(null);
    }

    [RelayCommand]
    private void RestoreDatabase()
    {
        if (CurrentView is DashboardViewModel dashboard)
            dashboard.RestoreDatabaseCommand.Execute(null);
    }

    private void UpdateStatusFromDashboard(DashboardViewModel dashboard)
    {
        StatusText = dashboard.StatusText;
    }

    partial void OnCurrentViewChanged(ViewModelBase value)
    {
        if (value is DashboardViewModel dashboard)
        {
            UpdateStatusFromDashboard(dashboard);
        }
        OnPropertyChanged(nameof(CanGoBack));
    }

    /// <summary>
    /// Handle a single file dropped onto the window — navigate to AddEdit view with file pre-filled.
    /// </summary>
    public void HandleDroppedFile(string filePath)
    {
        _navigationService.NavigateTo("add");
        if (CurrentView is AddEditViewModel addEditVm)
        {
            addEditVm.DuongDan = filePath;
            addEditVm.Ten = System.IO.Path.GetFileNameWithoutExtension(filePath);
            var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            addEditVm.Loai = ext switch
            {
                ".pdf" => "Tài liệu",
                ".doc" or ".docx" => "Tài liệu",
                ".ppt" or ".pptx" => "Tài liệu",
                ".xls" or ".xlsx" => "Tài liệu",
                ".txt" => "Tài liệu",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "Hình ảnh",
                ".mp4" or ".avi" or ".mkv" or ".mov" => "Video",
                ".mp3" or ".wav" or ".flac" => "Audio",
                ".zip" or ".rar" or ".7z" => "Nén",
                _ => ext.TrimStart('.').ToUpperInvariant()
            };
        }
        OnPropertyChanged(nameof(CanGoBack));
    }
}
