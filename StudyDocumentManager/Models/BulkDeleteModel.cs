using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class BulkDeleteModel : ModelBase
{
    private readonly IDocumentRepository _repository;
    private readonly IBulkOperationRepository _bulkRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _loc;

    private const string AllFilter = "All";

    [ObservableProperty] private List<SelectableDocument> _documents = new();
    [ObservableProperty] private string _searchKeyword = string.Empty;

    [ObservableProperty] private List<string> _subjects = new();
    [ObservableProperty] private List<string> _types = new();
    [ObservableProperty] private string _selectedSubject = AllFilter;
    [ObservableProperty] private string _selectedType = AllFilter;

    [ObservableProperty] private List<string> _availableSubjects = new();
    [ObservableProperty] private string? _newSubjectValue;

    [ObservableProperty] private string _statusText = "";
    public int SelectedCount => Documents.Count(d => d.IsSelected);
    public string SelectedCountText => string.Format(_loc["Bulk_SelectedCount"], SelectedCount);
    public bool HasSelection => SelectedCount > 0;

    public BulkDeleteModel(IDocumentRepository repository, IBulkOperationRepository bulkRepo, ICategoryRepository categoryRepo, IDialogService dialogService, INavigationService navigationService, ILocalizationService loc)
    {
        _repository = repository;
        _bulkRepo = bulkRepo;
        _categoryRepo = categoryRepo;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _loc = loc;
    }

    /// <summary>
    /// Called from code-behind after the view is fully loaded (deferred initialization).
    /// </summary>
    public void Initialize()
    {
        LoadFilterData();
        LoadData();
    }

    private void LoadFilterData()
    {
        var subjects = _categoryRepo.GetAllSubjects();
        var types = _categoryRepo.GetAllTypes();

        var subjectList = new List<string> { _loc["Filter_AllItems"] };
        subjectList.AddRange(subjects);
        Subjects = subjectList;

        var typeList = new List<string> { _loc["Filter_AllItems"] };
        typeList.AddRange(types);
        Types = typeList;

        SelectedSubject = _loc["Filter_AllItems"];
        SelectedType = _loc["Filter_AllItems"];

        AvailableSubjects = new List<string>(subjects);
    }

    private void LoadData(HashSet<int>? selectedIds = null)
    {
        foreach (var document in Documents)
            document.PropertyChanged -= OnSelectableDocumentPropertyChanged;

        var docs = _repository.GetAll();

        var allLabel = _loc["Filter_AllItems"];
        if (SelectedSubject != allLabel)
            docs = docs.Where(d => d.Subject == SelectedSubject).ToList();
        if (SelectedType != allLabel)
            docs = docs.Where(d => d.Type == SelectedType).ToList();
        if (!string.IsNullOrWhiteSpace(SearchKeyword))
            docs = docs.Where(d => d.Name.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase)
                || (d.Notes ?? "").Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase)).ToList();

        Documents = docs.Select(d => new SelectableDocument
        {
            Document = d,
            IsSelected = selectedIds?.Contains(d.Id) == true
        }).ToList();

        foreach (var document in Documents)
            document.PropertyChanged += OnSelectableDocumentPropertyChanged;

        StatusText = string.Format(_loc["Status_Showing"], Documents.Count);
        NotifySelectionChanged();
    }

    private void OnSelectableDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectableDocument.IsSelected))
            NotifySelectionChanged();
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    private void Search() => LoadData();

    partial void OnSelectedSubjectChanged(string value) => LoadData();
    partial void OnSelectedTypeChanged(string value) => LoadData();

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var selected = Documents.Where(d => d.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Bulk_NoDocSelected"]);
            return;
        }

        try
        {
            var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
                string.Format(_loc["Bulk_ConfirmDelete"], selected.Count),
                _loc["Action_Delete"], isDanger: true);
            if (!confirmed) return;

            var deleted = _bulkRepo.BulkSoftDelete(selected.Select(s => s.Document.Id).ToList());
            if (deleted != selected.Count)
            {
                LoadData(selected.Select(item => item.Document.Id).ToHashSet());
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"],
                    string.Format(_loc["Operation_Partial"], deleted, selected.Count));
                return;
            }

            await _dialogService.ShowMessageAsync(_loc["Dialog_Complete"],
                string.Format(_loc["Bulk_DeleteDone"], deleted));
            _navigationService.NavigateTo("dashboard");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private async Task MarkImportantAsync()
    {
        var selected = Documents.Where(d => d.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Bulk_NoDocSelected"]);
            return;
        }

        try
        {
            var ids = selected.Select(s => s.Document.Id).ToHashSet();
            var updated = _bulkRepo.BulkToggleImportant([.. ids], true);
            if (updated != selected.Count)
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"],
                    string.Format(_loc["Operation_Partial"], updated, selected.Count));
                return;
            }

            await _dialogService.ShowMessageAsync(_loc["Dialog_Complete"],
                string.Format(_loc["Bulk_MarkImportantDone"], updated));
            LoadData(ids);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private async Task ChangeSubjectAsync()
    {
        var selected = Documents.Where(d => d.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Bulk_NoDocSelected"]);
            return;
        }

        if (string.IsNullOrWhiteSpace(NewSubjectValue))
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Bulk_SelectNewSubject"]);
            return;
        }

        try
        {
            var confirmed = await _dialogService.ShowConfirmAsync(_loc["Bulk_ChangeSubjectTitle"],
                string.Format(_loc["Bulk_ConfirmChangeSubject"], selected.Count, NewSubjectValue));
            if (!confirmed) return;

            var ids = selected.Select(s => s.Document.Id).ToHashSet();
            var updated = _bulkRepo.BulkUpdateSubject([.. ids], NewSubjectValue);
            if (updated != selected.Count)
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"],
                    string.Format(_loc["Operation_Partial"], updated, selected.Count));
                return;
            }

            await _dialogService.ShowMessageAsync(_loc["Dialog_Complete"],
                string.Format(_loc["Bulk_ChangeSubjectDone"], updated));
            LoadData(ids);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var d in Documents)
            d.IsSelected = true;
        NotifySelectionChanged();
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var d in Documents)
            d.IsSelected = false;
        NotifySelectionChanged();
    }

    [RelayCommand]
    private void Cancel() => _navigationService.NavigateTo("dashboard");
}

public partial class SelectableDocument : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private StudyDocument _document = new();
}
