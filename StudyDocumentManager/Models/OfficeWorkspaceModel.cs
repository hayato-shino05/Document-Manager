using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public sealed class OfficeDocumentRow
{
    public StudyDocument Document { get; init; } = new();
    public OfficeDocumentMetadata Metadata { get; init; } = new();
    public int DocumentId => Document.Id;
    public string Name => Document.Name;
    public string? DocumentNumber => Metadata.DocumentNumber;
    public string? ContactName => Metadata.ContactName;
    public string? OrganizationOrProject => Metadata.OrganizationOrProject;
    public DateTime? EffectiveDate => Metadata.EffectiveDate;
    public DateTime? ExpiryDate => Metadata.ExpiryDate;
    public string ConfidentialityLevel => Metadata.ConfidentialityLevel;
    public bool ReminderEnabled => Metadata.ReminderEnabled;
    public int ReminderDaysBefore => Metadata.ReminderDaysBefore;
    public OfficeExpiryState ExpiryState { get; init; }
    public int DaysRemaining { get; init; }
    public string ExpiryStateLabel { get; init; } = string.Empty;
    public string ConfidentialityLabel { get; init; } = string.Empty;
    public string DocumentStatus => Document.Status;
    public string FilePath => Document.FilePath ?? string.Empty;
}

public sealed class FilterOption(string key, string label)
{
    public string Key { get; } = key;
    public string Label { get; } = label;
}

public partial class OfficeWorkspaceModel : ModelBase, IDisposable
{
    private readonly IOfficeMetadataRepository _officeRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IProcessLauncherService _processLauncher;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _loc;
    private readonly DateTime _today;
    private bool _disposed;
    private List<OfficeDocumentRow> _allRows = [];

    [ObservableProperty] private ObservableCollection<OfficeDocumentRow> _filteredRows = [];
    [ObservableProperty] private OfficeDocumentRow? _selectedRow;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private FilterOption? _selectedExpiryFilter;
    [ObservableProperty] private FilterOption? _selectedConfidentialityFilter;
    
    // Overview metrics
    [ObservableProperty] private int _overdueCount;
    [ObservableProperty] private int _dueSoonCount;
    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private int _noExpiryCount;
    [ObservableProperty] private int _totalCount;

    // Editor fields
    [ObservableProperty] private string _editingDocumentNumber = string.Empty;
    [ObservableProperty] private string _editingContactName = string.Empty;
    [ObservableProperty] private string _editingOrganizationOrProject = string.Empty;
    [ObservableProperty] private DateTimeOffset? _editingEffectiveDate;
    [ObservableProperty] private DateTimeOffset? _editingExpiryDate;
    [ObservableProperty] private string _editingConfidentialityLevel = OfficeConfidentialityLevel.Internal;
    [ObservableProperty] private bool _editingReminderEnabled = true;
    [ObservableProperty] private int _editingReminderDaysBefore = 3;
    [ObservableProperty] private string _statusText = string.Empty;

    public IReadOnlyList<FilterOption> ExpiryFilterOptions { get; private set; } = [];
    public IReadOnlyList<FilterOption> ConfidentialityFilterOptions { get; private set; } = [];
    public IReadOnlyList<string> ConfidentialityLevels { get; } = OfficeConfidentialityLevel.All;

    public bool HasSelection => SelectedRow != null;

    public OfficeWorkspaceModel(
        IOfficeMetadataRepository officeRepository,
        IDocumentRepository documentRepository,
        IProcessLauncherService processLauncher,
        IDialogService dialogService,
        INavigationService navigationService,
        ILocalizationService loc)
        : this(officeRepository, documentRepository, processLauncher, dialogService, navigationService, loc, DateTime.Today)
    {
    }

    internal OfficeWorkspaceModel(
        IOfficeMetadataRepository officeRepository,
        IDocumentRepository documentRepository,
        IProcessLauncherService processLauncher,
        IDialogService dialogService,
        INavigationService navigationService,
        ILocalizationService loc,
        DateTime today)
    {
        _officeRepository = officeRepository;
        _documentRepository = documentRepository;
        _processLauncher = processLauncher;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _loc = loc;
        _today = today.Date;

        RefreshFilterOptions();
        _loc.LanguageChanged += OnLanguageChanged;
        Refresh();
    }

