using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Core.Services;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public enum BatchImportFailureCode
{
    None,
    ImportFailed,
    FileError,
    PermissionError,
    DatabaseError
}

public partial class BatchImportModel : ModelBase, IDisposable
{
    private readonly IDialogService _dialogService;
    private readonly IFileDialogService _fileDialogService;
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _loc;
    private readonly IDroppedFileImportService _droppedFileImportService;
    private readonly IImportInboxRepository? _importInboxRepository;
    private readonly IAnalyticsService? _analytics;
    private readonly OperationProgress _operationProgress = new();
    private CancellationTokenSource? _importCancellation;

    [ObservableProperty] private string _folderPath = string.Empty;
    [ObservableProperty] private string _defaultSubject = string.Empty;
    [ObservableProperty] private ObservableCollection<FileImportItem> _files = new();
    [ObservableProperty] private int _importedCount;
    [ObservableProperty] private int _skippedDuplicateCount;
    [ObservableProperty] private int _failedCount;
    [ObservableProperty] private bool _isImporting;
    [ObservableProperty] private bool _isImportCancelled;
    [ObservableProperty] private int _processedCount;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private string _importStatusMessage = string.Empty;
    [ObservableProperty] private string _importErrorMessage = string.Empty;

    public bool HasFiles => Files.Count > 0;
    public bool HasFailedItems => Files.Any(file => file.IsFailed);
    public IReadOnlyList<string> FailedItems => Files
        .Where(file => file.IsFailed)
        .Select(file => file.FilePath)
        .ToList();
    public int SucceededCount => _operationProgress.Succeeded;
    public int SkippedCount => _operationProgress.Skipped;

