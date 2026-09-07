using System.Globalization;
using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class CleanInstallAndFirstRunLifecycleTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _dbPath;
    private readonly string _logPath;

    public CleanInstallAndFirstRunLifecycleTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"sdm_clean_install_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        _dbPath = Path.Combine(_tempDirectory, "data", "study_documents.db");
        _logPath = Path.Combine(_tempDirectory, "logs", "startup.log");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void CleanInstall_InitializesDatabase_CreatesAllExpectedTablesAndSeedsData()
    {
        Assert.False(File.Exists(_dbPath));

        var diagnostics = new FileStartupDiagnostics(_logPath);
        var db = new DatabaseHelper(diagnostics);
        db.SetDatabasePath(_dbPath);

        db.InitializeDatabase();

        Assert.True(File.Exists(_dbPath));
        Assert.True(File.Exists(_logPath));

        var logContent = File.ReadAllText(_logPath);
        Assert.Contains("event=database_initialization_succeeded", logContent);
        Assert.DoesNotContain("event=database_initialization_failed", logContent);

        using var connection = new SqliteConnection(db.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        using var reader = command.ExecuteReader();

        var tables = new List<string>();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        var expectedTables = new[]
        {
            "app_settings",
            "assignment_documents",
            "assignments",
            "categories",
            "collection_items",
            "collections",
            "courses",
            "document_relations",
            "document_types",
            "documents",
            "import_inbox",
            "office_document_metadata",
            "personal_notes",
            "recent_files",
            "saved_searches",
            "semesters",
            "student_context",
            "watched_folders"
        };

        Assert.True(tables.Count >= 18, $"Expected at least 18 tables, found {tables.Count}");
        foreach (var expectedTable in expectedTables)
        {
            Assert.Contains(expectedTable, tables);
        }

        using var checkVersionCmd = connection.CreateCommand();
        checkVersionCmd.CommandText = "SELECT value FROM app_settings WHERE key = 'schema_version'";
        var schemaVersion = checkVersionCmd.ExecuteScalar()?.ToString();
        Assert.Equal("4", schemaVersion);

        using var checkCategoriesCmd = connection.CreateCommand();
        checkCategoriesCmd.CommandText = "SELECT COUNT(*) FROM categories";
        var categoryCount = Convert.ToInt32(checkCategoriesCmd.ExecuteScalar());
        Assert.True(categoryCount > 0, "Categories table should contain seeded categories.");
    }

    [Theory]
    [InlineData("vi-VN", SupportedLanguage.Vietnamese)]
    [InlineData("vi", SupportedLanguage.Vietnamese)]
    [InlineData("ja-JP", SupportedLanguage.Japanese)]
    [InlineData("ja", SupportedLanguage.Japanese)]
    [InlineData("en-US", SupportedLanguage.English)]
    [InlineData("en-GB", SupportedLanguage.English)]
    [InlineData("en", SupportedLanguage.English)]
    [InlineData("zh-CN", SupportedLanguage.Chinese)]
    [InlineData("zh-TW", SupportedLanguage.Chinese)]
    [InlineData("zh", SupportedLanguage.Chinese)]
    [InlineData("fr-FR", SupportedLanguage.Japanese)]
    [InlineData("de-DE", SupportedLanguage.Japanese)]
    [InlineData("es-ES", SupportedLanguage.Japanese)]
    [InlineData(null, SupportedLanguage.Japanese)]
    public void SupportedLanguageResolver_ResolvesCorrectLanguage_FromOsCulture(string? cultureName, SupportedLanguage expectedLanguage)
    {
        var culture = cultureName is not null ? new CultureInfo(cultureName) : null;
        var resolution = SupportedLanguageResolver.Resolve(null, culture);

        Assert.Equal(expectedLanguage, resolution.Language);
        Assert.False(resolution.UsedSavedLanguage);
    }

    [Fact]
    public void CleanInstall_OnboardingLifecycle_WorksEndToEndWithDatabasePersistence()
    {
        var diagnostics = new FileStartupDiagnostics(_logPath);
        var db = new DatabaseHelper(diagnostics);
        db.SetDatabasePath(_dbPath);
        db.InitializeDatabase();

        var settingsRepo = new SettingsRepository(db);
        var loc = new LocalizationService();

        var onboarding1 = new OnboardingModel(settingsRepo, loc);
        Assert.True(onboarding1.ShouldShow);
        Assert.Equal(0, onboarding1.CurrentStepIndex);
        Assert.True(onboarding1.IsStep0);
        Assert.False(onboarding1.CanGoPrevious);
        Assert.True(onboarding1.CanGoNext);
        Assert.False(onboarding1.IsLastStep);
        Assert.Equal("1 / 5", onboarding1.StepNumberText);
        Assert.Equal(20.0, onboarding1.StepProgress);

        onboarding1.NextStepCommand.Execute(null);
        Assert.Equal(1, onboarding1.CurrentStepIndex);
        Assert.True(onboarding1.IsStep1);
        Assert.True(onboarding1.CanGoPrevious);
        Assert.True(onboarding1.CanGoNext);
        Assert.False(onboarding1.IsLastStep);
        Assert.Equal("2 / 5", onboarding1.StepNumberText);
        Assert.Equal(40.0, onboarding1.StepProgress);

        onboarding1.NextStepCommand.Execute(null);
        Assert.Equal(2, onboarding1.CurrentStepIndex);
        Assert.True(onboarding1.IsStep2);
        Assert.Equal("3 / 5", onboarding1.StepNumberText);
        Assert.Equal(60.0, onboarding1.StepProgress);

        onboarding1.NextStepCommand.Execute(null);
        Assert.Equal(3, onboarding1.CurrentStepIndex);
        Assert.True(onboarding1.IsStep3);
        Assert.Equal("4 / 5", onboarding1.StepNumberText);
        Assert.Equal(80.0, onboarding1.StepProgress);

        onboarding1.NextStepCommand.Execute(null);
        Assert.Equal(4, onboarding1.CurrentStepIndex);
        Assert.True(onboarding1.IsStep4);
        Assert.True(onboarding1.CanGoPrevious);
        Assert.False(onboarding1.CanGoNext);
        Assert.True(onboarding1.IsLastStep);
        Assert.Equal("5 / 5", onboarding1.StepNumberText);
        Assert.Equal(100.0, onboarding1.StepProgress);

        var completedFired = false;
        onboarding1.Completed += (_, _) => completedFired = true;

        onboarding1.FinishCommand.Execute(null);
        Assert.True(completedFired);

        Assert.Equal("true", settingsRepo.GetSetting(OnboardingModel.CompletionKey));

        var onboarding2 = new OnboardingModel(settingsRepo, loc);
        Assert.False(onboarding2.ShouldShow);
    }

    [Fact]
    public void CleanInstall_OnboardingLanguageSwitching_PersistsToAppSettingsAndUpdatesLocalizationService()
    {
        var diagnostics = new FileStartupDiagnostics(_logPath);
        var db = new DatabaseHelper(diagnostics);
        db.SetDatabasePath(_dbPath);
        db.InitializeDatabase();

        var settingsRepo = new SettingsRepository(db);
        var loc = new LocalizationService();

        var onboarding = new OnboardingModel(settingsRepo, loc);

        onboarding.SelectedLanguage = SupportedLanguage.Vietnamese;
        Assert.Equal(SupportedLanguage.Vietnamese, loc.CurrentLanguage);
        Assert.Equal(nameof(SupportedLanguage.Vietnamese), settingsRepo.GetSetting("language"));

        onboarding.SelectedLanguage = SupportedLanguage.Japanese;
        Assert.Equal(SupportedLanguage.Japanese, loc.CurrentLanguage);
        Assert.Equal(nameof(SupportedLanguage.Japanese), settingsRepo.GetSetting("language"));

        onboarding.SelectedLanguage = SupportedLanguage.English;
        Assert.Equal(SupportedLanguage.English, loc.CurrentLanguage);
        Assert.Equal(nameof(SupportedLanguage.English), settingsRepo.GetSetting("language"));

        onboarding.SelectedLanguage = SupportedLanguage.Chinese;
        Assert.Equal(SupportedLanguage.Chinese, loc.CurrentLanguage);
        Assert.Equal(nameof(SupportedLanguage.Chinese), settingsRepo.GetSetting("language"));
    }

    [Fact]
    public void CleanInstall_SimulateFirstAppLaunch_FullFlow()
    {
        var diagnostics = new FileStartupDiagnostics(_logPath);
        var db = new DatabaseHelper(diagnostics);
        db.SetDatabasePath(_dbPath);

        db.InitializeDatabase();

        var settings = new SettingsRepository(db);
        var loc = new LocalizationService();

        var osCulture = new CultureInfo("vi-VN");
        var savedLanguage = settings.GetSetting("language");
        var initialLang = SupportedLanguageResolver.Resolve(savedLanguage, osCulture).Language;
        loc.SetLanguage(initialLang);

        Assert.Equal(SupportedLanguage.Vietnamese, loc.CurrentLanguage);

        var onboarding = new OnboardingModel(settings, loc);
        Assert.True(onboarding.ShouldShow);

        onboarding.SelectedLanguage = SupportedLanguage.English;
        Assert.Equal(SupportedLanguage.English, loc.CurrentLanguage);
        Assert.Equal("English", settings.GetSetting("language"));

        onboarding.FinishCommand.Execute(null);

        var restartedSavedLang = settings.GetSetting("language");
        var restartedResolvedLang = SupportedLanguageResolver.Resolve(restartedSavedLang, osCulture);
        Assert.Equal(SupportedLanguage.English, restartedResolvedLang.Language);
        Assert.True(restartedResolvedLang.UsedSavedLanguage);

        var nextOnboarding = new OnboardingModel(settings, loc);
        Assert.False(nextOnboarding.ShouldShow);

        db.CloseAllConnections();
        SqliteConnection.ClearAllPools();
    }
}
