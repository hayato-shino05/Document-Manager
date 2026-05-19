using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Services;

public interface IDroppedFileImportService
{
    List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects);
    List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes);
    bool SaveDocument(StudyDocument document);
    StudyDocument BuildDocumentFromPath(string filePath);
}
