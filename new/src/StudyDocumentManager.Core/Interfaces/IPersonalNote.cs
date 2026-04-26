namespace StudyDocumentManager.Core.Interfaces;

public interface IPersonalNote
{
    string? GetNote(int documentId);
    bool SaveNote(int documentId, string content);
    bool DeleteNote(int documentId);
}
