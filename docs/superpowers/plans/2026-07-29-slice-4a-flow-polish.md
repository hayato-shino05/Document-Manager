# Slice 4A Flow Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve Add/Edit, drag and drop, shortcut routing, and Batch Import feedback so the import workflow is deterministic, validated, and testable without changing the Dashboard layout.

**Architecture:** Keep the current MVVM and DI structure. Add the smallest possible state to existing models for inline validation and pending feedback, route shell-level shortcut and drop events through `MainWindow` and `MainWindowModel`, and reuse `IDroppedFileImportService` as the canonical save path for imported documents.

**Tech Stack:** Avalonia 11.2.7, CommunityToolkit.Mvvm, xUnit, Avalonia.Headless.XUnit, Microsoft.Extensions.DependencyInjection

## Global Constraints

- Giữ MVVM, DI, repository boundary và Avalonia 11.2.7 hiện có.
- Không global compiled-binding migration, không thêm UI framework, không database redesign.
- Giữ layout Dashboard hiện tại.
- Chỉ viết comment khi thực sự cần thiết; nếu buộc phải thêm comment thì dùng tiếng Nhật.
- Dùng lại pattern và service hiện có trước khi thêm state hoặc abstraction mới.
- Mọi chuỗi user-visible mới phải được thêm đồng bộ vào `Strings.resx`, `Strings.en.resx`, `Strings.vi.resx`, `Strings.zh.resx`.
- TDD: test fail trước, rồi code tối thiểu để pass.
- Không commit, push, deploy hoặc thao tác dữ liệu người dùng.

---

## File Structure

### Files to modify

- `StudyDocumentManager/Models/AddEditModel.cs`
  - Owner of Add/Edit validation state, save behavior, and file autofill logic reusable by drag/drop.
- `StudyDocumentManager/Views/AddEdit.axaml`
  - Owner of inline validation message, named controls, and Save button state.
- `StudyDocumentManager/Views/AddEdit.axaml.cs`
  - Thin Avalonia focus bridge only.
- `StudyDocumentManager/Models/BatchImportModel.cs`
  - Owner of batch import pending/error state and safe scan/import flow.
- `StudyDocumentManager/Views/BatchImport.axaml`
  - Owner of pending/progress/error UI for batch import.
- `StudyDocumentManager/Views/MainWindow.axaml`
  - Owner of shell gesture definitions; remove Statistics `Ctrl+S` conflict here.
- `StudyDocumentManager/Views/MainWindow.axaml.cs`
  - Owner of shell keyboard and drag/drop event bridge.
- `StudyDocumentManager/Models/MainWindowModel.cs`
  - Owner of active-view-based drop routing and dashboard refresh after import.
- `StudyDocumentManager/Views/AddDocumentDialog.axaml`
  - Owner of one-file import inline validation UI.
- `StudyDocumentManager/Views/AddDocumentDialog.axaml.cs`
  - Owner of autofocus and inline error reset in one-file import dialog.
- `StudyDocumentManager/Resources/Strings.resx`
- `StudyDocumentManager/Resources/Strings.en.resx`
- `StudyDocumentManager/Resources/Strings.vi.resx`
- `StudyDocumentManager/Resources/Strings.zh.resx`
  - Owner of new validation/pending/drop-route messages.
- `StudyDocumentManager.Tests/AvaloniaBindingRegressionTests.cs`
  - Existing headless UI regression surface for Add/Edit.

### Files to create

- `StudyDocumentManager.Tests/Slice4FlowPolishTests.cs`
  - Focused model/UI tests for Add/Edit validation, shell shortcut precedence, drag/drop routing, and Batch Import cleanup.

### Existing reusable dependencies

- `StudyDocumentManager/Services/IDroppedFileImportService.cs`
- `StudyDocumentManager/Services/DroppedFileImportService.cs`
  - Canonical save path for imported documents; Batch Import must reuse this instead of calling repository directly.
- `StudyDocumentManager/Services/DialogService.cs`
  - Existing dialog boundary; no new dialog service needed.
- `StudyDocumentManager/Models/DashboardModel.cs`
  - Existing Dashboard refresh command after imports; do not change layout or backup/CSV logic in this slice.

## Task 1: Lock Add/Edit validation and one-file dialog behavior

