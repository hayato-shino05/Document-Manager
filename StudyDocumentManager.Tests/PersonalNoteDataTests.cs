using Microsoft.Data.Sqlite;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Repositories;
using Xunit;

namespace StudyDocumentManager.Tests;

public sealed class PersonalNoteDataTests : DatabaseTestBase
{
    private readonly IPersonalNoteRepository _noteRepository;

    public PersonalNoteDataTests()
    {
        _noteRepository = new PersonalNoteRepository(Db);
    }

    [Theory]
    [InlineData("general")]
    [InlineData("summary")]
    [InlineData("action")]
    [InlineData("quote")]
    [InlineData("lecture")]
    [InlineData("meeting")]
    public void NoteType_AcceptsOnlyContractValues(string value)
    {
        Assert.True(NoteType.TryParse(value, out _));
    }

    [Fact]
    public void NoteType_RejectsUnknownValue()
    {
        Assert.False(NoteType.TryParse("unknown", out _));
    }

    [Fact]
    public void Notes_SupportMultipleTypesAndPinnedState()
    {
        var document = AddDocument("notes");

        Assert.True(_noteRepository.SaveNote(new PersonalNote(0, document.Id, "summary", "S", false)));
        Assert.True(_noteRepository.SaveNote(new PersonalNote(0, document.Id, "action", "A", true)));

        var notes = _noteRepository.GetNotes(document.Id);

        Assert.Equal(2, notes.Count);
        Assert.True(notes.Single(note => note.NoteType == "action").IsPinned);
    }

    [Fact]
    public void SaveNote_RejectsUnknownTypeWithoutPersistingRow()
    {
        var document = AddDocument("invalid note type");

        Assert.False(_noteRepository.SaveNote(new PersonalNote(0, document.Id, "unknown", "content", false)));
        Assert.Empty(_noteRepository.GetNotes(document.Id));
    }

    [Fact]
    public void InitializeDatabase_AddsNoteColumnsWithoutForeignKeyViolations()
    {
        Db.CloseAllConnections();
        Db.InitializeDatabase();

        using var connection = new SqliteConnection(Db.ConnectionString);
        connection.Open();

        Assert.Contains("note_type", GetColumns(connection, "personal_notes"));
        Assert.Contains("is_pinned", GetColumns(connection, "personal_notes"));
        Assert.Equal(0L, GetCount(connection, "PRAGMA foreign_key_check"));
    }

    private StudyDocument AddDocument(string name)
    {
        Repo.Add(new StudyDocument { Name = name });
        return Assert.Single(Repo.GetAll());
    }

    private static IReadOnlyList<string> GetColumns(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName})";
        using var reader = command.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read())
            columns.Add(reader.GetString(1));
        return columns;
    }

    private static long GetCount(SqliteConnection connection, string query)
    {
        using var command = connection.CreateCommand();
        command.CommandText = query;
        using var reader = command.ExecuteReader();
        return reader.Read() ? 1L : 0L;
    }
}
