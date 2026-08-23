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
        var entry = _undo.Peek() ?? throw new InvalidOperationException("Nothing to undo.");

        if (entry.DeletedIds.Count > 0)
        {
            var restored = _recycleBin.RestoreDocuments(entry.DeletedIds);
            if (restored != entry.DeletedIds.Count)
                throw new InvalidOperationException("Undo restore did not restore every document.");
        }
        else if (entry.Collection is { } snapshot)
        {
            var collectionId = _collections.Create(snapshot.Name, snapshot.Description);
            if (collectionId <= 0)
                throw new InvalidOperationException("Undo collection recreation failed.");

            try
            {
                foreach (var documentId in snapshot.MemberDocumentIds)
                {
                    if (!_collections.AddDocument(collectionId, documentId))
                        throw new InvalidOperationException("Undo collection membership restoration failed.");
                }
            }
            catch
            {
                _collections.Delete(collectionId);
                throw;
            }
        }
        else
        {
            var currentDocuments = entry.Originals
                .Select(original => (Original: original, Current: _documents.GetById(original.Id)))
                .ToList();
            var updatedDocuments = new List<StudyDocument>();
            var removedMemberships = new List<CollectionMembership>();

            try
            {
                foreach (var item in currentDocuments)
                {
                    if (!_documents.Update(item.Original))
                        throw new InvalidOperationException("Undo document restoration failed.");
                    updatedDocuments.Add(item.Original);
                }

                foreach (var membership in entry.AddedCollectionMemberships)
                {
                    if (!_collections.RemoveDocument(membership.CollectionId, membership.DocumentId))
                        throw new InvalidOperationException("Undo collection membership removal failed.");
                    removedMemberships.Add(membership);
                }
            }
            catch
            {
                foreach (var item in currentDocuments.Where(item => item.Current != null && updatedDocuments.Contains(item.Original)))
                    _documents.Update(item.Current!);

                foreach (var membership in removedMemberships)
                    _collections.AddDocument(membership.CollectionId, membership.DocumentId);
                throw;
            }
        }

        _undo.Pop();
    }
}
