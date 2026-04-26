using StudyDocumentManager.Models;
using Microsoft.Extensions.DependencyInjection;

namespace StudyDocumentManager.Services;

/// <summary>
/// Navigation service implementation using ViewModel switching.
/// </summary>
public class NavigationService(IServiceProvider serviceProvider) : INavigationService
{
    private MainWindowModel? _mainModel;

    public bool CanGoBack => _mainModel?.CurrentView is not DashboardModel;

    public void SetMainModel(MainWindowModel mainModel)
    {
        _mainModel = mainModel;
    }

    public void NavigateTo(string viewKey)
    {
        NavigateTo(viewKey, null);
    }

    public void NavigateTo(string viewKey, object? parameter)
    {
        if (_mainModel == null) return;

        var viewModel = viewKey switch
        {
            "dashboard" => serviceProvider.GetRequiredService<DashboardModel>() as ModelBase,
            "addedit" or "add" => CreateAddEditModel(parameter as int?),
            "edit" => CreateAddEditModel(parameter as int?),
            "categories" => serviceProvider.GetRequiredService<CategoryManagementModel>(),
            "collections" => serviceProvider.GetRequiredService<CollectionManagementModel>(),
            "recyclebin" or "recycle" => serviceProvider.GetRequiredService<RecycleBinModel>(),
            "batchimport" or "batch-import" => serviceProvider.GetRequiredService<BatchImportModel>(),
            "bulkdelete" or "bulk-delete" => serviceProvider.GetRequiredService<BulkDeleteModel>(),
            "duplicates" => serviceProvider.GetRequiredService<DuplicateDetectionModel>(),
            "fileintegrity" or "integrity" => serviceProvider.GetRequiredService<FileIntegrityCheckModel>(),
            "report" => serviceProvider.GetRequiredService<ReportModel>(),
            "recentfiles" => serviceProvider.GetRequiredService<RecentFilesModel>(),
            "treemap" => serviceProvider.GetRequiredService<TreeMapModel>(),
            "personal-note" => CreatePersonalNoteModel(parameter),
            "related-docs" => CreateRelatedDocsModel(parameter),
            _ => serviceProvider.GetRequiredService<DashboardModel>(),
        };

        if (viewModel != null)
        {
            _mainModel.CurrentView = viewModel;
        }
    }

    // Always go back to Dashboard — matches WinForms behavior where closing sub-form = return to main
    public void GoBack()
    {
        if (_mainModel == null) return;
        _mainModel.CurrentView = serviceProvider.GetRequiredService<DashboardModel>();
    }

    private AddEditModel CreateAddEditModel(int? documentId)
    {
        var vm = serviceProvider.GetRequiredService<AddEditModel>();
        if (documentId.HasValue)
        {
            vm.LoadDocument(documentId.Value);
        }
        return vm;
    }

    private PersonalNoteModel CreatePersonalNoteModel(object? parameter)
    {
        var vm = serviceProvider.GetRequiredService<PersonalNoteModel>();
        if (parameter is (int docId, string docName))
        {
            vm.Load(docId, docName);
        }
        return vm;
    }

    private RelatedDocumentsModel CreateRelatedDocsModel(object? parameter)
    {
        var vm = serviceProvider.GetRequiredService<RelatedDocumentsModel>();
        if (parameter is (int docId, string docName))
        {
            vm.Load(docId, docName);
        }
        return vm;
    }
}
