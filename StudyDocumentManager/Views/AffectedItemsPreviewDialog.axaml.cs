using Avalonia.Controls;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Views;

public partial class AffectedItemsPreviewDialog : Window
{
    public bool? Result { get; private set; }

    private readonly ILocalizationService? _loc;
    private readonly int _totalCount;

    public AffectedItemsPreviewDialog() { } // XAML loader

    public AffectedItemsPreviewDialog(string title, int totalCount, IReadOnlyList<string> itemNames, string reversibilityNote, ILocalizationService? loc = null)
    {
        InitializeComponent();

        _loc = loc;
        _totalCount = totalCount;
        Title = title;
        this.FindControl<TextBlock>("TitleText")!.Text = title;
        this.FindControl<TextBlock>("ReversibilityNote")!.Text = reversibilityNote;
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
        UpdateAffectedNote();
        var confirmButton = this.FindControl<Button>("ConfirmButton");
        if (confirmButton != null && _loc != null)
            confirmButton.Content = _loc["Action_Delete"];
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
