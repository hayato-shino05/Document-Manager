using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

    [ObservableProperty] private ObservableCollection<CollectionItem> _collections = [];
    [ObservableProperty] private CollectionItem? _selectedCollection;
    [ObservableProperty] private ObservableCollection<StudyDocument> _documentsInCollection = [];
    [ObservableProperty] private StudyDocument? _selectedDocumentInCollection;
    [ObservableProperty] private ObservableCollection<StudyDocument> _allDocuments = [];
    // Multi-select for batch removal
    [ObservableProperty] private IList _selectedDocumentsInCollection = new List<StudyDocument>();

    public CollectionManagementModel(IDocument repository, Core.Interfaces.ICollection collectionRepo, IDialogService dialogService)
    {
        _repository = repository;
        _collectionRepo = collectionRepo;
        _dialogService = dialogService;
        LoadCollections();
    }

    private void LoadCollections()
    {
        try
        {
            Debug.WriteLine("[CollectionVM] LoadCollections() called");
            var data = _collectionRepo.GetAll();
            Debug.WriteLine($"[CollectionVM] GetCollections() returned {data.Count} items");
            Collections = new ObservableCollection<CollectionItem>(
                data.Select(c => new CollectionItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    ItemCount = c.ItemCount
                }));
            Debug.WriteLine($"[CollectionVM] Collections updated, Count={Collections.Count}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CollectionVM] LoadCollections ERROR: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine(ex.StackTrace);
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
        Debug.WriteLine("[CollectionVM] CreateCollectionAsync() STARTED");
        try
        {
            var name = await _dialogService.ShowInputAsync("Tạo bộ sưu tập", "Tên bộ sưu tập:");
            Debug.WriteLine($"[CollectionVM] ShowInputAsync returned: '{name}' (IsNull={name is null}, IsEmpty={string.IsNullOrWhiteSpace(name)})");

            if (!string.IsNullOrWhiteSpace(name))
            {
                Debug.WriteLine($"[CollectionVM] Calling _collectionRepo.Create('{name}')");
                var newId = _collectionRepo.Create(name);
                Debug.WriteLine($"[CollectionVM] Create returned id={newId}");
                LoadCollections();
                await _dialogService.ShowMessageAsync("Thành công", $"Đã tạo bộ sưu tập '{name}'");
            }
            else
            {
                Debug.WriteLine("[CollectionVM] Name is null/empty, skipping creation");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CollectionVM] CreateCollectionAsync ERROR: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine(ex.StackTrace);
            await _dialogService.ShowErrorAsync("Lỗi", $"Không thể tạo bộ sưu tập: {ex.Message}");
        }
        Debug.WriteLine("[CollectionVM] CreateCollectionAsync() DONE");
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

        Debug.WriteLine($"[CollectionVM] AddDocumentToCollectionAsync: collection='{SelectedCollection.Name}'");

        try
        {
            // Load all documents and IDs already in collection
            var allDocs = _repository.GetAll();
            var alreadyIn = _collectionRepo.GetDocuments(SelectedCollection.Id)
                                          .Select(d => d.Id)
                                          .ToList();

            Debug.WriteLine($"[CollectionVM] allDocs={allDocs.Count}, alreadyIn={alreadyIn.Count}");

            if (allDocs.Count == alreadyIn.Count)
            {
                await _dialogService.ShowMessageAsync(
                    "Thông báo",
                    "Tất cả tài liệu đã có trong bộ sưu tập này.");
                return;
            }

            // Show visual document picker (search + checkbox list)
            var selected = await _dialogService.ShowDocumentPickerAsync(
                SelectedCollection.Name,
                allDocs,
                alreadyIn);

            Debug.WriteLine($"[CollectionVM] Picker returned: {selected?.Count.ToString() ?? "null (cancelled)"}");

            if (selected == null || selected.Count == 0) return;

            // Snapshot before reload (LoadCollections resets SelectedCollection)
            int selectedId = SelectedCollection.Id;
            string collectionName = SelectedCollection.Name;

            // Batch-add selected documents
            int addedCount = 0;
            foreach (var doc in selected)
            {
                bool ok = _collectionRepo.AddDocument(selectedId, doc.Id);
                if (ok) addedCount++;
                Debug.WriteLine($"[CollectionVM] AddDocument({doc.Id}, '{doc.Ten}') = {ok}");
            }

            // Refresh document list before reload
            OnSelectedCollectionChanged(SelectedCollection);

            // Reload collections list and restore selection
            LoadCollections();
            SelectedCollection = Collections.FirstOrDefault(c => c.Id == selectedId);

            string msg = addedCount == 1
                ? $"Đã thêm 1 tài liệu vào '{collectionName}'"
                : $"Đã thêm {addedCount} tài liệu vào '{collectionName}'";
            await _dialogService.ShowMessageAsync("Thành công", msg);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CollectionVM] AddDocumentToCollectionAsync ERROR: {ex.GetType().Name}: {ex.Message}");
            await _dialogService.ShowErrorAsync("Lỗi", $"Không thể thêm tài liệu: {ex.Message}");
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
            ? $"Gỡ '{targets[0].Ten}' khỏi bộ sưu tập?"
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
