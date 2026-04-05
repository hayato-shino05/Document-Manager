using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace StudyDocumentManager.Services;

/// <summary>
/// Non-blocking toast notification system for Avalonia.
/// Shows a temporary message overlay at the top-right of the main window.
/// </summary>
public static class ToastService
{
    public enum ToastType { Success, Error, Warning, Info }

    public static void Show(string message, ToastType type = ToastType.Info, int durationMs = 3000)
    {
        Dispatcher.UIThread.Post(() => ShowInternal(message, type, durationMs));
    }

    private static void ShowInternal(string message, ToastType type, int durationMs)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var window = desktop.MainWindow;
        if (window == null) return;

        var (bg, fg) = type switch
        {
            ToastType.Success => (Color.Parse("#10B981"), Colors.White),
            ToastType.Error => (Color.Parse("#EF4444"), Colors.White),
            ToastType.Warning => (Color.Parse("#F59E0B"), Colors.White),
            _ => (Color.Parse("#3B82F6"), Colors.White),
        };

        var icon = type switch
        {
            ToastType.Success => "✓",
            ToastType.Error => "✕",
            ToastType.Warning => "⚠",
            _ => "ℹ",
        };

        var border = new Border
        {
            Background = new SolidColorBrush(bg),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 10),
            Margin = new Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0, OffsetY = 4, Blur = 12,
                Color = Color.FromArgb(80, 0, 0, 0)
            }),
            Opacity = 0.95,
            MaxWidth = 400,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = icon,
                        Foreground = new SolidColorBrush(fg),
                        FontSize = 16,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = message,
                        Foreground = new SolidColorBrush(fg),
                        FontSize = 13,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 320,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };

        // Add to window's overlay layer via AdornerLayer or Panel
        if (window.Content is Panel panel)
        {
            panel.Children.Add(border);

            // Auto-remove after duration
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
            timer.Tick += (_, _) =>
            {
                panel.Children.Remove(border);
                timer.Stop();
            };
            timer.Start();
        }
    }
}
