using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class RecycleBinModel : ModelBase
{
    private readonly IRecycleBinRepository _docRepo;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _loc;

    [ObservableProperty] private ObservableCollection<StudyDocument> _deletedDocuments = new();
    [ObservableProperty] private StudyDocument? _selectedDocument;

    public bool HasSelection => SelectedDocument != null;
    public bool HasDeletedDocuments => DeletedDocuments.Count > 0;

    public RecycleBinModel(IRecycleBinRepository docRepo, IDialogService dialogService, ILocalizationService loc)
    {
        _docRepo = docRepo;
        _dialogService = dialogService;
        _loc = loc;
        LoadData();
    }

    private void LoadData()
    {
        SelectedDocument = null;
        var docs = _docRepo.GetDeletedDocuments();
        DeletedDocuments = new ObservableCollection<StudyDocument>(docs);
        OnPropertyChanged(nameof(HasDeletedDocuments));
    }

    partial void OnSelectedDocumentChanged(StudyDocument? value)
        => OnPropertyChanged(nameof(HasSelection));

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (SelectedDocument == null) return;

        var selectedDocumentId = SelectedDocument.Id;
        try
        {
            var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
                string.Format(_loc["Recycle_ConfirmRestore"], SelectedDocument.Name));
            if (!confirmed) return;

            if (!_docRepo.RestoreDocument(selectedDocumentId))
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Recycle_RestoreError"]);
                return;
            }

            LoadData();
            await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], _loc["Recycle_RestoreSuccess"]);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            LoadData();
            SelectedDocument = DeletedDocuments.FirstOrDefault(document => document.Id == selectedDocumentId);
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private async Task PermanentDeleteAsync()
    {
        if (SelectedDocument == null) return;

        var selectedDocumentId = SelectedDocument.Id;
        try
        {
            var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
                string.Format(_loc["Recycle_ConfirmPermanentDelete"], SelectedDocument.Name),
                _loc["Btn_PermanentDelete"], isDanger: true);
            if (!confirmed) return;

            if (!_docRepo.PermanentDeleteDocument(selectedDocumentId))
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Recycle_PermanentDeleteError"]);
                return;
            }

            LoadData();
            await _dialogService.ShowMessageAsync(_loc["Dialog_Complete"], _loc["Recycle_PermanentDeleteSuccess"]);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            LoadData();
            SelectedDocument = DeletedDocuments.FirstOrDefault(document => document.Id == selectedDocumentId);
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private async Task EmptyTrashAsync()
    {
        if (DeletedDocuments.Count == 0) return;

        try
        {
            var deletedDocumentCount = DeletedDocuments.Count;
            var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
                string.Format(_loc["Recycle_ConfirmEmptyTrash"], deletedDocumentCount),
                _loc["Btn_DeleteAll"], isDanger: true);
            if (!confirmed) return;

            var count = _docRepo.EmptyRecycleBin();
            if (count != deletedDocumentCount)
            {
                LoadData();
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"],
                    string.Format(_loc["Operation_Partial"], count, deletedDocumentCount));
                return;
            }

            LoadData();
            await _dialogService.ShowMessageAsync(_loc["Dialog_Complete"],
                string.Format(_loc["Recycle_EmptyTrashDone"], count));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            LoadData();
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private void Refresh() => LoadData();
}
