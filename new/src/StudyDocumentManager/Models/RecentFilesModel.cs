using System.Collections.ObjectModel;
using System.Diagnostics;
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

    [ObservableProperty] private ObservableCollection<RecentFileItem> _recentFiles = new();

    public RecentFilesModel(IDialogService dialogService, INavigationService navigationService, IRecentFile recentRepo)
    {
        _dialogService = dialogService;
        _navigationService = navigationService;
        _recentRepo = recentRepo;
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
                DocumentName = f.Ten,
                Subject = f.MonHoc ?? "",
                FileType = f.Loai ?? "",
                FilePath = f.DuongDan ?? "",
                OpenedAt = f.OpenedAt,
                FileExists = !string.IsNullOrEmpty(f.DuongDan) && File.Exists(f.DuongDan)
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
            Process.Start(new ProcessStartInfo
            {
                FileName = item.FilePath,
                UseShellExecute = true
            });
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
