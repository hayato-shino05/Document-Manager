using Microsoft.Win32;
using System.Runtime.Versioning;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public sealed class InstallationIdentityService : IInstallationIdentityService
{
    private const string DefaultRegistryPath = @"Software\DocumentManager";
    private const string InstallationIdValueName = "InstallationId";
    private const string LinuxDataDirectoryName = "study-document-manager";
    private const string LinuxInstallationIdFileName = "installation-id";
    private readonly IPlatformInfo _platformInfo;
    private readonly string _registryPath;
    private readonly string _linuxInstallationIdFilePath;

    public InstallationIdentityService(IPlatformInfo platformInfo)
        : this(platformInfo, DefaultRegistryPath, GetDefaultLinuxInstallationIdFilePath())
    {
    }

    public InstallationIdentityService()
        : this(new PlatformInfo())
    {
    }

    internal InstallationIdentityService(string registryPath)
        : this(new PlatformInfo(), registryPath, GetDefaultLinuxInstallationIdFilePath())
    {
    }

    internal InstallationIdentityService(IPlatformInfo platformInfo, string linuxInstallationIdFilePath)
        : this(platformInfo, DefaultRegistryPath, linuxInstallationIdFilePath)
    {
    }

    private InstallationIdentityService(IPlatformInfo platformInfo, string registryPath, string linuxInstallationIdFilePath)
    {
        _platformInfo = platformInfo ?? throw new ArgumentNullException(nameof(platformInfo));
        ArgumentException.ThrowIfNullOrWhiteSpace(registryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(linuxInstallationIdFilePath);
        _registryPath = registryPath;
        _linuxInstallationIdFilePath = linuxInstallationIdFilePath;
    }

    public string GetInstallationId()
    {
        if (_platformInfo.IsLinux)
            return GetLinuxInstallationId();

        if (OperatingSystem.IsWindows())
            return GetWindowsInstallationId();

        throw new PlatformNotSupportedException("Installation identity is supported on Windows and Linux only.");
    }

    public void DeleteInstallationId()
    {
        if (_platformInfo.IsLinux)
        {
            File.Delete(_linuxInstallationIdFilePath);
            return;
        }

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Installation identity is supported on Windows and Linux only.");

        using var key = Registry.CurrentUser.OpenSubKey(_registryPath, writable: true);
        key?.DeleteValue(InstallationIdValueName, throwOnMissingValue: false);
    }

    [SupportedOSPlatform("windows")]
    private string GetWindowsInstallationId()
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

    private string GetLinuxInstallationId()
    {
        if (File.Exists(_linuxInstallationIdFilePath))
        {
            var installationId = File.ReadAllText(_linuxInstallationIdFilePath);
            if (Guid.TryParse(installationId, out _))
                return installationId;
        }

        var newInstallationId = Guid.NewGuid().ToString();
        Directory.CreateDirectory(Path.GetDirectoryName(_linuxInstallationIdFilePath)!);
        File.WriteAllText(_linuxInstallationIdFilePath, newInstallationId);
        return newInstallationId;
    }

    private static string GetDefaultLinuxInstallationIdFilePath()
    {
        var dataDirectory = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        var baseDirectory = string.IsNullOrWhiteSpace(dataDirectory) || !Path.IsPathFullyQualified(dataDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
            : dataDirectory;

        return Path.Combine(baseDirectory, LinuxDataDirectoryName, LinuxInstallationIdFileName);
    }
}
