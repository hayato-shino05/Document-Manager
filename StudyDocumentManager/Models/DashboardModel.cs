using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Core.Services;
using StudyDocumentManager.Models.Items;
using StudyDocumentManager.Services;


namespace StudyDocumentManager.Models;

public partial class DashboardModel : ModelBase, IDisposable
{
    private const string FILTER_ALL_SUBJECTS_KEY = "Filter_AllSubjects";
    private const string FILTER_ALL_TYPES_KEY = "Filter_AllTypes";
    public const string FILTER_ALL_STATUS_KEY = "Filter_AllStatuses";

    private readonly IDocumentRepository _repository;
    private readonly IRecycleBinRepository _recycleBinRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly ICollectionRepository _collectionRepo;
    private readonly IRecentFileRepository _recentFileRepo;
    private readonly IDialogService _dialogService;
    private readonly IFileDialogService _fileDialogService;
    private readonly ICustomDialogService _customDialogService;
    private readonly INavigationService _navigationService;
    private readonly IClipboardService _clipboardService;
    private readonly IProcessLauncherService _processLauncher;
    private readonly IExportService _exportService;
    private readonly IBackupService _backupService;
    private readonly IPersonalDocumentArchiveService _archiveService;
    private readonly ILocalizationService _loc;
    private bool _isLoadingData;
    private bool _isApplyingFilters;
    private CancellationTokenSource? _backupCancellation;
    private CancellationTokenSource? _restoreCancellation;

    // ═══ 文書一覧 ═══
    [ObservableProperty]
    private List<StudyDocument> _documents = new();

    [ObservableProperty]
    private StudyDocument? _selectedDocument;

    // ——— Preview ——— 
    public string PreviewIcon => SelectedDocument switch
    {
        null => "📄",
        var d when d.Type is "Image" => "🖼️",
        var d when d.Type is "Video" => "🎬",
        var d when d.Type is "Audio" => "🎵",
        var d when d.Type is "Archive" => "📦",
        var d when d.Type is "Document" => "📁",
        _ => "📄"
    };

    partial void OnSelectedDocumentChanged(StudyDocument? value)
    {
        if (_isLoadingData || _isApplyingFilters) return;
        OnPropertyChanged(nameof(PreviewIcon));
    }

    partial void OnDocumentsChanged(List<StudyDocument> value)
    {
        if (_isLoadingData) return;

        if (SelectedDocument != null && !value.Any(d => d.Id == SelectedDocument.Id))
            SelectedDocument = null;
    }

    // ——— Stats ——— 
    [ObservableProperty] private int _totalDocuments;
    [ObservableProperty] private int _importantDocuments;
    [ObservableProperty] private int _overdueDocuments;
    [ObservableProperty] private int _totalCategories;
    [ObservableProperty] private int _noFileDocuments;
    [ObservableProperty] private int _deletedCount;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _isEmptyState;
    [ObservableProperty] private bool _hasLoadError;
    [ObservableProperty] private string _stateMessage = string.Empty;
    [ObservableProperty] private string _lastBackupDisplay = string.Empty;
    [ObservableProperty] private bool _isBackupStale;
    [ObservableProperty] private string _lastBackupWarning = string.Empty;
    [ObservableProperty] private string _backupDirectoryInfo = string.Empty;
    [ObservableProperty] private bool _isBackingUp;
    [ObservableProperty] private bool _isRestoring;
    [ObservableProperty] private int _backupProgress;
    [ObservableProperty] private int _restoreProgress;
    [ObservableProperty] private string _backupError = string.Empty;
    [ObservableProperty] private string _restoreError = string.Empty;
    [ObservableProperty] private bool _backupCancelled;
    [ObservableProperty] private bool _restoreCancelled;

    public bool IsBackupOperationVisible => IsBackingUp || IsRestoring
        || BackupCancelled || RestoreCancelled
        || !string.IsNullOrEmpty(BackupError) || !string.IsNullOrEmpty(RestoreError);

    partial void OnIsBackingUpChanged(bool value) => OnPropertyChanged(nameof(IsBackupOperationVisible));
    partial void OnIsRestoringChanged(bool value) => OnPropertyChanged(nameof(IsBackupOperationVisible));
    partial void OnBackupErrorChanged(string value) => OnPropertyChanged(nameof(IsBackupOperationVisible));
    partial void OnRestoreErrorChanged(string value) => OnPropertyChanged(nameof(IsBackupOperationVisible));
    partial void OnBackupCancelledChanged(bool value) => OnPropertyChanged(nameof(IsBackupOperationVisible));
    partial void OnRestoreCancelledChanged(bool value) => OnPropertyChanged(nameof(IsBackupOperationVisible));

    // ——— Search & Filter ———
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private List<string> _subjects = new();
    [ObservableProperty] private List<string> _types = new();
    [ObservableProperty] private string _selectedSubject = FILTER_ALL_SUBJECTS_KEY;
    [ObservableProperty] private string _selectedType = FILTER_ALL_TYPES_KEY;
    [ObservableProperty] private string _selectedStatus = FILTER_ALL_STATUS_KEY;
    [ObservableProperty] private List<StatusOption> _statusOptions = [];

