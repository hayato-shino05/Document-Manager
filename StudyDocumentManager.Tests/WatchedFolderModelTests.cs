using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Tests;

public sealed class FakeWatchedFolderRepository : IWatchedFolderRepository
{
    private readonly List<WatchedFolder> _items = new();
    private int _nextId = 1;
    public List<(int id, bool enabled)> SetEnabledCalls { get; } = new();
    public List<int> Deleted { get; } = new();
    public List<(int id, DateTime at)> Scans { get; } = new();

    public IReadOnlyList<WatchedFolder> GetAll()
    {
        // Return fresh instances, mirroring how the real repository maps each
        // query into new entities (so callers cannot rely on object identity).
        var clones = _items.Select(i => new WatchedFolder
        {
            Id = i.Id,
            FolderPath = i.FolderPath,
            Enabled = i.Enabled,
            IncludeSubdirectories = i.IncludeSubdirectories,
            LastScanAt = i.LastScanAt,
            CreatedAt = i.CreatedAt
        }).ToList();
        return clones.AsReadOnly();
    }
    public IReadOnlyList<WatchedFolder> GetEnabled() => _items.FindAll(f => f.Enabled).AsReadOnly();
    public WatchedFolder? GetByPath(string path)
        => _items.Find(f => string.Equals(f.FolderPath, path, StringComparison.OrdinalIgnoreCase));

    public int Add(WatchedFolder item)
    {
        item.Id = _nextId++;
        _items.Add(item);
        return item.Id;
    }
    public bool Update(WatchedFolder item)
    {
        var existing = _items.Find(f => f.Id == item.Id);
        if (existing is null) return false;
        existing.Enabled = item.Enabled;
        existing.IncludeSubdirectories = item.IncludeSubdirectories;
        existing.FolderPath = item.FolderPath;
        return true;
    }
    public bool Delete(int id)
    {
        Deleted.Add(id);
        return _items.RemoveAll(f => f.Id == id) > 0;
    }
    public bool SetEnabled(int id, bool enabled)
    {
        SetEnabledCalls.Add((id, enabled));
        var existing = _items.Find(f => f.Id == id);
        if (existing is null) return false;
        existing.Enabled = enabled;
        return true;
    }
    public bool RecordScan(int id, DateTime at)
    {
        Scans.Add((id, at));
        return true;
    }
}

public sealed class FakeWatchedFolderWatcher : IWatchedFolderWatcher
{
    public bool Started { get; private set; }
    public bool Stopped { get; private set; }
    public bool Disposed { get; private set; }
    public bool IsRunning { get; private set; }

    public void Start() { Started = true; IsRunning = true; }
    public void Stop() { Stopped = true; IsRunning = false; }
    public void Dispose() => Disposed = true;
}

public sealed class FakeWatchedFolderWatcherFactory : IWatchedFolderWatcherFactory
{
    public List<FakeWatchedFolderWatcher> Created { get; } = new();
    public List<string> CreatedPaths { get; } = new();
    public List<bool> CreatedInclude { get; } = new();
    public IWatchedFolderWatcher Create(WatchedFolder config)
    {
        CreatedPaths.Add(config.FolderPath);
        CreatedInclude.Add(config.IncludeSubdirectories);
        var w = new FakeWatchedFolderWatcher();
        Created.Add(w);
        return w;
    }
}

public sealed class FakeNavigationService : INavigationService
{
    public string? LastNavigated;
    public void NavigateTo(string viewKey) => LastNavigated = viewKey;
    public void NavigateTo(string viewKey, object? parameter) => LastNavigated = viewKey;
    public bool CanGoBack => true;
    public void GoBack() => LastNavigated = "dashboard";
}

public sealed class FakeLog : ILog
{
    public void Information(string message) { }
    public void Warning(string message, Exception? exception = null) { }
    public void Error(string message, Exception? exception = null) { }
}

