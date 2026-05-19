using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Core.Services;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class DashboardModel : ModelBase
{
    private readonly IDocument _repository;
    private readonly ICategory _categoryRepo;
    private readonly ICollection _collectionRepo;
    private readonly IRecentFile _recentFileRepo;
    private readonly IDialogService _dialogService;
    private readonly IFileDialogService _fileDialogService;
    private readonly ICustomDialogService _customDialogService;
    private readonly INavigationService _navigationService;
    private readonly IClipboardService _clipboardService;
    private readonly IProcessLauncherService _processLauncher;
    private bool _isLoadingData; // Guard flag to prevent re-entrant ApplyFilters during LoadData
    private bool _isApplyingFilters; // Re-entrancy guard for ApplyFilters

    // â•â•â• Document list â•â•â•
    [ObservableProperty]
    private List<StudyDocument> _documents = new();

    [ObservableProperty]
    private StudyDocument? _selectedDocument;

    // ——— Preview ——— 
    public string PreviewIcon => SelectedDocument switch
    {
        null => "📄",
        var d when d.Type is "Hình ảnh" => "🖼️",
        var d when d.Type is "Video" => "🎬",
        var d when d.Type is "Audio" => "🎵",
        var d when d.Type is "Nén" => "📦",
        var d when d.Type is "Tài liệu" => "📁",
        _ => "📄"
    };

    partial void OnSelectedDocumentChanged(StudyDocument? value)
    {
        // Skip property change notifications during bulk updates to prevent
        // re-entrant rendering loops that cause StackOverflowException
        if (_isLoadingData || _isApplyingFilters) return;
        OnPropertyChanged(nameof(PreviewIcon));
    }

    // ——— Stats ——— 
    [ObservableProperty] private int _totalDocuments;
    [ObservableProperty] private int _importantDocuments;
    [ObservableProperty] private int _overdueDocuments;
    [ObservableProperty] private int _totalCategories;
    [ObservableProperty] private int _noFileDocuments;
    [ObservableProperty] private int _deletedCount;
    [ObservableProperty] private string _statusText = "Sẵn sàng";

    // ——— Search & Filter ——— 
    [ObservableProperty] private string _searchKeyword = string.Empty;
    [ObservableProperty] private List<string> _subjects = new();
    [ObservableProperty] private List<string> _types = new();
    [ObservableProperty] private string _selectedSubject = "Tất cả";
    [ObservableProperty] private string _selectedType = "Tất cả";

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
        IDocument repository,
        ICategory categoryRepo,
        ICollection collectionRepo,
        IRecentFile recentFileRepo,
        IDialogService dialogService,
        IFileDialogService fileDialogService,
        ICustomDialogService customDialogService,
        INavigationService navigationService,
        IClipboardService clipboardService,
        IProcessLauncherService processLauncher)
    {
        _repository = repository;
        _categoryRepo = categoryRepo;
        _collectionRepo = collectionRepo;
        _recentFileRepo = recentFileRepo;
        _dialogService = dialogService;
        _fileDialogService = fileDialogService;
        _customDialogService = customDialogService;
        _navigationService = navigationService;
        _clipboardService = clipboardService;
        _processLauncher = processLauncher;
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

        // Load documents
        var docs = _repository.GetAll();

        // Stats
        TotalDocuments = docs.Count;
        ImportantDocuments = docs.Count(d => d.IsImportant);
        OverdueDocuments = _repository.GetOverdueDocuments().Count;
        NoFileDocuments = docs.Count(d => string.IsNullOrEmpty(d.FilePath));
        TotalCategories = _categoryRepo.GetAllSubjects().Count;
        DeletedCount = _repository.GetDeletedDocumentCount();

        // Load filter dropdowns — clear and re-populate EXISTING collections
        // to avoid replacing the ObservableCollection reference which causes
        // Avalonia ComboBox binding loop (StackOverflowException).
        var subjects = _categoryRepo.GetAllSubjects();
        var types = _categoryRepo.GetAllTypes();

        var subjectList = new List<string> { "Tất cả" };
        subjectList.AddRange(subjects);
        Subjects = subjectList;

        var typeList = new List<string> { "Tất cả" };
        typeList.AddRange(types);
        Types = typeList;

        // Reset filter AFTER collections are populated
        SelectedSubject = "Tất cả";
        SelectedType = "Tất cả";

        // Assign new List (not ObservableCollection) — DataGrid ItemsSource
        // is set from code-behind to avoid binding loop.
        Documents = docs.ToList();

        // Build category tree
        BuildCategoryTree(docs, subjects, types);

        // Update status
        StatusText = $"Tổng: {TotalDocuments} tài liệu | Quan trọng: {ImportantDocuments} | Quá hạn: {OverdueDocuments}";

        // Notify stat card properties changed
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(SubjectCount));
        OnPropertyChanged(nameof(ImportantCount));
        OnPropertyChanged(nameof(OverdueCount));
        OnPropertyChanged(nameof(NoFileCount));
        OnPropertyChanged(nameof(RecycleBinCount));

        _isLoadingData = false;
    }

    private void BuildCategoryTree(IList<StudyDocument> docs, List<string> subjects, List<string> types)
    {
        var items = new List<CategoryTreeItem>();

        // ——————————————————————————————
        items.Add(new CategoryTreeItem
        {
            Name = "Tất cả tài liệu",
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
                Name = "Danh mục",
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
                Name = "Loại file",
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
            Name = "Quan trọng",
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
                    Name = "Bộ sưu tập",
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
                SelectedSubject = "Tất cả";
                SelectedType = "Tất cả";
                IsImportantOnly = false;
                break;
            case "subject":
                SelectedSubject = item.FilterValue;
                SelectedType = "Tất cả";
                IsImportantOnly = false;
                break;
            case "type":
                SelectedSubject = "Tất cả";
                SelectedType = item.FilterValue;
                IsImportantOnly = false;
                break;
            case "important":
                SelectedSubject = "Tất cả";
                SelectedType = "Tất cả";
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
                            ImportantDocuments = colDocs.Count(d => d.IsImportant);
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
            await _dialogService.ShowMessageAsync("Lỗi", "Không thể cập nhật danh mục.");
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
            string subject = SelectedSubject == "Tất cả" ? "" : SelectedSubject;
            string type = SelectedType == "Tất cả" ? "" : SelectedType;

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
            ImportantDocuments = results.Count(d => d.IsImportant);
        }
        finally
        {
            _isApplyingFilters = false;
        }
    }

    // ——— Document actions ——— 
    [RelayCommand]
    private void OpenFile()
    {
        if (SelectedDocument == null || string.IsNullOrEmpty(SelectedDocument.FilePath)) return;

        try
        {
            if (File.Exists(SelectedDocument.FilePath))
            {
                // Track recent file
                // NOTE: AddRecentFile is called via DashboardModel but RecentFileRepository is not injected here.
                // This is intentional — recent file tracking does not affect dashboard state.
                _recentFileRepo.Add(SelectedDocument.Id);

                _processLauncher.OpenFile(SelectedDocument.FilePath);
            }
        }
        catch { /* Ignore errors when opening files */ }
    }

    [RelayCommand]
    private async Task DeleteDocumentAsync()
    {
        if (SelectedDocument == null) return;

        var confirmed = await _dialogService.ShowConfirmAsync("Xác nhận xóa",
            $"Xóa tài liệu '{SelectedDocument.Name}' vào Thùng rác?",
            "Xoá", isDanger: true);
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
        var path = await _fileDialogService.ShowSaveFileAsync("Sao lưu", "backup_study_docs.db", "Database|*.db|All Files|*.*");
        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                _repository.BackupDatabase(path);
                await _dialogService.ShowMessageAsync("Thành công", $"Đã sao lưu cơ sở dữ liệu tại:\n{path}");
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("Lỗi", $"Không thể sao lưu: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        var path = await _fileDialogService.ShowSaveFileAsync("Xuất CSV", "documents_export.csv", "CSV|*.csv|All Files|*.*");
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var docs = Documents;
            using var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8);
            await writer.WriteLineAsync("ID,Name,Subject,Type,FilePath,Author,Tags,IsImportant,FileSize (MB),CreatedAt,Deadline,Notes");
            foreach (var doc in docs)
            {
                string line = string.Join(",",
                    doc.Id,
                    EscapeCsv(doc.Name),
                    EscapeCsv(doc.Subject),
                    EscapeCsv(doc.Type),
                    EscapeCsv(doc.FilePath),
                    EscapeCsv(doc.Author),
                    EscapeCsv(doc.Tags),
                    doc.IsImportant ? "Có" : "Không",
                    doc.FileSize?.ToString("F2") ?? "",
                    doc.CreatedAt.ToString("dd/MM/yyyy"),
                    doc.Deadline?.ToString("dd/MM/yyyy") ?? "",
                    EscapeCsv(doc.Notes)
                );
                await writer.WriteLineAsync(line);
            }
            await _dialogService.ShowMessageAsync("Thành công", $"Đã xuất {docs.Count} tài liệu ra file:\n{path}");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync("Lỗi", $"Không thể xuất CSV: {ex.Message}");
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
        var path = await _fileDialogService.ShowOpenFileAsync("Chọn file backup", "Database|*.db|All Files|*.*");
        if (string.IsNullOrWhiteSpace(path)) return;

        if (!File.Exists(path))
        {
            await _dialogService.ShowErrorAsync("Lỗi", "File backup không tồn tại.");
            return;
        }

        var confirmed = await _dialogService.ShowConfirmAsync("⚠️ Xác nhận",
            "Khôi phục sẽ GHI ĐÈ toàn bộ dữ liệu hiện tại. Bạn có chắc chắn?",
            "Ghi đè & Khôi phục", isDanger: true);
        if (confirmed)
        {
            try
            {
                File.Copy(path, _repository.DatabasePath, overwrite: true);
                LoadData();
                await _dialogService.ShowMessageAsync("Thành công", "Đã khôi phục cơ sở dữ liệu thành công!");
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("Lỗi", $"Không thể khôi phục: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        // Set guard BEFORE changing properties to block ApplyFilters re-entrance
        _isLoadingData = true;
        SearchKeyword = string.Empty;
        SelectedSubject = "Tất cả";
        SelectedType = "Tất cả";
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
            "Sửa Tags",
            $"Tags cho \"{SelectedDocument.Name}\":",
            SelectedDocument.Tags ?? "",
            "Ví dụ: lập trình, toán học, vật lý");

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
            "Sửa Ghi chú",
            $"Ghi chú cho \"{SelectedDocument.Name}\":",
            SelectedDocument.Notes ?? "",
            "Nhập ghi chú nội bộ...");

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
            await _dialogService.ShowMessageAsync("Thông báo",
                "Chưa có bộ sưu tập nào. Vui lòng tạo bộ sưu tập trong menu 'Bộ sưu tập' trước.");
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
            await _dialogService.ShowMessageAsync("Thành công",
                $"Đã thêm '{SelectedDocument.Name}' vào bộ sưu tập '{collection.Name}'.");
            Refresh();
        }
        else
        {
            await _dialogService.ShowMessageAsync("Thông báo",
                $"Tài liệu đã có trong bộ sưu tập '{collection.Name}' rồi.");
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

    // â• â• â•  About dialog â• â• â• 
    [RelayCommand]
    private async Task ShowAboutAsync()
    {
        var version = AppVersion.Current;
        await _dialogService.ShowMessageAsync("Giá»›i thiá»‡u",
            $"Study Document Manager v{version}\n\n" +
            "á»¨ng dá»¥ng quáº£n lÃ½ tÃ i liá»‡u há» c táº­p cÃ¡ nhÃ¢n\n" +
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
