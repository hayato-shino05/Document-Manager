namespace StudyDocumentManager.Core.Entities;

public sealed record ArchiveExportOptions(IReadOnlyList<int>? DocumentIds = null, bool IncludeDeleted = true);

public sealed record ArchiveImportOptions(bool ValidateOnly = false);

public enum ArchiveTransactionOutcome
{
    NotStarted,
    Committed,
    RolledBack
}

public sealed record ArchiveReportItem(
    string Code,
    string Message,
    string? ExportKey = null,
    string? ArchivePath = null);

public sealed record ArchiveMissingFile(string DocumentExportKey, string ArchivePath);

public sealed record ArchiveConflict(string Code, string Message, string? ExportKey = null, string? ArchivePath = null);

public sealed record ArchiveValidationReport(bool IsValid, IReadOnlyList<ArchiveReportItem> ValidationErrors);

public sealed record ArchiveExportReport(
    bool Success,
    int ExportedDocuments,
    IReadOnlyList<ArchiveMissingFile> MissingFiles,
    IReadOnlyList<ArchiveConflict> Conflicts,
    IReadOnlyList<ArchiveReportItem> ValidationErrors);

public sealed record ArchiveImportReport(
    bool Success,
    int ImportedDocuments,
    int SkippedDocuments,
    IReadOnlyList<ArchiveMissingFile> MissingFiles,
    IReadOnlyList<ArchiveConflict> Conflicts,
    IReadOnlyList<ArchiveReportItem> ValidationErrors,
    ArchiveTransactionOutcome TransactionOutcome);
