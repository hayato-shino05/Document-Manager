using System.Diagnostics;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

/// <summary>
/// Versioned backup over <see cref="IFileIntegrityRepository"/>: timestamped copies of the
/// live database stored under <c>&lt;db-folder&gt;/backups</c>, pruned by a persisted retention count.
/// The live database file is only ever written by <see cref="Restore"/> after explicit caller-side confirmation.
/// </summary>
public class VersionedBackupService : IVersionedBackupService
{
    private const string RetentionSettingKey = "backup_retention_count";
    private const int DefaultRetentionCount = 10;
    private const int MaxRetentionCount = 100;

    private readonly IFileIntegrityRepository _repo;
    private readonly ISettingsService _settings;

    public VersionedBackupService(IFileIntegrityRepository repo, ISettingsService settings)
    {
        _repo = repo;
        _settings = settings;
    }

    public string BackupDirectory => Path.Combine(
        Path.GetDirectoryName(_repo.DatabasePath) ?? ".",
        "backups");

    public int RetentionCount
    {
        get
        {
            var raw = _settings.GetSetting(RetentionSettingKey);
            return int.TryParse(raw, out var value)
                ? Math.Clamp(value, 1, MaxRetentionCount)
                : DefaultRetentionCount;
        }
        set
        {
            var clamped = Math.Clamp(value, 1, MaxRetentionCount);
            _settings.SetSetting(RetentionSettingKey,
                clamped.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public IReadOnlyList<BackupVersionInfo> ListVersions()
    {
        if (!Directory.Exists(BackupDirectory))
            return [];

        var files = Directory.GetFiles(BackupDirectory, "*.db");
        var result = new List<BackupVersionInfo>(files.Length);
        foreach (var file in files)
        {
            var info = new FileInfo(file);
            result.Add(new BackupVersionInfo(
                file,
                ParseTimestampFromName(file) ?? info.LastWriteTime,
                info.Length,
                IsValid: false,
                IsLatest: false));
        }

        result.Sort((a, b) => b.CreatedAtLocal.CompareTo(a.CreatedAtLocal));

        var validated = new List<BackupVersionInfo>(result.Count);
        for (var i = 0; i < result.Count; i++)
        {
            var candidate = result[i];
            validated.Add(candidate with
            {
                IsValid = IsRestorable(candidate.FilePath),
                IsLatest = i == 0
            });
        }

        return validated;
    }

    public BackupVersionInfo? GetLatest() => ListVersions().FirstOrDefault();

    public BackupVersionInfo? CreateVersion()
    {
        try
        {
            Directory.CreateDirectory(BackupDirectory);
            var destination = Path.Combine(BackupDirectory, BuildFileName(DateTime.Now));

            // Timestamp resolution is seconds; guarantee uniqueness without overwriting.
            var attempt = 0;
            while (File.Exists(destination) && attempt < 5)
            {
                attempt++;
                destination = Path.Combine(BackupDirectory, BuildFileName(DateTime.Now.AddSeconds(attempt)));
            }

            if (!_repo.BackupDatabase(destination, overwrite: false))
                return null;

            PruneRetention();

            var info = new FileInfo(destination);
            return new BackupVersionInfo(
                destination,
                ParseTimestampFromName(destination) ?? info.LastWriteTime,
                info.Length,
                IsValid: true,
                IsLatest: true);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public RestorePlan? PlanRestore(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)
            || !File.Exists(sourcePath)
            || !IsRestorable(sourcePath))
            return null;

        var info = new FileInfo(sourcePath);
        return new RestorePlan(
            sourcePath,
            ParseTimestampFromName(sourcePath) ?? info.LastWriteTime,
            _repo.DatabasePath,
            _repo.GetDocumentCount());
    }

    public RestoreOutcome Restore(string sourcePath)
    {
        if (!IsRestorable(sourcePath))
            return new RestoreOutcome(false, RestartRequired: false, ErrorKey: "RC_ErrorInvalidVersion");

        return _repo.RestoreDatabase(sourcePath)
            ? new RestoreOutcome(true, RestartRequired: true, ErrorKey: null)
            : new RestoreOutcome(false, RestartRequired: false, ErrorKey: "RC_ErrorRestoreFailed");
    }

    public int PruneRetention()
    {
        try
        {
            if (!Directory.Exists(BackupDirectory))
                return 0;

            var retention = RetentionCount;
            var versions = ListVersions();
            var removed = 0;
            for (var i = retention; i < versions.Count; i++)
            {
                try
                {
                    File.Delete(versions[i].FilePath);
                    removed++;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return removed;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public int EnsureFreshBackup(TimeSpan maxAge)
    {
        try
        {
            var latest = GetLatest();
            if (latest is not null && DateTime.Now - latest.CreatedAtLocal < maxAge)
                return 0;

            return CreateVersion() is null ? 0 : 1;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private bool IsRestorable(string path)
    {
        try
        {
            return File.Exists(path) && _repo.CanRestoreDatabase(path);
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static string BuildFileName(DateTime localTime)
        => $"study_documents_v{localTime:yyyyMMdd_HHmmss}.db";

    private static DateTime? ParseTimestampFromName(string filePath)
    {
        var name = Path.GetFileName(filePath);
        const string prefix = "study_documents_v";
        const string suffix = ".db";
        if (name.Length <= prefix.Length + suffix.Length
            || !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return null;

        var stamp = name[prefix.Length..^suffix.Length];
        return DateTime.TryParseExact(
            stamp,
            "yyyyMMdd_HHmmss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var value)
                ? value
                : null;
    }
}
