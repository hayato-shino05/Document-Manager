using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Core.Services;

namespace StudyDocumentManager.Services;

public class DroppedFileImportService(IDocument repository) : IDroppedFileImportService
{
    private readonly IDocument _repository = repository;

    public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects)
    {
        var subjects = _repository.GetDistinctSubjects();
        return subjects.Count > 0 ? subjects : fallbackSubjects.ToList();
    }

    public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes)
    {
        var types = _repository.GetDistinctTypes();
        return types.Count > 0 ? types : fallbackTypes.ToList();
    }

    public bool SaveDocument(StudyDocument document)
    {
        if (!_repository.Add(document))
            return false;

        _repository.EnsureSubjectExists(document.Subject);
        _repository.EnsureTypeExists(document.Type);
        return true;
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
