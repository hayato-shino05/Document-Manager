using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Xunit;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Tests;

public class FileSystemWatcherAdapterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sdm_adapter_{Guid.NewGuid():N}");
    private readonly List<string> _seen = new();
    private readonly object _gate = new();

    public FileSystemWatcherAdapterTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
        catch { /* ignore */ }
    }

    [Fact]
    public void Adapter_ForwardsCreatedAndChanged_AndTracksExtensionless()
    {
        using var adapter = new FileSystemWatcherAdapter(_dir, false);
        var signal = new ManualResetEventSlim();
        var withExt = Path.Combine(_dir, "a.txt");
        var noExt = Path.Combine(_dir, "b");
        adapter.FileCreated += (_, e) =>
        {
            lock (_seen)
            {
                _seen.Add(e.FullPath);
                if (_seen.Contains(noExt) && _seen.Contains(withExt))
                    signal.Set();
            }
        };
        adapter.Start();

        File.WriteAllText(withExt, "x");
        File.WriteAllText(noExt, "y");
        File.AppendAllText(withExt, "z"); // triggers a Changed event

        Assert.True(signal.Wait(TimeSpan.FromSeconds(10)), $"seen: {string.Join(",", _seen)}");
        lock (_seen)
        {
            Assert.Contains(withExt, _seen);
            Assert.Contains(noExt, _seen);
        }
    }
}
