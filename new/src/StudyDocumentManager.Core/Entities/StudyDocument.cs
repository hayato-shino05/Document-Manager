namespace StudyDocumentManager.Core.Entities;

/// <summary>
/// Main entity representing a study document.
/// Property names match Vietnamese DB column names for compatibility.
/// </summary>
public class StudyDocument
{
    public int Id { get; set; }
    public string Ten { get; set; } = string.Empty;
    public string MonHoc { get; set; } = string.Empty;
    public string Loai { get; set; } = string.Empty;
    public string DuongDan { get; set; } = string.Empty;
    public string GhiChu { get; set; } = string.Empty;
    public DateTime NgayThem { get; set; }
    public double? KichThuoc { get; set; }
    public string TacGia { get; set; } = string.Empty;
    public bool QuanTrong { get; set; }
    public string Tags { get; set; } = string.Empty;
    public DateTime? Deadline { get; set; }

    public StudyDocument()
    {
        NgayThem = DateTime.Now;
        QuanTrong = false;
    }
}
