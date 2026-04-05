using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.ViewModels;

public partial class CategoryManagementViewModel : ViewModelBase
{
    private readonly IDocumentRepository _repository;
    private readonly IDialogService _dialogService;

    [ObservableProperty] private ObservableCollection<CategoryItem> _subjects = new();
    [ObservableProperty] private ObservableCollection<CategoryItem> _types = new();
    [ObservableProperty] private CategoryItem? _selectedSubject;
    [ObservableProperty] private CategoryItem? _selectedType;
    [ObservableProperty] private int _selectedTabIndex;

    public CategoryManagementViewModel(IDocumentRepository repository, IDialogService dialogService)
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
    }

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

    [RelayCommand]
    private async Task DeleteSubjectAsync()
    {
        if (SelectedSubject == null) return;

        bool confirm = await _dialogService.ShowConfirmAsync(
            "Xóa danh mục",
            $"Bạn có chắc muốn xóa danh mục '{SelectedSubject.Name}' và {SelectedSubject.Count} tài liệu bên trong?");

        if (confirm)
        {
            DatabaseHelper.DeleteDocumentsBySubject(SelectedSubject.Name);
            LoadData();
            await _dialogService.ShowMessageAsync("Thành công", "Đã xóa danh mục và tài liệu liên quan");
        }
    }

    [RelayCommand]
    private async Task DeleteTypeAsync()
    {
        if (SelectedType == null) return;

        bool confirm = await _dialogService.ShowConfirmAsync(
            "Xóa loại tài liệu",
            $"Bạn có chắc muốn xóa loại '{SelectedType.Name}' và {SelectedType.Count} tài liệu bên trong?");

        if (confirm)
        {
            DatabaseHelper.DeleteDocumentsByType(SelectedType.Name);
            LoadData();
            await _dialogService.ShowMessageAsync("Thành công", "Đã xóa loại tài liệu và tài liệu liên quan");
        }
    }

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
