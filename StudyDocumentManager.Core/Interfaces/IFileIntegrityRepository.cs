namespace StudyDocumentManager.Core.Interfaces;

public interface IFileIntegrityRepository
{
    bool UpdateDocumentPath(int id, string newPath);
    bool ClearDocumentPath(int id);
    bool BackupDatabase(string destPath, bool overwrite);
    bool CanRestoreDatabase(string sourcePath);
    bool RestoreDatabase(string sourcePath);
    string DatabasePath { get; }
}
