using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class RelatedDocumentsModel : ModelBase
{
    private readonly IDocument _repository;
    private readonly IRelatedDocument _relatedDocRepo;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    [ObservableProperty] private int _documentId;
    [ObservableProperty] private string _documentName = string.Empty;
    [ObservableProperty] private ObservableCollection<RelatedDocItem> _relatedDocuments = new();
    [ObservableProperty] private ObservableCollection<StudyDocument> _availableDocuments = new();
    [ObservableProperty] private StudyDocument? _selectedAvailableDoc;
    [ObservableProperty] private string _selectedRelationType = "liên quan";

    public List<string> RelationTypes { get; } = new()
    {
        "liên quan",
        "tham khảo",
        "bổ sung",
        "tiền đề",
        "kế tiếp"
    };

    public RelatedDocumentsModel(IDocument repository, IRelatedDocument relatedDocRepo, IDialogService dialogService, INavigationService navigationService)
    {
        _repository = repository;
        _relatedDocRepo = relatedDocRepo;
        _dialogService = dialogService;
        _navigationService = navigationService;
    }

    public void Load(int docId, string docName)
    {
        DocumentId = docId;
        DocumentName = docName;
        RefreshRelated();
        LoadAvailable();
    }

    private void RefreshRelated()
    {
        RelatedDocuments.Clear();
        var related = _relatedDocRepo.GetRelated(DocumentId);
        foreach (var (doc, relId, relType) in related)
        {
            RelatedDocuments.Add(new RelatedDocItem
            {
                Document = doc,
                RelationId = relId,
                RelationType = relType
            });
        }
    }

    private void LoadAvailable()
    {
        var allDocs = _repository.GetAll();
        var relatedIds = RelatedDocuments.Select(r => r.Document.Id).ToHashSet();
        relatedIds.Add(DocumentId); // Exclude self

        AvailableDocuments = new ObservableCollection<StudyDocument>(
            allDocs.Where(d => !relatedIds.Contains(d.Id)));
    }

    [RelayCommand]
    private void AddRelation()
    {
        if (SelectedAvailableDoc == null) return;

        _relatedDocRepo.AddRelation(DocumentId, SelectedAvailableDoc.Id, SelectedRelationType);
        RefreshRelated();
        LoadAvailable();
    }

    [RelayCommand]
    private async Task RemoveRelationAsync(RelatedDocItem? item)
    {
        if (item == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync("Xác nhận",
            $"Gỡ liên kết với '{item.Document.Name}'?");
        if (!confirmed) return;

        _relatedDocRepo.RemoveRelation(item.RelationId);
        RefreshRelated();
        LoadAvailable();
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();
}

public class RelatedDocItem
{
    public StudyDocument Document { get; set; } = new();
    public int RelationId { get; set; }
    public string RelationType { get; set; } = "related";
}
