using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using StudyDocumentManager.Core;
using StudyDocumentManager.Models;
using StudyDocumentManager.Views;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;
using StudyDocumentManager.Data.Repositories;
using StudyDocumentManager.Services;

namespace StudyDocumentManager;

public partial class App : Application
{
    public static ServiceProvider? Services { get; private set; }
    private static int _sessionStarted;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        RemoveDataAnnotationsValidationPlugin();
        var osCulture = CultureInfo.CurrentUICulture;
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        var db = Services.GetRequiredService<DatabaseHelper>();
        db.InitializeDatabase();

        var localization = Services.GetRequiredService<LocalizationService>();
        var settings = Services.GetRequiredService<ISettingsService>();
        InitializeLanguage(localization, settings, osCulture);

        // Avalonia needs the concrete type (implements INotifyPropertyChanged) for indexer binding refresh
        Resources["Loc"] = localization;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainModel = Services.GetRequiredService<MainWindowModel>();

            var navService = Services.GetRequiredService<NavigationService>();
            navService.SetMainModel(mainModel);

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainModel
            };

            var analytics = Services.GetRequiredService<IAnalyticsService>();
            AnalyticsDispatch.Capture(analytics, "app_opened");
            if (Interlocked.Exchange(ref _sessionStarted, 1) == 0)
                AnalyticsDispatch.Capture(analytics, "session_started");
        }

        base.OnFrameworkInitializationCompleted();
    }


    private static void InitializeLanguage(LocalizationService localization, ISettingsService settings, CultureInfo osCulture)
    {
        var savedLanguage = settings.GetSetting("language");
        var installerLanguage = SupportedLanguageResolver.ReadInstallerLanguage();
        var resolution = SupportedLanguageResolver.Resolve(savedLanguage, installerLanguage, osCulture);

        if (!resolution.UsedSavedLanguage)
            settings.SetSetting("language", resolution.Language.ToString());

        localization.SetLanguage(resolution.Language);
    }

    private static void RemoveDataAnnotationsValidationPlugin()
    {
        var validators = Avalonia.Data.Core.Plugins.BindingPlugins.DataValidators;
        for (var index = validators.Count - 1; index >= 0; index--)
        {
            if (validators[index] is Avalonia.Data.Core.Plugins.DataAnnotationsValidationPlugin)
                validators.RemoveAt(index);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Infrastructure
        services.AddSingleton<DatabaseHelper>();

        // Repositories
        services.AddSingleton<DocumentRepository>();
        services.AddSingleton<IDocumentRepository>(sp => sp.GetRequiredService<DocumentRepository>());
        services.AddSingleton<IRecycleBinRepository>(sp => sp.GetRequiredService<DocumentRepository>());
        services.AddSingleton<IBulkOperationRepository>(sp => sp.GetRequiredService<DocumentRepository>());
        services.AddSingleton<IFileIntegrityRepository>(sp => sp.GetRequiredService<DocumentRepository>());
        services.AddSingleton<ICategoryRepository, CategoryRepository>();
        services.AddSingleton<ICollectionRepository, CollectionRepository>();
        services.AddSingleton<IPersonalNoteRepository, PersonalNoteRepository>();
        services.AddSingleton<IRelatedDocumentRepository, RelatedDocumentRepository>();
        services.AddSingleton<IRecentFileRepository, RecentFileRepository>();
        services.AddSingleton<IReportRepository, ReportRepository>();
        services.AddSingleton<ISettingsService, SettingsRepository>();

        // Services
        services.AddKeyedSingleton<HttpClient>("Analytics", (_, _) =>
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            var endpoint = Environment.GetEnvironmentVariable("STUDY_DOCUMENT_ANALYTICS_URL");
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var baseAddress))
                client.BaseAddress = baseAddress;
            return client;
        });
        services.AddSingleton<IPlatformInfo, PlatformInfo>();
        services.AddSingleton<IInstallationIdentityService, InstallationIdentityService>();
        services.AddSingleton<IAnalyticsService>(sp => new AnalyticsService(
            sp.GetRequiredKeyedService<HttpClient>("Analytics"),
            sp.GetRequiredService<IInstallationIdentityService>(),
            sp.GetRequiredService<IPlatformInfo>()));
        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());
        services.AddSingleton<IFileDialogService>(sp => sp.GetRequiredService<DialogService>());
        services.AddSingleton<ICustomDialogService>(sp => sp.GetRequiredService<DialogService>());
        services.AddSingleton<DroppedFileImportService>();
        services.AddSingleton<IDroppedFileImportService>(sp => sp.GetRequiredService<DroppedFileImportService>());
        services.AddSingleton<IApplicationLifecycleService, ApplicationLifecycleService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IProcessLauncherService, ProcessLauncherService>();
        services.AddSingleton<IExportService, CsvExportService>();
        services.AddSingleton<IBackupService, DatabaseBackupService>();
        services.AddSingleton<LocalizationService>();
        services.AddSingleton<ILocalizationService>(sp => sp.GetRequiredService<LocalizationService>());
        services.AddSingleton<IUpdateService, Services.UpdateService>();
        services.AddSingleton<IToastService, Services.ToastService>();

        // モデル — メイン
        services.AddSingleton<MainWindowModel>();
        services.AddTransient<DashboardModel>();

        // モデル — 文書
        services.AddTransient<AddEditModel>();
        services.AddTransient<BatchImportModel>();
        services.AddTransient<BulkDeleteModel>();
        services.AddTransient<DuplicateDetectionModel>();
        services.AddTransient<PersonalNoteModel>();
        services.AddTransient<RelatedDocumentsModel>();

        // モデル — 管理
        services.AddTransient<CategoryManagementModel>();
        services.AddTransient<CollectionManagementModel>();
        services.AddTransient<RecycleBinModel>();
        services.AddTransient<FileIntegrityCheckModel>();

        // モデル — レポート
        services.AddTransient<ReportModel>();
        services.AddTransient<TreeMapModel>();

        // モデル — ユーティリティ
        services.AddTransient<RecentFilesModel>();
    }
}
