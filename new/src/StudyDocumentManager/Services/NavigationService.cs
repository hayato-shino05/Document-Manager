using StudyDocumentManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace StudyDocumentManager.Services;

/// <summary>
/// Navigation service implementation using ViewModel switching.
/// </summary>
public class NavigationService(IServiceProvider serviceProvider) : INavigationService
{
    private MainWindowViewModel? _mainViewModel;

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
            "dashboard" => serviceProvider.GetRequiredService<DashboardViewModel>() as ViewModelBase,
            "addedit" or "add" => CreateAddEditViewModel(parameter as int?),
            "edit" => CreateAddEditViewModel(parameter as int?),
            "categories" => serviceProvider.GetRequiredService<CategoryManagementViewModel>(),
            "collections" => serviceProvider.GetRequiredService<CollectionManagementViewModel>(),
            "recyclebin" or "recycle" => serviceProvider.GetRequiredService<RecycleBinViewModel>(),
            "batchimport" or "batch-import" => serviceProvider.GetRequiredService<BatchImportViewModel>(),
            "bulkdelete" or "bulk-delete" => serviceProvider.GetRequiredService<BulkDeleteViewModel>(),
            "duplicates" => serviceProvider.GetRequiredService<DuplicateDetectionViewModel>(),
            "fileintegrity" or "integrity" => serviceProvider.GetRequiredService<FileIntegrityCheckViewModel>(),
            "report" => serviceProvider.GetRequiredService<ReportViewModel>(),
            "recentfiles" => serviceProvider.GetRequiredService<RecentFilesViewModel>(),
            "treemap" => serviceProvider.GetRequiredService<TreeMapViewModel>(),
            "personal-note" => CreatePersonalNoteViewModel(parameter),
            "related-docs" => CreateRelatedDocsViewModel(parameter),
            _ => serviceProvider.GetRequiredService<DashboardViewModel>(),
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
        _mainViewModel.CurrentView = serviceProvider.GetRequiredService<DashboardViewModel>();
    }

    private AddEditViewModel CreateAddEditViewModel(int? documentId)
    {
        var vm = serviceProvider.GetRequiredService<AddEditViewModel>();
        if (documentId.HasValue)
        {
            vm.LoadDocument(documentId.Value);
        }
        return vm;
    }

    private PersonalNoteViewModel CreatePersonalNoteViewModel(object? parameter)
    {
        var vm = serviceProvider.GetRequiredService<PersonalNoteViewModel>();
        if (parameter is (int docId, string docName))
        {
            vm.Load(docId, docName);
        }
        return vm;
    }

    private RelatedDocumentsViewModel CreateRelatedDocsViewModel(object? parameter)
    {
        var vm = serviceProvider.GetRequiredService<RelatedDocumentsViewModel>();
        if (parameter is (int docId, string docName))
        {
            vm.Load(docId, docName);
        }
        return vm;
    }
}
