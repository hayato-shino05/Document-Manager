using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Repositories;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class AssignmentRepositoryTests : DatabaseTestBase
{
    private readonly AssignmentRepository _assignments;

    public AssignmentRepositoryTests()
    {
        _assignments = new AssignmentRepository(Db);
    }

    [Fact]
    public void InitializeDatabase_MigratesLegacyDocumentsAndAddsStudentSchema()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sdm_assignment_legacy_{Guid.NewGuid():N}.db");
        try
        {
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE documents (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, subject TEXT, type TEXT, file_path TEXT, notes TEXT, created_at DATETIME, file_size REAL, author TEXT, is_important INTEGER, tags TEXT, deadline DATETIME, is_deleted INTEGER DEFAULT 0, deleted_at DATETIME); INSERT INTO documents(name) VALUES('Legacy document');";
                command.ExecuteNonQuery();
            }

            var database = new StudyDocumentManager.Data.Helpers.DatabaseHelper();
            database.SetDatabasePath(path);
            database.InitializeDatabase();
            database.InitializeDatabase();
            using var migrated = new Microsoft.Data.Sqlite.SqliteConnection(database.ConnectionString);
            migrated.Open();
            Assert.Equal(1L, Convert.ToInt64(Scalar(migrated, "SELECT COUNT(*) FROM documents WHERE name='Legacy document'")));
            Assert.Equal(1L, Convert.ToInt64(Scalar(migrated, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='assignment_documents'")));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void InitializeDatabase_AddsStudentSchemaIdempotentlyWithoutDataLoss()
    {
        var document = new StudyDocument { Name = "Existing document" };
        Assert.True(Repo.Add(document));
        Db.InitializeDatabase();
        Db.InitializeDatabase();

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(Db.ConnectionString);
        connection.Open();
        foreach (var table in new[] { "student_context", "courses", "semesters", "assignments", "assignment_documents" })
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name";
            command.Parameters.AddWithValue("@name", table);
            Assert.Equal(1L, Convert.ToInt64(command.ExecuteScalar()));
        }

        Assert.Equal(1, Repo.GetDocumentCount());
    }

    [Fact]
    public void InitializeDatabase_AcceptsCurrentFeatureTablesOnRepeatedStartup()
    {
        Db.InitializeDatabase();
        Db.InitializeDatabase();

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(Db.ConnectionString);
        connection.Open();
        foreach (var table in new[] { "import_inbox", "watched_folders", "saved_searches", "student_context", "courses", "semesters", "assignments", "assignment_documents" })
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name";
            command.Parameters.AddWithValue("@name", table);
            Assert.Equal(1L, Convert.ToInt64(command.ExecuteScalar()));
        }
    }

    [Fact]
    public void SaveStudentContext_UpsertsSingleContext()
    {
        var context = new StudentContext
        {
            AcademicYear = "2026",
            Semester = "Spring",
            Course = "Algorithms",
            Module = "Graphs",
            Owner = "Student"
        };

        Assert.True(_assignments.SaveStudentContext(context));
        context.Module = "Sorting";
        Assert.True(_assignments.SaveStudentContext(context));

        var loaded = _assignments.GetStudentContext();
        Assert.NotNull(loaded);
        Assert.Equal("Sorting", loaded!.Module);
    }

    [Fact]
    public void AddAndLoadAssignment_PreservesContextDeadlinesAndDocumentLinks()
    {
        var courseId = _assignments.AddCourse(new Course { Name = "Algorithms", Code = "CS201" });
        var semesterId = _assignments.AddSemester(new Semester
        {
            Name = "2026 Spring",
            StartsOn = new DateTime(2026, 1, 1),
            EndsOn = new DateTime(2026, 6, 30),
            IsActive = true
        });
        var document = new StudyDocument { Name = "Assignment brief" };
        Assert.True(Repo.Add(document));

        var assignmentId = _assignments.AddAssignment(new Assignment
        {
            Title = "Sorting report",
            CourseId = courseId,
            SemesterId = semesterId,
            OfficialDeadline = new DateTime(2026, 3, 10),
            PersonalDeadline = new DateTime(2026, 3, 5),
            Status = "in-progress",
            Priority = "high",
            Milestone = "Draft",
            Notes = "Submit PDF"
        });

        Assert.True(_assignments.LinkDocument(assignmentId, document.Id));
        var loaded = _assignments.GetAssignment(assignmentId);

        Assert.NotNull(loaded);
        Assert.Equal("Sorting report", loaded!.Title);
        Assert.Equal(courseId, loaded.CourseId);
        Assert.Equal(semesterId, loaded.SemesterId);
        Assert.Equal(new DateTime(2026, 3, 5), loaded.PersonalDeadline);
        Assert.Equal([document.Id], _assignments.GetDocumentIds(assignmentId));
    }

    [Fact]
    public void UpdateAssignment_PersistsEditableFields()
    {
        var courseId = _assignments.AddCourse(new Course { Name = "Algorithms" });
        var semesterId = _assignments.AddSemester(new Semester { Name = "2026 Spring" });
        var assignmentId = _assignments.AddAssignment(new Assignment
        {
            Title = "Initial title",
            CourseId = courseId,
            SemesterId = semesterId,
            OfficialDeadline = new DateTime(2026, 9, 1),
            Status = "planned",
            Priority = "normal",
            Milestone = "Draft",
            Notes = "Initial notes"
        });

        var assignment = _assignments.GetAssignment(assignmentId);
        Assert.NotNull(assignment);
        assignment!.Title = "Updated title";
        assignment.Status = "completed";
        assignment.Priority = "high";
        assignment.Milestone = "Final";
        assignment.Notes = "Updated notes";
        assignment.CourseId = courseId;
        assignment.SemesterId = semesterId;
        assignment.OfficialDeadline = new DateTime(2026, 10, 1);
        assignment.PersonalDeadline = new DateTime(2026, 9, 12);

        Assert.True(_assignments.UpdateAssignment(assignment));

        var updated = _assignments.GetAssignment(assignmentId);
        Assert.NotNull(updated);
        Assert.Equal("Updated title", updated!.Title);
        Assert.Equal("completed", updated.Status);
        Assert.Equal("high", updated.Priority);
        Assert.Equal("Final", updated.Milestone);
        Assert.Equal("Updated notes", updated.Notes);
        Assert.Equal(courseId, updated.CourseId);
        Assert.Equal(semesterId, updated.SemesterId);
        Assert.Equal(new DateTime(2026, 10, 1), updated.OfficialDeadline);
        Assert.Equal(new DateTime(2026, 9, 12), updated.PersonalDeadline);
    }

    [Fact]
    public void DeleteAssignment_RemovesAssignmentAndRejectsFurtherUpdate()
    {
        var assignmentId = _assignments.AddAssignment(new Assignment { Title = "To delete" });
        var assignment = _assignments.GetAssignment(assignmentId);
        Assert.NotNull(assignment);

        Assert.True(_assignments.DeleteAssignment(assignmentId));
        Assert.Null(_assignments.GetAssignment(assignmentId));
        Assert.False(_assignments.UpdateAssignment(assignment!));
        Assert.False(_assignments.DeleteAssignment(assignmentId));
    }

    [Fact]
    public void CourseAndSemester_UpdateAndDelete_AreExposedByContract()
    {
        var courseId = _assignments.AddCourse(new Course { Name = "Networks", Code = "N1" });
        var semesterId = _assignments.AddSemester(new Semester { Name = "2026 Autumn" });
        var course = _assignments.GetCourses().Single(c => c.Id == courseId);
        course.Name = "Advanced Networks";
        Assert.True(_assignments.UpdateCourse(course));
        var semester = _assignments.GetSemesters().Single(s => s.Id == semesterId);
        semester.IsActive = true;
        Assert.True(_assignments.UpdateSemester(semester));
        Assert.Equal("Advanced Networks", _assignments.GetCourses().Single(c => c.Id == courseId).Name);
        Assert.True(_assignments.DeleteCourse(courseId));
        Assert.True(_assignments.DeleteSemester(semesterId));
    }

    [Fact]
    public void DeleteCourseAndSemester_SetAssignmentReferencesNull()
    {
        var courseId = _assignments.AddCourse(new Course { Name = "Networks" });
        var semesterId = _assignments.AddSemester(new Semester { Name = "2026 Autumn" });
        var assignmentId = _assignments.AddAssignment(new Assignment
        {
            Title = "Network lab",
            CourseId = courseId,
            SemesterId = semesterId
        });

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(Db.ConnectionString);
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM courses WHERE id = @id; DELETE FROM semesters WHERE id = @semester";
            command.Parameters.AddWithValue("@id", courseId);
            command.Parameters.AddWithValue("@semester", semesterId);
            command.ExecuteNonQuery();
        }

        var loaded = _assignments.GetAssignment(assignmentId);
        Assert.NotNull(loaded);
        Assert.Null(loaded!.CourseId);
        Assert.Null(loaded.SemesterId);
    }

    private static object? Scalar(Microsoft.Data.Sqlite.SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    [Fact]
    public void LinkDocument_IsDuplicateSafe_AndCascadeDeletes()
    {
        var assignmentId = _assignments.AddAssignment(new Assignment { Title = "Lab" });
        var document = new StudyDocument { Name = "Lab notes" };
        Assert.True(Repo.Add(document));
        Assert.True(_assignments.LinkDocument(assignmentId, document.Id));
        Assert.False(_assignments.LinkDocument(assignmentId, document.Id));
        Assert.Single(_assignments.GetDocumentIds(assignmentId));
        Assert.True(Repo.Delete(document.Id));
        Assert.True(Repo.PermanentDeleteDocument(document.Id));
        Assert.Empty(_assignments.GetDocumentIds(assignmentId));
    }

    [Fact]
    public void ReplaceDocumentLinks_RollsBackWhenAnyDocumentIsInvalid()
    {
        var assignmentId = _assignments.AddAssignment(new Assignment { Title = "Lab" });
        var document = new StudyDocument { Name = "Lab notes" };
        Assert.True(Repo.Add(document));
        Assert.True(_assignments.LinkDocument(assignmentId, document.Id));
        Assert.False(_assignments.ReplaceDocumentLinks(assignmentId, [document.Id, 999999]));
        Assert.Equal([document.Id], _assignments.GetDocumentIds(assignmentId));
    }

    [Fact]
    public void LinkDocument_RejectsUnknownParents()
    {
        Assert.False(_assignments.LinkDocument(999, 999));
    }
}
