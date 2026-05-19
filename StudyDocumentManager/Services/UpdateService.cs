using StudyDocumentManager.Core.Interfaces;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace StudyDocumentManager.Services;

/// <summary>
/// Handles update download and installation for Avalonia app.
/// Unlike WinForms version, this opens the browser to download page
/// instead of downloading Setup.exe directly (cross-platform friendly).
/// </summary>
public static class UpdateService
{
    /// <summary>
    /// Show update notification and offer to open release page.
    /// </summary>
    public static async Task HandleUpdateAsync(UpdateInfo update, IDialogService dialogService)
    {
        if (update == null || !update.HasUpdate) return;

        var message = $"PhiÃªn báº£n má»›i {update.NewVersion} Ä‘Ã£ sáºµn sÃ ng!\n\n";

        if (!string.IsNullOrEmpty(update.ReleaseNotes))
        {
            // Trim release notes to first 200 chars
            var notes = update.ReleaseNotes.Length > 200
                ? update.ReleaseNotes[..200] + "..."
                : update.ReleaseNotes;
            message += $"Release Notes:\n{notes}\n\n";
        }

        message += "Báº¡n cÃ³ muá»‘n má»Ÿ trang táº£i vá» khÃ´ng?";

        var confirmed = await dialogService.ShowConfirmAsync("Cáº­p nháº­t cÃ³ sáºµn", message);
        if (!confirmed) return;

        // Open release page in browser
        var url = !string.IsNullOrEmpty(update.ReleasePageUrl)
            ? update.ReleasePageUrl
            : $"https://github.com/hayato-shino05/study-document-manager/releases/latest";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            await dialogService.ShowErrorAsync("Lá»—i",
                $"KhÃ´ng thá»ƒ má»Ÿ trÃ¬nh duyá»‡t.\nVui lÃ²ng truy cáº­p: {url}");
        }
    }

    /// <summary>
    /// Check for updates silently and show toast if available.
    /// </summary>
    public static async Task CheckSilentlyAsync(IDialogService dialogService)
    {
        var info = await UpdateChecker.CheckForUpdateAsync();
        if (info is { HasUpdate: true })
        {
            ToastService.Show(
                $"PhiÃªn báº£n má»›i {info.NewVersion} cÃ³ sáºµn! VÃ o Trá»£ giÃºp â†’ Kiá»ƒm tra cáº­p nháº­t.",
                ToastService.ToastType.Info,
                5000);
        }
    }
}
