using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace StudyDocumentManager.Services;

public class ClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = desktop.MainWindow ?? (desktop.Windows.Count > 0 ? desktop.Windows[0] : null);
                var clipboard = window?.Clipboard ?? (window != null ? Avalonia.Controls.TopLevel.GetTopLevel(window)?.Clipboard : null);
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(text ?? string.Empty);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClipboardService] Failed to set clipboard text: {ex.Message}");
        }
    }
}
