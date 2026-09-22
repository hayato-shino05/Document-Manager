namespace StudyDocumentManager.Core.Entities;

public sealed class Semester
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? StartsOn { get; set; }
    public DateTime? EndsOn { get; set; }
    public bool IsActive { get; set; }
}
