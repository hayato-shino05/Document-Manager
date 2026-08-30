using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

public interface IPersonalDocumentArchiveService
{
    Task<ArchiveExportReport> ExportAsync(string destinationZip, ArchiveExportOptions options);
    Task<ArchiveImportReport> ImportAsync(string sourceZip, ArchiveImportOptions options);
}
