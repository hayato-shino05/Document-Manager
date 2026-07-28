namespace StudyDocumentManager.Core.Interfaces;

public interface IFileIntegrityRepository
{
    bool UpdateDocumentPath(int id, string newPath);
    bool ClearDocumentPath(int id);
    bool BackupDatabase(string destPath);
    string DatabasePath { get; }
}
