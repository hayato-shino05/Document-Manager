using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace StudyDocumentManager.Services;

/// <summary>
/// Dialog service implementation using Avalonia Window dialogs and StorageProvider.
/// </summary>
public class DialogService : IDialogService
{
    private Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = CreateDialog(title, message, showCancel: false);
        await dialog.ShowDialog(GetMainWindow()!);
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        var dialog = CreateDialog(title, message, showCancel: false);
        await dialog.ShowDialog(GetMainWindow()!);
    }

    public async Task<bool> ShowConfirmAsync(string title, string message)
    {
        var result = false;
        var dialog = CreateDialog(title, message, showCancel: true);

        if (dialog.Content is StackPanel panel)
        {
            var buttonPanel = panel.Children.OfType<StackPanel>().LastOrDefault();
            if (buttonPanel != null)
            {
                var okButton = buttonPanel.Children.OfType<Button>().FirstOrDefault();
                if (okButton != null)
                {
                    okButton.Click += (_, _) => { result = true; dialog.Close(); };
                }
                var cancelButton = buttonPanel.Children.OfType<Button>().LastOrDefault();
                if (cancelButton != null)
                {
                    cancelButton.Click += (_, _) => { result = false; dialog.Close(); };
                }
            }
        }

        await dialog.ShowDialog(GetMainWindow()!);
        return result;
    }

    public async Task<string?> ShowInputAsync(string title, string label, string defaultValue = "")
    {
        string? result = null;
        var textBox = new TextBox
        {
            Text = defaultValue,
            Watermark = label,
            Margin = new Thickness(0, 8, 0, 16)
        };

        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(24),
            Children =
            {
                new TextBlock { Text = label, FontWeight = Avalonia.Media.FontWeight.Medium },
                textBox,
                CreateButtonPanel()
            }
        };

        var dialog = new Window
        {
            Title = title,
            Content = panel,
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var buttonPanel = panel.Children.OfType<StackPanel>().LastOrDefault();
        if (buttonPanel != null)
        {
            var okButton = buttonPanel.Children.OfType<Button>().FirstOrDefault();
            if (okButton != null)
            {
                okButton.Click += (_, _) => { result = textBox.Text; dialog.Close(); };
            }
            var cancelButton = buttonPanel.Children.OfType<Button>().LastOrDefault();
            if (cancelButton != null)
            {
                cancelButton.Click += (_, _) => { result = null; dialog.Close(); };
            }
        }

        await dialog.ShowDialog(GetMainWindow()!);
        return result;
    }

    // ═══ File/Folder picker dialogs ═══

    public async Task<string?> ShowOpenFileAsync(string title, string? filter = null)
    {
        var window = GetMainWindow();
        if (window == null) return null;

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = BuildFileFilter(filter)
        };

        var result = await window.StorageProvider.OpenFilePickerAsync(options);
        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    public async Task<string?> ShowOpenFolderAsync(string title)
    {
        var window = GetMainWindow();
        if (window == null) return null;

        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };

        var result = await window.StorageProvider.OpenFolderPickerAsync(options);
        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    public async Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null)
    {
        var window = GetMainWindow();
        if (window == null) return null;

        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            FileTypeChoices = BuildFileFilter(filter)
        };

        var result = await window.StorageProvider.SaveFilePickerAsync(options);
        return result?.Path.LocalPath;
    }

    // ═══ Helpers ═══

    private static List<FilePickerFileType>? BuildFileFilter(string? filter)
    {
        if (string.IsNullOrEmpty(filter)) return null;

        // Parse simple filter format: "All Files|*.*|CSV|*.csv"
        var parts = filter.Split('|');
        var types = new List<FilePickerFileType>();
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            var patterns = parts[i + 1].Split(';')
                .Select(p => p.Trim())
                .ToList();
            types.Add(new FilePickerFileType(parts[i]) { Patterns = patterns });
        }
        return types.Count > 0 ? types : null;
    }

    private static Window CreateDialog(string title, string message, bool showCancel)
    {
        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(24),
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    MaxWidth = 350
                },
                CreateButtonPanel(showCancel)
            }
        };

        var dialog = new Window
        {
            Title = title,
            Content = panel,
            Width = 400,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        if (!showCancel)
        {
            var buttonPanel = panel.Children.OfType<StackPanel>().LastOrDefault();
            var okButton = buttonPanel?.Children.OfType<Button>().FirstOrDefault();
            if (okButton != null)
            {
                okButton.Click += (_, _) => dialog.Close();
            }
        }

        return dialog;
    }

    private static StackPanel CreateButtonPanel(bool showCancel = true)
    {
        var panel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        panel.Children.Add(new Button
        {
            Content = "OK",
            MinWidth = 80,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
        });

        if (showCancel)
        {
            panel.Children.Add(new Button
            {
                Content = "Hủy",
                MinWidth = 80,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
            });
        }

        return panel;
    }
}
