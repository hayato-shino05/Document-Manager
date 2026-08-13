using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Models;
using StudyDocumentManager.Services;
using StudyDocumentManager.Core;
using Xunit;

namespace StudyDocumentManager.Tests;

public class Slice4FlowPolishTests
{
    [Fact]
    public async Task AddEdit_SaveBlankName_SetsInlineValidationInsteadOfNavigating()
    {
        var dialog = new RecordingDialogService();
        var navigation = new RecordingNavigationService();
        var model = CreateModel(dialog, new FakeFileDialogService(), navigation);
        model.Name = "   ";

        await model.SaveCommand.ExecuteAsync(null);

        Assert.True(model.HasNameValidationError);
        Assert.Equal("AddEdit_NameRequired", model.NameValidationMessage);
        Assert.Empty(navigation.Routes);
        Assert.Empty(dialog.Messages);
    }

    [Fact]
    public void AddEdit_NameChange_ClearsInlineValidation()
    {
        var model = CreateModel();
        model.HasNameValidationError = true;
        model.NameValidationMessage = "AddEdit_NameRequired";

        model.Name = "Algorithms notes";

        Assert.False(model.HasNameValidationError);
        Assert.Equal(string.Empty, model.NameValidationMessage);
    }

    [Fact]
    public async Task AddEdit_SaveNewDocument_UsesAtomicRepositoryPathWithoutCategoryRepoWrites()
    {
        var dialog = new RecordingDialogService();
        var navigation = new RecordingNavigationService();
        var model = new AddEditModel(
            new AtomicAddDocumentRepository(),
            new ThrowingCategoryRepository(),
            dialog,
            new FakeFileDialogService(),
            navigation,
            new TestLocalizationService());
        model.Name = "Atomic";
        model.Subject = "AtomicSubject";
        model.Type = "AtomicType";

        await model.SaveCommand.ExecuteAsync(null);

        Assert.Contains("AddEdit_SaveAdded", dialog.Messages);
        Assert.Contains("dashboard", navigation.Routes);
    }

    [Fact]
    public async Task AddEdit_SaveRepositoryException_ShowsErrorWithoutNavigation()
    {
        var dialog = new RecordingDialogService();
        var navigation = new RecordingNavigationService();
        var model = new AddEditModel(
            new ThrowingDocumentRepository(),
            new FakeCategoryRepository(),
            dialog,
            new FakeFileDialogService(),
            navigation,
            new TestLocalizationService());
        model.Name = "Atomic";
        model.Subject = "AtomicSubject";

        await model.SaveCommand.ExecuteAsync(null);

        Assert.Contains("AddEdit_SaveError", dialog.Messages);
        Assert.DoesNotContain("dashboard", navigation.Routes);
    }

