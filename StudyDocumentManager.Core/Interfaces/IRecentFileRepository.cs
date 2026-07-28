namespace StudyDocumentManager.Core.Interfaces;

public interface IRecentFileRepository
{
    List<(int Id, string Name, string? Subject, string? Type, string? FilePath, DateTime OpenedAt)> GetAll();
    void Add(int documentId);
    void Clear();
}