**Files:**
- Modify: `StudyDocumentManager/Models/AddEditModel.cs`
- Modify: `StudyDocumentManager/Views/AddEdit.axaml`
- Modify: `StudyDocumentManager/Views/AddEdit.axaml.cs`
- Modify: `StudyDocumentManager/Views/AddDocumentDialog.axaml`
- Modify: `StudyDocumentManager/Views/AddDocumentDialog.axaml.cs`
- Modify: `StudyDocumentManager/Resources/Strings.resx`
- Modify: `StudyDocumentManager/Resources/Strings.en.resx`
- Modify: `StudyDocumentManager/Resources/Strings.vi.resx`
- Modify: `StudyDocumentManager/Resources/Strings.zh.resx`
- Modify: `StudyDocumentManager.Tests/AvaloniaBindingRegressionTests.cs`
- Test: `StudyDocumentManager.Tests/Slice4FlowPolishTests.cs`

**Interfaces:**
- Consumes:
  - `IDialogService.ShowErrorAsync(string title, string message)`
  - `IFileDialogService.ShowOpenFileAsync(string title, string? filter = null)`
  - `INavigationService.NavigateTo(string route)`
- Produces:
  - `AddEditModel.HasNameValidationError : bool`
  - `AddEditModel.NameValidationMessage : string`
  - `AddEditModel.TryApplyFile(string filePath) : bool`
  - `AddEdit` name textbox with `x:Name="txtName"`
  - `AddDocumentDialog` inline validation text block and autofocus behavior

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task AddEdit_SaveBlankName_SetsInlineValidationInsteadOfNavigating()
{
    var dialog = new RecordingDialogService();
    var navigation = new RecordingNavigationService();
    var model = new AddEditModel(
        new FakeDocumentRepository(),
        new FakeCategoryRepository(),
        dialog,
        new FakeFileDialogService(),
        navigation,
        new TestLocalizationService())
    {
        Name = "   "
    };

    await model.SaveCommand.ExecuteAsync(null);

    Assert.True(model.HasNameValidationError);
    Assert.Equal("AddEdit_NameRequired", model.NameValidationMessage);
    Assert.Empty(navigation.Routes);
    Assert.Empty(dialog.Messages);
}

[Fact]
public void AddEdit_TryApplyFile_FillsPathNameAndTypeWhenAppropriate()
{
    var model = new AddEditModel(
        new FakeDocumentRepository(),
        new FakeCategoryRepository(),
        new RecordingDialogService(),
        new FakeFileDialogService(),
        new RecordingNavigationService(),
        new TestLocalizationService());

    var tempFile = CreateTempFile("notes", ".pdf");
    try
    {
        var result = model.TryApplyFile(tempFile);

        Assert.True(result);
        Assert.Equal(tempFile, model.FilePath);
        Assert.Equal("notes", model.Name);
        Assert.Equal("PDF", model.Type);
        Assert.False(model.HasNameValidationError);
    }
    finally
    {
        File.Delete(tempFile);
    }
}

[AvaloniaFact]
public void AddEdit_ShowsInlineErrorTextWhenValidationFails()
{
    var model = new AddEditModel(
        new FakeDocumentRepository(),
        new FakeCategoryRepository(),
        new RecordingDialogService(),
        new FakeFileDialogService(),
        new RecordingNavigationService(),
        new TestLocalizationService())
    {
        NameValidationMessage = "AddEdit_NameRequired"
    };
    model.HasNameValidationError = true;

    var view = new AddEdit { DataContext = model };
    var window = new Window { Content = view };

    try
    {
        window.Show();
        Assert.Contains(
            view.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == "AddEdit_NameRequired");
    }
    finally
    {
        window.Close();
    }
}

