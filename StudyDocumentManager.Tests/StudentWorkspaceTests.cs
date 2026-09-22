using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using StudyDocumentManager.Views;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class StudentWorkspaceTests
{
    [Theory]
    [InlineData(null, AssignmentDeadlineState.NoDate)]
    [InlineData("2026-08-25", AssignmentDeadlineState.Overdue)]
    [InlineData("2026-08-30", AssignmentDeadlineState.DueSoon)]
    [InlineData("2026-09-10", AssignmentDeadlineState.Scheduled)]
    public void ClassifyDeadline_HandlesNoDateAndDeadlineWindows(string? value, AssignmentDeadlineState expected)
    {
        DateTime? deadline = value == null ? null : DateTime.Parse(value);
        Assert.Equal(expected, StudentWorkspaceModel.ClassifyDeadline(deadline, new DateTime(2026, 8, 26)));
    }

    [AvaloniaFact]
    public void StudentWorkspace_BindsAssignmentEditorAndCourseActions()
    {
        var repository = new AssignmentRepositoryStub
        {
            Courses = [new Course { Id = 1, Name = "Algorithms" }],
            Semesters = [new Semester { Id = 1, Name = "Current", IsActive = true }],
            Assignments = [new Assignment { Id = 1, Title = "Task", SemesterId = 1 }]
        };
        using var model = new StudentWorkspaceModel(repository, new DocumentRepositoryStub(), new ProcessLauncherStub(), new DialogStub(), new NavigationStub(), new LocalizationService());
        var view = new StudentWorkspace { DataContext = model };
        var window = new Window { Content = view };
        try
        {
            window.Show();
            Assert.Same(model.AddCourseCommand, view.FindControl<Button>("StudentWorkspace_AddCourse")?.Command);
            Assert.NotNull(view.FindControl<Button>("StudentWorkspace_DeleteSemester"));
            Assert.NotNull(view.FindControl<Button>("StudentWorkspace_AddCourse"));
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Refresh_ShowsActiveSemesterByDefault_AndHistoryShowsAll()
    {
        var repository = new AssignmentRepositoryStub
        {
            Semesters =
            [
                new Semester { Id = 1, Name = "Current", IsActive = true },
                new Semester { Id = 2, Name = "Past", IsActive = false }
            ],
            Assignments =
            [
                new Assignment { Id = 1, Title = "Current task", SemesterId = 1 },
                new Assignment { Id = 2, Title = "Past task", SemesterId = 2 }
            ]
        };
        using var model = new StudentWorkspaceModel(repository, new DocumentRepositoryStub(), new ProcessLauncherStub(), new DialogStub(), new NavigationStub(), new LocalizationService());

        Assert.Single(model.Assignments);
        Assert.Equal("Current task", model.Assignments[0].Title);

        model.SelectedSemester = repository.Semesters[1];
        Assert.Single(model.Assignments);
        Assert.Equal("Past task", model.Assignments[0].Title);

        model.ShowHistory = true;

        Assert.Equal(2, model.Assignments.Count);

        model.SelectedAssignment = model.Assignments[0];
        model.EditAssignmentCommand.Execute(null);
        Assert.Equal("Current task", model.EditorTitle);
    }

    [AvaloniaFact]
    public void StudentWorkspace_BindsLocalizedAssignmentLabels()
    {
        var repository = new AssignmentRepositoryStub
        {
            Semesters = [new Semester { Id = 1, Name = "Current", IsActive = true }],
            Assignments = [new Assignment { Id = 1, Title = "Task", SemesterId = 1, Status = AssignmentStatuses.Completed, Priority = AssignmentPriorities.Low }]
        };
        var localization = new LocalizationService();
        using var model = new StudentWorkspaceModel(repository, new DocumentRepositoryStub(), new ProcessLauncherStub(), new DialogStub(), new NavigationStub(), localization);
        var view = new StudentWorkspace { DataContext = model };
        var window = new Window { Content = view };
        try
        {
            window.Show();
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == localization["SW_Status_Completed"]);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == localization["SW_Priority_Low"]);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void AssignmentLabels_LocalizeInternalStatusPriorityAndDeadlineValues()
    {
        var repository = new AssignmentRepositoryStub
        {
            Semesters = [new Semester { Id = 1, Name = "Current", IsActive = true }],
            Assignments = [new Assignment { Id = 1, Title = "Task", SemesterId = 1, Status = AssignmentStatuses.InProgress, Priority = AssignmentPriorities.High, PersonalDeadline = new DateTime(2026, 8, 30) }]
        };
        var localization = new LocalizationService();
        using var model = new StudentWorkspaceModel(repository, new DocumentRepositoryStub(), new ProcessLauncherStub(), new DialogStub(), new NavigationStub(), localization, new DateTime(2026, 8, 26));

        Assert.Equal(localization["SW_Status_InProgress"], model.Assignments[0].StatusLabel);
        Assert.Equal(localization["SW_Priority_High"], model.Assignments[0].PriorityLabel);
        Assert.Equal(localization["SW_Deadline_DueSoon"], model.Assignments[0].DeadlineStateLabel);

        localization.SetLanguage(Core.SupportedLanguage.English);

        Assert.Equal(localization["SW_Status_InProgress"], model.Assignments[0].StatusLabel);
        Assert.Equal(localization["SW_Priority_High"], model.Assignments[0].PriorityLabel);
    }

    [Fact]
    public void EditAndSaveAssignmentCommands_PersistUpdatedAssignmentAndSelection()
    {
        var repository = new AssignmentRepositoryStub
        {
            Semesters = [new Semester { Id = 1, Name = "Current", IsActive = true }],
            Assignments = [new Assignment { Id = 9, Title = "Before", SemesterId = 1 }]
        };
        using var model = new StudentWorkspaceModel(repository, new DocumentRepositoryStub(), new ProcessLauncherStub(), new DialogStub(), new NavigationStub(), new LocalizationService());
        model.SelectedAssignment = model.Assignments[0];

        model.EditAssignmentCommand.Execute(null);
        model.EditorTitle = "After";
        model.SaveAssignmentCommand.Execute(null);

        Assert.Equal("After", repository.GetAssignment(9)!.Title);
        Assert.Equal("After", model.SelectedAssignment!.Title);
        Assert.False(model.IsEditing);
    }

    [Fact]
    public async Task DeleteAssignment_ClearsSelectionAndEditorState()
    {
        var repository = new AssignmentRepositoryStub
        {
            Semesters = [new Semester { Id = 1, Name = "Current", IsActive = true }],
            Assignments = [new Assignment { Id = 9, Title = "Delete me", SemesterId = 1 }]
        };
        using var model = new StudentWorkspaceModel(repository, new DocumentRepositoryStub(), new ProcessLauncherStub(), new DialogStub(), new NavigationStub(), new LocalizationService());
        model.SelectedAssignment = model.Assignments[0];
        model.EditAssignmentCommand.Execute(null);

        model.DeleteAssignmentCommand.Execute(null);
        await model.DeleteAssignmentCommand.ExecutionTask!;

        Assert.Null(model.SelectedAssignment);
        Assert.False(model.HasSelection);
        Assert.False(model.IsEditing);
        Assert.Empty(model.Assignments);
        Assert.Empty(model.EditorTitle);
    }

    [Fact]
    public void SaveAssignment_AddsWithReturnedId_AndSelectsNewAssignment()
    {
        var repository = new AssignmentRepositoryStub
        {
            Semesters = [new Semester { Id = 1, Name = "Current", IsActive = true }]
        };
        using var model = new StudentWorkspaceModel(repository, new DocumentRepositoryStub(), new ProcessLauncherStub(), new DialogStub(), new NavigationStub(), new LocalizationService());

        model.NewAssignmentCommand.Execute(null);
        model.EditorTitle = "New task";
        model.SaveAssignmentCommand.Execute(null);

        Assert.NotNull(model.SelectedAssignment);
        Assert.Equal(42, model.SelectedAssignment!.Assignment.Id);
        Assert.Equal("New task", model.SelectedAssignment.Title);
    }

    [Fact]
    public async Task ContextAndCourseSemesterCommands_PersistAndDeleteThroughRepository()
    {
        var repository = new AssignmentRepositoryStub();
        var dialog = new DialogStub { Inputs = new Queue<string?>(["Algorithms", "Spring 2026"]) };
        using var model = new StudentWorkspaceModel(repository, new DocumentRepositoryStub(), new ProcessLauncherStub(), dialog, new NavigationStub(), new LocalizationService());

        model.StudentContext.AcademicYear = "2026";
        model.SaveContextCommand.Execute(null);
        Assert.Equal("2026", repository.Context!.AcademicYear);

        model.AddCourseCommand.Execute(null);
        await model.AddCourseCommand.ExecutionTask!;
        model.AddSemesterCommand.Execute(null);
        await model.AddSemesterCommand.ExecutionTask!;
        Assert.Contains(repository.Courses, course => course.Name == "Algorithms");
        Assert.Contains(repository.Semesters, semester => semester.Name == "Spring 2026");

        model.SelectedCourse = repository.Courses.Single();
        model.DeleteCourseCommand.Execute(null);
        await model.DeleteCourseCommand.ExecutionTask!;
        model.SelectedSemester = repository.Semesters.Single();
        model.DeleteSemesterCommand.Execute(null);
        await model.DeleteSemesterCommand.ExecutionTask!;

        Assert.Empty(repository.Courses);
        Assert.Empty(repository.Semesters);
    }

    private sealed class AssignmentRepositoryStub : IAssignmentRepository
    {
        public StudentContext? Context { get; set; }
        public List<Course> Courses { get; set; } = [];
        public List<Semester> Semesters { get; set; } = [];
        public List<Assignment> Assignments { get; set; } = [];
        public StudentContext? GetStudentContext() => Context;
        public bool SaveStudentContext(StudentContext context) { Context = context; return true; }
        public List<Course> GetCourses() => Courses;
        public int AddCourse(Course course)
        {
            course.Id = Courses.Count + 1;
            Courses.Add(course);
            return course.Id;
        }
        public bool UpdateCourse(Course course) => true;
        public bool DeleteCourse(int id) => Courses.RemoveAll(course => course.Id == id) > 0;
        public List<Semester> GetSemesters() => Semesters;
        public int AddSemester(Semester semester)
        {
            semester.Id = Semesters.Count + 1;
            Semesters.Add(semester);
            return semester.Id;
        }
        public bool UpdateSemester(Semester semester) => true;
        public bool DeleteSemester(int id) => Semesters.RemoveAll(semester => semester.Id == id) > 0;
        public List<Assignment> GetAssignments() => Assignments;
        public Assignment? GetAssignment(int id) => Assignments.FirstOrDefault(a => a.Id == id);
        public int AddAssignment(Assignment assignment)
        {
            assignment.Id = 42;
            Assignments.Add(assignment);
            return assignment.Id;
        }
        public bool UpdateAssignment(Assignment assignment)
        {
            var index = Assignments.FindIndex(a => a.Id == assignment.Id);
            if (index < 0) return false;
            Assignments[index] = assignment;
            return true;
        }
        public bool DeleteAssignment(int id)
        {
            var removed = Assignments.RemoveAll(a => a.Id == id);
            return removed > 0;
        }
        public bool LinkDocument(int assignmentId, int documentId) => true;
        public bool UnlinkDocument(int assignmentId, int documentId) => true;
        public bool ReplaceDocumentLinks(int assignmentId, IReadOnlyList<int> documentIds) => true;
        public List<int> GetDocumentIds(int assignmentId) => [];
    }

    private sealed class DocumentRepositoryStub : IDocumentRepository
    {
        public List<StudyDocument> GetAll() => [];
        public StudyDocument? GetById(int id) => null;
        public List<StudyDocument> Search(string keyword) => [];
        public List<StudyDocument> Filter(string subject, string type) => [];
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => [];
        public bool Add(StudyDocument document) => true;
        public bool AddWithCatalogs(StudyDocument document) => true;
        public bool Update(StudyDocument document) => true;
        public bool Delete(int id) => true;
        public List<string> GetDistinctSubjects() => [];
        public List<string> GetDistinctTypes() => [];
        public List<string> GetDistinctTags() => [];
        public List<StudyDocument> GetUpcomingDeadlines(int days) => [];
        public List<StudyDocument> GetOverdueDocuments() => [];
        public void EnsureSubjectExists(string subject) { }
        public void EnsureTypeExists(string type) { }
    }

    private sealed class ProcessLauncherStub : IProcessLauncherService
    {
        public void OpenFile(string filePath) { }
        public void RevealInExplorer(string filePath) { }
        public void OpenUrl(string url) { }
    }

    private sealed class DialogStub : IDialogService
    {
        public Queue<string?> Inputs { get; init; } = new();
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false) => Task.FromResult(true);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "")
            => Task.FromResult(Inputs.Count > 0 ? Inputs.Dequeue() : null);
    }

    private sealed class NavigationStub : INavigationService
    {
        public bool CanGoBack => true;
        public void NavigateTo(string viewKey) { }
        public void NavigateTo(string viewKey, object? parameter) { }
        public void GoBack() { }
    }
}
