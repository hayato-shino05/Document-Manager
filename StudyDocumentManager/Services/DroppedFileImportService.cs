using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Core.Services;
namespace StudyDocumentManager.Services;

public class DroppedFileImportService(
    IDocumentRepository repository,
    ICategoryRepository? categoryRepository = null) : IDroppedFileImportService
{
    private readonly IDocumentRepository _repository = repository;
    private readonly ICategoryRepository? _categoryRepository = categoryRepository;

    public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects)
    {
        var subjects = _categoryRepository?.GetAllSubjects() ?? [];
        if (subjects.Count > 0)
            return subjects;

        subjects = _repository.GetDistinctSubjects();
        return subjects.Count > 0 ? subjects : fallbackSubjects.ToList();
    }

    public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes)
    {
        var types = _categoryRepository?.GetAllTypes() ?? [];
        if (types.Count > 0)
            return types;

        types = _repository.GetDistinctTypes();
        return types.Count > 0 ? types : fallbackTypes.ToList();
    }

    public DocumentImportOutcome SaveDocument(StudyDocument document)
    {
        try
        {
            return _repository.AddWithCatalogs(document)
                ? DocumentImportOutcome.Imported
                : DocumentImportOutcome.Failed;
        }
        catch (SqliteException exception) when (
            exception.SqliteExtendedErrorCode == 2067 &&
            exception.Message.Contains("documents.file_path", StringComparison.Ordinal))
        {
            return DocumentImportOutcome.SkippedDuplicate;
        }
    }

    public StudyDocument BuildDocumentFromPath(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        return new StudyDocument
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            FilePath = filePath,
            Type = FileTypeDetector.DetectFromPath(filePath),
            FileSize = fileInfo.Length / (1024.0 * 1024.0)
        };
    }
}
