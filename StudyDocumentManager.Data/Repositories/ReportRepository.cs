using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public class ReportRepository : IReport
{
    public List<(string Label, int Count)> GetBySubject() => DatabaseHelper.GetDocumentsBySubject();

    public List<(string Label, int Count)> GetByType() => DatabaseHelper.GetDocumentsByType();

    public List<(string Label, int Count)> GetByDay(int days = 7) => DatabaseHelper.GetDocumentsByDay(days);

    public List<(string Label, int Count)> GetByMonth(int months = 12) => DatabaseHelper.GetDocumentsByMonth(months);
}
