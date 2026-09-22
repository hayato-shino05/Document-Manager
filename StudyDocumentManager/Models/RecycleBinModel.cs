using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class RecycleBinModel : ModelBase
{
    private readonly IRecycleBinRepository _docRepo;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _loc;

    [ObservableProperty] private ObservableCollection<SelectableDocument> _deletedItems = new();
    [ObservableProperty] private ObservableCollection<StudyDocument> _deletedDocuments = new();
    [ObservableProperty] private SelectableDocument? _selectedItem;
    [ObservableProperty] private StudyDocument? _selectedDocument;

    public bool HasSelection => SelectedDocument != null || HasCheckedItems;
    public bool HasCheckedItems => DeletedItems.Any(d => d.IsSelected);
    public int SelectedCount => DeletedItems.Count(d => d.IsSelected);
    public bool HasDeletedDocuments => DeletedDocuments.Count > 0;
    public string SelectedCountText => string.Format(_loc["BulkDelete_SelectedCount"] ?? "{0} items selected", SelectedCount);

    public RecycleBinModel(IRecycleBinRepository docRepo, IDialogService dialogService, ILocalizationService loc)
    {
        _docRepo = docRepo;
        _dialogService = dialogService;
        _loc = loc;
        LoadData();
    }

    private void LoadData()
    {
        SelectedDocument = null;
        SelectedItem = null;

        var docs = _docRepo.GetDeletedDocuments();
        DeletedDocuments = new ObservableCollection<StudyDocument>(docs);

        var items = docs.Select(d =>
        {
            var item = new SelectableDocument { Document = d, IsSelected = false };
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SelectableDocument.IsSelected))
                {
                    OnPropertyChanged(nameof(HasCheckedItems));
                    OnPropertyChanged(nameof(SelectedCount));
                    OnPropertyChanged(nameof(SelectedCountText));
                    OnPropertyChanged(nameof(HasSelection));
                }
            };
            return item;
        }).ToList();

        DeletedItems = new ObservableCollection<SelectableDocument>(items);
        OnPropertyChanged(nameof(HasDeletedDocuments));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasCheckedItems));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
    }

    partial void OnSelectedDocumentChanged(StudyDocument? value)
    {
        if (value == null)
        {
            if (SelectedItem != null) SelectedItem = null;
        }
        else
        {
            var match = DeletedItems.FirstOrDefault(i => ReferenceEquals(i.Document, value) || i.Document.Id == value.Id);
            if (SelectedItem != match) SelectedItem = match;
        }
        OnPropertyChanged(nameof(HasSelection));
    }

    partial void OnSelectedItemChanged(SelectableDocument? value)
    {
        if (!ReferenceEquals(_selectedDocument, value?.Document))
        {
            _selectedDocument = value?.Document;
            OnPropertyChanged(nameof(SelectedDocument));
        }
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    public void SelectAll()
    {
        foreach (var item in DeletedItems)
            item.IsSelected = true;
    }

    [RelayCommand]
    public void DeselectAll()
    {
        foreach (var item in DeletedItems)
            item.IsSelected = false;
    }

    private List<StudyDocument> GetTargetDocuments()
    {
        var checkedDocs = DeletedItems.Where(i => i.IsSelected).Select(i => i.Document).ToList();
        if (checkedDocs.Count > 0) return checkedDocs;
        if (SelectedDocument != null) return [SelectedDocument];
        return [];
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        var targets = GetTargetDocuments();
        if (targets.Count == 0) return;

        var singleTarget = targets.Count == 1 ? targets[0] : null;
        var selectedDocumentId = singleTarget?.Id ?? 0;

        try
        {
            var prompt = singleTarget != null
                ? string.Format(_loc["Recycle_ConfirmRestore"], singleTarget.Name)
                : string.Format(_loc["Recycle_ConfirmRestore"], $"{targets.Count} items");

            var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"], prompt);
            if (!confirmed) return;

            if (singleTarget != null)
            {
                if (!_docRepo.RestoreDocument(singleTarget.Id))
                {
                    await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Recycle_RestoreError"]);
                    return;
                }

                LoadData();
                await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], _loc["Recycle_RestoreSuccess"]);
            }
            else
            {
                int successCount = 0;
                foreach (var doc in targets)
                {
                    if (_docRepo.RestoreDocument(doc.Id))
                        successCount++;
                }

                LoadData();

                if (successCount == targets.Count)
                {
                    await _dialogService.ShowMessageAsync(_loc["Dialog_Success"], _loc["Recycle_RestoreSuccess"]);
                }
                else
                {
                    await _dialogService.ShowErrorAsync(_loc["Dialog_Error"],
                        string.Format(_loc["Operation_Partial"] ?? "{0}/{1} restored", successCount, targets.Count));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            LoadData();
            if (singleTarget != null)
            {
                SelectedDocument = DeletedDocuments.FirstOrDefault(document => document.Id == selectedDocumentId);
            }
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private async Task PermanentDeleteAsync()
    {
        var targets = GetTargetDocuments();
        if (targets.Count == 0) return;

        var singleTarget = targets.Count == 1 ? targets[0] : null;
        var selectedDocumentId = singleTarget?.Id ?? 0;

        try
        {
            var prompt = singleTarget != null
                ? string.Format(_loc["Recycle_ConfirmPermanentDelete"], singleTarget.Name)
                : string.Format(_loc["Recycle_ConfirmEmptyTrash"], targets.Count);

            var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"], prompt,
                _loc["Btn_PermanentDelete"], isDanger: true);
            if (!confirmed) return;

            if (singleTarget != null)
            {
                if (!_docRepo.PermanentDeleteDocument(singleTarget.Id))
                {
                    await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Recycle_PermanentDeleteError"]);
                    return;
                }

                LoadData();
                await _dialogService.ShowMessageAsync(_loc["Dialog_Complete"], _loc["Recycle_PermanentDeleteSuccess"]);
            }
            else
            {
                int successCount = 0;
                foreach (var doc in targets)
                {
                    if (_docRepo.PermanentDeleteDocument(doc.Id))
                        successCount++;
                }

                LoadData();

                if (successCount == targets.Count)
                {
                    await _dialogService.ShowMessageAsync(_loc["Dialog_Complete"], _loc["Recycle_PermanentDeleteSuccess"]);
                }
                else
                {
                    await _dialogService.ShowErrorAsync(_loc["Dialog_Error"],
                        string.Format(_loc["Operation_Partial"] ?? "{0}/{1} deleted", successCount, targets.Count));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            LoadData();
            if (singleTarget != null)
            {
                SelectedDocument = DeletedDocuments.FirstOrDefault(document => document.Id == selectedDocumentId);
            }
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private async Task EmptyTrashAsync()
    {
        if (DeletedDocuments.Count == 0) return;

        try
        {
            var deletedDocumentCount = DeletedDocuments.Count;
            var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
                string.Format(_loc["Recycle_ConfirmEmptyTrash"], deletedDocumentCount),
                _loc["Btn_DeleteAll"], isDanger: true);
            if (!confirmed) return;

            var count = _docRepo.EmptyRecycleBin();
            if (count != deletedDocumentCount)
            {
                LoadData();
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"],
                    string.Format(_loc["Operation_Partial"], count, deletedDocumentCount));
                return;
            }

            LoadData();
            await _dialogService.ShowMessageAsync(_loc["Dialog_Complete"],
                string.Format(_loc["Recycle_EmptyTrashDone"], count));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            LoadData();
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private void Refresh() => LoadData();
}
