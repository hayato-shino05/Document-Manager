using System.Globalization;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public sealed class FileStartupDiagnostics : IStartupDiagnostics
{
    public const string EnvironmentVariableName = "SDM_STARTUP_DIAGNOSTICS_PATH";

    private readonly string _filePath;
    private readonly object _sync = new();

    public FileStartupDiagnostics(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Startup diagnostics file path is required.", nameof(filePath));

        _filePath = filePath;
    }

    public void RecordDatabaseInitializationSucceeded()
    {
        if (!Append("database_initialization_succeeded", "information", null))
            Console.Error.WriteLine("Startup diagnostics unavailable.");
    }

    public void RecordDatabaseInitializationFailed(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (!Append("database_initialization_failed", "error", exception.GetType().Name, ClassifyFailure(exception)))
            Console.Error.WriteLine("Startup diagnostics unavailable.");
    }

    internal static string ClassifyFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var message = exception.Message;
        if (message.StartsWith("Incomplete legacy database tables:", StringComparison.Ordinal))
            return "incomplete_legacy_schema";
        if (message.StartsWith("Unsupported database tables:", StringComparison.Ordinal) ||
            message.StartsWith("Unsupported legacy table ", StringComparison.Ordinal))
            return "unsupported_legacy_tables";
        if (message.StartsWith("Unsupported columns in ", StringComparison.Ordinal))
            return "unsupported_legacy_columns";
        if (message.StartsWith("Missing required columns in ", StringComparison.Ordinal))
            return "incomplete_legacy_schema";
        if (message.StartsWith("Unsupported legacy database schema:", StringComparison.Ordinal))
            return "unsupported_legacy_schema";
        if (message.StartsWith("SDM_DATABASE_PATH must be an absolute path.", StringComparison.Ordinal))
            return "invalid_database_path";
        return "unexpected_database_initialization_failure";
    }

    private bool Append(string eventName, string severity, string? exceptionType, string? diagnosticCategory = null)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        var line = exceptionType is null
            ? $"{timestamp} event={eventName} stage=database_initialization severity={severity}{Environment.NewLine}"
            : $"{timestamp} event={eventName} stage=database_initialization severity={severity} diagnostic_category={diagnosticCategory} exception_type={exceptionType}{Environment.NewLine}";

        try
        {
            lock (_sync)
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
                File.AppendAllText(_filePath, line);
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