    // ——— Advanced Filter ——— 
    [ObservableProperty] private bool _isAdvancedFilterVisible;
    [ObservableProperty] private bool _isDateFilterEnabled;
    [ObservableProperty] private DateTimeOffset? _filterFromDate;
    [ObservableProperty] private DateTimeOffset? _filterToDate;
    [ObservableProperty] private bool _isSizeFilterEnabled;
    [ObservableProperty] private double _filterMinSize;
    [ObservableProperty] private double _filterMaxSize = 100;
    [ObservableProperty] private bool _isImportantOnly;

    public int ActiveFilterCount
    {
        get
        {
            int count = 0;
            if (IsDateFilterEnabled && (FilterFromDate.HasValue || FilterToDate.HasValue)) count++;
            if (IsSizeFilterEnabled) count++;
            if (IsImportantOnly) count++;
            if (SelectedStatus != FILTER_ALL_STATUS_KEY) count++;
            return count;
        }
    }

    // ——— Stat card aliases for view binding ——— 
    public int TotalCount => TotalDocuments;
    public int SubjectCount => TotalCategories;
    public int ImportantCount => ImportantDocuments;
    public int OverdueCount => OverdueDocuments;
    public int NoFileCount => NoFileDocuments;
    public int RecycleBinCount => DeletedCount;

    // ——— Category Tree Items ——— 
    [ObservableProperty]
    private ObservableCollection<CategoryTreeItem> _categoryTreeItems = new();

    private readonly List<StudyDocument> _allDocuments = [];
    private List<string> _availableSubjects = [];
    private List<string> _availableTypes = [];
    private string _statusKey = "Status_Ready";
    private object[] _statusArguments = [];

    public DashboardModel(
        IDocumentRepository repository,
        IRecycleBinRepository recycleBinRepo,
        ICategoryRepository categoryRepo,
        ICollectionRepository collectionRepo,
        IRecentFileRepository recentFileRepo,
        IDialogService dialogService,
        IFileDialogService fileDialogService,
        ICustomDialogService customDialogService,
        INavigationService navigationService,
        IClipboardService clipboardService,
        IProcessLauncherService processLauncher,
        IExportService exportService,
        IBackupService backupService,
        ILocalizationService localizationService)
        : this(repository, recycleBinRepo, categoryRepo, collectionRepo, recentFileRepo,
               dialogService, fileDialogService, customDialogService, navigationService,
               clipboardService, processLauncher, exportService, backupService,
               new NoopArchiveService(), localizationService)
    {
    }

    public DashboardModel(
        IDocumentRepository repository,
        IRecycleBinRepository recycleBinRepo,
        ICategoryRepository categoryRepo,
        ICollectionRepository collectionRepo,
        IRecentFileRepository recentFileRepo,
        IDialogService dialogService,
        IFileDialogService fileDialogService,
        ICustomDialogService customDialogService,
        INavigationService navigationService,
        IClipboardService clipboardService,
        IProcessLauncherService processLauncher,
        IExportService exportService,
        IBackupService backupService,
        IPersonalDocumentArchiveService archiveService,
        ILocalizationService localizationService)
    {
        _repository = repository;
        _recycleBinRepo = recycleBinRepo;
        _categoryRepo = categoryRepo;
        _collectionRepo = collectionRepo;
        _recentFileRepo = recentFileRepo;
        _dialogService = dialogService;
        _fileDialogService = fileDialogService;
        _customDialogService = customDialogService;
        _navigationService = navigationService;
        _clipboardService = clipboardService;
        _processLauncher = processLauncher;
        _exportService = exportService;
        _backupService = backupService;
        _archiveService = archiveService;
        _loc = localizationService;
        BuildStatusOptions();
        _statusText = _loc[_statusKey];
        _loc.LanguageChanged += OnLanguageChanged;
        // DO NOT call LoadData() here — it causes StackOverflowException
        // because DataGrid layout hasn't completed yet.
        // Call Initialize() from View.Loaded event instead.
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Subjects = [FILTER_ALL_SUBJECTS_KEY, .._availableSubjects];
        Types = [FILTER_ALL_TYPES_KEY, .._availableTypes];
        BuildStatusOptions();

        if (_allDocuments.Count > 0 || Documents.Count > 0 || IsEmptyState || HasLoadError)
            BuildCategoryTree(_allDocuments, _availableSubjects, _availableTypes);

        StateMessage = HasLoadError
            ? _loc["Dashboard_LoadError"]
            : IsEmptyState
                ? _loc["Dashboard_EmptyState"]
                : string.Empty;
        RefreshLocalizedStatus();
    }

    public void Dispose()
    {
        if (_backupCancellation is not null)
        {
            _backupCancellation.Cancel();
            _backupCancellation.Dispose();
            _backupCancellation = null;
        }
        if (_restoreCancellation is not null)
        {
            _restoreCancellation.Cancel();
            _restoreCancellation.Dispose();
            _restoreCancellation = null;
        }
        _loc.LanguageChanged -= OnLanguageChanged;
    }

    /// <summary>
    /// Called from View.Loaded event to populate data AFTER layout is complete.
    /// This prevents StackOverflowException from DataGrid rendering during initial layout pass.
    /// </summary>
    public void Initialize()
    {
        if (Documents.Count > 0) return; // Already initialized
        LoadData();
    }

