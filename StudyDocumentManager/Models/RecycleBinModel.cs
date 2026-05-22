using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class RecycleBinModel : ModelBase
{
    private readonly IDocument _docRepo;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _loc;

    [ObservableProperty] private ObservableCollection<StudyDocument> _deletedDocuments = new();
    [ObservableProperty] private StudyDocument? _selectedDocument;

    public RecycleBinModel(IDocument docRepo, IDialogService dialogService, ILocalizationService loc)
    {
        _docRepo = docRepo;
        _dialogService = dialogService;
        _loc = loc;
        LoadData();
    }

    private void LoadData()
    {
        var docs = _docRepo.GetDeletedDocuments();
        DeletedDocuments = new ObservableCollection<StudyDocument>(docs);
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (SelectedDocument == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
            string.Format(_loc["Recycle_ConfirmRestore"], SelectedDocument.Name));
        if (confirmed)
        {
            _docRepo.RestoreDocument(SelectedDocument.Id);
            LoadData();
            await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], _loc["Recycle_RestoreSuccess"]);
        }
    }

    [RelayCommand]
    private async Task PermanentDeleteAsync()
    {
        if (SelectedDocument == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
            string.Format(_loc["Recycle_ConfirmPermanentDelete"], SelectedDocument.Name),
            _loc["Btn_PermanentDelete"], isDanger: true);
        if (confirmed)
        {
            _docRepo.PermanentDeleteDocument(SelectedDocument.Id);
            LoadData();
        }
    }

    [RelayCommand]
    private async Task EmptyTrashAsync()
    {
        if (DeletedDocuments.Count == 0) return;

        var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
            string.Format(_loc["Recycle_ConfirmEmptyTrash"], DeletedDocuments.Count),
            _loc["Btn_DeleteAll"], isDanger: true);
        if (confirmed)
        {
            int count = _docRepo.EmptyRecycleBin();
            LoadData();
            await _dialogService.ShowMessageAsync(_loc["Dialog_Complete"],
                string.Format(_loc["Recycle_EmptyTrashDone"], count));
        }
    }

    [RelayCommand]
    private void Refresh() => LoadData();
}
