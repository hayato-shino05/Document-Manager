using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public sealed class FileSystemWatcherAdapterFactory : IFileSystemWatcherAdapterFactory
{
    public IFileSystemWatcherAdapter Create(string folderPath, bool includeSubdirectories)
        => new FileSystemWatcherAdapter(folderPath, includeSubdirectories);
}
