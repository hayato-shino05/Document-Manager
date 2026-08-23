using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public sealed class UndoApplier : IUndoApplier
{
    private readonly IUndoService _undo;
    private readonly IDocumentRepository _documents;
    private readonly IRecycleBinRepository _recycleBin;
    private readonly ICollectionRepository _collections;
    private readonly IUndoRepository? _undoRepository;

    public UndoApplier(IUndoService undo, IDocumentRepository documents, IRecycleBinRepository recycleBin, ICollectionRepository collections, IUndoRepository? undoRepository = null)
    {
        _undo = undo;
        _documents = documents;
        _recycleBin = recycleBin;
        _collections = collections;
        _undoRepository = undoRepository;
    }

    public bool CanUndo => _undo.CanUndo;

    public void ApplyLast()
    {
        var entry = _undo.Peek() ?? throw new InvalidOperationException("Nothing to undo.");

        if (entry.DeletedIds.Count > 0)
        {
            _recycleBin.RestoreDocuments(entry.DeletedIds);
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
            if (_undoRepository != null)
            {
                _undoRepository.ApplyMetadataUndo(
                    entry.Originals,
                    entry.AddedCollectionMemberships.Select(membership => (membership.CollectionId, membership.DocumentId)).ToList());
                _undo.Pop();
                return;
            }

            var currentDocuments = entry.Originals
                .Select(original => (Original: original, Current: _documents.GetById(original.Id)))
                .ToList();
            var updatedDocuments = new List<StudyDocument>();
            var removedMemberships = new List<CollectionMembership>();

            try
            {
                foreach (var item in currentDocuments)
                {
                    if (item.Current == null)
                        continue;
                    if (!_documents.Update(item.Original))
                        throw new InvalidOperationException("Undo document restoration failed.");
                    updatedDocuments.Add(item.Original);
                }

                foreach (var membership in entry.AddedCollectionMemberships)
                {
                    if (_collections.RemoveDocument(membership.CollectionId, membership.DocumentId))
                        removedMemberships.Add(membership);
                }
            }
            catch (Exception undoFailure)
            {
                Exception? compensationFailure = null;

                foreach (var item in currentDocuments.Where(item => item.Current != null && updatedDocuments.Contains(item.Original)))
                {
                    try
                    {
                        if (!_documents.Update(item.Current!))
                            compensationFailure ??= new InvalidOperationException("Undo document compensation failed.");
                    }
                    catch (Exception exception)
                    {
                        compensationFailure ??= exception;
                    }
                }

                foreach (var membership in removedMemberships)
                {
                    try
                    {
                        if (!_collections.AddDocument(membership.CollectionId, membership.DocumentId))
                            compensationFailure ??= new InvalidOperationException("Undo collection membership compensation failed.");
                    }
                    catch (Exception exception)
                    {
                        compensationFailure ??= exception;
                    }
                }

                if (compensationFailure != null)
                    throw new InvalidOperationException("Undo compensation did not complete.", new AggregateException(undoFailure, compensationFailure));

                throw;
            }
        }

        _undo.Pop();
    }
}
