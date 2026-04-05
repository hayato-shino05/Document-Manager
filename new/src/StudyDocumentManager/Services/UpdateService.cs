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

        var message = $"Phiên bản mới {update.NewVersion} đã sẵn sàng!\n\n";

        if (!string.IsNullOrEmpty(update.ReleaseNotes))
        {
            // Trim release notes to first 200 chars
            var notes = update.ReleaseNotes.Length > 200
                ? update.ReleaseNotes[..200] + "..."
                : update.ReleaseNotes;
            message += $"Release Notes:\n{notes}\n\n";
        }

        message += "Bạn có muốn mở trang tải về không?";

        var confirmed = await dialogService.ShowConfirmAsync("Cập nhật có sẵn", message);
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
            await dialogService.ShowErrorAsync("Lỗi",
                $"Không thể mở trình duyệt.\nVui lòng truy cập: {url}");
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
                $"Phiên bản mới {info.NewVersion} có sẵn! Vào Trợ giúp → Kiểm tra cập nhật.",
                ToastService.ToastType.Info,
                5000);
        }
    }
}
