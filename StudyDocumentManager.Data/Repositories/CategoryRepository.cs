using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public class CategoryRepository : ICategory
{
    public List<string> GetAllSubjects() => DatabaseHelper.GetAllSubjects();

    public List<string> GetAllTypes() => DatabaseHelper.GetAllTypes();

    public List<(string Name, int Count)> GetSubjectsWithCount() => DatabaseHelper.GetSubjectsWithCount();

    public List<(string Name, int Count)> GetTypesWithCount() => DatabaseHelper.GetTypesWithCount();

    public bool AddSubject(string name) => DatabaseHelper.AddSubject(name);

    public bool AddType(string name) => DatabaseHelper.AddType(name);

    public bool UpdateSubjectName(string oldName, string newName) => DatabaseHelper.UpdateSubjectName(oldName, newName);

    public bool UpdateTypeName(string oldName, string newName) => DatabaseHelper.UpdateTypeName(oldName, newName);

    public bool DeleteDocumentsBySubject(string subjectName) => DatabaseHelper.DeleteDocumentsBySubject(subjectName);

    public bool DeleteDocumentsByType(string typeName) => DatabaseHelper.DeleteDocumentsByType(typeName);

    public int GetTotalDocumentCount() => DatabaseHelper.GetTotalDocumentCount();
}
