using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using StudyDocumentManager.ViewModels;
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
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
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

        // ViewModels — Main
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();

        // ViewModels — Documents
        services.AddTransient<AddEditViewModel>();
        services.AddTransient<BatchImportViewModel>();
        services.AddTransient<BulkDeleteViewModel>();
        services.AddTransient<DuplicateDetectionViewModel>();
        services.AddTransient<PersonalNoteViewModel>();
        services.AddTransient<RelatedDocumentsViewModel>();

        // ViewModels — Management
        services.AddTransient<CategoryManagementViewModel>();
        services.AddTransient<CollectionManagementViewModel>();
        services.AddTransient<RecycleBinViewModel>();
        services.AddTransient<FileIntegrityCheckViewModel>();

        // ViewModels — Reports
        services.AddTransient<ReportViewModel>();
        services.AddTransient<TreeMapViewModel>();

        // ViewModels — Utilities
        services.AddTransient<RecentFilesViewModel>();
    }
}
