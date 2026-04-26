namespace StudyDocumentManager.Core.Interfaces;

public interface IReport
{
    List<(string Label, int Count)> GetBySubject();
    List<(string Label, int Count)> GetByType();
    List<(string Label, int Count)> GetByDay(int days = 7);
    List<(string Label, int Count)> GetByMonth(int months = 12);
}
