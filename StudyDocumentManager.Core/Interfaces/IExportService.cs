using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

public interface IExportService
{
    Task<ExportResult> ExportCsvAsync(IReadOnlyList<StudyDocument> documents, string? suggestedFileName);
}

public record ExportResult(bool Success, string? FilePath = null, int Count = 0, string? Error = null);
