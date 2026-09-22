using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class SmartViewsModel : ModelBase
{
    private readonly ISavedSearchRepository _savedSearchRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _loc;
    private bool _localizationSubscribed;
    private string _statusKey = "Status_Ready";
    private object[] _statusArguments = [];
    private int? _editingId;
    private string _editingOriginalName = string.Empty;

    [ObservableProperty] private ObservableCollection<SavedSearchRow> _savedViews = [];
    [ObservableProperty] private SavedSearchRow? _selectedSavedSearch;
    [ObservableProperty] private ObservableCollection<SmartViewKindOption> _kindOptions = [];
    [ObservableProperty] private SmartViewKindOption? _selectedKindOption;
    [ObservableProperty] private ObservableCollection<string> _subjects = [];
    [ObservableProperty] private ObservableCollection<string> _types = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasLoadError;
    [ObservableProperty] private string _statusText = string.Empty;

    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editorName = string.Empty;
    [ObservableProperty] private string _editorKeyword = string.Empty;
    [ObservableProperty] private string? _editorSubject;
    [ObservableProperty] private string? _editorType;
    [ObservableProperty] private DateTimeOffset? _editorFromDate;
    [ObservableProperty] private DateTimeOffset? _editorToDate;
    [ObservableProperty] private double? _editorMinSize;
    [ObservableProperty] private double? _editorMaxSize;
    [ObservableProperty] private bool _editorIsImportantOnly;
    [ObservableProperty] private int _editorRecentDays = 7;
    [ObservableProperty] private int _editorDeadlineDays = 7;

    public SmartViewsModel(
        ISavedSearchRepository savedSearchRepo,
        ICategoryRepository categoryRepo,
        IDialogService dialogService,
        INavigationService navigationService,
        ILocalizationService loc)
    {
        _savedSearchRepo = savedSearchRepo;
        _categoryRepo = categoryRepo;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _loc = loc;

        Subjects = new ObservableCollection<string>(_categoryRepo.GetAllSubjects());
        Types = new ObservableCollection<string>(_categoryRepo.GetAllTypes());
        RefreshKindLabels();
        StatusText = FormatLocalizedStatus();
        _loc.LanguageChanged += OnLanguageChanged;
        _localizationSubscribed = true;

        LoadViews();
    }

    public void AttachLocalization()
    {
        if (_localizationSubscribed)
            return;

        _loc.LanguageChanged += OnLanguageChanged;
        _localizationSubscribed = true;
        OnLanguageChanged(this, EventArgs.Empty);
    }

    public void DetachLocalization()
    {
        if (!_localizationSubscribed)
            return;

        _loc.LanguageChanged -= OnLanguageChanged;
        _localizationSubscribed = false;
    }

    public bool HasSelection => SelectedSavedSearch != null;
    public bool IsEmptyState => !IsLoading && !HasLoadError && SavedViews.Count == 0;
    public bool ShowNoSelectionHint => SelectedSavedSearch == null && !IsEditing;
    public bool IsStandardFieldsVisible => SelectedKindOption?.Key == SavedSearchKinds.Standard;
    public bool IsRecentDaysVisible => SelectedKindOption?.Key == SavedSearchKinds.RecentlyAdded;
    public bool IsDeadlineDaysVisible => SelectedKindOption?.Key == SavedSearchKinds.DueSoon;

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RefreshKindLabels();
        StatusText = FormatLocalizedStatus();
    }

    private void RefreshKindLabels()
    {
        var selectedKey = SelectedKindOption?.Key;
        KindOptions = new ObservableCollection<SmartViewKindOption>(BuildKindOptions());
        SelectedKindOption = KindOptions.FirstOrDefault(k => k.Key == selectedKey) ?? KindOptions[0];

        foreach (var row in SavedViews)
            row.KindLabel = GetKindLabel(row.Kind);
    }

    private List<SmartViewKindOption> BuildKindOptions() =>
    [
        new() { Key = SavedSearchKinds.Standard, Label = _loc["SV_Kind_Standard"] },
        new() { Key = SavedSearchKinds.Uncategorized, Label = _loc["SV_Kind_Uncategorized"] },
        new() { Key = SavedSearchKinds.MissingMetadata, Label = _loc["SV_Kind_MissingMetadata"] },
        new() { Key = SavedSearchKinds.MissingFile, Label = _loc["SV_Kind_MissingFile"] },
        new() { Key = SavedSearchKinds.RecentlyAdded, Label = _loc["SV_Kind_RecentlyAdded"] },
        new() { Key = SavedSearchKinds.Important, Label = _loc["SV_Kind_Important"] },
        new() { Key = SavedSearchKinds.DueSoon, Label = _loc["SV_Kind_DueSoon"] },
    ];

    private string GetKindLabel(string kind) => kind switch
    {
        SavedSearchKinds.Uncategorized => _loc["SV_Kind_Uncategorized"],
        SavedSearchKinds.MissingMetadata => _loc["SV_Kind_MissingMetadata"],
        SavedSearchKinds.MissingFile => _loc["SV_Kind_MissingFile"],
        SavedSearchKinds.RecentlyAdded => _loc["SV_Kind_RecentlyAdded"],
        SavedSearchKinds.Important => _loc["SV_Kind_Important"],
        SavedSearchKinds.DueSoon => _loc["SV_Kind_DueSoon"],
        _ => _loc["SV_Kind_Standard"]
    };

    private string FormatLocalizedStatus()
        => string.Format(_loc[_statusKey], _statusArguments);

    private void SetLocalizedStatus(string key, params object[] arguments)
    {
        _statusKey = key;
        _statusArguments = arguments;
        StatusText = FormatLocalizedStatus();
    }

    partial void OnSelectedSavedSearchChanged(SavedSearchRow? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowNoSelectionHint));
    }

    partial void OnIsEditingChanged(bool value)
        => OnPropertyChanged(nameof(ShowNoSelectionHint));

    partial void OnIsLoadingChanged(bool value)
        => OnPropertyChanged(nameof(IsEmptyState));

    partial void OnHasLoadErrorChanged(bool value)
        => OnPropertyChanged(nameof(IsEmptyState));

    partial void OnSelectedKindOptionChanged(SmartViewKindOption? value)
    {
        OnPropertyChanged(nameof(IsStandardFieldsVisible));
        OnPropertyChanged(nameof(IsRecentDaysVisible));
        OnPropertyChanged(nameof(IsDeadlineDaysVisible));
    }

    [RelayCommand]
    private void Refresh() => LoadViews();

    [RelayCommand]
    private void New()
    {
        _editingId = null;
        _editingOriginalName = string.Empty;
        EditorName = string.Empty;
        SelectedKindOption = KindOptions[0];
        EditorKeyword = string.Empty;
        EditorSubject = null;
        EditorType = null;
        EditorFromDate = null;
        EditorToDate = null;
        EditorMinSize = null;
        EditorMaxSize = null;
        EditorIsImportantOnly = false;
        EditorRecentDays = 7;
        EditorDeadlineDays = 7;
        IsEditing = true;
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedSavedSearch == null) return;

        var source = _savedSearchRepo.GetById(SelectedSavedSearch.Id);
        if (source == null) return;

        var criteria = SavedSearchCriteria.FromJson(source.CriteriaJson) ?? new SavedSearchCriteria();
        _editingId = source.Id;
        _editingOriginalName = source.Name.Trim();
        EditorName = source.Name;
        SelectedKindOption = KindOptions.FirstOrDefault(k => k.Key == criteria.Kind) ?? KindOptions[0];
        EditorKeyword = criteria.Keyword ?? string.Empty;
        EditorSubject = criteria.Subject;
        EditorType = criteria.Type;
        EditorFromDate = criteria.FromDate.HasValue ? new DateTimeOffset(criteria.FromDate.Value) : null;
        EditorToDate = criteria.ToDate.HasValue ? new DateTimeOffset(criteria.ToDate.Value) : null;
        EditorMinSize = criteria.MinSize;
        EditorMaxSize = criteria.MaxSize;
        EditorIsImportantOnly = criteria.IsImportant ?? false;
        EditorRecentDays = criteria.RecentDays;
        EditorDeadlineDays = criteria.DeadlineDays;
        IsEditing = true;
    }

    [RelayCommand]
    private void Save()
    {
        var trimmedName = EditorName.Trim();
        if (trimmedName.Length == 0)
        {
            SetLocalizedStatus("SV_NameRequired");
            return;
        }

        try
        {
            if (_editingId.HasValue)
            {
                var existing = _savedSearchRepo.GetById(_editingId.Value);
                if (existing == null)
                {
                    SetLocalizedStatus("Msg_Error");
                    return;
                }

                var renamed = trimmedName != _editingOriginalName;
                if (renamed && _savedSearchRepo.NameExists(trimmedName))
                {
                    SetLocalizedStatus("SV_NameExists");
                    return;
                }

                existing.Name = trimmedName;
                existing.CriteriaJson = BuildCriteria().ToJson();
                if (!_savedSearchRepo.Update(existing))
                {
                    SetLocalizedStatus("Msg_Error");
                    return;
                }
            }
            else
            {
                if (_savedSearchRepo.NameExists(trimmedName))
                {
                    SetLocalizedStatus("SV_NameExists");
                    return;
                }

                var addedId = _savedSearchRepo.Add(new SavedSearch
                {
                    Name = trimmedName,
                    CriteriaJson = BuildCriteria().ToJson(),
                    CreatedAt = DateTime.Now
                });
                if (addedId <= 0)
                {
                    SetLocalizedStatus("Msg_Error");
                    return;
                }
            }

            IsEditing = false;
            LoadViews();
            SetLocalizedStatus("SV_Saved");
        }
        catch
        {
            SetLocalizedStatus("Msg_Error");
        }
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private void Duplicate()
    {
        if (SelectedSavedSearch == null) return;

        try
        {
            var source = _savedSearchRepo.GetById(SelectedSavedSearch.Id);
            if (source == null) return;

            var candidate = source.Name;
            var suffix = 2;
            while (_savedSearchRepo.NameExists(candidate))
            {
                candidate = $"{source.Name} ({suffix})";
                suffix++;
            }

            var addedId = _savedSearchRepo.Add(new SavedSearch
            {
                Name = candidate,
                CriteriaJson = source.CriteriaJson,
                CreatedAt = DateTime.Now
            });
            if (addedId <= 0)
            {
                SetLocalizedStatus("Msg_Error");
                return;
            }

            LoadViews();
            SetLocalizedStatus("SV_Saved");
        }
        catch
        {
            SetLocalizedStatus("Msg_Error");
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedSavedSearch == null) return;

        var targetId = SelectedSavedSearch.Id;
        var targetName = SelectedSavedSearch.Name;
        try
        {
            var confirmed = await _dialogService.ShowConfirmAsync(
                _loc["Dialog_Confirm"],
                string.Format(_loc["SV_DeleteConfirm"], targetName),
                _loc["Action_Delete"], isDanger: true);
            if (!confirmed) return;

            if (!_savedSearchRepo.Delete(targetId))
            {
                SetLocalizedStatus("Msg_Error");
                return;
            }

            LoadViews();
            SetLocalizedStatus("SV_Deleted");
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            LoadViews();
            SetLocalizedStatus("Msg_Error");
        }
    }

    [RelayCommand]
    private void Open()
    {
        if (SelectedSavedSearch == null) return;
        SetLocalizedStatus("SV_Status_Applied", SelectedSavedSearch.Name);
        _navigationService.NavigateTo("run-smartview", SelectedSavedSearch.Id);
    }

    private void LoadViews()
    {
        IsLoading = true;
        SelectedSavedSearch = null;
        try
        {
            var data = _savedSearchRepo.GetAll();
            SavedViews = new ObservableCollection<SavedSearchRow>(
                data.Select(s =>
                {
                    var kind = ResolveKind(s.CriteriaJson);
                    return new SavedSearchRow
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Kind = kind,
                        KindLabel = GetKindLabel(kind)
                    };
                }));
            HasLoadError = false;
            SetLocalizedStatus("Status_TotalDocs", SavedViews.Count);
        }
        catch
        {
            SavedViews = [];
            HasLoadError = true;
            SetLocalizedStatus("SV_LoadError");
        }
        finally
        {
            IsLoading = false;
        }
        OnPropertyChanged(nameof(IsEmptyState));
    }

    private string ResolveKind(string criteriaJson)
        => SavedSearchCriteria.FromJson(criteriaJson)?.Kind ?? SavedSearchKinds.Standard;

    private SavedSearchCriteria BuildCriteria() => new()
    {
        Kind = SelectedKindOption?.Key ?? SavedSearchKinds.Standard,
        Keyword = NullIfEmpty(EditorKeyword),
        Subject = NullIfEmpty(EditorSubject),
        Type = NullIfEmpty(EditorType),
        FromDate = EditorFromDate?.DateTime,
        ToDate = EditorToDate?.DateTime,
        MinSize = EditorMinSize,
        MaxSize = EditorMaxSize,
        IsImportant = EditorIsImportantOnly ? true : null,
        RecentDays = EditorRecentDays,
        DeadlineDays = EditorDeadlineDays
    };

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public class SavedSearchRow
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = SavedSearchKinds.Standard;
    public string KindLabel { get; set; } = string.Empty;
}

public class SmartViewKindOption
{
    public string Key { get; init; } = SavedSearchKinds.Standard;
    public string Label { get; set; } = string.Empty;
}
