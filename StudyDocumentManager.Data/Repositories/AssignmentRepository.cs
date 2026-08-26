using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public sealed class AssignmentRepository : IAssignmentRepository
{
    private readonly DatabaseHelper _db;

    public AssignmentRepository(DatabaseHelper db) => _db = db;

    public StudentContext? GetStudentContext() => _db.GetStudentContext();
    public bool SaveStudentContext(StudentContext context) => _db.SaveStudentContext(context);
    public List<Course> GetCourses() => _db.GetCourses();
    public int AddCourse(Course course) => _db.AddCourse(course);
    public bool UpdateCourse(Course course) => _db.UpdateCourse(course);
    public bool DeleteCourse(int id) => _db.DeleteCourse(id);
    public List<Semester> GetSemesters() => _db.GetSemesters();
    public int AddSemester(Semester semester) => _db.AddSemester(semester);
    public bool UpdateSemester(Semester semester) => _db.UpdateSemester(semester);
    public bool DeleteSemester(int id) => _db.DeleteSemester(id);
    public List<Assignment> GetAssignments() => _db.GetAssignments();
    public Assignment? GetAssignment(int id) => _db.GetAssignment(id);
    public int AddAssignment(Assignment assignment) => _db.AddAssignment(assignment);
    public bool UpdateAssignment(Assignment assignment) => _db.UpdateAssignment(assignment);
    public bool DeleteAssignment(int id) => _db.DeleteAssignment(id);
    public bool LinkDocument(int assignmentId, int documentId) => _db.LinkAssignmentDocument(assignmentId, documentId);
    public bool UnlinkDocument(int assignmentId, int documentId) => _db.UnlinkAssignmentDocument(assignmentId, documentId);
    public bool ReplaceDocumentLinks(int assignmentId, IReadOnlyList<int> documentIds) => _db.ReplaceAssignmentDocumentLinks(assignmentId, documentIds);
    public List<int> GetDocumentIds(int assignmentId) => _db.GetAssignmentDocumentIds(assignmentId);
}
