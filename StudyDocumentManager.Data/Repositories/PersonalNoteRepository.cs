using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public class PersonalNoteRepository : IPersonalNoteRepository
{
    private readonly DatabaseHelper _db;

    public PersonalNoteRepository(DatabaseHelper db) => _db = db;

    public string? GetNote(int documentId) => _db.GetPersonalNote(documentId);

    public bool SaveNote(int documentId, string content) => _db.SavePersonalNote(documentId, content);

    public bool DeleteNote(int documentId) => _db.DeletePersonalNote(documentId);
}
