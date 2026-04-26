using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.DTOs;

public class AddDocumentDraft
{
    public string Ten { get; set; } = string.Empty;
    public string MonHoc { get; set; } = string.Empty;
    public string Loai { get; set; } = string.Empty;
    public string DuongDan { get; set; } = string.Empty;
    public string GhiChu { get; set; } = string.Empty;
    public string TacGia { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public bool QuanTrong { get; set; }
    public DateTime? Deadline { get; set; }
    public double? KichThuoc { get; set; }

    public StudyDocument ToStudyDocument()
    {
        return new StudyDocument
        {
            Ten = Ten,
            MonHoc = MonHoc,
            Loai = Loai,
            DuongDan = DuongDan,
            GhiChu = GhiChu,
            TacGia = TacGia,
            Tags = Tags,
            QuanTrong = QuanTrong,
            Deadline = Deadline,
            KichThuoc = KichThuoc
        };
    }
}
