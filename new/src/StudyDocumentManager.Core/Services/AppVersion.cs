namespace StudyDocumentManager.Core.Services;

/// <summary>
/// Application version management. Ported from WinForms Services/AppVersion.cs.
/// </summary>
public static class AppVersion
{
    public static string Current => "4.0.0";

    /// <summary>
    /// Compare two semver strings. Returns:
    /// -1 if current is older, 0 if equal, 1 if current is newer.
    /// </summary>
    public static int Compare(string current, string latest)
    {
        var currentParts = ParseVersion(current);
        var latestParts = ParseVersion(latest);

        for (int i = 0; i < 3; i++)
        {
            if (currentParts[i] < latestParts[i]) return -1;
            if (currentParts[i] > latestParts[i]) return 1;
        }
        return 0;
    }

    /// <summary>
    /// Returns true if the latest version is newer than current.
    /// </summary>
    public static bool IsNewer(string latest) => Compare(Current, latest) < 0;

    private static int[] ParseVersion(string version)
    {
        var parts = version.TrimStart('v', 'V').Split('.');
        var result = new int[3];
        for (int i = 0; i < Math.Min(parts.Length, 3); i++)
        {
            int.TryParse(parts[i], out result[i]);
        }
        return result;
    }
}
