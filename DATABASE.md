# データベーススキーマ

この文書では、Avalonia と .NET 9 アプリケーションが現在使用している SQLite スキーマを説明します。

実装上の正本は次のファイルです。

- `StudyDocumentManager.Data/Helpers/DatabaseHelper.cs`
- `StudyDocumentManager.Data/Helpers/DatabaseMigrator.cs`

## 概要

- エンジン: `Microsoft.Data.Sqlite` 経由の SQLite
- 既定のファイル: `%LOCALAPPDATA%/DocumentManager/data/study_documents.db`
- 初回起動時、旧配置 `AppDomain.CurrentDomain.BaseDirectory/data/study_documents.db` が存在し、新配置が未作成なら自動移行します
- installer の既定インストール先は `%LOCALAPPDATA%/Programs/DocumentManager` です
- テストでは `InitializeDatabase()` の前に `DatabaseHelper.SetDatabasePath(path)` を呼び出してパスを変更できます
- スキーマと migration は `DatabaseHelper.InitializeDatabase()` が `DatabaseMigrator.RunMigrations()` を呼び出して管理します
- 旧 WinForms のスキーマ名は migration の互換入力としてのみ残っています

## テーブル

### `documents`

| カラム | 型 | 説明 |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | 主キー |
| `name` | TEXT NOT NULL | 文書名 |
| `subject` | TEXT | カテゴリまたは科目のラベル |
| `type` | TEXT | 正規化された文書タイプ |
| `file_path` | TEXT | ディスク上のファイルパス |
| `notes` | TEXT | 一般的なメモ |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | 作成日時 |
| `file_size` | REAL | MB 単位のサイズ |
| `author` | TEXT | 作成者 |
| `is_important` | INTEGER DEFAULT 0 | 重要フラグ |
| `tags` | TEXT | カンマ区切りのタグ |
| `deadline` | DATETIME | 任意の期限 |
| `is_deleted` | INTEGER DEFAULT 0 | soft delete フラグ |
| `deleted_at` | DATETIME | soft delete の日時 |
| `status` | TEXT NOT NULL DEFAULT `'unread'` | 文書の状態。正規化された値は `unread` / `in-progress` / `read` / `needs-action` / `completed` / `archived` のみ |
| `archive_export_key` | TEXT UNIQUE | 個人文書アーカイブのエクスポート/インポートで利用する安定キー。`lower(hex(randomblob(16)))` で生成 |

### `collections`

| カラム | 型 | 説明 |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | 主キー |
| `name` | TEXT NOT NULL | collection 名 |
| `description` | TEXT | 任意の説明 |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | 作成日時 |

### `collection_items`

`collections` と `documents` の多対多リンクです。

| カラム | 型 | 説明 |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | 主キー |
| `collection_id` | INTEGER NOT NULL | `collections(id)` への FK |
| `document_id` | INTEGER NOT NULL | `documents(id)` への FK |
| `added_at` | DATETIME DEFAULT `datetime('now','localtime')` | 追加日時 |

制約:

- `FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE`
- `FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE`
- `UNIQUE(collection_id, document_id)`

### `personal_notes`

| カラム | 型 | 説明 |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | 主キー |
| `document_id` | INTEGER NOT NULL | `documents(id)` への FK |
| `note_type` | TEXT NOT NULL DEFAULT 'general' | ノートの分類種別 (`general` / `summary` / `action` / `quote` / `lecture` / `meeting` 等) |
| `content` | TEXT | メモの内容 |
| `is_pinned` | INTEGER NOT NULL DEFAULT 0 | ピン留めフラグ (1: 固定) |
| `is_deleted` | INTEGER NOT NULL DEFAULT 0 | soft delete フラグ (1: 削除済み) |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | 作成日時 |
| `updated_at` | DATETIME DEFAULT `datetime('now','localtime')` | 最終更新日時 |

制約:

- `FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE`

### `recent_files`

| カラム | 型 | 説明 |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | 主キー |
| `document_id` | INTEGER NOT NULL UNIQUE | 文書ごとに 1 行 |
| `opened_at` | DATETIME DEFAULT `datetime('now','localtime')` | 最終オープン日時 |

制約:

- `FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE`

### `document_relations`

正規化したペアで関連文書のリンクを保存します。

| カラム | 型 | 説明 |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | 主キー |
| `doc_id_1` | INTEGER NOT NULL | ペア内で小さい方の文書 ID |
| `doc_id_2` | INTEGER NOT NULL | ペア内で大きい方の文書 ID |
| `relation_type` | TEXT DEFAULT `'related'` | 関連ラベル |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | 作成日時 |

制約:

- `FOREIGN KEY (doc_id_1) REFERENCES documents(id) ON DELETE CASCADE`
- `FOREIGN KEY (doc_id_2) REFERENCES documents(id) ON DELETE CASCADE`
- `UNIQUE(doc_id_1, doc_id_2)`

### `categories`

カテゴリの lookup table です。

| カラム | 型 | 説明 |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | 主キー |
| `name` | TEXT NOT NULL UNIQUE | カテゴリ名 |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | 作成日時 |

