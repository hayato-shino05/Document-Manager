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
| `content` | TEXT | メモの内容 |
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

## インデックス

- `idx_documents_subject`: `documents(subject)`
- `idx_documents_type`: `documents(type)`
- `idx_documents_created_at`: `documents(created_at)`
- `idx_documents_deadline`: `documents(deadline)`
- `idx_collection_items_collection`: `collection_items(collection_id)`
- `idx_collection_items_document`: `collection_items(document_id)`
- `idx_documents_deleted`: `documents(is_deleted)`
- `idx_documents_important`: `documents(is_important)`
- `idx_documents_file_path_unique` は、`file_path IS NOT NULL AND file_path <> ''` の行だけを対象に `documents(file_path)` の完全一致を一意にする部分インデックスです。削除済み文書も対象で、SQLite の既定 `BINARY` 比較を使用します。

## migration の動作

`DatabaseMigrator.RunMigrations()` は、現在次の流れで処理します。

1. 現行スキーマの事前確認より前に、旧スキーマ一式の存在と構造を検出します。データをトランザクション内で現行スキーマへコピーし、関連する文書 ID を再マッピングし、子テーブルの外部キーを再構築します。`foreign_key_check` が成功した後に旧テーブルを削除します。
2. 不完全または構造上サポートされない旧スキーマは、書き込み前に拒否します。
3. 現行のテーブルとインデックスが存在しない場合に作成します。
4. 古いデータベースを更新するときは、`is_deleted` と `deleted_at` を冪等に追加します。
5. 履歴データにある空でない `file_path` の完全一致重複を、同一トランザクション内で解消します。最小の `id` のパスを保持し、それ以降の重複行は `file_path = NULL` にしてから部分一意インデックスを作成します。途中で失敗した場合は、データ変更とインデックス作成をまとめてロールバックします。
6. 既存の文書データから `categories` と `document_types` を seed し、新規インストールには既定値を追加します。
7. 既存の値とファイル拡張子に基づいてファイルタイプのラベルを正規化します。
8. `app_settings` を使い、schema version `3` までの旧 catalog ラベルを無効化します。

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
| `IBulkOperationRepository` | `DocumentRepository` | 一括削除、subject 更新、重要フラグ切り替え |
| `IFileIntegrityRepository` | `DocumentRepository` | パス更新、バックアップ、データベースパス |
| `ICategoryRepository` | `CategoryRepository` | カテゴリと文書タイプ |
| `ICollectionRepository` | `CollectionRepository` | collection と collection item |
| `IPersonalNoteRepository` | `PersonalNoteRepository` | 個人メモ |
| `IRecentFileRepository` | `RecentFileRepository` | 最近使ったファイル |
| `IRelatedDocumentRepository` | `RelatedDocumentRepository` | 文書 relation |
| `IReportRepository` | `ReportRepository` | 集計レポートクエリ |
| `ISettingsService` | `SettingsRepository` | key-value 設定 |

## スキーマ変更のルール

スキーマまたは永続化の動作を変更するときは、次のルールに従ってください。

- 現行スキーマと migration の流れを同時に更新する。
- repository contract、implementation、entity または DTO の mapping を一致させる。
- `StudyDocumentManager.Tests` のデータベーステストを更新する。
- 現行スキーマの contract が変わった場合は、この文書も更新する。
- 新しいコードでは現行スキーマの名前だけを使用する。旧スキーマ名は migration の互換ロジックに限定する。
