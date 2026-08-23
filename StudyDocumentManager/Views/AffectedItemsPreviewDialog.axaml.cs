using Avalonia.Controls;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Views;

public partial class AffectedItemsPreviewDialog : Window
{
    public bool? Result { get; private set; }

    private readonly ILocalizationService? _loc;
    private readonly int _totalCount;
    private readonly string _titleSource = string.Empty;
    private readonly string _noteSource = string.Empty;

    public AffectedItemsPreviewDialog() { } // XAML loader

    public AffectedItemsPreviewDialog(string title, int totalCount, IReadOnlyList<string> itemNames, string reversibilityNote, ILocalizationService? loc = null)
    {
        InitializeComponent();

        _loc = loc;
        _totalCount = totalCount;
        _titleSource = title;
        _noteSource = reversibilityNote;
        Title = Resolve(_titleSource);
        this.FindControl<TextBlock>("TitleText")!.Text = Title;
        this.FindControl<TextBlock>("ReversibilityNote")!.Text = Resolve(_noteSource);
        UpdateAffectedNote();
        if (_loc != null)
        {
            _loc.LanguageChanged += OnLanguageChanged;
            Closed += (_, _) => _loc.LanguageChanged -= OnLanguageChanged;
        }

        this.FindControl<ListBox>("ItemsList")!.ItemsSource = itemNames.ToList();

        var confirmButton = this.FindControl<Button>("ConfirmButton")!;
        confirmButton.Content = _loc == null ? "OK" : _loc["Action_Delete"];
        confirmButton.Click += (_, _) => { Result = true; Close(); };
        this.FindControl<Button>("CancelButton")!.Click += (_, _) => { Result = false; Close(); };
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Title = Resolve(_titleSource);
        var titleText = this.FindControl<TextBlock>("TitleText");
        if (titleText != null)
            titleText.Text = Title;
        var reversibilityNote = this.FindControl<TextBlock>("ReversibilityNote");
        if (reversibilityNote != null)
            reversibilityNote.Text = Resolve(_noteSource);
        UpdateAffectedNote();
        var confirmButton = this.FindControl<Button>("ConfirmButton");
        if (confirmButton != null && _loc != null)
            confirmButton.Content = _loc["Action_Delete"];
    }

    private string Resolve(string source)
    {
        if (_loc == null)
            return source;
        return _loc[source] == $"[{source}]" ? source : _loc[source];
    }

    private void UpdateAffectedNote()
    {
        var note = this.FindControl<TextBlock>("AffectedNote");
        if (note == null)
            return;

        note.Text = _loc == null
            ? $"Documents: {_totalCount}"
            : string.Format(_loc["BE_PreviewAffected"], _totalCount);
    }
}