### `document_types`

文書タイプの lookup table です。

| カラム | 型 | 説明 |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | 主キー |
| `name` | TEXT NOT NULL UNIQUE | タイプ名 |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | 作成日時 |

### `app_settings`

| カラム | 型 | 説明 |
| --- | --- | --- |
| `key` | TEXT PRIMARY KEY | 設定キー |
| `value` | TEXT | 設定値 |

### `saved_searches`

保存済み検索の名前と条件（JSON）を保存します。

| カラム | 型 | 説明 |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | 主キー |
| `name` | TEXT NOT NULL UNIQUE COLLATE NOCASE | 検索名。大文字小文字を区別せず一意 |
| `criteria_json` | TEXT NOT NULL | 検索条件の JSON (`SavedSearchCriteria`) |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | 作成日時 |

制約:

- `UNIQUE(name COLLATE NOCASE)`

### `import_inbox`

外部または監視フォルダーから取り込んだ処理待ちファイルを管理します。

| カラム | 型 | 説明 |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | 主キー |
| `document_id` | INTEGER | 取り込み完了後の `documents(id)` への FK (任意) |
| `source_path` | TEXT NOT NULL | 元のファイルパス |
| `display_name` | TEXT NOT NULL | 表示名 |
| `failure_code` | TEXT | エラーコード |
| `duplicate_candidate` | TEXT | 重複候補の判定情報 |
| `subject` | TEXT | 科目・カテゴリ候補 |
| `type` | TEXT | 文書タイプ候補 |
| `state` | TEXT NOT NULL DEFAULT 'Pending' | 状態 (`Pending` / `Imported` / `Ignored` / `Failed`) |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | 作成日時 |
| `updated_at` | DATETIME DEFAULT `datetime('now','localtime')` | 最終更新日時 |

制約:

- `FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE SET NULL`

### `watched_folders`

自動監視対象のフォルダー一覧です。

| カラム | 型 | 説明 |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | 主キー |
| `folder_path` | TEXT NOT NULL | 監視フォルダーパス |
| `enabled` | INTEGER NOT NULL DEFAULT 1 | 有効フラグ (1: 有効, 0: 無効) |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | 登録日時 |

制約:

- `UNIQUE(folder_path COLLATE NOCASE)`

## インデックス

- `idx_documents_subject`: `documents(subject)`
- `idx_documents_type`: `documents(type)`
- `idx_documents_created_at`: `documents(created_at)`
- `idx_documents_deadline`: `documents(deadline)`
- `idx_collection_items_collection`: `collection_items(collection_id)`
- `idx_collection_items_document`: `collection_items(document_id)`
- `idx_documents_deleted`: `documents(is_deleted)`
- `idx_documents_important`: `documents(is_important)`
- `ux_documents_archive_export_key`: `archive_export_key IS NOT NULL AND archive_export_key <> ''` の行を対象にした、`documents(archive_export_key)` の部分一意インデックス。
- `ux_watched_folders_path`: `watched_folders(folder_path COLLATE NOCASE)` の一意インデックス。
- `idx_documents_file_path_unique` は、`file_path IS NOT NULL AND file_path <> ''` の行だけを対象に `documents(file_path)` の完全一致を一意にする部分インデックスです。削除済み文書も対象で、SQLite の既定 `BINARY` 比較を使用します。

## migration の動作

`DatabaseMigrator.RunMigrations()` は、現在次の流れで処理します。

1. 現行スキーマの事前確認より前に、旧スキーマ一式の存在と構造を検出します。データをトランザクション内で現行スキーマへコピーし、関連する文書 ID を再マッピングし、子テーブルの外部キーを再構築します。`foreign_key_check` が成功した後に旧テーブルを削除します。
2. 不完全または構造上サポートされない旧スキーマは、書き込み前に拒否します。
3. 現行のテーブルとインデックスが存在しない場合に作成します。
4. 古いデータベースを更新するときは、`is_deleted` と `deleted_at` を冪等に追加します。同じ方法で `status`（`TEXT NOT NULL DEFAULT 'unread'`）も冪等に追加し、既存行は `unread` で backfill されます。additive migration のため `schema_version` の更新は行いません（`is_deleted` / `deleted_at` 追加時と同じ扱いです）。
5. 履歴データにある空でない `file_path` の完全一致重複を、同一トランザクション内で解消します。最小の `id` のパスを保持し、それ以降の重複行は `file_path = NULL` にしてから部分一意インデックスを作成します。途中で失敗した場合は、データ変更とインデックス作成をまとめてロールバックします。
6. 既存の文書データから `categories` と `document_types` を seed し、新規インストールには既定値を追加します。
7. 既存の値とファイル拡張子に基づいてファイルタイプのラベルを正規化します。
8. `app_settings` を使い、schema version `3` までの旧 catalog ラベルを無効化します。
9. 最後に、現在の `schema_version` が `4` 未満の場合に限り `4` に更新します。`saved_searches` テーブルは `CREATE TABLE IF NOT EXISTS` で作成されるため、この手順は冪等です。