[Fact]
public void AddDocumentDialog_BlankName_StaysOpenAndSetsInlineError()
{
    var dialog = new AddDocumentDialog("C:/drop/test.pdf", ["Study"], ["PDF"]);
    dialog.Show();
    dialog.FindControl<TextBox>("txtTen")!.Text = " ";

    dialog.GetType().GetMethod("OnSaveClick", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(dialog, [null, null]);

    var error = dialog.FindControl<TextBlock>("txtNameError");
    Assert.NotNull(error);
    Assert.False(string.IsNullOrWhiteSpace(error!.Text));
    dialog.Close();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Debug --filter "FullyQualifiedName~Slice4FlowPolishTests|FullyQualifiedName~AvaloniaBindingRegressionTests"`
Expected: FAIL because `HasNameValidationError`, `NameValidationMessage`, `TryApplyFile`, `txtName`, and dialog inline error UI do not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
// AddEditModel.cs
[ObservableProperty] private bool _hasNameValidationError;
[ObservableProperty] private string _nameValidationMessage = string.Empty;

partial void OnNameChanged(string value)
{
    if (!string.IsNullOrWhiteSpace(value) && HasNameValidationError)
    {
        HasNameValidationError = false;
        NameValidationMessage = string.Empty;
    }
}

private bool ValidateName()
{
    if (!string.IsNullOrWhiteSpace(Name))
    {
        HasNameValidationError = false;
        NameValidationMessage = string.Empty;
        return true;
    }

    HasNameValidationError = true;
    NameValidationMessage = _loc["AddEdit_NameRequired"];
    return false;
}

public bool TryApplyFile(string filePath)
{
    if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        return false;

    FilePath = filePath;
    if (string.IsNullOrWhiteSpace(Name))
        Name = Path.GetFileNameWithoutExtension(filePath);

    if (string.IsNullOrWhiteSpace(Type))
    {
        var detectedType = FileTypeDetector.Detect(Path.GetExtension(filePath));
        Type = detectedType;
        if (!string.IsNullOrWhiteSpace(detectedType) && !Types.Contains(detectedType))
            Types.Add(detectedType);
    }

    HasNameValidationError = false;
    NameValidationMessage = string.Empty;
    return true;
}

private async Task SaveAsync()
{
    if (!ValidateName())
        return;
    // existing save body unchanged
}

private async Task BrowseFileAsync()
{
    var path = await _fileDialogService.ShowOpenFileAsync(_loc["AddEdit_BrowseFile"], _loc["AddEdit_FileFilter"]);
    if (string.IsNullOrEmpty(path))
        return;

    TryApplyFile(path);
}
```

```xml
<!-- AddEdit.axaml -->
<TextBox x:Name="txtName"
         Text="{Binding Name, Mode=TwoWay}"
         FontSize="{StaticResource FontSizeLg}"
         Watermark="{loc:Localize AddEdit_PlaceholderDocName}"
         Height="32"/>
<TextBlock Text="{Binding NameValidationMessage}"
           IsVisible="{Binding HasNameValidationError}"
           Foreground="{StaticResource SemanticDangerBrush}"
           FontSize="{StaticResource FontSizeSm}"/>
```

```csharp
// AddEdit.axaml.cs
public AddEdit()
{
    InitializeComponent();
    DataContextChanged += (_, _) => WireValidationFocus();
}

private void WireValidationFocus()
{
    if (DataContext is not AddEditModel model)
        return;

    model.PropertyChanged -= OnModelPropertyChanged;
    model.PropertyChanged += OnModelPropertyChanged;
}

private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(AddEditModel.HasNameValidationError)
        && sender is AddEditModel { HasNameValidationError: true })
    {
        FindControl<TextBox>("txtName")?.Focus();
    }
}
```

```xml
<!-- AddDocumentDialog.axaml -->
<TextBox Name="txtTen" FontSize="{StaticResource FontSizeMd}"/>
<TextBlock Name="txtNameError"
           IsVisible="False"
           Foreground="{StaticResource SemanticDangerBrush}"
           FontSize="{StaticResource FontSizeSm}"/>
```

```csharp
// AddDocumentDialog.axaml.cs
public AddDocumentDialog(string filePath, IList<string> subjects, IList<string> types) : this()
{
    // existing setup
    Opened += (_, _) => txtTen.Focus();
}

private void OnSaveClick(object? sender, RoutedEventArgs e)
{
    var name = txtTen?.Text?.Trim();
    if (string.IsNullOrWhiteSpace(name))
    {
        txtNameError!.Text = "AddEdit_NameRequired";
        txtNameError.IsVisible = true;
        txtTen?.Focus();
        return;
    }

    txtNameError!.IsVisible = false;
    // existing result creation
}
```

Add four new resx keys if missing and only if no existing key fits:
- `AddEdit_NameRequiredInline`
- `BatchImport_InvalidDrop`
- `BatchImport_ScanError`
- `BatchImport_StatusImporting`

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Debug --filter "FullyQualifiedName~Slice4FlowPolishTests|FullyQualifiedName~AvaloniaBindingRegressionTests"`
Expected: PASS for Add/Edit validation, file autofill, and dialog inline-error tests.

- [ ] **Step 5: Commit**

```bash
git add StudyDocumentManager/Models/AddEditModel.cs StudyDocumentManager/Views/AddEdit.axaml StudyDocumentManager/Views/AddEdit.axaml.cs StudyDocumentManager/Views/AddDocumentDialog.axaml StudyDocumentManager/Views/AddDocumentDialog.axaml.cs StudyDocumentManager/Resources/Strings.resx StudyDocumentManager/Resources/Strings.en.resx StudyDocumentManager/Resources/Strings.vi.resx StudyDocumentManager/Resources/Strings.zh.resx StudyDocumentManager.Tests/AvaloniaBindingRegressionTests.cs StudyDocumentManager.Tests/Slice4FlowPolishTests.cs
git commit -m "feat: add inline validation for add edit flow"
```

## Task 2: Make shell shortcuts and drop routing deterministic

**Files:**
- Modify: `StudyDocumentManager/Views/MainWindow.axaml`
- Modify: `StudyDocumentManager/Views/MainWindow.axaml.cs`
- Modify: `StudyDocumentManager/Models/MainWindowModel.cs`
- Test: `StudyDocumentManager.Tests/Slice4FlowPolishTests.cs`

**Interfaces:**
- Consumes:
  - `MainWindowModel.CurrentView : ModelBase`
  - `AddEditModel.SaveCommand : IAsyncRelayCommand`
  - `AddEditModel.TryApplyFile(string filePath) : bool`
  - `BatchImportModel` file queue API from Task 3
- Produces:
  - `MainWindow` handles `Ctrl+S` for `AddEditModel` before base routing
  - `MainWindow.OnDragOver` only advertises drop on Dashboard/AddEdit/BatchImport
  - `MainWindowModel.HandleDroppedFilesAsync(IReadOnlyList<string>)` rejects unsupported screens and invalid files deterministically

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task HandleDroppedFilesAsync_AddEditSingleFile_AppliesFileInsteadOfImporting()
{
    var filePath = CreateTempFile("draft", ".pdf");
    var addEdit = new AddEditModel(
        new FakeDocumentRepository(),
        new FakeCategoryRepository(),
        new RecordingDialogService(),
        new FakeFileDialogService(),
        new RecordingNavigationService(),
        new TestLocalizationService());
    var mainWindow = BuildMainWindowModel(addEdit);

    try
    {
        await mainWindow.HandleDroppedFilesAsync([filePath]);

        Assert.Equal(filePath, addEdit.FilePath);
        Assert.Equal("draft", addEdit.Name);
        Assert.Equal(0, mainWindow.ImportedCountForTest);
    }
    finally
    {
        File.Delete(filePath);
    }
}

[Fact]
public async Task HandleDroppedFilesAsync_UnsupportedScreen_ShowsNoImportAndNoCrash()
{
    var mainWindow = BuildMainWindowModel(new ReportModelStub());
    var filePath = CreateTempFile("draft", ".pdf");

    try
    {
        await mainWindow.HandleDroppedFilesAsync([filePath]);
        Assert.Equal("BatchImport_InvalidDrop", mainWindow.LastDropStatusForTest);
    }
    finally
    {
        File.Delete(filePath);
    }
}

[Fact]
public void MainWindow_StatisticsMenu_NoLongerUsesCtrlS()
{
    var xaml = File.ReadAllText(@"D:/Github-Project/study-document-manager/StudyDocumentManager/Views/MainWindow.axaml");
    Assert.DoesNotContain("Menu_Statistics\" Command=\"{Binding NavigateCommand}\" CommandParameter=\"report\" InputGesture=\"Ctrl+S\"", xaml);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Debug --filter "FullyQualifiedName~Slice4FlowPolishTests"`
Expected: FAIL because AddEdit route, unsupported-screen route, and Statistics shortcut removal are not implemented.

- [ ] **Step 3: Write minimal implementation**

```xml
<!-- MainWindow.axaml -->
<MenuItem Header="{loc:Localize Menu_Statistics}"
          Command="{Binding NavigateCommand}"
          CommandParameter="report"/>
```

```csharp
// MainWindow.axaml.cs
protected override void OnKeyDown(KeyEventArgs e)
{
    if (DataContext is MainWindowModel { CurrentView: AddEditModel addEdit }
        && e.KeyModifiers == KeyModifiers.Control
        && e.Key == Key.S)
    {
        addEdit.SaveCommand.Execute(null);
        e.Handled = true;
        return;
    }

    var vm = DataContext as MainWindowModel;
    if (vm?.CurrentView is DashboardModel dashboard)
    {
        // existing dashboard shortcuts
    }

    base.OnKeyDown(e);
}

private void OnDragOver(object? sender, DragEventArgs e)
{
    if (DataContext is not MainWindowModel vm || !e.Data.Contains(DataFormats.Files))
    {
        e.DragEffects = DragDropEffects.None;
        e.Handled = true;
        return;
    }

    e.DragEffects = vm.CanAcceptDroppedFiles
        ? DragDropEffects.Copy
        : DragDropEffects.None;
    e.Handled = true;
}

private async void OnDrop(object? sender, DragEventArgs e)
{
    try
    {
        // existing file extraction
        await vm.HandleDroppedFilesAsync(filePaths);
    }
    catch
    {
        // boundary should swallow after model translated state
    }
    finally
    {
        e.Handled = true;
    }
}
```

```csharp
// MainWindowModel.cs
public bool CanAcceptDroppedFiles => CurrentView is DashboardModel or AddEditModel or BatchImportModel;

public async Task HandleDroppedFilesAsync(IReadOnlyList<string> filePaths)
{
    if (filePaths.Count == 0)
        return;

    var validPaths = filePaths
        .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (validPaths.Count == 0)
    {
        LastDropStatusForTest = _loc["BatchImport_InvalidDrop"];
        return;
    }

    switch (CurrentView)
    {
        case AddEditModel addEdit when validPaths.Count == 1:
            addEdit.TryApplyFile(validPaths[0]);
            return;

        case BatchImportModel batchImport:
            await batchImport.AddDroppedFilesAsync(validPaths);
            return;

        case DashboardModel dashboard:
            break;

        default:
            LastDropStatusForTest = _loc["BatchImport_InvalidDrop"];
            return;
    }

    // existing dashboard import logic
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Debug --filter "FullyQualifiedName~Slice4FlowPolishTests"`
Expected: PASS for Add/Edit routing, unsupported-screen rejection, and Statistics shortcut conflict removal.

- [ ] **Step 5: Commit**

```bash
git add StudyDocumentManager/Views/MainWindow.axaml StudyDocumentManager/Views/MainWindow.axaml.cs StudyDocumentManager/Models/MainWindowModel.cs StudyDocumentManager.Tests/Slice4FlowPolishTests.cs
git commit -m "feat: make shell shortcut and drop routing deterministic"
```

## Task 3: Harden Batch Import and reuse the shared drop-save path

**Files:**
- Modify: `StudyDocumentManager/Models/BatchImportModel.cs`
- Modify: `StudyDocumentManager/Views/BatchImport.axaml`
- Modify: `StudyDocumentManager/App.axaml.cs`
- Test: `StudyDocumentManager.Tests/Slice4FlowPolishTests.cs`

**Interfaces:**
- Consumes:
  - `IDroppedFileImportService.GetAvailableSubjects(IReadOnlyList<string>)`
  - `IDroppedFileImportService.GetAvailableTypes(IReadOnlyList<string>)`
  - `IDroppedFileImportService.SaveDocument(StudyDocument document)`
  - `BatchImportModel.AddDroppedFilesAsync(IReadOnlyList<string>)` from Task 2
- Produces:
  - `BatchImportModel.ImportStatusMessage : string`
  - `BatchImportModel.ImportErrorMessage : string`
  - `BatchImportModel.HasFiles : bool`
  - `BatchImportModel.AddDroppedFilesAsync(IReadOnlyList<string> filePaths) : Task<int>`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task BatchImport_ImportException_AlwaysClearsIsImporting()
{
    var service = new ThrowingDroppedFileImportService();
    var model = BuildBatchImportModel(service);
    model.Files = new ObservableCollection<FileImportItem>
    {
        new() { FileName = "Doc", FilePath = "C:/doc.pdf", FileType = "PDF", FileSizeMB = 1.2, IsSelected = true }
    };

    await model.ImportCommand.ExecuteAsync(null);

    Assert.False(model.IsImporting);
    Assert.Equal("BatchImport_ScanError", model.ImportErrorMessage);
}

[Fact]
public async Task BatchImport_AddDroppedFilesAsync_DeduplicatesAndQueuesPreviewOnly()
{
    var model = BuildBatchImportModel(new RecordingDroppedFileImportService());
    var fileA = CreateTempFile("a", ".pdf");
    var fileB = CreateTempFile("b", ".docx");

    try
    {
        var added = await model.AddDroppedFilesAsync([fileA, fileA, fileB]);

        Assert.Equal(2, added);
        Assert.Equal(2, model.Files.Count);
        Assert.Equal(["a", "b"], model.Files.Select(file => file.FileName));
    }
    finally
    {
        File.Delete(fileA);
        File.Delete(fileB);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Debug --filter "FullyQualifiedName~Slice4FlowPolishTests"`
Expected: FAIL because Batch Import has no `ImportStatusMessage`, `ImportErrorMessage`, `AddDroppedFilesAsync`, and does not reuse `IDroppedFileImportService`.

- [ ] **Step 3: Write minimal implementation**

```csharp
// App.axaml.cs registration / constructor chain
services.AddSingleton<IDroppedFileImportService, DroppedFileImportService>();
```

```csharp
// BatchImportModel.cs
private readonly IDroppedFileImportService _droppedFileImportService;
[ObservableProperty] private string _importStatusMessage = string.Empty;
[ObservableProperty] private string _importErrorMessage = string.Empty;
public bool HasFiles => Files.Count > 0;

public BatchImportModel(
    IDocumentRepository repository,
    IDialogService dialogService,
    IFileDialogService fileDialogService,
    INavigationService navigationService,
    ILocalizationService loc,
    IDroppedFileImportService droppedFileImportService)
{
    // existing assignments
    _droppedFileImportService = droppedFileImportService;
}

private void ScanFolder()
{
    if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath) || IsImporting)
        return;

    ImportErrorMessage = string.Empty;
    try
    {
        // existing scan logic
        OnPropertyChanged(nameof(HasFiles));
    }
    catch
    {
        Files.Clear();
        ImportErrorMessage = _loc["BatchImport_ScanError"];
        OnPropertyChanged(nameof(HasFiles));
    }
}

public Task<int> AddDroppedFilesAsync(IReadOnlyList<string> filePaths)
{
    var added = 0;
    ImportErrorMessage = string.Empty;

    foreach (var filePath in filePaths.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        if (Files.Any(file => string.Equals(file.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
            continue;

        try
        {
            var document = _droppedFileImportService.BuildDocumentFromPath(filePath);
            Files.Add(new FileImportItem
            {
                FileName = document.Name,
                FilePath = document.FilePath,
                FileType = document.Type,
                FileSizeMB = document.FileSize ?? 0,
                IsSelected = true
            });
            added++;
        }
        catch
        {
            ImportErrorMessage = _loc["BatchImport_ScanError"];
        }
    }

    OnPropertyChanged(nameof(HasFiles));
    return Task.FromResult(added);
}

private async Task ImportAsync()
{
    if (IsImporting)
        return;

    var selected = Files.Where(file => file.IsSelected).ToList();
    if (selected.Count == 0)
    {
        await _dialogService.ShowErrorAsync(_loc["Dialog_Error"], _loc["Import_NoFileSelected"]);
        return;
    }

    IsImporting = true;
    ImportErrorMessage = string.Empty;
    ImportStatusMessage = _loc["BatchImport_StatusImporting"];
    ImportedCount = 0;

    try
    {
        foreach (var item in selected)
        {
            var document = new StudyDocument
            {
                Name = item.FileName,
                Subject = DefaultSubject,
                Type = item.FileType,
                FilePath = item.FilePath,
                FileSize = item.FileSizeMB
            };

            if (_droppedFileImportService.SaveDocument(document))
                ImportedCount++;
        }

        ImportStatusMessage = string.Format(_loc["Import_Done"], ImportedCount, selected.Count);
        await _dialogService.ShowMessageAsync(_loc["Dialog_Complete"], ImportStatusMessage);
        _navigationService.NavigateTo("dashboard");
    }
    catch
    {
        ImportErrorMessage = _loc["BatchImport_ScanError"];
    }
    finally
    {
        IsImporting = false;
    }
}
```

```xml
<!-- BatchImport.axaml -->
<TextBlock Text="{Binding ImportStatusMessage}"
           IsVisible="{Binding IsImporting}"
           Foreground="{StaticResource TextSecondary}"/>
<TextBlock Text="{Binding ImportErrorMessage}"
           IsVisible="{Binding ImportErrorMessage, Converter={StaticResource StringNotEmptyConverter}}"
           Foreground="{StaticResource SemanticDangerBrush}"/>
<Button Classes="primary"
        Command="{Binding ImportCommand}"
        IsEnabled="{Binding !IsImporting}"/>
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Debug --filter "FullyQualifiedName~Slice4FlowPolishTests"`
Expected: PASS for dedupe preview, pending cleanup, and shared save path reuse.

- [ ] **Step 5: Commit**

```bash
git add StudyDocumentManager/Models/BatchImportModel.cs StudyDocumentManager/Views/BatchImport.axaml StudyDocumentManager/App.axaml.cs StudyDocumentManager.Tests/Slice4FlowPolishTests.cs
git commit -m "feat: harden batch import flow"
```

## Task 4: Full verification and docs truthfulness

**Files:**
- Modify: `docs/TEST_MATRIX.md`
- Test: `StudyDocumentManager.Tests/Slice4FlowPolishTests.cs`

**Interfaces:**
- Consumes:
  - `Slice4FlowPolishTests`
  - `AvaloniaBindingRegressionTests`
  - existing full suite commands
- Produces:
  - truthful Slice 4A evidence in `docs/TEST_MATRIX.md`

- [ ] **Step 1: Write the failing documentation assertions**

```text
Need evidence entries for:
- Add/Edit inline validation + focus
- MainWindow shortcut precedence and drop routing
- Batch Import pending/error cleanup
```

- [ ] **Step 2: Run focused and full verification**

Run:
```powershell
dotnet build "StudyDocumentManager.sln" -c Debug --no-restore
dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Debug --no-build --filter "FullyQualifiedName~Slice4FlowPolishTests|FullyQualifiedName~AvaloniaBindingRegressionTests"
dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Debug --no-build
dotnet build "StudyDocumentManager.sln" -c Release --no-restore
dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Release --no-build
```
Expected: all commands pass; classify any failures as introduced vs pre-existing.

- [ ] **Step 3: Update `docs/TEST_MATRIX.md` with exact proof**

```markdown
| Add/Edit flow polish | Inline required-name validation and focus bridge | yes | yes | no | yes | implemented | `Slice4FlowPolishTests`, `AvaloniaBindingRegressionTests` |
| Drag/drop route control | Dashboard/AddEdit/BatchImport only; invalid screens rejected | yes | yes | no | limited | implemented | `Slice4FlowPolishTests`; shell event bridge remains desktop runtime code |
| Batch import pending cleanup | Import/scan failures clear pending and preserve preview | yes | yes | no | no | implemented | `Slice4FlowPolishTests` |
```

- [ ] **Step 4: Run a final diff sanity check**

Run:
```powershell
git diff --check
git status --short
```
Expected: no whitespace errors; only intended Slice 4A files changed.

- [ ] **Step 5: Commit**

```bash
git add docs/TEST_MATRIX.md StudyDocumentManager.Tests/Slice4FlowPolishTests.cs
git commit -m "test: document slice 4a proof surface"
```

## Self-Review

- **Spec coverage:** This plan covers the approved Slice 4A scope only: Add/Edit inline validation/focus, deterministic `Ctrl+S`, bounded drag/drop routing, and Batch Import pending/error cleanup with shared save path reuse. Collection/Bulk/Recycle/Report remain for later Slice 4 tasks and are intentionally excluded.
- **Placeholder scan:** No TBD/TODO placeholders remain. Every task lists exact files, exact tests, and concrete implementation snippets.
- **Type consistency:** The plan introduces `AddEditModel.TryApplyFile(string)`, `BatchImportModel.AddDroppedFilesAsync(IReadOnlyList<string>)`, `MainWindowModel.CanAcceptDroppedFiles`, `BatchImportModel.ImportStatusMessage`, and `BatchImportModel.ImportErrorMessage`; all later tasks reference the same names and signatures.

Plan complete and saved to `docs/superpowers/plans/2026-07-29-slice-4a-flow-polish.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**