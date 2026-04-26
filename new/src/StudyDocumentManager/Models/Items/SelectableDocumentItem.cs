using System.ComponentModel;
using System.Runtime.CompilerServices;
using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Models.Items;

public class SelectableDocumentItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public SelectableDocumentItem(StudyDocument document)
    {
        Document = document;
    }

    public StudyDocument Document { get; }

    public bool HasAuthor => !string.IsNullOrWhiteSpace(Document.TacGia);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            OnPropertyChanged();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool MatchesSearch(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return true;

        term = term.Trim();
        return Document.Ten.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (Document.MonHoc?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (Document.Loai?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (Document.TacGia?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (Document.Tags?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    public event EventHandler? SelectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
