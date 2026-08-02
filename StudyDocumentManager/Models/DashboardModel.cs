using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Core.Services;
using StudyDocumentManager.Models.Items;
using StudyDocumentManager.Services;


namespace StudyDocumentManager.Models;

public partial class DashboardModel : ModelBase
{
    private const string FILTER_ALL_SUBJECTS_KEY = "Filter_AllSubjects";
    private const string FILTER_ALL_TYPES_KEY = "Filter_AllTypes";

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
    private readonly ILocalizationService _loc;
    private bool _isLoadingData;
    private bool _isApplyingFilters;

    // â•â•â• Document list â•â•â•
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

    // ——— Search & Filter ——— 
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private List<string> _subjects = new();
    [ObservableProperty] private List<string> _types = new();
    [ObservableProperty] private string _selectedSubject = FILTER_ALL_SUBJECTS_KEY;
    [ObservableProperty] private string _selectedType = FILTER_ALL_TYPES_KEY;

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
        _loc = localizationService;
        _statusText = _loc["Status_Ready"];
        _loc.LanguageChanged += (_, _) =>
        {
            Subjects = [.. Subjects];
            Types = [.. Types];
        };
        // DO NOT call LoadData() here — it causes StackOverflowException
        // because DataGrid layout hasn't completed yet.
        // Call Initialize() from View.Loaded event instead.
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
            Documents = docs.ToList();
            BuildCategoryTree(docs, subjects, types);

            HasLoadError = false;
            IsEmptyState = docs.Count == 0;
            StateMessage = IsEmptyState ? _loc["Dashboard_EmptyState"] : string.Empty;
            StatusText = string.Format(_loc["Status_TotalSummary"], TotalDocuments, ImportantDocuments, OverdueDocuments);
            NotifyStatPropertiesChanged();
        }
        catch
        {
            Documents = [];
            HasLoadError = true;
            IsEmptyState = false;
            StateMessage = _loc["Dashboard_LoadError"];
            StatusText = StateMessage;
            NotifyStatPropertiesChanged();
        }
        finally
        {
            _isLoadingData = false;
            IsLoading = false;
        }
    }

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
        StatusText = string.Format(_loc["Status_TotalSummary"], TotalDocuments, ImportantDocuments, OverdueDocuments);
        NotifyStatPropertiesChanged();
    }

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

        // ——— Section: Danh mục ——————————————————————
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

        // ——— Section: Loại file ——————————————————————
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
                    Name = t.Name, Count = t.Count,
                    IconKey = t.Name,  // resolved to file icon in ResolvedIconSource
                    FilterType = "type", FilterValue = t.Name,
                    IsIndented = true
                });
        }

        // ——— Quan trọng ——————————————————————————————
        items.Add(new CategoryTreeItem
        {
            Name = _loc["CategoryTree_Important"],
            Count = docs.Count(d => d.IsImportant),
            IconKey = "IconStar",
            FilterType = "important",
            FilterValue = ""
        });

        // ——— Bộ sưu tập ——————————————————————————————
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
            string keyword = SearchKeyword?.Trim() ?? "";
            string subject = SelectedSubject == FILTER_ALL_SUBJECTS_KEY ? "" : SelectedSubject;
            string type = SelectedType == FILTER_ALL_TYPES_KEY ? "" : SelectedType;
            DateTime? fromDate = IsDateFilterEnabled && FilterFromDate.HasValue ? FilterFromDate.Value.DateTime : null;
            DateTime? toDate = IsDateFilterEnabled && FilterToDate.HasValue ? FilterToDate.Value.DateTime : null;
            double? minSize = IsSizeFilterEnabled ? FilterMinSize : null;
            double? maxSize = IsSizeFilterEnabled ? FilterMaxSize : null;
            bool? isImportant = IsImportantOnly ? true : null;
            bool hasFilter = !string.IsNullOrEmpty(subject) || !string.IsNullOrEmpty(type)
                || fromDate.HasValue || toDate.HasValue || minSize.HasValue || maxSize.HasValue
                || isImportant.HasValue || !string.IsNullOrEmpty(keyword);

            var results = hasFilter
                ? _repository.SearchAdvanced(keyword, subject, type, fromDate, toDate, minSize, maxSize, isImportant)
                : _repository.GetAll();

            UpdateVisibleState(results);
        }
        finally
        {
            _isApplyingFilters = false;
        }
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
        var (success, path, error) = await _backupService.BackupAsync();
        if (success)
            await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], string.Format(_loc["Dashboard_BackupDone"], path));
        else if (error != null)
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], string.Format(_loc["Dashboard_BackupError"], error));
    }

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
    private async Task RestoreDatabaseAsync()
    {
        var (success, error) = await _backupService.RestoreAsync();
        if (!success && error is not null)
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], string.Format(_loc["Dashboard_RestoreError"], error));
    }

    [RelayCommand]
    private void Refresh()
    {
        // Set guard BEFORE changing properties to block ApplyFilters re-entrance
        _isLoadingData = true;
        SearchKeyword = string.Empty;
        SelectedSubject = FILTER_ALL_SUBJECTS_KEY;
        SelectedType = FILTER_ALL_TYPES_KEY;
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
    private void OpenBatchImport() => _navigationService.NavigateTo("batchimport");

    [RelayCommand]
    private void OpenBulkDelete() => _navigationService.NavigateTo("bulkdelete");

    [RelayCommand]
    private void OpenRecycleBin() => _navigationService.NavigateTo("recyclebin");

    [RelayCommand]
    private void OpenCategoryManagement() => _navigationService.NavigateTo("categories");

    [RelayCommand]
    private void OpenCollectionManagement() => _navigationService.NavigateTo("collections");

    [RelayCommand]
    private void OpenDuplicateDetection() => _navigationService.NavigateTo("duplicates");

    [RelayCommand]
    private void OpenFileIntegrityCheck() => _navigationService.NavigateTo("fileintegrity");

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

    // â• â• â•  Deadline quick filters â• â• â• 
    // ——— Deadline quick filters ——— 
    [RelayCommand]
    private void ShowUpcomingDeadlines()
    {
        var docs = _repository.GetUpcomingDeadlines(7);
        Documents = docs.ToList();
        StatusText = string.Format(_loc["Status_UpcomingDeadlines"], docs.Count);
    }

    [RelayCommand]
    private void ShowOverdue()
    {
        var docs = _repository.GetOverdueDocuments();
        Documents = docs.ToList();
        StatusText = string.Format(_loc["Status_Overdue"], docs.Count);
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
}
