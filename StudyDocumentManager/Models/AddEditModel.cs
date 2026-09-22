using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Core.Services;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class AddEditModel : ModelBase
{
    private readonly IDocumentRepository _repository;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IDialogService _dialogService;
    private readonly IFileDialogService _fileDialogService;
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _loc;
    private bool _localizationSubscribed;
    private int? _editingId;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _subject = string.Empty;
    [ObservableProperty] private string _type = string.Empty;
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _author = string.Empty;
    [ObservableProperty] private string _tags = string.Empty;
    [ObservableProperty] private bool _isImportant;
    [ObservableProperty] private DateTimeOffset? _deadline;
    [ObservableProperty] private string _pageTitle = string.Empty;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _hasNameValidationError;
    [ObservableProperty] private string _nameValidationMessage = string.Empty;
    [ObservableProperty] private string _selectedStatus = DocumentStatus.Unread;
    [ObservableProperty] private bool _hasStatusValidationError;
    [ObservableProperty] private string _statusValidationMessage = string.Empty;

    [ObservableProperty] private ObservableCollection<string> _subjects = new();
    [ObservableProperty] private ObservableCollection<string> _types = new();
    [ObservableProperty] private List<StatusOption> _statusOptions = [];

    public AddEditModel(IDocumentRepository repository, ICategoryRepository categoryRepo, IDialogService dialogService, IFileDialogService fileDialogService, INavigationService navigationService, ILocalizationService loc)
    {
        _repository = repository;
        _categoryRepo = categoryRepo;
        _dialogService = dialogService;
        _fileDialogService = fileDialogService;
        _navigationService = navigationService;
        _loc = loc;
        _loc.LanguageChanged += OnLanguageChanged;
        _localizationSubscribed = true;

        PageTitle = _loc["AddEdit_PageTitleAdd"];
        Subjects = new ObservableCollection<string>(_categoryRepo.GetAllSubjects());
        Types = new ObservableCollection<string>(_categoryRepo.GetAllTypes());
        BuildStatusOptions();
    }

    public void AttachLocalization()
    {
        if (_localizationSubscribed)
            return;

        _loc.LanguageChanged += OnLanguageChanged;
        _localizationSubscribed = true;
        OnLanguageChanged(this, EventArgs.Empty);
    }

    public void DetachLocalization()
    {
        if (!_localizationSubscribed)
            return;

        _loc.LanguageChanged -= OnLanguageChanged;
        _localizationSubscribed = false;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        PageTitle = _loc[IsEditing ? "AddEdit_PageTitleEdit" : "AddEdit_PageTitleAdd"];
        BuildStatusOptions();
        if (HasNameValidationError)
            NameValidationMessage = _loc["AddEdit_NameRequired"];
        if (HasStatusValidationError)
            StatusValidationMessage = _loc["DS_Error_InvalidStatus"];
    }

    private void BuildStatusOptions()
        => StatusOptions = [..DocumentStatus.All.Select(s => new StatusOption(s, GetStatusLabel(s)))];

    private string GetStatusLabel(string status) => status switch
    {
        DocumentStatus.Unread => _loc["DS_Kind_Unread"],
        DocumentStatus.InProgress => _loc["DS_Kind_InProgress"],
        DocumentStatus.Read => _loc["DS_Kind_Read"],
        DocumentStatus.NeedsAction => _loc["DS_Kind_NeedsAction"],
        DocumentStatus.Completed => _loc["DS_Kind_Completed"],
        DocumentStatus.Archived => _loc["DS_Kind_Archived"],
        _ => status
    };

    public void LoadDocument(int documentId)
    {
        var doc = _repository.GetById(documentId);
        if (doc == null) return;

        _editingId = doc.Id;
        IsEditing = true;
        PageTitle = _loc["AddEdit_PageTitleEdit"];

        Name = doc.Name;
        Subject = doc.Subject;
        Type = doc.Type;
        FilePath = doc.FilePath;
        Notes = doc.Notes;
        Author = doc.Author;
        Tags = doc.Tags;
        IsImportant = doc.IsImportant;
        Deadline = doc.Deadline.HasValue ? new DateTimeOffset(doc.Deadline.Value) : null;
        SelectedStatus = DocumentStatus.IsValid(doc.Status) ? doc.Status : DocumentStatus.Unread;
    }

    partial void OnNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && HasNameValidationError)
        {
            HasNameValidationError = false;
            NameValidationMessage = string.Empty;
        }
    }

    partial void OnSelectedStatusChanged(string value)
    {
        if (DocumentStatus.IsValid(value) && HasStatusValidationError)
        {
            HasStatusValidationError = false;
            StatusValidationMessage = string.Empty;
        }
    }

    private bool ValidateStatus()
    {
        if (DocumentStatus.IsValid(SelectedStatus))
        {
            HasStatusValidationError = false;
            StatusValidationMessage = string.Empty;
            return true;
        }

        HasStatusValidationError = true;
        StatusValidationMessage = _loc["DS_Error_InvalidStatus"];
        return false;
    }

    private bool ValidateName()
    {
        if (!string.IsNullOrWhiteSpace(Name))
        {
            HasNameValidationError = false;
            NameValidationMessage = string.Empty;
            return true;
        }

        HasNameValidationError = true;
        NameValidationMessage = _loc["AddEdit_NameRequired"];
        return false;
    }

    public bool TryApplyFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        FilePath = filePath;

        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = Path.GetFileNameWithoutExtension(filePath);
        }

        if (string.IsNullOrWhiteSpace(Type))
        {
            var detectedType = FileTypeDetector.Detect(Path.GetExtension(filePath));
            if (!string.IsNullOrWhiteSpace(detectedType))
            {
                Type = detectedType;
                if (!Types.Contains(detectedType))
                {
                    Types.Add(detectedType);
                }
            }
        }

        HasNameValidationError = false;
        NameValidationMessage = string.Empty;
        return true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!ValidateName())
        {
            return;
        }

        if (!ValidateStatus())
        {
            return;
        }

        var filePath = FilePath.Trim();
        var doc = new StudyDocument
        {
            Name = Name.Trim(),
            Subject = Subject.Trim(),
            Type = Type.Trim(),
            FilePath = filePath,
            Notes = Notes.Trim(),
            Author = Author.Trim(),
            Tags = Tags.Trim(),
            IsImportant = IsImportant,
            Deadline = Deadline?.DateTime,
            FileSize = GetFileSize(filePath),
            Status = SelectedStatus
        };

        try
        {
            bool success;
            if (IsEditing && _editingId.HasValue)
            {
                doc.Id = _editingId.Value;
                success = _repository.Update(doc);
            }
            else
            {
                success = _repository.AddWithCatalogs(doc);
            }

            if (success)
            {
                await _dialogService.ShowMessageAsync(_loc["Dialog_Success"],
                    IsEditing ? _loc["AddEdit_SaveUpdated"] : _loc["AddEdit_SaveAdded"]);
                _navigationService.NavigateTo("dashboard");
            }
            else
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["AddEdit_SaveError"]);
            }
        }
        catch
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["AddEdit_SaveError"]);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _navigationService.NavigateTo("dashboard");
    }

    [RelayCommand]
    private async Task BrowseFileAsync()
    {
        var path = await _fileDialogService.ShowOpenFileAsync(_loc["AddEdit_BrowseFile"], _loc["AddEdit_FileFilter"]);
        if (string.IsNullOrEmpty(path)) return;

        TryApplyFile(path);
    }

    private static double? GetFileSize(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                return new FileInfo(path).Length / (1024.0 * 1024.0);
            }
        }
        catch { /* ignore */ }
        return null;
    }
}
