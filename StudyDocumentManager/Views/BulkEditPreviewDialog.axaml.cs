using Avalonia.Controls;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Views;

public sealed record BulkEditPreviewRow(string FieldLabel, string NewValue);

public partial class BulkEditPreviewDialog : Window
{
    public bool? Result { get; private set; }

    private readonly ILocalizationService? _loc;
    private readonly int _affectedCount;

    public BulkEditPreviewDialog() { } // XAML loader

    public BulkEditPreviewDialog(int affectedCount, IReadOnlyList<(string FieldLabel, string NewValue)> changes, ILocalizationService? loc = null)
    {
        InitializeComponent();

        _loc = loc;
        _affectedCount = affectedCount;
        UpdateAffectedNote();
        if (_loc != null)
        {
            _loc.LanguageChanged += OnLanguageChanged;
            Closed += (_, _) => _loc.LanguageChanged -= OnLanguageChanged;
        }

        this.FindControl<ItemsControl>("ChangesList")!.ItemsSource =
            changes.Select(c => new BulkEditPreviewRow(c.FieldLabel, c.NewValue)).ToList();

        this.FindControl<Button>("ConfirmButton")!.Click += (_, _) => { Result = true; Close(); };
        this.FindControl<Button>("CancelButton")!.Click += (_, _) => { Result = false; Close(); };

        this.Opened += (_, _) => this.FindControl<Button>("CancelButton")?.Focus();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
        => UpdateAffectedNote();

    private void UpdateAffectedNote()
    {
        var note = this.FindControl<TextBlock>("AffectedNote");
        if (note == null)
            return;

        note.Text = _loc == null
            ? $"Documents: {_affectedCount}"
            : string.Format(_loc["BE_PreviewAffected"], _affectedCount);
    }
}
