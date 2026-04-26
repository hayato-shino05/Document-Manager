using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class BatchImportModel : ModelBase
{
    private readonly IDocument _repository;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    [ObservableProperty] private string _folderPath = string.Empty;
    [ObservableProperty] private string _defaultSubject = string.Empty;
    [ObservableProperty] private ObservableCollection<FileImportItem> _files = new();
    [ObservableProperty] private int _importedCount;
    [ObservableProperty] private bool _isImporting;

    public BatchImportModel(IDocument repository, IDialogService dialogService, INavigationService navigationService)
    {
        _repository = repository;
        _dialogService = dialogService;
        _navigationService = navigationService;
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var path = await _dialogService.ShowOpenFolderAsync("Chọn thư mục chứa tài liệu");
        if (!string.IsNullOrEmpty(path))
        {
            FolderPath = path;
            ScanFolder();
        }
    }

    [RelayCommand]
    private void ScanFolder()
    {
        if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath)) return;

        Files.Clear();
        var supportedExts = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".mp4", ".mp3", ".zip", ".rar" };

        foreach (var file in Directory.EnumerateFiles(FolderPath, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (supportedExts.Contains(ext))
            {
                var info = new FileInfo(file);
                Files.Add(new FileImportItem
                {
                    FileName = Path.GetFileNameWithoutExtension(file),
                    FilePath = file,
                    FileType = Services.FileTypeDetector.Detect(ext),
                    FileSizeMB = info.Length / (1024.0 * 1024.0),
                    IsSelected = true
                });
            }
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var selected = Files.Where(f => f.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await _dialogService.ShowErrorAsync("Lỗi", "Không có file nào được chọn để import.");
            return;
        }

        IsImporting = true;
        ImportedCount = 0;

        foreach (var item in selected)
        {
            var doc = new StudyDocument
            {
                Ten = item.FileName,
                MonHoc = DefaultSubject,
                Loai = item.FileType,
                DuongDan = item.FilePath,
                KichThuoc = item.FileSizeMB
            };

            if (_repository.Add(doc))
            {
                ImportedCount++;
            }
        }

        IsImporting = false;
        await _dialogService.ShowMessageAsync("Hoàn tất", $"Đã import thành công {ImportedCount}/{selected.Count} tài liệu!");
        _navigationService.NavigateTo("dashboard");
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var f in Files) f.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var f in Files) f.IsSelected = false;
    }

    [RelayCommand]
    private void Cancel() => _navigationService.NavigateTo("dashboard");
}

public partial class FileImportItem : ObservableObject
{
    [ObservableProperty] private string _fileName = string.Empty;
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _fileType = string.Empty;
    [ObservableProperty] private double _fileSizeMB;
    [ObservableProperty] private bool _isSelected = true;
}
