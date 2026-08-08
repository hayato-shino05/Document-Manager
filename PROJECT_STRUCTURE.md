# プロジェクト構成、Document Manager

この文書では、Avalonia と .NET 9 で構成された現在のコードベースを説明します。`old-version/` 以下の WinForms 実装は、過去の実装を参照するためだけに使用します。

## 1. アーキテクチャ概要

```text
Avalonia Desktop App (.NET 9)
├── StudyDocumentManager
│   ├── Views (*.axaml)          UI マークアップ
│   ├── Models (*Model.cs)       MVVM の状態とコマンド
│   ├── Services                 UI 向けおよびアプリケーションサービス
│   ├── Converters               Avalonia の binding converter
│   ├── Markup                   localization extension などの XAML helper
│   ├── Resources                `.resx` localization resource
│   └── Themes                   color token、icon、共有 style
├── StudyDocumentManager.Core
│   ├── Entities                 domain entity
│   ├── DTOs                     data transfer object
│   ├── Interfaces               repository と service の contract
│   └── Services                 framework に依存しない helper
├── StudyDocumentManager.Data
│   ├── Helpers                  SQLite の schema、migration、query facade
│   └── Repositories             contract の implementation
└── StudyDocumentManager.Tests   xUnit の検証と分離された SQLite テスト
```

## 2. プロジェクト

| プロジェクト | 対象 | 責務 |
| --- | --- | --- |
| `StudyDocumentManager` | `net9.0`, `WinExe` | Avalonia desktop UI とアプリケーション構成 |
| `StudyDocumentManager.Core` | `net9.0` | entity、DTO、contract、共有 helper |
| `StudyDocumentManager.Data` | `net9.0` | `Microsoft.Data.Sqlite` による SQLite 永続化 |
| `StudyDocumentManager.Tests` | `net9.0` | xUnit テスト |

`StudyDocumentManager.sln` が使用中の solution です。

## 3. 起動フロー

```text
Program.Main(args)
  -> BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)
  -> App.Initialize()
  -> App.OnFrameworkInitializationCompleted()
       -> 既定の Avalonia data validator plugin を削除
       -> ConfigureServices(ServiceCollection)
       -> BuildServiceProvider()
       -> DatabaseHelper.InitializeDatabase()
       -> LocalizationService を resource "Loc" として公開
       -> MainWindowModel を解決
       -> NavigationService.SetMainModel(mainModel)
       -> new MainWindow { DataContext = mainModel }
```

主なファイル:

- `StudyDocumentManager/Program.cs`
- `StudyDocumentManager/App.axaml.cs`
- `StudyDocumentManager/Views/MainWindow.axaml`
- `StudyDocumentManager/Models/MainWindowModel.cs`

## 4. 依存関係のルール

```text
StudyDocumentManager.Core
  <- StudyDocumentManager.Data
      <- StudyDocumentManager

StudyDocumentManager
  -> StudyDocumentManager.Core
```

Core は framework への依存を抑え、共有 contract と entity を所有します。Data は Core に依存します。Presentation は Core と Data の両方に依存します。

## 5. Dependency Injection の構成

登録は `StudyDocumentManager/App.axaml.cs` にあります。

### Repository

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

### Service

| Service | 責務 |
| --- | --- |
| `NavigationService` / `INavigationService` | main shell の active model を切り替える |
| `DialogService` | message dialog、confirmation、input dialog、file dialog、custom dialog |
| `DroppedFileImportService` | drop されたファイルから文書を作成して保存する |
| `ApplicationLifecycleService` | application shutdown の abstraction |
| `ClipboardService` | clipboard 操作 |
| `ProcessLauncherService` | OS 経由でファイルとフォルダーを開く |
| `CsvExportService` | CSV export |
| `DatabaseBackupService` | backup の orchestration |
| `LocalizationService` / `ILocalizationService` | runtime localization |
| `UpdateService` / `IUpdateService` | version check と update flow |
| `ToastService` / `IToastService` | non-blocking notification |

### Model

- Singleton: `MainWindowModel`
- Transient screen: `DashboardModel`, `AddEditModel`, `BatchImportModel`, `BulkDeleteModel`, `DuplicateDetectionModel`, `PersonalNoteModel`, `RelatedDocumentsModel`, `CategoryManagementModel`, `CollectionManagementModel`, `RecycleBinModel`, `FileIntegrityCheckModel`, `ReportModel`, `TreeMapModel`, `RecentFilesModel`

## 6. Navigation のモデル

`NavigationService.NavigateTo(viewKey, parameter)` は route key を model instance に対応付けます。

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

`MainWindow.axaml` は `ContentControl.DataTemplates` を通じて、これらの model type を view に対応付けます。

## 7. ディレクトリ構成

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

## 8. UI 層

### Shell

`StudyDocumentManager/Views/MainWindow.axaml` は次の要素を定義します。

- menu bar
- toolbar
- status area
- language selector
- active view を表示する `ContentControl`

### View

使用中の view は `StudyDocumentManager/Views` にあります。

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

Dialog view には `AddDocumentDialog.axaml`、`AddToCollectionDialog.axaml`、`ChangeCategoryDialog.axaml`、`SelectCollectionDialog.axaml` があります。

### Theme system

| ファイル | 責務 |
| --- | --- |
| `Themes/ColorTokens.axaml` | semantic color、brush、spacing、共有 token |
| `Themes/AppTheme.axaml` | icon drawing resource |
| `Themes/SharedStyles.axaml` | 共有 control style、layout helper、button と card class |

## 9. Localization

Localization は次の要素で実装します。

