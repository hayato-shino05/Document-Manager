namespace StudyDocumentManager.Services;

/// <summary>
/// Navigation service for switching views in the main content area.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navigate to a specific view by its key name.
    /// </summary>
    void NavigateTo(string viewKey);

    /// <summary>
    /// Navigate to a specific view with a parameter (e.g. document ID).
    /// </summary>
    void NavigateTo(string viewKey, object? parameter);

    /// <summary>
    /// Check if we can go back.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Go to the previous view.
    /// </summary>
    void GoBack();
}
