using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.ViewModels.Items;
using Xunit;

namespace StudyDocumentManager.Tests;

public class SelectableDocumentItemTests
{
    [Fact]
    public void MatchesSearch_FindsNameAuthorTypeAndTags_CaseInsensitive()
    {
        var item = new SelectableDocumentItem(new StudyDocument
        {
            Ten = "Giáo trình Toán",
            MonHoc = "Học tập",
            Loai = "PDF",
            TacGia = "Hayato",
            Tags = "math,pdf"
        });

        Assert.True(item.MatchesSearch("toán"));
        Assert.True(item.MatchesSearch("học"));
        Assert.True(item.MatchesSearch("pdf"));
        Assert.True(item.MatchesSearch("hayato"));
        Assert.True(item.MatchesSearch("math"));
    }

    [Fact]
    public void HasAuthor_ReturnsFalse_WhenAuthorMissing()
    {
        var item = new SelectableDocumentItem(new StudyDocument { Ten = "Doc", TacGia = "" });
        Assert.False(item.HasAuthor);
    }
}
