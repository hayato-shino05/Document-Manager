using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class AddEditModel : ModelBase
{
    private readonly IDocumentRepository _repository;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private int? _editingId;

    [ObservableProperty] private string _ten = string.Empty;
    [ObservableProperty] private string _monHoc = string.Empty;
    [ObservableProperty] private string _loai = string.Empty;
    [ObservableProperty] private string _duongDan = string.Empty;
    [ObservableProperty] private string _ghiChu = string.Empty;
    [ObservableProperty] private string _tacGia = string.Empty;
    [ObservableProperty] private string _tags = string.Empty;
    [ObservableProperty] private bool _quanTrong;
    [ObservableProperty] private DateTimeOffset? _deadline;
    [ObservableProperty] private string _pageTitle = "Thêm tài liệu mới";
    [ObservableProperty] private bool _isEditing;

    [ObservableProperty] private ObservableCollection<string> _subjects = new();
    [ObservableProperty] private ObservableCollection<string> _types = new();

    public AddEditModel(IDocumentRepository repository, IDialogService dialogService, INavigationService navigationService)
    {
        _repository = repository;
        _dialogService = dialogService;
        _navigationService = navigationService;

        // Load dropdown data from lookup tables
        Subjects = new ObservableCollection<string>(DatabaseHelper.GetAllSubjects());
        Types = new ObservableCollection<string>(DatabaseHelper.GetAllTypes());
    }

    public void LoadDocument(int documentId)
    {
        var doc = _repository.GetById(documentId);
        if (doc == null) return;

        _editingId = doc.Id;
        IsEditing = true;
        PageTitle = "Chỉnh sửa tài liệu";

        Ten = doc.Ten;
        MonHoc = doc.MonHoc;
        Loai = doc.Loai;
        DuongDan = doc.DuongDan;
        GhiChu = doc.GhiChu;
        TacGia = doc.TacGia;
        Tags = doc.Tags;
        QuanTrong = doc.QuanTrong;
        Deadline = doc.Deadline.HasValue ? new DateTimeOffset(doc.Deadline.Value) : null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Ten))
        {
            await _dialogService.ShowErrorAsync("Lỗi", "Tên tài liệu không được để trống!");
            return;
        }

        var doc = new StudyDocument
        {
            Ten = Ten.Trim(),
            MonHoc = MonHoc.Trim(),
            Loai = Loai.Trim(),
            DuongDan = DuongDan.Trim(),
            GhiChu = GhiChu.Trim(),
            TacGia = TacGia.Trim(),
            Tags = Tags.Trim(),
            QuanTrong = QuanTrong,
            Deadline = Deadline?.DateTime,
            KichThuoc = GetFileSize(DuongDan)
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
            if (!string.IsNullOrWhiteSpace(doc.MonHoc))
                DatabaseHelper.AddSubject(doc.MonHoc);
            if (!string.IsNullOrWhiteSpace(doc.Loai))
                DatabaseHelper.AddType(doc.Loai);

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
        var path = await _dialogService.ShowOpenFileAsync("Chọn tài liệu",
            "All Files|*.*|PDF|*.pdf|Word|*.doc;*.docx|Excel|*.xls;*.xlsx|Image|*.png;*.jpg;*.jpeg;*.gif;*.bmp");
        if (string.IsNullOrEmpty(path)) return;

        DuongDan = path;

        // Auto-fill name if empty
        if (string.IsNullOrWhiteSpace(Ten))
        {
            Ten = Path.GetFileNameWithoutExtension(path);
        }

        // Auto-detect file type using shared helper
        if (string.IsNullOrWhiteSpace(Loai))
        {
            var ext = Path.GetExtension(path);
            Loai = Services.FileTypeDetector.Detect(ext);

            // Ensure detected type is available in the ComboBox dropdown
            if (!string.IsNullOrWhiteSpace(Loai) && !Types.Contains(Loai))
            {
                Types.Add(Loai);
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
