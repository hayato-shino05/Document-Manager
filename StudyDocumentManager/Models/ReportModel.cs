using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class ReportModel : ModelBase
{
    [ObservableProperty] private ObservableCollection<ChartDataItem> _bySubjectData = new();
    [ObservableProperty] private ObservableCollection<ChartDataItem> _byTypeData = new();
    [ObservableProperty] private ObservableCollection<ChartDataItem> _byDayData = new();
    [ObservableProperty] private ObservableCollection<ChartDataItem> _byMonthData = new();
    [ObservableProperty] private ObservableCollection<StatusCountItem> _byStatusData = new();
    [ObservableProperty] private bool _hasDayData;
    [ObservableProperty] private bool _hasMonthData;


    private readonly IReportRepository _reportRepo;
    private readonly IDocumentRepository? _documentRepo;
    private readonly ILocalizationService? _loc;
    private Dictionary<string, int>? _lastStatusCounts;
    private bool _localizationSubscribed;

    public ReportModel(IReportRepository reportRepo, IDocumentRepository? documentRepo = null, ILocalizationService? localizationService = null)
    {
        _reportRepo = reportRepo;
        _documentRepo = documentRepo;
        _loc = localizationService;
        LoadAllData();
    }

    public void AttachLocalization()
    {
        if (_localizationSubscribed || _loc == null)
            return;

        _loc.LanguageChanged += OnLanguageChanged;
        _localizationSubscribed = true;
        RefreshStatusLabels();
    }

    public void DetachLocalization()
    {
        if (!_localizationSubscribed || _loc == null)
            return;

        _loc.LanguageChanged -= OnLanguageChanged;
        _localizationSubscribed = false;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RefreshStatusLabels();

    private void RefreshStatusLabels()
    {
        if (_lastStatusCounts == null)
            return;

        ByStatusData = BuildStatusItems(_lastStatusCounts);
    }

    [RelayCommand]
    private void LoadAllData()
    {
        BySubjectData = CreateChartData(
            _reportRepo.GetBySubject().Select(x => new ChartDataItem { Label = x.Label, Value = x.Count }));

        ByTypeData = CreateChartData(
            _reportRepo.GetByType().Select(x => new ChartDataItem { Label = x.Label, Value = x.Count }));

        ByDayData = CreateChartData(
            _reportRepo.GetByDay(7).Select(x => new ChartDataItem { Label = x.Label, Value = x.Count }));

        ByMonthData = CreateChartData(
            _reportRepo.GetByMonth(12).Select(x => new ChartDataItem { Label = x.Label, Value = x.Count }));

        HasDayData = ByDayData.Any(item => item.Value > 0);
        HasMonthData = ByMonthData.Any(item => item.Value > 0);
        ByStatusData = CreateStatusCounts();
    }

    private ObservableCollection<StatusCountItem> CreateStatusCounts()
    {
        var counts = _documentRepo?.GetStatusCounts() ?? [];
        _lastStatusCounts = counts;
        return BuildStatusItems(counts);
    }

    private ObservableCollection<StatusCountItem> BuildStatusItems(Dictionary<string, int> counts)
    {
        var items = DocumentStatus.All
            .Select(kind => new StatusCountItem
            {
                Kind = kind,
                Label = GetStatusLabel(kind),
                Value = counts.TryGetValue(kind, out int count) ? count : 0
            })
            .ToList();
        var max = items.Count > 0 ? items.Max(x => x.Value) : 1;
        if (max == 0) max = 1;
        foreach (var item in items) item.MaxValue = max;
        return new ObservableCollection<StatusCountItem>(items);
    }

    private string GetStatusLabel(string status) => status switch
    {
        DocumentStatus.Unread => Loc("DS_Kind_Unread"),
        DocumentStatus.InProgress => Loc("DS_Kind_InProgress"),
        DocumentStatus.Read => Loc("DS_Kind_Read"),
        DocumentStatus.NeedsAction => Loc("DS_Kind_NeedsAction"),
        DocumentStatus.Completed => Loc("DS_Kind_Completed"),
        DocumentStatus.Archived => Loc("DS_Kind_Archived"),
        _ => status
    };

    private string Loc(string key) => _loc?[key] ?? key;

    private static ObservableCollection<ChartDataItem> CreateChartData(IEnumerable<ChartDataItem> items)
    {
        var list = items.ToList();
        var max = list.Count > 0 ? list.Max(x => x.Value) : 1;
        if (max == 0) max = 1;
        foreach (var item in list) item.MaxValue = max;
        return new ObservableCollection<ChartDataItem>(list);
    }
}

public class ChartDataItem
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }

    // Set by parent to the max value in the collection for proportional scaling
    public int MaxValue { get; set; } = 1;

    // For bar chart visualization (proportional width, max ~300px)
    public double BarWidth => MaxValue > 0 ? (double)Value / MaxValue * 300.0 : 0;
}

public class StatusCountItem
{
    public string Kind { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
    public int MaxValue { get; set; } = 1;

    public double BarWidth => MaxValue > 0 ? (double)Value / MaxValue * 300.0 : 0;
}
