using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Services;

public enum DocumentImportOutcome
{
    Imported,
    SkippedDuplicate,
    Failed
}

public interface IDroppedFileImportService
{
    List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects);
    List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes);
    DocumentImportOutcome SaveDocument(StudyDocument document);
    StudyDocument BuildDocumentFromPath(string filePath);
}
