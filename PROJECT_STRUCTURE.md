# Project Structure — Study Document Manager

This document describes the current Avalonia and .NET 9 codebase. The WinForms implementation under `old-version/` is historical reference only.

## 1. Architecture Overview

```text
Avalonia Desktop App (.NET 9)
├── StudyDocumentManager
│   ├── Views (*.axaml)          UI markup
│   ├── Models (*Model.cs)       MVVM state and commands
│   ├── Services                 UI-facing and application services
│   ├── Converters               Avalonia binding converters
│   ├── Markup                   XAML helpers such as localization extensions
│   ├── Resources                `.resx` localization resources
│   └── Themes                   Color tokens, icons, shared styles
├── StudyDocumentManager.Core
│   ├── Entities                 Domain entities
│   ├── DTOs                     Data transfer objects
│   ├── Interfaces               Repository and service contracts
│   └── Services                 Framework-independent helpers
├── StudyDocumentManager.Data
│   ├── Helpers                  SQLite schema, migrations, query facade
│   └── Repositories             Contract implementations
└── StudyDocumentManager.Tests   xUnit coverage and isolated SQLite tests
```

## 2. Projects

| Project | Target | Responsibility |
| --- | --- | --- |
| `StudyDocumentManager` | `net9.0`, `WinExe` | Avalonia desktop UI and app composition |
| `StudyDocumentManager.Core` | `net9.0` | Entities, DTOs, contracts, shared helpers |
| `StudyDocumentManager.Data` | `net9.0` | SQLite persistence via `Microsoft.Data.Sqlite` |
| `StudyDocumentManager.Tests` | `net9.0` | xUnit tests |

`StudyDocumentManager.sln` is the active solution.

## 3. Startup Flow

```text
Program.Main(args)
  -> BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)
  -> App.Initialize()
  -> App.OnFrameworkInitializationCompleted()
       -> remove default Avalonia data validator plugin
       -> ConfigureServices(ServiceCollection)
       -> BuildServiceProvider()
       -> DatabaseHelper.InitializeDatabase()
       -> expose LocalizationService as resource "Loc"
       -> resolve MainWindowModel
       -> NavigationService.SetMainModel(mainModel)
       -> new MainWindow { DataContext = mainModel }
```

Key files:

- `StudyDocumentManager/Program.cs`
- `StudyDocumentManager/App.axaml.cs`
- `StudyDocumentManager/Views/MainWindow.axaml`
- `StudyDocumentManager/Models/MainWindowModel.cs`

## 4. Dependency Rule

```text
StudyDocumentManager.Core
  <- StudyDocumentManager.Data
      <- StudyDocumentManager

StudyDocumentManager
  -> StudyDocumentManager.Core
```

Core is framework-light and owns shared contracts and entities. Data depends on Core. Presentation depends on both Core and Data.

## 5. Dependency Injection Graph

Registrations live in `StudyDocumentManager/App.axaml.cs`.

### Repositories

| Contract | Implementation |
| --- | --- |
| `IDocumentRepository` | `DocumentRepository` |
| `IRecycleBinRepository` | `DocumentRepository` |
| `IBulkOperationRepository` | `DocumentRepository` |
| `IFileIntegrityRepository` | `DocumentRepository` |
| `ICategoryRepository` | `CategoryRepository` |
| `ICollectionRepository` | `CollectionRepository` |
| `IPersonalNoteRepository` | `PersonalNoteRepository` |
| `IRelatedDocumentRepository` | `RelatedDocumentRepository` |
| `IRecentFileRepository` | `RecentFileRepository` |
| `IReportRepository` | `ReportRepository` |
| `ISettingsService` | `SettingsRepository` |

### Services

| Service | Responsibility |
| --- | --- |
| `NavigationService` / `INavigationService` | Switches the active model in the main shell |
| `DialogService` | Message dialogs, confirmations, input dialogs, file dialogs, custom dialogs |
| `DroppedFileImportService` | Builds and saves documents from dropped files |
| `ApplicationLifecycleService` | Application shutdown abstraction |
| `ClipboardService` | Clipboard operations |
| `ProcessLauncherService` | Opens files and folders through the OS |
| `CsvExportService` | CSV export |
| `DatabaseBackupService` | Backup orchestration |
| `LocalizationService` / `ILocalizationService` | Runtime localization |
| `UpdateService` / `IUpdateService` | Version check and update flow |
| `ToastService` / `IToastService` | Non-blocking notifications |

### Models

- Singleton: `MainWindowModel`
- Transient screens: `DashboardModel`, `AddEditModel`, `BatchImportModel`, `BulkDeleteModel`, `DuplicateDetectionModel`, `PersonalNoteModel`, `RelatedDocumentsModel`, `CategoryManagementModel`, `CollectionManagementModel`, `RecycleBinModel`, `FileIntegrityCheckModel`, `ReportModel`, `TreeMapModel`, `RecentFilesModel`

## 6. Navigation Model

`NavigationService.NavigateTo(viewKey, parameter)` maps route keys to model instances.

