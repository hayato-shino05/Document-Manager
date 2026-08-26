using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class DuplicateDetectionModel : ModelBase
{
    private readonly IDocumentRepository _repository;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _loc;

    [ObservableProperty] private ObservableCollection<DuplicateGroup> _duplicateGroups = new();
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private int _totalGroups;

    public DuplicateDetectionModel(IDocumentRepository repository, IDialogService dialogService, ILocalizationService loc)
    {
        _repository = repository;
        _dialogService = dialogService;
        _loc = loc;
    }

    public bool HasResults => DuplicateGroups.Count > 0;

    [RelayCommand]
    private async Task ScanDuplicatesAsync()
    {
        IsScanning = true;
        DuplicateGroups.Clear();
        TotalGroups = 0;
        OnPropertyChanged(nameof(HasResults));

        try
        {
            var docs = _repository.GetAll();
            var groups = docs
                .GroupBy(d => d.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in groups)
            {
                DuplicateGroups.Add(new DuplicateGroup
                {
                    GroupName = group.First().Name,
                    Documents = new ObservableCollection<StudyDocument>(group.ToList()),
                    Count = group.Count(),
                    MatchInfo = string.Format(_loc["Duplicate_MatchInfo"], group.Count())
                });
            }

            TotalGroups = DuplicateGroups.Count;
            OnPropertyChanged(nameof(HasResults));

            if (TotalGroups == 0)
            {
                await _dialogService.ShowMessageAsync(_loc["Dialog_Result"], _loc["Duplicate_NoDuplicates"]);
            }
        }
        catch (Exception)
        {
            DuplicateGroups.Clear();
            TotalGroups = 0;
            OnPropertyChanged(nameof(HasResults));
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task MergeDuplicateAsync(DuplicateGroup? group)
    {
        if (group is null || group.Documents.Count < 2)
            return;

        var survivor = group.Documents[0];
        var duplicateIds = group.Documents.Skip(1).Select(document => document.Id).ToArray();
        var confirmed = await _dialogService.ShowConfirmAsync(
            _loc["Dialog_Confirm"],
            string.Format(_loc["Duplicate_ConfirmMerge"], survivor.Name, duplicateIds.Length),
            _loc["Duplicate_Merge"],
            isDanger: true);
        if (!confirmed)
            return;

        try
        {
            if (!_repository.MergeDocuments(survivor.Id, duplicateIds))
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                return;
            }

            await _dialogService.ShowMessageAsync(
                _loc["Dialog_Result"],
                string.Format(_loc["Duplicate_MergeSuccess"], survivor.Name, duplicateIds.Length));
            await ScanDuplicatesAsync();
        }
        catch (Exception)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }

    [RelayCommand]
    private async Task DeleteDuplicateAsync(StudyDocument? doc)
    {
        if (doc == null) return;

        try
        {
            var confirmed = await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"],
                string.Format(_loc["Duplicate_ConfirmDelete"], doc.Name, doc.Id),
                _loc["Action_Delete"], isDanger: true);
            if (!confirmed) return;

            if (!_repository.Delete(doc.Id))
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                return;
            }

            await ScanDuplicatesAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
        }
    }
}

public class DuplicateGroup
{
    public string GroupName { get; set; } = string.Empty;
    public ObservableCollection<StudyDocument> Documents { get; set; } = new();
    public int Count { get; set; }
    public string GroupTitle => GroupName;
    public string MatchInfo { get; set; } = string.Empty;
}
