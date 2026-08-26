using System.Collections.ObjectModel;
using Microsoft.Data.Sqlite;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public partial class ImportInboxModel : ModelBase, IDisposable
{
    private readonly IImportInboxRepository _repository;
    private readonly IProcessLauncherService _processLauncher;
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _loc;
    private readonly IDroppedFileImportService? _importService;
    private readonly IBulkOperationRepository? _bulkRepository;

    [ObservableProperty] private ObservableCollection<ImportInboxItem> _items = [];
    [ObservableProperty] private ImportInboxState? _stateFilter;
    [ObservableProperty] private StateFilterOption? _selectedStateOption;
    [ObservableProperty] private bool _includeProcessed;
    [ObservableProperty] private ImportInboxItem? _selectedItem;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _bulkSubject = string.Empty;
    [ObservableProperty] private string _bulkType = string.Empty;
    public ObservableCollection<ImportInboxItem> SelectedItems { get; } = [];

    public ImportInboxModel(IImportInboxRepository repository, IProcessLauncherService processLauncher, INavigationService navigationService, ILocalizationService loc, IDroppedFileImportService? importService = null, IBulkOperationRepository? bulkRepository = null)
    {
        _repository = repository;
        _processLauncher = processLauncher;
        _navigationService = navigationService;
        _loc = loc;
        _importService = importService;
        _bulkRepository = bulkRepository;
        _loc.LanguageChanged += OnLanguageChanged;
        UpdateStateLabels();
        Refresh();
    }

    public IReadOnlyList<StateFilterOption> StateOptions { get; } = [new(null, "All"), new(ImportInboxState.Pending, "Pending"), new(ImportInboxState.Held, "Held"), new(ImportInboxState.Failed, "Failed"), new(ImportInboxState.MissingMetadata, "MissingMetadata"), new(ImportInboxState.Ambiguous, "Ambiguous"), new(ImportInboxState.Processed, "Processed")];
    public sealed class StateFilterOption(ImportInboxState? state, string label)
    {
        public ImportInboxState? State { get; } = state;
        public string Label { get; private set; } = label;
        public void SetLabel(string label) => Label = label;
        public override string ToString() => Label;
    }
    partial void OnSelectedStateOptionChanged(StateFilterOption? value) { StateFilter = value?.State; }
    public bool HasItems => Items.Count > 0;
    public bool IsEmpty => Items.Count == 0;
    public bool CanBulkEdit => SelectedItems.Any(item => item.DocumentId.HasValue) && (!string.IsNullOrWhiteSpace(BulkSubject) || !string.IsNullOrWhiteSpace(BulkType));
    partial void OnBulkTypeChanged(string value) => NotifyBulkEditState();
    partial void OnBulkSubjectChanged(string value) => NotifyBulkEditState();
    public void AddSelected(ImportInboxItem item) { if (!SelectedItems.Contains(item)) SelectedItems.Add(item); NotifyBulkEditState(); }
    public void RemoveSelected(ImportInboxItem item) { SelectedItems.Remove(item); NotifyBulkEditState(); }
    private void NotifyBulkEditState() { OnPropertyChanged(nameof(CanBulkEdit)); ApplyBulkMetadataCommand.NotifyCanExecuteChanged(); }
    partial void OnIncludeProcessedChanged(bool value) => Refresh();
    partial void OnStateFilterChanged(ImportInboxState? value) => Refresh();
    public void Dispose() => _loc.LanguageChanged -= OnLanguageChanged;
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        UpdateStateLabels();
        Refresh();
    }

    private void UpdateStateLabels()
    {
        foreach (var option in StateOptions)
            option.SetLabel(option.State is null ? _loc["ImportInbox_All"] : _loc[$"ImportInbox_State_{option.State}"]);
        foreach (var item in Items)
        {
            item.StateLabel = _loc[$"ImportInbox_State_{item.State}"];
            item.FailureLabel = FailureCodeToLabel(item.FailureCode);
        }
        OnPropertyChanged(nameof(StateOptions));
    }

    private string FailureCodeToLabel(string? failureCode) => failureCode switch
    {
        "FileError" => _loc["BatchImport_FileError"],
        "PermissionError" => _loc["BatchImport_PermissionError"],
        "DatabaseError" => _loc["BatchImport_DatabaseError"],
        "ImportFailed" => _loc["BatchImport_ItemFailed"],
        _ => string.Empty
    };

    [RelayCommand]
    private void Refresh()
    {
        var source = _repository.GetAll(IncludeProcessed);
        foreach (var item in source)
        {
            item.StateLabel = _loc[$"ImportInbox_State_{item.State}"];
            item.FailureLabel = FailureCodeToLabel(item.FailureCode);
        }
        Items = new ObservableCollection<ImportInboxItem>(StateFilter is null ? source : source.Where(i => i.State == StateFilter));
        SelectedItems.Clear();
        SelectedItem = null;
        NotifyBulkEditState();
        StatusText = FormatStatus();
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void ApplyBulkMetadata()
    {
        var ids = SelectedItems.Where(item => item.DocumentId.HasValue).Select(item => item.DocumentId!.Value).Distinct().ToList();
        if (_bulkRepository is null || ids.Count == 0 || (string.IsNullOrWhiteSpace(BulkSubject) && string.IsNullOrWhiteSpace(BulkType))) return;
        var outcome = _bulkRepository.BulkEditMetadata(ids, new BulkEditChanges { Subject = string.IsNullOrWhiteSpace(BulkSubject) ? null : BulkSubject.Trim(), Type = string.IsNullOrWhiteSpace(BulkType) ? null : BulkType.Trim() });
        foreach (var item in SelectedItems.Where(item => item.DocumentId.HasValue))
        {
            var documentId = item.DocumentId!.Value;
            if (!outcome.FailedIds.Contains(documentId))
            {
                if (!string.IsNullOrWhiteSpace(BulkSubject)) item.Subject = BulkSubject.Trim();
                if (!string.IsNullOrWhiteSpace(BulkType)) item.Type = BulkType.Trim();
                _repository.Update(item);
                _repository.UpdateState(item.Id, ImportInboxState.Processed);
            }
        }
        Refresh();
    }

    [RelayCommand]
    private void RetrySelected()
    {
        if (SelectedItem is null) return;
        if (!File.Exists(SelectedItem.SourcePath))
        {
            StatusText = _loc["ImportInbox_SourceMissing"];
            return;
        }
        if (_importService is null) return;
        DocumentImportOutcome outcome;
        string? failureCode = null;
        var document = new StudyDocument();
        try
        {
            document = _importService.BuildDocumentFromPath(SelectedItem.SourcePath);
            outcome = _importService.SaveDocument(document);
        }
        catch (IOException)
        {
            outcome = DocumentImportOutcome.Failed;
            failureCode = "FileError";
        }
        catch (UnauthorizedAccessException)
        {
            outcome = DocumentImportOutcome.Failed;
            failureCode = "PermissionError";
        }
        catch (SqliteException)
        {
            outcome = DocumentImportOutcome.Failed;
            failureCode = "DatabaseError";
        }
        catch (Exception)
        {
            outcome = DocumentImportOutcome.Failed;
            failureCode = "ImportFailed";
        }
        var state = outcome switch
        {
            DocumentImportOutcome.Imported => ImportInboxState.Processed,
            DocumentImportOutcome.SkippedDuplicate => ImportInboxState.Held,
            _ => ImportInboxState.Failed
        };
        if (outcome == DocumentImportOutcome.Imported)
        {
            SelectedItem.DocumentId = document.Id;
            SelectedItem.Subject = document.Subject;
            SelectedItem.Type = document.Type;
            _repository.Update(SelectedItem);
        }
        _repository.UpdateState(SelectedItem.Id, state, failureCode);
        Refresh();
    }

    [RelayCommand]
    private void RevealSelected()
    {
        if (SelectedItem is null || !File.Exists(SelectedItem.SourcePath))
        {
            StatusText = _loc["ImportInbox_SourceMissing"];
            return;
        }
        _processLauncher.RevealInExplorer(SelectedItem.SourcePath);
    }

    [RelayCommand]
    private void GoBack() => _navigationService.NavigateTo("dashboard");

    private string FormatStatus() => string.Format(_loc["ImportInbox_Status"], Items.Count);
}
