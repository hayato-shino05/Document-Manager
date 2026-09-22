namespace StudyDocumentManager.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}
