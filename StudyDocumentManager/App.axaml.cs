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
        Avalonia.Data.Core.Plugins.BindingPlugins.DataValidators.RemoveAt(0);
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        DatabaseHelper.InitializeDatabase();

        // AXAML側で{StaticResource Loc}として使えるよう登録
        Resources["Loc"] = Services.GetRequiredService<ILocalizationService>();

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

    private static void ConfigureServices(IServiceCollection services)
    {
        // Repositories
        services.AddSingleton<IDocument, DocumentRepository>();
        services.AddSingleton<ICategory, CategoryRepository>();
        services.AddSingleton<ICollection, CollectionRepository>();
        services.AddSingleton<IPersonalNote, PersonalNoteRepository>();
        services.AddSingleton<IRelatedDocument, RelatedDocumentRepository>();
        services.AddSingleton<IRecentFile, RecentFileRepository>();
        services.AddSingleton<IReport, ReportRepository>();

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
        services.AddSingleton<LocalizationService>();
        services.AddSingleton<ILocalizationService>(sp => sp.GetRequiredService<LocalizationService>());

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
