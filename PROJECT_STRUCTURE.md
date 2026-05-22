# Project Structure — Study Document Manager

Tài liệu này mô tả source hiện tại của Study Document Manager sau khi chuyển sang Avalonia/.NET 9. Các thư mục WinForms cũ trong `old-version/` chỉ là tham chiếu lịch sử.

## 1. Tổng quan kiến trúc

```text
Avalonia Desktop App (.NET 9)
├── StudyDocumentManager
│   ├── Views (*.axaml)          UI markup
│   ├── Models (*Model.cs)       MVVM state + commands
│   ├── Services                 UI/application services
│   ├── Converters               Avalonia binding converters
│   └── Themes                   Color tokens, icons, shared styles
├── StudyDocumentManager.Core
│   ├── Entities                 Domain entities
│   ├── DTOs                     Data transfer objects
│   ├── Interfaces               Repository/service contracts
│   └── Services                 Framework-independent helpers
├── StudyDocumentManager.Data
│   ├── Helpers/DatabaseHelper.cs SQLite schema, migrations, query facade
│   └── Repositories             Interface implementations
└── StudyDocumentManager.Tests   xUnit coverage and isolated SQLite tests
```

## 2. Projects

| Project | Target | Responsibility |
|---|---|---|
| `StudyDocumentManager` | `net9.0`, `WinExe` | Avalonia desktop UI and app composition |
| `StudyDocumentManager.Core` | `net9.0` | Entities, DTOs, interfaces, shared helpers |
| `StudyDocumentManager.Data` | `net9.0` | SQLite persistence via `Microsoft.Data.Sqlite` |
| `StudyDocumentManager.Tests` | `net9.0` | xUnit tests |

`StudyDocumentManager.sln` is the current solution. If a stale solution path mentions `src\...`, verify against actual folders before relying on it.

## 3. Startup flow

```text
Program.Main(args)
  -> BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)
  -> App.Initialize()
  -> App.OnFrameworkInitializationCompleted()
       -> remove default Avalonia data validator plugin
       -> ConfigureServices(ServiceCollection)
       -> BuildServiceProvider()
       -> DatabaseHelper.InitializeDatabase()
       -> resolve MainWindowModel
       -> NavigationService.SetMainModel(mainModel)
       -> new MainWindow { DataContext = mainModel }
```

Key files:
- `StudyDocumentManager/Program.cs`
- `StudyDocumentManager/App.axaml.cs`
- `StudyDocumentManager/Views/MainWindow.axaml`
- `StudyDocumentManager/Models/MainWindowModel.cs`

## 4. Dependency injection graph

Registered in `StudyDocumentManager/App.axaml.cs`.

### Repositories

| Interface | Implementation |
|---|---|
| `IDocument` | `DocumentRepository` |
| `ICategory` | `CategoryRepository` |
| `ICollection` | `CollectionRepository` |
| `IPersonalNote` | `PersonalNoteRepository` |
| `IRelatedDocument` | `RelatedDocumentRepository` |
| `IRecentFile` | `RecentFileRepository` |
| `IReport` | `ReportRepository` |

### Services

| Service | Responsibility |
|---|---|
| `NavigationService` / `INavigationService` | Switches current view model |
| `DialogService` | Message, confirm, input, file, and custom dialogs |
| `DroppedFileImportService` | Builds/saves documents from dropped files |
| `ApplicationLifecycleService` | App shutdown abstraction |
| `ClipboardService` | Clipboard operations |
| `ProcessLauncherService` | Open files/folders via OS process |

### Models

- Singleton: `MainWindowModel`.
- Transient screens: `DashboardModel`, `AddEditModel`, `BatchImportModel`, `BulkDeleteModel`, `DuplicateDetectionModel`, `PersonalNoteModel`, `RelatedDocumentsModel`, `CategoryManagementModel`, `CollectionManagementModel`, `RecycleBinModel`, `FileIntegrityCheckModel`, `ReportModel`, `TreeMapModel`, `RecentFilesModel`.

## 5. Navigation model

`NavigationService.NavigateTo(viewKey, parameter)` maps route keys to models:

| Route key | Model |
|---|---|
| `dashboard` | `DashboardModel` |
| `add`, `addedit`, `edit` | `AddEditModel` |
| `categories` | `CategoryManagementModel` |
| `collections` | `CollectionManagementModel` |
| `recycle`, `recyclebin` | `RecycleBinModel` |
| `batch-import`, `batchimport` | `BatchImportModel` |
| `bulk-delete`, `bulkdelete` | `BulkDeleteModel` |
| `duplicates` | `DuplicateDetectionModel` |
| `integrity`, `fileintegrity` | `FileIntegrityCheckModel` |
| `report` | `ReportModel` |
| `recentfiles` | `RecentFilesModel` |
| `treemap` | `TreeMapModel` |
| `personal-note` | `PersonalNoteModel` with `(docId, docName)` parameter |
| `related-docs` | `RelatedDocumentsModel` with `(docId, docName)` parameter |

`MainWindow.axaml` contains matching `ContentControl.DataTemplates` from model types to views.

## 6. Directory map

