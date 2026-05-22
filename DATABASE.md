# Database Schema

Tài liệu này mô tả schema SQLite hiện tại của source Avalonia/.NET 9. Nguồn sự thật là `StudyDocumentManager.Data/Helpers/DatabaseHelper.cs`, đặc biệt `CreateTables()` và các migration chạy trong `InitializeDatabase()`.

## Tổng quan

- Engine: SQLite qua `Microsoft.Data.Sqlite`.
- File mặc định: `AppDomain.CurrentDomain.BaseDirectory/data/study_documents.db`.
- Test có thể override bằng `DatabaseHelper.SetDatabasePath(path)` trước `InitializeDatabase()`.
- Schema hiện tại dùng tên bảng/cột tiếng Anh. App vẫn có migration từ schema WinForms cũ (`tai_lieu`, `mon_hoc`, `loai`, `duong_dan`, v.v.) sang schema mới.

## Tables

### `documents`

| Column | Type | Notes |
|---|---|---|
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | Khóa chính |
| `name` | TEXT NOT NULL | Tên tài liệu |
| `subject` | TEXT | Danh mục/môn học |
| `type` | TEXT | Loại tài liệu đã chuẩn hóa |
| `file_path` | TEXT | Đường dẫn file trên máy |
| `notes` | TEXT | Ghi chú chung |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | Ngày thêm |
| `file_size` | REAL | Dung lượng MB |
| `author` | TEXT | Tác giả |
| `is_important` | INTEGER DEFAULT 0 | Cờ quan trọng |
| `tags` | TEXT | Tags dạng text |
| `deadline` | DATETIME | Hạn chót tùy chọn |
| `is_deleted` | INTEGER DEFAULT 0 | Soft delete |
| `deleted_at` | DATETIME | Thời điểm đưa vào thùng rác |

### `collections`

| Column | Type | Notes |
|---|---|---|
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | Khóa chính |
| `name` | TEXT NOT NULL | Tên bộ sưu tập |
| `description` | TEXT | Mô tả tùy chọn |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | Ngày tạo |

### `collection_items`

Bảng nối many-to-many giữa `collections` và `documents`.

| Column | Type | Notes |
|---|---|---|
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | Khóa chính |
| `collection_id` | INTEGER NOT NULL | FK đến `collections(id)` |
| `document_id` | INTEGER NOT NULL | FK đến `documents(id)` |
| `added_at` | DATETIME DEFAULT `datetime('now','localtime')` | Ngày thêm vào bộ sưu tập |

Constraints:
- `FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE`
- `FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE`
- `UNIQUE(collection_id, document_id)`

### `personal_notes`

| Column | Type | Notes |
|---|---|---|
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | Khóa chính |
| `document_id` | INTEGER NOT NULL | FK đến `documents(id)` |
| `content` | TEXT | Nội dung ghi chú |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | Ngày tạo |
| `updated_at` | DATETIME DEFAULT `datetime('now','localtime')` | Ngày cập nhật |

Constraint: `FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE`.

### `recent_files`

| Column | Type | Notes |
|---|---|---|
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | Khóa chính |
| `document_id` | INTEGER NOT NULL UNIQUE | Mỗi tài liệu có tối đa một bản ghi recent |
| `opened_at` | DATETIME DEFAULT `datetime('now','localtime')` | Lần mở gần nhất |

Constraint: `FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE`.

### `document_relations`

Liên kết tài liệu liên quan, lưu theo cặp bidirectional.

| Column | Type | Notes |
|---|---|---|
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | Khóa chính |
| `doc_id_1` | INTEGER NOT NULL | ID nhỏ hơn trong cặp |
| `doc_id_2` | INTEGER NOT NULL | ID lớn hơn trong cặp |
| `relation_type` | TEXT DEFAULT `'related'` | Loại quan hệ |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | Ngày tạo |

