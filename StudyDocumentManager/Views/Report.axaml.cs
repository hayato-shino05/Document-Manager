using Avalonia.Controls;
using StudyDocumentManager.Models;

namespace StudyDocumentManager.Views;

public partial class Report : UserControl
{
    private ReportModel? _model;

    public Report()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _model?.DetachLocalization();

        _model = DataContext as ReportModel;
        if (VisualRoot is not null)
            _model?.AttachLocalization();
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
        => _model?.AttachLocalization();

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
        => _model?.DetachLocalization();
}
