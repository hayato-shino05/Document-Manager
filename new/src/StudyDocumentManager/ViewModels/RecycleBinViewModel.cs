using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.ViewModels;

public partial class RecycleBinViewModel : ViewModelBase
{
    private readonly IDialogService _dialogService;

    [ObservableProperty] private ObservableCollection<StudyDocument> _deletedDocuments = new();
    [ObservableProperty] private StudyDocument? _selectedDocument;

    public RecycleBinViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
        LoadData();
    }

    private void LoadData()
    {
        var docs = DatabaseHelper.GetDeletedDocuments();
        DeletedDocuments = new ObservableCollection<StudyDocument>(docs);
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (SelectedDocument == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync("Khôi phục",
            $"Khôi phục tài liệu '{SelectedDocument.Ten}'?");
        if (confirmed)
        {
            DatabaseHelper.RestoreDocument(SelectedDocument.Id);
            LoadData();
            await _dialogService.ShowMessageAsync("Thành công", "Đã khôi phục tài liệu!");
        }
    }

    [RelayCommand]
    private async Task PermanentDeleteAsync()
    {
        if (SelectedDocument == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync("Xóa vĩnh viễn",
            $"Xóa vĩnh viễn '{SelectedDocument.Ten}'? Hành động này KHÔNG THỂ hoàn tác!");
        if (confirmed)
        {
            DatabaseHelper.PermanentDeleteDocument(SelectedDocument.Id);
            LoadData();
        }
    }

    [RelayCommand]
    private async Task EmptyTrashAsync()
    {
        if (DeletedDocuments.Count == 0) return;

        var confirmed = await _dialogService.ShowConfirmAsync("Xóa tất cả",
            $"Xóa vĩnh viễn {DeletedDocuments.Count} tài liệu? KHÔNG THỂ hoàn tác!");
        if (confirmed)
        {
            int count = DatabaseHelper.EmptyRecycleBin();
            LoadData();
            await _dialogService.ShowMessageAsync("Hoàn tất", $"Đã xóa vĩnh viễn {count} tài liệu.");
        }
    }

    [RelayCommand]
    private void Refresh() => LoadData();
}
