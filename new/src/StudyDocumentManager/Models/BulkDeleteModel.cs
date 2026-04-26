using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class BulkDeleteModel : ModelBase
{
    private readonly IDocumentRepository _repository;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    // Use List<T> instead of ObservableCollection to prevent binding-engine re-render loops
    [ObservableProperty] private List<SelectableDocument> _documents = new();
    [ObservableProperty] private string _searchKeyword = string.Empty;

    // Filter dropdowns — List<string> to avoid Clear()+Add() loop
    [ObservableProperty] private List<string> _subjects = new();
    [ObservableProperty] private List<string> _types = new();
    [ObservableProperty] private string _selectedSubject = "Tất cả";
    [ObservableProperty] private string _selectedType = "Tất cả";

    // For ChangeSubject dialog
    [ObservableProperty] private List<string> _availableSubjects = new();
    [ObservableProperty] private string? _newSubjectValue;

    // Status
    [ObservableProperty] private string _statusText = "";
    public int SelectedCount => Documents.Count(d => d.IsSelected);

    public BulkDeleteModel(IDocumentRepository repository, IDialogService dialogService, INavigationService navigationService)
    {
        _repository = repository;
        _dialogService = dialogService;
        _navigationService = navigationService;
    }

    /// <summary>
    /// Called from code-behind after the view is fully loaded (deferred initialization).
    /// </summary>
    public void Initialize()
    {
        LoadFilterData();
        LoadData();
    }

    private void LoadFilterData()
    {
        var subjects = DatabaseHelper.GetAllSubjects();
        var types = DatabaseHelper.GetAllTypes();

        // Assign new List references instead of Clear()+Add()
        var subjectList = new List<string> { "Tất cả" };
        subjectList.AddRange(subjects);
        Subjects = subjectList;

        var typeList = new List<string> { "Tất cả" };
        typeList.AddRange(types);
        Types = typeList;

        AvailableSubjects = new List<string>(subjects);
    }

    private void LoadData()
    {
        var docs = _repository.GetAll();

        // Apply filters
        if (SelectedSubject != "Tất cả")
            docs = docs.Where(d => d.MonHoc == SelectedSubject).ToList();
        if (SelectedType != "Tất cả")
            docs = docs.Where(d => d.Loai == SelectedType).ToList();
        if (!string.IsNullOrWhiteSpace(SearchKeyword))
            docs = docs.Where(d => d.Ten.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase)
                || (d.GhiChu ?? "").Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase)).ToList();

        // Assign new List reference (not Clear+Add on ObservableCollection)
        Documents = docs.Select(d => new SelectableDocument { Document = d, IsSelected = false }).ToList();
        StatusText = $"Hiển thị {Documents.Count} tài liệu";
    }

    [RelayCommand]
    private void Search() => LoadData();

    partial void OnSelectedSubjectChanged(string value) => LoadData();
    partial void OnSelectedTypeChanged(string value) => LoadData();

    // ═══ Bulk Delete (existing) ═══
    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var selected = Documents.Where(d => d.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await _dialogService.ShowErrorAsync("Lỗi", "Chưa chọn tài liệu nào để xóa.");
            return;
        }

        var confirmed = await _dialogService.ShowConfirmAsync("Xác nhận xóa",
            $"Bạn có chắc muốn xóa {selected.Count} tài liệu? (Có thể khôi phục từ Thùng rác)",
            "Xoá", isDanger: true);
        if (!confirmed) return;

        var ids = selected.Select(s => s.Document.Id).ToList();
        int deleted = DatabaseHelper.BulkSoftDelete(ids);

        await _dialogService.ShowMessageAsync("Hoàn tất", $"Đã xóa {deleted} tài liệu vào Thùng rác.");
        _navigationService.NavigateTo("dashboard");
    }

    // ═══ Bulk Mark Important (NEW) ═══
    [RelayCommand]
    private async Task MarkImportantAsync()
    {
        var selected = Documents.Where(d => d.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await _dialogService.ShowErrorAsync("Lỗi", "Chưa chọn tài liệu nào.");
            return;
        }

        var ids = selected.Select(s => s.Document.Id).ToList();
        int updated = DatabaseHelper.BulkToggleImportant(ids, true);

        await _dialogService.ShowMessageAsync("Hoàn tất", $"Đã đánh dấu {updated} tài liệu là quan trọng.");
        LoadData();
    }

    // ═══ Bulk Change Subject (NEW) ═══
    [RelayCommand]
    private async Task ChangeSubjectAsync()
    {
        var selected = Documents.Where(d => d.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await _dialogService.ShowErrorAsync("Lỗi", "Chưa chọn tài liệu nào.");
            return;
        }

        if (string.IsNullOrWhiteSpace(NewSubjectValue))
        {
            await _dialogService.ShowErrorAsync("Lỗi", "Vui lòng chọn danh mục mới từ dropdown bên cạnh.");
            return;
        }

        var confirmed = await _dialogService.ShowConfirmAsync("Đổi danh mục",
            $"Đổi danh mục của {selected.Count} tài liệu thành '{NewSubjectValue}'?");
        if (!confirmed) return;

        var ids = selected.Select(s => s.Document.Id).ToList();
        int updated = DatabaseHelper.BulkUpdateSubject(ids, NewSubjectValue);

        await _dialogService.ShowMessageAsync("Hoàn tất", $"Đã cập nhật danh mục cho {updated} tài liệu.");
        LoadData();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var d in Documents) d.IsSelected = true;
        // Force re-bind since List<T> doesn't notify
        OnPropertyChanged(nameof(Documents));
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var d in Documents) d.IsSelected = false;
        OnPropertyChanged(nameof(Documents));
    }

    [RelayCommand]
    private void Cancel() => _navigationService.NavigateTo("dashboard");
}

public partial class SelectableDocument : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private StudyDocument _document = new();
}
