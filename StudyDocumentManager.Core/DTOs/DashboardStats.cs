namespace StudyDocumentManager.Core.DTOs;

/// <summary>
/// Dashboard statistics DTO.
/// </summary>
public class DashboardStats
{
    public int TotalDocuments { get; set; }
    public int ImportantDocuments { get; set; }
    public int NoFileDocuments { get; set; }
    public int NearDeadlineDocuments { get; set; }
    public int OverdueDocuments { get; set; }
    public int TotalCategories { get; set; }
    public int TotalCollections { get; set; }
}
