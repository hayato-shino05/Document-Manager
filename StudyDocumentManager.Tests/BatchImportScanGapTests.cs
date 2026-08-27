using System.Security.AccessControl;
using StudyDocumentManager.Core;
using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using Xunit;

namespace StudyDocumentManager.Tests;

public class BatchImportScanGapTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void ScanFolder_BlankFolderPath_DoesNotThrowAndLeavesPreviewEmpty(string folderPath)
    {
        var model = CreateBatchImportModel();
        model.FolderPath = folderPath;

        model.ScanFolderCommand.Execute(null);

        Assert.Empty(model.Files);
        Assert.False(model.HasFiles);
        Assert.Equal(string.Empty, model.ImportErrorMessage);
    }

    [Fact]
    public void ScanFolder_NonExistentFolder_DoesNotThrowAndLeavesPreviewEmpty()
    {
        var model = CreateBatchImportModel();
        model.FolderPath = Path.Combine(Path.GetTempPath(), "sdm_missing_" + Guid.NewGuid().ToString("N"));

        model.ScanFolderCommand.Execute(null);

        Assert.Empty(model.Files);
        Assert.False(model.HasFiles);
        Assert.Equal(string.Empty, model.ImportErrorMessage);
    }

    [Fact]
    public void ScanFolder_SupportedFiles_PopulatesPreviewWithDetectedTypesAndSizes()
    {
        var folder = CreateTempFolder();
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "notes.pdf"), [1, 2, 3]);
            File.WriteAllBytes(Path.Combine(folder, "report.docx"), [1, 2, 3, 4, 5]);
            File.WriteAllBytes(Path.Combine(folder, "readme.txt"), [1, 2, 3]);

            var model = CreateBatchImportModel();
            model.FolderPath = folder;
            model.ScanFolderCommand.Execute(null);

            Assert.Equal(3, model.Files.Count);
            Assert.True(model.HasFiles);
            Assert.True(model.Files.All(file => file.IsSelected));
            Assert.Equal(string.Empty, model.ImportErrorMessage);

            var pdf = model.Files.Single(file => file.FileName == "notes");
            Assert.Equal("PDF", pdf.FileType);
            Assert.Equal(3.0 / (1024.0 * 1024.0), pdf.FileSizeMB, 10);
            Assert.EndsWith(".pdf", pdf.FilePath, StringComparison.OrdinalIgnoreCase);

            Assert.Equal("Word", model.Files.Single(file => file.FileName == "report").FileType);
            Assert.Equal("Document", model.Files.Single(file => file.FileName == "readme").FileType);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void ScanFolder_SubFolderFiles_IncludedInPreview()
    {
        var folder = CreateTempFolder();
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "top.pdf"), [1, 2, 3]);
            var sub = Directory.CreateDirectory(Path.Combine(folder, "nested"));
            File.WriteAllBytes(Path.Combine(sub.FullName, "inner.docx"), [1, 2, 3, 4]);

            var model = CreateBatchImportModel();
            model.FolderPath = folder;
            model.ScanFolderCommand.Execute(null);

            Assert.Equal(2, model.Files.Count);
            Assert.Contains(model.Files, file => file.FileName == "top" && file.FileType == "PDF");
            Assert.Contains(model.Files, file => file.FileName == "inner" && file.FileType == "Word");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void ScanFolder_OnlyUnsupportedExtensions_LeavesPreviewEmptyWithoutError()
    {
        var folder = CreateTempFolder();
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "data.xyz"), [1, 2, 3]);
            File.WriteAllBytes(Path.Combine(folder, "tool.exe"), [1, 2, 3, 4]);

            var model = CreateBatchImportModel();
            model.FolderPath = folder;
            model.ScanFolderCommand.Execute(null);

            Assert.Empty(model.Files);
            Assert.False(model.HasFiles);
            Assert.Equal(string.Empty, model.ImportErrorMessage);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void ScanFolder_EmptyFolder_LeavesPreviewEmptyWithoutError()
    {
        var folder = CreateTempFolder();
        try
        {
            var model = CreateBatchImportModel();
            model.FolderPath = folder;
            model.ScanFolderCommand.Execute(null);

            Assert.Empty(model.Files);
            Assert.False(model.HasFiles);
            Assert.Equal(string.Empty, model.ImportErrorMessage);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void ScanFolder_Rescan_ClearsPreviousPreviewWithoutDuplicates()
    {
        var folder = CreateTempFolder();
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "a.pdf"), [1, 2, 3]);

            var model = CreateBatchImportModel();
            model.FolderPath = folder;

            model.ScanFolderCommand.Execute(null);
            Assert.Single(model.Files);

            model.ScanFolderCommand.Execute(null);
            Assert.Single(model.Files);

            File.WriteAllBytes(Path.Combine(folder, "b.docx"), [1, 2, 3, 4]);
            model.ScanFolderCommand.Execute(null);

            Assert.Equal(2, model.Files.Count);
            Assert.Single(model.Files, file => file.FileName == "a");
            Assert.Single(model.Files, file => file.FileName == "b");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void ScanFolder_EnumerationError_SetsScanErrorMessageAndClearsPreview()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var folder = CreateTempFolder();
        var denied = default(DirectoryInfo);
        try
        {
            denied = Directory.CreateDirectory(Path.Combine(folder, "locked"));
            var acl = denied.GetAccessControl();
            acl.AddAccessRule(new FileSystemAccessRule(
                Environment.UserName, FileSystemRights.ReadData, AccessControlType.Deny));
            denied.SetAccessControl(acl);

            var model = CreateBatchImportModel();
            model.FolderPath = folder;
            model.ScanFolderCommand.Execute(null);

            Assert.Equal("BatchImport_ScanError", model.ImportErrorMessage);
            Assert.Empty(model.Files);
            Assert.False(model.HasFiles);
        }
        catch (PlatformNotSupportedException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        finally
        {
            if (denied is not null)
            {
                try
                {
                    var acl = denied.GetAccessControl();
                    acl.RemoveAccessRule(new FileSystemAccessRule(
                        Environment.UserName, FileSystemRights.ReadData, AccessControlType.Deny));
                    denied.SetAccessControl(acl);
                }
                catch (Exception)
                {
                }
            }

            try
            {
                Directory.Delete(folder, true);
            }
            catch (Exception)
            {
            }
        }
    }

    private static string CreateTempFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), "sdm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static BatchImportModel CreateBatchImportModel()
    {
        return new BatchImportModel(
            new RecordingDialogService(),
            new FakeFileDialogService(),
            new RecordingNavigationService(),
            new TestLocalizationService(),
            new RecordingDroppedFileImportService());
    }

    private sealed class RecordingDialogService : IDialogService
    {
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(false);
        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
            => Task.FromResult(false);
        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "")
            => Task.FromResult<string?>(null);
    }

    private sealed class FakeFileDialogService : IFileDialogService
    {
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null)
            => Task.FromResult<string?>(null);
    }

    private sealed class RecordingNavigationService : INavigationService
    {
        public bool CanGoBack => false;
        public void NavigateTo(string viewKey) { }
        public void NavigateTo(string viewKey, object? parameter) { }
        public void GoBack() { }
    }

    private sealed class TestLocalizationService : ILocalizationService
    {
        public string this[string key] => key;
        public SupportedLanguage CurrentLanguage { get; private set; } = SupportedLanguage.Japanese;
        public void SetLanguage(SupportedLanguage language) => CurrentLanguage = language;
        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [];
        public event EventHandler? LanguageChanged { add { } remove { } }
    }

    private sealed class RecordingDroppedFileImportService : IDroppedFileImportService
    {
        public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects) => fallbackSubjects.ToList();
        public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes) => fallbackTypes.ToList();
        public DocumentImportOutcome SaveDocument(StudyDocument document) => DocumentImportOutcome.Imported;
        public StudyDocument BuildDocumentFromPath(string filePath) => new();
    }
}