using StudyDocumentManager.Core.Entities;
using Xunit;
using StudyDocumentManager.Core.Services;
using StudyDocumentManager.Models;
using StudyDocumentManager.Tests.TestDoubles;

namespace StudyDocumentManager.Tests;

/// <summary>
/// Isolated file-integrity and relink proofs (Issue #45): relink preserves identity and
/// metadata, never touches files on disk, and broken-path states are distinguished.
/// All state lives in per-test temp databases and temp folders.
/// </summary>
public sealed class IntegrityRelinkTests : DatabaseTestBase
{
    private static KeyLocalizationService Loc => new();

    private FileIntegrityCheckModel CreateModel(
        StubFileDialogService? fileDialogs = null,
        RecordingDialogService? dialogs = null,
        StubClipboardService? clipboard = null,
        StubProcessLauncherService? launcher = null,
        Func<string, bool>? fileProbe = null,
        Func<string, bool>? rootReadyProbe = null)
        => new(
            Repo,
            Repo,
            dialogs ?? new RecordingDialogService(),
            fileDialogs ?? new StubFileDialogService(),
            Loc,
            clipboardService: clipboard ?? new StubClipboardService(),
            processLauncher: launcher ?? new StubProcessLauncherService(),
            fileProbe: fileProbe,
            rootReadyProbe: rootReadyProbe);

    private StudyDocument SeedDocument(string name, string filePath)
    {
        var doc = new StudyDocument
        {
            Name = name,
            Subject = "Relink",
            Type = "PDF",
            Author = "QA",
            Tags = "tag1;tag2",
            Notes = "metadata must survive",
            IsImportant = true,
            FilePath = filePath
        };
        Assert.True(Repo.Add(doc));
        return doc;
    }

    private static string MakeFile(string name, string content = "original")
    {
        var path = Path.Combine(Path.GetTempPath(), $"sdm_relink_{Guid.NewGuid():N}", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static int HResultFromWin32(int win32Error)
        => unchecked((int)(0x80070000u | (uint)win32Error));

    [Fact]
    public void Relink_UpdatesOnlyFilePath_AndPreservesIdentityAndMetadata()
    {
        var originalPath = MakeFile("original.pdf");
        var replacementPath = MakeFile("replacement.pdf", "replacement");
        var doc = SeedDocument("thesis", originalPath);

        Assert.True(Repo.UpdateDocumentPath(doc.Id, replacementPath));

        var reloaded = Repo.GetAll().Single(d => d.Id == doc.Id);
        Assert.Equal(doc.Id, reloaded.Id);
        Assert.Equal("thesis", reloaded.Name);
        Assert.Equal("Relink", reloaded.Subject);
        Assert.Equal("PDF", reloaded.Type);
        Assert.Equal("QA", reloaded.Author);
        Assert.Equal("tag1;tag2", reloaded.Tags);
        Assert.Equal("metadata must survive", reloaded.Notes);
        Assert.True(reloaded.IsImportant);
        Assert.Equal(replacementPath, reloaded.FilePath);

        // The original file is never moved or deleted by a relink.
        Assert.True(File.Exists(originalPath));
        Assert.Equal("original", File.ReadAllText(originalPath));
    }

    [Fact]
    public void ClearPath_KeepsMetadataAndEmptiesFilePath()
    {
        var originalPath = MakeFile("clear-me.pdf");
        var doc = SeedDocument("clear target", originalPath);

        Assert.True(Repo.ClearDocumentPath(doc.Id));

        var reloaded = Repo.GetAll().Single(d => d.Id == doc.Id);
        Assert.Equal(string.Empty, reloaded.FilePath);
        Assert.Equal("clear target", reloaded.Name);
        Assert.Equal("QA", reloaded.Author);
        Assert.True(File.Exists(originalPath));
    }

    [Fact]
    public async Task Scan_DistinguishesMissingInvalidAndNotSetStates()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"sdm_relink_{Guid.NewGuid():N}", "gone.pdf");
        SeedDocument("missing doc", missingPath);
        SeedDocument("invalid doc", "relative/nope.pdf");
        SeedDocument("no file doc", string.Empty);
        var okPath = MakeFile("ok.pdf");
        SeedDocument("ok doc", okPath);

        var model = CreateModel();
        await model.CheckIntegrityCommand.ExecuteAsync(null);

        Assert.Equal(2, model.Results.Count);
        var statuses = model.Results.Select(r => r.StatusKey).OrderBy(k => k).ToList();
        Assert.Equal(["FileState_InvalidPath", "Integrity_FileNotExist"], statuses);
        Assert.Contains(model.Results, r => r.State == DocumentFileState.Missing && r.FilePath == missingPath);
        Assert.Contains(model.Results, r => r.State == DocumentFileState.InvalidPath);
    }

    [Fact]
    public async Task Relink_Model_RejectsInaccessibleTarget_AndKeepsCurrentPath()
    {
        var missingPath = MakeFile("will-vanish.pdf");
        File.Delete(missingPath);
        var doc = SeedDocument("relink target", missingPath);
        var dialogs = new RecordingDialogService();
        var fileDialogs = new StubFileDialogService(Path.Combine(Path.GetTempPath(), $"sdm_relink_{Guid.NewGuid():N}", "also-missing.pdf"));
        var model = CreateModel(fileDialogs: fileDialogs, dialogs: dialogs);
        await model.CheckIntegrityCommand.ExecuteAsync(null);
        var item = Assert.Single(model.Results);

        await model.SelectNewFileCommand.ExecuteAsync(item);

        Assert.Contains(dialogs.Timeline, t => t.StartsWith("error|", StringComparison.Ordinal));
        var reloaded = Repo.GetAll().Single(d => d.Id == doc.Id);
        Assert.Equal(missingPath, reloaded.FilePath); // unchanged
        Assert.Single(model.Results); // item stays listed
    }

