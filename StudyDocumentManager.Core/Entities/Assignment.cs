namespace StudyDocumentManager.Core.Entities;

public sealed class Assignment
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? CourseId { get; set; }
    public int? SemesterId { get; set; }
    public DateTime? OfficialDeadline { get; set; }
    public DateTime? PersonalDeadline { get; set; }
    public string Status { get; set; } = "planned";
    public string Priority { get; set; } = "normal";
    public string Milestone { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
