using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Views;

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

    public async Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "")
    {
        Debug.WriteLine($"[DialogService] ShowInputAsync called: title='{title}', label='{label}', defaultValue='{defaultValue}'");

        string? result = null;
        string liveText = defaultValue;

        var textBox = new TextBox
        {
            Text = defaultValue,
            Watermark = string.IsNullOrEmpty(watermark) ? label : watermark,
            Margin = new Thickness(0, 8, 0, 16)
        };

        // Always keep liveText up-to-date as user types
        textBox.TextChanged += (_, _) =>
        {
            liveText = textBox.Text ?? string.Empty;
            Debug.WriteLine($"[DialogService] TextChanged: liveText='{liveText}'");
        };

        // Keep direct references to buttons — do NOT rely on OfType traversal
        var okButton = new Button
        {
            Content = "OK",
            MinWidth = 80,
            IsDefault = true,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        var cancelButton = new Button
        {
            Content = "Hủy",
            MinWidth = 80,
            IsCancel = true,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        var buttonRowPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0),
            Children = { okButton, cancelButton }
        };

        var contentPanel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(24),
            Children =
            {
                new TextBlock { Text = label, FontWeight = Avalonia.Media.FontWeight.Medium },
                textBox,
                buttonRowPanel
            }
        };

        Window? dialog = null;
        dialog = new Window
        {
            Title = title,
            Content = contentPanel,
            Width = 400,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        // Focus the TextBox once the dialog is fully rendered
        dialog.Opened += (_, _) =>
        {
            textBox.Focus();
            // Move caret to end so user can append to defaultValue
            textBox.CaretIndex = liveText.Length;
        };

        okButton.Click += (_, _) =>
        {
            // Use liveText (updated by TextChanged) — NOT textBox.Text which may be stale
            result = liveText;
            Debug.WriteLine($"[DialogService] OK clicked, liveText='{result}', textBox.Text='{textBox.Text}'");
            dialog?.Close();
        };

        cancelButton.Click += (_, _) =>
        {
            result = null;
            Debug.WriteLine($"[DialogService] Cancel clicked");
            dialog?.Close();
        };

        Debug.WriteLine($"[DialogService] ShowDialog starting...");
        var owner = GetMainWindow();
        Debug.WriteLine($"[DialogService] MainWindow found: {owner != null}");

        if (owner == null)
        {
            Debug.WriteLine("[DialogService] ERROR: MainWindow is null! Cannot show dialog.");
            return null;
        }

        await dialog.ShowDialog(owner);
        Debug.WriteLine($"[DialogService] Dialog closed, result='{result}'");
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

    // ═══ Category Picker ═══

    public async Task<string?> ShowChangeCategoryAsync(string documentName, IList<string> existingCategories, string currentCategory)
    {
        var owner = GetMainWindow();
        if (owner == null) return null;

        var dialog = new ChangeCategoryDialog(documentName, existingCategories, currentCategory);
        await dialog.ShowDialog(owner);
        return dialog.Result;
    }

    // ═══ Document Picker ═══

    public async Task<List<StudyDocument>?> ShowDocumentPickerAsync(
        string collectionName,
        IEnumerable<StudyDocument> allDocuments,
        IEnumerable<int> alreadyInCollection)
    {
        var owner = GetMainWindow();
        if (owner == null) return null;

        var dialog = new AddToCollectionDialog(
            allDocuments,
            alreadyInCollection,
            collectionName);

        await dialog.ShowDialog(owner);
        return dialog.Result;
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
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            IsDefault = true
        });

        if (showCancel)
        {
            panel.Children.Add(new Button
            {
                Content = "Hủy",
                MinWidth = 80,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                IsCancel = true
            });
        }

        return panel;
    }
}
