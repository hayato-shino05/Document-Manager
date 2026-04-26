using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class TreeMapModel : ModelBase
{
    private readonly INavigationService _navigationService;

    [ObservableProperty] private ObservableCollection<TreeMapItem> _items = new();
    [ObservableProperty] private string _selectedMode = "subject"; // "subject" or "type"
    [ObservableProperty] private int _totalDocuments;

    // Predefined colors for tree map blocks
    private static readonly string[] BlockColors =
    {
        "#3b82f6", "#ef4444", "#22c55e", "#f59e0b", "#8b5cf6",
        "#06b6d4", "#ec4899", "#14b8a6", "#f97316", "#6366f1",
        "#84cc16", "#e11d48", "#0ea5e9", "#a855f7", "#eab308"
    };

    public TreeMapModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        LoadData();
    }

    partial void OnSelectedModeChanged(string value) => LoadData();

    [RelayCommand]
    private void LoadData()
    {
        var data = SelectedMode == "type"
            ? DatabaseHelper.GetDocumentsByType()
            : DatabaseHelper.GetDocumentsBySubject();

        TotalDocuments = data.Sum(d => d.Count);

        var list = new ObservableCollection<TreeMapItem>();
        for (int i = 0; i < data.Count; i++)
        {
            var item = data[i];
            double percentage = TotalDocuments > 0 ? (double)item.Count / TotalDocuments * 100 : 0;
            list.Add(new TreeMapItem
            {
                Label = item.Label,
                Count = item.Count,
                Color = BlockColors[i % BlockColors.Length],
                Percentage = percentage,
                DisplayText = $"{item.Label}\n{item.Count} ({percentage:F1}%)"
            });
        }

        Items = list;
    }

    [RelayCommand]
    private void ShowBySubject()
    {
        SelectedMode = "subject";
    }

    [RelayCommand]
    private void ShowByType()
    {
        SelectedMode = "type";
    }

    [RelayCommand]
    private void GoBack() => _navigationService.NavigateTo("dashboard");
}

public class TreeMapItem
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Color { get; set; } = "#3b82f6";
    public double Percentage { get; set; }
    public string DisplayText { get; set; } = string.Empty;
}
