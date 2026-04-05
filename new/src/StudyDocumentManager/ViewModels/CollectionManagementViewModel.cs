using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.ViewModels;

public partial class CollectionManagementViewModel : ViewModelBase
{
    private readonly IDocumentRepository _repository;
    private readonly IDialogService _dialogService;

    [ObservableProperty] private ObservableCollection<CollectionItem> _collections = new();
    [ObservableProperty] private CollectionItem? _selectedCollection;
    [ObservableProperty] private ObservableCollection<StudyDocument> _documentsInCollection = new();
    [ObservableProperty] private ObservableCollection<StudyDocument> _allDocuments = new();

    public CollectionManagementViewModel(IDocumentRepository repository, IDialogService dialogService)
    {
        _repository = repository;
        _dialogService = dialogService;
        LoadCollections();
    }

    private void LoadCollections()
    {
        var data = DatabaseHelper.GetCollections();
        Collections = new ObservableCollection<CollectionItem>(
            data.Select(c => new CollectionItem
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ItemCount = c.ItemCount
            }));
    }

    partial void OnSelectedCollectionChanged(CollectionItem? value)
    {
        if (value != null)
        {
            var docs = DatabaseHelper.GetDocumentsInCollection(value.Id);
            DocumentsInCollection = new ObservableCollection<StudyDocument>(docs);
        }
        else
        {
            DocumentsInCollection.Clear();
        }
    }

    [RelayCommand]
    private async Task CreateCollectionAsync()
    {
        var name = await _dialogService.ShowInputAsync("Tạo bộ sưu tập", "Tên bộ sưu tập:");
        if (!string.IsNullOrWhiteSpace(name))
        {
            DatabaseHelper.CreateCollection(name);
            LoadCollections();
            await _dialogService.ShowMessageAsync("Thành công", $"Đã tạo bộ sưu tập '{name}'");
        }
    }

    [RelayCommand]
    private async Task RenameCollectionAsync()
    {
        if (SelectedCollection == null) return;

        var newName = await _dialogService.ShowInputAsync("Đổi tên", "Tên mới:", SelectedCollection.Name);
        if (!string.IsNullOrWhiteSpace(newName) && newName != SelectedCollection.Name)
        {
            DatabaseHelper.UpdateCollection(SelectedCollection.Id, newName);
            LoadCollections();
        }
    }

    [RelayCommand]
    private async Task DeleteCollectionAsync()
    {
        if (SelectedCollection == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync("Xác nhận",
            $"Bạn có chắc muốn xóa bộ sưu tập '{SelectedCollection.Name}'?");
        if (confirmed)
        {
            DatabaseHelper.DeleteCollection(SelectedCollection.Id);
            SelectedCollection = null;
            LoadCollections();
        }
    }

    [RelayCommand]
    private async Task AddDocumentToCollectionAsync()
    {
        if (SelectedCollection == null) return;

        // Search document by name (improved UX vs raw ID input)
        var keyword = await _dialogService.ShowInputAsync(
            "Thêm tài liệu vào bộ sưu tập",
            "Nhập tên tài liệu (hoặc một phần tên) để tìm:");
        if (string.IsNullOrWhiteSpace(keyword)) return;

        // Search for matching documents
        var allDocs = _repository.GetAll();
        var matches = allDocs
            .Where(d => d.Ten.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            await _dialogService.ShowMessageAsync("Không tìm thấy",
                $"Không tìm thấy tài liệu nào khớp với '{keyword}'");
            return;
        }

        // If multiple matches, let user pick by showing list
        StudyDocument? selected;
        if (matches.Count == 1)
        {
            selected = matches[0];
        }
        else
        {
            // Show numbered list for selection
            var list = string.Join("\n", matches.Select((d, i) => $"  {i + 1}. {d.Ten} ({d.MonHoc})"));
            var indexStr = await _dialogService.ShowInputAsync(
                $"Tìm thấy {matches.Count} kết quả",
                $"Chọn số thứ tự:\n{list}");
            if (!int.TryParse(indexStr, out int idx) || idx < 1 || idx > matches.Count) return;
            selected = matches[idx - 1];
        }

        bool added = DatabaseHelper.AddDocumentToCollection(SelectedCollection.Id, selected.Id);
        if (added)
        {
            OnSelectedCollectionChanged(SelectedCollection);
            LoadCollections();
            await _dialogService.ShowMessageAsync("Thành công",
                $"Đã thêm '{selected.Ten}' vào '{SelectedCollection.Name}'");
        }
        else
        {
            await _dialogService.ShowMessageAsync("Lưu ý", "Tài liệu đã có trong bộ sưu tập này");
        }
    }

    [RelayCommand]
    private async Task RemoveDocumentFromCollectionAsync(StudyDocument? doc)
    {
        if (SelectedCollection == null || doc == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync("Xác nhận",
            $"Gỡ '{doc.Ten}' khỏi bộ sưu tập?");
        if (confirmed)
        {
            DatabaseHelper.RemoveDocumentFromCollection(SelectedCollection.Id, doc.Id);
            OnSelectedCollectionChanged(SelectedCollection);
            LoadCollections();
        }
    }

    [RelayCommand]
    private void Refresh() => LoadCollections();
}

public class CollectionItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ItemCount { get; set; }
    public string Display => $"{Name} ({ItemCount})";
}
