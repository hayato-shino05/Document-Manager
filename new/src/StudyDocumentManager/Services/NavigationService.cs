using StudyDocumentManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace StudyDocumentManager.Services;

/// <summary>
/// Navigation service implementation using ViewModel switching.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private MainWindowViewModel? _mainViewModel;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public bool CanGoBack => _mainViewModel?.CurrentView is not DashboardViewModel;

    public void SetMainViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public void NavigateTo(string viewKey)
    {
        NavigateTo(viewKey, null);
    }

    public void NavigateTo(string viewKey, object? parameter)
    {
        if (_mainViewModel == null) return;

        var viewModel = viewKey switch
        {
            "dashboard" => _serviceProvider.GetRequiredService<DashboardViewModel>() as ViewModelBase,
            "addedit" or "add" => CreateAddEditViewModel(parameter as int?),
            "edit" => CreateAddEditViewModel(parameter as int?),
            "categories" => _serviceProvider.GetRequiredService<CategoryManagementViewModel>(),
            "collections" => _serviceProvider.GetRequiredService<CollectionManagementViewModel>(),
            "recyclebin" or "recycle" => _serviceProvider.GetRequiredService<RecycleBinViewModel>(),
            "batchimport" or "batch-import" => _serviceProvider.GetRequiredService<BatchImportViewModel>(),
            "bulkdelete" or "bulk-delete" => _serviceProvider.GetRequiredService<BulkDeleteViewModel>(),
            "duplicates" => _serviceProvider.GetRequiredService<DuplicateDetectionViewModel>(),
            "fileintegrity" or "integrity" => _serviceProvider.GetRequiredService<FileIntegrityCheckViewModel>(),
            "report" => _serviceProvider.GetRequiredService<ReportViewModel>(),
            "recentfiles" => _serviceProvider.GetRequiredService<RecentFilesViewModel>(),
            "treemap" => _serviceProvider.GetRequiredService<TreeMapViewModel>(),
            "personal-note" => CreatePersonalNoteViewModel(parameter),
            "related-docs" => CreateRelatedDocsViewModel(parameter),
            _ => _serviceProvider.GetRequiredService<DashboardViewModel>(),
        };

        if (viewModel != null)
        {
            _mainViewModel.CurrentView = viewModel;
        }
    }

    // Always go back to Dashboard — matches WinForms behavior where closing sub-form = return to main
    public void GoBack()
    {
        if (_mainViewModel == null) return;
        _mainViewModel.CurrentView = _serviceProvider.GetRequiredService<DashboardViewModel>();
    }

    private ViewModelBase CreateAddEditViewModel(int? documentId)
    {
        var vm = _serviceProvider.GetRequiredService<AddEditViewModel>();
        if (documentId.HasValue)
        {
            vm.LoadDocument(documentId.Value);
        }
        return vm;
    }

    private ViewModelBase CreatePersonalNoteViewModel(object? parameter)
    {
        var vm = _serviceProvider.GetRequiredService<PersonalNoteViewModel>();
        if (parameter is (int docId, string docName))
        {
            vm.Load(docId, docName);
        }
        return vm;
    }

    private ViewModelBase CreateRelatedDocsViewModel(object? parameter)
    {
        var vm = _serviceProvider.GetRequiredService<RelatedDocumentsViewModel>();
        if (parameter is (int docId, string docName))
        {
            vm.Load(docId, docName);
        }
        return vm;
    }
}
