using Avalonia.Controls;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Services;

namespace StudyDocumentManager.Views;

public partial class AffectedItemsPreviewDialog : Window
{
    public bool? Result { get; private set; }

    private readonly ILocalizationService? _loc;
    private readonly int _totalCount;
    private readonly string _titleSource = string.Empty;
    private readonly string _noteSource = string.Empty;
    private readonly PreviewTextSource? _titleTextSource;
    private readonly PreviewTextSource? _noteTextSource;

    public AffectedItemsPreviewDialog() { } // XAML loader

    public AffectedItemsPreviewDialog(string title, int totalCount, IReadOnlyList<string> itemNames, string reversibilityNote, ILocalizationService? loc = null,
        PreviewTextSource? titleSource = null, PreviewTextSource? noteSource = null)
    {
        InitializeComponent();

        _loc = loc;
        _totalCount = totalCount;
        _titleSource = title;
        _noteSource = reversibilityNote;
        _titleTextSource = titleSource;
        _noteTextSource = noteSource;

        ApplyComposedTexts();
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
        ApplyComposedTexts();
        UpdateAffectedNote();
        var confirmButton = this.FindControl<Button>("ConfirmButton");
        if (confirmButton != null && _loc != null)
            confirmButton.Content = _loc["Action_Delete"];
    }

    private void ApplyComposedTexts()
    {
        Title = ResolveTitle();
        var titleText = this.FindControl<TextBlock>("TitleText");
        if (titleText != null)
            titleText.Text = Title;
        var reversibilityNote = this.FindControl<TextBlock>("ReversibilityNote");
        if (reversibilityNote != null)
            reversibilityNote.Text = ResolveNote();
    }

    private string ResolveTitle() => _titleTextSource != null ? Format(_titleTextSource) : Resolve(_titleSource);

    private string ResolveNote() => _noteTextSource != null ? Format(_noteTextSource) : Resolve(_noteSource);

    private string Format(PreviewTextSource source)
    {
        if (source.Kind == PreviewTextKind.Text)
            return string.Format(source.KeyOrText, source.FormatArgs.ToArray());

        var format = _loc == null ? source.KeyOrText : _loc[source.KeyOrText];
        var args = source.FormatArgsFactory?.Invoke() ?? source.FormatArgs;
        return string.Format(format, args.ToArray());
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
