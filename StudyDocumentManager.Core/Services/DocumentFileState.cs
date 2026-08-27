namespace StudyDocumentManager.Core.Services;

/// <summary>
/// Lifecycle state of a document's linked file, as reported by FileStateClassifier.
/// </summary>
public enum DocumentFileState
{
    /// <summary>No path stored on the document.</summary>
    NotSet,

    /// <summary>File exists and is readable.</summary>
    Ok,

    /// <summary>Path is well formed but no file exists there.</summary>
    Missing,

    /// <summary>Access was denied by the OS (permissions or lock).</summary>
    AccessDenied,

    /// <summary>The path lives on a drive or network share that is not reachable.</summary>
    DriveDisconnected,

    /// <summary>Path is malformed, relative, or otherwise unusable.</summary>
    InvalidPath
}

public static class FileStateClassifier
{
    /// <summary>
    /// Classifies a stored document path. The probe decides whether the file is readable;
    /// it is expected to throw the concrete IO exception when access fails so the
    /// failure kind (missing / denied / disconnected drive / invalid) can be reported.
    /// <paramref name="rootReadyProbe"/> reports whether the path's drive or share root is
    /// reachable; it disambiguates DirectoryNotFoundException, which Windows raises with
    /// HResult 0x80070003 both for a missing folder on a healthy drive and for any path
    /// on a disconnected drive or share.
    /// </summary>
    public static DocumentFileState Classify(string? path, Func<string, bool> probe, Func<string, bool>? rootReadyProbe = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return DocumentFileState.NotSet;

        if (!IsWellFormedAbsolutePath(path))
            return DocumentFileState.InvalidPath;

        try
        {
            return probe(path) ? DocumentFileState.Ok : DocumentFileState.Missing;
        }
        catch (UnauthorizedAccessException)
        {
            return DocumentFileState.AccessDenied;
        }
        catch (DirectoryNotFoundException)
        {
            return rootReadyProbe is not null && !rootReadyProbe(path)
                ? DocumentFileState.DriveDisconnected
                : DocumentFileState.Missing;
        }
        catch (IOException ex) when (IsDriveDisconnected(ex))
        {
            return DocumentFileState.DriveDisconnected;
        }
        catch (IOException ex) when (IsSharingViolation(ex))
        {
            return DocumentFileState.AccessDenied;
        }
        catch (IOException)
        {
            return DocumentFileState.Missing;
        }
        catch (ArgumentException)
        {
            return DocumentFileState.InvalidPath;
        }
        catch (NotSupportedException)
        {
            return DocumentFileState.InvalidPath;
        }
    }

    /// <summary>
    /// Production root-readiness probe. Drive-letter roots report DriveInfo.IsReady.
    /// A UNC share root either resolves to an existing directory (reachable) or it does
    /// not (unreachable share) — a reachable share's root always exists, so existence is
    /// the reachability signal. Unreachable shares surface as DirectoryNotFoundException
    /// with HResult 0x80070003, which the classifier maps to DriveDisconnected via this probe.
    /// </summary>
    public static bool RootReadyProbe(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root))
            return true;

        if (root.StartsWith(@"\\"))
            return Directory.Exists(root);

        try
        {
            return new DriveInfo(root).IsReady;
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Production probe: opens the file for read with share flags so merely locked
    /// files still classify as Ok, while permission/drive problems surface as exceptions.
    /// </summary>
    public static bool ReadableProbe(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        return true;
    }

    private static bool IsWellFormedAbsolutePath(string path)
    {
        try
        {
            if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                return false;

            var root = Path.GetPathRoot(path);
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(path))
                return false;

            // Reject drive-relative forms like "C:file" (root without a separator).
            if (root.Length == 2 && root[1] == ':')
                return false;

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsDriveDisconnected(IOException ex)
        => (uint)(ex.HResult & 0xFFFF) is 21u or 53u or 64u or 67u;

    private static bool IsSharingViolation(IOException ex)
        => (uint)(ex.HResult & 0xFFFF) is 32u or 33u;
}
