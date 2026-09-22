namespace StudyDocumentManager.Core.Interfaces;

/// <summary>
/// Core dialog service for showing messages, confirmations, and text input.
/// Platform-specific dialogs (file pickers, custom UI dialogs) belong in
/// IFileDialogService and ICustomDialogService in the Presentation layer.
/// </summary>
public interface IDialogService
{
    /// <summary>Show an informational message.</summary>
    Task ShowMessageAsync(string title, string message);

    /// <summary>Show an error message.</summary>
    Task ShowErrorAsync(string title, string message);

    /// <summary>Show a confirmation dialog. Returns true if confirmed.</summary>
    Task<bool> ShowConfirmAsync(string title, string message);

    /// <summary>
    /// Show a confirmation dialog with custom confirm button text and optional danger styling.
    /// Use isDanger=true for destructive actions (delete, permanent remove etc.).
    /// Returns true if confirmed.
    /// </summary>
    Task<bool> ShowConfirmAsync(string title, string message,
        string confirmText, bool isDanger = false);

    /// <summary>Show an input dialog. Returns the entered string or null if cancelled.</summary>
    Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "");
}
