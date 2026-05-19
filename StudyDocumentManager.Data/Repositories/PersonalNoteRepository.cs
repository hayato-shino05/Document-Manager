using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public class PersonalNoteRepository : IPersonalNote
{
    public string? GetNote(int documentId) => DatabaseHelper.GetPersonalNote(documentId);

    public bool SaveNote(int documentId, string content) => DatabaseHelper.SavePersonalNote(documentId, content);

    public bool DeleteNote(int documentId) => DatabaseHelper.DeletePersonalNote(documentId);
}
