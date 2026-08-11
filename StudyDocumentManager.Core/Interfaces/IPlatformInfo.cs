namespace StudyDocumentManager.Core.Interfaces;

public interface IPlatformInfo
{
    bool IsLinux { get; }

    string AnalyticsPlatform { get; }
}
