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
    private PersonalNote? _loadedNote;
    private bool _isRestoringSelection;

    [ObservableProperty] private int _documentId;
    [ObservableProperty] private string _documentName = string.Empty;
    [ObservableProperty] private string _noteContent = string.Empty;
    [ObservableProperty] private string _savedNoteContent = string.Empty;
    [ObservableProperty] private bool _hasExistingNote;
    [ObservableProperty] private PersonalNote? _selectedNote;
    [ObservableProperty] private string _selectedNoteType = "general";
    [ObservableProperty] private bool _isPinned;

    public ObservableCollection<PersonalNote> Notes { get; } = [];
    public IReadOnlyList<string> NoteTypes => NoteType.All;
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
        ReloadNotes();
    }

    partial void OnNoteContentChanged(string value)
        => OnPropertyChanged(nameof(CanSaveNote));

    partial void OnSavedNoteContentChanged(string value)
        => OnPropertyChanged(nameof(HasSavedNotePreview));

    partial void OnHasExistingNoteChanged(bool value)
        => OnPropertyChanged(nameof(HasSavedNotePreview));

    partial void OnSelectedNoteChanged(PersonalNote? value)
    {
        if (_isRestoringSelection)
            return;

        if (!string.Equals(NoteContent, SavedNoteContent, StringComparison.Ordinal))
        {
            _isRestoringSelection = true;
            SelectedNote = _loadedNote;
            _isRestoringSelection = false;
            return;
        }

        if (value is null)
        {
            NoteContent = string.Empty;
            SavedNoteContent = string.Empty;
            SelectedNoteType = "general";
            IsPinned = false;
            HasExistingNote = false;
            _loadedNote = null;
            return;
        }

        NoteContent = value.Content;
        SavedNoteContent = value.Content;
        SelectedNoteType = value.NoteType;
        IsPinned = value.IsPinned;
        HasExistingNote = true;
        _loadedNote = value;
    }

    [RelayCommand]
    private void NewNote()
    {
        if (!string.Equals(NoteContent, SavedNoteContent, StringComparison.Ordinal))
            return;

        SelectedNote = null;
    }

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
        var note = new PersonalNote(SelectedNote?.Id ?? 0, DocumentId, SelectedNoteType, content, IsPinned);
        if (!_noteRepo.SaveNote(note))
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Note_SaveError"]);
            return;
        }

        SavedNoteContent = content;
        HasExistingNote = true;
        ReloadNotes(note.Id, note.NoteType, content);
        if (!string.Equals(SelectedNote?.Content, content, StringComparison.Ordinal))
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Note_SaveError"]);
            return;
        }

        await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], _loc["Note_SaveSuccess"]);
    }

    [RelayCommand]
    private async Task TogglePinnedAsync()
    {
        var newValue = !IsPinned;
        if (SelectedNote is not null && SelectedNote.Id != 0 && !_noteRepo.SetPinned(SelectedNote.Id, newValue))
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Note_SaveError"]);
            return;
        }

        IsPinned = newValue;
        if (SelectedNote is not null)
        {
            var updated = SelectedNote with { IsPinned = newValue };
            Notes[Notes.IndexOf(SelectedNote)] = updated;
            SelectedNote = updated;
        }
    }

    [RelayCommand]
    private async Task DeleteNoteAsync()
    {
        if (!HasExistingNote) return;

        var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"], _loc["Note_ConfirmDelete"]);
        if (!confirmed) return;

        var deleted = SelectedNote is { Id: not 0 }
            ? _noteRepo.DeleteNoteById(SelectedNote.Id)
            : _noteRepo.DeleteNote(DocumentId);
        if (!deleted)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Note_SaveError"]);
            return;
        }

        ReloadNotes();
        await _dialogService.ShowMessageAsync(_loc["Dialog_Deleted"], _loc["Note_DeleteSuccess"]);
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        if (string.Equals(NoteContent, SavedNoteContent, StringComparison.Ordinal))
        {
            _navigationService.GoBack();
            return;
        }

        var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"], _loc["Note_ConfirmDiscard"], _loc["Note_Discard"]);
        if (confirmed)
            _navigationService.GoBack();
    }

    private void ReloadNotes(int noteId = 0, string? noteType = null, string? content = null)
    {
        Notes.Clear();
        foreach (var note in _noteRepo.GetNotes(DocumentId))
            Notes.Add(note);

        SelectedNote = noteId != 0
            ? Notes.FirstOrDefault(note => note.Id == noteId)
            : noteType is not null && content is not null
                ? Notes.LastOrDefault(note => note.NoteType == noteType && note.Content == content)
                : Notes.FirstOrDefault(note => note.NoteType == "general") ?? Notes.FirstOrDefault();

        if (SelectedNote is null)
            HasExistingNote = false;
    }
}
