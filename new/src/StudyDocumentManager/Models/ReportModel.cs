using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class ReportModel : ModelBase
{
    [ObservableProperty] private ObservableCollection<ChartDataItem> _bySubjectData = new();
    [ObservableProperty] private ObservableCollection<ChartDataItem> _byTypeData = new();
    [ObservableProperty] private ObservableCollection<ChartDataItem> _byDayData = new();
    [ObservableProperty] private ObservableCollection<ChartDataItem> _byMonthData = new();

    [ObservableProperty] private string _selectedTab = "subject";

    public ReportModel()
    {
        LoadAllData();
    }

    [RelayCommand]
    private void LoadAllData()
    {
        BySubjectData = CreateChartData(
            DatabaseHelper.GetDocumentsBySubject().Select(x => new ChartDataItem { Label = x.Label, Value = x.Count }));

        ByTypeData = CreateChartData(
            DatabaseHelper.GetDocumentsByType().Select(x => new ChartDataItem { Label = x.Label, Value = x.Count }));

        ByDayData = CreateChartData(
            DatabaseHelper.GetDocumentsByDay(7).Select(x => new ChartDataItem { Label = x.Label, Value = x.Count }));

        ByMonthData = CreateChartData(
            DatabaseHelper.GetDocumentsByMonth(12).Select(x => new ChartDataItem { Label = x.Label, Value = x.Count }));
    }

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
