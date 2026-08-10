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
    private readonly IDialogService _dialogService;
    private readonly IFileDialogService _fileDialogService;
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _loc;
    private readonly IDroppedFileImportService _droppedFileImportService;
    private readonly IAnalyticsService? _analytics;

    [ObservableProperty] private string _folderPath = string.Empty;
    [ObservableProperty] private string _defaultSubject = string.Empty;
    [ObservableProperty] private ObservableCollection<FileImportItem> _files = new();
    [ObservableProperty] private int _importedCount;
    [ObservableProperty] private int _skippedDuplicateCount;
    [ObservableProperty] private int _failedCount;
    [ObservableProperty] private bool _isImporting;
    [ObservableProperty] private string _importStatusMessage = string.Empty;
    [ObservableProperty] private string _importErrorMessage = string.Empty;

    public bool HasFiles => Files.Count > 0;

    public BatchImportModel(
        IDialogService dialogService,
        IFileDialogService fileDialogService,
        INavigationService navigationService,
        ILocalizationService loc,
        IDroppedFileImportService droppedFileImportService,
        IAnalyticsService? analytics = null)
    {
        _dialogService = dialogService;
        _fileDialogService = fileDialogService;
        _navigationService = navigationService;
        _loc = loc;
        _droppedFileImportService = droppedFileImportService;
        _analytics = analytics;
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
        if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath) || IsImporting)
            return;

        Files.Clear();
        ImportErrorMessage = string.Empty;

        try
        {
            var supportedExts = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".mp4", ".mp3", ".zip", ".rar" };

            foreach (var file in Directory.EnumerateFiles(FolderPath, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (!supportedExts.Contains(ext))
                    continue;

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
        catch (Exception)
        {
            Files.Clear();
            ImportErrorMessage = _loc["BatchImport_ScanError"];
        }

        OnPropertyChanged(nameof(HasFiles));
    }

    public Task<int> AddDroppedFilesAsync(IReadOnlyList<string> filePaths)
    {
        var added = 0;
        ImportErrorMessage = string.Empty;

        foreach (var filePath in filePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(filePath) || Files.Any(file => string.Equals(file.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
                continue;

            try
            {
                var document = _droppedFileImportService.BuildDocumentFromPath(filePath);
                Files.Add(new FileImportItem
                {
                    FileName = document.Name,
                    FilePath = document.FilePath,
                    FileType = document.Type,
                    FileSizeMB = document.FileSize ?? 0,
                    IsSelected = true
                });
                added++;
            }
            catch (Exception)
            {
                ImportErrorMessage = _loc["BatchImport_ScanError"];
            }
        }

        OnPropertyChanged(nameof(HasFiles));
        return Task.FromResult(added);
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (IsImporting)
            return;

        var selected = Files.Where(file => file.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Import_NoFileSelected"]);
            return;
        }

        IsImporting = true;
        ImportedCount = 0;
        SkippedDuplicateCount = 0;
        FailedCount = 0;
        ImportErrorMessage = string.Empty;
        ImportStatusMessage = _loc["BatchImport_StatusImporting"];

        try
        {
            await Task.Yield();

            foreach (var item in selected)
            {
                var document = new StudyDocument
                {
                    Name = item.FileName,
                    Subject = DefaultSubject,
                    Type = item.FileType,
                    FilePath = item.FilePath,
                    FileSize = item.FileSizeMB
                };

                DocumentImportOutcome outcome;
                try
                {
                    outcome = _droppedFileImportService.SaveDocument(document);
                }
                catch (Exception)
                {
                    outcome = DocumentImportOutcome.Failed;
                }

                switch (outcome)
                {
                    case DocumentImportOutcome.Imported:
                        ImportedCount++;
                        item.IsSelected = false;
                        break;
                    case DocumentImportOutcome.SkippedDuplicate:
                        SkippedDuplicateCount++;
                        item.IsSelected = false;
                        break;
                    case DocumentImportOutcome.Failed:
                        FailedCount++;
                        break;
                }
            }
        }
        finally
        {
            IsImporting = false;
        }

        ImportStatusMessage = string.Format(
            _loc["BatchImport_ResultSummary"],
            ImportedCount,
            SkippedDuplicateCount,
            FailedCount);

        if (FailedCount > 0)
        {
            ImportErrorMessage = string.Format(_loc["BatchImport_FailuresRemain"], FailedCount);
            return;
        }

        if (_analytics is not null)
            AnalyticsDispatch.Capture(_analytics, "batch_import_completed");

        await _dialogService.ShowMessageAsync(_loc["Dialog_Complete"], ImportStatusMessage);
        _navigationService.NavigateTo("dashboard");
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var file in Files)
            file.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var file in Files)
            file.IsSelected = false;
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
