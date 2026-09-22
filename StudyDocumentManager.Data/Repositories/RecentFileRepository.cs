using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public class RecentFileRepository : IRecentFileRepository
{
    private readonly DatabaseHelper _db;

    public RecentFileRepository(DatabaseHelper db) => _db = db;

    public List<(int Id, string Name, string? Subject, string? Type, string? FilePath, DateTime OpenedAt)> GetAll()
        => _db.GetRecentFiles();

    public bool Add(int documentId) => _db.AddRecentFile(documentId);

    public void Clear() => _db.ClearRecentFiles();
}
