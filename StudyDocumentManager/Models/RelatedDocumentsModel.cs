using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class RelatedDocumentsModel : ModelBase
{
    private readonly IDocumentRepository _repository;
    private readonly IRelatedDocumentRepository _relatedDocRepo;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _loc;

    [ObservableProperty] private int _documentId;
    [ObservableProperty] private string _documentName = string.Empty;
    [ObservableProperty] private ObservableCollection<RelatedDocItem> _relatedDocuments = new();
    [ObservableProperty] private ObservableCollection<StudyDocument> _availableDocuments = new();
    [ObservableProperty] private StudyDocument? _selectedAvailableDoc;
    [ObservableProperty] private string _selectedRelationType = "related";

    // DB stores English keys; UI displays localized labels
    public List<string> RelationTypes { get; } = new()
    {
        "related",
        "reference",
        "supplement",
        "prerequisite",
        "sequel"
    };

    public RelatedDocumentsModel(IDocumentRepository repository, IRelatedDocumentRepository relatedDocRepo, IDialogService dialogService, INavigationService navigationService, ILocalizationService loc)
    {
        _repository = repository;
        _relatedDocRepo = relatedDocRepo;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _loc = loc;
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
        relatedIds.Add(DocumentId);

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

        var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
            string.Format(_loc["Related_ConfirmRemove"], item.Document.Name));
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
