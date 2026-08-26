using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
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
            "recovery" or "recoverycenter" or "recovery-center" => serviceProvider.GetRequiredService<RecoveryCenterModel>(),
            "batchimport" or "batch-import" => serviceProvider.GetRequiredService<BatchImportModel>(),
            "bulkdelete" or "bulk-delete" => serviceProvider.GetRequiredService<BulkDeleteModel>(),
            "duplicates" => serviceProvider.GetRequiredService<DuplicateDetectionModel>(),
            "fileintegrity" or "integrity" => serviceProvider.GetRequiredService<FileIntegrityCheckModel>(),
            "report" => serviceProvider.GetRequiredService<ReportModel>(),
            "recentfiles" => serviceProvider.GetRequiredService<RecentFilesModel>(),
            "treemap" => serviceProvider.GetRequiredService<TreeMapModel>(),
            "personal-note" => CreatePersonalNoteModel(parameter),
            "related-docs" => CreateRelatedDocsModel(parameter),
            "smartviews" or "smart-views" or "savedsearches" => serviceProvider.GetRequiredService<SmartViewsModel>(),
            "student" or "assignments" or "student-workspace" => serviceProvider.GetRequiredService<StudentWorkspaceModel>(),
            "run-smartview" => CreateDashboardWithSavedSearch(parameter),
            _ => serviceProvider.GetRequiredService<DashboardModel>(),
        };

        if (viewModel != null)
        {
            SetCurrentView(viewModel);
        }
    }

    // Always go back to Dashboard — matches WinForms behavior where closing sub-form = return to main
    public void GoBack()
    {
        if (_mainModel == null) return;
        SetCurrentView(serviceProvider.GetRequiredService<DashboardModel>());
    }

    private void SetCurrentView(ModelBase viewModel)
    {
        if (_mainModel is null || ReferenceEquals(_mainModel.CurrentView, viewModel))
            return;

        if (_mainModel.CurrentView is IDisposable disposable)
            disposable.Dispose();

        _mainModel.CurrentView = viewModel;
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

    private DashboardModel CreateDashboardWithSavedSearch(object? parameter)
    {
        var vm = serviceProvider.GetRequiredService<DashboardModel>();
        if (parameter is int savedSearchId)
        {
            var savedSearch = serviceProvider.GetRequiredService<ISavedSearchRepository>().GetById(savedSearchId);
            var criteria = savedSearch == null ? null : SavedSearchCriteria.FromJson(savedSearch.CriteriaJson);
            if (criteria != null)
            {
                vm.ApplySavedSearch(criteria);
            }
        }
        return vm;
    }
}
