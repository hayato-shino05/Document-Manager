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
    private readonly ILocalizationService _loc;

    [ObservableProperty] private ObservableCollection<CollectionItem> _collections = [];
    [ObservableProperty] private CollectionItem? _selectedCollection;
    [ObservableProperty] private ObservableCollection<StudyDocument> _documentsInCollection = [];
    [ObservableProperty] private StudyDocument? _selectedDocumentInCollection;
    [ObservableProperty] private ObservableCollection<StudyDocument> _allDocuments = [];
    [ObservableProperty] private IList _selectedDocumentsInCollection = new List<StudyDocument>();

    public CollectionManagementModel(IDocument repository, Core.Interfaces.ICollection collectionRepo, IDialogService dialogService, ICustomDialogService customDialogService, ILocalizationService loc)
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

            if (!string.IsNullOrWhiteSpace(name))
            {
                _collectionRepo.Create(name);
                LoadCollections();
                await _dialogService.ShowMessageAsync(_loc["Dialog_Success"],
                    string.Format(_loc["Collection_Created"], name));
            }
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

        var newName = await _dialogService.ShowInputAsync(_loc["Collection_RenameTitle"], _loc["Category_NewNameLabel"], SelectedCollection.Name);
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

        var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
            string.Format(_loc["Collection_ConfirmDelete"], SelectedCollection.Name),
            _loc["Action_Delete"], isDanger: true);
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
                    _loc["Dialog_Notice"],
                    _loc["Collection_AllDocsAlreadyIn"]);
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
            ? string.Format(_loc["Collection_ConfirmRemoveDoc"], targets[0].Name)
            : string.Format(_loc["Collection_ConfirmRemoveDocs"], targets.Count);

        bool confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"], confirmMsg);
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