    private void LoadData()
    {
        _isLoadingData = true;
        IsLoading = true;
        SelectedDocument = null;

        try
        {
            var docs = _repository.GetAll();
            var subjects = _categoryRepo.GetAllSubjects();
            var types = _categoryRepo.GetAllTypes();

            _allDocuments.Clear();
            _allDocuments.AddRange(docs);
            _availableSubjects = [..subjects];
            _availableTypes = [..types];

            TotalDocuments = docs.Count;
            ImportantDocuments = docs.Count(d => d.IsImportant);
            OverdueDocuments = _repository.GetOverdueDocuments().Count;
            NoFileDocuments = docs.Count(d => string.IsNullOrEmpty(d.FilePath));
            TotalCategories = subjects.Count;
            DeletedCount = _recycleBinRepo.GetDeletedDocumentCount();

            Subjects = [FILTER_ALL_SUBJECTS_KEY, ..subjects];
            Types = [FILTER_ALL_TYPES_KEY, ..types];
            SelectedSubject = FILTER_ALL_SUBJECTS_KEY;
            SelectedType = FILTER_ALL_TYPES_KEY;
            SelectedStatus = FILTER_ALL_STATUS_KEY;
            Documents = docs.ToList();
            BuildCategoryTree(docs, subjects, types);

            HasLoadError = false;
            IsEmptyState = docs.Count == 0;
            StateMessage = IsEmptyState ? _loc["Dashboard_EmptyState"] : string.Empty;
            SetLocalizedStatus("Status_TotalSummary", TotalDocuments, ImportantDocuments, OverdueDocuments);
            NotifyStatPropertiesChanged();
        }
        catch
        {
            Documents = [];
            HasLoadError = true;
            IsEmptyState = false;
            StateMessage = _loc["Dashboard_LoadError"];
            SetLocalizedStatus("Dashboard_LoadError");
            NotifyStatPropertiesChanged();
        }
        finally
        {
            _isLoadingData = false;
            IsLoading = false;
        }

        if (_pendingSavedSearch != null)
        {
            var pending = _pendingSavedSearch;
            _pendingSavedSearch = null;
            ApplySavedSearch(pending);
        }
    }

    private void SetLocalizedStatus(string key, params object[] arguments)
    {
        _statusKey = key;
        _statusArguments = arguments;
        RefreshLocalizedStatus();
    }

    private void RefreshLocalizedStatus()
        => StatusText = string.Format(_loc[_statusKey], _statusArguments);

