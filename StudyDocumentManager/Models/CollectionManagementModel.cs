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
    private readonly IDocumentRepository _repository;
    private readonly ICollectionRepository _collectionRepo;
    private readonly IDialogService _dialogService;
    private readonly ICustomDialogService _customDialogService;
    private readonly ILocalizationService _loc;

    [ObservableProperty] private ObservableCollection<CollectionItem> _collections = [];
    [ObservableProperty] private CollectionItem? _selectedCollection;
    [ObservableProperty] private ObservableCollection<StudyDocument> _documentsInCollection = [];
    [ObservableProperty] private StudyDocument? _selectedDocumentInCollection;
    [ObservableProperty] private ObservableCollection<StudyDocument> _allDocuments = [];
    [ObservableProperty] private IList _selectedDocumentsInCollection = new List<StudyDocument>();

    public CollectionManagementModel(IDocumentRepository repository, ICollectionRepository collectionRepo, IDialogService dialogService, ICustomDialogService customDialogService, ILocalizationService loc)
    {
        _repository = repository;
        _collectionRepo = collectionRepo;
        _dialogService = dialogService;
        _customDialogService = customDialogService;
        _loc = loc;
        LoadCollections();
    }

    private void LoadCollections()
    {
        int? selectedId = SelectedCollection?.Id;
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
            SelectedCollection = selectedId is int id
                ? Collections.FirstOrDefault(c => c.Id == id)
                : null;
        }
        catch (Exception ex)
        {
            _ = _dialogService.ShowErrorAsync(_loc["Dialog_Error"],
                string.Format(_loc["Collection_LoadError"], ex.Message));
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
            var name = await _dialogService.ShowInputAsync(_loc["Collection_CreateTitle"], _loc["Collection_CreateLabel"]);
            if (string.IsNullOrWhiteSpace(name)) return;

            var trimmed = name.Trim();
            if (_collectionRepo.Create(trimmed) <= 0)
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                return;
            }

            LoadCollections();
            await _dialogService.ShowMessageAsync(_loc["Dialog_Success"],
                string.Format(_loc["Collection_Created"], trimmed));
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"],
                string.Format(_loc["Collection_CreateError"], ex.Message));
        }
    }

    [RelayCommand]
    private async Task RenameCollectionAsync()
    {
        if (SelectedCollection == null) return;

        var collection = SelectedCollection;
        var newName = await _dialogService.ShowInputAsync(_loc["Collection_RenameTitle"], _loc["Category_NewNameLabel"], collection.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == collection.Name) return;

        try
        {
            if (!_collectionRepo.Update(collection.Id, newName.Trim()))
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                return;
            }

            LoadCollections();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"],
                string.Format(_loc["Collection_LoadError"], ex.Message));
        }
    }

    [RelayCommand]
    private async Task DeleteCollectionAsync()
    {
        if (SelectedCollection == null) return;

        var collectionId = SelectedCollection.Id;
        var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
            string.Format(_loc["Collection_ConfirmDelete"], SelectedCollection.Name),
            _loc["Action_Delete"], isDanger: true);
        if (!confirmed) return;

        try
        {
            if (!_collectionRepo.Delete(collectionId))
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                return;
            }

            SelectedCollection = null;
            LoadCollections();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"],
                string.Format(_loc["Collection_LoadError"], ex.Message));
        }
    }

    [RelayCommand]
    private async Task AddDocumentToCollectionAsync()
    {
        if (SelectedCollection == null) return;

        try
        {
            var collectionId = SelectedCollection.Id;
            var collectionName = SelectedCollection.Name;
            var allDocs = _repository.GetAll();
            var alreadyIn = _collectionRepo.GetDocuments(collectionId)
                                          .Select(d => d.Id)
                                          .ToList();

            if (allDocs.Count > 0 && allDocs.Count == alreadyIn.Count)
            {
                await _dialogService.ShowMessageAsync(
                    _loc["Dialog_Notice"],
                    _loc["Collection_AllDocsAlreadyIn"]);
                return;
            }

            var selected = await _customDialogService.ShowDocumentPickerAsync(
                collectionName,
                allDocs,
                alreadyIn);

            if (selected == null || selected.Count == 0) return;

            int addedCount = 0;
            bool failed = false;
            foreach (var doc in selected)
            {
                try
                {
                    if (_collectionRepo.AddDocument(collectionId, doc.Id))
                        addedCount++;
                    else
                        failed = true;
                }
                catch
                {
                    failed = true;
                }
            }

            LoadCollections();
            if (failed || addedCount != selected.Count)
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                return;
            }

            await _dialogService.ShowMessageAsync(_loc["Dialog_Success"],
                string.Format(_loc["Collection_AddedDocs"], addedCount, collectionName));
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"],
                string.Format(_loc["Collection_AddError"], ex.Message));
        }
    }

    [RelayCommand]
    private async Task RemoveDocumentFromCollectionAsync(StudyDocument? doc)
    {
        if (SelectedCollection == null || doc == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
            string.Format(_loc["Collection_ConfirmRemoveDoc"], doc.Name));
        if (!confirmed) return;

        try
        {
            int selectedId = SelectedCollection.Id;
            if (!_collectionRepo.RemoveDocument(selectedId, doc.Id))
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                return;
            }

            LoadCollections();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"],
                string.Format(_loc["Collection_LoadError"], ex.Message));
        }
    }

    [RelayCommand]
    private async Task RemoveSelectedDocumentsAsync()
    {
        if (SelectedCollection == null) return;

        var targets = SelectedDocumentsInCollection.Cast<StudyDocument>().ToList();
        if (targets.Count == 0) return;

        string confirmMsg = targets.Count == 1
            ? string.Format(_loc["Collection_ConfirmRemoveDoc"], targets[0].Name)
            : string.Format(_loc["Collection_ConfirmRemoveDocs"], targets.Count);

        bool confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"], confirmMsg);
        if (!confirmed) return;

        int selectedId = SelectedCollection.Id;
        int removedCount = 0;
        bool failed = false;
        foreach (var document in targets)
        {
            try
            {
                if (_collectionRepo.RemoveDocument(selectedId, document.Id))
                    removedCount++;
                else
                    failed = true;
            }
            catch
            {
                failed = true;
            }
        }

        LoadCollections();
        SelectedDocumentsInCollection = new List<StudyDocument>();
        if (failed || removedCount != targets.Count)
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
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
