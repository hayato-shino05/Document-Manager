using System.Linq;

namespace StudyDocumentManager.Core.Entities;

public static class DocumentStatus
{
    public const string Unread = "unread";
    public const string InProgress = "in-progress";
    public const string Read = "read";
    public const string NeedsAction = "needs-action";
    public const string Completed = "completed";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All =
    [
        Unread,
        InProgress,
        Read,
        NeedsAction,
        Completed,
        Archived
    ];

    public static bool IsValid(string? value)
        => !string.IsNullOrEmpty(value) && All.Contains(value);
}
