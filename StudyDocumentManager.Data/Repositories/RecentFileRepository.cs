using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public class RecentFileRepository : IRecentFile
{
    public List<(int Id, string Name, string? Subject, string? Type, string? FilePath, DateTime OpenedAt)> GetAll()
        => DatabaseHelper.GetRecentFiles();

    public void Add(int documentId) => DatabaseHelper.AddRecentFile(documentId);

    public void Clear() => DatabaseHelper.ClearRecentFiles();
}
