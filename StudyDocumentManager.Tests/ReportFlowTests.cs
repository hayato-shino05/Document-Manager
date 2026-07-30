using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

public class ReportFlowTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DatabaseHelper _db;

    public ReportFlowTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sdm_report_{Guid.NewGuid():N}.db");
        _db = new DatabaseHelper();
        _db.SetDatabasePath(_dbPath);
        _db.InitializeDatabase();
    }

    [Fact]
    public void ReportModel_AllZeroPeriodicSeries_ReportsNoDayAndMonthData()
    {
        var model = new ReportModel(new ZeroReportRepository());

        Assert.False(model.HasDayData);
        Assert.False(model.HasMonthData);
        Assert.Equal(7, model.ByDayData.Count);
        Assert.Equal(12, model.ByMonthData.Count);
        Assert.All(model.ByDayData, item => Assert.Equal(0, item.Value));
        Assert.All(model.ByMonthData, item => Assert.Equal(0, item.Value));
    }

    [Fact]
    public void GetDocumentsByDay_InternalGap_ReturnsZeroSlotInSeries()
    {
        var repo = new StudyDocumentManager.Data.Repositories.DocumentRepository(_db);
        repo.Add(new StudyDocumentManager.Core.Entities.StudyDocument { Name = "Day A" });
        repo.Add(new StudyDocumentManager.Core.Entities.StudyDocument { Name = "Day B" });

        var docs = repo.GetAll().OrderBy(document => document.Name).ToList();
        using (var connection = new SqliteConnection(_db.ConnectionString))
        {
            connection.Open();
            Execute(connection, $"UPDATE documents SET created_at = date('now','localtime','-6 days') WHERE id = {docs[0].Id}");
            Execute(connection, $"UPDATE documents SET created_at = date('now','localtime','-4 days') WHERE id = {docs[1].Id}");
        }

        var data = _db.GetDocumentsByDay(7);

        Assert.Equal(7, data.Count);
        Assert.Equal(1, data[0].Count);
        Assert.Equal(0, data[1].Count);
        Assert.Equal(1, data[2].Count);
    }

    [AvaloniaFact]
    public void Report_EmptyPeriodicSeries_ShowsNoDataForDayAndMonth()
    {
        var model = new ReportModel(new ZeroReportRepository());
        var view = new Report { DataContext = model };
        var window = new Window { Content = view };

        try
        {
            window.Show();
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var localization = (LocalizationService)Application.Current!.Resources["Loc"]!;
            var noDataText = localization["Report_NoData"];
            var texts = view.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsVisible).Select(text => text.Text).ToList();
            Assert.True(texts.Count(text => text == noDataText) >= 2);
        }
        finally
        {
            window.Close();
        }
    }

    public void Dispose()
    {
        _db.CloseAllConnections();
        try
        {
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
        catch
        {
        }
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private sealed class ZeroReportRepository : IReportRepository
    {
        public List<(string Label, int Count)> GetBySubject() => [];
        public List<(string Label, int Count)> GetByType() => [];
        public List<(string Label, int Count)> GetByDay(int days)
            => Enumerable.Range(0, days).Select(index => ($"D{index}", 0)).ToList();
        public List<(string Label, int Count)> GetByMonth(int months)
            => Enumerable.Range(0, months).Select(index => ($"M{index}", 0)).ToList();
    }
}
