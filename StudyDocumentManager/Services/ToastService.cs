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

        var (accentBrush, badgeBg, iconImage, titleText) = ResolveVisuals(type);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("4,Auto,*")
        };

        // Left accent strip
        var accentStrip = new Border
        {
            Background = accentBrush,
            CornerRadius = new CornerRadius(8, 0, 0, 8),
            Width = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Grid.SetColumn(accentStrip, 0);
        grid.Children.Add(accentStrip);

        // Icon badge
        var iconElement = new Image
        {
            Width = 14,
            Height = 14,
            Source = iconImage,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var iconBadge = new Border
        {
            Background = badgeBg,
            CornerRadius = new CornerRadius(14),
            Width = 28,
            Height = 28,
            Margin = new Thickness(10, 10, 8, 10),
            VerticalAlignment = VerticalAlignment.Center,
            Child = iconElement
        };
        Grid.SetColumn(iconBadge, 1);
        grid.Children.Add(iconBadge);

        // Text details (Title + Message)
        var titleBlock = new TextBlock
        {
            Text = titleText,
            Foreground = GetBrush("TextPrimary", Brushes.Black),
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };

        var messageBlock = new TextBlock
        {
            Text = message,
            Foreground = GetBrush("TextSecondary", Brushes.DarkSlateGray),
            FontSize = 12,
            FontWeight = FontWeight.Normal,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 280,
            VerticalAlignment = VerticalAlignment.Center
        };

        var textPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 8, 14, 8),
            Spacing = 2,
            Children = { titleBlock, messageBlock }
        };
        Grid.SetColumn(textPanel, 2);
        grid.Children.Add(textPanel);

        var toast = new Border
        {
            Background = GetBrush("CardBackground", Brushes.White),
            BorderBrush = GetBrush("CardBorder", new SolidColorBrush(Color.Parse("#E5E7EB"))),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Right,
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 4,
                Blur = 18,
                Spread = -2,
                Color = Color.FromArgb(35, 0, 0, 0)
            }),
            MinWidth = 280,
            MaxWidth = 380,
            Opacity = 0,
            RenderTransform = new TranslateTransform(24, 0),
            Child = grid
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

    private static IImage? GetImage(string key)
    {
        if (Application.Current?.TryGetResource(key, Avalonia.Styling.ThemeVariant.Default, out var resource) == true
            && resource is IImage img)
        {
            return img;
        }

        return null;
    }

    private static string GetLocalized(string key, string fallback)
    {
        try
        {
            if (Application.Current?.TryGetResource("Loc", Avalonia.Styling.ThemeVariant.Default, out var resource) == true
                && resource is ILocalizationService loc)
            {
                return loc[key];
            }
        }
        catch
        {
            // fallback if not available
        }
        return fallback;
    }

    private static (IBrush accent, IBrush badgeBg, IImage? icon, string title) ResolveVisuals(ToastType type)
    {
        return type switch
        {
            ToastType.Success => (
                GetBrush("SuccessBrush", Brushes.ForestGreen),
                GetBrush("ToastSuccessBrush", Brushes.LightGreen),
                GetImage("IconCheckSuccess") ?? GetImage("IconCheck"),
                GetLocalized("Toast_TitleSuccess", "Success")),
            ToastType.Error => (
                GetBrush("DangerBrush", Brushes.Crimson),
                GetBrush("ToastErrorBrush", Brushes.LightPink),
                GetImage("IconClose") ?? GetImage("IconDeleteWhite"),
                GetLocalized("Toast_TitleError", "Error")),
            ToastType.Warning => (
                GetBrush("WarningBrush", Brushes.DarkOrange),
                GetBrush("ToastWarningBrush", Brushes.LightYellow),
                GetImage("IconWarning"),
                GetLocalized("Toast_TitleWarning", "Warning")),
            _ => (
                GetBrush("AccentBrush", Brushes.RoyalBlue),
                GetBrush("ToastInfoBrush", Brushes.LightBlue),
                GetImage("IconInfo"),
                GetLocalized("Toast_TitleInfo", "Info")),
        };
    }

    private sealed record ToastEntry(string Message, ToastType Type, int DurationMs);
}
