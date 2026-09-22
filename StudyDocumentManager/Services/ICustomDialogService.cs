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

    Task<int?> ShowDuplicateMergeReviewAsync(string groupName, string matchReason, IReadOnlyList<StudyDocument> candidates)
        => throw new NotSupportedException($"{nameof(ShowDuplicateMergeReviewAsync)} is not implemented by this dialog service.");
}

public enum PreviewTextKind
{
    Key,
    Text
}

public sealed record PreviewTextSource(PreviewTextKind Kind, string KeyOrText, IReadOnlyList<string> FormatArgs)
{
    public Func<IReadOnlyList<string>>? FormatArgsFactory { get; init; }

    public static PreviewTextSource Key(string key, params string[] args) => new(PreviewTextKind.Key, key, args);

    public static PreviewTextSource Key(string key, Func<IReadOnlyList<string>> argsFactory)
        => new(PreviewTextKind.Key, key, []) { FormatArgsFactory = argsFactory };

    public static PreviewTextSource Text(string text, params string[] args) => new(PreviewTextKind.Text, text, args);
}
