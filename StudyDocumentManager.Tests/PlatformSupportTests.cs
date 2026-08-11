using System.Diagnostics;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class PlatformSupportTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"study-document-manager-{Guid.NewGuid():N}");

    [Fact]
    public void LinuxInstallationIdentity_PersistsIdInDataFile()
    {
        var identityFilePath = Path.Combine(_temporaryDirectory, "installation-id");
        var service = new InstallationIdentityService(new PlatformInfoStub(isLinux: true), identityFilePath);

        var installationId = service.GetInstallationId();

        Assert.True(Guid.TryParse(installationId, out _));
        Assert.Equal(installationId, File.ReadAllText(identityFilePath));
        Assert.Equal(installationId, service.GetInstallationId());
    }

    [Fact]
    public void LinuxLauncher_RevealInExplorer_UsesXdgOpenForContainingDirectory()
    {
        ProcessStartInfo? startedProcess = null;
        Directory.CreateDirectory(_temporaryDirectory);
        var documentPath = Path.Combine(_temporaryDirectory, "document.pdf");
        var service = new ProcessLauncherService(
            new PlatformInfoStub(isLinux: true),
            processStartInfo => startedProcess = processStartInfo);

        service.RevealInExplorer(documentPath);

        Assert.NotNull(startedProcess);
        Assert.Equal("xdg-open", startedProcess!.FileName);
        Assert.False(startedProcess.UseShellExecute);
        Assert.Equal(_temporaryDirectory, Assert.Single(startedProcess.ArgumentList));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
            Directory.Delete(_temporaryDirectory, recursive: true);
    }

    private sealed class PlatformInfoStub(bool isLinux) : IPlatformInfo
    {
        public bool IsLinux => isLinux;

        public string AnalyticsPlatform => isLinux ? "linux" : "windows";
    }
}
