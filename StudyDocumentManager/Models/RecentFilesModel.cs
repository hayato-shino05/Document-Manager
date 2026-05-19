using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class RecentFilesModel : ModelBase
{
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly IRecentFile _recentRepo;
    private readonly IProcessLauncherService _processLauncher;

    [ObservableProperty] private ObservableCollection<RecentFileItem> _recentFiles = new();

    public RecentFilesModel(
        IDialogService dialogService,
        INavigationService navigationService,
        IRecentFile recentRepo,
        IProcessLauncherService processLauncher)
    {
        _dialogService = dialogService;
        _navigationService = navigationService;
        _recentRepo = recentRepo;
        _processLauncher = processLauncher;
        LoadData();
    }

    private void LoadData()
    {
        var files = _recentRepo.GetAll();
        // Use Clear+Add to keep same ObservableCollection reference (avoid StackOverflow)
        RecentFiles.Clear();
        foreach (var f in files)
        {
            RecentFiles.Add(new RecentFileItem
            {
                DocumentId = f.Id,
                DocumentName = f.Name,
                Subject = f.Subject ?? "",
                FileType = f.Type ?? "",
                FilePath = f.FilePath ?? "",
                OpenedAt = f.OpenedAt,
                FileExists = !string.IsNullOrEmpty(f.FilePath) && File.Exists(f.FilePath)
            });
        }
    }

    [RelayCommand]
    private void OpenFile(RecentFileItem? item)
    {
        if (item == null || string.IsNullOrEmpty(item.FilePath) || !item.FileExists) return;

        try
        {
            _recentRepo.Add(item.DocumentId);
            _processLauncher.OpenFile(item.FilePath);
        }
        catch { /* Ignore */ }
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        if (RecentFiles.Count == 0) return;

        var confirmed = await _dialogService.ShowConfirmAsync("Xác nhận", "Xóa toàn bộ lịch sử file đã mở?");
        if (confirmed)
        {
            _recentRepo.Clear();
            LoadData();
        }
    }

    [RelayCommand]
    private void Refresh() => LoadData();

    [RelayCommand]
    private void GoBack() => _navigationService.NavigateTo("dashboard");
}

public class RecentFileItem
{
    public int DocumentId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    public bool FileExists { get; set; }
    public string OpenedAtDisplay => OpenedAt.ToString("dd/MM/yyyy HH:mm");
    public string StatusDisplay => FileExists ? "✓" : "✗ Mất file";
}
