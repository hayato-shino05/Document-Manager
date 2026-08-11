using Microsoft.Win32;
using System.Runtime.Versioning;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

[SupportedOSPlatform("windows")]
public sealed class InstallationIdentityServiceTests : IDisposable
{
    private readonly string _registryPath = $@"Software\DocumentManager.Tests\{Guid.NewGuid():N}";

    [WindowsFact]
    public void GetInstallationId_FirstCall_CreatesValidGuid()
    {
        var installationId = CreateService().GetInstallationId();

        Assert.True(Guid.TryParse(installationId, out _));
    }

    [WindowsFact]
    public void GetInstallationId_RepeatedCalls_ReturnSameValue()
    {
        var service = CreateService();

        var firstInstallationId = service.GetInstallationId();
        var repeatedInstallationId = service.GetInstallationId();

        Assert.Equal(firstInstallationId, repeatedInstallationId);
    }

    [WindowsFact]
    public void DeleteInstallationId_NextCall_CreatesDifferentValue()
    {
        var service = CreateService();
        var firstInstallationId = service.GetInstallationId();

        service.DeleteInstallationId();
        var replacementInstallationId = service.GetInstallationId();

        Assert.NotEqual(firstInstallationId, replacementInstallationId);
        Assert.True(Guid.TryParse(replacementInstallationId, out _));
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_registryPath, false);
    }

    private InstallationIdentityService CreateService() => new(_registryPath);
}


[AttributeUsage(AttributeTargets.Method)]
internal sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Windows Registry tests require Windows.";
    }
}
