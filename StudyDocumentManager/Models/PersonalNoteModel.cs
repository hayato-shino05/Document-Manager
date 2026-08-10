using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class PersonalNoteModel : ModelBase
{
    private readonly IPersonalNoteRepository _noteRepo;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _loc;

    [ObservableProperty] private int _documentId;
    [ObservableProperty] private string _documentName = string.Empty;
    [ObservableProperty] private string _noteContent = string.Empty;
    [ObservableProperty] private string _savedNoteContent = string.Empty;
    [ObservableProperty] private bool _hasExistingNote;

    public bool CanSaveNote => !string.IsNullOrWhiteSpace(NoteContent);
    public bool HasSavedNotePreview => HasExistingNote && !string.IsNullOrWhiteSpace(SavedNoteContent);

    public PersonalNoteModel(IPersonalNoteRepository noteRepo, IDialogService dialogService, INavigationService navigationService, ILocalizationService loc)
    {
        _noteRepo = noteRepo;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _loc = loc;
    }

    public void Load(int docId, string docName)
    {
        DocumentId = docId;
        DocumentName = docName;
        var note = _noteRepo.GetNote(docId);
        NoteContent = note ?? string.Empty;
        SavedNoteContent = note ?? string.Empty;
        HasExistingNote = note != null;
    }

    partial void OnNoteContentChanged(string value)
        => OnPropertyChanged(nameof(CanSaveNote));

    partial void OnSavedNoteContentChanged(string value)
        => OnPropertyChanged(nameof(HasSavedNotePreview));

    partial void OnHasExistingNoteChanged(bool value)
        => OnPropertyChanged(nameof(HasSavedNotePreview));

    [RelayCommand]
    private async Task SaveNoteAsync()
    {
        var content = NoteContent.Trim();
        if (content.Length == 0)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Note_SaveError"]);
            return;
        }

        NoteContent = content;
        if (!_noteRepo.SaveNote(DocumentId, content))
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Note_SaveError"]);
            return;
        }

        var persistedContent = _noteRepo.GetNote(DocumentId);
        if (!string.Equals(persistedContent, content, StringComparison.Ordinal))
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Note_SaveError"]);
            return;
        }

        SavedNoteContent = content;
        HasExistingNote = true;
        await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], _loc["Note_SaveSuccess"]);
    }

    [RelayCommand]
    private async Task DeleteNoteAsync()
    {
        if (!HasExistingNote) return;

        var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"], _loc["Note_ConfirmDelete"]);
        if (!confirmed) return;

        if (_noteRepo.DeleteNote(DocumentId))
        {
            NoteContent = string.Empty;
            SavedNoteContent = string.Empty;
            HasExistingNote = false;
            await _dialogService.ShowMessageAsync(_loc["Dialog_Deleted"], _loc["Note_DeleteSuccess"]);
        }
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();
}
