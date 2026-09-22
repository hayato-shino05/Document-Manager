namespace StudyDocumentManager.Core.Entities;

public sealed class Course
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
