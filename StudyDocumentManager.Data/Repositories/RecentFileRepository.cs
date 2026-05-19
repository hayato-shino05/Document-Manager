using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public class RecentFileRepository : IRecentFile
{
    public List<(int Id, string Ten, string? MonHoc, string? Loai, string? DuongDan, DateTime OpenedAt)> GetAll()
        => DatabaseHelper.GetRecentFiles();

    public void Add(int documentId) => DatabaseHelper.AddRecentFile(documentId);

    public void Clear() => DatabaseHelper.ClearRecentFiles();
}
