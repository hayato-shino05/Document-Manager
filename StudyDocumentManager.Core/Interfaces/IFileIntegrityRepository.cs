namespace StudyDocumentManager.Core.Interfaces;

public interface IFileIntegrityRepository
{
    bool UpdateDocumentPath(int id, string newPath);
    bool ClearDocumentPath(int id);
    bool BackupDatabase(string destPath, bool overwrite);
    bool CanRestoreDatabase(string sourcePath);
    bool RestoreDatabase(string sourcePath);

    /// <summary>
    /// Number of non-deleted documents, used for restore impact summaries.
    /// Default interface member so existing test stubs stay valid; production overrides with a real query.
    /// </summary>
    int GetDocumentCount() => 0;

    string DatabasePath { get; }
}
