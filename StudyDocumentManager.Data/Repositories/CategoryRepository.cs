using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly DatabaseHelper _db;

    public CategoryRepository(DatabaseHelper db) => _db = db;

    public List<string> GetAllSubjects() => _db.GetAllSubjects();

    public List<string> GetAllTypes() => _db.GetAllTypes();

    public List<(string Name, int Count)> GetSubjectsWithCount() => _db.GetSubjectsWithCount();

    public List<(string Name, int Count)> GetTypesWithCount() => _db.GetTypesWithCount();

    public bool AddSubject(string name) => _db.AddSubject(name);

    public bool AddType(string name) => _db.AddType(name);

    public bool UpdateSubjectName(string oldName, string newName) => _db.UpdateSubjectName(oldName, newName);

    public bool UpdateTypeName(string oldName, string newName) => _db.UpdateTypeName(oldName, newName);

    public bool DeleteDocumentsBySubject(string subjectName) => _db.DeleteDocumentsBySubject(subjectName);

    public bool DeleteDocumentsByType(string typeName) => _db.DeleteDocumentsByType(typeName);

    public int GetTotalDocumentCount() => _db.GetTotalDocumentCount();
}
