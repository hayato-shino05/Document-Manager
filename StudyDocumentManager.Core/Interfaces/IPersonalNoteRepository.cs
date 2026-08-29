using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

public interface IPersonalNoteRepository
{
    IReadOnlyList<PersonalNote> GetNotes(int documentId, bool includeDeleted = false)
    {
        var content = GetNote(documentId);
        return content is null
            ? []
            : [new PersonalNote(0, documentId, "general", content, false)];
    }

    bool SaveNote(PersonalNote note)
        => NoteType.TryParse(note.NoteType, out _) && SaveNote(note.DocumentId, note.Content);

    bool DeleteNoteById(int noteId) => false;

    bool SetPinned(int noteId, bool isPinned) => false;

    string? GetNote(int documentId);
    bool SaveNote(int documentId, string content);
    bool DeleteNote(int documentId);
}
