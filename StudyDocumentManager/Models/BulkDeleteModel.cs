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
    private readonly ICustomDialogService? _customDialogs;
    private readonly ICollectionRepository? _collectionRepo;
    private readonly IUndoService _undo;
    private readonly IUndoApplier? _undoApplier;

    private const string AllFilter = "All";

    [ObservableProperty] private List<SelectableDocument> _documents = new();
    [ObservableProperty] private string _searchKeyword = string.Empty;

    [ObservableProperty] private List<string> _subjects = new();
    [ObservableProperty] private List<string> _types = new();
    [ObservableProperty] private string _selectedSubject = AllFilter;
    [ObservableProperty] private string _selectedType = AllFilter;

    [ObservableProperty] private List<string> _availableSubjects = new();
    [ObservableProperty] private string? _newSubjectValue;

    [ObservableProperty] private bool _enableSubject;
    [ObservableProperty] private string _newSubject = "";
    [ObservableProperty] private bool _enableType;
    [ObservableProperty] private string? _newType;
    [ObservableProperty] private bool _enableTags;
    [ObservableProperty] private string? _newTags;
    [ObservableProperty] private bool _enableImportant;
    [ObservableProperty] private bool _newImportant;
    [ObservableProperty] private bool _enableDeadline;
    [ObservableProperty] private DateTimeOffset? _newDeadline;
    [ObservableProperty] private bool _enableStatus;
    [ObservableProperty] private string _newStatus = DocumentStatus.Unread;
    [ObservableProperty] private List<StatusOption> _statusOptions = [];
    [ObservableProperty] private StatusOption? _selectedStatusOption;
    [ObservableProperty] private bool _enableCollectionAdd;
    [ObservableProperty] private int? _selectedCollectionId;
    [ObservableProperty] private List<BulkCollectionOption> _collectionOptions = [];
    [ObservableProperty] private BulkCollectionOption? _selectedCollectionOption;

    [ObservableProperty] private string _statusText = "";
    public int SelectedCount => Documents.Count(d => d.IsSelected);
    public string SelectedCountText => string.Format(_loc["Bulk_SelectedCount"], SelectedCount);
    public bool HasSelection => SelectedCount > 0;
    public bool HasAnyEnabledChange =>
        (EnableSubject && !string.IsNullOrWhiteSpace(NewSubject))
        || (EnableType && !string.IsNullOrWhiteSpace(NewType))
        || (EnableTags && !string.IsNullOrWhiteSpace(NewTags))
        || EnableImportant
        || (EnableDeadline && NewDeadline.HasValue)
        || EnableStatus
        || (EnableCollectionAdd && SelectedCollectionId.HasValue);

    public BulkDeleteModel(IDocumentRepository repository, IBulkOperationRepository bulkRepo, ICategoryRepository categoryRepo, IDialogService dialogService, INavigationService navigationService, ILocalizationService loc,
        ICustomDialogService? customDialogService = null, ICollectionRepository? collectionRepo = null, IUndoService? undoService = null, IUndoApplier? undoApplier = null)
    {
        _repository = repository;
        _bulkRepo = bulkRepo;
        _categoryRepo = categoryRepo;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _loc = loc;
        _customDialogs = customDialogService;
        _collectionRepo = collectionRepo;
        _undo = undoService ?? new UndoService();
        _undoApplier = undoApplier;
    }

    /// <summary>
    /// Called from code-behind after the view is fully loaded (deferred initialization).
    /// </summary>
    public void Initialize()
    {
        LoadFilterData();
        LoadBulkEditOptions();
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

    private void LoadBulkEditOptions()
    {
        StatusOptions = [.. DocumentStatus.All.Select(s => new StatusOption(s, GetStatusLabel(s)))];
        SelectedStatusOption = StatusOptions.FirstOrDefault(o => o.Value == NewStatus) ?? StatusOptions.FirstOrDefault();

        var options = new List<BulkCollectionOption> { new() { Id = null, Label = _loc["BE_CollectionNone"] } };
        if (_collectionRepo != null)
            foreach (var collection in _collectionRepo.GetAll())
                options.Add(new BulkCollectionOption { Id = collection.Id, Label = collection.Name });
        CollectionOptions = options;
        SelectedCollectionOption = options[0];
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
        ApplyBulkEditCommand.NotifyCanExecuteChanged();
    }

    private void NotifyBulkEditStateChanged()
    {
        OnPropertyChanged(nameof(HasAnyEnabledChange));
        ApplyBulkEditCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedStatusOptionChanged(StatusOption? value)
    {
        if (value != null)
            NewStatus = value.Value;
        NotifyBulkEditStateChanged();
    }

    partial void OnSelectedCollectionOptionChanged(BulkCollectionOption? value)
    {
        SelectedCollectionId = value?.Id;
    }

    partial void OnEnableSubjectChanged(bool value) => NotifyBulkEditStateChanged();
    partial void OnNewSubjectChanged(string value) => NotifyBulkEditStateChanged();
    partial void OnEnableTypeChanged(bool value) => NotifyBulkEditStateChanged();
    partial void OnNewTypeChanged(string? value) => NotifyBulkEditStateChanged();
    partial void OnEnableTagsChanged(bool value) => NotifyBulkEditStateChanged();
    partial void OnNewTagsChanged(string? value) => NotifyBulkEditStateChanged();
    partial void OnEnableImportantChanged(bool value) => NotifyBulkEditStateChanged();
    partial void OnNewImportantChanged(bool value) => NotifyBulkEditStateChanged();
    partial void OnEnableDeadlineChanged(bool value) => NotifyBulkEditStateChanged();
    partial void OnNewDeadlineChanged(DateTimeOffset? value) => NotifyBulkEditStateChanged();
    partial void OnEnableStatusChanged(bool value) => NotifyBulkEditStateChanged();
    partial void OnNewStatusChanged(string value) => NotifyBulkEditStateChanged();
    partial void OnEnableCollectionAddChanged(bool value) => NotifyBulkEditStateChanged();
    partial void OnSelectedCollectionIdChanged(int? value) => NotifyBulkEditStateChanged();

    [RelayCommand]
    private void Search() => LoadData();

    partial void OnSelectedSubjectChanged(string value) => LoadData();
    partial void OnSelectedTypeChanged(string value) => LoadData();

    private bool CanApplyBulkEdit() => HasSelection && HasAnyEnabledChange;

    [RelayCommand(CanExecute = nameof(CanApplyBulkEdit))]
    private async Task ApplyBulkEditAsync()
    {
        var selected = Documents.Where(d => d.IsSelected).ToList();
        if (selected.Count == 0)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["BE_NoSelectionHint"]);
            return;
        }

        var changes = BuildChanges();
        if (!changes.HasAnyChange)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["BE_NoFieldsEnabledHint"]);
            return;
        }

        try
        {
            var ids = selected.Select(s => s.Document.Id).ToList();
            var originals = new List<StudyDocument>();
            foreach (var id in ids)
            {
                var snapshot = _repository.GetById(id);
                if (snapshot != null)
                    originals.Add(snapshot);
            }

            if (originals.Count == 0)
                return;

            HashSet<int> existingCollectionMembers = changes.AddToCollectionId is int collectionId && _collectionRepo != null
                ? _collectionRepo.GetDocuments(collectionId).Select(document => document.Id).ToHashSet()
                : [];

            var confirmed = await ConfirmPreviewAsync(originals.Count, BuildPreviewPairs(changes));
            if (!confirmed) return;

            var outcome = _bulkRepo.BulkEditMetadata([.. originals.Select(o => o.Id)], changes);

            if (outcome.Succeeded > 0)
            {
                var addedMemberships = changes.AddToCollectionId is int addedCollectionId
                    ? outcome.Items
                        .Where(item => item.Success && !existingCollectionMembers.Contains(item.DocumentId))
                        .Select(item => new CollectionMembership(addedCollectionId, item.DocumentId))
                        .ToList()
                    : [];

                _undo.Push(new UndoEntry
                {
                    DescriptionKey = "BE_UndoDescription",
                    DescriptionArgs = [outcome.Succeeded],
                    Originals = originals,
                    AddedCollectionMemberships = addedMemberships,
                    CreatedAt = DateTime.Now
                });
                UndoLastCommand.NotifyCanExecuteChanged();
            }

            if (outcome.Succeeded == outcome.Requested)
            {
                await _dialogService.ShowMessageAsync(_loc["Dialog_Complete"],
                    string.Format(_loc["BE_Result_AllSuccess"], outcome.Succeeded, outcome.Requested));
            }
            else
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], BuildPartialReport(outcome));
            }

            LoadData([.. ids]);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    private bool CanUndoLast() => _undo.CanUndo;

    [RelayCommand(CanExecute = nameof(CanUndoLast))]
    private async Task UndoLastAsync()
    {
        var entry = _undo.Peek();
        if (entry == null) return;
        UndoLastCommand.NotifyCanExecuteChanged();

        try
        {
            if (_undoApplier != null)
            {
                _undoApplier.ApplyLast();
                LoadData();
            }
            else
            {
                _ = _undo.Pop() ?? throw new InvalidOperationException("Nothing to undo.");
                foreach (var original in entry.Originals)
                    _repository.Update(original);
                LoadData([.. entry.Originals.Select(o => o.Id)]);
            }

            var detail = string.Format(_loc[entry.DescriptionKey], entry.DescriptionArgs ?? []);
            StatusText = string.Format(_loc["UN_Applied"], detail);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    private BulkEditChanges BuildChanges() => new()
    {
        Subject = EnableSubject && !string.IsNullOrWhiteSpace(NewSubject) ? NewSubject.Trim() : null,
        Type = EnableType && !string.IsNullOrWhiteSpace(NewType) ? NewType!.Trim() : null,
        Tags = EnableTags && !string.IsNullOrWhiteSpace(NewTags) ? NewTags!.Trim() : null,
        IsImportant = EnableImportant ? NewImportant : null,
        Deadline = EnableDeadline ? NewDeadline?.DateTime : null,
        Status = EnableStatus && !string.IsNullOrWhiteSpace(NewStatus) ? NewStatus : null,
        AddToCollectionId = EnableCollectionAdd && SelectedCollectionId.HasValue ? SelectedCollectionId : null
    };

    private List<(string FieldLabel, string NewValue)> BuildPreviewPairs(BulkEditChanges changes)
    {
        var pairs = new List<(string FieldLabel, string NewValue)>();
        if (changes.Subject != null) pairs.Add((_loc["BE_Field_Subject"], changes.Subject));
        if (changes.Type != null) pairs.Add((_loc["BE_Field_Type"], changes.Type));
        if (changes.Tags != null) pairs.Add((_loc["BE_Field_Tags"], changes.Tags));
        if (changes.IsImportant.HasValue)
            pairs.Add((_loc["BE_Field_Important"], changes.IsImportant.Value ? _loc["Dashboard_CsvYes"] : _loc["Dashboard_CsvNo"]));
        if (changes.Deadline.HasValue) pairs.Add((_loc["BE_Field_Deadline"], changes.Deadline.Value.ToString("yyyy/MM/dd")));
        if (changes.Status != null) pairs.Add((_loc["BE_Field_Status"], GetStatusLabel(changes.Status)));
        if (changes.AddToCollectionId.HasValue)
            pairs.Add((_loc["BE_Field_CollectionAdd"], ResolveCollectionLabel(changes.AddToCollectionId.Value)));
        return pairs;
    }

    private async Task<bool> ConfirmPreviewAsync(int affectedCount, List<(string FieldLabel, string NewValue)> changes)
    {
        if (_customDialogs != null)
            return await _customDialogs.ShowBulkEditPreviewAsync(affectedCount, changes);

        var summary = string.Join(Environment.NewLine, changes.Select(c => $"{c.FieldLabel}: {c.NewValue}"));
        return await _dialogService.ShowConfirmAsync(_loc["BE_PreviewTitle"],
            string.Format(_loc["BE_PreviewAffected"], affectedCount) + Environment.NewLine + summary,
            _loc["BE_ConfirmApply"], isDanger: true);
    }

    private string BuildPartialReport(BulkEditOutcome outcome)
    {
        var failedIds = outcome.FailedIds;
        var failedNames = failedIds
            .Select(id => _repository.GetById(id)?.Name ?? $"#{id}")
            .ToList();

        return string.Format(_loc["BE_Result_Partial"], outcome.Succeeded, outcome.Requested)
            + Environment.NewLine + _loc["BE_FailedItemsHeader"]
            + Environment.NewLine + string.Join(Environment.NewLine, failedNames);
    }

    private string ResolveCollectionLabel(int collectionId)
        => CollectionOptions.FirstOrDefault(o => o.Id == collectionId)?.Label ?? $"#{collectionId}";

    private string GetStatusLabel(string status) => status switch
    {
        DocumentStatus.Unread => _loc["DS_Kind_Unread"],
        DocumentStatus.InProgress => _loc["DS_Kind_InProgress"],
        DocumentStatus.Read => _loc["DS_Kind_Read"],
        DocumentStatus.NeedsAction => _loc["DS_Kind_NeedsAction"],
        DocumentStatus.Completed => _loc["DS_Kind_Completed"],
        DocumentStatus.Archived => _loc["DS_Kind_Archived"],
        _ => status
    };

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

            var deletedIds = selected.Select(s => s.Document.Id).ToList();
            var originals = new List<StudyDocument>();
            foreach (var id in deletedIds)
            {
                var snapshot = _repository.GetById(id);
                if (snapshot != null)
                    originals.Add(snapshot);
            }

            var deleted = _bulkRepo.BulkSoftDelete(deletedIds);
            if (deleted != selected.Count)
            {
                LoadData(selected.Select(item => item.Document.Id).ToHashSet());
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"],
                    string.Format(_loc["Operation_Partial"], deleted, selected.Count));
                return;
            }

            _undo.Push(new UndoEntry
            {
                DescriptionKey = "UN_DeletedDocuments",
                DescriptionArgs = [deleted],
                Originals = originals,
                DeletedIds = deletedIds,
                CreatedAt = DateTime.Now
            });
            UndoLastCommand.NotifyCanExecuteChanged();

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

public sealed class BulkCollectionOption
{
    public int? Id { get; init; }
    public string Label { get; init; } = string.Empty;
}
