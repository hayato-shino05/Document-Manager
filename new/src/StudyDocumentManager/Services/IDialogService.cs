using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Services;

/// <summary>
/// Dialog service for showing message boxes, confirmations, and file pickers.
/// </summary>
public interface IDialogService
{
    /// <summary>Show an informational message.</summary>
    Task ShowMessageAsync(string title, string message);

    /// <summary>Show an error message.</summary>
    Task ShowErrorAsync(string title, string message);

    /// <summary>Show a confirmation dialog. Returns true if confirmed.</summary>
    Task<bool> ShowConfirmAsync(string title, string message);

    /// <summary>Show an input dialog. Returns the entered string or null if cancelled.</summary>
    Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "");

    /// <summary>
    /// Show the dedicated category-picker dialog with chip pills and autocomplete.
    /// Returns the chosen/entered category name, or null if cancelled.
    /// </summary>
    Task<string?> ShowChangeCategoryAsync(string documentName, IList<string> existingCategories, string currentCategory);

    /// <summary>
    /// Show a searchable document picker dialog.
    /// Returns the selected documents, or null if cancelled.
    /// </summary>
    Task<List<StudyDocument>?> ShowDocumentPickerAsync(
        string collectionName,
        IEnumerable<StudyDocument> allDocuments,
        IEnumerable<int> alreadyInCollection);

    /// <summary>Show a file open dialog. Returns selected file path or null.</summary>
    Task<string?> ShowOpenFileAsync(string title, string? filter = null);

    /// <summary>Show a folder open dialog. Returns selected folder path or null.</summary>
    Task<string?> ShowOpenFolderAsync(string title);

    /// <summary>Show a file save dialog. Returns selected path or null.</summary>
    Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null);
}
