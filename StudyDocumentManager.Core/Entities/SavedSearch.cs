namespace StudyDocumentManager.Core.Entities;

public class SavedSearch
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CriteriaJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
