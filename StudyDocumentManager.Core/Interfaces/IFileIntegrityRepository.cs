using System.Threading;

namespace StudyDocumentManager.Core.Interfaces;

public interface IFileIntegrityRepository
{
    bool UpdateDocumentPath(int id, string newPath);
    bool ClearDocumentPath(int id);
    bool BackupDatabase(string destPath, bool overwrite);
    bool CanRestoreDatabase(string sourcePath);
    bool RestoreDatabase(string sourcePath);

    /// <summary>
    /// Cancellation-aware backup. Defaults to the non-token overload so existing
    /// implementations keep working; production overrides to honor the token.
    /// </summary>
    bool BackupDatabase(string destPath, bool overwrite, CancellationToken cancellationToken)
        => BackupDatabase(destPath, overwrite);

    /// <summary>
    /// Cancellation-aware restore. Defaults to the non-token overload so existing
    /// implementations keep working; production overrides to honor the token.
    /// Must not replace the live database after cancellation is requested.
    /// </summary>
    bool RestoreDatabase(string sourcePath, CancellationToken cancellationToken)
        => RestoreDatabase(sourcePath);

    /// <summary>
    /// Number of non-deleted documents, used for restore impact summaries.
    /// Default interface member so existing test stubs stay valid; production overrides with a real query.
    /// </summary>
    int GetDocumentCount() => 0;

    string DatabasePath { get; }
}
