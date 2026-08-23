using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
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

    [Fact]
    public void ReportModel_LanguageChanged_RebuildsStatusLabelsWithoutRequeryingRepositories()
    {
        var reportRepo = new CountingReportRepository();
        var docRepo = new CountingDocumentRepository();
        var loc = new SwitchableLocalizationService();
        var model = new ReportModel(reportRepo, docRepo, loc);
        model.AttachLocalization();

        var countsBefore = model.ByStatusData.ToDictionary(i => i.Kind, i => i.Value);
        var queriesBefore = reportRepo.TotalCalls + docRepo.StatusCalls;

        loc.SwitchTo("JA");

        Assert.Equal(6, model.ByStatusData.Count);
        Assert.All(model.ByStatusData, item => Assert.StartsWith("JA:", item.Label));
        Assert.DoesNotContain(model.ByStatusData, item => item.Label.StartsWith("EN:", StringComparison.Ordinal));
        Assert.Equal(countsBefore, model.ByStatusData.ToDictionary(i => i.Kind, i => i.Value));
        Assert.Equal(queriesBefore, reportRepo.TotalCalls + docRepo.StatusCalls);
    }

    [Fact]
    public void ReportModel_DetachLocalization_FreezesLabelsOnLanguageChangeWithoutThrowing()
    {
        var loc = new SwitchableLocalizationService();
        var model = new ReportModel(new CountingReportRepository(), new CountingDocumentRepository(), loc);

        model.DetachLocalization();
        model.AttachLocalization();
        model.DetachLocalization();

        loc.SwitchTo("JA");

        Assert.Equal(6, model.ByStatusData.Count);
        Assert.All(model.ByStatusData, item => Assert.StartsWith("EN:", item.Label));
    }

    [Fact]
    public void ReportModel_DoubleAttachLocalization_RebuildsOnceWithSingleSetOfSixItems()
    {
        var loc = new SwitchableLocalizationService();
        var model = new ReportModel(new CountingReportRepository(), new CountingDocumentRepository(), loc);

        model.AttachLocalization();
        model.AttachLocalization();

        loc.SwitchTo("JA");

        Assert.Equal(6, model.ByStatusData.Count);
        Assert.All(model.ByStatusData, item => Assert.StartsWith("JA:", item.Label));
        Assert.All(model.ByStatusData, item => Assert.DoesNotContain("EN:", item.Label, StringComparison.Ordinal));
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

    private sealed class CountingReportRepository : IReportRepository
    {
        public int TotalCalls { get; private set; }

        public List<(string Label, int Count)> GetBySubject() { TotalCalls++; return []; }
        public List<(string Label, int Count)> GetByType() { TotalCalls++; return []; }
        public List<(string Label, int Count)> GetByDay(int days) { TotalCalls++; return []; }
        public List<(string Label, int Count)> GetByMonth(int months) { TotalCalls++; return []; }
    }

    private sealed class CountingDocumentRepository : IDocumentRepository
    {
        public int StatusCalls { get; private set; }

        public Dictionary<string, int> GetStatusCounts()
        {
            StatusCalls++;
            return new Dictionary<string, int> { [DocumentStatus.Unread] = 3 };
        }

        public List<StudyDocument> GetAll() => [];
        public StudyDocument? GetById(int id) => null;
        public List<StudyDocument> Search(string keyword) => [];
        public List<StudyDocument> Filter(string subject, string type) => [];
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type,
            DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => [];
        public bool Add(StudyDocument document) => true;
        public bool AddWithCatalogs(StudyDocument document) => true;
        public bool Update(StudyDocument document) => true;
        public bool Delete(int id) => true;
        public List<string> GetDistinctSubjects() => [];
        public List<string> GetDistinctTypes() => [];
        public List<string> GetDistinctTags() => [];
        public List<StudyDocument> GetUpcomingDeadlines(int days) => [];
        public List<StudyDocument> GetOverdueDocuments() => [];
        public void EnsureSubjectExists(string subject) { }
        public void EnsureTypeExists(string type) { }
    }

    private sealed class SwitchableLocalizationService : ILocalizationService
    {
        private string _prefix = "EN";

        public string this[string key] => $"{_prefix}:{key}";
        public SupportedLanguage CurrentLanguage => SupportedLanguage.Japanese;
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [];

        public event EventHandler? LanguageChanged;

        public void SetLanguage(SupportedLanguage language) { }

        public void SwitchTo(string prefix)
        {
            _prefix = prefix;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
