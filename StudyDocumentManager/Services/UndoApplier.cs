using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public sealed class UndoApplier : IUndoApplier
{
    private readonly IUndoService _undo;
    private readonly IDocumentRepository _documents;
    private readonly IRecycleBinRepository _recycleBin;
    private readonly ICollectionRepository _collections;

    public UndoApplier(IUndoService undo, IDocumentRepository documents, IRecycleBinRepository recycleBin, ICollectionRepository collections)
    {
        _undo = undo;
        _documents = documents;
        _recycleBin = recycleBin;
        _collections = collections;
    }

    public bool CanUndo => _undo.CanUndo;

    public void ApplyLast()
    {
        var entry = _undo.Pop() ?? throw new InvalidOperationException("Nothing to undo.");

        if (entry.DeletedIds.Count > 0)
        {
            _recycleBin.RestoreDocuments(entry.DeletedIds);
        }
        else if (entry.Collection is { } snapshot)
        {
            var collectionId = _collections.Create(snapshot.Name, snapshot.Description);
            foreach (var documentId in snapshot.MemberDocumentIds)
                _collections.AddDocument(collectionId, documentId);
        }
        else
        {
            foreach (var original in entry.Originals)
                _documents.Update(original);
        }
    }
}