    [Fact]
    public async Task Relink_Model_AcceptsAccessibleTarget_UpdatesPathAndKeepsMetadata()
    {
        var missingPath = MakeFile("vanished.pdf");
        File.Delete(missingPath);
        var replacementPath = MakeFile("replacement.pdf", "replacement");
        var doc = SeedDocument("relink ok", missingPath);
        var dialogs = new RecordingDialogService();
        var fileDialogs = new StubFileDialogService(replacementPath);
        var model = CreateModel(fileDialogs: fileDialogs, dialogs: dialogs);
        await model.CheckIntegrityCommand.ExecuteAsync(null);
        var item = Assert.Single(model.Results);

        await model.SelectNewFileCommand.ExecuteAsync(item);

        Assert.Empty(model.Results);
        var reloaded = Repo.GetAll().Single(d => d.Id == doc.Id);
        Assert.Equal(doc.Id, reloaded.Id);
        Assert.Equal("relink ok", reloaded.Name);
        Assert.Equal("QA", reloaded.Author);
        Assert.Equal(replacementPath, reloaded.FilePath);
        Assert.Contains(dialogs.Timeline, t => t.StartsWith("message|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Scan_ClassifiesDirectoryNotFoundOnUnreadyRootAsDriveDisconnected()
    {
        SeedDocument("disconnected doc", @"Z:\missing\doc.pdf");
        var model = CreateModel(
            fileProbe: _ => throw new DirectoryNotFoundException("path not found") { HResult = HResultFromWin32(3) },
            rootReadyProbe: _ => false);

        await model.CheckIntegrityCommand.ExecuteAsync(null);

        var result = Assert.Single(model.Results);
        Assert.Equal(DocumentFileState.DriveDisconnected, result.State);
        Assert.Equal("FileState_DriveDisconnected", result.StatusKey);
    }

    [Fact]
    public async Task Scan_ClassifiesDirectoryNotFoundOnReadyRootAsMissing()
    {
        SeedDocument("missing dir doc", @"C:\docs\really\gone.pdf");
        var model = CreateModel(
            fileProbe: _ => throw new DirectoryNotFoundException("path not found") { HResult = HResultFromWin32(3) },
            rootReadyProbe: _ => true);

        await model.CheckIntegrityCommand.ExecuteAsync(null);

        var result = Assert.Single(model.Results);
        Assert.Equal(DocumentFileState.Missing, result.State);
        Assert.Equal("Integrity_FileNotExist", result.StatusKey);
    }

    [Fact]
    public async Task Scan_ClassifiesDirectoryNotFoundOnUnreachableUncShareAsDriveDisconnected()
    {
        SeedDocument("unc doc", @"\\server\share\missing\doc.pdf");
        var model = CreateModel(
            fileProbe: _ => throw new DirectoryNotFoundException("path not found") { HResult = HResultFromWin32(3) },
            rootReadyProbe: _ => false);

        await model.CheckIntegrityCommand.ExecuteAsync(null);

        var result = Assert.Single(model.Results);
        Assert.Equal(DocumentFileState.DriveDisconnected, result.State);
        Assert.Equal("FileState_DriveDisconnected", result.StatusKey);
    }

    [Fact]
    public async Task OpenContainingFolder_UsesLauncherWithBrokenPath()
    {
        var parent = Path.Combine(Path.GetTempPath(), $"sdm_relink_{Guid.NewGuid():N}");
        Directory.CreateDirectory(parent);
        var missingPath = Path.Combine(parent, "gone.pdf");
        var doc = SeedDocument("folder target", missingPath);
        var launcher = new StubProcessLauncherService();
        var model = CreateModel(launcher: launcher);
        await model.CheckIntegrityCommand.ExecuteAsync(null);
        var item = Assert.Single(model.Results);

        await model.OpenContainingFolderCommand.ExecuteAsync(item);

        Assert.Contains(missingPath, launcher.Revealed);
    }

    [Fact]
    public async Task OpenContainingFolder_WithoutParentDirectory_ShowsErrorAndSkipsLauncher()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"sdm_relink_{Guid.NewGuid():N}", "gone.pdf");
        SeedDocument("no parent", missingPath);
        var dialogs = new RecordingDialogService();
        var launcher = new StubProcessLauncherService();
        var model = CreateModel(dialogs: dialogs, launcher: launcher);
        await model.CheckIntegrityCommand.ExecuteAsync(null);
        var item = Assert.Single(model.Results);

        await model.OpenContainingFolderCommand.ExecuteAsync(item);

        Assert.Empty(launcher.Revealed);
        Assert.Contains(dialogs.Timeline, t => t.StartsWith("error|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CopyPath_WritesClipboard_AndSetsLocalizedStatus()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"sdm_relink_{Guid.NewGuid():N}", "gone.pdf");
        SeedDocument("copy target", missingPath);
        var clipboard = new StubClipboardService();
        var model = CreateModel(clipboard: clipboard);
        await model.CheckIntegrityCommand.ExecuteAsync(null);
        var item = Assert.Single(model.Results);

        await model.CopyPathCommand.ExecuteAsync(item);

        Assert.Equal(missingPath, Assert.Single(clipboard.Copied));
        Assert.Equal("Integrity_PathCopied", model.StatusText);
    }
}
