using StudyDocumentManager.Core.DTOs;
using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Services;

public interface ICustomDialogService
{
    Task<string?> ShowChangeCategoryAsync(string documentName, IList<string> existingCategories, string currentCategory);

    Task<int> ShowSelectCollectionAsync(string documentName, IList<(int Id, string Name, int DocCount)> collections);

    Task<List<StudyDocument>?> ShowDocumentPickerAsync(
        string collectionName,
        IEnumerable<StudyDocument> allDocuments,
        IEnumerable<int> alreadyInCollection);

    Task<AddDocumentDraft?> ShowAddDocumentAsync(string filePath, IList<string> subjects, IList<string> types);

    Task<bool> ShowBulkEditPreviewAsync(int affectedCount, IReadOnlyList<(string FieldLabel, string NewValue)> changes)
        => throw new NotSupportedException($"{nameof(ShowBulkEditPreviewAsync)} is not implemented by this dialog service.");

    Task<bool> ShowAffectedItemsPreviewAsync(string title, int totalCount, IReadOnlyList<string> itemNames, string reversibilityNote)
        => throw new NotSupportedException($"{nameof(ShowAffectedItemsPreviewAsync)} is not implemented by this dialog service.");

    Task<bool> ShowAffectedItemsPreviewAsync(int totalCount, IReadOnlyList<string> itemNames, PreviewTextSource title, PreviewTextSource reversibilityNote)
        => throw new NotSupportedException($"{nameof(ShowAffectedItemsPreviewAsync)} is not implemented by this dialog service.");
}

public sealed record PreviewTextSource(string KeyOrText, IReadOnlyList<string> FormatArgs)
{
    public static PreviewTextSource Key(string key, params string[] args) => new(key, args);
}
