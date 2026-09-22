using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public class ReportRepository : IReportRepository
{
    private readonly DatabaseHelper _db;

    public ReportRepository(DatabaseHelper db) => _db = db;

    public List<(string Label, int Count)> GetBySubject() => _db.GetDocumentsBySubject();

    public List<(string Label, int Count)> GetByType() => _db.GetDocumentsByType();

    public List<(string Label, int Count)> GetByDay(int days = 7) => _db.GetDocumentsByDay(days);

    public List<(string Label, int Count)> GetByMonth(int months = 12) => _db.GetDocumentsByMonth(months);
}
