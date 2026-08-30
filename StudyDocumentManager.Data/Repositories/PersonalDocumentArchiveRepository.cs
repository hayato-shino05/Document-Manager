using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public sealed class PersonalDocumentArchiveRepository
{
    private readonly IDocumentRepository _documents;
    private readonly IRecycleBinRepository _recycleBin;
    private readonly IPersonalNoteRepository _notes;
    private readonly ICollectionRepository _collections;
    private readonly IRelatedDocumentRepository _relations;
    private readonly IFileIntegrityRepository _fileIntegrity;
    private readonly DatabaseHelper _database;

    public PersonalDocumentArchiveRepository(
        IDocumentRepository documents,
        IRecycleBinRepository recycleBin,
        IPersonalNoteRepository notes,
        ICollectionRepository collections,
        IRelatedDocumentRepository relations,
        IFileIntegrityRepository fileIntegrity,
        DatabaseHelper database)
    {
        _documents = documents;
        _recycleBin = recycleBin;
        _notes = notes;
        _collections = collections;
        _relations = relations;
        _fileIntegrity = fileIntegrity;
        _database = database;
    }

    public IReadOnlyList<StudyDocument> GetExistingDocuments()
        => _documents.GetAll().Concat(_recycleBin.GetDeletedDocuments()).GroupBy(document => document.Id).Select(group => group.First()).ToArray();

    public IReadOnlyDictionary<string, int> ImportGraph(
        DocumentArchiveManifest manifest,
        IReadOnlyList<DocumentArchiveDocument> documents)
        => _database.ImportArchiveGraph(manifest, documents);

    public ArchiveSnapshot ReadSnapshot(IReadOnlyList<int>? documentIds, bool includeDeleted)
    {
        var active = _documents.GetAll();
        var deleted = includeDeleted ? _recycleBin.GetDeletedDocuments() : [];
        var candidates = active.Concat(deleted)
            .GroupBy(document => document.Id)
            .Select(group => group.First())
            .Where(document => documentIds is null || documentIds.Contains(document.Id))
            .OrderBy(document => document.Id)
            .ToArray();

        var documents = candidates
            .Select(document => new ArchiveDocumentSource(document, deleted.Any(item => item.Id == document.Id)))
            .ToArray();
        var selectedIds = documents.Select(item => item.Document.Id).ToHashSet();
        var notes = documents.SelectMany(item => _notes.GetNotes(item.Document.Id, includeDeleted: true)
            .Select(note => new ArchiveNoteSource(note))).ToArray();

        var collections = _collections.GetAll()
            .Select(collection => new ArchiveCollectionSource(
                collection.Name,
                _collections.GetDocuments(collection.Id)
                    .Where(document => selectedIds.Contains(document.Id))
                    .Select(document => document.Id)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray()))
            .Where(collection => collection.DocumentIds.Count > 0)
            .OrderBy(collection => collection.Name, StringComparer.Ordinal)
            .ToArray();

        var relationMap = documents.ToDictionary(item => item.Document.Id);
        var relations = new List<ArchiveRelationSource>();
        foreach (var source in documents)
        {
            foreach (var related in _relations.GetRelated(source.Document.Id))
            {
                if (!selectedIds.Contains(related.Doc.Id) || !relationMap.ContainsKey(related.Doc.Id))
                    continue;
                var pair = source.Document.Id < related.Doc.Id
                    ? (source.Document.Id, related.Doc.Id)
                    : (related.Doc.Id, source.Document.Id);
                if (relations.Any(item => item.SourceId == pair.Item1 && item.TargetId == pair.Item2 && item.RelationType == related.RelationType))
                    continue;
                relations.Add(new ArchiveRelationSource(pair.Item1, pair.Item2, related.RelationType));
            }
        }

        return new ArchiveSnapshot(documents, notes, collections, relations);
    }

    public sealed record ArchiveSnapshot(
        IReadOnlyList<ArchiveDocumentSource> Documents,
        IReadOnlyList<ArchiveNoteSource> Notes,
        IReadOnlyList<ArchiveCollectionSource> Collections,
        IReadOnlyList<ArchiveRelationSource> Relations);

    public sealed record ArchiveDocumentSource(StudyDocument Document, bool IsDeleted);
    public sealed record ArchiveNoteSource(PersonalNote Note);
    public sealed record ArchiveCollectionSource(string Name, IReadOnlyList<int> DocumentIds);
    public sealed record ArchiveRelationSource(int SourceId, int TargetId, string RelationType);
}