| Route key | Model |
| --- | --- |
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
| `personal-note` | `PersonalNoteModel` with `(docId, docName)` |
| `related-docs` | `RelatedDocumentsModel` with `(docId, docName)` |

`MainWindow.axaml` maps these model types to views through `ContentControl.DataTemplates`.

## 7. Directory Map

```text
study-document-manager/
├── AGENTS.md
├── CLAUDE.md
├── CONTRIBUTING.md
├── DATABASE.md
├── PROJECT_STRUCTURE.md
├── README.md
├── StudyDocumentManager.sln
├── StudyDocumentManager/
│   ├── App.axaml
│   ├── App.axaml.cs
│   ├── Program.cs
│   ├── StudyDocumentManager.csproj
│   ├── Assets/
│   ├── Converters/
│   ├── Markup/
│   ├── Models/
│   ├── Resources/
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
├── docs/
├── installer/
├── old-version/
├── packages/
└── redist/
```

## 8. UI Layer

### Shell

`StudyDocumentManager/Views/MainWindow.axaml` defines:

- menu bar
- toolbar
- status area
- language selector
- `ContentControl` hosting the active view

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

### Theme System

| File | Responsibility |
| --- | --- |
| `Themes/ColorTokens.axaml` | Semantic colors, brushes, spacing, shared tokens |
| `Themes/AppTheme.axaml` | Icon drawing resources |
| `Themes/SharedStyles.axaml` | Shared control styles, layout helpers, button and card classes |

## 9. Localization

Localization is implemented through:

- `StudyDocumentManager/Services/LocalizationService.cs`
- `StudyDocumentManager/Markup/LocalizeExtension.cs`
- `StudyDocumentManager/Resources/Strings.resx`
- `StudyDocumentManager/Resources/Strings.en.resx`
- `StudyDocumentManager/Resources/Strings.vi.resx`
- `StudyDocumentManager/Resources/Strings.zh.resx`

Language selection is exposed in `MainWindowModel` and persisted in SQLite through `app_settings.language`.

## 10. Data Layer

`StudyDocumentManager.Data/Helpers/DatabaseHelper.cs` owns:

- SQLite path and connection string management
- document CRUD and search
- collections, categories, document types
- personal notes, recent files, related documents
- recycle bin and bulk operations
- backup and file integrity helpers
- report aggregate queries
- settings access

Schema creation and migration orchestration runs through `DatabaseHelper.InitializeDatabase()` and `DatabaseMigrator.RunMigrations()`.

See `DATABASE.md` for schema details.

## 11. Main Features

| Feature | Primary model and view | Main contracts |
| --- | --- | --- |
| Dashboard search, filter, list | `DashboardModel`, `Dashboard.axaml` | `IDocumentRepository`, `ICategoryRepository`, `ICollectionRepository` |
| Add and edit document | `AddEditModel`, `AddEdit.axaml` | `IDocumentRepository` |
| Batch import | `BatchImportModel`, `BatchImport.axaml` | `IDocumentRepository`, file and dialog services |
| Bulk operations | `BulkDeleteModel`, `BulkDelete.axaml` | `IBulkOperationRepository` |
| Duplicate detection | `DuplicateDetectionModel`, `DuplicateDetection.axaml` | `IDocumentRepository` |
| Categories and types | `CategoryManagementModel`, `CategoryManagement.axaml` | `ICategoryRepository` |
| Collections | `CollectionManagementModel`, `CollectionManagement.axaml` | `ICollectionRepository`, `IDocumentRepository` |
| Recycle bin | `RecycleBinModel`, `RecycleBin.axaml` | `IRecycleBinRepository` |
| File integrity | `FileIntegrityCheckModel`, `FileIntegrityCheck.axaml` | `IFileIntegrityRepository` |
| Personal notes | `PersonalNoteModel`, `PersonalNote.axaml` | `IPersonalNoteRepository` |
| Related documents | `RelatedDocumentsModel`, `RelatedDocuments.axaml` | `IRelatedDocumentRepository`, `IDocumentRepository` |
| Recent files | `RecentFilesModel`, `RecentFiles.axaml` | `IRecentFileRepository` |
| Reports | `ReportModel`, `Report.axaml` | `IReportRepository` |
| TreeMap | `TreeMapModel`, `TreeMap.axaml` | aggregate report data |

## 12. Tests

`StudyDocumentManager.Tests` uses xUnit. `DatabaseTestBase` creates a unique temporary SQLite file per test-class instance and overrides the database path before initialization.

The current proof surface is strongest on database and repository behavior. Avalonia runtime UI behavior has more limited proof and should be verified carefully when changed.

## 13. Verification Commands

```powershell
dotnet build "StudyDocumentManager.sln" -c Debug
dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Debug
```

## 14. Known Drift Traps

- Docs or historical files may still mention WinForms, .NET Framework 4.8, `System.Data.SQLite`, `tai_lieu`, or MVP. Those belong to retired source, not the current app.
- Current schema table is `documents`, not `tai_lieu`.
- Current entity properties are English, such as `Name`, `Subject`, and `FilePath`.
- Current app uses Avalonia views and models, not WinForms forms and presenters.
- `old-version/` and the historical comparison documents are migration reference only.
