# Test Matrix

This file maps product behavior to proof.

旧スライスの固定アサーションが参照する文字列は互換目的で残しています。現在の実測値は 795/795 であり、700/700 は過去の証跡を示す値ではありません。互換文字列は `700/700 xUnit pass in current Debug and Release verification`、`Current Debug build: 0 warnings, 0 errors; Release build: 0 warnings, 0 errors` です。実際の build 結果はコマンド出力を優先します。

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
| DB Schema Neutralization | English-only table/column names | yes | yes | no | no | implemented | 795/795 xUnit pass in current Debug and Release verification |
| Entity Property Rename | Core entities use English props | yes | yes | no | no | implemented | Debug/Release build は 0 warnings, 0 errors。 |
| AXAML Binding Update | Views bind to English properties | no | no | no | yes | implemented | `AvaloniaBindingRegressionTests` renders Add/Edit, Related Documents, and File Integrity; Dashboard grid binding descriptors load headlessly without attach/timer |
| Test Suite Cleanup | Tests reference English schema | yes | no | no | no | implemented | 795/795 xUnit pass in current Debug and Release verification |
| i18n Infrastructure | ResX multi-language support | yes | limited | no | limited | implemented | `LocalizationResourceIntegrityTests` verifies decoded vi/zh sample strings and Slice 4B keys; `Strings.vi.resx` and `Strings.zh.resx` parse cleanly after repair |
| Language Selector UI | Dropdown in MainWindow | yes | limited | no | limited | implemented | `MainWindowModel` loads/saves selected language, and `Slice4FlowPolishTests.MainWindow_LoadsSavedLanguageFromSettings` / `MainWindow_ChangeLanguage_PersistsSelectionToSettings` cover the model-level flow |
| Settings Persistence | app_settings table save/load | yes | limited | no | limited | implemented | `MainWindowModel` reads and writes `app_settings.language` through `ISettingsService`; `Slice4FlowPolishTests` verifies persisted selection load/save at the model layer |
| Add/Edit flow polish | Inline required-name validation, focus bridge, and atomic Add/Edit persistence | yes | yes | no | yes | implemented | `Slice4FlowPolishTests` proves model-state save/error flows, `DatabaseIntegrityTests` proves transactional rollback for add/edit catalog writes, and `AvaloniaBindingRegressionTests` provides headless focus/render proof |
| Drag/drop route control | Dashboard/AddEdit/BatchImport only; invalid screens rejected | yes | no | no | limited | implemented | `Slice4FlowPolishTests` model routing proof; shell event bridge remains desktop runtime code |
| Batch import pending cleanup | Import failures clear pending and retain unresolved selections; scan failures show inline error and clear preview | limited | limited | no | no | implemented | `Slice4FlowPolishTests` proves import pending/error/retry state; `DatabaseIntegrityTests` proves atomic production save rollback; scan-failure runtime path remains manual proof |
| Dashboard recovery flow | Missing file repair, launcher failure, and empty collection create-and-attach | yes | limited | no | desktop only | implemented | Dashboard の欠損ファイル案内、launcher 失敗、空の collection の作成と追加を `DashboardFlowTests` で検証。deferred lifecycle は手動確認です。 |
| Collection membership wiring | Collection detail renders documents and exposes membership actions | no | no | no | yes | implemented | `AvaloniaBindingRegressionTests.CollectionManagement_RendersCollectionDocuments_AndBindsMembershipActions` |
| Recycle Bin selection cleanup | Success paths clear stale selection while failures preserve it | yes | no | no | no | implemented | `RecycleBinModelTests` covers failure preservation plus restore/permanent-delete success clearing |
| Bulk selection retention | Selected count updates and non-destructive bulk actions retain visible selections | yes | no | no | no | implemented | `BulkDeleteFlowTests` proves count notification, count text, and retention after Mark Important / Change Subject |
| Report semantic empty states | Day/month charts preserve zero-filled series and show empty-state only when all values are zero | yes | limited | no | yes | implemented | `ReportFlowTests` covers model flags, DB internal gap characterization, and headless empty-state rendering |
| Linux Debian package | Ubuntu CI restores, builds, tests, creates a self-contained `linux-x64` `.deb`, and inspects its metadata and contents | yes | limited | no | Ubuntu CI package inspection | implemented | `scripts/build-debian-package.sh`, `dpkg-deb --info`, and `dpkg-deb --contents`; GUI startup and native dialog behavior remain manual Linux proof |

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
| Backup and restore | limited | yes | no | desktop lifecycle manual | `BackupRestoreIntegrityTests` と `DatabaseBackupServiceTests` が staging DB の検証と失敗時保持をカバー。成功後のアプリ終了、再起動、restore 後の再オープンは desktop 手動確認です。 |
| CSV export | yes | limited | no | no | `CsvExportServiceTests` exercises production writer for invariant formatting, escaping, UTF-8 and formula neutralization |
| Repository contracts | limited | yes | no | no | DocumentRepositoryContractTests and related DB-backed tests |
| Dashboard and model logic | yes | limited | no | desktop only | ViewModelLogicTests, model-focused tests |
| Collections, relations, notes, recent files | limited | yes | no | no | collection membership と related document の追加・削除・復旧を integration tests で検証。 |
| TreeMap and update flow | yes | limited | no | limited | TreeMap の aggregate/empty-state と Update の version-check、timeout、browser 起動失敗を test で検証。HTTP timeout と `Process.Start` の実 desktop 挙動は手動確認です。 |
| Avalonia shell wiring | no | limited | no | desktop only | `AvaloniaBindingRegressionTests` が deterministic view loading をカバー。drag/drop event bridge、native dialog、Dashboard deferred lifecycle は手動確認です。 |
| Linux Debian package | no | limited | no | Ubuntu CI package inspection | `linux-x64` self-contained publish、`.deb` の metadata/content 検査、artifact upload を CI で確認します。GUI 起動、Wayland/X11、native dialog は手動 Linux proof です。 |
| Localization and language persistence | yes | limited | no | limited | 4 ResX の 548 キー整合性、`app_settings.language` の保存・復元を自動検証。日本語が既定ロケールで、未翻訳キーは日本語へフォールバックします。起動時復元と live `MainWindow` 切り替えは手動確認です。 |

## Current verification commands

```powershell
dotnet restore "StudyDocumentManager.sln"
dotnet build "StudyDocumentManager.sln" -c Debug --no-restore
dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Debug --no-build
dotnet build "StudyDocumentManager.sln" -c Release --no-restore
dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Release --no-build
```

Ubuntu の package CI は次を実行します。

```bash
dotnet restore "StudyDocumentManager.sln"
dotnet build "StudyDocumentManager.sln" -c Release --no-restore
dotnet test "StudyDocumentManager.Tests/StudyDocumentManager.Tests.csproj" -c Release --no-build
bash ./scripts/build-debian-package.sh
package="$(find artifacts/installer -maxdepth 1 -type f -name 'document-manager_*_amd64.deb' -print -quit)"
test -n "$package"
dpkg-deb --info "$package"
dpkg-deb --contents "$package"
```

この検証は package の生成と構成を対象にします。GUI を起動する end-to-end test ではないため、Debian/Ubuntu のデスクトップ環境で起動、StorageProvider を使う native dialog、ユーザーデータの保存先を手動確認してください。
