namespace StudyDocumentManager.Core.Entities;

public sealed class StudentContext
{
    public int Id { get; set; }
    public string AcademicYear { get; set; } = string.Empty;
    public string Semester { get; set; } = string.Empty;
    public string Course { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
}
