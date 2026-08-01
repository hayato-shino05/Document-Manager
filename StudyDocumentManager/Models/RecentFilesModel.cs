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
    private readonly IRecentFileRepository _recentRepo;
    private readonly IProcessLauncherService _processLauncher;
    private readonly ILocalizationService _loc;

    [ObservableProperty] private ObservableCollection<RecentFileItem> _recentFiles = new();

    public RecentFilesModel(
        IDialogService dialogService,
        INavigationService navigationService,
        IRecentFileRepository recentRepo,
        IProcessLauncherService processLauncher,
        ILocalizationService loc)
    {
        _dialogService = dialogService;
        _navigationService = navigationService;
        _recentRepo = recentRepo;
        _processLauncher = processLauncher;
        _loc = loc;
        _loc.LanguageChanged += (_, _) => LoadData();
        LoadData();
    }

    private void LoadData()
    {
        var files = _recentRepo.GetAll();
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
                FileExists = !string.IsNullOrEmpty(f.FilePath) && File.Exists(f.FilePath),
                FileExistsLabel = _loc["Recent_FileExists"],
                FileMissingLabel = _loc["Recent_FileMissing"]
            });
        }
    }

    [RelayCommand]
    private async Task OpenFile(RecentFileItem? item)
    {
        if (item == null || string.IsNullOrEmpty(item.FilePath) || !item.FileExists) return;

        try
        {
            _processLauncher.OpenFile(item.FilePath);
        }
        catch
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
            return;
        }

        _recentRepo.Add(item.DocumentId);
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        if (RecentFiles.Count == 0) return;

        var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"], _loc["Recent_ConfirmClear"]);
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
    public string FileExistsLabel { get; set; } = "✓";
    public string FileMissingLabel { get; set; } = "✗";
    public string OpenedAtDisplay => OpenedAt.ToString("dd/MM/yyyy HH:mm");
    public string StatusDisplay => FileExists ? FileExistsLabel : FileMissingLabel;
}
