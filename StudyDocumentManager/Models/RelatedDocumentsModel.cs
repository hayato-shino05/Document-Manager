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
    [ObservableProperty] private string _selectedRelationType = string.Empty;

    private static readonly string[] CanonicalRelationTypes =
    [
        "related",
        "reference",
        "supplement",
        "prerequisite",
        "sequel"
    ];

    public List<string> RelationTypes { get; private set; } = [];

    public RelatedDocumentsModel(IDocumentRepository repository, IRelatedDocumentRepository relatedDocRepo, IDialogService dialogService, INavigationService navigationService, ILocalizationService loc)
    {
        _repository = repository;
        _relatedDocRepo = relatedDocRepo;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _loc = loc;
        _loc.LanguageChanged += (_, _) => RefreshLocalizedStrings();
        RefreshRelationTypes();
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
                CanonicalRelationType = relType,
                RelationType = LocalizeRelationType(relType)
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

    private void RefreshLocalizedStrings()
    {
        RefreshRelationTypes();
        foreach (var item in RelatedDocuments)
            item.RelationType = LocalizeRelationType(item.CanonicalRelationType);
    }

    private void RefreshRelationTypes()
    {
        var selectedCanonical = ToCanonicalRelationType(SelectedRelationType);
        RelationTypes = [.. CanonicalRelationTypes.Select(LocalizeRelationType)];
        OnPropertyChanged(nameof(RelationTypes));
        SelectedRelationType = LocalizeRelationType(selectedCanonical);
    }

    private string LocalizeRelationType(string canonical)
        => _loc[$"RelatedDocs_RelationType_{canonical}"];

    private string ToCanonicalRelationType(string value)
    {
        foreach (var canonical in CanonicalRelationTypes)
        {
            if (value == canonical || value == LocalizeRelationType(canonical))
                return canonical;
        }

        return "related";
    }

    [RelayCommand]
    private async Task AddRelation()
    {
        var document = SelectedAvailableDoc;
        if (document == null) return;

        try
        {
            _relatedDocRepo.AddRelation(DocumentId, document.Id, ToCanonicalRelationType(SelectedRelationType));
            SelectedAvailableDoc = null;
            RefreshRelated();
            LoadAvailable();
        }
        catch
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private async Task RemoveRelationAsync(RelatedDocItem? item)
    {
        if (item == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
            string.Format(_loc["Related_ConfirmRemove"], item.Document.Name));
        if (!confirmed) return;

        try
        {
            _relatedDocRepo.RemoveRelation(item.RelationId);
            RefreshRelated();
            LoadAvailable();
        }
        catch
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();
}

public class RelatedDocItem
{
    public StudyDocument Document { get; set; } = new();
    public int RelationId { get; set; }
    public string CanonicalRelationType { get; set; } = string.Empty;
    public string RelationType { get; set; } = string.Empty;
}