public sealed class FakeLocalizationService : ILocalizationService
{
    public event EventHandler? LanguageChanged;
    public SupportedLanguage CurrentLanguage { get; set; } = SupportedLanguage.Japanese;
    public IReadOnlyList<SupportedLanguage> AvailableLanguages =>
        new[] { SupportedLanguage.Japanese, SupportedLanguage.English };
    public string Suffix = "";
    public string this[string key] => key + Suffix;
    public void SetLanguage(SupportedLanguage language)
    {
        CurrentLanguage = language;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
    public void RaiseLanguageChanged() => LanguageChanged?.Invoke(this, EventArgs.Empty);
}

public class WatchedFolderModelTests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), $"sdm_model_{Guid.NewGuid():N}");

    private (WatchedFolderModel model, FakeWatchedFolderRepository repo, FakeWatchedFolderWatcherFactory factory) Build()
    {
        var repo = new FakeWatchedFolderRepository();
        var factory = new FakeWatchedFolderWatcherFactory();
        var model = new WatchedFolderModel(repo, factory, new FakeLog(), new FakeNavigationService(), new FakeLocalizationService());
        return (model, repo, factory);
    }

    [Fact]
    public void Load_PopulatesFolders_AndStartsWatchers()
    {
        var (model, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });

        model.Load();

        Assert.Single(model.Folders);
        Assert.Single(factory.Created);
        Assert.True(factory.Created[0].Started);
        Assert.True(model.IsWatching);
    }

    [Fact]
    public void AddFolder_ValidPath_AddsToFolders_AndStartsWatcher()
    {
        var (model, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);

        model.AddFolder(dir);

        Assert.Single(model.Folders);
        Assert.Equal(dir, model.Folders[0].FolderPath);
        Assert.Single(factory.Created);
        Assert.True(factory.Created[0].Started);
        Assert.True(model.HasFolders);
    }

    [Fact]
    public void AddFolder_MissingPath_SetsLastError()
    {
        var (model, repo, factory) = Build();
        model.AddFolder(Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}"));
        Assert.False(string.IsNullOrEmpty(model.LastError));
        Assert.Empty(model.Folders);
    }

    [Fact]
    public void ToggleEnabled_Disabled_StopsWatcher()
    {
        var (model, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        model.Load();
        var item = model.Folders[0];
        item.Enabled = false;

        model.ToggleEnabled(item);

        Assert.Contains((item.Id, false), repo.SetEnabledCalls);
        Assert.True(factory.Created[0].Stopped);
    }

    [Fact]
    public void RemoveFolder_StopsAndRemoves()
    {
        var (model, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        model.Load();
        var id = model.Folders[0].Id;

        model.RemoveFolder(id);

        Assert.Empty(model.Folders);
        Assert.Contains(id, repo.Deleted);
        Assert.True(factory.Created[0].Stopped);
        Assert.True(factory.Created[0].Disposed);
    }

    [Fact]
    public void StopWatching_StopsAllWatchers()
    {
        var (model, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        model.Load();

        model.StopWatching();

        Assert.True(factory.Created[0].Stopped);
        Assert.False(model.IsWatching);
    }

    [Fact]
    public void GoBack_NavigatesToDashboard()
    {
        var (model, _, _) = Build();
        var nav = new FakeNavigationService();
        var m2 = new WatchedFolderModel(new FakeWatchedFolderRepository(), new FakeWatchedFolderWatcherFactory(), new FakeLog(), nav, new FakeLocalizationService());
        m2.GoBack();
        Assert.Equal("dashboard", nav.LastNavigated);
    }

    [Fact]
    public void Dispose_StopsWatchers()
    {
        var (model, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        model.Load();

        model.Dispose();

        Assert.True(factory.Created[0].Stopped);
        Assert.True(factory.Created[0].Disposed);
    }

    [Fact]
    public void Load_CalledAgain_DoesNotDuplicateWatchers()
    {
        var (model, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        model.Load();
        var firstWatcher = factory.Created[0];

        model.Load();

        Assert.Single(factory.Created);
        Assert.Same(firstWatcher, factory.Created[0]);
        Assert.True(firstWatcher.Started);
        Assert.False(firstWatcher.Stopped);
    }

    [Fact]
    public void Load_StartsWatchersForPersistedFolders()
    {
        var (model, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });

        model.Load();

        Assert.True(model.IsWatching);
        Assert.Single(factory.Created);
        Assert.True(factory.Created[0].Started);
    }

    [Fact]
    public void StatusMessage_UsesLocalization()
    {
        var (model, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });

        model.Load();

        Assert.Equal("WF_Status_Watching", model.StatusMessage);
    }

    [Fact]
    public void AddFolder_MissingPath_LocalizesError()
    {
        var (model, repo, factory) = Build();
        model.AddFolder(Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}"));
        Assert.Equal("WF_Error_NotFound", model.LastError);
    }

    [Fact]
    public void AddFolder_EmptyPath_LocalizesError()
    {
        var (model, repo, factory) = Build();
        model.AddFolder("   ");
        Assert.Equal("WF_Error_PathRequired", model.LastError);
    }

    [Fact]
    public void LanguageChanged_RefreshesStatus()
    {
        var repo2 = new FakeWatchedFolderRepository();
        var dir2 = TempDir();
        Directory.CreateDirectory(dir2);
        repo2.Add(new WatchedFolder { FolderPath = dir2, Enabled = true });
        var loc2 = new FakeLocalizationService();
        var m2 = new WatchedFolderModel(repo2, new FakeWatchedFolderWatcherFactory(), new FakeLog(), new FakeNavigationService(), loc2);
        m2.Load();
        Assert.Equal("WF_Status_Watching", m2.StatusMessage);

        loc2.Suffix = "#en";
        loc2.RaiseLanguageChanged();

        Assert.Equal("WF_Status_Watching#en", m2.StatusMessage);
    }

    [Fact]
    public void Dispose_UnsubscribesLanguageChanged()
    {
        var repo = new FakeWatchedFolderRepository();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        var loc = new FakeLocalizationService();
        var m = new WatchedFolderModel(repo, new FakeWatchedFolderWatcherFactory(), new FakeLog(), new FakeNavigationService(), loc);
        m.Load();
        m.Dispose();

        loc.Suffix = "#x";
        loc.RaiseLanguageChanged();

        Assert.Equal("WF_Status_Watching", m.StatusMessage);
    }

    [Fact]
    public void Load_MissingFolder_NotAddedToActive_ReportsError_NotFakeWatching()
    {
        var (model, repo, factory) = Build();
        var missing = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}");
        repo.Add(new WatchedFolder { FolderPath = missing, Enabled = true });

        model.Load();

        Assert.False(model.IsWatching);
        Assert.Empty(factory.Created);
        Assert.Equal(WatcherStatus.Error, model.Folders[0].WatcherStatus);
        Assert.False(string.IsNullOrEmpty(model.Folders[0].WatcherError));
    }

    [Fact]
    public void Load_DisabledFolder_NotStarted_ReportsDisabled()
    {
        var (model, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = false });

        model.Load();

        Assert.False(model.IsWatching);
        Assert.Empty(factory.Created);
        Assert.Equal(WatcherStatus.Disabled, model.Folders[0].WatcherStatus);
    }

    [Fact]
    public void RetryFolder_AfterPathFixed_StartsWatcher()
    {
        var (model, repo, factory) = Build();
        var missing = Path.Combine(Path.GetTempPath(), $"retry_{Guid.NewGuid():N}");
        repo.Add(new WatchedFolder { FolderPath = missing, Enabled = true });
        model.Load();
        Assert.False(model.IsWatching);
        Assert.Empty(factory.Created);

        Directory.CreateDirectory(missing);
        model.RetryFolder(model.Folders[0].Id);

        Assert.True(model.IsWatching);
        Assert.Single(factory.Created);
        Assert.True(factory.Created[0].Started);
        Assert.Equal(WatcherStatus.Running, model.Folders[0].WatcherStatus);
    }

    [Fact]
    public void AddFolder_TrimsPath_BeforeCheckingExistence()
    {
        var (model, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var padded = "  " + dir + "  ";

        model.AddFolder(padded);

        Assert.Single(model.Folders);
        Assert.Equal(dir, model.Folders[0].FolderPath);
        Assert.Single(factory.Created);
    }

    [Fact]
    public void AddFolder_IncludeSubdirectories_PassedFromModelProperty()
    {
        var (model, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        model.NewFolderPath = dir;
        model.NewFolderIncludeSubdirectories = true;

        model.AddNewFolder();

        Assert.Single(model.Folders);
        Assert.True(model.Folders[0].IncludeSubdirectories);
        Assert.Single(factory.Created);
    }

    [Fact]
    public void Load_Reconciles_RemovedFolder_StopsStale_StartsRemaining()
    {
        var (model, repo, factory) = Build();
        var dirA = TempDir();
        Directory.CreateDirectory(dirA);
        var a = new WatchedFolder { FolderPath = dirA, Enabled = true };
        repo.Add(a);
        model.Load();
        Assert.Single(factory.Created);
        Assert.True(factory.Created[0].Started);

        var dirB = TempDir();
        Directory.CreateDirectory(dirB);
        repo.Delete(a.Id);
        repo.Add(new WatchedFolder { FolderPath = dirB, Enabled = true });
        model.Load();

        Assert.True(factory.Created[0].Stopped);
        Assert.Single(factory.Created, c => c.Started && !c.Stopped);
        Assert.True(model.IsWatching);
        Assert.Single(model.Folders);
    }

    [Fact]
    public void Load_Reconciles_DisabledFolder_StopsWatcher()
    {
        var (model, repo, factory) = Build();
        var dirA = TempDir();
        Directory.CreateDirectory(dirA);
        var a = new WatchedFolder { FolderPath = dirA, Enabled = true };
        repo.Add(a);
        model.Load();
        Assert.Single(factory.Created);
        Assert.True(factory.Created[0].Started);

        repo.SetEnabled(a.Id, false);
        model.Load();

        Assert.True(factory.Created[0].Stopped);
        Assert.False(model.IsWatching);
        Assert.DoesNotContain(factory.Created, c => c.Started && !c.Stopped);
    }

    [Fact]
    public void Load_Reconciles_PathChanged_RestartsWithNewPath()
    {
        var (model, repo, factory) = Build();
        var dirA = TempDir();
        Directory.CreateDirectory(dirA);
        var a = new WatchedFolder { FolderPath = dirA, Enabled = true };
        repo.Add(a);
        model.Load();
        Assert.Equal(dirA, factory.CreatedPaths[0]);

        var dirB = TempDir();
        Directory.CreateDirectory(dirB);
        repo.Update(new WatchedFolder { Id = a.Id, FolderPath = dirB, Enabled = true });
        model.Load();

        Assert.True(factory.Created[0].Stopped);
        Assert.Equal(2, factory.Created.Count);
        Assert.Equal(dirB, factory.CreatedPaths[1]);
        Assert.True(factory.Created[1].Started);
        Assert.True(model.IsWatching);
    }

    [Fact]
    public void Load_MissingFolder_SetsObservableErrorOnBoundItem()
    {
        var (model, repo, factory) = Build();
        var missing = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}");
        repo.Add(new WatchedFolder { FolderPath = missing, Enabled = true });

        model.Load();

        var bound = model.Folders[0];
        Assert.Equal(WatcherStatus.Error, bound.WatcherStatus);
        Assert.False(string.IsNullOrEmpty(bound.WatcherError));
        Assert.IsAssignableFrom<System.ComponentModel.INotifyPropertyChanged>(bound);
    }

    [Fact]
    public void RetryFolder_RaisesPropertyChanged_WhenStatusChanges()
    {
        var (model, repo, factory) = Build();
        var missing = Path.Combine(Path.GetTempPath(), $"retry_{Guid.NewGuid():N}");
        repo.Add(new WatchedFolder { FolderPath = missing, Enabled = true });
        model.Load();
        var bound = model.Folders[0];
        var changes = new List<string?>();
        bound.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        Directory.CreateDirectory(missing);
        model.RetryFolder(bound.Id);

        Assert.Contains("WatcherStatus", changes);
        Assert.Equal(WatcherStatus.Running, bound.WatcherStatus);
    }

    [Fact]
    public void ToggleEnabled_Disabled_RaisesPropertyChanged_ForStatus()
    {
        var (model, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        model.Load();
        var bound = model.Folders[0];
        var changes = new List<string?>();
        bound.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        bound.Enabled = false;
        model.ToggleEnabled(bound);

        Assert.Contains("WatcherStatus", changes);
        Assert.Equal(WatcherStatus.Disabled, bound.WatcherStatus);
    }

    [Fact]
    public void Load_ReloadedItem_ReflectsRunningStatus_NotUnknown()
    {
        var (model, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var a = new WatchedFolder { FolderPath = dir, Enabled = true, IncludeSubdirectories = false };
        repo.Add(a);
        model.Load();
        Assert.Equal(WatcherStatus.Running, model.Folders[0].WatcherStatus);

        // Reload fresh items from the repository (e.g. re-navigation).
        model.Load();

        Assert.Equal(WatcherStatus.Running, model.Folders[0].WatcherStatus);
        Assert.Single(factory.Created);
        Assert.False(factory.Created[0].Stopped);
    }

    [Fact]
    public void Load_IncludeSubdirectoriesChanged_RestartsWatcher_NoDuplicate()
    {
        var (model, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var a = new WatchedFolder { FolderPath = dir, Enabled = true, IncludeSubdirectories = false };
        repo.Add(a);
        model.Load();
        Assert.Single(factory.Created);
        Assert.False(factory.CreatedInclude[0]);

        // Configuration changes only IncludeSubdirectories -> restart with new config.
        repo.Update(new WatchedFolder { Id = a.Id, FolderPath = dir, Enabled = true, IncludeSubdirectories = true });
        model.Load();

        Assert.Equal(2, factory.Created.Count);
        Assert.True(factory.Created[0].Stopped);
        Assert.True(factory.Created[1].Started);
        Assert.Single(factory.Created, c => c.Started && !c.Stopped);
        Assert.True(factory.CreatedInclude[1]);
        Assert.Equal(WatcherStatus.Running, model.Folders[0].WatcherStatus);
        Assert.True(model.Folders[0].IncludeSubdirectories);
    }

    [Fact]
    public void StopWatching_UpdatesItemStatus_ToStopped_NotFakeRunning()
    {
        var (model, repo, factory) = Build();
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        repo.Add(new WatchedFolder { FolderPath = dir, Enabled = true });
        model.Load();
        Assert.Equal(WatcherStatus.Running, model.Folders[0].WatcherStatus);

        var changes = new List<string?>();
        model.Folders[0].PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        model.StopWatching();

        Assert.False(model.IsWatching);
        Assert.True(factory.Created[0].Stopped);
        Assert.Equal(WatcherStatus.Stopped, model.Folders[0].WatcherStatus);
        Assert.Null(model.Folders[0].WatcherError);
        Assert.Contains("WatcherStatus", changes);
    }

    [Fact]
    public void LanguageChanged_RecomputesPerItemWatcherError()
    {
        var repo = new FakeWatchedFolderRepository();
        var missing = Path.Combine(Path.GetTempPath(), $"lang_{Guid.NewGuid():N}");
        repo.Add(new WatchedFolder { FolderPath = missing, Enabled = true });
        var loc = new FakeLocalizationService();
        var factory = new FakeWatchedFolderWatcherFactory();
        var model = new WatchedFolderModel(repo, factory, new FakeLog(), new FakeNavigationService(), loc);
        model.Load();

        Assert.Equal("WF_Error_NotFound", model.Folders[0].WatcherError);
        Assert.Equal("WF_Error_NotFound", model.Folders[0].WatcherErrorKey);

        loc.Suffix = "#en";
        loc.RaiseLanguageChanged();

        Assert.Equal("WF_Error_NotFound#en", model.Folders[0].WatcherError);
    }
}
