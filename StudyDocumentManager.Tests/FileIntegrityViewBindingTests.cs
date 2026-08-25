using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Services;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using StudyDocumentManager.Tests.TestDoubles;
using Xunit;

namespace StudyDocumentManager.Tests;

/// <summary>
/// Headless Avalonia proofs that the FileIntegrityCheck view's row buttons resolve their
/// bindings, receive real pointer input, and execute the model contract end to end —
/// including AccessDenied/DriveDisconnected state mapping through an injectable probe.
/// Commands have no CanExecute: row buttons are gated by the IsChecking binding, which is
/// asserted directly.
/// </summary>
public sealed class FileIntegrityViewBindingTests : DatabaseTestBase, IDisposable
{
    private readonly List<string> _tempDirs = new();

    private static KeyLocalizationService Loc => new();

    void IDisposable.Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
                // Temp dir still locked by a just-closed headless window; next run's
                // GUID-named dirs make leftovers harmless.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        base.Dispose();
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task View_OpenContainingFolderButton_ClickRevealsPath()
    {
        var parent = NewTempDir();
        var missingPath = Path.Combine(parent, "gone.pdf");
        SeedDocument("folder doc", missingPath);
        var launcher = new StubProcessLauncherService();
        var (view, window) = Mount(CreateModel(launcher: launcher));

        try
        {
            await ScanAsync(view);
            var button = GetRowButton(view, "FileIntegrity_OpenFolder", "folder doc");

            await ClickButtonAsync(window, button);

            Assert.Contains(missingPath, launcher.Revealed);
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task View_CopyPathButton_ClickCopiesBoundPathToClipboard()
    {
        var missingPath = NewMissingPath();
        SeedDocument("copy doc", missingPath);
        var clipboard = new StubClipboardService();
        var (view, window) = Mount(CreateModel(clipboard: clipboard));

        try
        {
            await ScanAsync(view);
            var button = GetRowButton(view, "FileIntegrity_CopyPath", "copy doc");
            var boundItem = Assert.IsType<IntegrityResult>(button.CommandParameter);

            await ClickButtonAsync(window, button);

            Assert.Equal(boundItem.FilePath, Assert.Single(clipboard.Copied));
            Assert.Equal(missingPath, clipboard.Copied[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task View_RelinkButton_ClickUpdatesRepository_AndNeverTouchesFiles()
    {
        // The original file really exists; the injected probe pretends it is gone so the
        // row is offered for relink. The relink must not move or delete that file.
        var originalPath = Path.Combine(NewTempDir(), "original.pdf");
        File.WriteAllText(originalPath, "original-content");
        SeedDocument("relink doc", originalPath);
        var replacement = MakeFile("replacement.pdf", "replacement");
        var dialogs = new RecordingDialogService { ConfirmResult = true };
        var fileDialogs = new StubFileDialogService(replacement);
        var (view, window) = Mount(CreateModel(
            dialogs: dialogs,
            fileDialogs: fileDialogs,
            fileProbe: path => path == originalPath
                ? false
                : FileStateClassifier.ReadableProbe(path)));

        try
        {
            await ScanAsync(view);
            var button = GetRowButton(view, "FileIntegrity_BtnRelink", "relink doc");
            var boundItem = Assert.IsType<IntegrityResult>(button.CommandParameter);

            await ClickButtonAsync(window, button);

            var reloaded = Repo.GetAll().Single(d => d.Id == boundItem.Document.Id);
            Assert.Equal(replacement, reloaded.FilePath);
            Assert.Equal("QA", reloaded.Author); // metadata preserved

            // The original file survives the relink byte-for-byte, and the replacement
            // is referenced in place rather than moved or deleted by the app.
            Assert.True(File.Exists(originalPath));
            Assert.Equal("original-content", File.ReadAllText(originalPath));
            Assert.True(File.Exists(replacement));
            Assert.Equal("replacement", File.ReadAllText(replacement));
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task View_ClearPathAndDeleteButtons_ClickWithBoundParameter_UpdateRepository()
    {
        SeedDocument("clear doc", NewMissingPath());
        SeedDocument("delete doc", NewMissingPath());
        var dialogs = new RecordingDialogService { ConfirmResult = true };
        var (view, window) = Mount(CreateModel(dialogs: dialogs));

        try
        {
            await ScanAsync(view);
            Assert.Equal(2, model(view).Results.Count);

            var clearButton = GetRowButton(view, "FileIntegrity_BtnClearPath", "clear doc");
            var clearParam = Assert.IsType<IntegrityResult>(clearButton.CommandParameter);
            await ClickButtonAsync(window, clearButton);

            var deleteButton = GetRowButton(view, "FileIntegrity_BtnDelete", "delete doc");
            var deleteParam = Assert.IsType<IntegrityResult>(deleteButton.CommandParameter);
            await ClickButtonAsync(window, deleteButton);

            // The executed commands must have operated on their own bound rows.
            Assert.Equal(string.Empty, Repo.GetAll().Single(d => d.Id == clearParam.Document.Id).FilePath);
            Assert.DoesNotContain(Repo.GetAll(), d => d.Id == deleteParam.Document.Id); // soft-deleted
            Assert.Empty(model(view).Results);
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task View_AccessDeniedState_MapsToLocalizedRowStatus()
    {
        var doc = SeedDocument("denied doc", NewMissingPath());
        var (view, window) = Mount(CreateModel(fileProbe: _ => throw new UnauthorizedAccessException()));

        try
        {
            await ScanAsync(view);

            var result = Assert.Single(model(view).Results);
            Assert.Equal(doc.Id, result.Document.Id);
            Assert.Equal("FileState_AccessDenied", result.StatusKey);
            Assert.Equal("FileState_AccessDenied", result.Status); // KeyLocalizationService returns the key
            Assert.True(IsRowRendered(view, result));
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task View_DriveDisconnectedState_MapsToLocalizedRowStatus()
    {
        SeedDocument("drive doc", @"C:\docs\network.pdf");
        const int errorNotReady = 21;
        var (view, window) = Mount(CreateModel(fileProbe: _ =>
            throw new System.IO.IOException("not ready", unchecked((int)(0x80070000u | (uint)errorNotReady)))));

        try
        {
            await ScanAsync(view);

            var result = Assert.Single(model(view).Results);
            Assert.Equal("FileState_DriveDisconnected", result.StatusKey);
            Assert.Equal("FileState_DriveDisconnected", result.Status);
            Assert.True(IsRowRendered(view, result));
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task View_InvalidPathState_MapsToLocalizedRowStatus()
    {
        SeedDocument("invalid doc", "relative/nope.pdf");
        var (view, window) = Mount(CreateModel());

        try
        {
            await ScanAsync(view);

            var result = Assert.Single(model(view).Results);
            Assert.Equal(DocumentFileState.InvalidPath, result.State);
            Assert.Equal("FileState_InvalidPath", result.StatusKey);
            Assert.Equal("FileState_InvalidPath", result.Status);
            Assert.True(IsRowRendered(view, result));
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task View_DatabaseLocationAndRowPathStatus_BindRenderedValues()
    {
        var missingPath = NewMissingPath();
        SeedDocument("binding doc", missingPath);
        var (view, window) = Mount(CreateModel());

        try
        {
            await ScanAsync(view);

            // Database location binding: model value equals the isolated test database path
            // and the rendered TextBlock shows the same string.
            Assert.Equal(DbPath, model(view).DatabaseLocation);
            var locationText = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(t => AutomationProperties.GetAutomationId(t) == "FileIntegrity_DatabaseLocation");
            Assert.Equal(DbPath, locationText.Text);

            // Row path and status bindings render the bound item's values.
            var result = Assert.Single(model(view).Results);
            Assert.Equal(missingPath, result.FilePath);
            Assert.Contains(missingPath, RenderedTexts(view));
            Assert.Contains(result.Status, RenderedTexts(view));
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task View_RowButtons_DisabledWhileChecking_ClickCausesNoSideEffects()
    {
        SeedDocument("gate doc", NewMissingPath());
        var dialogs = new RecordingDialogService();
        var clipboard = new StubClipboardService();
        var launcher = new StubProcessLauncherService();
        var (view, window) = Mount(CreateModel(dialogs: dialogs, clipboard: clipboard, launcher: launcher));

        try
        {
            await ScanAsync(view);
            var buttons = RowButtons(view);
            Assert.NotEmpty(buttons);
            Assert.All(buttons, b => Assert.True(b.IsEnabled));

            // Commands define no CanExecute; the IsChecking binding gates the buttons.
            model(view).IsChecking = true;
            FlushBindings();
            Assert.All(buttons, b => Assert.False(b.IsEnabled));

            // Clicking every gated button while checking must not run any command:
            // no launcher, clipboard, dialog, or repository side effects.
            foreach (var button in buttons)
            {
                window.MouseMove(ButtonCenter(window, button));
                window.MouseDown(ButtonCenter(window, button), MouseButton.Left);
                window.MouseUp(ButtonCenter(window, button), MouseButton.Left);
            }

            FlushBindings();
            Assert.Empty(launcher.Revealed);
            Assert.Empty(clipboard.Copied);
            Assert.Empty(dialogs.Timeline);
            Assert.All(model(view).Results, r => Assert.False(_repositoryDeleted(r.Document.Id)));

            model(view).IsChecking = false;
            FlushBindings();
            Assert.All(RowButtons(view), b => Assert.True(b.IsEnabled));
        }
        finally
        {
            window.Close();
        }
    }

    private bool _repositoryDeleted(int id) => Repo.GetAll().All(d => d.Id != id);

    // --- helpers ---

    private static FileIntegrityCheckModel model(StudyDocumentManager.Views.FileIntegrityCheck view) => (FileIntegrityCheckModel)view.DataContext!;

    private string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sdm_view_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    private string NewMissingPath() => Path.Combine(NewTempDir(), "gone.pdf");

    private string MakeFile(string name, string content)
    {
        var path = Path.Combine(NewTempDir(), name);
        File.WriteAllText(path, content);
        return path;
    }

    private StudyDocument SeedDocument(string name, string filePath)
    {
        var doc = new StudyDocument
        {
            Name = name,
            Subject = "ViewBinding",
            Type = "PDF",
            Author = "QA",
            FilePath = filePath
        };
        Assert.True(Repo.Add(doc));
        return doc;
    }

    private FileIntegrityCheckModel CreateModel(
        RecordingDialogService? dialogs = null,
        StubFileDialogService? fileDialogs = null,
        StubClipboardService? clipboard = null,
        StubProcessLauncherService? launcher = null,
        Func<string, bool>? fileProbe = null)
        => new(
            Repo,
            Repo,
            dialogs ?? new RecordingDialogService(),
            fileDialogs ?? new StubFileDialogService(),
            Loc,
            clipboardService: clipboard ?? new StubClipboardService(),
            processLauncher: launcher ?? new StubProcessLauncherService(),
            fileProbe: fileProbe);

    private static (StudyDocumentManager.Views.FileIntegrityCheck View, Window Window) Mount(FileIntegrityCheckModel model)
    {
        var view = new StudyDocumentManager.Views.FileIntegrityCheck { DataContext = model };
        var window = new Window { Content = view, Width = 1280, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (view, window);
    }

    private static async Task ScanAsync(StudyDocumentManager.Views.FileIntegrityCheck view)
    {
        await model(view).CheckIntegrityCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Row template buttons repeat per rendered row, so callers disambiguate by bound document name.
    /// </summary>
    private static Button GetRowButton(StudyDocumentManager.Views.FileIntegrityCheck view, string automationId, string documentName)
    {
        FlushBindings();
        return view.GetVisualDescendants()
            .OfType<Button>()
            .Where(b => AutomationProperties.GetAutomationId(b) == automationId)
            .Single(b => b.CommandParameter is IntegrityResult item && item.Document.Name == documentName);
    }

    private static List<Button> RowButtons(StudyDocumentManager.Views.FileIntegrityCheck view)
    {
        FlushBindings();
        string[] ids =
        [
            "FileIntegrity_OpenFolder",
            "FileIntegrity_CopyPath",
            "FileIntegrity_BtnRelink",
            "FileIntegrity_BtnClearPath",
            "FileIntegrity_BtnDelete"
        ];
        return view.GetVisualDescendants()
            .OfType<Button>()
            .Where(b => ids.Contains(AutomationProperties.GetAutomationId(b)))
            .ToList();
    }

    /// <summary>
    /// Sends real headless pointer input through the window, then awaits the async command
    /// the button raised so tests observe completed state instead of in-flight state.
    /// </summary>
    private static async Task ClickButtonAsync(Window window, Button button)
    {
        Assert.True(button.IsEnabled, "button must be enabled before the click");
        var clickPoint = ButtonCenter(window, button);

        window.MouseMove(clickPoint);
        window.MouseDown(clickPoint, MouseButton.Left);
        window.MouseUp(clickPoint, MouseButton.Left);

        if (button.Command is IAsyncRelayCommand asyncCommand)
        {
            while (asyncCommand.IsRunning)
                await Task.Delay(10);

            if (asyncCommand.ExecutionTask is { IsFaulted: true } faulted)
                throw faulted.Exception!;
        }

        FlushBindings();
    }

    private static Point ButtonCenter(Window window, Button button)
        => button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("button is not attached to the test window");

    private static bool IsRowRendered(StudyDocumentManager.Views.FileIntegrityCheck view, IntegrityResult result)
    {
        FlushBindings();
        var texts = RenderedTexts(view);
        return texts.Contains(result.Status) && texts.Contains(result.FilePath);
    }

    private static HashSet<string> RenderedTexts(StudyDocumentManager.Views.FileIntegrityCheck view)
        => view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(t => t.Text ?? string.Empty)
            .ToHashSet();

    private static void FlushBindings() => Dispatcher.UIThread.RunJobs();
}
