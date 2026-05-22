using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Models.Items;

/// <summary>
/// Presentation model for collection chips in SelectCollectionDialog
/// </summary>
public record CollectionChipItem(int Id, string Name, int DocCount)
{
    public string Label => DocCount > 0 ? $"{Name}  ({DocCount})" : Name;
}

public class SelectCollectionDialogState
{
    private readonly Dictionary<int, string> _namesById;
    private readonly ILocalizationService _loc;

    public SelectCollectionDialogState(IList<(int Id, string Name, int DocCount)> collections, ILocalizationService loc)
    {
        _namesById = collections.ToDictionary(item => item.Id, item => item.Name);
        _loc = loc;
        SelectedLabel = _loc["SelectCollection_None"];
    }

    public int SelectedId { get; private set; } = -1;
    public string SelectedLabel { get; private set; }
    public bool CanConfirm { get; private set; }

    public void Select(int collectionId)
    {
        SelectedId = collectionId;
        SelectedLabel = _namesById.TryGetValue(collectionId, out var name)
            ? string.Format(_loc["SelectCollection_Selected"], name)
            : _loc["SelectCollection_None"];
        CanConfirm = collectionId >= 0;
    }

    public static string BuildChipLabel(string name, int docCount)
        => docCount > 0 ? $"{name}  ({docCount})" : name;
}
