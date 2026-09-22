# Document Manager への貢献

Document Manager への貢献を歓迎します。このガイドでは、Avalonia と .NET 9 で構成された現在の開発フローを説明します。

## 目次

- [前提条件](#前提条件)
- [リポジトリのセットアップ](#リポジトリのセットアップ)
- [ビルドとテスト](#ビルドとテスト)
- [開発時の注意](#開発時の注意)
- [プルリクエスト](#プルリクエスト)
- [バグ報告と機能提案](#バグ報告と機能提案)
- [関連ドキュメント](#関連ドキュメント)

## 前提条件

- .NET 9 SDK
- Git
- .NET デスクトップ開発に対応したエディターまたは IDE

SQLite のデータベースはアプリケーションがローカルに作成します。別途データベースサーバーを用意する必要はありません。

## リポジトリのセットアップ

```bash
git clone https://github.com/hayato-shino05/study-document-manager.git
cd study-document-manager
```

変更ごとに目的を絞ったブランチを作成してください。

- 新機能: `feature/short-description`
- バグ修正: `fix/short-description`
- ドキュメント: `docs/short-description`

例:

```bash
git checkout -b feature/language-menu-polish
```

## ビルドとテスト

次のコマンドでローカル確認を行います。

```powershell
dotnet build "StudyDocumentManager.sln" -c Debug
dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Debug
```

起動、ルーティング、スキーマ、ローカライズ、テーマリソースに関わる変更では、編集前にプロジェクトのガイドラインを確認してください。

## 開発時の注意

### アーキテクチャの境界

- `StudyDocumentManager` は Avalonia の view、model、service、theme を含みます。
- `StudyDocumentManager.Core` は entity、DTO、service と repository の contract を含みます。
- `StudyDocumentManager.Data` は SQLite の schema、migration、repository implementation を含みます。
- `StudyDocumentManager.Tests` は xUnit のテストを含みます。

### UI とテーマの変更

view に共有の色や brush を直接記述しないでください。既存の theme resource を使用してください。

- `StudyDocumentManager/Themes/ColorTokens.axaml`
- `StudyDocumentManager/Themes/AppTheme.axaml`
- `StudyDocumentManager/Themes/SharedStyles.axaml`

view の状態は `Models/*Model.cs` で管理します。code-behind は Avalonia の event bridge または control lifecycle に対応する必要がある場合だけ使用してください。

### データとスキーマの変更

schema や repository の動作を変更する場合は、次の範囲を同時に更新してください。

- `StudyDocumentManager.Data/Helpers/DatabaseHelper.cs`
- `StudyDocumentManager.Data/Helpers/DatabaseMigrator.cs`
- `StudyDocumentManager.Core/Interfaces/*.cs`
- `StudyDocumentManager.Data/Repositories/*.cs`
- `DATABASE.md`
- `StudyDocumentManager.Tests` の関連テスト

`documents.file_path` の空でない完全一致を対象とする部分一意インデックス、soft delete、restore、`app_settings.language` の保存と復元を変更する場合は、schema、migration、contract、mapping、テストを確認してください。

### ローカライズの変更

現在のローカライズは `.resx` resource、`LocalizationService`、`LocalizeExtension` で実装しています。label、menu、dialog に触れる場合は、resource key の整合性と実行中の言語切り替えを確認してください。日本語が既定ロケールで、未翻訳の key は日本語へフォールバックします。

### Dashboard とデスクトップ実行

Dashboard は `Dashboard.axaml.cs` で初期化を遅延させ、DataGrid と layout の binding loop を避けています。欠損ファイル、launcher 失敗、空の collection は復旧フローを持ちます。deferred lifecycle、drag/drop の event bridge、native dialog、restore 後の再オープンは、モデル／SQLite の自動テストに加えてデスクトップ実行で確認してください。

## プルリクエスト

プルリクエストを作成する前に、次を確認してください。

1. 変更範囲を 1 つの目的に絞る。
2. 関連する build と test コマンドを実行する。
3. contract または workflow が変わる場合はドキュメントを更新する。
4. 変更内容、確認方法、残っている制限を説明する。

推奨する commit message の形式:

- `feat: add collection filter shortcut`
- `fix: preserve selection after dashboard refresh`
- `docs: rewrite readme for avalonia app`

## バグ報告と機能提案

Issue を作成するときは、次の情報を含めてください。

- 簡潔な概要
- 影響する画面、service、workflow
- 再現手順
- 期待される動作
- 実際の動作
- 必要に応じたスクリーンショットまたはログ

機能提案では、最初に解決したい利用者の問題を説明し、その後に提案する動作を記載してください。

## 関連ドキュメント

- [DATABASE.md](./DATABASE.md)
