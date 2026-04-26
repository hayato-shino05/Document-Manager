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
        // CRITICAL: Remove Avalonia's built-in INPC binding plugin to prevent
        // duplicate PropertyChanged subscriptions with CommunityToolkit.Mvvm.
        // Without this, DataGrid triggers StackOverflowException from infinite
        // binding loops (InpcPropertyAccessorPlugin double-subscribes).
        Avalonia.Data.Core.Plugins.BindingPlugins.DataValidators.RemoveAt(0);
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        DatabaseHelper.InitializeDatabase();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Repositories
        services.AddSingleton<IDocumentRepository, DocumentRepository>();

        // Services
        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<DroppedFileImportService>();

        // Models — Main
        services.AddSingleton<MainWindowModel>();
        services.AddTransient<DashboardModel>();

        // Models — Documents
        services.AddTransient<AddEditModel>();
        services.AddTransient<BatchImportModel>();
        services.AddTransient<BulkDeleteModel>();
        services.AddTransient<DuplicateDetectionModel>();
        services.AddTransient<PersonalNoteModel>();
        services.AddTransient<RelatedDocumentsModel>();

        // Models — Management
        services.AddTransient<CategoryManagementModel>();
        services.AddTransient<CollectionManagementModel>();
        services.AddTransient<RecycleBinModel>();
        services.AddTransient<FileIntegrityCheckModel>();

        // Models — Reports
        services.AddTransient<ReportModel>();
        services.AddTransient<TreeMapModel>();

        // Models — Utilities
        services.AddTransient<RecentFilesModel>();
    }
}
