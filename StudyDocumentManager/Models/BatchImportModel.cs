using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Core.Services;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class BatchImportModel : ModelBase
{
    private readonly IDocument _repository;
    private readonly IDialogService _dialogService;
    private readonly IFileDialogService _fileDialogService;
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _loc;

    [ObservableProperty] private string _folderPath = string.Empty;
    [ObservableProperty] private string _defaultSubject = string.Empty;
    [ObservableProperty] private ObservableCollection<FileImportItem> _files = new();
    [ObservableProperty] private int _importedCount;
    [ObservableProperty] private bool _isImporting;

    public BatchImportModel(IDocument repository, IDialogService dialogService, IFileDialogService fileDialogService, INavigationService navigationService, ILocalizationService loc)
    {
        _repository = repository;
        _dialogService = dialogService;
        _fileDialogService = fileDialogService;
        _navigationService = navigationService;
        _loc = loc;
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var path = await _fileDialogService.ShowOpenFolderAsync(_loc["Import_SelectFolder"]);
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
                    FileType = FileTypeDetector.Detect(ext),
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
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Import_NoFileSelected"]);
            return;
        }

        IsImporting = true;
        ImportedCount = 0;

        foreach (var item in selected)
        {
            var doc = new StudyDocument
            {
                Name = item.FileName,
                Subject = DefaultSubject,
                Type = item.FileType,
                FilePath = item.FilePath,
                FileSize = item.FileSizeMB
            };

            if (_repository.Add(doc))
            {
                ImportedCount++;
            }
        }

        IsImporting = false;
        await _dialogService.ShowMessageAsync(_loc["Dialog_Complete"],
            string.Format(_loc["Import_Done"], ImportedCount, selected.Count));
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
