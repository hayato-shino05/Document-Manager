using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class PersonalNoteModel : ModelBase
{
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    [ObservableProperty] private int _documentId;
    [ObservableProperty] private string _documentName = string.Empty;
    [ObservableProperty] private string _noteContent = string.Empty;
    [ObservableProperty] private bool _hasExistingNote;

    public PersonalNoteModel(IDialogService dialogService, INavigationService navigationService)
    {
        _dialogService = dialogService;
        _navigationService = navigationService;
    }

    public void Load(int docId, string docName)
    {
        DocumentId = docId;
        DocumentName = docName;
        var note = DatabaseHelper.GetPersonalNote(docId);
        NoteContent = note ?? string.Empty;
        HasExistingNote = note != null;
    }

    [RelayCommand]
    private async Task SaveNoteAsync()
    {
        if (DatabaseHelper.SavePersonalNote(DocumentId, NoteContent))
        {
            await _dialogService.ShowMessageAsync("Thành công", "Đã lưu ghi chú!");
            HasExistingNote = true;
        }
        else
        {
            await _dialogService.ShowErrorAsync("Lỗi", "Không thể lưu ghi chú.");
        }
    }

    [RelayCommand]
    private async Task DeleteNoteAsync()
    {
        if (!HasExistingNote) return;

        var confirmed = await _dialogService.ShowConfirmAsync("Xác nhận", "Xóa ghi chú này?");
        if (!confirmed) return;

        if (DatabaseHelper.DeletePersonalNote(DocumentId))
        {
            NoteContent = string.Empty;
            HasExistingNote = false;
            await _dialogService.ShowMessageAsync("Đã xóa", "Ghi chú đã được xóa.");
        }
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();
}
