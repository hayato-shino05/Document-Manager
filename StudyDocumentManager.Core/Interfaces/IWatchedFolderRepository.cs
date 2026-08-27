using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

public interface IWatchedFolderRepository
{
    IReadOnlyList<WatchedFolder> GetAll();
    IReadOnlyList<WatchedFolder> GetEnabled();
    WatchedFolder? GetByPath(string folderPath);
    int Add(WatchedFolder item);
    bool Update(WatchedFolder item);
    bool Delete(int id);
    bool SetEnabled(int id, bool enabled);
    bool RecordScan(int id, DateTime scannedAt);
}
