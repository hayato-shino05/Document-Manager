using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public class AddDocumentDraftTests
{
    [Fact]
    public void ToStudyDocument_MapsAllFieldsAndProvidedSize()
    {
        var draft = new AddDocumentDraft
        {
            Ten = "Giáo trình Toán",
            MonHoc = "Học tập",
            Loai = "PDF",
            DuongDan = @"C:\docs\math.pdf",
            GhiChu = "Ghi chú",
            TacGia = "Hayato",
            Tags = "math,pdf",
            QuanTrong = true,
            Deadline = new DateTime(2026, 4, 30),
            KichThuoc = 1.25
        };

        var document = draft.ToStudyDocument();

        Assert.Equal("Giáo trình Toán", document.Ten);
        Assert.Equal("Học tập", document.MonHoc);
        Assert.Equal("PDF", document.Loai);
        Assert.Equal(@"C:\docs\math.pdf", document.DuongDan);
        Assert.Equal("Ghi chú", document.GhiChu);
        Assert.Equal("Hayato", document.TacGia);
        Assert.Equal("math,pdf", document.Tags);
        Assert.True(document.QuanTrong);
        Assert.Equal(new DateTime(2026, 4, 30), document.Deadline);
        Assert.Equal(1.25, document.KichThuoc);
    }
}