    [Fact]
    public void AddEdit_TryApplyFile_FillsPathNameAndTypeWhenAppropriate()
    {
        var model = CreateModel();
        var tempFile = Path.Combine(Path.GetTempPath(), $"slice4_{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(tempFile, [1, 2, 3, 4]);

        try
        {
            var result = model.TryApplyFile(tempFile);

            Assert.True(result);
            Assert.Equal(tempFile, model.FilePath);
            Assert.Equal(Path.GetFileNameWithoutExtension(tempFile), model.Name);
            Assert.Equal("PDF", model.Type);
            Assert.Contains("PDF", model.Types);
            Assert.False(model.HasNameValidationError);
            Assert.Equal(string.Empty, model.NameValidationMessage);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }


    [Fact]
    public void AddEdit_LoadDocument_PopulatesEditStateFromRepository()
    {
        var deadline = new DateTime(2026, 8, 3, 14, 30, 0);
        var document = new StudyDocument
        {
            Id = 42,
            Name = "Algorithms notes",
            Subject = "Computer Science",
            Type = "PDF",
            FilePath = "C:/study/algorithms.pdf",
            Notes = "Read chapter three",
            Author = "Ada",
            Tags = "algorithms",
            Deadline = deadline,
            IsImportant = true
        };
        var repository = new RecordingDocumentRepository { Document = document };
        var model = CreateModel(repository: repository);

        model.LoadDocument(document.Id);

        Assert.True(model.IsEditing);
        Assert.Equal("AddEdit_PageTitleEdit", model.PageTitle);
        Assert.Equal(document.Name, model.Name);
        Assert.Equal(document.Subject, model.Subject);
        Assert.Equal(document.Type, model.Type);
        Assert.Equal(document.FilePath, model.FilePath);
        Assert.Equal(document.Deadline, model.Deadline!.Value.DateTime);
        Assert.True(model.IsImportant);
    }

    [Fact]
    public void AddEdit_LoadDocument_MissingDocumentKeepsInitialAddState()
    {
        var model = CreateModel(repository: new RecordingDocumentRepository());

        model.LoadDocument(404);

        Assert.False(model.IsEditing);
        Assert.Equal("AddEdit_PageTitleAdd", model.PageTitle);
        Assert.Equal(string.Empty, model.Name);
        Assert.Equal(string.Empty, model.FilePath);
    }

    [Fact]
    public async Task AddEdit_SaveEdit_TrimsFieldsAndUpdatesExistingDocument()
    {
        var deadline = new DateTime(2026, 8, 3, 14, 30, 0);
        var repository = new RecordingDocumentRepository
        {
            Document = new StudyDocument { Id = 42, Name = "Original" }
        };
        var dialog = new RecordingDialogService();
        var navigation = new RecordingNavigationService();
        var model = CreateModel(dialogService: dialog, navigationService: navigation, repository: repository);
        model.LoadDocument(42);
        model.Name = "  Updated name  ";
        model.Subject = "  Computer Science  ";
        model.Type = "  PDF  ";
        model.FilePath = "  C:/study/updated.pdf  ";
        model.Notes = "  Notes  ";
        model.Author = "  Ada  ";
        model.Tags = "  algorithms  ";
        model.Deadline = new DateTimeOffset(deadline);
        model.IsImportant = true;

        await model.SaveCommand.ExecuteAsync(null);

        var updated = Assert.Single(repository.UpdatedDocuments);
        Assert.Equal(42, updated.Id);
        Assert.Equal("Updated name", updated.Name);
        Assert.Equal("Computer Science", updated.Subject);
        Assert.Equal("PDF", updated.Type);
        Assert.Equal("C:/study/updated.pdf", updated.FilePath);
        Assert.Equal("Notes", updated.Notes);
        Assert.Equal("Ada", updated.Author);
        Assert.Equal("algorithms", updated.Tags);
        Assert.Equal(deadline, updated.Deadline);
        Assert.True(updated.IsImportant);
        Assert.Equal(["AddEdit_SaveUpdated"], dialog.Messages);
        Assert.Equal(["dashboard"], navigation.Routes);
    }

    [Fact]
    public async Task AddEdit_SaveNewDocument_UsesTrimmedPathForFileSize()
    {
        var filePath = CreateTempFile("trimmed-size", ".pdf");
        var repository = new RecordingDocumentRepository();
        var model = CreateModel(repository: repository);
        model.Name = "Trimmed path";
        model.FilePath = $"  {filePath}  ";

        try
        {
            await model.SaveCommand.ExecuteAsync(null);

            var saved = Assert.Single(repository.AddedDocuments);
            Assert.Equal(filePath, saved.FilePath);
            Assert.NotNull(saved.FileSize);
            Assert.Equal(4 / (1024.0 * 1024.0), saved.FileSize.Value);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task AddEdit_SaveEditReturningFalse_ShowsErrorWithoutNavigation()
    {
        var repository = new RecordingDocumentRepository
        {
            Document = new StudyDocument { Id = 42, Name = "Original" },
            UpdateResult = false
        };
        var dialog = new RecordingDialogService();
        var navigation = new RecordingNavigationService();
        var model = CreateModel(dialogService: dialog, navigationService: navigation, repository: repository);
        model.LoadDocument(42);
        model.Name = "Updated";

        await model.SaveCommand.ExecuteAsync(null);

        Assert.Single(repository.UpdatedDocuments);
        Assert.Equal(["AddEdit_SaveError"], dialog.Messages);
        Assert.Empty(navigation.Routes);
    }

    [Fact]
    public async Task AddEdit_SaveEditException_ShowsErrorWithoutNavigation()
    {
        var repository = new RecordingDocumentRepository
        {
            Document = new StudyDocument { Id = 42, Name = "Original" },
            UpdateException = new InvalidOperationException("save failed")
        };
        var dialog = new RecordingDialogService();
        var navigation = new RecordingNavigationService();
        var model = CreateModel(dialogService: dialog, navigationService: navigation, repository: repository);
        model.LoadDocument(42);
        model.Name = "Updated";

        await model.SaveCommand.ExecuteAsync(null);

        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal(["AddEdit_SaveError"], dialog.Messages);
        Assert.Empty(navigation.Routes);
    }

    [Fact]
    public async Task AddEdit_SaveNewReturningFalse_ShowsErrorWithoutNavigation()
    {
        var repository = new RecordingDocumentRepository { AddResult = false };
        var dialog = new RecordingDialogService();
        var navigation = new RecordingNavigationService();
        var model = CreateModel(dialogService: dialog, navigationService: navigation, repository: repository);
        model.Name = "New document";

        await model.SaveCommand.ExecuteAsync(null);

        Assert.Single(repository.AddedDocuments);
        Assert.Equal(["AddEdit_SaveError"], dialog.Messages);
        Assert.Empty(navigation.Routes);
    }

    [Fact]
    public async Task AddEdit_BrowseExistingFile_AppliesFileAndPassesLocalizedPickerArguments()
    {
        var filePath = CreateTempFile("browse", ".pdf");
        var fileDialog = new RecordingFileDialogService(filePath);
        var model = CreateModel(fileDialogService: fileDialog);

        try
        {
            await model.BrowseFileCommand.ExecuteAsync(null);

            Assert.Equal(filePath, model.FilePath);
            Assert.Equal(Path.GetFileNameWithoutExtension(filePath), model.Name);
            Assert.Equal("PDF", model.Type);
            Assert.Equal("AddEdit_BrowseFile", fileDialog.Title);
            Assert.Equal("AddEdit_FileFilter", fileDialog.Filter);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task AddEdit_BrowseCancelledFile_KeepsExistingState()
    {
        var fileDialog = new RecordingFileDialogService(null);
        var model = CreateModel(fileDialogService: fileDialog);
        model.FilePath = "existing.pdf";
        model.Name = "Existing name";
        model.Type = "PDF";

        await model.BrowseFileCommand.ExecuteAsync(null);

        Assert.Equal("existing.pdf", model.FilePath);
        Assert.Equal("Existing name", model.Name);
        Assert.Equal("PDF", model.Type);
    }

    [Fact]
    public async Task AddEdit_BrowseMissingFile_KeepsExistingState()
    {
        var fileDialog = new RecordingFileDialogService("Z:/missing-file.pdf");
        var model = CreateModel(fileDialogService: fileDialog);
        model.FilePath = "existing.pdf";
        model.Name = "Existing name";
        model.Type = "PDF";

        await model.BrowseFileCommand.ExecuteAsync(null);

        Assert.Equal("existing.pdf", model.FilePath);
        Assert.Equal("Existing name", model.Name);
        Assert.Equal("PDF", model.Type);
    }

    [Fact]
    public void AddEdit_Cancel_NavigatesToDashboardWithoutRepositoryWrites()
    {
        var repository = new RecordingDocumentRepository();
        var navigation = new RecordingNavigationService();
        var model = CreateModel(repository: repository, navigationService: navigation);

        model.CancelCommand.Execute(null);

        Assert.Equal(["dashboard"], navigation.Routes);
        Assert.Empty(repository.UpdatedDocuments);
        Assert.Empty(repository.AddedDocuments);
    }

    [Fact]
    public async Task HandleDroppedFilesAsync_AddEditMultipleFiles_RejectsDropWithoutChangingState()
    {
        var firstPath = CreateTempFile("multi-a", ".pdf");
        var secondPath = CreateTempFile("multi-b", ".pdf");
        var addEdit = CreateModel();
        addEdit.FilePath = "existing.pdf";
        addEdit.Name = "Existing name";
        var importService = new RecordingDroppedFileImportService();
        var mainWindow = BuildMainWindowModel(addEdit, importService);

        try
        {
            await mainWindow.HandleDroppedFilesAsync([firstPath, secondPath]);

            Assert.Equal("BatchImport_InvalidDrop", mainWindow.StatusText);
            Assert.Equal("existing.pdf", addEdit.FilePath);
            Assert.Equal("Existing name", addEdit.Name);
            Assert.Empty(importService.SavedDocuments);
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    }

    [Fact]
    public void AddEdit_TryApplyFile_BlankOrMissingPath_KeepsExistingState()
    {
        var model = CreateModel();
        model.FilePath = "existing.pdf";
        model.Name = "Existing name";
        model.Type = "PDF";

        Assert.False(model.TryApplyFile("   "));
        Assert.False(model.TryApplyFile("Z:/missing-file.pdf"));
        Assert.Equal("existing.pdf", model.FilePath);
        Assert.Equal("Existing name", model.Name);
        Assert.Equal("PDF", model.Type);
    }

    [Fact]
    public void AddEdit_TryApplyFile_PreservesExistingNameAndType()
    {
        var filePath = CreateTempFile("preserve", ".docx");
        var model = CreateModel();
        model.Name = "Existing name";
        model.Type = "Custom type";

        try
        {
            Assert.True(model.TryApplyFile(filePath));
            Assert.Equal(filePath, model.FilePath);
            Assert.Equal("Existing name", model.Name);
            Assert.Equal("Custom type", model.Type);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void AddEdit_TryApplyFile_UnknownExtensionUsesOtherType()
    {
        var filePath = CreateTempFile("unknown", ".unknown");
        var model = CreateModel();

        try
        {
            Assert.True(model.TryApplyFile(filePath));
            Assert.Equal(filePath, model.FilePath);
            Assert.Equal(Path.GetFileNameWithoutExtension(filePath), model.Name);
            Assert.Equal("Other", model.Type);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task HandleDroppedFilesAsync_AddEditSingleFile_AppliesFileInsteadOfImporting()
    {
        var filePath = CreateTempFile("draft", ".pdf");
        var addEdit = CreateModel();
        var importService = new RecordingDroppedFileImportService();
        var mainWindow = BuildMainWindowModel(addEdit, importService);

        try
        {
            await mainWindow.HandleDroppedFilesAsync([filePath]);

            Assert.Equal(filePath, addEdit.FilePath);
            Assert.Equal(Path.GetFileNameWithoutExtension(filePath), addEdit.Name);
            Assert.Empty(importService.SavedDocuments);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task HandleDroppedFilesAsync_UnsupportedScreen_SetsDropStatus()
    {
        var filePath = CreateTempFile("draft", ".pdf");
        var mainWindow = BuildMainWindowModel(new FakeOtherViewModel(), new RecordingDroppedFileImportService());

        try
        {
            await mainWindow.HandleDroppedFilesAsync([filePath]);
            Assert.Equal("BatchImport_InvalidDrop", mainWindow.StatusText);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task HandleDroppedFilesAsync_InvalidFiles_SetsDropStatus()
    {
        var mainWindow = BuildMainWindowModel(CreateModel(), new RecordingDroppedFileImportService());

        await mainWindow.HandleDroppedFilesAsync(["Z:/missing-file.pdf"]);

        Assert.Equal("BatchImport_InvalidDrop", mainWindow.StatusText);
    }


    [Fact]
    public void SourceFilePath_ResolvesFromTestOutputDirectory()
    {
        var sourceFile = GetSourceFilePath("StudyDocumentManager", "Views", "MainWindow.axaml.cs");

        Assert.True(File.Exists(sourceFile));
        Assert.Equal("MainWindow.axaml.cs", Path.GetFileName(sourceFile));
    }

    [Fact]
    public void MainWindowCodeBehind_ChecksActualDraggedFiles()
    {
        var codeBehind = File.ReadAllText(GetSourceFilePath("StudyDocumentManager", "Views", "MainWindow.axaml.cs"));
        Assert.Contains("GetFiles()?", codeBehind);
        Assert.Contains("ShowInvalidDropStatus", codeBehind);
    }

    [Fact]
    public void MainWindow_StatisticsMenu_NoLongerUsesCtrlS()
    {
        var xaml = File.ReadAllText(GetSourceFilePath("StudyDocumentManager", "Views", "MainWindow.axaml"));
        Assert.DoesNotContain("CommandParameter=\"report\" InputGesture=\"Ctrl+S\"", xaml);
    }

    [Fact]
    public void BuildMainWindowModel_CanAcceptDroppedFilesOnlyForSupportedViews()
    {
        var mainWindow = BuildMainWindowModel(CreateModel(), new RecordingDroppedFileImportService());
        Assert.True(mainWindow.CanAcceptDroppedFiles);

        mainWindow.CurrentView = CreateBatchImportModel();
        Assert.True(mainWindow.CanAcceptDroppedFiles);

        mainWindow.CurrentView = new FakeOtherViewModel();
        Assert.False(mainWindow.CanAcceptDroppedFiles);
    }

    [Fact]
    public async Task HandleDroppedFilesAsync_BatchImportSingleFile_QueuesPreviewOnly()
    {
        var filePath = CreateTempFile("batch", ".pdf");
        var batchImport = CreateBatchImportModel();
        var importService = new RecordingDroppedFileImportService();
        var mainWindow = BuildMainWindowModel(batchImport, importService);

        try
        {
            await mainWindow.HandleDroppedFilesAsync([filePath]);

            Assert.Single(batchImport.Files);
            Assert.Equal(filePath, batchImport.Files[0].FilePath);
            Assert.Empty(importService.SavedDocuments);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task BatchImport_ImportException_AlwaysClearsIsImporting()
    {
        var model = CreateBatchImportModel(new ThrowingDroppedFileImportService());
        model.Files = new System.Collections.ObjectModel.ObservableCollection<FileImportItem>
        {
            new() { FileName = "Doc", FilePath = "C:/doc.pdf", FileType = "PDF", FileSizeMB = 1.2, IsSelected = true }
        };

        await model.ImportCommand.ExecuteAsync(null);

        Assert.False(model.IsImporting);
        Assert.Equal("BatchImport_FailuresRemain", model.ImportErrorMessage);
    }

    [Fact]
    public async Task BatchImport_PartialFailure_DeselectsSavedItemsBeforeRetry()
    {
        var importService = new PartialFailureDroppedFileImportService();
        var dialog = new RecordingDialogService();
        var navigation = new RecordingNavigationService();
        var model = CreateBatchImportModel(importService, dialog, navigation);
        model.Files = new System.Collections.ObjectModel.ObservableCollection<FileImportItem>
        {
            new() { FileName = "First", FilePath = "C:/first.pdf", FileType = "PDF", FileSizeMB = 1, IsSelected = true },
            new() { FileName = "Second", FilePath = "C:/second.pdf", FileType = "PDF", FileSizeMB = 1, IsSelected = true }
        };

        await model.ImportCommand.ExecuteAsync(null);

        Assert.Equal(1, model.ImportedCount);
        Assert.False(model.Files[0].IsSelected);
        Assert.True(model.Files[1].IsSelected);
        Assert.Equal("BatchImport_FailuresRemain", model.ImportErrorMessage);
        Assert.Empty(dialog.Messages);
        Assert.Empty(navigation.Routes);

        await model.ImportCommand.ExecuteAsync(null);

        Assert.Equal(["C:/first.pdf", "C:/second.pdf"], importService.SavedPaths);
        Assert.False(model.Files[1].IsSelected);
    }

    [Fact]
    public async Task BatchImport_MixedOutcomes_ContinuesAndRetainsOnlyFailuresForRetry()
    {
        var importService = new MixedOutcomeDroppedFileImportService();
        var dialog = new RecordingDialogService();
        var navigation = new RecordingNavigationService();
        var model = CreateBatchImportModel(importService, dialog, navigation);
        model.Files = new System.Collections.ObjectModel.ObservableCollection<FileImportItem>
        {
            new() { FileName = "Unique 1", FilePath = "C:/unique-1.pdf", FileType = "PDF", IsSelected = true },
            new() { FileName = "Active duplicate", FilePath = "C:/active-duplicate.pdf", FileType = "PDF", IsSelected = true },
            new() { FileName = "Deleted duplicate", FilePath = "C:/deleted-duplicate.pdf", FileType = "PDF", IsSelected = true },
            new() { FileName = "Unique 2", FilePath = "C:/unique-2.pdf", FileType = "PDF", IsSelected = true },
            new() { FileName = "Failed", FilePath = "C:/failed.pdf", FileType = "PDF", IsSelected = true }
        };

        await model.ImportCommand.ExecuteAsync(null);

        Assert.Equal(2, model.ImportedCount);
        Assert.Equal(2, model.SkippedDuplicateCount);
        Assert.Equal(1, model.FailedCount);
        Assert.All(model.Files.Take(4), item => Assert.False(item.IsSelected));
        Assert.True(model.Files[4].IsSelected);
        Assert.Equal("BatchImport_ResultSummary", model.ImportStatusMessage);
        Assert.Equal("BatchImport_FailuresRemain", model.ImportErrorMessage);
        Assert.Empty(dialog.Messages);
        Assert.Empty(navigation.Routes);
        Assert.Equal(5, importService.AttemptedPaths.Count);

        await model.ImportCommand.ExecuteAsync(null);

        Assert.Equal(1, model.ImportedCount);
        Assert.Equal(0, model.SkippedDuplicateCount);
        Assert.Equal(0, model.FailedCount);
        Assert.False(model.Files[4].IsSelected);
        Assert.Single(dialog.Messages);
        Assert.Equal(["dashboard"], navigation.Routes);
        Assert.Equal(6, importService.AttemptedPaths.Count);
    }

    [Fact]
    public async Task BatchImport_SaveReturnsFalse_ShowsErrorWithoutCompletionOrNavigation()
    {
        var dialog = new RecordingDialogService();
        var navigation = new RecordingNavigationService();
        var model = CreateBatchImportModel(new FailingDroppedFileImportService(), dialog, navigation);
        model.Files = new System.Collections.ObjectModel.ObservableCollection<FileImportItem>
        {
            new() { FileName = "Failed", FilePath = "C:/failed.pdf", FileType = "PDF", FileSizeMB = 1, IsSelected = true }
        };

        await model.ImportCommand.ExecuteAsync(null);

        Assert.Equal(0, model.ImportedCount);
        Assert.True(model.Files[0].IsSelected);
        Assert.Equal("BatchImport_FailuresRemain", model.ImportErrorMessage);
        Assert.Empty(dialog.Messages);
        Assert.Empty(navigation.Routes);
    }


    [Fact]
    public async Task BatchImport_ImportCommand_SetsPendingStatusBeforeWork()
    {
        var signal = new TaskCompletionSource();
        var gate = new TaskCompletionSource();
        var model = CreateBatchImportModel(new BlockingDroppedFileImportService(signal, gate));
        model.Files = new System.Collections.ObjectModel.ObservableCollection<FileImportItem>
        {
            new() { FileName = "Doc", FilePath = "C:/doc.pdf", FileType = "PDF", FileSizeMB = 1.2, IsSelected = true }
        };

        var run = model.ImportCommand.ExecuteAsync(null);
        await signal.Task;

        Assert.True(model.IsImporting);
        Assert.Equal("BatchImport_StatusImporting", model.ImportStatusMessage);

        gate.SetResult();
        await run;
    }

    [Fact]
    public async Task BatchImport_AddDroppedFilesAsync_DeduplicatesAndQueuesPreviewOnly()
    {
        var importService = new RecordingDroppedFileImportService();
        var model = CreateBatchImportModel(importService);
        var tempDir = Path.Combine(Path.GetTempPath(), $"slice4_drop_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var fileA = Path.Combine(tempDir, "a.pdf");
        var fileB = Path.Combine(tempDir, "b.docx");
        File.WriteAllBytes(fileA, [1, 2, 3, 4]);
        File.WriteAllBytes(fileB, [1, 2, 3, 4]);

        try
        {
            var added = await model.AddDroppedFilesAsync([fileA, fileA, fileB]);

            Assert.Equal(2, added);
            Assert.Equal(2, model.Files.Count);
            Assert.True(model.HasFiles);
            Assert.Equal([fileA, fileB], importService.BuiltPaths);
            Assert.Equal(["a", "b"], model.Files.Select(file => file.FileName));
            Assert.All(model.Files, file => Assert.Equal("PDF", file.FileType));
            Assert.All(model.Files, file => Assert.Equal(0.01, file.FileSizeMB, 3));
        }
        finally
        {
            File.Delete(fileA);
            File.Delete(fileB);
            Directory.Delete(tempDir);
        }
    }


    [Fact]
    public void TestMatrix_ReportsSlice4AProofSurface()
    {
        var matrix = File.ReadAllText(GetSourceFilePath("docs", "TEST_MATRIX.md"));
        Assert.Contains("| Add/Edit flow polish | Inline required-name validation, focus bridge, and atomic Add/Edit persistence | yes | yes | no | yes | implemented | `Slice4FlowPolishTests` proves model-state save/error flows, `DatabaseIntegrityTests` proves transactional rollback for add/edit catalog writes, and `AvaloniaBindingRegressionTests` provides headless focus/render proof |", matrix);
        Assert.Contains("700/700 xUnit pass in current Debug and Release verification", matrix);
        Assert.Contains("Current Debug build: 0 warnings, 0 errors; Release build: 0 warnings, 0 errors", matrix);
        Assert.Contains("| i18n Infrastructure | ResX multi-language support | yes | limited | no | limited | implemented | `LocalizationResourceIntegrityTests` verifies decoded vi/zh sample strings and Slice 4B keys; `Strings.vi.resx` and `Strings.zh.resx` parse cleanly after repair |", matrix);
        Assert.Contains("| Language Selector UI | Dropdown in MainWindow | yes | limited | no | limited | implemented | `MainWindowModel` loads/saves selected language, and `Slice4FlowPolishTests.MainWindow_LoadsSavedLanguageFromSettings` / `MainWindow_ChangeLanguage_PersistsSelectionToSettings` cover the model-level flow |", matrix);
        Assert.Contains("| Settings Persistence | app_settings table save/load | yes | limited | no | limited | implemented | `MainWindowModel` reads and writes `app_settings.language` through `ISettingsService`; `Slice4FlowPolishTests` verifies persisted selection load/save at the model layer |", matrix);
        Assert.Contains("| Drag/drop route control | Dashboard/AddEdit/BatchImport only; invalid screens rejected | yes | no | no | limited | implemented | `Slice4FlowPolishTests` model routing proof; shell event bridge remains desktop runtime code |", matrix);
        Assert.Contains("| Batch import pending cleanup | Import failures clear pending and retain unresolved selections; scan failures show inline error and clear preview | limited | limited | no | no | implemented | `Slice4FlowPolishTests` proves import pending/error/retry state; `DatabaseIntegrityTests` proves atomic production save rollback; scan-failure runtime path remains manual proof |", matrix);
    }

    [Fact]
    public void MainWindow_UsesStartupLanguageWithoutReReadingSettings()
    {
        var loc = new TestLocalizationService();
        loc.SetLanguage(SupportedLanguage.English);
        var settings = new FakeSettingsService(new Dictionary<string, string>
        {
            ["language"] = nameof(SupportedLanguage.Japanese)
        });

        var mainWindow = BuildMainWindowModel(
            CreateModel(),
            new RecordingDroppedFileImportService(),
            loc,
            settings);

        Assert.Equal(SupportedLanguage.English, mainWindow.SelectedLanguage);
        Assert.Equal(SupportedLanguage.English, loc.CurrentLanguage);
        Assert.Equal(SupportedLanguage.English, loc.LastSetLanguage);
    }

    [Fact]
    public void MainWindow_ChangeLanguage_PersistsSelectionToSettings()
    {
        var loc = new TestLocalizationService();
        var settings = new FakeSettingsService();
        var mainWindow = BuildMainWindowModel(
            CreateModel(),
            new RecordingDroppedFileImportService(),
            loc,
            settings);

        mainWindow.ChangeLanguageCommand.Execute(nameof(SupportedLanguage.Vietnamese));

        Assert.Equal(SupportedLanguage.Vietnamese, mainWindow.SelectedLanguage);
        Assert.Equal(SupportedLanguage.Vietnamese, loc.CurrentLanguage);
        Assert.Equal(nameof(SupportedLanguage.Vietnamese), settings.GetSetting("language"));
    }

    private static AddEditModel CreateModel(
        IDialogService? dialogService = null,
        IFileDialogService? fileDialogService = null,
        INavigationService? navigationService = null,
        IDocumentRepository? repository = null)
    {
        return new AddEditModel(
            repository ?? new FakeDocumentRepository(),
            new FakeCategoryRepository(),
            dialogService ?? new RecordingDialogService(),
            fileDialogService ?? new FakeFileDialogService(),
            navigationService ?? new RecordingNavigationService(),
            new TestLocalizationService());
    }


    private static BatchImportModel CreateBatchImportModel(
        IDroppedFileImportService? droppedFileImportService = null,
        RecordingDialogService? dialogService = null,
        RecordingNavigationService? navigationService = null)
    {
        return new BatchImportModel(
            dialogService ?? new RecordingDialogService(),
            new FakeFileDialogService(),
            navigationService ?? new RecordingNavigationService(),
            new TestLocalizationService(),
            droppedFileImportService ?? new RecordingDroppedFileImportService());
    }

    private static MainWindowModel BuildMainWindowModel(
        ModelBase currentView,
        IDroppedFileImportService? droppedFileImportService = null,
        ILocalizationService? localizationService = null,
        ISettingsService? settingsService = null)
    {
        localizationService ??= new TestLocalizationService();
        settingsService ??= new FakeSettingsService();

        var dashboard = new DashboardModel(
            new FakeDocumentRepository(),
            new FakeRecycleBinRepository(),
            new FakeCategoryRepository(),
            new FakeCollectionRepository(),
            new FakeRecentFileRepository(),
            new RecordingDialogService(),
            new FakeFileDialogService(),
            new FakeCustomDialogService(),
            new RecordingNavigationService(),
            new FakeClipboardService(),
            new FakeProcessLauncherService(),
            new FakeExportService(),
            new FakeBackupService(),
            localizationService);

        var mainWindow = new MainWindowModel(
            dashboard,
            new RecordingNavigationService(),
            new RecordingDialogService(),
            new FakeCustomDialogService(),
            droppedFileImportService ?? new RecordingDroppedFileImportService(),
            new FakeApplicationLifecycleService(),
            localizationService,
            settingsService,
            new FakeUpdateService());

        mainWindow.CurrentView = currentView;
        return mainWindow;
    }

    private static string CreateTempFile(string stem, string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"slice4_{stem}_{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, [1, 2, 3, 4]);
        return path;
    }

    private static string GetSourceFilePath(params string[] pathSegments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StudyDocumentManager.sln")))
                return Path.Combine(directory.FullName, Path.Combine(pathSegments));
        }

        throw new DirectoryNotFoundException("Could not locate the solution root.");
    }

    private sealed class RecordingDialogService : IDialogService
    {
        public List<string> Messages { get; } = [];

        public Task ShowMessageAsync(string title, string message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task ShowErrorAsync(string title, string message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<bool> ShowConfirmAsync(string title, string message) => Task.FromResult(false);

        public Task<bool> ShowConfirmAsync(string title, string message, string confirmText, bool isDanger = false)
            => Task.FromResult(false);

        public Task<string?> ShowInputAsync(string title, string label, string defaultValue = "", string watermark = "")
            => Task.FromResult<string?>(null);
    }

    private sealed class RecordingFileDialogService(string? path) : IFileDialogService
    {
        public string? Title { get; private set; }
        public string? Filter { get; private set; }

        public Task<string?> ShowOpenFileAsync(string title, string? filter = null)
        {
            Title = title;
            Filter = filter;
            return Task.FromResult(path);
        }

        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
    }

    private sealed class FakeFileDialogService(string? path = null) : IFileDialogService
    {
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult(path);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
    }

    private sealed class RecordingNavigationService : INavigationService
    {
        public List<string> Routes { get; } = [];
        public bool CanGoBack => false;

        public void NavigateTo(string viewKey) => Routes.Add(viewKey);
        public void NavigateTo(string viewKey, object? parameter) => Routes.Add(viewKey);
        public void GoBack() { }
    }

    private sealed class FakeCategoryRepository : ICategoryRepository
    {
        public List<string> GetAllSubjects() => [];
        public List<string> GetAllTypes() => [];
        public List<(string Name, int Count)> GetSubjectsWithCount() => [];
        public List<(string Name, int Count)> GetTypesWithCount() => [];
        public bool AddSubject(string name) => true;
        public bool AddType(string name) => true;
        public bool UpdateSubjectName(string oldName, string newName) => false;
        public bool UpdateTypeName(string oldName, string newName) => false;
        public bool DeleteDocumentsBySubject(string subjectName) => false;
        public bool DeleteDocumentsByType(string typeName) => false;
        public int GetTotalDocumentCount() => 0;
    }

    private sealed class RecordingDocumentRepository : IDocumentRepository
    {
        public StudyDocument? Document { get; init; }
        public bool AddResult { get; init; } = true;
        public bool UpdateResult { get; init; } = true;
        public Exception? UpdateException { get; init; }
        public List<StudyDocument> AddedDocuments { get; } = [];
        public List<StudyDocument> UpdatedDocuments { get; } = [];
        public int UpdateCallCount { get; private set; }

        public List<StudyDocument> GetAll() => [];
        public StudyDocument? GetById(int id) => Document?.Id == id ? Document : null;
        public List<StudyDocument> Search(string keyword) => [];
        public List<StudyDocument> Filter(string subject, string type) => [];
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => [];
        public bool Add(StudyDocument document) => throw new InvalidOperationException("Add should not be called");
        public bool AddWithCatalogs(StudyDocument document)
        {
            AddedDocuments.Add(document);
            return AddResult;
        }
        public bool Update(StudyDocument document)
        {
            UpdateCallCount++;
            if (UpdateException is not null)
                throw UpdateException;

            UpdatedDocuments.Add(document);
            return UpdateResult;
        }
        public bool Delete(int id) => true;
        public List<string> GetDistinctSubjects() => [];
        public List<string> GetDistinctTypes() => [];
        public List<string> GetDistinctTags() => [];
        public List<StudyDocument> GetUpcomingDeadlines(int days) => [];
        public List<StudyDocument> GetOverdueDocuments() => [];
        public void EnsureSubjectExists(string subject) { }
        public void EnsureTypeExists(string type) { }
    }

    private sealed class FakeDocumentRepository : IDocumentRepository
    {
        public List<StudyDocument> GetAll() => [];
        public StudyDocument? GetById(int id) => null;
        public List<StudyDocument> Search(string keyword) => [];
        public List<StudyDocument> Filter(string subject, string type) => [];
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => [];
        public bool Add(StudyDocument document) => true;
        public bool AddWithCatalogs(StudyDocument document) => true;
        public bool Update(StudyDocument document) => true;
        public bool Delete(int id) => true;
        public List<string> GetDistinctSubjects() => [];
        public List<string> GetDistinctTypes() => [];
        public List<string> GetDistinctTags() => [];
        public List<StudyDocument> GetUpcomingDeadlines(int days) => [];
        public List<StudyDocument> GetOverdueDocuments() => [];
        public void EnsureSubjectExists(string subject) { }
        public void EnsureTypeExists(string type) { }
    }

    private sealed class TestLocalizationService : ILocalizationService
    {
        public string this[string key] => key;
        public SupportedLanguage CurrentLanguage { get; private set; } = SupportedLanguage.Japanese;
        public SupportedLanguage? LastSetLanguage { get; private set; }

        public void SetLanguage(SupportedLanguage language)
        {
            CurrentLanguage = language;
            LastSetLanguage = language;
        }

        public IReadOnlyList<SupportedLanguage> AvailableLanguages { get; } = [];
        public event EventHandler? LanguageChanged { add { } remove { } }
    }


    private sealed class RecordingDroppedFileImportService : IDroppedFileImportService
    {
        public List<StudyDocument> SavedDocuments { get; } = [];
        public List<string> BuiltPaths { get; } = [];

        public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects) => fallbackSubjects.ToList();
        public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes) => fallbackTypes.ToList();
        public DocumentImportOutcome SaveDocument(StudyDocument document)
        {
            SavedDocuments.Add(document);
            return DocumentImportOutcome.Imported;
        }

        public StudyDocument BuildDocumentFromPath(string filePath)
        {
            BuiltPaths.Add(filePath);
            return new StudyDocument
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                FilePath = filePath,
                Type = "PDF",
                FileSize = 0.01
            };
        }
    }

    private sealed class AtomicAddDocumentRepository : IDocumentRepository
    {
        public List<StudyDocument> GetAll() => [];
        public StudyDocument? GetById(int id) => null;
        public List<StudyDocument> Search(string keyword) => [];
        public List<StudyDocument> Filter(string subject, string type) => [];
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => [];
        public bool Add(StudyDocument document) => false;
        public bool AddWithCatalogs(StudyDocument document) => true;
        public bool Update(StudyDocument document) => true;
        public bool Delete(int id) => true;
        public List<string> GetDistinctSubjects() => [];
        public List<string> GetDistinctTypes() => [];
        public List<string> GetDistinctTags() => [];
        public List<StudyDocument> GetUpcomingDeadlines(int days) => [];
        public List<StudyDocument> GetOverdueDocuments() => [];
        public void EnsureSubjectExists(string subject) { }
        public void EnsureTypeExists(string type) { }
    }

    private sealed class ThrowingDocumentRepository : IDocumentRepository
    {
        public List<StudyDocument> GetAll() => [];
        public StudyDocument? GetById(int id) => null;
        public List<StudyDocument> Search(string keyword) => [];
        public List<StudyDocument> Filter(string subject, string type) => [];
        public List<StudyDocument> SearchAdvanced(string keyword, string subject, string type, DateTime? fromDate, DateTime? toDate, double? minSize, double? maxSize, bool? isImportant) => [];
        public bool Add(StudyDocument document) => throw new InvalidOperationException("save failed");
        public bool AddWithCatalogs(StudyDocument document) => throw new InvalidOperationException("save failed");
        public bool Update(StudyDocument document) => throw new InvalidOperationException("save failed");
        public bool Delete(int id) => true;
        public List<string> GetDistinctSubjects() => [];
        public List<string> GetDistinctTypes() => [];
        public List<string> GetDistinctTags() => [];
        public List<StudyDocument> GetUpcomingDeadlines(int days) => [];
        public List<StudyDocument> GetOverdueDocuments() => [];
        public void EnsureSubjectExists(string subject) { }
        public void EnsureTypeExists(string type) { }
    }

    private sealed class ThrowingCategoryRepository : ICategoryRepository
    {
        public List<string> GetAllSubjects() => [];
        public List<string> GetAllTypes() => [];
        public List<(string Name, int Count)> GetSubjectsWithCount() => [];
        public List<(string Name, int Count)> GetTypesWithCount() => [];
        public bool AddSubject(string name) => throw new InvalidOperationException("category write should not be called");
        public bool AddType(string name) => throw new InvalidOperationException("type write should not be called");
        public bool UpdateSubjectName(string oldName, string newName) => false;
        public bool UpdateTypeName(string oldName, string newName) => false;
        public bool DeleteDocumentsBySubject(string subjectName) => false;
        public bool DeleteDocumentsByType(string typeName) => false;
        public int GetTotalDocumentCount() => 0;
    }

    private sealed class ThrowingDroppedFileImportService : IDroppedFileImportService
    {
        public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects) => fallbackSubjects.ToList();
        public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes) => fallbackTypes.ToList();
        public DocumentImportOutcome SaveDocument(StudyDocument document) => throw new IOException("save failed");
        public StudyDocument BuildDocumentFromPath(string filePath)
            => new()
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                FilePath = filePath,
                Type = "PDF",
                FileSize = 0.01
            };
    }


    private sealed class PartialFailureDroppedFileImportService : IDroppedFileImportService
    {
        private int _saveAttempts;

        public List<string> SavedPaths { get; } = [];

        public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects) => fallbackSubjects.ToList();
        public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes) => fallbackTypes.ToList();

        public DocumentImportOutcome SaveDocument(StudyDocument document)
        {
            _saveAttempts++;
            if (_saveAttempts == 2)
                throw new IOException("save failed");

            SavedPaths.Add(document.FilePath);
            return DocumentImportOutcome.Imported;
        }

        public StudyDocument BuildDocumentFromPath(string filePath) => throw new NotImplementedException();
    }


    private sealed class MixedOutcomeDroppedFileImportService : IDroppedFileImportService
    {
        private bool _failedOnce;

        public List<string> AttemptedPaths { get; } = [];

        public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects) => fallbackSubjects.ToList();
        public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes) => fallbackTypes.ToList();

        public DocumentImportOutcome SaveDocument(StudyDocument document)
        {
            AttemptedPaths.Add(document.FilePath);
            return document.FilePath switch
            {
                "C:/active-duplicate.pdf" or "C:/deleted-duplicate.pdf" => DocumentImportOutcome.SkippedDuplicate,
                "C:/failed.pdf" when !_failedOnce => FailOnce(),
                _ => DocumentImportOutcome.Imported
            };
        }

        public StudyDocument BuildDocumentFromPath(string filePath) => throw new NotImplementedException();

        private DocumentImportOutcome FailOnce()
        {
            _failedOnce = true;
            return DocumentImportOutcome.Failed;
        }
    }

    private sealed class FailingDroppedFileImportService : IDroppedFileImportService
    {
        public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects) => fallbackSubjects.ToList();
        public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes) => fallbackTypes.ToList();
        public DocumentImportOutcome SaveDocument(StudyDocument document) => DocumentImportOutcome.Failed;
        public StudyDocument BuildDocumentFromPath(string filePath) => throw new NotImplementedException();
    }


    private sealed class BlockingDroppedFileImportService(TaskCompletionSource signal, TaskCompletionSource gate) : IDroppedFileImportService
    {
        public List<string> GetAvailableSubjects(IReadOnlyList<string> fallbackSubjects) => fallbackSubjects.ToList();
        public List<string> GetAvailableTypes(IReadOnlyList<string> fallbackTypes) => fallbackTypes.ToList();
        public DocumentImportOutcome SaveDocument(StudyDocument document)
        {
            signal.TrySetResult();
            gate.Task.GetAwaiter().GetResult();
            return DocumentImportOutcome.Imported;
        }
        public StudyDocument BuildDocumentFromPath(string filePath)
            => new()
            {
                Name = Path.GetFileNameWithoutExtension(filePath),
                FilePath = filePath,
                Type = "PDF",
                FileSize = 0.01
            };
    }

    private sealed class FakeOtherViewModel : ModelBase;

    private sealed class FakeRecycleBinRepository : IRecycleBinRepository
    {
        public List<StudyDocument> GetDeletedDocuments() => [];
        public bool RestoreDocument(int id) => false;
        public bool PermanentDeleteDocument(int id) => false;
        public int EmptyRecycleBin() => 0;
        public int GetDeletedDocumentCount() => 0;
    }

    private sealed class FakeCollectionRepository : ICollectionRepository
    {
        public List<(int Id, string Name, string? Description, DateTime CreatedAt, int ItemCount)> GetAll() => [];
        public int Create(string name, string? description = null) => 0;
        public bool Update(int id, string name, string? description = null) => false;
        public bool Delete(int id) => false;
        public List<StudyDocument> GetDocuments(int collectionId) => [];
        public bool AddDocument(int collectionId, int documentId) => false;
        public bool RemoveDocument(int collectionId, int documentId) => false;
    }

    private sealed class FakeRecentFileRepository : IRecentFileRepository
    {
        public List<(int Id, string Name, string? Subject, string? Type, string? FilePath, DateTime OpenedAt)> GetAll() => [];
        public bool Add(int documentId) => true;
        public void Clear() { }
    }

    private sealed class FakeCustomDialogService : ICustomDialogService
    {
        public Task<string?> ShowOpenFileAsync(string title, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowOpenFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<string?> ShowSaveFileAsync(string title, string defaultFileName, string? filter = null) => Task.FromResult<string?>(null);
        public Task<string?> ShowChangeCategoryAsync(string documentName, IList<string> existingCategories, string currentCategory) => Task.FromResult<string?>(null);
        public Task<StudyDocumentManager.Core.DTOs.AddDocumentDraft?> ShowAddDocumentAsync(string filePath, IList<string> subjects, IList<string> types) => Task.FromResult<StudyDocumentManager.Core.DTOs.AddDocumentDraft?>(null);
        public Task<List<StudyDocument>?> ShowDocumentPickerAsync(string collectionName, IEnumerable<StudyDocument> allDocuments, IEnumerable<int> alreadyInCollection) => Task.FromResult<List<StudyDocument>?>(null);
        public Task<int> ShowSelectCollectionAsync(string documentName, IList<(int Id, string Name, int DocCount)> collections) => Task.FromResult(-1);
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public Task SetTextAsync(string text) => Task.CompletedTask;
    }

    private sealed class FakeProcessLauncherService : IProcessLauncherService
    {
        public void OpenFile(string filePath) { }
        public void OpenFolderAndSelect(string filePath) { }
        public void RevealInExplorer(string filePath) { }
        public void OpenUrl(string url) { }
    }

    private sealed class FakeExportService : IExportService
    {
        public Task<ExportResult> ExportCsvAsync(IReadOnlyList<StudyDocument> documents, string? suggestedFileName) => Task.FromResult(new ExportResult(false));
    }

    private sealed class FakeBackupService : IBackupService
    {
        public Task<(bool Success, string? Path, string? Error)> BackupAsync() => Task.FromResult((false, (string?)null, (string?)null));
        public Task<(bool Success, string? Error)> RestoreAsync() => Task.FromResult((false, (string?)null));
    }

    private sealed class FakeApplicationLifecycleService : IApplicationLifecycleService
    {
        public void Shutdown() { }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly Dictionary<string, string> _values;

        public FakeSettingsService(Dictionary<string, string>? values = null)
        {
            _values = values ?? [];
        }

        public string? GetSetting(string key) => _values.TryGetValue(key, out var value) ? value : null;

        public void SetSetting(string key, string value)
        {
            _values[key] = value;
        }
    }

    private sealed class FakeUpdateService : IUpdateService
    {
        public Task<Core.DTOs.UpdateInfo?> CheckForUpdateAsync() => Task.FromResult<Core.DTOs.UpdateInfo?>(null);
        public Task CheckSilentlyAsync() => Task.CompletedTask;
        public Task HandleUpdateAsync(Core.DTOs.UpdateInfo update) => Task.CompletedTask;
    }
}

