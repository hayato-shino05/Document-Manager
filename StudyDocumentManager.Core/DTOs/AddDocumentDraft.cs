using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.DTOs;

public class AddDocumentDraft
{
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public bool IsImportant { get; set; }
    public DateTime? Deadline { get; set; }
    public double? FileSize { get; set; }

    public StudyDocument ToStudyDocument()
    {
        return new StudyDocument
        {
            Name = Name,
            Subject = Subject,
            Type = Type,
            FilePath = FilePath,
            Notes = Notes,
            Author = Author,
            Tags = Tags,
            IsImportant = IsImportant,
            Deadline = Deadline,
            FileSize = FileSize
        };
    }
}
