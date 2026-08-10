using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using StudyDocumentManager.Converters;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class DashboardTreeMapRegressionTests
{
    [AvaloniaFact]
    public void TreeMap_RendersWithoutPaddingCastFailure()
    {
        var model = new TreeMapModel(new NavigationStub(), new DocumentRepositoryStub(), new ReportStub
        {
            Subjects = [("数学", 2)]
        });
        var view = new TreeMap { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "数学");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TreeMap_ExposesAllItemsMode()
    {
        var model = new TreeMapModel(
            new NavigationStub(),
            new DocumentRepositoryStub(new StudyDocument { Id = 1, Name = "Guide" }),
            new ReportStub());
        var view = new TreeMap { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            var allItemsButton = view.GetVisualDescendants().OfType<Button>()
                .Single(button => AutomationProperties.GetAutomationId(button) == "TreeMap_AllItems");

            allItemsButton.Command!.Execute(null);

            Assert.Equal("all", model.SelectedMode);
            Assert.Equal("Guide", model.Items[0].Label);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DashboardFilterLabelConverter_LocalizesAllFilterSentinel()
    {
        var localization = new LocalizationService();
        Application.Current!.Resources["Loc"] = localization;
        var converter = DashboardFilterLabelConverter.Instance;

        Assert.Equal(localization["Filter_AllSubjects"], converter.Convert(
            "Filter_AllSubjects", typeof(string), "Filter_AllSubjects", CultureInfo.InvariantCulture));
        Assert.Equal(localization["Filter_AllTypes"], converter.Convert(
            "Filter_AllTypes", typeof(string), "Filter_AllTypes", CultureInfo.InvariantCulture));
    }

    private sealed class ReportStub : IReportRepository
    {
        public List<(string Label, int Count)> Subjects { get; init; } = [];
        public List<(string Label, int Count)> Types { get; init; } = [];
        public List<(string Label, int Count)> GetBySubject() => Subjects;
        public List<(string Label, int Count)> GetByType() => Types;
        public List<(string Label, int Count)> GetByDay(int days = 7) => [];
        public List<(string Label, int Count)> GetByMonth(int months = 12) => [];
    }

    private sealed class NavigationStub : INavigationService
    {
        public bool CanGoBack => false;
        public void NavigateTo(string viewKey) { }
        public void NavigateTo(string viewKey, object? parameter) { }
        public void GoBack() { }
    }
}
