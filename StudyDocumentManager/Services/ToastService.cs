using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public sealed class ToastService : IToastService
{
    private Panel? _container;
    private readonly Queue<ToastEntry> _queue = new();
    private const int MaxVisible = 3;
    private int _visibleCount;

    public void Show(string message, ToastType type = ToastType.Info, int durationMs = 3000)
    {
        Dispatcher.UIThread.Post(() => ShowInternal(message, type, durationMs));
    }

    private void ShowInternal(string message, ToastType type, int durationMs)
    {
        EnsureContainer();
        if (_container == null) return;

        if (_visibleCount >= MaxVisible)
        {
            _queue.Enqueue(new ToastEntry(message, type, durationMs));
            return;
        }

        Present(message, type, durationMs);
    }

    private void Present(string message, ToastType type, int durationMs)
    {
        if (_container == null) return;
        _visibleCount++;

        var (bg, fg, icon) = ResolveVisuals(type);

        var iconBlock = new TextBlock
        {
            Text = icon,
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = fg,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 20,
            TextAlignment = TextAlignment.Center
        };

        var messageBlock = new TextBlock
        {
            Text = message,
            Foreground = fg,
            FontSize = 12.5,
            FontWeight = FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300,
            VerticalAlignment = VerticalAlignment.Center
        };

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children = { iconBlock, messageBlock }
        };

        var toast = new Border
        {
            Background = bg,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 10, 16, 10),
            Margin = new Thickness(0, 0, 0, 6),
            HorizontalAlignment = HorizontalAlignment.Right,
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 2,
                Blur = 16,
                Spread = -2,
                Color = Color.FromArgb(40, 0, 0, 0)
            }),
            MaxWidth = 380,
            Opacity = 0,
            RenderTransform = new TranslateTransform(24, 0),
            Child = content
        };

        _container.Children.Add(toast);
        AnimateIn(toast, durationMs);
    }

    private async void AnimateIn(Border toast, int durationMs)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var slideIn = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(280),
                Easing = new CubicEaseOut(),
                Children =
                {
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, 0.0), new Setter(TranslateTransform.XProperty, 24.0) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 1.0), new Setter(TranslateTransform.XProperty, 0.0) } }
                }
            };

            await slideIn.RunAsync(toast);

            await Task.Delay(durationMs);

            var fadeOut = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(220),
                Easing = new CubicEaseIn(),
                Children =
                {
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, 1.0), new Setter(TranslateTransform.XProperty, 0.0) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 0.0), new Setter(TranslateTransform.XProperty, 16.0) } }
                }
            };

            await fadeOut.RunAsync(toast);

            _container?.Children.Remove(toast);
            _visibleCount--;

            if (_queue.Count > 0)
            {
                var next = _queue.Dequeue();
                Present(next.Message, next.Type, next.DurationMs);
            }
        });
    }

    private void EnsureContainer()
    {
        if (_container != null) return;

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var window = desktop.MainWindow;
        if (window?.Content is not Panel rootPanel) return;

        _container = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 48, 16, 0),
            Spacing = 0,
            IsHitTestVisible = false
        };

        rootPanel.Children.Add(_container);
    }

    private static IBrush GetBrush(string key, IBrush fallback)
    {
        if (Application.Current?.TryGetResource(key, Avalonia.Styling.ThemeVariant.Default, out var resource) == true
            && resource is IBrush brush)
        {
            return brush;
        }

        return fallback;
    }

    private static (IBrush bg, IBrush fg, string icon) ResolveVisuals(ToastType type)
    {
        return type switch
        {
            ToastType.Success => (
                GetBrush("ToastSuccessBrush", Brushes.Transparent),
                GetBrush("ToastSuccessForegroundBrush", Brushes.Black),
                "✓"),
            ToastType.Error => (
                GetBrush("ToastErrorBrush", Brushes.Transparent),
                GetBrush("ToastErrorForegroundBrush", Brushes.Black),
                "✕"),
            ToastType.Warning => (
                GetBrush("ToastWarningBrush", Brushes.Transparent),
                GetBrush("ToastWarningForegroundBrush", Brushes.Black),
                "⚠"),
            _ => (
                GetBrush("ToastInfoBrush", Brushes.Transparent),
                GetBrush("ToastInfoForegroundBrush", Brushes.Black),
                "ℹ"),
        };
    }

    private sealed record ToastEntry(string Message, ToastType Type, int DurationMs);
}
