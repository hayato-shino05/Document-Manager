using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class FileIntegrityCheckModel : ModelBase
{
    private readonly IDocument _repository;
    private readonly IDialogService _dialogService;
    private readonly IFileDialogService _fileDialogService;
    private readonly ILocalizationService _loc;

    [ObservableProperty] private ObservableCollection<IntegrityResult> _results = new();
    [ObservableProperty] private bool _isChecking;
    [ObservableProperty] private int _totalChecked;
    [ObservableProperty] private int _missingCount;
    [ObservableProperty] private string _statusText = string.Empty;

    public FileIntegrityCheckModel(IDocument repository, IDialogService dialogService, IFileDialogService fileDialogService, ILocalizationService loc)
    {
        _repository = repository;
        _dialogService = dialogService;
        _fileDialogService = fileDialogService;
        _loc = loc;
        _statusText = _loc["Status_ScanPrompt"];
    }

    [RelayCommand]
    private async Task CheckIntegrityAsync()
    {
        IsChecking = true;
        Results.Clear();
        TotalChecked = 0;
        MissingCount = 0;

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
                    Status = _loc["Integrity_FileNotExist"]
                });
            }
        }

        IsChecking = false;
        StatusText = string.Format(_loc["Status_ScanComplete"], MissingCount, TotalChecked);

        if (MissingCount == 0)
        {
            await _dialogService.ShowMessageAsync(_loc["Dialog_Result"],
                string.Format(_loc["Integrity_AllFilesOk"], TotalChecked));
        }
    }

    /// <summary>
    /// Select a new file to replace the missing one - updates path in DB.
    /// </summary>
    [RelayCommand]
    private async Task SelectNewFileAsync(IntegrityResult? item)
    {
        if (item == null) return;

        var newPath = await _fileDialogService.ShowOpenFileAsync(
            _loc["Integrity_SelectNewFile"], _loc["Integrity_FileFilter"]);
        if (string.IsNullOrWhiteSpace(newPath)) return;

        if (_repository.UpdateDocumentPath(item.Document.Id, newPath))
        {
            Results.Remove(item);
            MissingCount--;
            StatusText = string.Format(_loc["Status_MissingFiles"], MissingCount);
            await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], _loc["Integrity_PathUpdated"]);
        }
    }

    /// <summary>
    /// Clear the file path but keep metadata.
    /// </summary>
    [RelayCommand]
    private async Task ClearFilePathAsync(IntegrityResult? item)
    {
        if (item == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
            _loc["Integrity_ConfirmClearPath"]);
        if (!confirmed) return;

        if (_repository.ClearDocumentPath(item.Document.Id))
        {
            Results.Remove(item);
            MissingCount--;
            StatusText = string.Format(_loc["Status_MissingFiles"], MissingCount);
        }
    }

    /// <summary>
    /// Soft-delete a single document with missing file.
    /// </summary>
    [RelayCommand]
    private async Task DeleteDocumentAsync(IntegrityResult? item)
    {
        if (item == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
            string.Format(_loc["Integrity_ConfirmDeleteDoc"], item.Document.Name),
            _loc["Action_Delete"], isDanger: true);
        if (!confirmed) return;

        if (_repository.Delete(item.Document.Id))
        {
            Results.Remove(item);
            MissingCount--;
            StatusText = string.Format(_loc["Status_MissingFiles"], MissingCount);
        }
    }

    /// <summary>
    /// Remove ALL documents with missing files (bulk soft-delete).
    /// </summary>
    [RelayCommand]
    private async Task RemoveMissingAsync()
    {
        if (Results.Count == 0) return;

        var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
            string.Format(_loc["Integrity_ConfirmRemoveAll"], Results.Count),
            _loc["Btn_DeleteAll"], isDanger: true);
        if (!confirmed) return;

        int removed = 0;
        foreach (var result in Results.ToList())
        {
            if (_repository.Delete(result.Document.Id))
            {
                removed++;
                Results.Remove(result);
            }
        }

        MissingCount = Results.Count;
        StatusText = string.Format(_loc["Integrity_MovedToTrash"], removed);
        await _dialogService.ShowMessageAsync(_loc["Dialog_Complete"],
            string.Format(_loc["Integrity_MovedToTrash"], removed));
    }
}

public class IntegrityResult
{
    public StudyDocument Document { get; set; } = new();
    public string FilePath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
