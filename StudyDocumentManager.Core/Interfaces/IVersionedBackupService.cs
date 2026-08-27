namespace StudyDocumentManager.Core.Interfaces;

/// <summary>
/// One restore point file inside the versioned backup directory.
/// </summary>
public sealed record BackupVersionInfo(
    string FilePath,
    DateTime CreatedAtLocal,
    long SizeBytes,
    bool IsValid,
    bool IsLatest);

/// <summary>
/// Impact summary shown to the user before a restore is confirmed.
/// </summary>
public sealed record RestorePlan(
    string SourcePath,
    DateTime SourceCreatedAtLocal,
    string CurrentDatabasePath,
    int CurrentDocumentCount);

/// <summary>
/// Result of a restore attempt. RestartRequired is always true on success:
/// pooled connections and in-memory caches must be reloaded by restarting the app.
/// </summary>
public sealed record RestoreOutcome(bool Success, bool RestartRequired, string? ErrorKey);

public interface IVersionedBackupService
{
    /// <summary>Folder that holds all backup versions. Never the live database folder root.</summary>
    string BackupDirectory { get; }

    /// <summary>Configured number of versions to keep (persisted in app_settings, clamped 1..100).</summary>
    int RetentionCount { get; set; }

    /// <summary>All versions, newest first. IsValid reflects a restore-candidate integrity check.</summary>
    IReadOnlyList<BackupVersionInfo> ListVersions();

    /// <summary>Newest version, or null when no backup exists yet.</summary>
    BackupVersionInfo? GetLatest();

    /// <summary>
    /// Creates a new backup version from the current database and applies retention pruning.
    /// Never touches the live database file itself.
    /// </summary>
    BackupVersionInfo? CreateVersion();

    /// <summary>Impact summary for the confirmation dialog. Null when the source is not a valid restore candidate.</summary>
    RestorePlan? PlanRestore(string sourcePath);

    /// <summary>
    /// Restores the given version. The caller must have confirmed with the user first;
    /// this method overwrites the current database file when it succeeds.
    /// </summary>
    RestoreOutcome Restore(string sourcePath);

    /// <summary>Removes oldest versions beyond RetentionCount. Returns the number removed.</summary>
    int PruneRetention();

    /// <summary>
    /// Creates a backup when the newest version is older than maxAge. Intended for app startup.
    /// Returns the number of versions created; never throws.
    /// </summary>
    int EnsureFreshBackup(TimeSpan maxAge);
}
