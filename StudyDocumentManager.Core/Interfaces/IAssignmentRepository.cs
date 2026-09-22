using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

public interface IAssignmentRepository
{
    StudentContext? GetStudentContext();
    bool SaveStudentContext(StudentContext context);
    List<Course> GetCourses();
    int AddCourse(Course course);
    bool UpdateCourse(Course course);
    bool DeleteCourse(int id);
    List<Semester> GetSemesters();
    int AddSemester(Semester semester);
    bool UpdateSemester(Semester semester);
    bool DeleteSemester(int id);
    List<Assignment> GetAssignments();
    Assignment? GetAssignment(int id);
    int AddAssignment(Assignment assignment);
    bool UpdateAssignment(Assignment assignment);
    bool DeleteAssignment(int id);
    bool LinkDocument(int assignmentId, int documentId);
    bool UnlinkDocument(int assignmentId, int documentId);
    bool ReplaceDocumentLinks(int assignmentId, IReadOnlyList<int> documentIds);
    List<int> GetDocumentIds(int assignmentId);
}
