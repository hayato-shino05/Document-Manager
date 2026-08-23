using Avalonia.Controls;
using StudyDocumentManager.Models;

namespace StudyDocumentManager.Views;

public partial class SmartViews : UserControl
{
    private SmartViewsModel? _model;

    public SmartViews()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _model?.DetachLocalization();

        _model = DataContext as SmartViewsModel;
        if (VisualRoot is not null)
            _model?.AttachLocalization();
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
        => _model?.AttachLocalization();

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
        => _model?.DetachLocalization();
}
