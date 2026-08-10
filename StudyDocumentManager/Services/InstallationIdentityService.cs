using Microsoft.Win32;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public sealed class InstallationIdentityService : IInstallationIdentityService
{
    private const string DefaultRegistryPath = @"Software\DocumentManager";
    private const string InstallationIdValueName = "InstallationId";
    private readonly string _registryPath;

    public InstallationIdentityService()
        : this(DefaultRegistryPath)
    {
    }

    internal InstallationIdentityService(string registryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registryPath);
        _registryPath = registryPath;
    }

    public string GetInstallationId()
    {
        using var key = Registry.CurrentUser.CreateSubKey(_registryPath, writable: true)
            ?? throw new InvalidOperationException($"Registry key '{_registryPath}' could not be opened.");

        if (key.GetValue(InstallationIdValueName) is string installationId && Guid.TryParse(installationId, out _))
        {
            return installationId;
        }

        var newInstallationId = Guid.NewGuid().ToString();
        key.SetValue(InstallationIdValueName, newInstallationId, RegistryValueKind.String);
        return newInstallationId;
    }

    public void DeleteInstallationId()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_registryPath, writable: true);
        key?.DeleteValue(InstallationIdValueName, throwOnMissingValue: false);
    }
}
