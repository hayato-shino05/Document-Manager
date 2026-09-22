using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Models.Items;
using Xunit;

namespace StudyDocumentManager.Tests;

public class SelectableDocumentItemTests
{
    [Fact]
    public void MatchesSearch_FindsNameAuthorTypeAndTags_CaseInsensitive()
    {
        var item = new SelectableDocumentItem(new StudyDocument
        {
            Name = "Math Textbook",
            Subject = "Study",
            Type = "PDF",
            Author = "Hayato",
            Tags = "math,pdf"
        });

        Assert.True(item.MatchesSearch("math"));
        Assert.True(item.MatchesSearch("study"));
        Assert.True(item.MatchesSearch("pdf"));
        Assert.True(item.MatchesSearch("hayato"));
        Assert.True(item.MatchesSearch("textbook"));
    }

    [Fact]
    public void HasAuthor_ReturnsFalse_WhenAuthorMissing()
    {
        var item = new SelectableDocumentItem(new StudyDocument { Name = "Doc", Author = "" });
        Assert.False(item.HasAuthor);
    }
}
