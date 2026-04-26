using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class DashboardModel : ModelBase
{
    private readonly IDocument _repository;
    private readonly ICategory _categoryRepo;
    private readonly ICollection _collectionRepo;
    private readonly IRecentFile _recentFileRepo;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private bool _isLoadingData; // Guard flag to prevent re-entrant ApplyFilters during LoadData
    private bool _isApplyingFilters; // Re-entrancy guard for ApplyFilters

    // â•â•â• Document list â•â•â•
    [ObservableProperty]
    private List<StudyDocument> _documents = new();

    [ObservableProperty]
    private StudyDocument? _selectedDocument;

    // â•â•â• Preview â•â•â•
    public string PreviewIcon => SelectedDocument switch
    {
        null => "ðŸ“„",
        var d when d.Loai is "HÃ¬nh áº£nh" => "ðŸ–¼ï¸",
        var d when d.Loai is "Video" => "ðŸŽ¬",
        var d when d.Loai is "Audio" => "ðŸŽµ",
        var d when d.Loai is "NÃ©n" => "ðŸ“¦",
        var d when d.Loai is "TÃ i liá»‡u" => "ðŸ“",
        _ => "ðŸ“„"
    };

    partial void OnSelectedDocumentChanged(StudyDocument? value)
    {
        // Skip property change notifications during bulk updates to prevent
        // re-entrant rendering loops that cause StackOverflowException
        if (_isLoadingData || _isApplyingFilters) return;
        OnPropertyChanged(nameof(PreviewIcon));
    }

    // â•â•â• Stats â•â•â•
    [ObservableProperty] private int _totalDocuments;
    [ObservableProperty] private int _importantDocuments;
    [ObservableProperty] private int _overdueDocuments;
    [ObservableProperty] private int _totalCategories;
    [ObservableProperty] private int _noFileDocuments;
    [ObservableProperty] private int _deletedCount;
    [ObservableProperty] private string _statusText = "Sáºµn sÃ ng";

    // â•â•â• Search & Filter â•â•â•
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private List<string> _subjects = new();
    [ObservableProperty] private List<string> _types = new();
    [ObservableProperty] private string _selectedSubject = "Táº¥t cáº£";
    [ObservableProperty] private string _selectedType = "Táº¥t cáº£";

    // â•â•â• Advanced Filter â•â•â•
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

    // â•â•â• Stat card aliases for view binding â•â•â•
    public int TotalCount => TotalDocuments;
    public int SubjectCount => TotalCategories;
    public int ImportantCount => ImportantDocuments;
    public int OverdueCount => OverdueDocuments;
    public int NoFileCount => NoFileDocuments;
    public int RecycleBinCount => DeletedCount;

    // â•â•â• Category Tree Items â•â•â•
    [ObservableProperty]
    private ObservableCollection<CategoryTreeItem> _categoryTreeItems = new();

    public DashboardModel(
        IDocument repository,
        ICategory categoryRepo,
        ICollection collectionRepo,
        IRecentFile recentFileRepo,
        IDialogService dialogService,
        INavigationService navigationService)
    {
        _repository = repository;
        _categoryRepo = categoryRepo;
        _collectionRepo = collectionRepo;
        _recentFileRepo = recentFileRepo;
        _dialogService = dialogService;
        _navigationService = navigationService;
        // DO NOT call LoadData() here â€” it causes StackOverflowException
        // because DataGrid layout hasn't completed yet.
        // Call Initialize() from View.Loaded event instead.
    }

    /// <summary>
    /// Called from View.Loaded event to populate data AFTER layout is complete.
    /// This prevents StackOverflowException from DataGrid rendering during initial layout pass.
    /// </summary>
    public void Initialize()
    {
        if (_documents.Count > 0) return; // Already initialized
        LoadData();
    }

    private void LoadData()
    {
        _isLoadingData = true;
        System.Diagnostics.Debug.WriteLine("[DEBUG] === LoadData START ===");

        // Load documents
        var docs = _repository.GetAll();
        System.Diagnostics.Debug.WriteLine($"[DEBUG] GetAll returned {docs.Count} documents");

        // Stats
        TotalDocuments = docs.Count;
        ImportantDocuments = docs.Count(d => d.QuanTrong);
        OverdueDocuments = _repository.GetOverdueDocuments().Count;
        NoFileDocuments = docs.Count(d => string.IsNullOrEmpty(d.DuongDan));
        TotalCategories = _categoryRepo.GetAllSubjects().Count;
        DeletedCount = _repository.GetDeletedDocumentCount();

        // Load filter dropdowns â€” clear and re-populate EXISTING collections
        // to avoid replacing the ObservableCollection reference which causes
        // Avalonia ComboBox binding loop (StackOverflowException).
        var subjects = _categoryRepo.GetAllSubjects();
        var types = _categoryRepo.GetAllTypes();

        var subjectList = new List<string> { "Táº¥t cáº£" };
        subjectList.AddRange(subjects);
        Subjects = subjectList;

        var typeList = new List<string> { "Táº¥t cáº£" };
        typeList.AddRange(types);
        Types = typeList;

        System.Diagnostics.Debug.WriteLine($"[DEBUG] Subjects={Subjects.Count}, Types={Types.Count}");

        // Reset filter AFTER collections are populated
        SelectedSubject = "Táº¥t cáº£";
        SelectedType = "Táº¥t cáº£";
        System.Diagnostics.Debug.WriteLine($"[DEBUG] After filter reset: Subject='{SelectedSubject}', Type='{SelectedType}'");

        // Assign new List (not ObservableCollection) â€” DataGrid ItemsSource
        // is set from code-behind to avoid binding loop.
        Documents = docs.ToList();

        // Build category tree
        BuildCategoryTree(docs, subjects, types);

        // Update status
        StatusText = $"Tá»•ng: {TotalDocuments} tÃ i liá»‡u | Quan trá»ng: {ImportantDocuments} | QuÃ¡ háº¡n: {OverdueDocuments}";

        // Notify stat card properties changed
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(SubjectCount));
        OnPropertyChanged(nameof(ImportantCount));
        OnPropertyChanged(nameof(OverdueCount));
        OnPropertyChanged(nameof(NoFileCount));
        OnPropertyChanged(nameof(RecycleBinCount));

        _isLoadingData = false;
        System.Diagnostics.Debug.WriteLine($"[DEBUG] === LoadData END === Documents.Count={Documents.Count}");
    }

    private void BuildCategoryTree(IList<StudyDocument> docs, List<string> subjects, List<string> types)
    {
        var items = new List<CategoryTreeItem>();

        // â”€â”€â”€ Táº¥t cáº£ â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        items.Add(new CategoryTreeItem
        {
            Name = "Táº¥t cáº£ tÃ i liá»‡u",
            Count = docs.Count,
            IconKey = "IconAllDocs",
            FilterType = "all",
            FilterValue = ""
        });

        // â”€â”€â”€ Section: Danh má»¥c â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var subjectItems = subjects
            .Select(s => new { Name = s, Count = docs.Count(d => d.MonHoc == s) })
            .Where(x => x.Count > 0).ToList();

        if (subjectItems.Count > 0)
        {
            items.Add(new CategoryTreeItem
            {
                Name = "Danh má»¥c",
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

        // â”€â”€â”€ Section: Loáº¡i file â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var typeItems = types
            .Select(t => new { Name = t, Count = docs.Count(d => d.Loai == t) })
            .Where(x => x.Count > 0).ToList();

        if (typeItems.Count > 0)
        {
            items.Add(new CategoryTreeItem
            {
                Name = "Loáº¡i file",
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

        // â”€â”€â”€ Quan trá»ng â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        items.Add(new CategoryTreeItem
        {
            Name = "Quan trá»ng",
            Count = docs.Count(d => d.QuanTrong),
            IconKey = "IconStar",
            FilterType = "important",
            FilterValue = ""
        });

        // â”€â”€â”€ Bá»™ sÆ°u táº­p â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        try
        {
            var collections = _collectionRepo.GetAll();
            if (collections.Count > 0)
            {
                items.Add(new CategoryTreeItem
                {
                    Name = "Bá»™ sÆ°u táº­p",
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

    // â•â•â• Search & Filter commands â•â•â•
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
                SelectedSubject = "Táº¥t cáº£";
                SelectedType = "Táº¥t cáº£";
                IsImportantOnly = false;
                break;
            case "subject":
                SelectedSubject = item.FilterValue;
                SelectedType = "Táº¥t cáº£";
                IsImportantOnly = false;
                break;
            case "type":
                SelectedSubject = "Táº¥t cáº£";
                SelectedType = item.FilterValue;
                IsImportantOnly = false;
                break;
            case "important":
                SelectedSubject = "Táº¥t cáº£";
                SelectedType = "Táº¥t cáº£";
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
                            Documents = colDocs;
                            TotalDocuments = colDocs.Count;
                            ImportantDocuments = colDocs.Count(d => d.QuanTrong);
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
        var newCategory = await _dialogService.ShowChangeCategoryAsync(
            SelectedDocument.Ten,
            existing,
            SelectedDocument.MonHoc ?? "");

        if (newCategory == null) return; // cancelled
        newCategory = newCategory.Trim();
        if (newCategory == (SelectedDocument.MonHoc ?? "")) return; // no change

        SelectedDocument.MonHoc = newCategory;
        var ok = _repository.Update(SelectedDocument);
        if (ok)
            LoadData();
        else
            await _dialogService.ShowMessageAsync("Lá»—i", "KhÃ´ng thá»ƒ cáº­p nháº­t danh má»¥c.");
    }

    private void ApplyFilters()
    {
        // Skip during initial data load
        if (_isLoadingData) return;
        // Re-entrancy guard: setting Documents triggers OnDocumentsChanged which could re-invoke
        if (_isApplyingFilters) return;
        _isApplyingFilters = true;

        try
        {
            string keyword = SearchKeyword?.Trim() ?? "";
            string subject = SelectedSubject == "Táº¥t cáº£" ? "" : SelectedSubject;
            string type = SelectedType == "Táº¥t cáº£" ? "" : SelectedType;

            DateTime? fromDate = IsDateFilterEnabled && FilterFromDate.HasValue
                ? FilterFromDate.Value.DateTime : null;
            DateTime? toDate = IsDateFilterEnabled && FilterToDate.HasValue
                ? FilterToDate.Value.DateTime : null;
            double? minSize = IsSizeFilterEnabled ? FilterMinSize : null;
            double? maxSize = IsSizeFilterEnabled ? FilterMaxSize : null;
            bool? isImportant = IsImportantOnly ? true : null;

            bool hasFilter = !string.IsNullOrEmpty(subject) || !string.IsNullOrEmpty(type)
                || fromDate.HasValue || toDate.HasValue
                || minSize.HasValue || maxSize.HasValue
                || isImportant.HasValue || !string.IsNullOrEmpty(keyword);

            List<StudyDocument> results = hasFilter
                ? _repository.SearchAdvanced(keyword, subject, type, fromDate, toDate, minSize, maxSize, isImportant)
                : _repository.GetAll();

            Documents = results.ToList();
            TotalDocuments = results.Count;
            ImportantDocuments = results.Count(d => d.QuanTrong);
        }
        finally
        {
            _isApplyingFilters = false;
        }
    }

    // â•â•â• Document actions â•â•â•
    [RelayCommand]
    private void OpenFile()
    {
        if (SelectedDocument == null || string.IsNullOrEmpty(SelectedDocument.DuongDan)) return;

        try
        {
            if (File.Exists(SelectedDocument.DuongDan))
            {
                // Track recent file
                // NOTE: AddRecentFile is called via DashboardModel but RecentFileRepository is not injected here.
                // This is intentional â€” recent file tracking does not affect dashboard state.
                _recentFileRepo.Add(SelectedDocument.Id);

                Process.Start(new ProcessStartInfo
                {
                    FileName = SelectedDocument.DuongDan,
                    UseShellExecute = true
                });
            }
        }
        catch { /* Ignore errors when opening files */ }
    }

    [RelayCommand]
    private async Task DeleteDocumentAsync()
    {
        if (SelectedDocument == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync("XÃ¡c nháº­n xÃ³a",
            $"XÃ³a tÃ i liá»‡u '{SelectedDocument.Ten}' vÃ o ThÃ¹ng rÃ¡c?",
            "XoÃ¡", isDanger: true);
        if (confirmed)
        {
            _repository.Delete(SelectedDocument.Id);
            LoadData();
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

        SelectedDocument.QuanTrong = !SelectedDocument.QuanTrong;
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
        doc.QuanTrong = !doc.QuanTrong;
        _repository.Update(doc);
        LoadData();
    }

    [RelayCommand]
    private async Task BackupDatabaseAsync()
    {
        var path = await _dialogService.ShowSaveFileAsync("Sao lÆ°u", "backup_study_docs.db", "Database|*.db|All Files|*.*");
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                _repository.BackupDatabase(path);
                await _dialogService.ShowMessageAsync("ThÃ nh cÃ´ng", $"ÄÃ£ sao lÆ°u cÆ¡ sá»Ÿ dá»¯ liá»‡u táº¡i:\n{path}");
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("Lá»—i", $"KhÃ´ng thá»ƒ sao lÆ°u: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        var path = await _dialogService.ShowSaveFileAsync("Xuáº¥t CSV", "tai_lieu_export.csv", "CSV|*.csv|All Files|*.*");
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var docs = Documents;
            using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
            await writer.WriteLineAsync("ID,TÃªn,Danh má»¥c,Loáº¡i,ÄÆ°á»ng dáº«n,TÃ¡c giáº£,Tags,Quan trá»ng,Dung lÆ°á»£ng (MB),NgÃ y thÃªm,Deadline,Ghi chÃº");
            foreach (var doc in docs)
            {
                string line = string.Join(",",
                    doc.Id,
                    EscapeCsv(doc.Ten),
                    EscapeCsv(doc.MonHoc),
                    EscapeCsv(doc.Loai),
                    EscapeCsv(doc.DuongDan),
                    EscapeCsv(doc.TacGia),
                    EscapeCsv(doc.Tags),
                    doc.QuanTrong ? "CÃ³" : "KhÃ´ng",
                    doc.KichThuoc?.ToString("F2") ?? "",
                    doc.NgayThem.ToString("dd/MM/yyyy"),
                    doc.Deadline?.ToString("dd/MM/yyyy") ?? "",
                    EscapeCsv(doc.GhiChu)
                );
                await writer.WriteLineAsync(line);
            }
            await _dialogService.ShowMessageAsync("ThÃ nh cÃ´ng", $"ÄÃ£ xuáº¥t {docs.Count} tÃ i liá»‡u ra file:\n{path}");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync("Lá»—i", $"KhÃ´ng thá»ƒ xuáº¥t CSV: {ex.Message}");
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    [RelayCommand]
    private async Task RestoreDatabaseAsync()
    {
        var path = await _dialogService.ShowOpenFileAsync("Chá»n file backup", "Database|*.db|All Files|*.*");
        if (string.IsNullOrWhiteSpace(path)) return;

        if (!File.Exists(path))
        {
            await _dialogService.ShowErrorAsync("Lá»—i", "File backup khÃ´ng tá»“n táº¡i.");
            return;
        }

        var confirmed = await _dialogService.ShowConfirmAsync("âš ï¸ XÃ¡c nháº­n",
            "KhÃ´i phá»¥c sáº½ GHI ÄÃˆ toÃ n bá»™ dá»¯ liá»‡u hiá»‡n táº¡i. Báº¡n cÃ³ cháº¯c cháº¯n?",
            "Ghi Ä‘Ã¨ & KhÃ´i phá»¥c", isDanger: true);
        if (confirmed)
        {
            try
            {
                File.Copy(path, _repository.DatabasePath, overwrite: true);
                LoadData();
                await _dialogService.ShowMessageAsync("ThÃ nh cÃ´ng", "ÄÃ£ khÃ´i phá»¥c cÆ¡ sá»Ÿ dá»¯ liá»‡u thÃ nh cÃ´ng!");
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("Lá»—i", $"KhÃ´ng thá»ƒ khÃ´i phá»¥c: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        // Set guard BEFORE changing properties to block ApplyFilters re-entrance
        _isLoadingData = true;
        SearchKeyword = string.Empty;
        SelectedSubject = "Táº¥t cáº£";
        SelectedType = "Táº¥t cáº£";
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

    // â•â•â• Navigation commands â•â•â•
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
        _navigationService.NavigateTo("personal-note", (SelectedDocument.Id, SelectedDocument.Ten));
    }

    [RelayCommand]
    private void OpenRelatedDocuments()
    {
        if (SelectedDocument == null) return;
        _navigationService.NavigateTo("related-docs", (SelectedDocument.Id, SelectedDocument.Ten));
    }

    // â•â•â• Context menu actions â•â•â•
    [RelayCommand]
    private void CopyPath()
    {
        if (SelectedDocument == null || string.IsNullOrEmpty(SelectedDocument.DuongDan)) return;
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow?.Clipboard?.SetTextAsync(SelectedDocument.DuongDan);
            }
        }
        catch { /* ignore clipboard errors */ }
    }

    [RelayCommand]
    private void CopyName()
    {
        if (SelectedDocument == null || string.IsNullOrEmpty(SelectedDocument.Ten)) return;
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow?.Clipboard?.SetTextAsync(SelectedDocument.Ten);
        }
        catch { /* ignore clipboard errors */ }
    }

    [RelayCommand]
    private async Task QuickEditTagsAsync()
    {
        if (SelectedDocument == null) return;

        var result = await _dialogService.ShowInputAsync(
            "Sá»­a Tags",
            $"Tags cho \"{SelectedDocument.Ten}\":",
            SelectedDocument.Tags ?? "",
            "VÃ­ dá»¥: láº­p trÃ¬nh, toÃ¡n há»c, váº­t lÃ½");

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
            "Sá»­a Ghi chÃº",
            $"Ghi chÃº cho \"{SelectedDocument.Ten}\":",
            SelectedDocument.GhiChu ?? "",
            "Nháº­p ghi chÃº ná»™i bá»™...");

        if (result == null) return; // cancelled

        SelectedDocument.GhiChu = result.Trim();
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
            await _dialogService.ShowMessageAsync("ThÃ´ng bÃ¡o",
                "ChÆ°a cÃ³ bá»™ sÆ°u táº­p nÃ o. Vui lÃ²ng táº¡o bá»™ sÆ°u táº­p trong menu 'Bá»™ sÆ°u táº­p' trÆ°á»›c.");
            return;
        }

        // Build picker items with doc counts
        var items = rawCollections
            .Select(c => (c.Id, c.Name,
                DocCount: _collectionRepo.GetDocuments(c.Id)?.Count ?? 0))
            .ToList();

        var selectedId = await _dialogService.ShowSelectCollectionAsync(
            SelectedDocument.Ten, items);

        if (selectedId < 0) return; // cancelled

        var collection = rawCollections.First(c => c.Id == selectedId);

        if (_collectionRepo.AddDocument(collection.Id, SelectedDocument.Id))
        {
            await _dialogService.ShowMessageAsync("ThÃ nh cÃ´ng",
                $"ÄÃ£ thÃªm '{SelectedDocument.Ten}' vÃ o bá»™ sÆ°u táº­p '{collection.Name}'.");
            Refresh();
        }
        else
        {
            await _dialogService.ShowMessageAsync("ThÃ´ng bÃ¡o",
                $"TÃ i liá»‡u Ä‘Ã£ cÃ³ trong bá»™ sÆ°u táº­p '{collection.Name}' rá»“i.");
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (SelectedDocument == null || string.IsNullOrEmpty(SelectedDocument.DuongDan)) return;
        try
        {
            if (File.Exists(SelectedDocument.DuongDan))
            {
                Process.Start("explorer.exe", $"/select,\"{SelectedDocument.DuongDan}\"");
            }
            else
            {
                var dir = Path.GetDirectoryName(SelectedDocument.DuongDan);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    Process.Start("explorer.exe", dir);
            }
        }
        catch { /* ignore */ }
    }

    // â•â•â• Deadline quick filters â•â•â•
    [RelayCommand]
    private void ShowUpcomingDeadlines()
    {
        var docs = _repository.GetUpcomingDeadlines(7);
        Documents = docs.ToList();
        StatusText = $"Sáº¯p Ä‘áº¿n háº¡n (7 ngÃ y): {docs.Count} tÃ i liá»‡u";
    }

    [RelayCommand]
    private void ShowOverdue()
    {
        var docs = _repository.GetOverdueDocuments();
        Documents = docs.ToList();
        StatusText = $"QuÃ¡ háº¡n: {docs.Count} tÃ i liá»‡u";
    }

    // â•â•â• About dialog â•â•â•
    [RelayCommand]
    private async Task ShowAboutAsync()
    {
        var version = StudyDocumentManager.Services.AppVersion.Current;
        await _dialogService.ShowMessageAsync("Giá»›i thiá»‡u",
            $"Study Document Manager v{version}\n\n" +
            "á»¨ng dá»¥ng quáº£n lÃ½ tÃ i liá»‡u há»c táº­p cÃ¡ nhÃ¢n\n" +
            "Framework: Avalonia UI (.NET 9)\n" +
            "Database: SQLite (local)\n\n" +
            "Â© 2025 hayato-shino05\n" +
            "GitHub: github.com/hayato-shino05/study-document-manager");
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

/// <summary>
/// Item model for the category tree panel (left side of dashboard).
/// </summary>
public class CategoryTreeItem
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
    public string IconKey { get; set; } = "IconCategory";
    public string FilterType { get; set; } = "";
    public string FilterValue { get; set; } = "";
    public bool IsIndented { get; set; }
    public bool IsHeader { get; set; } // true = section separator, not clickable

    public string DisplayText => IsHeader ? Name : $"{Name} ({Count})";
    public Avalonia.Thickness IndentMargin => IsIndented ? new Avalonia.Thickness(16, 0, 0, 0) : new Avalonia.Thickness(0);

    /// <summary>
    /// Returns the appropriate icon for this tree item.
    /// For "type" filter nodes: loads the file-type PNG from Assets via DocumentTypeIconConverter.
    /// For all others: resolves a DrawingImage from application resources.
    /// </summary>
    public Avalonia.Media.IImage? ResolvedIconSource
    {
        get
        {
            // File-type nodes â†’ load real file icon from Assets
            if (FilterType == "type")
            {
                return StudyDocumentManager.Converters.DocumentTypeIconConverter.Instance
                    .Convert(IconKey, typeof(Avalonia.Media.IImage), null,
                             System.Globalization.CultureInfo.InvariantCulture)
                    as Avalonia.Media.IImage;
            }

            // All other nodes â†’ DrawingImage from resource dictionary
            var app = Avalonia.Application.Current;
            if (app == null) return null;

            if (app.Resources.TryGetResource(IconKey, Avalonia.Styling.ThemeVariant.Default, out var resource) &&
                resource is Avalonia.Media.IImage img)
            {
                return img;
            }

            foreach (var style in app.Styles)
            {
                if (style is Avalonia.Styling.Styles styleGroup &&
                    styleGroup.Resources.TryGetResource(IconKey, Avalonia.Styling.ThemeVariant.Default, out var res) &&
                    res is Avalonia.Media.IImage img2)
                {
                    return img2;
                }
            }
            return null;
        }
    }

    // Keep IconSource for legacy compat
    public Avalonia.Media.IImage? IconSource => ResolvedIconSource;
}
