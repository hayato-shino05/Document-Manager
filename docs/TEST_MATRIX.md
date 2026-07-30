# Test Matrix

This file maps product behavior to proof.

## Status Values

| Status | Meaning |
| --- | --- |
| planned | Accepted as intended behavior, not implemented |
| in_progress | Actively being built |
| implemented | Implemented and proof exists |
| changed | Contract changed after earlier implementation |
| retired | No longer part of the product contract |

## Matrix

| Story | Contract | Unit | Integration | E2E | Platform | Status | Evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| DB Schema Neutralization | English-only table/column names | yes | yes | no | no | implemented | 700/700 xUnit pass in current Debug and Release verification |
| Entity Property Rename | Core entities use English props | yes | yes | no | no | implemented | Current Debug build: 0 warnings, 0 errors; Release build: 0 warnings, 0 errors |
| AXAML Binding Update | Views bind to English properties | no | no | no | yes | implemented | `AvaloniaBindingRegressionTests` renders Add/Edit, Related Documents, and File Integrity; Dashboard grid binding descriptors load headlessly without attach/timer |
| Test Suite Cleanup | Tests reference English schema | yes | no | no | no | implemented | 700/700 xUnit pass in current Debug and Release verification |
| i18n Infrastructure | ResX multi-language support | yes | limited | no | limited | implemented | `LocalizationResourceIntegrityTests` verifies decoded vi/zh sample strings and Slice 4B keys; `Strings.vi.resx` and `Strings.zh.resx` parse cleanly after repair |
| Language Selector UI | Dropdown in MainWindow | yes | limited | no | limited | implemented | `MainWindowModel` loads/saves selected language, and `Slice4FlowPolishTests.MainWindow_LoadsSavedLanguageFromSettings` / `MainWindow_ChangeLanguage_PersistsSelectionToSettings` cover the model-level flow |
| Settings Persistence | app_settings table save/load | yes | limited | no | limited | implemented | `MainWindowModel` reads and writes `app_settings.language` through `ISettingsService`; `Slice4FlowPolishTests` verifies persisted selection load/save at the model layer |
| Add/Edit flow polish | Inline required-name validation, focus bridge, and atomic Add/Edit persistence | yes | yes | no | yes | implemented | `Slice4FlowPolishTests` proves model-state save/error flows, `DatabaseIntegrityTests` proves transactional rollback for add/edit catalog writes, and `AvaloniaBindingRegressionTests` provides headless focus/render proof |
| Drag/drop route control | Dashboard/AddEdit/BatchImport only; invalid screens rejected | yes | no | no | limited | implemented | `Slice4FlowPolishTests` model routing proof; shell event bridge remains desktop runtime code |
| Batch import pending cleanup | Import failures clear pending and retain unresolved selections; scan failures show inline error and clear preview | limited | limited | no | no | implemented | `Slice4FlowPolishTests` proves import pending/error/retry state; `DatabaseIntegrityTests` proves atomic production save rollback; scan-failure runtime path remains manual proof |
| Dashboard recovery flow | Missing file explains repair path, launcher failures show error, and empty collection can create-and-attach in one flow | yes | no | no | no | implemented | `DashboardFlowTests` proves missing-file route, launcher failure handling, and empty-collection create/attach without layout changes |
| Collection membership wiring | Collection detail renders documents and exposes membership actions | no | no | no | yes | implemented | `AvaloniaBindingRegressionTests.CollectionManagement_RendersCollectionDocuments_AndBindsMembershipActions` |
| Recycle Bin selection cleanup | Success paths clear stale selection while failures preserve it | yes | no | no | no | implemented | `RecycleBinModelTests` covers failure preservation plus restore/permanent-delete success clearing |
| Bulk selection retention | Selected count updates and non-destructive bulk actions retain visible selections | yes | no | no | no | implemented | `BulkDeleteFlowTests` proves count notification, count text, and retention after Mark Important / Change Subject |
| Report semantic empty states | Day/month charts preserve zero-filled series and show empty-state only when all values are zero | yes | limited | no | yes | implemented | `ReportFlowTests` covers model flags, DB internal gap characterization, and headless empty-state rendering |
## Evidence Rules

- Unit proof covers pure domain and application rules.
- Integration proof covers backend enforcement, data integrity, provider
  behavior, jobs, or service contracts.
- E2E proof covers user-visible browser flows.
- Platform proof covers only shell, deployment, mobile, desktop, or runtime
  behavior that cannot be proven in lower layers.
- A story can be implemented without every proof column if the story packet
  explains why.

## Current Proof Surface — Study Document Manager

The repository already has implementation and test coverage.

| Area | Unit | Integration | E2E | Platform | Current evidence |
| --- | --- | --- | --- | --- | --- |
| Version semantics | yes | no | no | no | StudyDocumentManager.Tests/*AppVersion* |
| SQLite schema and migrations | limited | yes | no | no | `DatabaseIntegrityTests`, `DatabaseTestBase`, schema and repository tests |
| Recycle Bin integrity | limited | yes | no | no | `DatabaseIntegrityTests`, `RecycleBinModelTests` |
| Backup and restore | limited | yes | no | desktop lifecycle manual | `BackupRestoreIntegrityTests`, `DatabaseBackupServiceTests`; success intentionally shuts down and requires reopen |
| CSV export | yes | limited | no | no | `CsvExportServiceTests` exercises production writer for invariant formatting, escaping, UTF-8 and formula neutralization |
| Repository contracts | limited | yes | no | no | DocumentRepositoryContractTests and related DB-backed tests |
| Dashboard and model logic | yes | limited | no | desktop only | ViewModelLogicTests, model-focused tests |
| Collections, relations, notes, recent files | limited | yes | no | no | extended/integration tests |
| Avalonia shell wiring | no | limited | no | desktop only | `AvaloniaBindingRegressionTests` covers deterministic view loading; Dashboard deferred attach/timer remains manual proof |
| Localization and language persistence | yes | limited | no | limited | `LocalizationResourceIntegrityTests`, `Slice4FlowPolishTests.MainWindow_LoadsSavedLanguageFromSettings`, `Slice4FlowPolishTests.MainWindow_ChangeLanguage_PersistsSelectionToSettings`; vi/zh remain partial and fall back to Japanese for untranslated keys, and live MainWindow switching/startup restoration still rely on limited/manual proof |

## Current verification commands

```powershell
dotnet restore "StudyDocumentManager.sln"
dotnet build "StudyDocumentManager.sln" -c Debug --no-restore
dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Debug --no-build
dotnet build "StudyDocumentManager.sln" -c Release --no-restore
dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Release --no-build
```
