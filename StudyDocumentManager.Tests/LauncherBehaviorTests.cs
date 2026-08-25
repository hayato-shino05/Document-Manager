using System.Diagnostics;
using Xunit;
using StudyDocumentManager.Services;
using StudyDocumentManager.Tests.TestDoubles;

namespace StudyDocumentManager.Tests;

/// <summary>
/// Platform launcher behavior proofs (Issue #45): Windows and Linux branches are
/// verified through the injectable start-process seam without launching anything.
/// </summary>
public sealed class LauncherBehaviorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"sdm_launcher_{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch { /* best effort */ }
    }

    private string MakeDir()
    {
        Directory.CreateDirectory(_root);
        return _root;
    }

    private string MakeFile(string name)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, "launcher test");
        return path;
    }

    private static string? SingleFileName(IReadOnlyList<ProcessStartInfo> started)
        => started.Count == 1 ? started[0].FileName : null;

    [Fact]
    public void Linux_RevealInExplorer_ExistingFile_OpensParentDirectory()
    {
        var started = new List<ProcessStartInfo>();
        var launcher = new ProcessLauncherService(new StubPlatformInfo(isLinux: true), psi => started.Add(psi));
        var file = Path.Combine(MakeDir(), "doc.pdf");
        File.WriteAllText(file, "x");

        launcher.RevealInExplorer(file);

        var psi = Assert.Single(started);
        Assert.Equal("xdg-open", psi.FileName);
        Assert.Equal(_root, Assert.Single(psi.ArgumentList));
    }

    [Fact]
    public void Linux_OpenFolder_StartsXdgOpenOnFolder()
    {
        var started = new List<ProcessStartInfo>();
        var launcher = new ProcessLauncherService(new StubPlatformInfo(isLinux: true), psi => started.Add(psi));
        var dir = MakeDir();

        launcher.OpenFolder(dir);

        var psi = Assert.Single(started);
        Assert.Equal("xdg-open", psi.FileName);
        Assert.Equal(dir, Assert.Single(psi.ArgumentList));
    }

    [Fact]
    public void Linux_RevealInExplorer_MissingDirectory_StartsNothing()
    {
        var started = new List<ProcessStartInfo>();
        var launcher = new ProcessLauncherService(new StubPlatformInfo(isLinux: true), psi => started.Add(psi));

        launcher.RevealInExplorer(Path.Combine(_root, "gone", "doc.pdf"));

        Assert.Empty(started);
    }

    [Fact]
    public void Windows_RevealInExplorer_ExistingFile_SelectsTheFile()
    {
        var started = new List<ProcessStartInfo>();
        var launcher = new ProcessLauncherService(new StubPlatformInfo(isLinux: false), psi => started.Add(psi));
        var file = MakeFile("doc.pdf");

        launcher.RevealInExplorer(file);

        var psi = Assert.Single(started);
        Assert.Equal("explorer.exe", psi.FileName);
        Assert.Equal($"/select,\"{file}\"", psi.Arguments);
    }

    [Fact]
    public void Windows_RevealInExplorer_MissingFileWithExistingDir_OpensDirectory()
    {
        var started = new List<ProcessStartInfo>();
        var launcher = new ProcessLauncherService(new StubPlatformInfo(isLinux: false), psi => started.Add(psi));
        var dir = MakeDir();

        launcher.RevealInExplorer(Path.Combine(dir, "missing.pdf"));

        var psi = Assert.Single(started);
        Assert.Equal("explorer.exe", psi.FileName);
        Assert.Equal($"\"{dir}\"", psi.Arguments);
    }

    [Fact]
    public void Windows_RevealInExplorer_MissingFileAndDirectory_StartsNothing()
    {
        var started = new List<ProcessStartInfo>();
        var launcher = new ProcessLauncherService(new StubPlatformInfo(isLinux: false), psi => started.Add(psi));

        launcher.RevealInExplorer(Path.Combine(_root, "gone", "doc.pdf"));

        Assert.Empty(started);
    }

    [Fact]
    public void Windows_OpenFolder_ExistingDirectory_ShellOpensFolder()
    {
        var started = new List<ProcessStartInfo>();
        var launcher = new ProcessLauncherService(new StubPlatformInfo(isLinux: false), psi => started.Add(psi));
        var dir = MakeDir();

        launcher.OpenFolder(dir);

        var psi = Assert.Single(started);
        Assert.Equal(dir, psi.FileName);
        Assert.True(psi.UseShellExecute);
    }

    [Fact]
    public void Windows_OpenFolder_MissingDirectory_StartsNothing()
    {
        var started = new List<ProcessStartInfo>();
        var launcher = new ProcessLauncherService(new StubPlatformInfo(isLinux: false), psi => started.Add(psi));

        launcher.OpenFolder(Path.Combine(_root, "gone"));

        Assert.Empty(started);
    }
}
