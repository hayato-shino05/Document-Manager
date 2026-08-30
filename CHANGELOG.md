# 更新履歴

個人文書ワークフローと個人アーカイブの更新履歴を残します。
各エントリは Conventional Commits 形式の commit 単位で記載します。

## 4.1.0 — 2026-08-30

### Added

- 文書ごとの個人メモを 1 対多へ拡張 (`personal_notes` の `note_type` / `is_pinned` / `is_deleted`)。コミット `ada8fc0` 個人ノートの基盤契約と移行を追加する。
- 個人メモの全文検索と複数メモ UI。コミット `0249b60` 個人メモ検索と複数メモUIを追加する。
- 文書マージの Undo と個人メモ保持。コミット `650a67a` 文書マージのUndoとノート保持を実装する。
- ZIP エクスポート/インポート用のアーカイブマニフェスト契約 `DocumentArchiveManifest`。コミット `36c5768` アーカイブマニフェスト契約を追加する。
- ZIP アーカイブエクスポート。コミット `fee38cb` ZIP アーカイブエクスポートを実装する。
- アーカイブ結果レポートに manifest を含める。コミット `9652cd3` アーカイブ結果にマニフェストを含める。
- 安全な ZIP インポートと `archive_export_key` ベースの安定キー。コミット `ac9ab7a` 安全なZIPインポートと安定キーを実装。
- `documents.archive_export_key` を SQLite に追加 (column-level UNIQUE、stable key)。

### Changed

- `StudyDocumentManager.Core/Services/AppVersion.cs` の `Current` を `4.0.0` から `4.1.0` に更新。
- `DatabaseMigrator.ValidateDocumentIndexesAndTriggers` と `EnsureNoUnsupportedIndexesOrTriggers` に `archive_export_key` UNIQUE の許可を追加。
- `DatabaseHelper.ValidateIndexesAndTriggers` にも `archive_export_key` UNIQUE 許可を追加 (Greptile P1 修正、PR #63 内)。
- `docs/TEST_MATRIX.md` の Smart Views / Document status / Bulk Edit / Undo の 4 行を「current Debug: all 39 GapTests pass」に更新。

### Known limitations

- `DocumentPathUniquenessTests.RestoreDatabase_MigratesDocumentPathNoCaseAutoIndex` は引き続き 1 件 fail のままで known-fail 扱い。`DatabaseHelper.ValidateIndexesAndTriggers` で `archive_export_key` UNIQUE の許可は入れたが、テストが想定する `idx_documents_file_path_unique` (BINARY) の full circle 復元が別の経路で崩れているため、別 Issue で追跡する。
- desktop UIA / screen reader の runtime proof、Linux package install/uninstall 検証、150% / 200% DPI matrix、全 19 画面の headless 動作、GitHub Advanced Security、追加 SAST tool は本スライスでは未実施。
- Admin Web (`admin/**`、Vercel デプロイ) は対象外。

## 4.1.1 — 2026-08-30 (PR #67 follow-up)

PR #63 で #51 / #52 / #54 / #55 の core contract はマージ済み。本エントリは
`feature/followup-issues-51-55-remaining` ブランチ (PR #67) で残った
Task 7 UI / round-trip proof / a11y / 1 known-fail test を片付ける。

### Added

- `MainWindow` の Menu_File に `Menu_ExportArchive` / `Menu_ImportArchive` を追加
  (Ctrl+Shift+I でインポート)。コミット `cd28bcd` feat(ui): 個人ノートと
  ZIP アーカイブの入口をメニューに追加
- `DashboardModel.ExportArchiveAsync` / `ImportArchiveAsync` を実装、
  `IFileDialogService` で save/open .zip 選択、`IPersonalDocumentArchiveService`
  に委譲
- `MainWindowModel` に `ExportArchive` / `ImportArchive` command を追加
- 4 言語 `Strings*.resx` に Archive 系 8 キー追加 (807 → 815 キー parity)
- `RelatedDocuments.axaml` に `AutomationProperties.AutomationId` を 3 箇所
  追加 (Issue #54 の最小スコープ)。コミット `dd4f330`
- `PersonalDocumentArchiveTests.Export_Import_RoundTrip_PreservesDocumentsFilesAndManifest`
  を追加: 2 文書 + ファイル実体で round-trip 検証、Manifest.Checksums と
  再計算 SHA-256 一致を確認。コミット `321a959`

### Changed

- `DashboardModel` コンストラクタに `IPersonalDocumentArchiveService` パラメータ
  を追加。既存 10 ファイル分のテストは互換 overload + `NoopArchiveService` で
  保護。

### Known limitations (継続)

- `DocumentPathUniquenessTests.RestoreDatabase_MigratesDocumentPathNoCaseAutoIndex`
  は引き続き 1 件 known-fail。`docs/TEST_MATRIX.md` と同類に GitHub CI 上での
  み検出される別 Issue で追跡
- desktop UIA runtime proof、Linux package install/uninstall、DPI 150/200% matrix
  は Issue #64 / #65 と関連 track で継続
- Admin Web / Vercel deploy は対象外
