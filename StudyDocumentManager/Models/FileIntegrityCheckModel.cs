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
    private string _statusKey = "Status_ScanPrompt";
    private object[] _statusArguments = [];

    [ObservableProperty] private ObservableCollection<IntegrityResult> _results = new();
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private int _totalChecked;
    [ObservableProperty] private int _missingCount;
    [ObservableProperty] private string _statusText = string.Empty;

    public FileIntegrityCheckModel(IDocumentRepository repository, IFileIntegrityRepository fileIntegrityRepo, IDialogService dialogService, IFileDialogService fileDialogService, ILocalizationService loc)
    {
        _repository = repository;
        _fileIntegrityRepo = fileIntegrityRepo;
        _dialogService = dialogService;
        _fileDialogService = fileDialogService;
        _loc = loc;
        _loc.LanguageChanged += (_, _) => RefreshLocalizedStrings();
        SetLocalizedStatus("Status_ScanPrompt");
    }

    [RelayCommand]
    private async Task CheckIntegrityAsync()
    {
        IsChecking = true;
        Results.Clear();
        TotalChecked = 0;
        MissingCount = 0;

        try
        {
            var docs = _repository.GetAll();
            foreach (var doc in docs)
            {
                TotalChecked++;
                if (!string.IsNullOrEmpty(doc.FilePath) && !File.Exists(doc.FilePath))
                {
                    MissingCount++;
                    Results.Add(new IntegrityResult
                    {
                        Document = doc,
                        FilePath = doc.FilePath,
                        StatusKey = "Integrity_FileNotExist",
                        Status = _loc["Integrity_FileNotExist"]
                    });
                }
            }

            SetLocalizedStatus("Status_ScanComplete", MissingCount, TotalChecked);

            if (MissingCount == 0)
            {
                await _dialogService.ShowMessageAsync(_loc["Dialog_Result"],
                    string.Format(_loc["Integrity_AllFilesOk"], TotalChecked));
            }
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
        }
    }

    /// <summary>
    /// Select a new file to replace the missing one - updates path in DB.
    /// </summary>
    [RelayCommand]
    private async Task SelectNewFileAsync(IntegrityResult? item)
    {
        if (item == null) return;

        try
        {
            var newPath = await _fileDialogService.ShowOpenFileAsync(
                _loc["Integrity_SelectNewFile"], _loc["Integrity_FileFilter"]);
            if (string.IsNullOrWhiteSpace(newPath)) return;

            if (!_fileIntegrityRepo.UpdateDocumentPath(item.Document.Id, newPath))
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
        if (item == null) return;

        try
        {
            var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
                _loc["Integrity_ConfirmClearPath"]);
            if (!confirmed) return;

            if (!_fileIntegrityRepo.ClearDocumentPath(item.Document.Id))
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
        if (item == null) return;

        try
        {
            var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
                string.Format(_loc["Integrity_ConfirmDeleteDoc"], item.Document.Name),
                _loc["Action_Delete"], isDanger: true);
            if (!confirmed) return;

            if (!_repository.Delete(item.Document.Id))
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
        if (Results.Count == 0) return;

        var total = Results.Count;
        var removed = 0;
        try
        {
            var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
                string.Format(_loc["Integrity_ConfirmRemoveAll"], total),
                _loc["Btn_DeleteAll"], isDanger: true);
            if (!confirmed) return;

            foreach (var result in Results.ToList())
            {
                if (_repository.Delete(result.Document.Id))
                {
                    removed++;
                    Results.Remove(result);
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
}

public partial class IntegrityResult : ObservableObject
{
    public StudyDocument Document { get; set; } = new();
    public string FilePath { get; set; } = string.Empty;
    public string StatusKey { get; set; } = string.Empty;
    public object[] StatusArguments { get; set; } = [];
    [ObservableProperty] private string _status = string.Empty;
}
