namespace StudyDocumentManager.Core.Interfaces;

public interface IRecentFile
{
    List<(int Id, string Ten, string? MonHoc, string? Loai, string? DuongDan, DateTime OpenedAt)> GetAll();
    void Add(int documentId);
    void Clear();
}
