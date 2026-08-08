using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class TreeMapModel : ModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IReportRepository _reportRepo;

    [ObservableProperty] private ObservableCollection<TreeMapItem> _items = new();
    [ObservableProperty] private string _selectedMode = "subject"; // "subject" or "type"
    [ObservableProperty] private int _totalDocuments;

    // Predefined colors for tree map blocks
    private static readonly string[] BlockColors =
    {
        "#1D4ED8", "#B91C1C", "#15803D", "#B45309", "#6D28D9",
        "#0E7490", "#BE185D", "#0F766E", "#C2410C", "#4338CA",
        "#4D7C0F", "#9F1239", "#0369A1", "#7E22CE", "#A16207"
    };

    public TreeMapModel(INavigationService navigationService, IReportRepository reportRepo)
    {
        _navigationService = navigationService;
        _reportRepo = reportRepo;
        LoadData();
    }

    partial void OnSelectedModeChanged(string value) => LoadData();

    [RelayCommand]
    private void LoadData()
    {
        var data = SelectedMode == "type"
            ? _reportRepo.GetByType()
            : _reportRepo.GetBySubject();

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
    public string Color { get; set; } = "#1D4ED8";
    public double Percentage { get; set; }
    public string DisplayText { get; set; } = string.Empty;
}