    private void NotifyStatPropertiesChanged()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(SubjectCount));
        OnPropertyChanged(nameof(ImportantCount));
        OnPropertyChanged(nameof(OverdueCount));
        OnPropertyChanged(nameof(NoFileCount));
        OnPropertyChanged(nameof(RecycleBinCount));
    }


    private void UpdateVisibleState(IList<StudyDocument> documents)
    {
        SelectedDocument = null;
        Documents = documents.ToList();
        TotalDocuments = documents.Count;
        ImportantDocuments = documents.Count(d => d.IsImportant);
        OverdueDocuments = documents.Count(d => d.Deadline.HasValue && d.Deadline.Value < DateTime.Now);
        NoFileDocuments = documents.Count(d => string.IsNullOrEmpty(d.FilePath));
        IsEmptyState = documents.Count == 0;
        HasLoadError = false;
        StateMessage = IsEmptyState ? _loc["Dashboard_EmptyState"] : string.Empty;
        SetLocalizedStatus("Status_TotalSummary", TotalDocuments, ImportantDocuments, OverdueDocuments);
        NotifyStatPropertiesChanged();
    }

    private string LocalizeFileType(string canonicalType)
    {
        var key = canonicalType switch
        {
            "PDF" => "FileType_PDF",
            "Word" => "FileType_Word",
            "PowerPoint" => "FileType_PowerPoint",
            "Excel" => "FileType_Excel",
            "Document" => "FileType_Document",
            "Data" => "FileType_Data",
            "Code" => "FileType_Code",
            "Book" => "FileType_Book",
            "Image" => "FileType_Image",
            "Video" => "FileType_Video",
            "Audio" => "FileType_Audio",
            "Archive" => "FileType_Archive",
            "Design" => "FileType_Design",
            "Other" => "FileType_Other",
            _ => null
        };

        return key is null ? canonicalType : _loc[key];
    }

    private void BuildStatusOptions()
        => StatusOptions =
        [
            new(FILTER_ALL_STATUS_KEY, _loc["DS_FilterAll"]),
            ..DocumentStatus.All.Select(s => new StatusOption(s, GetStatusLabel(s)))
        ];

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

    private void BuildCategoryTree(IList<StudyDocument> docs, List<string> subjects, List<string> types)
    {
        var items = new List<CategoryTreeItem>();

        // ——————————————————————————————
        items.Add(new CategoryTreeItem
        {
            Name = _loc["CategoryTree_AllDocs"],
            Count = docs.Count,
            IconKey = "IconAllDocs",
            FilterType = "all",
            FilterValue = ""
        });

        // ——— セクション: カテゴリ ——————————————————————
        var subjectItems = subjects
            .Select(s => new { Name = s, Count = docs.Count(d => d.Subject == s) })
            .Where(x => x.Count > 0).ToList();

        if (subjectItems.Count > 0)
        {
            items.Add(new CategoryTreeItem
            {
                Name = _loc["CategoryTree_Category"],
                IconKey = "IconCategory",
                FilterType = "section-header",
                IsHeader = true
            });
            foreach (var s in subjectItems)
                items.Add(new CategoryTreeItem
                {
                    Name = s.Name, Count = s.Count,
                    IconKey = "IconCategory",
                    FilterType = "subject", FilterValue = s.Name,
                    IsIndented = true
                });
        }

        // ——— セクション: ファイル種別 ——————————————————————
        var typeItems = types
            .Select(t => new { Name = t, Count = docs.Count(d => d.Type == t) })
            .Where(x => x.Count > 0).ToList();

        if (typeItems.Count > 0)
        {
            items.Add(new CategoryTreeItem
            {
                Name = _loc["CategoryTree_FileType"],
                IconKey = "IconDuplicate",
                FilterType = "section-header",
                IsHeader = true
            });
            foreach (var t in typeItems)
                items.Add(new CategoryTreeItem
                {
                    Name = LocalizeFileType(t.Name), Count = t.Count,
                    IconKey = t.Name,  // resolved to file icon in ResolvedIconSource
                    FilterType = "type", FilterValue = t.Name,
                    IsIndented = true
                });
        }

        // ——— 重要 ——————————————————————————————
        items.Add(new CategoryTreeItem
        {
            Name = _loc["CategoryTree_Important"],
            Count = docs.Count(d => d.IsImportant),
            IconKey = "IconStar",
            FilterType = "important",
            FilterValue = ""
        });

        // ——— コレクション ——————————————————————————————
        try
        {
            var collections = _collectionRepo.GetAll();
            if (collections.Count > 0)
            {
                items.Add(new CategoryTreeItem
                {
                    Name = _loc["CategoryTree_Collection"],
                    IconKey = "IconCategory",
                    FilterType = "section-header",
                    IsHeader = true
                });
                foreach (var col in collections)
                {
                    int colCount = _collectionRepo.GetDocuments(col.Id)?.Count ?? 0;
                    items.Add(new CategoryTreeItem
                    {
                        Name = col.Name, Count = colCount,
                        IconKey = "IconStar",
                        FilterType = "collection", FilterValue = col.Id.ToString(),
                        IsIndented = true
                    });
                }
            }
        }
        catch { /* Collections table may not exist yet */ }

        CategoryTreeItems.Clear();
        foreach (var it in items) CategoryTreeItems.Add(it);
    }

    // ——— Search & Filter commands ——— 
    [RelayCommand]
    private void Search() => ApplyFilters();

    [RelayCommand]
    private void ToggleAdvancedFilter() => IsAdvancedFilterVisible = !IsAdvancedFilterVisible;

    [RelayCommand]
    private void ApplyAdvancedFilter() => ApplyFilters();

    [RelayCommand]
    private void ClearAdvancedFilter()
    {
        IsDateFilterEnabled = false;
        FilterFromDate = null;
        FilterToDate = null;
        IsSizeFilterEnabled = false;
        FilterMinSize = 0;
        FilterMaxSize = 100;
        IsImportantOnly = false;
        ApplyFilters();
    }

    [RelayCommand]
    private void FilterByCategory(CategoryTreeItem? item)
    {
        if (item == null || item.IsHeader) return; // section headers are not clickable

        switch (item.FilterType)
        {
            case "all":
                SelectedSubject = FILTER_ALL_SUBJECTS_KEY;
                SelectedType = FILTER_ALL_TYPES_KEY;
                IsImportantOnly = false;
                break;
            case "subject":
                SelectedSubject = item.FilterValue;
                SelectedType = FILTER_ALL_TYPES_KEY;
                IsImportantOnly = false;
                break;
            case "type":
                SelectedSubject = FILTER_ALL_SUBJECTS_KEY;
                SelectedType = item.FilterValue;
                IsImportantOnly = false;
                break;
            case "important":
                SelectedSubject = FILTER_ALL_SUBJECTS_KEY;
                SelectedType = FILTER_ALL_TYPES_KEY;
                IsImportantOnly = true;
                break;
            case "collection":
                if (int.TryParse(item.FilterValue, out int colId))
                {
                    var colDocs = _collectionRepo.GetDocuments(colId);
                    if (colDocs != null)
                    {
                        _isApplyingFilters = true;
                        try
                        {
                            UpdateVisibleState(colDocs);
                        }
                        finally { _isApplyingFilters = false; }
                        return;
                    }
                }
                break;
            case "section-header":
            case "collection-header":
                return; // non-clickable
        }

        ApplyFilters();
    }

    /// <summary>
    /// Change the category (MonHoc) of the selected document directly from the context menu.
    /// Presents a picker dialogue with all existing subjects.
    /// </summary>
    [RelayCommand]
    private async Task ChangeCategoryAsync()
    {
        if (SelectedDocument == null) return;

        var existing = _categoryRepo.GetSubjectsWithCount().Select(s => s.Name).ToList();
        var newCategory = await _customDialogService.ShowChangeCategoryAsync(
            SelectedDocument.Name,
            existing,
            SelectedDocument.Subject ?? "");

        if (newCategory == null) return; // cancelled
        newCategory = newCategory.Trim();
        if (newCategory == (SelectedDocument.Subject ?? "")) return;

        SelectedDocument.Subject = newCategory;
        var ok = _repository.Update(SelectedDocument);
        if (ok)
            LoadData();
        else
            await _dialogService.ShowMessageAsync(_loc["Dialog_Error"], _loc["Dashboard_CannotUpdateCategory"]);
    }

    private void ApplyFilters()
    {
        if (_isLoadingData || _isApplyingFilters) return;
        _isApplyingFilters = true;

        try
        {
            ApplyFiltersCore();
        }
        finally
        {
            _isApplyingFilters = false;
        }
    }

    private void ApplyFiltersCore()
    {
        string keyword = SearchKeyword?.Trim() ?? "";
        string subject = SelectedSubject == FILTER_ALL_SUBJECTS_KEY ? "" : SelectedSubject;
        string type = SelectedType == FILTER_ALL_TYPES_KEY ? "" : SelectedType;
        DateTime? fromDate = IsDateFilterEnabled && FilterFromDate.HasValue ? FilterFromDate.Value.DateTime : null;
        DateTime? toDate = IsDateFilterEnabled && FilterToDate.HasValue ? FilterToDate.Value.DateTime : null;
        double? minSize = IsSizeFilterEnabled ? FilterMinSize : null;
        double? maxSize = IsSizeFilterEnabled ? FilterMaxSize : null;
        bool? isImportant = IsImportantOnly ? true : null;
        string? status = SelectedStatus == FILTER_ALL_STATUS_KEY ? null : SelectedStatus;
        bool hasFilter = !string.IsNullOrEmpty(subject) || !string.IsNullOrEmpty(type)
            || fromDate.HasValue || toDate.HasValue || minSize.HasValue || maxSize.HasValue
            || isImportant.HasValue || !string.IsNullOrEmpty(keyword)
            || status != null;

        var results = !hasFilter
            ? _repository.GetAll()
            : status != null
                ? _repository.SearchAdvancedWithStatus(keyword, subject, type, fromDate, toDate, minSize, maxSize, isImportant, status)
                : _repository.SearchAdvancedWithNotes(keyword, subject, type, fromDate, toDate, minSize, maxSize, isImportant);

        OnPropertyChanged(nameof(ActiveFilterCount));
        UpdateVisibleState(results);
    }

    // ——— Document actions ——— 
    [RelayCommand]
    private async Task OpenFileAsync()
    {
        if (SelectedDocument == null || string.IsNullOrEmpty(SelectedDocument.FilePath))
            return;

        if (!File.Exists(SelectedDocument.FilePath))
        {
            var confirmed = await _dialogService.ShowConfirmAsync(
                _loc["Dialog_Notice"],
                _loc["Dashboard_FileMissingMessage"],
                _loc["Menu_FileCheck"]);
            if (confirmed)
                _navigationService.NavigateTo("fileintegrity");
            return;
        }

        try
        {
            _processLauncher.OpenFile(SelectedDocument.FilePath);
        }
        catch
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
            return;
        }

        if (!_recentFileRepo.Add(SelectedDocument.Id))
            return;
    }

    [RelayCommand]
    private async Task DeleteDocumentAsync()
    {
        if (SelectedDocument == null) return;

        try
        {
            var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
                string.Format(_loc["Dashboard_ConfirmDelete"], SelectedDocument.Name),
                _loc["Action_Delete"], isDanger: true);
            if (!confirmed) return;

            if (!_repository.Delete(SelectedDocument.Id))
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                return;
            }

            LoadData();
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
    private void EditDocument()
    {
        if (SelectedDocument == null) return;
        _navigationService.NavigateTo("addedit", SelectedDocument.Id);
    }

    [RelayCommand]
    private void AddDocument()
    {
        _navigationService.NavigateTo("addedit");
    }

    [RelayCommand]
    private async Task ToggleImportantAsync()
    {
        if (SelectedDocument == null) return;

        SelectedDocument.IsImportant = !SelectedDocument.IsImportant;
        _repository.Update(SelectedDocument);
        LoadData();
    }

    /// <summary>
    /// Toggle important flag directly from DataGrid row click (receives doc as parameter).
    /// </summary>
    [RelayCommand]
    private void ToggleImportantInline(StudyDocument? doc)
    {
        if (doc == null) return;
        doc.IsImportant = !doc.IsImportant;
        _repository.Update(doc);
        LoadData();
    }

    [RelayCommand]
    private async Task BackupDatabaseAsync()
    {
        if (IsBackingUp || IsRestoring) return;

        _backupCancellation?.Dispose();
        _backupCancellation = new CancellationTokenSource();
        var cancellationToken = _backupCancellation.Token;
        IsBackingUp = true;
        BackupProgress = 0;
        BackupError = string.Empty;
        BackupCancelled = false;

        try
        {
            var (success, path, error) = await _backupService.BackupAsync(cancellationToken);
            if (success)
            {
                BackupProgress = 100;
                await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], string.Format(_loc["Dashboard_BackupDone"], path));
            }
            else if (cancellationToken.IsCancellationRequested)
            {
                BackupCancelled = true;
                BackupProgress = 0;
                return;
            }
            else if (error != null)
            {
                BackupError = _loc["Dashboard_BackupFailed"];
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], string.Format(_loc["Dashboard_BackupError"], BackupError));
            }
        }
        catch (Exception)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                BackupError = _loc["Dashboard_BackupFailed"];
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], string.Format(_loc["Dashboard_BackupError"], BackupError));
            }
            else
                BackupCancelled = true;
        }
        finally
        {
            IsBackingUp = false;
            _backupCancellation?.Dispose();
            _backupCancellation = null;
        }
    }

    [RelayCommand]
    private void CancelBackup() => _backupCancellation?.Cancel();

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        var result = await _exportService.ExportCsvAsync(Documents, "documents_export.csv");

        if (result.Success)
            await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], string.Format(_loc["Dashboard_ExportDone"], result.Count, result.FilePath));
        else if (result.Error != null)
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], string.Format(_loc["Dashboard_ExportError"], result.Error));
    }

    [RelayCommand]
    private async Task ExportArchiveAsync()
    {
        var zipPath = await _fileDialogService.ShowSaveFileAsync(
            _loc["Archive_ExportTitle"], "documents_archive.zip", "Zip files (*.zip)|*.zip");
        if (string.IsNullOrEmpty(zipPath)) return;

        try
        {
            var report = await _archiveService.ExportAsync(zipPath, new ArchiveExportOptions());
            if (report.Success)
                await _dialogService.ShowMessageAsync(_loc["Dialog_Success"],
                    string.Format(_loc["Archive_ExportSuccess"], report.ExportedDocuments));
            else
                await _dialogService.ShowErrorAsync(_loc["Archive_FailureMessage"], string.Join("; ", report.ValidationErrors.Select(item => item.Code)));
        }
        catch (Exception)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Archive_FailureMessage"]);
        }
    }

    [RelayCommand]
    private async Task ImportArchiveAsync()
    {
        var zipPath = await _fileDialogService.ShowOpenFileAsync(_loc["Archive_ImportTitle"], "Zip files (*.zip)|*.zip");
        if (string.IsNullOrEmpty(zipPath)) return;

        var destinationRoot = await _fileDialogService.ShowOpenFolderAsync(_loc["Import_SelectFolder"]);
        if (string.IsNullOrWhiteSpace(destinationRoot)) return;

        try
        {
            var report = await _archiveService.ImportAsync(zipPath, new ArchiveImportOptions(destinationRoot));
            if (report.Success)
            {
                await _dialogService.ShowMessageAsync(_loc["Dialog_Success"],
                    string.Format(_loc["Archive_ImportSuccess"], report.ImportedDocuments));
                RefreshCommand.Execute(null);
            }
            else
            {
                await _dialogService.ShowErrorAsync(_loc["Archive_FailureMessage"],
                    string.Join("; ", report.ValidationErrors.Select(item => item.Code)));
            }
        }
        catch (Exception)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Archive_FailureMessage"]);
        }
    }

    [RelayCommand]
    private async Task RestoreDatabaseAsync()
    {
        if (IsBackingUp || IsRestoring) return;

        _restoreCancellation?.Dispose();
        _restoreCancellation = new CancellationTokenSource();
        var cancellationToken = _restoreCancellation.Token;
        IsRestoring = true;
        RestoreProgress = 0;
        RestoreError = string.Empty;
        RestoreCancelled = false;

        try
        {
            var (success, error) = await _backupService.RestoreAsync(cancellationToken);
            if (success)
                RestoreProgress = 100;
            else if (cancellationToken.IsCancellationRequested)
            {
                RestoreCancelled = true;
                return;
            }
            else if (error is not null)
            {
                RestoreError = _loc["Dashboard_RestoreFailed"];
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], string.Format(_loc["Dashboard_RestoreError"], RestoreError));
            }
        }
        catch (Exception)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                RestoreError = _loc["Dashboard_RestoreFailed"];
                await _dialogService.ShowErrorAsync(
                    _loc["Dialog_Error"],
                    string.Format(_loc["Dashboard_RestoreError"], RestoreError));
            }
            else
                RestoreCancelled = true;
        }
        finally
        {
            IsRestoring = false;
            _restoreCancellation?.Dispose();
            _restoreCancellation = null;
        }
    }

    [RelayCommand]
    private void CancelRestore() => _restoreCancellation?.Cancel();

    [RelayCommand]
    private void Refresh()
    {
        // Set guard BEFORE changing properties to block ApplyFilters re-entrance
        _isLoadingData = true;
        SearchKeyword = string.Empty;
        SelectedSubject = FILTER_ALL_SUBJECTS_KEY;
        SelectedType = FILTER_ALL_TYPES_KEY;
        SelectedStatus = FILTER_ALL_STATUS_KEY;
        IsAdvancedFilterVisible = false;
        IsDateFilterEnabled = false;
        FilterFromDate = null;
        FilterToDate = null;
        IsSizeFilterEnabled = false;
        FilterMinSize = 0;
        FilterMaxSize = 100;
        IsImportantOnly = false;
        _isLoadingData = false;
        // LoadData() will handle data reload with its own _isLoadingData guard
        LoadData();
    }

    // ——— Navigation commands ———
    [RelayCommand]
    private void OpenReport() => _navigationService.NavigateTo("report");

    [RelayCommand]
    private void OpenRecentFiles() => _navigationService.NavigateTo("recentfiles");

    [RelayCommand]
    private void OpenTreeMap() => _navigationService.NavigateTo("treemap");

    [RelayCommand]
    private void OpenPersonalNote()
    {
        if (SelectedDocument == null) return;
        _navigationService.NavigateTo("personal-note", (SelectedDocument.Id, SelectedDocument.Name));
    }

    [RelayCommand]
    private void OpenRelatedDocuments()
    {
        if (SelectedDocument == null) return;
        _navigationService.NavigateTo("related-docs", (SelectedDocument.Id, SelectedDocument.Name));
    }

    // ——— Context menu actions ——— 
    [RelayCommand]
    private async Task CopyPathAsync()
    {
        if (SelectedDocument == null || string.IsNullOrEmpty(SelectedDocument.FilePath)) return;
        try
        {
            await _clipboardService.SetTextAsync(SelectedDocument.FilePath);
        }
        catch { }
    }

    [RelayCommand]
    private async Task CopyNameAsync()
    {
        if (SelectedDocument == null || string.IsNullOrEmpty(SelectedDocument.Name)) return;
        try
        {
            await _clipboardService.SetTextAsync(SelectedDocument.Name);
        }
        catch { }
    }

    [RelayCommand]
    private async Task QuickEditTagsAsync()
    {
        if (SelectedDocument == null) return;

        var result = await _dialogService.ShowInputAsync(
            _loc["Dashboard_QuickEditTagsTitle"],
            string.Format(_loc["Dashboard_QuickEditTagsLabel"], SelectedDocument.Name),
            SelectedDocument.Tags ?? "",
            _loc["Dashboard_QuickEditTagsHint"]);

        if (result == null) return; // cancelled

        SelectedDocument.Tags = result.Trim();
        if (_repository.Update(SelectedDocument))
            LoadData();
    }

    [RelayCommand]
    private async Task QuickEditGhiChuAsync()
    {
        if (SelectedDocument == null) return;

        var result = await _dialogService.ShowInputAsync(
            _loc["Dashboard_QuickEditNotesTitle"],
            string.Format(_loc["Dashboard_QuickEditNotesLabel"], SelectedDocument.Name),
            SelectedDocument.Notes ?? "",
            _loc["Dashboard_QuickEditNotesHint"]);

        if (result == null) return; // cancelled

        SelectedDocument.Notes = result.Trim();
        if (_repository.Update(SelectedDocument))
            LoadData();
    }

    [RelayCommand]
    private async Task AddToCollectionAsync()
    {
        if (SelectedDocument == null) return;

        var rawCollections = _collectionRepo.GetAll();
        if (rawCollections.Count == 0)
        {
            var collectionName = await _dialogService.ShowInputAsync(
                _loc["Collection_CreateTitle"],
                _loc["Collection_CreateLabel"]);
            if (string.IsNullOrWhiteSpace(collectionName))
                return;

            collectionName = collectionName.Trim();
            var collectionId = _collectionRepo.Create(collectionName);
            if (collectionId <= 0)
            {
                await _dialogService.ShowErrorAsync(
                    _loc["Dialog_Error"],
                    string.Format(_loc["Collection_CreateError"], collectionName));
                return;
            }

            if (_collectionRepo.AddDocument(collectionId, SelectedDocument.Id))
            {
                await _dialogService.ShowMessageAsync(
                    _loc["Dialog_Success"],
                    string.Format(_loc["Dashboard_AddedToCollection"], SelectedDocument.Name, collectionName));
                Refresh();
                return;
            }

            await _dialogService.ShowErrorAsync(
                _loc["Dialog_Error"],
                string.Format(_loc["Collection_AddError"], collectionName));
            return;
        }

        // Build picker items with doc counts
        var items = rawCollections
            .Select(c => (c.Id, c.Name,
                DocCount: _collectionRepo.GetDocuments(c.Id)?.Count ?? 0))
            .ToList();

        var selectedId = await _customDialogService.ShowSelectCollectionAsync(
            SelectedDocument.Name, items);

        if (selectedId < 0) return; // cancelled

        var collection = rawCollections.First(c => c.Id == selectedId);

        if (_collectionRepo.AddDocument(collection.Id, SelectedDocument.Id))
        {
            await _dialogService.ShowMessageAsync(_loc["Dialog_Success"],
                string.Format(_loc["Dashboard_AddedToCollection"], SelectedDocument.Name, collection.Name));
            Refresh();
        }
        else
        {
            await _dialogService.ShowMessageAsync(_loc["Dialog_Notice"],
                string.Format(_loc["Dashboard_AlreadyInCollection"], collection.Name));
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (SelectedDocument == null || string.IsNullOrEmpty(SelectedDocument.FilePath)) return;
        try
        {
            _processLauncher.RevealInExplorer(SelectedDocument.FilePath);
        }
        catch { }
    }

    // 期限クイックフィルター
    // ——— Deadline quick filters ——— 
    [RelayCommand]
    private void ShowUpcomingDeadlines()
    {
        var docs = _repository.GetUpcomingDeadlines(7);
        UpdateVisibleState(docs);
        SetLocalizedStatus("Status_UpcomingDeadlines", docs.Count);
    }

    [RelayCommand]
    private void ShowOverdue()
    {
        var docs = _repository.GetOverdueDocuments();
        UpdateVisibleState(docs);
        SetLocalizedStatus("Status_Overdue", docs.Count);
    }

    // スマートビュー実行（NavigationService "run-smartview" 経由）
    public void ApplySavedSearch(SavedSearchCriteria? criteria)
    {
        if (criteria == null || _isApplyingFilters) return;

        // View の遅延初期化前に呼ばれた場合は保留し、LoadData 完了後に適用する
        if (_isLoadingData || IsLoading)
        {
            _pendingSavedSearch = criteria;
            return;
        }

        ApplySavedSearchCore(criteria);
    }

    private SavedSearchCriteria? _pendingSavedSearch;

    private void ApplySavedSearchCore(SavedSearchCriteria criteria)
    {
        _isApplyingFilters = true;
        try
        {
            SearchKeyword = criteria.Keyword ?? string.Empty;
            SelectedSubject = string.IsNullOrEmpty(criteria.Subject) ? FILTER_ALL_SUBJECTS_KEY : criteria.Subject;
            SelectedType = string.IsNullOrEmpty(criteria.Type) ? FILTER_ALL_TYPES_KEY : criteria.Type;
            SelectedStatus = FILTER_ALL_STATUS_KEY;
            FilterFromDate = criteria.FromDate.HasValue ? new DateTimeOffset(criteria.FromDate.Value) : null;
            FilterToDate = criteria.ToDate.HasValue ? new DateTimeOffset(criteria.ToDate.Value) : null;
            FilterMinSize = criteria.MinSize ?? 0;
            FilterMaxSize = criteria.MaxSize ?? 100;
            IsDateFilterEnabled = criteria.FromDate.HasValue || criteria.ToDate.HasValue;
            IsSizeFilterEnabled = criteria.MinSize.HasValue || criteria.MaxSize.HasValue;
            IsImportantOnly = criteria.IsImportant ?? false;
            IsAdvancedFilterVisible = IsDateFilterEnabled || IsSizeFilterEnabled || (criteria.IsImportant ?? false);

            switch (criteria.Kind)
            {
                case SavedSearchKinds.Uncategorized:
                    UpdateVisibleState(_repository.GetUncategorizedDocuments());
                    break;
                case SavedSearchKinds.MissingMetadata:
                    UpdateVisibleState(_repository.GetDocumentsWithMissingMetadata());
                    break;
                case SavedSearchKinds.MissingFile:
                    UpdateVisibleState(_repository.GetAll()
                        .Where(d => !string.IsNullOrEmpty(d.FilePath) && !File.Exists(d.FilePath))
                        .ToList());
                    break;
                case SavedSearchKinds.RecentlyAdded:
                    FilterFromDate = DateTimeOffset.Now.AddDays(-criteria.RecentDays);
                    IsDateFilterEnabled = true;
                    ApplyFiltersCore();
                    break;
                case SavedSearchKinds.Important:
                    IsImportantOnly = true;
                    ApplyFiltersCore();
                    break;
                case SavedSearchKinds.DueSoon:
                    var upcoming = _repository.GetUpcomingDeadlines(criteria.DeadlineDays);
                    Documents = upcoming.ToList();
                    SetLocalizedStatus("Status_UpcomingDeadlines", upcoming.Count);
                    break;
                default:
                    ApplyFiltersCore();
                    break;
            }
        }
        finally
        {
            _isApplyingFilters = false;
        }
    }

    // ——— About dialog ——— 
    [RelayCommand]
    private async Task ShowAboutAsync()
    {
        var version = AppVersion.Current;
        await _dialogService.ShowMessageAsync(_loc["Dialog_About"],
            string.Format(_loc["Dashboard_About"], version));
    }

    partial void OnSelectedSubjectChanged(string value)
    {
        if (!_isLoadingData) ApplyFilters();
    }

    partial void OnSelectedTypeChanged(string value)
    {
        if (!_isLoadingData) ApplyFilters();
    }

    partial void OnSelectedStatusChanged(string value)
    {
        if (!_isLoadingData) ApplyFilters();
    }
}

public sealed record StatusOption(string Value, string Display);

internal sealed class NoopArchiveService : IPersonalDocumentArchiveService
{
    public Task<ArchiveExportReport> ExportAsync(string destinationZip, ArchiveExportOptions options)
        => Task.FromResult(new ArchiveExportReport(false, 0, [], [], []));
    public Task<ArchiveImportReport> ImportAsync(string sourceZip, ArchiveImportOptions options)
        => Task.FromResult(new ArchiveImportReport(false, 0, 0, [], [], [], ArchiveTransactionOutcome.NotStarted));
}