バックアップ検証（`ValidateBackupCandidate`）は `schema_version` として `3` または `4` を受け付けます。`saved_searches` を含まない旧バックアップ（version `3`）も restore 時に migrator がテーブルを再作成するため復元できます。`documents.status` は additive カラムのため、このカラムを持たない旧バックアップも引き続き検証と restore の対象になります。

引退した WinForms 実装の旧テーブル名とカラム名は、検証済みの完全一致 migration 入力としてのみ受け付けます。現行スキーマの contract ではありません。

## soft delete とごみ箱

- 通常の削除では `documents.is_deleted = 1` を設定し、`deleted_at` に日時を保存します。
- メインの文書一覧は `is_deleted` が null または `0` のレコードだけを表示します。
- restore では `is_deleted = 0` を設定し、`deleted_at = NULL` にします。
- 完全削除では `documents` の行を削除します。
- ごみ箱を空にすると、soft delete 済みの文書行をすべて完全削除します。

## repository の対応

| Contract | Implementation | 主な backing helper |
| --- | --- | --- |
| `IDocumentRepository` | `DocumentRepository` | 文書 CRUD、検索、フィルター、期限 |
| `IRecycleBinRepository` | `DocumentRepository` | ごみ箱の動作 |
| `IBulkOperationRepository` | `DocumentRepository` | 一括削除、subject 更新、重要フラグ切り替え、status 更新、アイテム単位の結果を返す一括メタデータ編集（`BulkEditMetadata`） |
| `IFileIntegrityRepository` | `DocumentRepository` | パス更新、バックアップ、データベースパス |
| `ICategoryRepository` | `CategoryRepository` | カテゴリと文書タイプ |
| `ICollectionRepository` | `CollectionRepository` | collection と collection item |
| `IPersonalNoteRepository` | `PersonalNoteRepository` | 個人メモ |
| `IRecentFileRepository` | `RecentFileRepository` | 最近使ったファイル |
| `IRelatedDocumentRepository` | `RelatedDocumentRepository` | 文書 relation |
| `IReportRepository` | `ReportRepository` | 集計レポートクエリ |
| `ISavedSearchRepository` | `SavedSearchRepository` | 保存済み検索 |
| `ISettingsService` | `SettingsRepository` | key-value 設定 |
| `IPersonalDocumentArchiveService` | `PersonalDocumentArchiveService` (in `Data/Services/`) | ZIP エクスポート/インポート、マニフェスト付き |
|  | `PersonalDocumentArchiveRepository` (in `Data/Repositories/`) | アーカイブ用の一括読み書き、manifest の永続化 |

## アーカイブマニフェスト

個人文書アーカイブのエクスポート/インポートでは、`DocumentArchiveManifest` という
JSON ファイルを ZIP 内に同梱します。エクスポート時には対象文書数、合計サイズ、
ファイル単位の `ArchivePath` と SHA-256 `checksum` を保存します。インポート時は
manifest と ZIP の構造、参照、各ファイルの長さおよび checksum を書き込み前に検証します。
checksum が欠落、形式不正、または実体と不一致の場合は、ファイル単位で skip せず、
インポート全体を拒否します。この preflight 検証で拒否された場合、`ArchiveImportReport`
は `Success = false` と `TransactionOutcome = RolledBack` を返し、データベースや宛先
ファイルは変更されません。

stage 領域は `%TEMP%/study-document-archive/<guid>` 配下に文書ごとに
`<archive_export_key>/<file_name>` の形で展開します。preflight と staging が成功した後、
競合がなければ、文書と notes、collections、relations、削除状態を SQLite の 1 transaction
で書き込みます。書き込みまたはファイル確定に失敗した場合は transaction を rollback し、
新たに作成した宛先ファイルと空の親ディレクトリを削除します。競合を検出した場合や
`ValidateOnly` では書き込みを開始せず、`TransactionOutcome = NotStarted` を返します。
成功時は `TransactionOutcome = Committed` です。`archive_export_key` は
`documents.archive_export_key` と一致するため、re-import 後の文書を stable key 経由で
照合できます。

インポート時の `Document.FilePath` は `DestinationRoot` 配下に解決されます。相対パスは
`DestinationRoot` を基準にし、絶対パスも同じ root 配下の場合だけ受け付けます。親ディレクトリ
移動、および既存の reparse point（junction や symbolic link）を含む宛先パスは preflight で
拒否し、拒否時はファイルやデータベースを変更しません。検査とファイル確定の間に別プロセスが
reparse point を差し替える競合を原子的には防げないため、インポート先ディレクトリへの書き込み
権限を信頼できる利用者に限定してください。

## スキーマ変更のルール

スキーマまたは永続化の動作を変更するときは、次のルールに従ってください。

- 現行スキーマと migration の流れを同時に更新する。
- repository contract、implementation、entity または DTO の mapping を一致させる。
- `StudyDocumentManager.Tests` のデータベーステストを更新する。
- 現行スキーマの contract が変わった場合は、この文書も更新する。
- 新しいコードでは現行スキーマの名前だけを使用する。旧スキーマ名は migration の互換ロジックに限定する。
