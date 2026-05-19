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
    private readonly IDocument _repository;
    private readonly ICategory _categoryRepo;
    private readonly IDialogService _dialogService;
    private readonly IFileDialogService _fileDialogService;
    private readonly INavigationService _navigationService;
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
    [ObservableProperty] private string _pageTitle = "Thêm tài liệu mới";
    [ObservableProperty] private bool _isEditing;

    [ObservableProperty] private ObservableCollection<string> _subjects = new();
    [ObservableProperty] private ObservableCollection<string> _types = new();

    public AddEditModel(IDocument repository, ICategory categoryRepo, IDialogService dialogService, IFileDialogService fileDialogService, INavigationService navigationService)
    {
        _repository = repository;
        _categoryRepo = categoryRepo;
        _dialogService = dialogService;
        _fileDialogService = fileDialogService;
        _navigationService = navigationService;

        Subjects = new ObservableCollection<string>(_categoryRepo.GetAllSubjects());
        Types = new ObservableCollection<string>(_categoryRepo.GetAllTypes());
    }

    public void LoadDocument(int documentId)
    {
        var doc = _repository.GetById(documentId);
        if (doc == null) return;

        _editingId = doc.Id;
        IsEditing = true;
        PageTitle = "Chỉnh sửa tài liệu";

        Name = doc.Name;
        Subject = doc.Subject;
        Type = doc.Type;
        FilePath = doc.FilePath;
        Notes = doc.Notes;
        Author = doc.Author;
        Tags = doc.Tags;
        IsImportant = doc.IsImportant;
        Deadline = doc.Deadline.HasValue ? new DateTimeOffset(doc.Deadline.Value) : null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await _dialogService.ShowErrorAsync("Lỗi", "Tên tài liệu không được để trống!");
            return;
        }

        var doc = new StudyDocument
        {
            Name = Name.Trim(),
            Subject = Subject.Trim(),
            Type = Type.Trim(),
            FilePath = FilePath.Trim(),
            Notes = Notes.Trim(),
            Author = Author.Trim(),
            Tags = Tags.Trim(),
            IsImportant = IsImportant,
            Deadline = Deadline?.DateTime,
            FileSize = GetFileSize(FilePath)
        };

        bool success;
        if (IsEditing && _editingId.HasValue)
        {
            doc.Id = _editingId.Value;
            success = _repository.Update(doc);
        }
        else
        {
            success = _repository.Add(doc);
        }

        if (success)
        {
            // Sync new categories to lookup tables
            if (!string.IsNullOrWhiteSpace(doc.Subject))
                _categoryRepo.AddSubject(doc.Subject);
            if (!string.IsNullOrWhiteSpace(doc.Type))
                _categoryRepo.AddType(doc.Type);

            await _dialogService.ShowMessageAsync("Thành công",
                IsEditing ? "Đã cập nhật tài liệu!" : "Đã thêm tài liệu mới!");
            _navigationService.NavigateTo("dashboard");
        }
        else
        {
            await _dialogService.ShowErrorAsync("Lỗi", "Không thể lưu tài liệu. Vui lòng thử lại.");
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
        var path = await _fileDialogService.ShowOpenFileAsync("Chọn tài liệu",
            "All Files|*.*|PDF|*.pdf|Word|*.doc;*.docx|Excel|*.xls;*.xlsx|Image|*.png;*.jpg;*.jpeg;*.gif;*.bmp");
        if (string.IsNullOrEmpty(path)) return;

        FilePath = path;

        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = Path.GetFileNameWithoutExtension(path);
        }

        // Auto-detect file type using shared helper
        if (string.IsNullOrWhiteSpace(Type))
        {
            var ext = Path.GetExtension(path);
            Type = FileTypeDetector.Detect(ext);

            // Ensure detected type is available in the ComboBox dropdown
            if (!string.IsNullOrWhiteSpace(Type) && !Types.Contains(Type))
            {
                Types.Add(Type);
            }
        }
    }

    private static double? GetFileSize(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                return new FileInfo(path).Length / (1024.0 * 1024.0); // MB
            }
        }
        catch { /* ignore */ }
        return null;
    }
}
