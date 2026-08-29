using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public class PersonalNoteRepository : IPersonalNoteRepository
{
    private readonly DatabaseHelper _db;

    public PersonalNoteRepository(DatabaseHelper db) => _db = db;

    public IReadOnlyList<PersonalNote> GetNotes(int documentId, bool includeDeleted = false)
        => _db.GetPersonalNotes(documentId, includeDeleted);

    public bool SaveNote(PersonalNote note) => _db.SavePersonalNote(note);

    public bool DeleteNoteById(int noteId) => _db.DeletePersonalNoteById(noteId);

    public bool SetPinned(int noteId, bool isPinned) => _db.SetPersonalNotePinned(noteId, isPinned);

    public string? GetNote(int documentId) => _db.GetPersonalNote(documentId);

    public bool SaveNote(int documentId, string content) => _db.SavePersonalNote(documentId, content);

    public bool DeleteNote(int documentId) => _db.DeletePersonalNote(documentId);
}
