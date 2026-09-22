using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public sealed class WatchedFolderRepository(DatabaseHelper db) : IWatchedFolderRepository
{
    public IReadOnlyList<WatchedFolder> GetAll() => db.GetWatchedFolders();
    public IReadOnlyList<WatchedFolder> GetEnabled() => db.GetEnabledWatchedFolders();
    public WatchedFolder? GetByPath(string folderPath) => db.GetWatchedFolderByPath(folderPath);
    public int Add(WatchedFolder item) => db.InsertWatchedFolder(item);
    public bool Update(WatchedFolder item) => db.UpdateWatchedFolder(item);
    public bool Delete(int id) => db.DeleteWatchedFolder(id);
    public bool SetEnabled(int id, bool enabled) => db.SetWatchedFolderEnabled(id, enabled);
    public bool RecordScan(int id, DateTime scannedAt) => db.RecordWatchedFolderScan(id, scannedAt);
}
