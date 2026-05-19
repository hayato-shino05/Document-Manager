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

    [ObservableProperty] private ObservableCollection<StudyDocument> _deletedDocuments = new();
    [ObservableProperty] private StudyDocument? _selectedDocument;

    public RecycleBinModel(IDocument docRepo, IDialogService dialogService)
    {
        _docRepo = docRepo;
        _dialogService = dialogService;
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

        var confirmed = await _dialogService.ShowConfirmAsync("Khôi phục",
            $"Khôi phục tài liệu '{SelectedDocument.Name}'?");
        if (confirmed)
        {
            _docRepo.RestoreDocument(SelectedDocument.Id);
            LoadData();
            await _dialogService.ShowMessageAsync("Thành công", "Đã khôi phục tài liệu!");
        }
    }

    [RelayCommand]
    private async Task PermanentDeleteAsync()
    {
        if (SelectedDocument == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync("Xóa vĩnh viễn",
            $"Xóa vĩnh viễn '{SelectedDocument.Name}'? Hành động này KHÔNG THỂ hoàn tác!",
            "Xoá vĩnh viễn", isDanger: true);
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

        var confirmed = await _dialogService.ShowConfirmAsync("Xóa tất cả",
            $"Xóa vĩnh viễn {DeletedDocuments.Count} tài liệu? KHÔNG THỂ hoàn tác!",
            "Xoá tất cả", isDanger: true);
        if (confirmed)
        {
            int count = _docRepo.EmptyRecycleBin();
            LoadData();
            await _dialogService.ShowMessageAsync("Hoàn tất", $"Đã xóa vĩnh viễn {count} tài liệu.");
        }
    }

    [RelayCommand]
    private void Refresh() => LoadData();
}
