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

    public bool HasAuthor => !string.IsNullOrWhiteSpace(Document.Author);

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
        return Document.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (Document.Subject?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (Document.Type?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (Document.Author?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (Document.Tags?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    public event EventHandler? SelectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
