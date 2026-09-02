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
    private readonly IProcessLauncherService? _processLauncher;
    private readonly IUndoRepository? _undoRepository;
    private readonly IUndoService? _undo;
    private readonly IDuplicateReviewService? _duplicateReviewService;

    [ObservableProperty] private ObservableCollection<DuplicateGroup> _duplicateGroups = new();
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _hasScanned;
    [ObservableProperty] private int _totalGroups;

    public DuplicateDetectionModel(
        IDocumentRepository repository,
        IDialogService dialogService,
        ILocalizationService loc,
        IProcessLauncherService? processLauncher = null,
        IUndoRepository? undoRepository = null,
        IUndoService? undo = null,
        IDuplicateReviewService? duplicateReviewService = null)
    {
        _repository = repository;
        _dialogService = dialogService;
        _loc = loc;
        _processLauncher = processLauncher;
        _undoRepository = undoRepository;
        _undo = undo;
        _duplicateReviewService = duplicateReviewService;
        _loc.LanguageChanged += (_, _) => OnPropertyChanged(nameof(CleanSummaryText));
    }

    public bool HasResults => DuplicateGroups.Count > 0;
    public bool IsInitialState => !HasScanned && !IsScanning;
    public bool IsCleanState => HasScanned && !IsScanning && TotalGroups == 0;
    public string CleanSummaryText => _loc["Duplicate_NoDuplicates"];

    [RelayCommand]
    private async Task ScanDuplicatesAsync()
    {
        IsScanning = true;
        HasScanned = true;
        DuplicateGroups.Clear();
        TotalGroups = 0;
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(IsInitialState));
        OnPropertyChanged(nameof(IsCleanState));
        OnPropertyChanged(nameof(CleanSummaryText));

        try
        {
            var docs = _repository.GetAll();
            if (_duplicateReviewService != null)
            {
                var reviewGroups = _duplicateReviewService.DetectDuplicates(docs);
                foreach (var group in reviewGroups)
                {
                    var displayName = !string.IsNullOrWhiteSpace(group.Candidates[0].Name)
                        ? group.Candidates[0].Name
                        : System.IO.Path.GetFileName(group.Candidates[0].FilePath);

                    DuplicateGroups.Add(new DuplicateGroup
                    {
                        GroupName = displayName,
                        Documents = new ObservableCollection<StudyDocument>(group.Candidates),
                        Count = group.Candidates.Count,
                        MatchInfo = group.MatchDescription
                    });
                }
            }
            else
            {
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
            OnPropertyChanged(nameof(IsInitialState));
            OnPropertyChanged(nameof(IsCleanState));
        }
    }

    [RelayCommand]
    private async Task MergeDuplicateAsync(DuplicateGroup? group)
    {
        if (group is null || group.Documents.Count < 2)
            return;

        int? selectedSurvivorId = null;
        if (_dialogService is ICustomDialogService customDialog)
        {
            selectedSurvivorId = await customDialog.ShowDuplicateMergeReviewAsync(
                group.GroupName,
                group.MatchInfo,
                group.Documents.ToList());
        }
        else
        {
            var defaultSurvivor = group.Documents[0];
            var confirmed = await _dialogService.ShowConfirmAsync(
                _loc["Dialog_Confirm"],
                string.Format(_loc["Duplicate_ConfirmMerge"], defaultSurvivor.Name, group.Documents.Count - 1),
                _loc["Duplicate_Merge"],
                isDanger: true);
            if (confirmed)
                selectedSurvivorId = defaultSurvivor.Id;
        }

        if (!selectedSurvivorId.HasValue)
            return;

        var survivor = group.Documents.FirstOrDefault(d => d.Id == selectedSurvivorId.Value);
        if (survivor is null)
            return;

        var duplicateIds = group.Documents
            .Where(d => d.Id != survivor.Id)
            .Select(d => d.Id)
            .ToArray();

        if (duplicateIds.Length == 0)
            return;

        try
        {
            var mergeUndo = _undoRepository?.CaptureMergeUndo(survivor.Id, duplicateIds);
            if (!_repository.MergeDocuments(survivor.Id, duplicateIds))
            {
                await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Msg_Error"]);
                return;
            }
            if (mergeUndo is not null && _undo is not null)
            {
                _undo.Push(new UndoEntry
                {
                    DescriptionKey = "Duplicate_MergeSuccess",
                    DescriptionArgs = [survivor.Name, duplicateIds.Length],
                    Merge = mergeUndo,
                    CreatedAt = DateTime.Now
                });
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

    [RelayCommand]
    private void ViewDocument(StudyDocument? doc)
    {
        if (doc is null || _processLauncher is null || string.IsNullOrEmpty(doc.FilePath))
            return;
        _processLauncher.OpenFile(doc.FilePath);
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
