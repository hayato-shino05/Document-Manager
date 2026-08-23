using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class FileIntegrityCheckModel : ModelBase
{
    private readonly IDocumentRepository _repository;
    private readonly IFileIntegrityRepository _fileIntegrityRepo;
    private readonly IDialogService _dialogService;
    private readonly IFileDialogService _fileDialogService;
    private readonly ILocalizationService _loc;
    private readonly Action<int>? _scanProgress;
    private CancellationTokenSource? _checkCancellation;
    private string _statusKey = "Status_ScanPrompt";
    private object[] _statusArguments = [];

    [ObservableProperty] private ObservableCollection<IntegrityResult> _results = new();
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private bool _isCheckCancelled;
    [ObservableProperty] private int _totalChecked;
    [ObservableProperty] private int _missingCount;
    [ObservableProperty] private string _statusText = string.Empty;

    public FileIntegrityCheckModel(IDocumentRepository repository, IFileIntegrityRepository fileIntegrityRepo, IDialogService dialogService, IFileDialogService fileDialogService, ILocalizationService loc, Action<int>? scanProgress = null)
    {
        _repository = repository;
        _fileIntegrityRepo = fileIntegrityRepo;
        _dialogService = dialogService;
        _fileDialogService = fileDialogService;
        _loc = loc;
        _scanProgress = scanProgress;
        _loc.LanguageChanged += (_, _) => RefreshLocalizedStrings();
        SetLocalizedStatus("Status_ScanPrompt");
    }

    [RelayCommand]
    private async Task CheckIntegrityAsync()
    {
        if (IsChecking)
            return;

        IsChecking = true;
        IsCheckCancelled = false;
        Results.Clear();
        TotalChecked = 0;
        MissingCount = 0;
        _checkCancellation?.Dispose();
        _checkCancellation = new CancellationTokenSource();
        var cancellation = _checkCancellation;

        try
        {
            var scan = await Task.Run(() => ScanDocuments(_repository.GetAll(), cancellation.Token), cancellation.Token);
            TotalChecked = scan.Processed;
            foreach (var document in scan.MissingDocuments)
                Results.Add(CreateMissingResult(document));
            MissingCount = Results.Count;

            if (scan.IsCancelled)
            {
                IsCheckCancelled = true;
                SetLocalizedStatus("FileIntegrity_Cancelled", TotalChecked, scan.Total);
            }
            else
            {
                SetLocalizedStatus("Status_ScanComplete", MissingCount, TotalChecked);
                if (MissingCount == 0)
                {
                    await _dialogService.ShowMessageAsync(_loc["Dialog_Result"],
                        string.Format(_loc["Integrity_AllFilesOk"], TotalChecked));
                }
            }
        }
        catch (OperationCanceledException)
        {
            IsCheckCancelled = true;
            SetLocalizedStatus("FileIntegrity_Cancelled", TotalChecked, TotalChecked);
        }
        catch (Exception)
        {
            Results.Clear();
            TotalChecked = 0;
            MissingCount = 0;
            SetLocalizedStatus("Status_ScanPrompt");
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
        finally
        {
            IsChecking = false;
            if (ReferenceEquals(_checkCancellation, cancellation))
            {
                _checkCancellation.Dispose();
                _checkCancellation = null;
            }
        }
    }

    [RelayCommand]
    private void CancelCheck()
    {
        if (!IsChecking)
            return;

        _checkCancellation?.Cancel();
    }

    private ScanResult ScanDocuments(IReadOnlyList<StudyDocument> documents, CancellationToken cancellationToken)
    {
        var missingDocuments = new List<StudyDocument>();
        var processed = 0;

        foreach (var document in documents)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (!string.IsNullOrEmpty(document.FilePath) && !File.Exists(document.FilePath))
                missingDocuments.Add(document);

            processed++;
            _scanProgress?.Invoke(processed);
        }

        return new ScanResult(processed, documents.Count, missingDocuments, cancellationToken.IsCancellationRequested);
    }

    private IntegrityResult CreateMissingResult(StudyDocument document)
        => new()
        {
            Document = document,
            FilePath = document.FilePath,
            StatusKey = "Integrity_FileNotExist",
            Status = _loc["Integrity_FileNotExist"]
        };

    [RelayCommand]
    private async Task RetryMissingAsync()
    {
        if (IsChecking || Results.Count == 0)
            return;

        var documents = Results.Select(result => result.Document).ToList();
        IsChecking = true;
        IsCheckCancelled = false;
        TotalChecked = 0;
        MissingCount = 0;
        _checkCancellation?.Dispose();
        _checkCancellation = new CancellationTokenSource();
        var cancellation = _checkCancellation;

        try
        {
            var scan = await Task.Run(() => ScanDocuments(documents, cancellation.Token), cancellation.Token);
            Results.Clear();
            TotalChecked = scan.Processed;
            foreach (var document in scan.MissingDocuments)
                Results.Add(CreateMissingResult(document));
            if (scan.IsCancelled)
            {
                foreach (var document in documents.Skip(scan.Processed))
                    Results.Add(CreateMissingResult(document));
            }
            MissingCount = Results.Count;

            if (scan.IsCancelled)
            {
                IsCheckCancelled = true;
                SetLocalizedStatus("FileIntegrity_Cancelled", TotalChecked, scan.Total);
            }
            else
            {
                SetLocalizedStatus("Status_MissingFiles", MissingCount);
            }
        }
        catch (OperationCanceledException)
        {
            IsCheckCancelled = true;
            MissingCount = Results.Count;
            SetLocalizedStatus("FileIntegrity_Cancelled", TotalChecked, documents.Count);
        }
        catch (Exception)
        {
            SetLocalizedStatus("Status_MissingFiles", MissingCount);
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
        finally
        {
            IsChecking = false;
            if (ReferenceEquals(_checkCancellation, cancellation))
            {
                _checkCancellation.Dispose();
                _checkCancellation = null;
            }
        }
    }

    /// <summary>
    /// Select a new file to replace the missing one - updates path in DB.
    /// </summary>
    [RelayCommand]
    private async Task SelectNewFileAsync(IntegrityResult? item)
    {
        if (IsChecking || item == null) return;

        var snapshot = CaptureRemovalSnapshot(item);
        try
        {
            var newPath = await _fileDialogService.ShowOpenFileAsync(
                _loc["Integrity_SelectNewFile"], _loc["Integrity_FileFilter"]);
            if (IsChecking || string.IsNullOrWhiteSpace(newPath) || !IsRemovalSnapshotCurrent(snapshot)) return;

            if (!_fileIntegrityRepo.UpdateDocumentPath(snapshot.DocumentId, newPath))
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                return;
            }

            Results.Remove(item);
            MissingCount--;
            SetLocalizedStatus("Status_MissingFiles", MissingCount);
            await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], _loc["Integrity_PathUpdated"]);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    /// <summary>
    /// Clear the file path but keep metadata.
    /// </summary>
    [RelayCommand]
    private async Task ClearFilePathAsync(IntegrityResult? item)
    {
        if (IsChecking || item == null) return;

        var snapshot = CaptureRemovalSnapshot(item);
        try
        {
            var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
                _loc["Integrity_ConfirmClearPath"]);
            if (IsChecking || !confirmed || !IsRemovalSnapshotCurrent(snapshot)) return;

            if (!_fileIntegrityRepo.ClearDocumentPath(snapshot.DocumentId))
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                return;
            }

            Results.Remove(item);
            MissingCount--;
            SetLocalizedStatus("Status_MissingFiles", MissingCount);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    /// <summary>
    /// Soft-delete a single document with missing file.
    /// </summary>
    [RelayCommand]
    private async Task DeleteDocumentAsync(IntegrityResult? item)
    {
        if (IsChecking || item == null) return;

        var snapshot = CaptureRemovalSnapshot(item);
        try
        {
            var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
                string.Format(_loc["Integrity_ConfirmDeleteDoc"], snapshot.Document.Name),
                _loc["Action_Delete"], isDanger: true);
            if (IsChecking || !confirmed || !IsRemovalSnapshotCurrent(snapshot)) return;

            if (!_repository.Delete(snapshot.DocumentId))
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                return;
            }

            Results.Remove(item);
            MissingCount--;
            SetLocalizedStatus("Status_MissingFiles", MissingCount);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    /// <summary>
    /// Remove ALL documents with missing files (bulk soft-delete).
    /// </summary>
    [RelayCommand]
    private async Task RemoveMissingAsync()
    {
        if (IsChecking || Results.Count == 0) return;

        var snapshot = Results
            .Select(result => new RemovalSnapshot(
                result,
                result.Document,
                result.Document.Id,
                result.Document.FilePath,
                result.FilePath))
            .ToArray();
        var total = snapshot.Length;
        var removed = 0;
        try
        {
            var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
                string.Format(_loc["Integrity_ConfirmRemoveAll"], total),
                _loc["Btn_DeleteAll"], isDanger: true);
            if (IsChecking || !confirmed || !IsRemovalSnapshotCurrent(snapshot)) return;

            foreach (var entry in snapshot)
            {
                if (_repository.Delete(entry.DocumentId))
                {
                    removed++;
                    Results.Remove(entry.Result);
                }
            }

            MissingCount = Results.Count;
            if (removed != 0 && Results.Count == 0)
            {
                SetLocalizedStatus("Integrity_MovedToTrash", removed);
                await _dialogService.ShowMessageAsync(_loc["Dialog_Complete"],
                    string.Format(_loc["Integrity_MovedToTrash"], removed));
                return;
            }

            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"],
                string.Format(_loc["Operation_Partial"], removed, total));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            MissingCount = Results.Count;
            SetLocalizedStatus("Status_MissingFiles", MissingCount);
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"],
                removed == 0 ? _loc["Msg_Error"] : string.Format(_loc["Operation_Partial"], removed, total));
        }
    }

    private static RemovalSnapshot CaptureRemovalSnapshot(IntegrityResult result)
        => new(result, result.Document, result.Document.Id, result.Document.FilePath, result.FilePath);

    private bool IsRemovalSnapshotCurrent(RemovalSnapshot snapshot)
        => Results.Contains(snapshot.Result) && IsRemovalSnapshotMatch(snapshot.Result, snapshot);

    private bool IsRemovalSnapshotCurrent(IReadOnlyList<RemovalSnapshot> snapshot)
    {
        if (snapshot.Count != Results.Count)
            return false;

        for (var index = 0; index < snapshot.Count; index++)
        {
            if (!IsRemovalSnapshotMatch(Results[index], snapshot[index]))
                return false;
        }

        return true;
    }

    private static bool IsRemovalSnapshotMatch(IntegrityResult result, RemovalSnapshot snapshot)
        => ReferenceEquals(result, snapshot.Result) &&
            ReferenceEquals(result.Document, snapshot.Document) &&
            result.Document.Id == snapshot.DocumentId &&
            string.Equals(result.Document.FilePath, snapshot.DocumentFilePath, StringComparison.Ordinal) &&
            string.Equals(result.FilePath, snapshot.ResultFilePath, StringComparison.Ordinal);

    private void RefreshLocalizedStrings()
    {
        StatusText = FormatLocalized(_statusKey, _statusArguments);
        foreach (var result in Results)
            result.Status = FormatLocalized(result.StatusKey, result.StatusArguments);
    }

    private void SetLocalizedStatus(string key, params object[] args)
    {
        _statusKey = key;
        _statusArguments = args;
        StatusText = FormatLocalized(key, args);
    }

    private string FormatLocalized(string key, params object[] args)
        => args.Length == 0 ? _loc[key] : string.Format(_loc[key], args);

    private sealed record RemovalSnapshot(
        IntegrityResult Result,
        StudyDocument Document,
        int DocumentId,
        string? DocumentFilePath,
        string ResultFilePath);

    private sealed record ScanResult(
        int Processed,
        int Total,
        IReadOnlyList<StudyDocument> MissingDocuments,
        bool IsCancelled);
}

public partial class IntegrityResult : ObservableObject
{
    public StudyDocument Document { get; set; } = new();
    public string FilePath { get; set; } = string.Empty;
    public string StatusKey { get; set; } = string.Empty;
    public object[] StatusArguments { get; set; } = [];
    [ObservableProperty] private string _status = string.Empty;
}
