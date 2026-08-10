using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.DesktopSmokeTests;

public sealed class DesktopAppFixture : IDisposable
{
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(10);
    private readonly string _sourcePublishFolder;
    private readonly int _processId = -1;
    private bool _disposed;
    private UIA3Automation? _automation;

    public DesktopAppFixture()
    {
        _sourcePublishFolder = ResolvePublishFolder();
        PublishFolder = CreateIsolatedPublishFolder(_sourcePublishFolder);

        try
        {
            SeedDatabase(PublishFolder);
            App = Application.Launch(Path.Combine(PublishFolder, "DocumentManager.exe"));
            _processId = App.ProcessId;
            _automation = new UIA3Automation();
            Window = WaitUntil(
                () => App.GetMainWindow(_automation!)!,
                window => window is not null && window.IsAvailable,
                LaunchTimeout,
                "メインウィンドウ");
            MainWindow = new MainWindowPage(Window);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public Application App { get; private set; } = null!;

    public Window Window { get; private set; } = null!;

    public MainWindowPage MainWindow { get; private set; } = null!;

    public string PublishFolder { get; }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            RequestProcessClose();
            try
            {
                WaitUntil(
                    () => HasExited(_processId),
                    exited => exited,
                    CleanupTimeout,
                    "アプリケーション終了");
            }
            catch (TimeoutException)
            {
                KillCapturedProcessIfNeeded();
            }

            if (!HasExited(_processId))
                throw new TimeoutException("アプリケーションを終了できませんでした。");
        }
        finally
        {
            KillCapturedProcessIfNeeded();
            _automation?.Dispose();
            App?.Dispose();
            DeleteDirectory(PublishFolder);
        }
    }

    private static string ResolvePublishFolder()
    {
        var configured = Environment.GetEnvironmentVariable("SDM_DESKTOP_SMOKE_APP");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "SDM_DESKTOP_SMOKE_APP に DocumentManager の publish folder を指定してください。通常の bin フォルダーは使用できません。");
        }

        var folder = Path.GetFullPath(configured.Trim());
        if (!Directory.Exists(folder))
            throw new InvalidOperationException($"SDM_DESKTOP_SMOKE_APP の publish folder が存在しません: {folder}");

        var normalized = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (normalized.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}debug{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"SDM_DESKTOP_SMOKE_APP は publish folder を指定してください。working tree の bin フォルダーは使用できません: {folder}");
        }

        var executable = Path.Combine(folder, "DocumentManager.exe");
        if (!File.Exists(executable))
            throw new InvalidOperationException($"publish folder に DocumentManager.exe がありません: {folder}");

        return folder;
    }

    private static string CreateIsolatedPublishFolder(string sourceFolder)
{
    var destination = Path.Combine(
        Path.GetTempPath(),
        "StudyDocumentManager.DesktopSmokeTests",
        Guid.NewGuid().ToString("N"));

    try
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.GetDirectories(sourceFolder, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(sourceFolder, directory)));
        }

        foreach (var file in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(sourceFolder, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }

        return destination;
    }
    catch (Exception copyError)
    {
        try
        {
            DeleteDirectory(destination);
        }
        catch (Exception cleanupError)
        {
            throw new AggregateException(copyError, cleanupError);
        }

        throw;
    }
}

    private static void SeedDatabase(string publishFolder)
    {
        var dataFolder = Path.Combine(publishFolder, "data");
        if (Directory.Exists(dataFolder))
            Directory.Delete(dataFolder, recursive: true);

        var database = new DatabaseHelper();
        database.SetDatabasePath(Path.Combine(dataFolder, "study_documents.db"));
        database.InitializeDatabase();
        database.InsertDocumentWithCatalogs(new StudyDocument
        {
            Name = "Desktop smoke document",
            Subject = "Smoke",
            Type = "Note",
            Notes = "Seeded for desktop smoke tests"
        });
        database.CloseAllConnections();
    }

    private void RequestProcessClose()
    {
        if (_processId <= 0 || HasExited(_processId))
            return;

        try
        {
            using var process = Process.GetProcessById(_processId);
            process.CloseMainWindow();
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void KillCapturedProcessIfNeeded()
    {
        if (_processId <= 0 || HasExited(_processId))
            return;

        try
        {
            using var process = Process.GetProcessById(_processId);
            process.Kill(entireProcessTree: false);
            process.WaitForExit((int)CleanupTimeout.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static bool HasExited(int processId)
    {
        if (processId <= 0)
            return true;

        try
        {
            using var process = Process.GetProcessById(processId);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    private static T WaitUntil<T>(Func<T> action, Func<T, bool> condition, TimeSpan timeout, string operation)
    {
        var stopwatch = Stopwatch.StartNew();
        Exception? lastError = null;
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                var value = action();
                if (condition(value))
                    return value;
            }
            catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
            {
                lastError = ex;
            }

            Task.Delay(100).GetAwaiter().GetResult();
        }

        throw new TimeoutException($"{operation} が {timeout.TotalSeconds:0} 秒以内に完了しませんでした。{lastError?.Message}");
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        var stopwatch = Stopwatch.StartNew();
        Exception? lastError = null;
        while (stopwatch.Elapsed < CleanupTimeout)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException ex)
            {
                lastError = ex;
            }
            catch (UnauthorizedAccessException ex)
            {
                lastError = ex;
            }

            Task.Delay(100).GetAwaiter().GetResult();
        }

        throw new IOException($"一時 publish folder を削除できませんでした: {path}", lastError);
    }
}
