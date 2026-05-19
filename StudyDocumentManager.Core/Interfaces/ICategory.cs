namespace StudyDocumentManager.Core.Interfaces;

public interface ICategory
{
    List<string> GetAllSubjects();
    List<string> GetAllTypes();
    List<(string Name, int Count)> GetSubjectsWithCount();
    List<(string Name, int Count)> GetTypesWithCount();
    bool AddSubject(string name);
    bool AddType(string name);
    bool UpdateSubjectName(string oldName, string newName);
    bool UpdateTypeName(string oldName, string newName);
    bool DeleteDocumentsBySubject(string subjectName);
    bool DeleteDocumentsByType(string typeName);
    int GetTotalDocumentCount();
}
