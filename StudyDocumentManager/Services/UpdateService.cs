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
    public static async Task HandleUpdateAsync(UpdateInfo update, IDialogService dialogService, ILocalizationService loc)
    {
        if (update == null || !update.HasUpdate) return;

        var message = string.Format(loc["Update_NewVersionReady"], update.NewVersion) + "\n\n";

        if (!string.IsNullOrEmpty(update.ReleaseNotes))
        {
            var notes = update.ReleaseNotes.Length > 200
                ? update.ReleaseNotes[..200] + "..."
                : update.ReleaseNotes;
            message += $"Release Notes:\n{notes}\n\n";
        }

        message += loc["Update_OpenDownloadPage"];

        var confirmed = await dialogService.ShowConfirmAsync(loc["Update_DialogTitle"], message);
        if (!confirmed) return;

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
            await dialogService.ShowErrorAsync(loc["Update_ErrorTitle"],
                string.Format(loc["Update_BrowserError"], url));
        }
    }

    /// <summary>
    /// Check for updates silently and show toast if available.
    /// </summary>
    public static async Task CheckSilentlyAsync(IDialogService dialogService, ILocalizationService loc)
    {
        var info = await UpdateChecker.CheckForUpdateAsync();
        if (info is { HasUpdate: true })
        {
            ToastService.Show(
                string.Format(loc["Update_ToastNewVersion"], info.NewVersion),
                ToastService.ToastType.Info,
                5000);
        }
    }
}
