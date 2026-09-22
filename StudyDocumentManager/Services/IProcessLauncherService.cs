namespace StudyDocumentManager.Services;

public interface IProcessLauncherService
{
    void OpenFile(string filePath);

    /// <summary>
    /// Opens the given folder in the platform file browser.
    /// Default interface member so existing test stubs stay valid; production overrides with a real launcher.
    /// </summary>
    void OpenFolder(string folderPath)
    {
    }

    void RevealInExplorer(string filePath);
    void OpenUrl(string url);
}