```text
study-document-manager/
├── AGENTS.md
├── CLAUDE.md
├── DATABASE.md
├── PROJECT_STRUCTURE.md
├── StudyDocumentManager.sln
├── COMPARISON_TASK.md
├── StudyDocumentManager/
│   ├── App.axaml
│   ├── App.axaml.cs
│   ├── Program.cs
│   ├── StudyDocumentManager.csproj
│   ├── Assets/
│   ├── Converters/
│   ├── Models/
│   ├── Services/
│   ├── Themes/
│   └── Views/
├── StudyDocumentManager.Core/
│   ├── DTOs/
│   ├── Entities/
│   ├── Interfaces/
│   └── Services/
├── StudyDocumentManager.Data/
│   ├── Helpers/
│   └── Repositories/
├── StudyDocumentManager.Tests/
├── ManagerSetup/
├── installer/
├── old-version/
├── packages/
├── redist/
└── tests/
```

## 7. UI layer

### Shell

`StudyDocumentManager/Views/MainWindow.axaml` defines:
- Menu bar: file, edit, tools, help.
- Toolbar: CRUD, import, recycle bin, bulk delete, recent files, backup, duplicates, report, TreeMap.
- Status bar.
- `ContentControl` hosting the active view.

### Views

Active views live in `StudyDocumentManager/Views`:

- `Dashboard.axaml`
- `AddEdit.axaml`
- `BatchImport.axaml`
- `BulkDelete.axaml`
- `CategoryManagement.axaml`
- `CollectionManagement.axaml`
- `DuplicateDetection.axaml`
- `FileIntegrityCheck.axaml`
- `PersonalNote.axaml`
- `RecentFiles.axaml`
- `RecycleBin.axaml`
- `RelatedDocuments.axaml`
- `Report.axaml`
- `TreeMap.axaml`

Dialog views include `AddDocumentDialog.axaml`, `AddToCollectionDialog.axaml`, `ChangeCategoryDialog.axaml`, and `SelectCollectionDialog.axaml`.

### Theme system

| File | Responsibility |
|---|---|
| `Themes/ColorTokens.axaml` | Semantic colors and brushes |
| `Themes/AppTheme.axaml` | Icon DrawingImage resources |
| `Themes/SharedStyles.axaml` | Shared control styles, spacing, typography |

## 8. Data layer

`StudyDocumentManager.Data/Helpers/DatabaseHelper.cs` owns:
- SQLite path and connection string.
- Schema creation.
- Migration from old Vietnamese WinForms schema to English schema.
- Document CRUD and search.
- Collections, categories, document types.
- Personal notes, recent files, related documents.
- Recycle bin, bulk operations, backup, file integrity helpers.
- Report aggregate queries.

Repositories in `StudyDocumentManager.Data/Repositories` implement Core interfaces and delegate to `DatabaseHelper`.

See `DATABASE.md` for schema details.

## 9. Main features

| Feature | Primary model/view | Data contract |
|---|---|---|
| Dashboard search/filter/list | `DashboardModel`, `Dashboard.axaml` | `IDocument`, `ICategory`, `ICollection` |
| Add/edit document | `AddEditModel`, `AddEdit.axaml` | `IDocument` |
| Batch import | `BatchImportModel`, `BatchImport.axaml` | `IDocument`, file services |
| Bulk operations | `BulkDeleteModel`, `BulkDelete.axaml` | `IDocument` |
| Duplicate detection | `DuplicateDetectionModel`, `DuplicateDetection.axaml` | `IDocument` |
| Categories/types | `CategoryManagementModel`, `CategoryManagement.axaml` | `ICategory` |
| Collections | `CollectionManagementModel`, `CollectionManagement.axaml` | `ICollection`, `IDocument` |
| Recycle bin | `RecycleBinModel`, `RecycleBin.axaml` | `IDocument` |
| File integrity | `FileIntegrityCheckModel`, `FileIntegrityCheck.axaml` | `IDocument` |
| Personal notes | `PersonalNoteModel`, `PersonalNote.axaml` | `IPersonalNote` |
| Related documents | `RelatedDocumentsModel`, `RelatedDocuments.axaml` | `IRelatedDocument`, `IDocument` |
| Recent files | `RecentFilesModel`, `RecentFiles.axaml` | `IRecentFile` |
| Reports | `ReportModel`, `Report.axaml` | `IReport` |
| TreeMap | `TreeMapModel`, `TreeMap.axaml` | `IReport` or aggregate data |

## 10. Tests

`StudyDocumentManager.Tests` uses xUnit. Database-backed tests inherit `DatabaseTestBase`, which creates a unique temp SQLite file per test class instance and calls `DatabaseHelper.SetDatabasePath()` before initialization.

Important test areas:
- Repository contracts.
- Database schema and migration behavior.
- Extended feature behavior: recent files, recycle bin, relations, notes, collections.
- Model/service logic.

## 11. Commands

```powershell
dotnet build "StudyDocumentManager.sln" -c Debug
dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Debug
```

If GitNexus index is stale:

```powershell
npx gitnexus analyze
```

## 12. Known drift traps

- Docs or old files may still mention WinForms, .NET Framework 4.8, `System.Data.SQLite`, `tai_lieu`, or MVP. Those belong to historical source, not current app.
- Current schema table is `documents`, not `tai_lieu`.
- Current entity properties are English (`Name`, `Subject`, `FilePath`), not Vietnamese (`Ten`, `MonHoc`, `DuongDan`).
- Current app uses Avalonia views/models, not WinForms forms/presenters.
- `COMPARISON_TASK.md` is a migration checklist and may contain stale unchecked rows.
