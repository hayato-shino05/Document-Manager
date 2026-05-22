using StudyDocumentManager.Core.DTOs;
using Xunit;

namespace StudyDocumentManager.Tests;

public class AddDocumentDraftTests
{
    [Fact]
    public void ToStudyDocument_MapsAllFieldsAndProvidedSize()
    {
        var draft = new AddDocumentDraft
        {
            Name = "Math Textbook",
            Subject = "Study",
            Type = "PDF",
            FilePath = @"C:\docs\math.pdf",
            Notes = "Important notes",
            Author = "Hayato",
            Tags = "math,pdf",
            IsImportant = true,
            Deadline = new DateTime(2026, 4, 30),
            FileSize = 1.25
        };

        var document = draft.ToStudyDocument();

        Assert.Equal("Math Textbook", document.Name);
        Assert.Equal("Study", document.Subject);
        Assert.Equal("PDF", document.Type);
        Assert.Equal(@"C:\docs\math.pdf", document.FilePath);
        Assert.Equal("Important notes", document.Notes);
        Assert.Equal("Hayato", document.Author);
        Assert.Equal("math,pdf", document.Tags);
        Assert.True(document.IsImportant);
        Assert.Equal(new DateTime(2026, 4, 30), document.Deadline);
        Assert.Equal(1.25, document.FileSize);
    }
}
