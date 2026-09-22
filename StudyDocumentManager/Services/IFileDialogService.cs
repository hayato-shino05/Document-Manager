namespace StudyDocumentManager.Services;

public interface IFileDialogService
{
    Task<string?> ShowOpenFileAsync(string title, string? filter = null);

    Task<string?> ShowOpenFolderAsync(string title);

    Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null);
}