    public BatchImportModel(
        IDialogService dialogService,
        IFileDialogService fileDialogService,
        INavigationService navigationService,
        ILocalizationService loc,
        IDroppedFileImportService droppedFileImportService,
        IImportInboxRepository? importInboxRepository = null,
        IAnalyticsService? analytics = null)
    {
        _dialogService = dialogService;
        _fileDialogService = fileDialogService;
        _navigationService = navigationService;
        _loc = loc;
        _droppedFileImportService = droppedFileImportService;
        _importInboxRepository = importInboxRepository;
        _analytics = analytics;
        _loc.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var item in Files.Where(file => file.FailureCode != BatchImportFailureCode.None))
            item.FailureReason = GetFailureReason(item.FailureCode);
    }

    private string GetFailureReason(BatchImportFailureCode code)
        => code switch
        {
            BatchImportFailureCode.FileError => _loc["BatchImport_FileError"],
            BatchImportFailureCode.PermissionError => _loc["BatchImport_PermissionError"],
            BatchImportFailureCode.DatabaseError => _loc["BatchImport_DatabaseError"],
            _ => _loc["BatchImport_ItemFailed"]
        };

    public void Dispose()
    {
        _importCancellation?.Cancel();
        _loc.LanguageChanged -= OnLanguageChanged;
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
        ImportedCount = 0;
        SkippedDuplicateCount = 0;
        FailedCount = 0;
        IsImportCancelled = false;
        ProcessedCount = 0;
        TotalCount = 0;
        ImportStatusMessage = string.Empty;
        ImportErrorMessage = string.Empty;
        _operationProgress.Start(0);
        _operationProgress.Stop();
        NotifyOperationStateChanged();

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
    private Task ImportAsync() => ImportSelectedAsync(Files.Where(file => file.IsSelected).ToList());

    [RelayCommand]
    private Task RetryFailedAsync()
        => ImportSelectedAsync(Files.Where(file => file.IsFailed).ToList(), preserveCounters: true);

    private async Task ImportSelectedAsync(
        IReadOnlyList<FileImportItem> selected,
        bool preserveCounters = false)
    {
        if (IsImporting)
            return;

        if (selected.Count == 0)
        {
            if (!preserveCounters)
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Import_NoFileSelected"]);
            return;
        }

        selected = selected
            .GroupBy(file => file.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        IsImporting = true;
        IsImportCancelled = false;
        if (!preserveCounters)
        {
            foreach (var item in selected)
            {
                item.IsFailed = false;
                item.FailureCode = BatchImportFailureCode.None;
                item.FailureReason = string.Empty;
            }

            NotifyFailureStateChanged();
            ImportedCount = 0;
            SkippedDuplicateCount = 0;
        }

        FailedCount = Files.Count(file => file.IsFailed);
        _operationProgress.Start(selected.Count);
        NotifyOperationStateChanged();
        ProcessedCount = 0;
        TotalCount = selected.Count;
        ImportErrorMessage = string.Empty;
        ImportStatusMessage = _loc["BatchImport_StatusImporting"];
        var cancellation = new CancellationTokenSource();
        _importCancellation = cancellation;
        try
        {
            var defaultSubject = DefaultSubject.Trim();
            var inboxIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in selected)
            {
                var pending = new ImportInboxItem { SourcePath = item.FilePath, DisplayName = item.FileName, Subject = defaultSubject, Type = item.FileType, State = ImportInboxState.Pending };
                var persistCode = PersistInboxSafe(pending);
                if (persistCode != BatchImportFailureCode.None)
                {
                    item.IsFailed = true;
                    item.FailureCode = persistCode;
                    item.FailureReason = GetFailureReason(persistCode);
                    FailedCount = Files.Count(file => file.IsFailed);
                    _operationProgress.RecordFailure(item.FilePath);
                    continue;
                }

                if (pending.Id > 0)
                    inboxIds[item.FilePath] = pending.Id;
            }

            foreach (var item in selected)
            {
                if (cancellation?.IsCancellationRequested == true)
                {
                    _operationProgress.Cancel();
                    IsImportCancelled = true;
                    break;
                }

                if (item.IsFailed && !preserveCounters)
                    continue;

                var document = new StudyDocument
                {
                    Name = item.FileName,
                    Subject = defaultSubject,
                    Type = item.FileType,
                    FilePath = item.FilePath,
                    FileSize = item.FileSizeMB
                };

                DocumentImportOutcome outcome;
                var failureCode = BatchImportFailureCode.ImportFailed;
                try
                {
                    outcome = await Task.Run(
                        () => _droppedFileImportService.SaveDocument(document),
                        cancellation?.Token ?? CancellationToken.None);
                }
                catch (OperationCanceledException) when (cancellation?.IsCancellationRequested == true)
                {
                    _operationProgress.Cancel();
                    IsImportCancelled = true;
                    break;
                }
                catch (UnauthorizedAccessException)
                {
                    outcome = DocumentImportOutcome.Failed;
                    failureCode = BatchImportFailureCode.PermissionError;
                }
                catch (IOException)
                {
                    outcome = DocumentImportOutcome.Failed;
                    failureCode = BatchImportFailureCode.FileError;
                }
                catch (SqliteException)
                {
                    outcome = DocumentImportOutcome.Failed;
                    failureCode = BatchImportFailureCode.DatabaseError;
                }
                catch (Exception)
                {
                    outcome = DocumentImportOutcome.Failed;
                    failureCode = BatchImportFailureCode.ImportFailed;
                }

                switch (outcome)
                {
                    case DocumentImportOutcome.Imported:
                        ImportedCount++;
                        var ambiguousMatches = _droppedFileImportService
                            .FindExistingByName(document?.Name ?? string.Empty)
                            .Where(match => match.Id != (document?.Id ?? 0))
                            .ToList();
                        if (ambiguousMatches.Count >= 2)
                        {
                            var candidateList = string.Join("|", ambiguousMatches.Select(match => $"{match.Id}:{match.Name}"));
                            UpdateInboxOutcome(inboxIds, item, document, ImportInboxState.Ambiguous, candidateList);
                        }
                        else
                        {
                            var missingMetadata = string.IsNullOrWhiteSpace(document?.Subject) || string.IsNullOrWhiteSpace(document?.Type);
                            UpdateInboxOutcome(inboxIds, item, document, missingMetadata ? ImportInboxState.MissingMetadata : ImportInboxState.Processed, null);
                        }
                        item.IsSelected = false;
                        item.IsFailed = false;
                        item.FailureCode = BatchImportFailureCode.None;
                        item.FailureReason = string.Empty;
                        _operationProgress.RecordSuccess(item.FilePath);
                        break;
                    case DocumentImportOutcome.SkippedDuplicate:
                        SkippedDuplicateCount++;
                        var existingDoc = _droppedFileImportService.FindExistingByFilePath(item.FilePath);
                        var duplicateCandidate = existingDoc is not null ? $"{existingDoc.Id}:{existingDoc.Name}" : item.FilePath;
                        UpdateInboxOutcome(inboxIds, item, null, ImportInboxState.Held, duplicateCandidate);
                        item.IsSelected = false;
                        item.IsFailed = false;
                        item.FailureCode = BatchImportFailureCode.None;
                        item.FailureReason = string.Empty;
                        _operationProgress.RecordSkipped(item.FilePath);
                        break;
                    case DocumentImportOutcome.Failed:
                        UpdateInboxOutcome(inboxIds, item, null, ImportInboxState.Failed, null, failureCode.ToString());
                        item.IsFailed = true;
                        item.FailureCode = failureCode;
                        item.FailureReason = GetFailureReason(item.FailureCode);
                        _operationProgress.RecordFailure(item.FilePath);
                        break;
                }

                FailedCount = Files.Count(file => file.IsFailed);
                ProcessedCount = _operationProgress.Processed;
                NotifyOperationStateChanged();
            }

            if (cancellation?.IsCancellationRequested == true)
            {
                _operationProgress.Cancel();
                IsImportCancelled = true;
            }
        }
        finally
        {
            _operationProgress.Stop();
            IsImporting = false;
            cancellation.Dispose();
            if (ReferenceEquals(_importCancellation, cancellation))
                _importCancellation = null;
        }

        if (IsImportCancelled)
        {
            ImportStatusMessage = string.Format(
                _loc["BatchImport_Cancelled"],
                ProcessedCount,
                TotalCount);
            ImportErrorMessage = string.Empty;
            return;
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

    private BatchImportFailureCode PersistInboxSafe(ImportInboxItem item)
    {
        try
        {
            _importInboxRepository?.Add(item);
            return BatchImportFailureCode.None;
        }
        catch (SqliteException) { return BatchImportFailureCode.DatabaseError; }
        catch (IOException) { return BatchImportFailureCode.FileError; }
    }

    private void UpdateInboxOutcome(Dictionary<string, int> inboxIds, FileImportItem item, StudyDocument? document, ImportInboxState state, string? duplicateCandidate, string? failureCode = null)
    {
        if (_importInboxRepository is null || !inboxIds.TryGetValue(item.FilePath, out var id)) return;
        var pending = _importInboxRepository.GetById(id);
        if (pending is null) return;
        pending.DocumentId = document?.Id;
        pending.Subject = document?.Subject ?? pending.Subject;
        pending.Type = document?.Type ?? pending.Type;
        pending.DuplicateCandidate = duplicateCandidate ?? pending.DuplicateCandidate;
        pending.FailureCode = failureCode;
        pending.State = state;
        _importInboxRepository.Update(pending);
    }

    private void NotifyFailureStateChanged()
    {
        OnPropertyChanged(nameof(FailedItems));
        OnPropertyChanged(nameof(HasFailedItems));
    }

    private void NotifyOperationStateChanged()
    {
        NotifyFailureStateChanged();
        OnPropertyChanged(nameof(SucceededCount));
        OnPropertyChanged(nameof(SkippedCount));
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
    private void Cancel()
    {
        if (IsImporting)
        {
            _importCancellation?.Cancel();
            return;
        }

        _navigationService.NavigateTo("dashboard");
    }
}

public partial class FileImportItem : ObservableObject
{
    [ObservableProperty] private string _fileName = string.Empty;
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _fileType = string.Empty;
    [ObservableProperty] private double _fileSizeMB;
    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private bool _isFailed;
    [ObservableProperty] private BatchImportFailureCode _failureCode;
    [ObservableProperty] private string _failureReason = string.Empty;
}
