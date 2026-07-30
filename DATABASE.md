# Database Schema

This document describes the current SQLite schema used by the Avalonia and .NET 9 application.

The implementation authority is:

- `StudyDocumentManager.Data/Helpers/DatabaseHelper.cs`
- `StudyDocumentManager.Data/Helpers/DatabaseMigrator.cs`

## Overview

- Engine: SQLite through `Microsoft.Data.Sqlite`
- Default file: `AppDomain.CurrentDomain.BaseDirectory/data/study_documents.db`
- Tests can override the path through `DatabaseHelper.SetDatabasePath(path)` before `InitializeDatabase()`
- Schema and migrations are orchestrated by `DatabaseHelper.InitializeDatabase()` which calls `DatabaseMigrator.RunMigrations()`
- Legacy WinForms schema names remain relevant only as migration compatibility input

## Tables

### `documents`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | Primary key |
| `name` | TEXT NOT NULL | Document name |
| `subject` | TEXT | Category or subject label |
| `type` | TEXT | Normalized document type |
| `file_path` | TEXT | File path on disk |
| `notes` | TEXT | General notes |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | Creation timestamp |
| `file_size` | REAL | Size in MB |
| `author` | TEXT | Author |
| `is_important` | INTEGER DEFAULT 0 | Important flag |
| `tags` | TEXT | Comma-separated tags |
| `deadline` | DATETIME | Optional deadline |
| `is_deleted` | INTEGER DEFAULT 0 | Soft-delete flag |
| `deleted_at` | DATETIME | Soft-delete timestamp |

### `collections`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | Primary key |
| `name` | TEXT NOT NULL | Collection name |
| `description` | TEXT | Optional description |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | Creation timestamp |

### `collection_items`

Many-to-many link between `collections` and `documents`.

| Column | Type | Notes |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | Primary key |
| `collection_id` | INTEGER NOT NULL | FK to `collections(id)` |
| `document_id` | INTEGER NOT NULL | FK to `documents(id)` |
| `added_at` | DATETIME DEFAULT `datetime('now','localtime')` | Added timestamp |

Constraints:

- `FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE`
- `FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE`
- `UNIQUE(collection_id, document_id)`

### `personal_notes`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | Primary key |
| `document_id` | INTEGER NOT NULL | FK to `documents(id)` |
| `content` | TEXT | Note content |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | Creation timestamp |
| `updated_at` | DATETIME DEFAULT `datetime('now','localtime')` | Last update timestamp |

Constraint:

- `FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE`

### `recent_files`

| Column | Type | Notes |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | Primary key |
| `document_id` | INTEGER NOT NULL UNIQUE | One recent-file row per document |
| `opened_at` | DATETIME DEFAULT `datetime('now','localtime')` | Last-opened timestamp |

Constraint:

- `FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE`

### `document_relations`

Stores related-document links by normalized pair.

| Column | Type | Notes |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | Primary key |
| `doc_id_1` | INTEGER NOT NULL | Lower document id in the pair |
| `doc_id_2` | INTEGER NOT NULL | Higher document id in the pair |
| `relation_type` | TEXT DEFAULT `'related'` | Relation label |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | Creation timestamp |

Constraints:

- `FOREIGN KEY (doc_id_1) REFERENCES documents(id) ON DELETE CASCADE`
- `FOREIGN KEY (doc_id_2) REFERENCES documents(id) ON DELETE CASCADE`
- `UNIQUE(doc_id_1, doc_id_2)`

### `categories`

Lookup table for categories.

| Column | Type | Notes |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | Primary key |
| `name` | TEXT NOT NULL UNIQUE | Category name |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | Creation timestamp |

### `document_types`

Lookup table for document types.

| Column | Type | Notes |
| --- | --- | --- |
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | Primary key |
| `name` | TEXT NOT NULL UNIQUE | Type name |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | Creation timestamp |

### `app_settings`

| Column | Type | Notes |
| --- | --- | --- |
| `key` | TEXT PRIMARY KEY | Setting key |
| `value` | TEXT | Setting value |

## Indexes

- `idx_documents_subject` on `documents(subject)`
- `idx_documents_type` on `documents(type)`
- `idx_documents_created_at` on `documents(created_at)`
- `idx_documents_deadline` on `documents(deadline)`
- `idx_collection_items_collection` on `collection_items(collection_id)`
- `idx_collection_items_document` on `collection_items(document_id)`
- `idx_documents_deleted` on `documents(is_deleted)`
- `idx_documents_important` on `documents(is_important)`

## Migration Behavior

`DatabaseMigrator.RunMigrations()` currently performs the following current-state flow:

1. Detects the complete legacy Vietnamese table set (`tai_lieu`, `danh_muc`, `loai_tai_lieu`) before current-schema preflight. It copies data into the English schema transactionally, remaps dependent document IDs, rebuilds child-table foreign keys, and drops the legacy tables only after `foreign_key_check` succeeds.
2. Rejects incomplete or structurally unsupported legacy schemas before writing changes.
3. Creates the current tables and indexes if they do not exist.
4. Adds `is_deleted` and `deleted_at` idempotently when upgrading older databases.
5. Seeds `categories` and `document_types` from existing document data, then adds default values for fresh installs.
6. Normalizes file-type labels based on existing values and file extensions.
7. Neutralizes legacy catalog labels through schema version `3` using `app_settings`.

Legacy table and column names from the retired WinForms implementation are accepted only as an exact, validated migration input. They are not the active schema contract.

## Soft Delete and Recycle Bin

- Normal deletion marks `documents.is_deleted = 1` and sets `deleted_at`.
- Main document lists filter for records where `is_deleted` is null or `0`.
- Restore sets `is_deleted = 0` and `deleted_at = NULL`.
- Permanent delete removes rows from `documents`.
- Empty recycle bin permanently deletes all soft-deleted document rows.

## Repository Mapping

| Contract | Implementation | Main backing helper |
| --- | --- | --- |
| `IDocumentRepository` | `DocumentRepository` | document CRUD, search, filters, deadlines |
| `IRecycleBinRepository` | `DocumentRepository` | recycle-bin behavior |
| `IBulkOperationRepository` | `DocumentRepository` | bulk delete, subject update, important toggle |
| `IFileIntegrityRepository` | `DocumentRepository` | path updates, backup, database path |
| `ICategoryRepository` | `CategoryRepository` | categories and document types |
| `ICollectionRepository` | `CollectionRepository` | collections and collection items |
| `IPersonalNoteRepository` | `PersonalNoteRepository` | personal notes |
| `IRecentFileRepository` | `RecentFileRepository` | recent files |
| `IRelatedDocumentRepository` | `RelatedDocumentRepository` | document relations |
| `IReportRepository` | `ReportRepository` | aggregate report queries |
| `ISettingsService` | `SettingsRepository` | key-value settings |

## Rules for Schema Changes

When changing schema or persistence behavior:

- Update the current schema and migration flow together.
- Keep repository contracts, implementations, and entity or DTO mappings aligned.
- Update database-backed tests in `StudyDocumentManager.Tests`.
- Update this document when the active schema contract changes.
- Keep new code on English schema names only. Legacy Vietnamese names belong only to migration compatibility logic.
