namespace StudyDocumentManager.Services;

public interface IProcessLauncherService
{
    void OpenFile(string filePath);
    void RevealInExplorer(string filePath);
    void OpenUrl(string url);
}
