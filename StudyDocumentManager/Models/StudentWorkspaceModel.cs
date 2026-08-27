using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models;

public enum AssignmentDeadlineState
{
    NoDate,
    Overdue,
    DueSoon,
    Scheduled
}

public sealed class AssignmentOption
{
    public AssignmentOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }
    public string Label { get; }
}

public sealed class AssignmentRow
{
    public Assignment Assignment { get; init; } = new();
    public string Title => Assignment.Title;
    public string Status => Assignment.Status;
    public string Priority => Assignment.Priority;
    public string StatusLabel { get; init; } = string.Empty;
    public string PriorityLabel { get; init; } = string.Empty;
    public string DeadlineStateLabel { get; init; } = string.Empty;
    public DateTime? Deadline => Assignment.PersonalDeadline ?? Assignment.OfficialDeadline;
    public AssignmentDeadlineState DeadlineState { get; init; }
    public bool HasDeadline => Deadline.HasValue;
}

public partial class StudentWorkspaceModel : ModelBase, IDisposable
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IProcessLauncherService _processLauncher;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;
    private readonly ILocalizationService _loc;
    private readonly DateTime _today;
    private bool _disposed;
    private int? _editingAssignmentId;

    [ObservableProperty] private StudentContext _studentContext = new();
    [ObservableProperty] private ObservableCollection<Course> _courses = [];
    [ObservableProperty] private ObservableCollection<Semester> _semesters = [];
    [ObservableProperty] private ObservableCollection<AssignmentRow> _assignments = [];
    [ObservableProperty] private Course? _selectedCourse;
    [ObservableProperty] private Semester? _selectedSemester;
    [ObservableProperty] private AssignmentRow? _selectedAssignment;
    [ObservableProperty] private bool _showHistory;
    [ObservableProperty] private string _editorTitle = string.Empty;
    [ObservableProperty] private Course? _editorCourse;
    [ObservableProperty] private Semester? _editorSemester;
    [ObservableProperty] private DateTimeOffset? _editorOfficialDeadline;
    [ObservableProperty] private DateTimeOffset? _editorPersonalDeadline;
    [ObservableProperty] private string _editorStatus = AssignmentStatuses.Planned;
    [ObservableProperty] private string _editorPriority = AssignmentPriorities.Normal;
    [ObservableProperty] private AssignmentOption? _selectedStatusOption;
    [ObservableProperty] private AssignmentOption? _selectedPriorityOption;
    [ObservableProperty] private string _editorMilestone = string.Empty;
    [ObservableProperty] private string _editorNotes = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;

    public IReadOnlyList<AssignmentOption> StatusOptions { get; private set; } = [];
    public IReadOnlyList<AssignmentOption> PriorityOptions { get; private set; } = [];

    public StudentWorkspaceModel(
        IAssignmentRepository assignmentRepository,
        IDocumentRepository documentRepository,
        IProcessLauncherService processLauncher,
        IDialogService dialogService,
        INavigationService navigationService,
        ILocalizationService loc)
        : this(assignmentRepository, documentRepository, processLauncher, dialogService, navigationService, loc, DateTime.Today)
    {
    }

    internal StudentWorkspaceModel(
        IAssignmentRepository assignmentRepository,
        IDocumentRepository documentRepository,
        IProcessLauncherService processLauncher,
        IDialogService dialogService,
        INavigationService navigationService,
        ILocalizationService loc,
        DateTime today)
    {
        _assignmentRepository = assignmentRepository;
        _documentRepository = documentRepository;
        _processLauncher = processLauncher;
        _dialogService = dialogService;
        _navigationService = navigationService;
        _loc = loc;
        _today = today.Date;
        RefreshLocalizedOptions();
        _loc.LanguageChanged += OnLanguageChanged;
        Refresh();
    }

    public bool HasSelection => SelectedAssignment != null;
    public bool HasRelatedDocuments => SelectedAssignment != null && _assignmentRepository.GetDocumentIds(SelectedAssignment.Assignment.Id).Count > 0;
    public bool IsEditing => _editingAssignmentId.HasValue;

    [RelayCommand]
    private void Refresh()
    {
        StudentContext = _assignmentRepository.GetStudentContext() ?? new StudentContext();
        Courses = new ObservableCollection<Course>(_assignmentRepository.GetCourses());
        Semesters = new ObservableCollection<Semester>(_assignmentRepository.GetSemesters());
        SelectedCourse = Courses.FirstOrDefault();
        SelectedSemester = Semesters.FirstOrDefault(s => s.IsActive) ?? Semesters.FirstOrDefault();
        RebuildAssignments();
        StatusText = string.Format(_loc["SW_StatusReady"], Assignments.Count);
    }

    [RelayCommand]
    private void SaveContext()
    {
        StatusText = _assignmentRepository.SaveStudentContext(StudentContext)
            ? _loc["SW_ContextSaved"]
            : _loc["Msg_Error"];
    }

    [RelayCommand]
    private async Task AddCourseAsync()
    {
        var name = await _dialogService.ShowInputAsync(_loc["SW_AddCourse"], _loc["SW_CourseName"]);
        if (string.IsNullOrWhiteSpace(name)) return;
        var id = _assignmentRepository.AddCourse(new Course { Name = name.Trim() });
        if (id > 0) Refresh();
    }

    [RelayCommand]
    private async Task DeleteCourseAsync()
    {
        if (SelectedCourse == null) return;
        if (!await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"], string.Format(_loc["SW_DeleteCourseConfirm"], SelectedCourse.Name), _loc["Action_Delete"], true)) return;
        if (_assignmentRepository.DeleteCourse(SelectedCourse.Id)) Refresh();
    }

    [RelayCommand]
    private async Task AddSemesterAsync()
    {
        var name = await _dialogService.ShowInputAsync(_loc["SW_AddSemester"], _loc["SW_SemesterName"]);
        if (string.IsNullOrWhiteSpace(name)) return;
        var id = _assignmentRepository.AddSemester(new Semester { Name = name.Trim(), IsActive = Semesters.Count == 0 });
        if (id > 0) Refresh();
    }

    [RelayCommand]
    private async Task DeleteSemesterAsync()
    {
        if (SelectedSemester == null) return;
        if (!await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"], string.Format(_loc["SW_DeleteSemesterConfirm"], SelectedSemester.Name), _loc["Action_Delete"], true)) return;
        if (_assignmentRepository.DeleteSemester(SelectedSemester.Id)) Refresh();
    }

    [RelayCommand]
    private void NewAssignment()
    {
        _editingAssignmentId = null;
        EditorTitle = string.Empty;
        EditorCourse = SelectedCourse;
        EditorSemester = SelectedSemester;
        EditorOfficialDeadline = null;
        EditorPersonalDeadline = null;
        EditorStatus = AssignmentStatuses.Planned;
        EditorPriority = AssignmentPriorities.Normal;
        SelectedStatusOption = StatusOptions[0];
        SelectedPriorityOption = PriorityOptions[1];
        EditorMilestone = string.Empty;
        EditorNotes = string.Empty;
        OnPropertyChanged(nameof(IsEditing));
    }

    [RelayCommand]
    private void EditAssignment()
    {
        if (SelectedAssignment == null) return;
        var assignment = _assignmentRepository.GetAssignment(SelectedAssignment.Assignment.Id);
        if (assignment == null) return;
        _editingAssignmentId = assignment.Id;
        EditorTitle = assignment.Title;
        EditorCourse = Courses.FirstOrDefault(c => c.Id == assignment.CourseId);
        EditorSemester = Semesters.FirstOrDefault(s => s.Id == assignment.SemesterId);
        EditorOfficialDeadline = ToOffset(assignment.OfficialDeadline);
        EditorPersonalDeadline = ToOffset(assignment.PersonalDeadline);
        EditorStatus = assignment.Status;
        EditorPriority = assignment.Priority;
        SelectedStatusOption = StatusOptions.FirstOrDefault(option => option.Key == assignment.Status);
        SelectedPriorityOption = PriorityOptions.FirstOrDefault(option => option.Key == assignment.Priority);
        EditorMilestone = assignment.Milestone;
        EditorNotes = assignment.Notes;
        OnPropertyChanged(nameof(IsEditing));
    }

    [RelayCommand]
    private void SaveAssignment()
    {
        if (string.IsNullOrWhiteSpace(EditorTitle))
        {
            StatusText = _loc["SW_TitleRequired"];
            return;
        }

        var assignment = new Assignment
        {
            Id = _editingAssignmentId ?? 0,
            Title = EditorTitle.Trim(),
            CourseId = EditorCourse?.Id,
            SemesterId = EditorSemester?.Id,
            OfficialDeadline = EditorOfficialDeadline?.DateTime.Date,
            PersonalDeadline = EditorPersonalDeadline?.DateTime.Date,
            Status = EditorStatus,
            Priority = EditorPriority,
            Milestone = EditorMilestone.Trim(),
            Notes = EditorNotes.Trim()
        };

        var saved = true;
        if (_editingAssignmentId.HasValue)
        {
            saved = _assignmentRepository.UpdateAssignment(assignment);
        }
        else
        {
            var addedId = _assignmentRepository.AddAssignment(assignment);
            saved = addedId > 0;
            if (saved)
                assignment.Id = addedId;
        }

        if (!saved)
        {
            StatusText = _loc["Msg_Error"];
            return;
        }

        _editingAssignmentId = null;
        OnPropertyChanged(nameof(IsEditing));
        Refresh();
        SelectedAssignment = Assignments.FirstOrDefault(row => row.Assignment.Id == assignment.Id);
    }

    [RelayCommand]
    private async Task DeleteAssignmentAsync()
    {
        if (SelectedAssignment == null) return;
        if (!await _dialogService.ShowConfirmAsync(_loc["Dialog_Confirm"], string.Format(_loc["SW_DeleteAssignmentConfirm"], SelectedAssignment.Title), _loc["Action_Delete"], true)) return;
        if (_assignmentRepository.DeleteAssignment(SelectedAssignment.Assignment.Id))
        {
            SelectedAssignment = null;
            _editingAssignmentId = null;
            EditorTitle = string.Empty;
            EditorCourse = null;
            EditorSemester = null;
            EditorOfficialDeadline = null;
            EditorPersonalDeadline = null;
            EditorStatus = AssignmentStatuses.Planned;
            EditorPriority = AssignmentPriorities.Normal;
            SelectedStatusOption = StatusOptions[0];
            SelectedPriorityOption = PriorityOptions[1];
            EditorMilestone = string.Empty;
            EditorNotes = string.Empty;
            OnPropertyChanged(nameof(IsEditing));
            Refresh();
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        _editingAssignmentId = null;
        OnPropertyChanged(nameof(IsEditing));
    }

    [RelayCommand]
    private void OpenRelatedDocument()
    {
        if (SelectedAssignment == null) return;
        var documentId = _assignmentRepository.GetDocumentIds(SelectedAssignment.Assignment.Id).FirstOrDefault();
        if (documentId <= 0)
        {
            StatusText = _loc["SW_NoRelatedDocument"];
            return;
        }
        var document = _documentRepository.GetById(documentId);
        if (document == null || string.IsNullOrWhiteSpace(document.FilePath) || !File.Exists(document.FilePath))
        {
            StatusText = _loc["SW_RelatedDocumentMissing"];
            return;
        }
        _processLauncher.OpenFile(document.FilePath);
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();

    partial void OnSelectedAssignmentChanged(AssignmentRow? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasRelatedDocuments));
    }

    partial void OnSelectedStatusOptionChanged(AssignmentOption? value)
    {
        if (value != null) EditorStatus = value.Key;
    }

    partial void OnSelectedPriorityOptionChanged(AssignmentOption? value)
    {
        if (value != null) EditorPriority = value.Key;
    }

    partial void OnShowHistoryChanged(bool value) => RebuildAssignments();
    partial void OnSelectedSemesterChanged(Semester? value) => RebuildAssignments();

    private void RebuildAssignments()
    {
        var semesterId = SelectedSemester?.Id ?? Semesters.FirstOrDefault(s => s.IsActive)?.Id;
        var source = _assignmentRepository.GetAssignments()
            .Where(a => ShowHistory || semesterId == null || a.SemesterId == semesterId)
            .OrderBy(a => a.PersonalDeadline ?? a.OfficialDeadline ?? DateTime.MaxValue)
            .Select(a =>
            {
                var deadlineState = ClassifyDeadline(a.PersonalDeadline ?? a.OfficialDeadline, _today);
                return new AssignmentRow
                {
                    Assignment = a,
                    DeadlineState = deadlineState,
                    DeadlineStateLabel = GetDeadlineStateLabel(deadlineState),
                    StatusLabel = GetStatusLabel(a.Status),
                    PriorityLabel = GetPriorityLabel(a.Priority)
                };
            });
        Assignments = new ObservableCollection<AssignmentRow>(source);
        OnPropertyChanged(nameof(HasRelatedDocuments));
    }

    public static AssignmentDeadlineState ClassifyDeadline(DateTime? deadline, DateTime today)
    {
        if (!deadline.HasValue) return AssignmentDeadlineState.NoDate;
        var remaining = (deadline.Value.Date - today.Date).Days;
        return remaining < 0 ? AssignmentDeadlineState.Overdue : remaining <= 7 ? AssignmentDeadlineState.DueSoon : AssignmentDeadlineState.Scheduled;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RefreshLocalizedOptions();
        RebuildAssignments();
        StatusText = string.Format(_loc["SW_StatusReady"], Assignments.Count);
    }

    private void RefreshLocalizedOptions()
    {
        StatusOptions =
        [
            new(AssignmentStatuses.Planned, _loc["SW_Status_Planned"]),
            new(AssignmentStatuses.InProgress, _loc["SW_Status_InProgress"]),
            new(AssignmentStatuses.Completed, _loc["SW_Status_Completed"])
        ];
        PriorityOptions =
        [
            new(AssignmentPriorities.Low, _loc["SW_Priority_Low"]),
            new(AssignmentPriorities.Normal, _loc["SW_Priority_Normal"]),
            new(AssignmentPriorities.High, _loc["SW_Priority_High"])
        ];
        OnPropertyChanged(nameof(StatusOptions));
        OnPropertyChanged(nameof(PriorityOptions));
    }

    private string GetStatusLabel(string value) => value switch
    {
        AssignmentStatuses.InProgress => _loc["SW_Status_InProgress"],
        AssignmentStatuses.Completed => _loc["SW_Status_Completed"],
        _ => _loc["SW_Status_Planned"]
    };

    private string GetPriorityLabel(string value) => value switch
    {
        AssignmentPriorities.Low => _loc["SW_Priority_Low"],
        AssignmentPriorities.High => _loc["SW_Priority_High"],
        _ => _loc["SW_Priority_Normal"]
    };

    private string GetDeadlineStateLabel(AssignmentDeadlineState value) => value switch
    {
        AssignmentDeadlineState.NoDate => _loc["SW_Deadline_NoDate"],
        AssignmentDeadlineState.Overdue => _loc["SW_Deadline_Overdue"],
        AssignmentDeadlineState.DueSoon => _loc["SW_Deadline_DueSoon"],
        _ => _loc["SW_Deadline_Scheduled"]
    };

    private static DateTimeOffset? ToOffset(DateTime? value) => value.HasValue ? new DateTimeOffset(value.Value) : null;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _loc.LanguageChanged -= OnLanguageChanged;
    }
}

public static class AssignmentStatuses
{
    public const string Planned = "planned";
    public const string InProgress = "in-progress";
    public const string Completed = "completed";
}

public static class AssignmentPriorities
{
    public const string Low = "low";
    public const string Normal = "normal";
    public const string High = "high";
}