Constraints:
- `FOREIGN KEY (doc_id_1) REFERENCES documents(id) ON DELETE CASCADE`
- `FOREIGN KEY (doc_id_2) REFERENCES documents(id) ON DELETE CASCADE`
- `UNIQUE(doc_id_1, doc_id_2)`

### `categories`

Lookup table cho danh mục.

| Column | Type | Notes |
|---|---|---|
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | Khóa chính |
| `name` | TEXT NOT NULL UNIQUE | Tên danh mục |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | Ngày tạo |

### `document_types`

Lookup table cho loại tài liệu.

| Column | Type | Notes |
|---|---|---|
| `id` | INTEGER PRIMARY KEY AUTOINCREMENT | Khóa chính |
| `name` | TEXT NOT NULL UNIQUE | Tên loại |
| `created_at` | DATETIME DEFAULT `datetime('now','localtime')` | Ngày tạo |

### `app_settings`

| Column | Type | Notes |
|---|---|---|
| `key` | TEXT PRIMARY KEY | Tên setting |
| `value` | TEXT | Giá trị |

## Indexes

- `idx_documents_subject` trên `documents(subject)`
- `idx_documents_type` trên `documents(type)`
- `idx_documents_created_at` trên `documents(created_at)`
- `idx_documents_deadline` trên `documents(deadline)`
- `idx_collection_items_collection` trên `collection_items(collection_id)`
- `idx_collection_items_document` trên `collection_items(document_id)`
- `idx_documents_deleted` trên `documents(is_deleted)`
- `idx_documents_important` trên `documents(is_important)`

## Migration rules

`DatabaseHelper.InitializeDatabase()` gọi `CreateTables()`, sau đó chạy các bước:

1. Tạo bảng và index nếu chưa tồn tại.
2. `MigrateToEnglishSchema(conn)`: chuyển dữ liệu từ schema WinForms cũ sang bảng/cột tiếng Anh.
3. `MigrateAddColumn(conn, "documents", "is_deleted", "INTEGER DEFAULT 0")`.
4. `MigrateAddColumn(conn, "documents", "deleted_at", "DATETIME")`.
5. `MigrateSeedCategories(conn)`: seed `categories` và `document_types` từ dữ liệu hiện có, rồi thêm default values cho fresh install.
6. `MigrateNormalizeFileTypes(conn)`: chuẩn hóa `type` từ extension cũ và từ `file_path`.

## Soft delete và thùng rác

- Xóa thông thường dùng `documents.is_deleted = 1` và set `deleted_at`.
- Query danh sách chính lọc `(is_deleted IS NULL OR is_deleted = 0)`.
- Restore set `is_deleted = 0`, `deleted_at = NULL`.
- Permanent delete dùng `DELETE` thật trên `documents`.
- Empty recycle bin xóa toàn bộ documents có `is_deleted = 1`.

## Repository mapping

| Interface | Repository | Backing helper |
|---|---|---|
| `IDocument` | `DocumentRepository` | `DatabaseHelper` document CRUD, recycle bin, bulk, backup, file integrity |
| `ICategory` | `CategoryRepository` | category/type lookup methods |
| `ICollection` | `CollectionRepository` | collections và collection_items |
| `IPersonalNote` | `PersonalNoteRepository` | personal_notes |
| `IRecentFile` | `RecentFileRepository` | recent_files |
| `IRelatedDocument` | `RelatedDocumentRepository` | document_relations |
| `IReport` | `ReportRepository` | report aggregate queries |

## Quy tắc khi sửa schema

- Sửa `CreateTables()` trước, sau đó thêm migration idempotent nếu cần nâng cấp DB cũ.
- Cập nhật entity/DTO trong `StudyDocumentManager.Core` và repository contract tương ứng.
- Cập nhật tests database trong `StudyDocumentManager.Tests`; các test DB dùng file SQLite tạm qua `DatabaseTestBase`.
- Không quay lại tên bảng/cột tiếng Việt trong code mới; chỉ giữ migration cho tương thích dữ liệu cũ.
