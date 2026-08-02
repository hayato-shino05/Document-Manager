# Architecture

## Stack

| Surface | Technology |
| --- | --- |
| Desktop | Avalonia UI 11.x (.NET 9) |
| Pattern | MVVM (Model-View-ViewModel) |
| Database | SQLite (Microsoft.Data.Sqlite) |
| Language | C# 13 |

## Project Structure

```text
StudyDocumentManager.sln
├── StudyDocumentManager.Core/     # Domain: entities, interfaces, DTOs
│   ├── Entities/StudyDocument.cs
│   ├── Interfaces/IDocument.cs
│   └── DTOs/AddDocumentDraft.cs
├── StudyDocumentManager.Data/     # Infrastructure: database, repositories
│   ├── Helpers/DatabaseHelper.cs  # SQLite queries, schema, seed
│   └── Repositories/DocumentRepository.cs
├── StudyDocumentManager/          # Presentation: views, viewmodels, services
│   ├── Views/                     # AXAML + code-behind
│   ├── ViewModels/
│   ├── Models/
│   ├── Services/
│   └── App.axaml.cs               # DI composition root
└── StudyDocumentManager.Tests/    # xUnit test suite (795 tests)
```

## Dependency Rule

```text
Core (entities, interfaces)
  <- Data (SQLite, repositories)
      <- Presentation (Avalonia views, viewmodels)
```

Inner layers must not depend on outer layers. Core has zero framework
dependencies. Data depends only on Core. Presentation depends on both.

## Database Schema (English-neutral)

All table and column names use English. No legacy Vietnamese naming.

| Table | Purpose |
| --- | --- |
| documents | Primary document storage |
| categories | Subject/category lookup |
| document_types | Document type lookup |
| collections | User-defined groupings |
| collection_items | Many-to-many: documents ↔ collections |
| recent_files | Recently accessed documents |
| app_settings | 言語などのアプリケーション設定 |

## MVVM Separation (Mandatory)

| Layer | Responsibility | Forbidden |
| --- | --- | --- |
| AXAML | UI markup + bindings + StaticResource | Hardcoded colors, business logic |
| ViewModel | UI state, commands, presentation logic | Direct UI manipulation |
| Service/Model | Business logic, DB, I/O | Any UI awareness |
| Code-behind | UI glue (focus, animation) only | DB calls, API calls |

## i18n Strategy

Schema uses English column names as neutral keys. UI strings are externalized to `.resx` resource files supporting Japanese (default), English, Vietnamese, and Chinese. `Strings.resx`、`Strings.en.resx`、`Strings.vi.resx`、`Strings.zh.resx` はすべて 548 キーで構成され、未翻訳キーは日本語へフォールバックします。言語選択は `app_settings.language` に保存されます。ResX のキー整合性と model 層の保存・復元は自動テスト済みですが、起動時の復元と実行中の `MainWindow` 切り替えは desktop 手動確認です。

## Project Overlay — Study Document Manager

The current repository has already selected an application stack and already contains implementation code.

### Current stack

- Surface: desktop application.
- UI framework: Avalonia 11.2.7.
- Runtime: .NET 9.0.
- Presentation model: MVVM via `CommunityToolkit.Mvvm`.
- Dependency injection: `Microsoft.Extensions.DependencyInjection`.
- Database: SQLite via `Microsoft.Data.Sqlite`.

### Current module layout

```text
StudyDocumentManager/
  Views/        Avalonia XAML views
  Models/       screen/view models and shell state
  Services/     UI-facing services and process helpers
  Converters/   Avalonia converters
  Themes/       tokens, icons, shared styles

StudyDocumentManager.Core/
  Entities/     domain entities
  DTOs/         transfer objects
  Interfaces/   repository/service contracts
  Services/     framework-independent helpers

StudyDocumentManager.Data/
  Helpers/      DatabaseHelper schema/migrations/query facade
  Repositories/ concrete implementations of Core interfaces

StudyDocumentManager.Tests/
  xUnit verification and isolated SQLite tests
```

### Concrete dependency rule in this repo

- Views depend on model properties/commands, not raw database helpers.
- Models orchestrate behavior through Core interfaces and injected services.
- Repositories implement Core interfaces and delegate persistence to DatabaseHelper.
- DatabaseHelper is the SQLite boundary and owns schema creation, migrations, and shared query behavior.
- Tests prove repository behavior, schema expectations, and model/service flows.

### Concrete startup shape

```text
Program.Main
  -> BuildAvaloniaApp()
  -> App.Initialize()
  -> App.OnFrameworkInitializationCompleted()
       -> ConfigureServices(ServiceCollection)
       -> BuildServiceProvider()
       -> DatabaseHelper.InitializeDatabase()
       -> resolve MainWindowModel
       -> set NavigationService main model
       -> create MainWindow with DataContext
```

### Concrete navigation shape

NavigationService switches MainWindowModel.CurrentView between model instances, and MainWindow.axaml maps those model types to views through ContentControl.DataTemplates.

### Data model constraint

Use English schema names such as documents, collections, document_types, personal_notes, recent_files, and document_relations as the current truth. Vietnamese names belong only to migration logic from the retired implementation.
