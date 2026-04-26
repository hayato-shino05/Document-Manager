namespace StudyDocumentManager.Models.Items;

/// <summary>
/// Presentation model cho một chip bộ sưu tập trong SelectCollectionDialog.
/// </summary>
public record CollectionChipItem(int Id, string Name, int DocCount)
{
    public string Label => DocCount > 0 ? $"{Name}  ({DocCount})" : Name;
}



public class SelectCollectionDialogState
{
    private readonly Dictionary<int, string> _namesById;

    public SelectCollectionDialogState(IList<(int Id, string Name, int DocCount)> collections)
    {
        _namesById = collections.ToDictionary(item => item.Id, item => item.Name);
        SelectedLabel = "Chưa chọn bộ sưu tập nào.";
    }

    public int SelectedId { get; private set; } = -1;
    public string SelectedLabel { get; private set; }
    public bool CanConfirm { get; private set; }

    public void Select(int collectionId)
    {
        SelectedId = collectionId;
        SelectedLabel = _namesById.TryGetValue(collectionId, out var name)
            ? $"Đã chọn: {name}"
            : "Chưa chọn bộ sưu tập nào.";
        CanConfirm = collectionId >= 0;
    }

    public static string BuildChipLabel(string name, int docCount)
        => docCount > 0 ? $"{name}  ({docCount})" : name;
}
