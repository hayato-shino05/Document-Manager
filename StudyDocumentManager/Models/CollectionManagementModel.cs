using System.Collections;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class CollectionManagementModel : ModelBase
{
    private readonly IDocument _repository;
    private readonly Core.Interfaces.ICollection _collectionRepo;
    private readonly IDialogService _dialogService;
    private readonly ICustomDialogService _customDialogService;

    [ObservableProperty] private ObservableCollection<CollectionItem> _collections = [];
    [ObservableProperty] private CollectionItem? _selectedCollection;
    [ObservableProperty] private ObservableCollection<StudyDocument> _documentsInCollection = [];
    [ObservableProperty] private StudyDocument? _selectedDocumentInCollection;
    [ObservableProperty] private ObservableCollection<StudyDocument> _allDocuments = [];
    [ObservableProperty] private IList _selectedDocumentsInCollection = new List<StudyDocument>();

    public CollectionManagementModel(IDocument repository, Core.Interfaces.ICollection collectionRepo, IDialogService dialogService, ICustomDialogService customDialogService)
    {
        _repository = repository;
        _collectionRepo = collectionRepo;
        _dialogService = dialogService;
        _customDialogService = customDialogService;
        LoadCollections();
    }

    private void LoadCollections()
    {
        try
        {
            var data = _collectionRepo.GetAll();
            Collections = new ObservableCollection<CollectionItem>(
                data.Select(c => new CollectionItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    ItemCount = c.ItemCount
                }));
        }
        catch (Exception ex)
        {
            _ = _dialogService.ShowErrorAsync("Lỗi", $"Không thể tải bộ sưu tập: {ex.Message}");
        }
    }

    partial void OnSelectedCollectionChanged(CollectionItem? value)
    {
        if (value != null)
        {
            var docs = _collectionRepo.GetDocuments(value.Id);
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
        try
        {
            var name = await _dialogService.ShowInputAsync("Tạo bộ sưu tập", "Tên bộ sưu tập:");

            if (!string.IsNullOrWhiteSpace(name))
            {
                _collectionRepo.Create(name);
                LoadCollections();
                await _dialogService.ShowMessageAsync("Thành công", $"Đã tạo bộ sưu tập '{name}'");
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync("Lỗi", $"Không thể tạo bộ sưu tập: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RenameCollectionAsync()
    {
        if (SelectedCollection == null) return;

        var newName = await _dialogService.ShowInputAsync("Đổi tên", "Tên mới:", SelectedCollection.Name);
        if (!string.IsNullOrWhiteSpace(newName) && newName != SelectedCollection.Name)
        {
            _collectionRepo.Update(SelectedCollection.Id, newName);
            LoadCollections();
        }
    }

    [RelayCommand]
    private async Task DeleteCollectionAsync()
    {
        if (SelectedCollection == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync("Xác nhận",
            $"Bạn có chắc muốn xóa bộ sưu tập '{SelectedCollection.Name}'?",
            "Xoá", isDanger: true);
        if (confirmed)
        {
            _collectionRepo.Delete(SelectedCollection.Id);
            SelectedCollection = null;
            LoadCollections();
        }
    }

    [RelayCommand]
    private async Task AddDocumentToCollectionAsync()
    {
        if (SelectedCollection == null) return;

        try
        {
            var allDocs = _repository.GetAll();
            var alreadyIn = _collectionRepo.GetDocuments(SelectedCollection.Id)
                                          .Select(d => d.Id)
                                          .ToList();

            if (allDocs.Count == alreadyIn.Count)
            {
                await _dialogService.ShowMessageAsync(
                    "Thông báo",
                    "Tất cả tài liệu đã có trong bộ sưu tập này.");
                return;
            }

            var selected = await _customDialogService.ShowDocumentPickerAsync(
                SelectedCollection.Name,
                allDocs,
                alreadyIn);

            if (selected == null || selected.Count == 0) return;

            int selectedId = SelectedCollection.Id;
            string collectionName = SelectedCollection.Name;

            int addedCount = 0;
            foreach (var doc in selected)
            {
                bool ok = _collectionRepo.AddDocument(selectedId, doc.Id);
                if (ok) addedCount++;
            }

            OnSelectedCollectionChanged(SelectedCollection);

            LoadCollections();
            SelectedCollection = Collections.FirstOrDefault(c => c.Id == selectedId);

            string msg = addedCount == 1
                ? $"Đã thêm 1 tài liệu vào '{collectionName}'"
                : $"Đã thêm {addedCount} tài liệu vào '{collectionName}'";
            await _dialogService.ShowMessageAsync("Thành công", msg);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync("Lỗi", $"Không thể thêm tài liệu: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RemoveDocumentFromCollectionAsync(StudyDocument? doc)
    {
        if (SelectedCollection == null || doc == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync("Xác nhận",
            $"Gỡ '{doc.Name}' khỏi bộ sưu tập?");
        if (confirmed)
        {
            int selectedId = SelectedCollection.Id;
            _collectionRepo.RemoveDocument(selectedId, doc.Id);
            OnSelectedCollectionChanged(SelectedCollection);
            LoadCollections();
            SelectedCollection = Collections.FirstOrDefault(c => c.Id == selectedId);
        }
    }

    [RelayCommand]
    private async Task RemoveSelectedDocumentsAsync()
    {
        if (SelectedCollection == null) return;

        var targets = SelectedDocumentsInCollection.Cast<StudyDocument>().ToList();
        if (targets.Count == 0) return;

        string confirmMsg = targets.Count == 1
            ? $"Gỡ '{targets[0].Name}' khỏi bộ sưu tập?"
            : $"Gỡ {targets.Count} tài liệu khỏi bộ sưu tập?";

        bool confirmed = await _dialogService.ShowConfirmAsync("Xác nhận", confirmMsg);
        if (!confirmed) return;

        int selectedId = SelectedCollection.Id;
        foreach (var d in targets)
            _collectionRepo.RemoveDocument(selectedId, d.Id);

        OnSelectedCollectionChanged(SelectedCollection);
        LoadCollections();
        SelectedCollection = Collections.FirstOrDefault(c => c.Id == selectedId);
        SelectedDocumentsInCollection = new List<StudyDocument>();
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
