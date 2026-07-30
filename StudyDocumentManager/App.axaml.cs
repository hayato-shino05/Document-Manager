using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
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

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        RemoveDataAnnotationsValidationPlugin();
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        var db = Services.GetRequiredService<DatabaseHelper>();
        db.InitializeDatabase();

        // Avalonia needs the concrete type (implements INotifyPropertyChanged) for indexer binding refresh
        Resources["Loc"] = Services.GetRequiredService<LocalizationService>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainModel = Services.GetRequiredService<MainWindowModel>();

            var navService = Services.GetRequiredService<NavigationService>();
            navService.SetMainModel(mainModel);

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainModel
            };
        }

        base.OnFrameworkInitializationCompleted();
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

        // Models â€” Main
        services.AddSingleton<MainWindowModel>();
        services.AddTransient<DashboardModel>();

        // Models â€” Documents
        services.AddTransient<AddEditModel>();
        services.AddTransient<BatchImportModel>();
        services.AddTransient<BulkDeleteModel>();
        services.AddTransient<DuplicateDetectionModel>();
        services.AddTransient<PersonalNoteModel>();
        services.AddTransient<RelatedDocumentsModel>();

        // Models â€” Management
        services.AddTransient<CategoryManagementModel>();
        services.AddTransient<CollectionManagementModel>();
        services.AddTransient<RecycleBinModel>();
        services.AddTransient<FileIntegrityCheckModel>();

        // Models â€” Reports
        services.AddTransient<ReportModel>();
        services.AddTransient<TreeMapModel>();

        // Models â€” Utilities
        services.AddTransient<RecentFilesModel>();
    }
}
