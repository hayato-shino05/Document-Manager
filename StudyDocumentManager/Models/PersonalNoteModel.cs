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
    private readonly IClipboardService? _clipboardService;
    private readonly IDocumentRepository? _documentRepository;
    private PersonalNote? _loadedNote;
    private bool _isRestoringSelection;
    private bool _isFiltering;

    [ObservableProperty] private int _documentId;
    [ObservableProperty] private string _documentName = string.Empty;
    [ObservableProperty] private string _noteContent = string.Empty;
    [ObservableProperty] private string _savedNoteContent = string.Empty;
    [ObservableProperty] private bool _hasExistingNote;
    [ObservableProperty] private PersonalNote? _selectedNote;
    [ObservableProperty] private string _selectedNoteType = "general";
    [ObservableProperty] private bool _isPinned;

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string _selectedTypeFilter = "all";

    public ObservableCollection<PersonalNote> Notes { get; } = [];
    public ObservableCollection<PersonalNote> FilteredNotes { get; } = [];
    public IReadOnlyList<string> NoteTypes => NoteType.All;
    public IReadOnlyList<string> TypeFilters { get; } = ["all", "general", "summary", "action", "quote", "lecture", "meeting"];

    public bool CanSaveNote => !string.IsNullOrWhiteSpace(NoteContent);
    public bool HasSavedNotePreview => HasExistingNote && !string.IsNullOrWhiteSpace(SavedNoteContent);
    public bool HasUnsavedChanges => !string.Equals(NoteContent, SavedNoteContent, StringComparison.Ordinal);
    public int NotesCount => Notes.Count;
    public int FilteredNotesCount => FilteredNotes.Count;
    public int CharCount => NoteContent.Length;
    public int WordCount => CountWords(NoteContent);
    public bool IsEditingExistingNote => SelectedNote is not null && SelectedNote.Id != 0;

    public PersonalNoteModel(
        IPersonalNoteRepository noteRepo,
        IDialogService dialogService,
        INavigationService navigationService,
        ILocalizationService loc,
        IClipboardService? clipboardService = null,
        IDocumentRepository? documentRepository = null)
    {
        _noteRepo = noteRepo;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _loc = loc;
        _clipboardService = clipboardService;
        _documentRepository = documentRepository;
    }

    public void Load(int docId, string docName)
    {
        DocumentId = docId;
        DocumentName = docName;
        SearchQuery = string.Empty;
        SelectedTypeFilter = "all";
        ReloadNotes();
    }

    partial void OnNoteContentChanged(string value)
    {
        OnPropertyChanged(nameof(CanSaveNote));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(CharCount));
        OnPropertyChanged(nameof(WordCount));
    }

    partial void OnSavedNoteContentChanged(string value)
    {
        OnPropertyChanged(nameof(HasSavedNotePreview));
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    partial void OnHasExistingNoteChanged(bool value)
    {
        OnPropertyChanged(nameof(HasSavedNotePreview));
        OnPropertyChanged(nameof(IsEditingExistingNote));
    }

    partial void OnSearchQueryChanged(string value)
        => ApplyFilter();

    partial void OnSelectedTypeFilterChanged(string value)
        => ApplyFilter();

    partial void OnSelectedNoteChanged(PersonalNote? value)
    {
        if (_isRestoringSelection || _isFiltering)
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
            OnPropertyChanged(nameof(IsEditingExistingNote));
            return;
        }

        NoteContent = value.Content;
        SavedNoteContent = value.Content;
        SelectedNoteType = value.NoteType;
        IsPinned = value.IsPinned;
        HasExistingNote = true;
        _loadedNote = value;
        OnPropertyChanged(nameof(IsEditingExistingNote));
    }

    [RelayCommand]
    private void NewNote()
    {
        if (!string.Equals(NoteContent, SavedNoteContent, StringComparison.Ordinal))
            return;

        SelectedNote = null;
        NoteContent = string.Empty;
        SavedNoteContent = string.Empty;
        SelectedNoteType = "general";
        IsPinned = false;
        HasExistingNote = false;
        _loadedNote = null;
        OnPropertyChanged(nameof(IsEditingExistingNote));
    }

    [RelayCommand]
    private void SetFilter(string filter)
    {
        SelectedTypeFilter = filter ?? "all";
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
        var existingNoteIds = SelectedNote is null
            ? _noteRepo.GetNotes(DocumentId).Select(n => n.Id).ToHashSet()
            : null;
        var note = new PersonalNote(SelectedNote?.Id ?? 0, DocumentId, SelectedNoteType, content, IsPinned);
        if (!_noteRepo.SaveNote(note))
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Note_SaveError"]);
            return;
        }

        // Bridge: Sync primary note into document repository notes if documentRepository is available
        if (SelectedNoteType == "general" && _documentRepository != null)
        {
            var doc = _documentRepository.GetById(DocumentId);
            if (doc != null)
            {
                doc.Notes = content;
                _documentRepository.Update(doc);
            }
        }

        SavedNoteContent = content;
        HasExistingNote = true;
        var savedNoteId = note.Id != 0
            ? note.Id
            : _noteRepo.GetNotes(DocumentId)
                .Where(savedNote =>
                    savedNote.NoteType == note.NoteType &&
                    savedNote.Content == content &&
                    (existingNoteIds is null || !existingNoteIds.Contains(savedNote.Id)))
                .OrderByDescending(savedNote => savedNote.Id)
                .Select(savedNote => (int?)savedNote.Id)
                .FirstOrDefault();

        ReloadNotes(savedNoteId ?? 0, note.NoteType, content);
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
            var idx = Notes.IndexOf(SelectedNote);
            if (idx >= 0)
                Notes[idx] = updated;
            SelectedNote = updated;
            ApplyFilter();
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
    private async Task CopyContentAsync()
    {
        if (string.IsNullOrEmpty(NoteContent)) return;

        if (_clipboardService != null)
        {
            await _clipboardService.SetTextAsync(NoteContent);
            await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], _loc["PersonalNote_CopySuccess"]);
        }
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

        OnPropertyChanged(nameof(NotesCount));

        SelectedNote = noteId != 0
            ? Notes.FirstOrDefault(note => note.Id == noteId)
            : noteType is not null && content is not null
                ? Notes.Where(note => note.NoteType == noteType && note.Content == content)
                    .OrderByDescending(note => note.Id)
                    .FirstOrDefault()
                : Notes.FirstOrDefault(note => note.NoteType == "general") ?? Notes.FirstOrDefault();

        if (SelectedNote is null)
            HasExistingNote = false;

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        _isFiltering = true;
        var currentSelected = SelectedNote;
        FilteredNotes.Clear();

        var query = SearchQuery.Trim();
        var typeFilter = SelectedTypeFilter;

        foreach (var note in Notes)
        {
            if (typeFilter != "all" && !string.Equals(note.NoteType, typeFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            if (query.Length > 0 && !note.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;

            FilteredNotes.Add(note);
        }

        OnPropertyChanged(nameof(FilteredNotesCount));
        _isFiltering = false;
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var words = 0;
        var inWord = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                words++;
            }
        }
        return words;
    }
}
