using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public sealed class PlatformInfo : IPlatformInfo
{
    public bool IsLinux => OperatingSystem.IsLinux();

    public string AnalyticsPlatform => IsLinux ? "linux" : "windows";
}
