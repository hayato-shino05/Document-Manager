using System.Collections;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class CategoryManagementModel : ModelBase
{
    private readonly IDocumentRepository _repository;
    private readonly IDialogService _dialogService;

    [ObservableProperty] private ObservableCollection<CategoryItem> _subjects = new();
    [ObservableProperty] private ObservableCollection<CategoryItem> _types = new();

    // Single selection (for Rename & single-delete fallback)
    [ObservableProperty] private CategoryItem? _selectedSubject;
    [ObservableProperty] private CategoryItem? _selectedType;

    // Multi-selection (bound to ListBox.SelectedItems)
    [ObservableProperty] private IList _selectedSubjects = new List<CategoryItem>();
    [ObservableProperty] private IList _selectedTypes = new List<CategoryItem>();

    [ObservableProperty] private int _selectedTabIndex;

    // Total document count in DB (not sum of category counts, which misses uncategorised docs)
    [ObservableProperty] private int _totalDocumentCount;

    /// <summary>Status bar text for the main window footer.</summary>
    public string StatusText => $"Tổng số: {TotalDocumentCount} tài liệu | Danh mục: {Subjects.Count} | Loại: {Types.Count}";

    public CategoryManagementModel(IDocumentRepository repository, IDialogService dialogService)
    {
        _repository = repository;
        _dialogService = dialogService;
        LoadData();
    }

    private void LoadData()
    {
        var subjectsData = DatabaseHelper.GetSubjectsWithCount();
        Subjects = new ObservableCollection<CategoryItem>(
            subjectsData.Select(s => new CategoryItem(s.Name, s.Count)));

        var typesData = DatabaseHelper.GetTypesWithCount();
        Types = new ObservableCollection<CategoryItem>(
            typesData.Select(t => new CategoryItem(t.Name, t.Count)));

        TotalDocumentCount = DatabaseHelper.GetTotalDocumentCount();
        OnPropertyChanged(nameof(StatusText));
    }

    // ─── Rename ──────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RenameSubjectAsync()
    {
        if (SelectedSubject == null) return;

        var newName = await _dialogService.ShowInputAsync("Đổi tên danh mục", "Tên mới:", SelectedSubject.Name);
        if (!string.IsNullOrWhiteSpace(newName) && newName != SelectedSubject.Name)
        {
            DatabaseHelper.UpdateSubjectName(SelectedSubject.Name, newName);
            LoadData();
            await _dialogService.ShowMessageAsync("Thành công", $"Đã đổi tên danh mục thành '{newName}'");
        }
    }

    [RelayCommand]
    private async Task RenameTypeAsync()
    {
        if (SelectedType == null) return;

        var newName = await _dialogService.ShowInputAsync("Đổi tên loại", "Tên mới:", SelectedType.Name);
        if (!string.IsNullOrWhiteSpace(newName) && newName != SelectedType.Name)
        {
            DatabaseHelper.UpdateTypeName(SelectedType.Name, newName);
            LoadData();
            await _dialogService.ShowMessageAsync("Thành công", $"Đã đổi tên loại thành '{newName}'");
        }
    }

    // ─── Delete (multi-select aware) ─────────────────────────────────

    [RelayCommand]
    private async Task DeleteSubjectAsync()
    {
        var targets = SelectedSubjects.Cast<CategoryItem>().ToList();
        if (targets.Count == 0)
        {
            if (SelectedSubject == null) return;
            targets = [SelectedSubject];
        }

        int totalDocs = targets.Sum(t => t.Count);
        string namesStr = targets.Count == 1
            ? $"'{targets[0].Name}'"
            : $"{targets.Count} danh mục đã chọn";

        string confirmMsg = totalDocs == 0
            ? $"Xóa {namesStr}?"
            : $"Xóa {namesStr}?\n\n{totalDocs} tài liệu liên quan sẽ được chuyển vào Thùng rác.";

        bool confirm = await _dialogService.ShowConfirmAsync("Xóa danh mục", confirmMsg,
            "Xoá", isDanger: true);
        if (!confirm) return;

        foreach (var item in targets)
            DatabaseHelper.DeleteDocumentsBySubject(item.Name);

        LoadData();
        SelectedSubjects = new List<CategoryItem>();

        string doneMsg = targets.Count == 1
            ? $"Đã xóa danh mục '{targets[0].Name}'"
            : $"Đã xóa {targets.Count} danh mục";
        await _dialogService.ShowMessageAsync("Thành công", doneMsg);
    }

    [RelayCommand]
    private async Task DeleteTypeAsync()
    {
        var targets = SelectedTypes.Cast<CategoryItem>().ToList();
        if (targets.Count == 0)
        {
            if (SelectedType == null) return;
            targets = [SelectedType];
        }

        int totalDocs = targets.Sum(t => t.Count);
        string namesStr = targets.Count == 1
            ? $"'{targets[0].Name}'"
            : $"{targets.Count} loại đã chọn";

        string confirmMsg = totalDocs == 0
            ? $"Xóa {namesStr}?"
            : $"Xóa {namesStr}?\n\n{totalDocs} tài liệu liên quan sẽ được chuyển vào Thùng rác.";

        bool confirm = await _dialogService.ShowConfirmAsync("Xóa loại tài liệu", confirmMsg,
            "Xoá", isDanger: true);
        if (!confirm) return;

        foreach (var item in targets)
            DatabaseHelper.DeleteDocumentsByType(item.Name);

        LoadData();
        SelectedTypes = new List<CategoryItem>();

        string doneMsg = targets.Count == 1
            ? $"Đã xóa loại '{targets[0].Name}'"
            : $"Đã xóa {targets.Count} loại tài liệu";
        await _dialogService.ShowMessageAsync("Thành công", doneMsg);
    }

    // ─── Add ─────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AddSubjectAsync()
    {
        var name = await _dialogService.ShowInputAsync("Thêm danh mục", "Tên danh mục mới:", "");
        if (!string.IsNullOrWhiteSpace(name))
        {
            var trimmed = name.Trim();
            if (Subjects.Any(s => s.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                await _dialogService.ShowMessageAsync("Lỗi", $"Danh mục '{trimmed}' đã tồn tại.");
                return;
            }
            DatabaseHelper.AddSubject(trimmed);
            LoadData();
            await _dialogService.ShowMessageAsync("Thành công", $"Đã thêm danh mục '{trimmed}'");
        }
    }

    [RelayCommand]
    private async Task AddTypeAsync()
    {
        var name = await _dialogService.ShowInputAsync("Thêm loại tài liệu", "Tên loại mới:", "");
        if (!string.IsNullOrWhiteSpace(name))
        {
            var trimmed = name.Trim();
            if (Types.Any(t => t.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                await _dialogService.ShowMessageAsync("Lỗi", $"Loại '{trimmed}' đã tồn tại.");
                return;
            }
            DatabaseHelper.AddType(trimmed);
            LoadData();
            await _dialogService.ShowMessageAsync("Thành công", $"Đã thêm loại '{trimmed}'");
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadData();
    }
}

/// <summary>
/// Category item with name and document count
/// </summary>
public class CategoryItem
{
    public string Name { get; set; }
    public int Count { get; set; }
    public string Display => $"{Name} ({Count})";

    public CategoryItem(string name, int count)
    {
        Name = name;
        Count = count;
    }
}