- `StudyDocumentManager/Services/LocalizationService.cs`
- `StudyDocumentManager/Markup/LocalizeExtension.cs`
- `StudyDocumentManager/Resources/Strings.resx`
- `StudyDocumentManager/Resources/Strings.en.resx`
- `StudyDocumentManager/Resources/Strings.vi.resx`
- `StudyDocumentManager/Resources/Strings.zh.resx`

日本語を既定ロケールとして使用します。4 つの ResX は同じ 548 キーを持ち、未翻訳キーは日本語へフォールバックします。言語選択は `MainWindowModel` から変更でき、SQLite の `app_settings.language` に保存されます。XAML の表示は `LocalizationService` と `LocalizeExtension` が更新しますが、起動時の復元と表示済み model の文字列を実行中に更新する動作は desktop 実行で確認する必要があります。

`Harano Aji Gothic` の Regular/Bold と SIL Open Font License 1.1 の通知を `StudyDocumentManager/Assets/Fonts` に同梱し、`fonts:HaranoAji#Harano Aji Gothic` として参照できるよう登録します。Avalonia の現行 headless runtime はこの CFF OpenType から glyph typeface を生成できないため、既定 UI フォントは Inter のままです。

## 10. Data 層

`StudyDocumentManager.Data/Helpers/DatabaseHelper.cs` は次を所有します。

- SQLite の path と connection string の管理
- 文書の CRUD と検索
- collection、category、document type
- personal note、recent file、related document
- recycle bin と bulk operation
- backup と file integrity helper
- report の aggregate query
- settings へのアクセス

Schema の作成と migration の orchestration は `DatabaseHelper.InitializeDatabase()` と `DatabaseMigrator.RunMigrations()` を通じて行います。

詳細な schema は `DATABASE.md` を参照してください。

## 11. 主な機能

| 機能 | 主な model と view | 主な contract |
| --- | --- | --- |
| Dashboard の検索、filter、一覧 | `DashboardModel`, `Dashboard.axaml` | `IDocumentRepository`, `ICategoryRepository`, `ICollectionRepository` |
| 文書の追加と編集 | `AddEditModel`, `AddEdit.axaml` | `IDocumentRepository` |
| Batch import | `BatchImportModel`, `BatchImport.axaml` | `IDocumentRepository`, file と dialog service |
| 一括操作 | `BulkDeleteModel`, `BulkDelete.axaml` | `IBulkOperationRepository` |
| 重複検出 | `DuplicateDetectionModel`, `DuplicateDetection.axaml` | `IDocumentRepository` |
| カテゴリとタイプ | `CategoryManagementModel`, `CategoryManagement.axaml` | `ICategoryRepository` |
| Collection | `CollectionManagementModel`, `CollectionManagement.axaml` | `ICollectionRepository`, `IDocumentRepository` |
| ごみ箱 | `RecycleBinModel`, `RecycleBin.axaml` | `IRecycleBinRepository` |
| ファイル整合性 | `FileIntegrityCheckModel`, `FileIntegrityCheck.axaml` | `IFileIntegrityRepository` |
| 個人メモ | `PersonalNoteModel`, `PersonalNote.axaml` | `IPersonalNoteRepository` |
| 関連文書 | `RelatedDocumentsModel`, `RelatedDocuments.axaml` | `IRelatedDocumentRepository`, `IDocumentRepository` |
| 最近使ったファイル | `RecentFilesModel`, `RecentFiles.axaml` | `IRecentFileRepository` |
| レポート | `ReportModel`, `Report.axaml` | `IReportRepository` |
| TreeMap | `TreeMapModel`, `TreeMap.axaml` | aggregate report data |

Batch Import は全件成功だけでなく部分成功を許容します。保存済み項目を保持し、失敗項目と未解決の選択を再試行できる状態に残します。`file_path` は空でない完全一致を部分一意インデックスで制約し、旧データの重複は migration 時に最小 `id` の行を保持して解消します。

Dashboard は欠損ファイルと launcher 失敗をエラーとして案内し、空の collection は作成と文書追加を同じフローで復旧できます。Recycle Bin、collection、related document の復旧と選択状態の保持は model／SQLite テストで検証済みです。Backup restore は staging DB の検証まで自動化し、成功後のアプリ終了と再起動は desktop 手動確認が必要です。

## 12. テスト

`StudyDocumentManager.Tests` は xUnit を使用します。`DatabaseTestBase` はテストクラスのインスタンスごとに一意な一時 SQLite ファイルを作成し、初期化前にデータベースパスを上書きします。現行スイートは 795 テストです。

データベース、repository、model/service の自動検証は強い一方、次の desktop runtime の証跡は手動確認として残ります。drag/drop の event bridge、native dialog、Dashboard の deferred lifecycle、HTTP timeout と browser の `Process.Start`、restore 後の再オープン、実行中の `MainWindow` ローカライズ切り替えです。

## 13. 検証コマンド

```powershell
dotnet build "StudyDocumentManager.sln" -c Debug
dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Debug
```

## 14. 取り違えやすい古い情報

- ドキュメントや過去のファイルには、WinForms、.NET Framework 4.8、`System.Data.SQLite`、`tai_lieu`、MVP が残っている場合があります。これらは引退した実装の情報であり、現在のアプリの正本ではありません。
- 現行スキーマのテーブルは `documents` であり、`tai_lieu` ではありません。
- 現行 entity の property は `Name`、`Subject`、`FilePath` など英語です。
- 現行アプリは WinForms の form と presenter ではなく、Avalonia の view と model を使用します。
- `old-version/` と過去の比較文書は migration の参照専用です。
