using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public class DroppedFileImportService(IDocument repository)
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

        _repository.EnsureSubjectExists(document.MonHoc);
        _repository.EnsureTypeExists(document.Loai);
        return true;
    }

    public StudyDocument BuildDocumentFromPath(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        return new StudyDocument
        {
            Ten = Path.GetFileNameWithoutExtension(filePath),
            DuongDan = filePath,
            Loai = FileTypeDetector.DetectFromPath(filePath),
            KichThuoc = fileInfo.Length / (1024.0 * 1024.0)
        };
    }
}
