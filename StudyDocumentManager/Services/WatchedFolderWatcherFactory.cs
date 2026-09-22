using System;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public interface IWatchedFolderWatcherFactory
{
    IWatchedFolderWatcher Create(WatchedFolder config);
}

public sealed class WatchedFolderWatcherFactory : IWatchedFolderWatcherFactory
{
    private readonly IImportInboxRepository _inbox;
    private readonly IWatchedFolderRepository _folders;
    private readonly IFileSystemWatcherAdapterFactory _adapterFactory;
    private readonly ILog _log;
    private readonly TimeSpan? _debounce;

    public WatchedFolderWatcherFactory(
        IImportInboxRepository inbox,
        IWatchedFolderRepository folders,
        IFileSystemWatcherAdapterFactory adapterFactory,
        ILog log,
        TimeSpan? debounce = null)
    {
        _inbox = inbox ?? throw new ArgumentNullException(nameof(inbox));
        _folders = folders ?? throw new ArgumentNullException(nameof(folders));
        _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _debounce = debounce;
    }

    public IWatchedFolderWatcher Create(WatchedFolder config)
        => new WatchedFolderWatcher(config, _inbox, _folders, _adapterFactory, _log, _debounce);
}