    private void RefreshFilterOptions()
    {
        ExpiryFilterOptions =
        [
            new FilterOption("all", _loc["Common_All"] ?? "すべて"),
            new FilterOption("due-soon", _loc["OW_ExpiryState_DueSoon"] ?? "期限切迫"),
            new FilterOption("overdue", _loc["OW_ExpiryState_Overdue"] ?? "期限超過"),
            new FilterOption("active", _loc["OW_ExpiryState_Active"] ?? "有効"),
            new FilterOption("no-expiry", _loc["OW_ExpiryState_None"] ?? "期限なし")
        ];

        ConfidentialityFilterOptions =
        [
            new FilterOption("all", _loc["Common_All"] ?? "すべて"),
            new FilterOption(OfficeConfidentialityLevel.Public, "Public"),
            new FilterOption(OfficeConfidentialityLevel.Internal, "Internal"),
            new FilterOption(OfficeConfidentialityLevel.Confidential, "Confidential"),
            new FilterOption(OfficeConfidentialityLevel.Restricted, "Restricted")
        ];

        SelectedExpiryFilter = ExpiryFilterOptions[0];
        SelectedConfidentialityFilter = ConfidentialityFilterOptions[0];
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RefreshFilterOptions();
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedExpiryFilterChanged(FilterOption? value) => ApplyFilter();
    partial void OnSelectedConfidentialityFilterChanged(FilterOption? value) => ApplyFilter();

    partial void OnSelectedRowChanged(OfficeDocumentRow? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        if (value == null)
        {
            EditingDocumentNumber = string.Empty;
            EditingContactName = string.Empty;
            EditingOrganizationOrProject = string.Empty;
            EditingEffectiveDate = null;
            EditingExpiryDate = null;
            EditingConfidentialityLevel = OfficeConfidentialityLevel.Internal;
            EditingReminderEnabled = true;
            EditingReminderDaysBefore = 3;
            return;
        }

        var meta = value.Metadata;
        EditingDocumentNumber = meta.DocumentNumber ?? string.Empty;
        EditingContactName = meta.ContactName ?? string.Empty;
        EditingOrganizationOrProject = meta.OrganizationOrProject ?? string.Empty;
        EditingEffectiveDate = meta.EffectiveDate.HasValue ? new DateTimeOffset(meta.EffectiveDate.Value) : null;
        EditingExpiryDate = meta.ExpiryDate.HasValue ? new DateTimeOffset(meta.ExpiryDate.Value) : null;
        EditingConfidentialityLevel = string.IsNullOrWhiteSpace(meta.ConfidentialityLevel)
            ? OfficeConfidentialityLevel.Internal
            : meta.ConfidentialityLevel;
        EditingReminderEnabled = meta.ReminderEnabled;
        EditingReminderDaysBefore = meta.ReminderDaysBefore > 0 ? meta.ReminderDaysBefore : 3;
    }

    [RelayCommand]
    public void Refresh()
    {
        var documents = _documentRepository.GetAll();
        var rows = new List<OfficeDocumentRow>();
        int overdue = 0;
        int dueSoon = 0;
        int active = 0;
        int noExpiry = 0;

        foreach (var doc in documents)
        {
            var meta = _officeRepository.GetByDocumentId(doc.Id) ?? new OfficeDocumentMetadata
            {
                DocumentId = doc.Id,
                ConfidentialityLevel = OfficeConfidentialityLevel.Internal,
                ReminderEnabled = true,
                ReminderDaysBefore = 3
            };

            OfficeExpiryState state;
            int daysRemaining = 0;
            if (!meta.ExpiryDate.HasValue)
            {
                state = OfficeExpiryState.None;
                noExpiry++;
            }
            else
            {
                var diff = (meta.ExpiryDate.Value.Date - _today).TotalDays;
                daysRemaining = (int)diff;
                if (diff < 0)
                {
                    state = OfficeExpiryState.Overdue;
                    overdue++;
                }
                else if (diff <= (meta.ReminderDaysBefore > 0 ? meta.ReminderDaysBefore : 7))
                {
                    state = OfficeExpiryState.DueSoon;
                    dueSoon++;
                }
                else
                {
                    state = OfficeExpiryState.Active;
                    active++;
                }
            }

            string stateLabel = state switch
            {
                OfficeExpiryState.Overdue => _loc["OW_ExpiryState_Overdue"] ?? "期限超過",
                OfficeExpiryState.DueSoon => _loc["OW_ExpiryState_DueSoon"] ?? "期限切迫",
                OfficeExpiryState.Active => _loc["OW_ExpiryState_Active"] ?? "有効",
                _ => _loc["OW_ExpiryState_None"] ?? "期限なし"
            };

            rows.Add(new OfficeDocumentRow
            {
                Document = doc,
                Metadata = meta,
                ExpiryState = state,
                DaysRemaining = daysRemaining,
                ExpiryStateLabel = stateLabel,
                ConfidentialityLabel = meta.ConfidentialityLevel
            });
        }

        _allRows = rows;
        TotalCount = rows.Count;
        OverdueCount = overdue;
        DueSoonCount = dueSoon;
        ActiveCount = active;
        NoExpiryCount = noExpiry;

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = _allRows.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(r =>
                r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (r.DocumentNumber != null && r.DocumentNumber.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.ContactName != null && r.ContactName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.OrganizationOrProject != null && r.OrganizationOrProject.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        if (SelectedExpiryFilter != null && SelectedExpiryFilter.Key != "all")
        {
            query = SelectedExpiryFilter.Key switch
            {
                "due-soon" => query.Where(r => r.ExpiryState == OfficeExpiryState.DueSoon),
                "overdue" => query.Where(r => r.ExpiryState == OfficeExpiryState.Overdue),
                "active" => query.Where(r => r.ExpiryState == OfficeExpiryState.Active),
                "no-expiry" => query.Where(r => r.ExpiryState == OfficeExpiryState.None),
                _ => query
            };
        }

        if (SelectedConfidentialityFilter != null && SelectedConfidentialityFilter.Key != "all")
        {
            query = query.Where(r => string.Equals(r.ConfidentialityLevel, SelectedConfidentialityFilter.Key, StringComparison.OrdinalIgnoreCase));
        }

        FilteredRows = new ObservableCollection<OfficeDocumentRow>(query.ToList());
    }

    [RelayCommand]
    public async Task SaveMetadataAsync()
    {
        if (SelectedRow == null)
            return;

        var meta = SelectedRow.Metadata;
        meta.DocumentNumber = string.IsNullOrWhiteSpace(EditingDocumentNumber) ? null : EditingDocumentNumber.Trim();
        meta.ContactName = string.IsNullOrWhiteSpace(EditingContactName) ? null : EditingContactName.Trim();
        meta.OrganizationOrProject = string.IsNullOrWhiteSpace(EditingOrganizationOrProject) ? null : EditingOrganizationOrProject.Trim();
        meta.EffectiveDate = EditingEffectiveDate?.DateTime;
        meta.ExpiryDate = EditingExpiryDate?.DateTime;
        meta.ConfidentialityLevel = string.IsNullOrWhiteSpace(EditingConfidentialityLevel)
            ? OfficeConfidentialityLevel.Internal
            : EditingConfidentialityLevel;
        meta.ReminderEnabled = EditingReminderEnabled;
        meta.ReminderDaysBefore = EditingReminderDaysBefore > 0 ? EditingReminderDaysBefore : 3;

        bool saved = _officeRepository.Save(meta);
        if (saved)
        {
            StatusText = _loc["Message_SavedSuccessfully"] ?? "保存しました。";
            int selectedId = SelectedRow.DocumentId;
            Refresh();
            SelectedRow = FilteredRows.FirstOrDefault(r => r.DocumentId == selectedId);
        }
        else
        {
            await _dialogService.ShowMessageAsync(
                _loc["Common_Error"] ?? "エラー",
                _loc["Error_SaveFailed"] ?? "保存に失敗しました。");
        }
    }

    [RelayCommand]
    public void OpenFile()
    {
        if (SelectedRow != null && !string.IsNullOrWhiteSpace(SelectedRow.FilePath))
        {
            _processLauncher.OpenFile(SelectedRow.FilePath);
        }
    }

    [RelayCommand]
    public void GoBack()
    {
        _navigationService.GoBack();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _loc.LanguageChanged -= OnLanguageChanged;
            _disposed = true;
        }
    }
}
