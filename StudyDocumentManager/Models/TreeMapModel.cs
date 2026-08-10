using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class TreeMapModel : ModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IReportRepository _reportRepo;

    [ObservableProperty] private ObservableCollection<TreeMapItem> _items = new();
    [ObservableProperty] private string _selectedMode = "subject";
    [ObservableProperty] private int _totalDocuments;

    private static readonly string[] BlockColors =
    {
        "#1D4ED8", "#B91C1C", "#15803D", "#B45309", "#0E7490",
        "#0F766E", "#C2410C", "#4D7C0F", "#0369A1", "#A16207",
        "#9A3412", "#166534", "#075985", "#92400E", "#7F1D1D"
    };

    public TreeMapModel(
        INavigationService navigationService,
        IDocumentRepository documentRepository,
        IReportRepository reportRepo)
    {
        _navigationService = navigationService;
        _documentRepository = documentRepository;
        _reportRepo = reportRepo;
        LoadData();
    }

    partial void OnSelectedModeChanged(string value) => LoadData();

    [RelayCommand]
    private void LoadData()
    {
        var data = SelectedMode switch
        {
            "all" => _documentRepository.GetAll()
                .Select(document => (Label: document.Name, Count: 1))
                .ToList(),
            "type" => _reportRepo.GetByType(),
            _ => _reportRepo.GetBySubject()
        };

        TotalDocuments = data.Sum(item => item.Count);

        var list = new ObservableCollection<TreeMapItem>();
        for (var i = 0; i < data.Count; i++)
        {
            var item = data[i];
            var percentage = TotalDocuments > 0 ? (double)item.Count / TotalDocuments * 100 : 0;
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
    private void ShowAll()
    {
        SelectedMode = "all";
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
