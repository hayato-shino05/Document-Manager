# 📚 Study Document Manager — Đặc Tả Chức Năng & Giao Diện

> **Version**: 3.1.2 "Professional Edition" · **.NET Framework 4.8** · **Windows Forms** · **SQLite**  
> **Kiến trúc**: MVP + Repository Pattern  
> **Verified**: 100% từ source code (35 file `.cs`) — 2026-04-05

---

## Mục Lục

1. [Kiến Trúc Codebase](#1-kiến-trúc)
2. [Entity Chính](#2-entity)
3. [Forms & Giao Diện](#3-forms)
   - 3.1 Dashboard (Form chính)
   - 3.2 Thêm / Sửa Tài Liệu
   - 3.3 Ghi Chú Cá Nhân
   - 3.4 Quản Lý Hàng Loạt
   - 3.5 Import Hàng Loạt
   - 3.6 Lịch Sử Gần Đây
   - 3.7 Phát Hiện Trùng Lặp
   - 3.8 Tài Liệu Liên Quan
   - 3.9 Quản Lý Danh Mục
   - 3.10 Quản Lý Bộ Sưu Tập
   - 3.11 Kiểm Tra Toàn Vẹn File
   - 3.12 Thùng Rác
   - 3.13 Báo Cáo Thống Kê
   - 3.14 TreeMap Phân Bố
4. [Tầng Repository](#4-repository)
5. [Tầng Presenter (MVP)](#5-presenter)
6. [Custom UI Controls](#6-controls)
7. [Toast Notification](#7-toast)
8. [Theme System](#8-theme)
9. [Icon Helper](#9-icons)
10. [Dịch Vụ Cập Nhật](#10-update)
11. [DatabaseHelper — API Tham Chiếu](#11-database)
12. [Checklist Chức Năng](#12-checklist)

---

## 1. Kiến Trúc

```
study-document-manager/
├── Core/
│   ├── Entities/StudyDocument.cs       # Entity 12 trường
│   └── Interfaces/                     # IDashboardView, IDocumentRepository
├── Application/DTOs/ + Services/
├── Infrastructure/Repositories/        # DocumentRepository : IDocumentRepository
├── Data/
│   ├── DatabaseHelper.cs               # ~49 KB — toàn bộ SQL
│   └── DashboardStats.cs               # Model thống kê tổng hợp
├── Services/
│   ├── AppVersion.cs
│   ├── UpdateChecker.cs                # async HTTP GET GitHub Releases
│   └── UpdateInstaller.cs
├── Documents/                          # 7 Forms tài liệu
├── Management/                         # 4 Forms quản lý
├── Reports/                            # 2 Forms biểu đồ
└── UI/
    ├── AppTheme.cs                     # ~31 KB — design tokens
    ├── IconHelper.cs                   # ~34 KB — icon vẽ thuần GDI+
    ├── ToastNotification.cs
    ├── Controls/                       # 7 custom controls
    └── Presenters/DashboardPresenter.cs
```

---

## 2. Entity

### `StudyDocument` (`Core/Entities/StudyDocument.cs`)

> ⚠️ `is_deleted` và `deleted_at` **chỉ tồn tại ở tầng DB**, không có trong entity class.

| Trường | Kiểu | Ghi chú |
|---|---|---|
| `Id` | `int` | PK |
| `Ten` | `string` | Tên tài liệu |
| `MonHoc` | `string` | Danh mục |
| `Loai` | `string` | Loại tài liệu |
| `DuongDan` | `string` | Đường dẫn file |
| `GhiChu` | `string` | Ghi chú |
| `NgayThem` | `DateTime` | Mặc định `DateTime.Now` |
| `KichThuoc` | `double?` | MB |
| `TacGia` | `string` | Tác giả |
| `QuanTrong` | `bool` | Mặc định `false` |
| `Tags` | `string` | Thẻ, phân cách dấu phẩy |
| `Deadline` | `DateTime?` | Hạn chót |

---

## 3. Forms

### 3.1 Dashboard — Form Chính

**File**: `Documents/Dashboard.cs` (~1950 dòng)  
**Implements**: `IDashboardView` · **Presenter**: `DashboardPresenter`

#### Bố cục

| Khu vực | Mô tả |
|---|---|
| **Menu Bar** | *File* / *Chỉnh sửa* (Làm mới) / *Xem* (Danh mục, Sắp đến hạn 7 ngày, Quá hạn) / *Công cụ* (Quản lý hàng loạt, Kiểm tra file, Bộ sưu tập) / *Trợ giúp* (Giới thiệu) |
| **Toolbar** | 14 nút: **New · Edit · Delete · Open · Export CSV · Refresh · Import · Thùng rác · Hàng loạt · Gần đây · Sao lưu · Trùng lặp · Thống kê · TreeMap** + nút *Update* (ẩn, hiện khi có bản mới) |
| **Sidebar** | `DoubleBufferedTreeView`: *Tất cả / Danh mục / Loại file / Bộ sưu tập / Quan trọng* — badge số lượng mỗi node |
| **Panel lọc** | Ô tìm kiếm (Enter), combo Danh mục, combo Loại, `chkImportantOnly`, `chkEnableDateFilter` + 2 DateTimePicker, `chkEnableSizeFilter` + 2 NumericUpDown |
| **Bảng tài liệu** | `DataGridView`: Icon(24px) · Tên · Loại · Ngày thêm · Kích thước(MB) · ★(inline checkbox) · Hạn chót |
| **Preview Panel** | `DocumentPreviewPanel` trong `SplitContainer`, ẩn mặc định |
| **Status Bar** | `lblCount` "Tổng số: N tài liệu" · `lblStatus` trạng thái hành động |

#### Context Menu (chuột phải)

| Mục | Hành động |
|---|---|
| Mở file | `Process.Start(path)` |
| Sửa | `AddEditForm(doc.Id)` |
| Xóa | Soft delete → Thùng rác |
| Sao chép đường dẫn | `Clipboard.SetText(path)` |
| Mở thư mục chứa | `explorer /select,"path"` |
| Toggle Quan trọng | Đảo `QuanTrong`, lưu DB |
| Ghi chú cá nhân... | `PersonalNoteForm(id, name)` |
| Thêm vào bộ sưu tập... | Popup chọn/tạo Collection |

#### Cell Formatting — Cột Deadline

| Trạng thái | Nền | Chữ |
|---|---|---|
| Quá hạn (< 0 ngày) | Đỏ nhạt (`ValidationErrorLight`) | Đỏ (`StatusError`) |
| Sắp hạn ≤ 3 ngày | Cam nhạt (`ValidationWarningLight`) | Cam (`StatusWarning`) |
| Sắp hạn ≤ 7 ngày | Cam nhạt | Mặc định |

#### Phím tắt

| Phím | Hành động |
|---|---|
| `Ctrl+N` | Thêm mới |
| `Ctrl+F` | Focus + Select All ô tìm kiếm |
| `Ctrl+E` | Xuất CSV |
| `Ctrl+O` | Mở file đang chọn |
| `Del` | Xóa (khi DGV focused) |
| `F5` | Làm mới + Populate sidebar |

---

### 3.2 Thêm / Sửa Tài Liệu

**File**: `Documents/AddEditForm.cs` (390 dòng)  
`new AddEditForm()` → Add | `new AddEditForm(int id)` → Edit

#### Các trường nhập liệu (11 trường)

| Control | Nhãn hiển thị | Bắt buộc | Ghi chú |
|---|---|---|---|
| `txt_ten` | Tên tài liệu | ✅ | Auto-fill từ tên file |
| `txt_duong_dan` + Button | Đường dẫn / Chọn file | ✅ | Validate `File.Exists` khi lưu |
| `cbo_mon_hoc` | Danh mục | — | Công việc / Cá nhân / Học tập / Dự án / Tài chính / Hợp đồng / Tham khảo / Khác |
| `cbo_loai` | Loại | — | Tài liệu / Báo cáo / Hướng dẫn / Biểu mẫu / Hình ảnh / Video / Khác |
| `txt_kich_thuoc` | Kích thước (MB) | — | Auto-calc từ `FileInfo.Length` |
| `txt_tac_gia` | Tác giả | — | — |
| `txt_ghi_chu` | Ghi chú | — | TextBox đa dòng |
| `txtTags` | Tags | — | Nhập tự do, phân cách phẩy |
| `chkHasDeadline` | Có deadline? | — | Enable/disable `dtpDeadline` |
| `dtpDeadline` | Deadline | — | Mặc định = Now + 7 ngày khi bật |
| `chk_quan_trong` | ★ Quan trọng | — | Style Amber + Bold |

**Auto-detect loại khi chọn file:**

| Extension | Loại |
|---|---|
| `.jpg .png .gif .bmp .ico .tiff .webp` | Hình ảnh |
| `.mp4 .avi .mkv .mov .wmv .webm .flv .m4v` | Video |
| `.pdf .doc .docx .xls .xlsx .ppt .pptx .txt` | Tài liệu |

---

### 3.3 Ghi Chú Cá Nhân

**File**: `Documents/PersonalNoteForm.cs`

- Title: `"Ghi chú: {TênTàiLiệu}"`
- TextBox đa dòng + label trạng thái
- Lưu/tải theo `DocumentId`

---

### 3.4 Quản Lý Hàng Loạt

**File**: `Documents/BulkDeleteForm.cs` (544 dòng) · Size: 900×600, Resizable

| Thành phần | Chi tiết |
|---|---|
| **Filter realtime** | Tìm tên · combo Danh mục · combo Loại (filter không xóa selection cũ) |
| **Bảng** | ☑ · Tên · Danh mục · Loại · Ngày thêm · ★ |
| **Status bar** | `"{N} tài liệu đã chọn (hiển thị {M}/{Total})"` |
| **Chọn tất cả / Bỏ chọn** | Áp trên `_filteredDocs` |
| **Xóa đã chọn** | `BulkSoftDelete(ids)` + confirm |
| **Đánh dấu quan trọng** | `BulkToggleImportant(ids, true)` |
| **Đổi danh mục** | Popup dialog → `BulkUpdateSubject(ids, subject)` |

> `DataChanged = true` khi có thay đổi → Dashboard `TriggerRefresh()` khi đóng form.

---

### 3.5 Import Hàng Loạt

**File**: `Documents/BatchImportForm.cs` (377 dòng) · Size: 900×600, FixedDialog

| Thành phần | Chi tiết |
|---|---|
| **Thư mục nguồn** | `FolderBrowserDialog` |
| **Bao gồm thư mục con** | `chkRecursive` — mặc định **Checked = true** |
| **Bảng preview** | ☑ · Tên file · Loại · Kích thước · Đường dẫn |
| **Danh mục áp dụng** | `cboSubject` — áp cho tất cả file |
| **Chọn tất cả / Bỏ chọn** | Toggle checkbox toàn bảng |
| **ProgressBar** | Hiện khi import, ẩn sau khi xong |
| **Nút Import** | Disable đến khi có file; disabled lại khi đang chạy |
| **Kết quả** | Toast + label `"Hoàn tất: {N}/{M} file đã import"` |

**Nhận diện loại file tự động:**

| Extension | Nhãn |
|---|---|
| `.pdf` | PDF |
| `.doc .docx` | Word |
| `.xls .xlsx` | Excel |
| `.ppt .pptx` | PowerPoint |
| `.txt` | Text |
| `.jpg .jpeg .png .gif .bmp` | Hình ảnh |
| `.mp4 .avi .mkv .mov` | Video |
| `.mp3 .wav .flac` | Audio |
| `.zip .rar .7z` | Nén |
| `.html .htm` | HTML |
| `.cs .java .py .js .ts` | Code |
| Khác | Tên đuôi viết hoa (vd: `PSD`) |

---

### 3.6 Lịch Sử Gần Đây

**File**: `Documents/RecentFilesForm.cs` (229 dòng) · Size: 850×500, FixedDialog

**Bảng** (6 cột): Tên · Môn học · Loại · Đường dẫn · **Thời gian mở** · DocId (ẩn)

| Nút | Hành động |
|---|---|
| **Mở file** / Double-click | `Process.Start(path)`, check `File.Exists` |
| **Xóa mục này** | `RemoveRecentFile(docId)` |
| **Xóa toàn bộ lịch sử** | `ClearRecentFiles()` + confirm |

---

### 3.7 Phát Hiện Trùng Lặp

**File**: `Documents/DuplicateDetectionForm.cs` (285 dòng) · Size: 950×600, FixedDialog

> **Thuật toán**: MD5 hash nội dung file — `async Task.Run`, không phải so tên.

**Bảng**: ☑ · Nhóm (#N) · Tên · Loại · Kích thước · Đường dẫn · DocId (ẩn)

| Nút | Hành động |
|---|---|
| **Quét** | ProgressBar async MD5, disable khi đang quét |
| **Xóa bản trùng đã chọn** | `BulkSoftDelete(ids)` — **file thật không bị xóa** + confirm |

---

### 3.8 Tài Liệu Liên Quan

**File**: `Documents/RelatedDocumentsForm.cs` (219 dòng) · Size: 750×500, FixedDialog  
**Title**: `"Tài liệu liên quan - {TênTàiLiệu}"`

**Thêm liên kết:**
- `cmbDocuments` — Autocomplete tất cả tài liệu (trừ tài liệu hiện tại)
- `cmbRelationType` — **Liên quan / Bài tập / Bài giảng / Tham khảo / Phụ lục**

**Bảng**: Tên · Môn học · Loại · **Quan hệ** · RelationId (ẩn) · DocId (ẩn)

| Nút | Hành động |
|---|---|
| **Thêm** | `AddDocumentRelation(fromId, toId, relType)` |
| **Xóa liên kết** | `RemoveDocumentRelation(relationId)` + confirm |

---

### 3.9 Quản Lý Danh Mục

**File**: `Management/CategoryManagementForm.cs` (345 dòng)

> **2 Tab**: `Danh mục` và `Loại tài liệu` — dùng chung 1 DataGridView (Tên · Số lượng tài liệu).

| Nút | Hành động thực tế |
|---|---|
| **Thêm** | `ModernInputBox.Show()` → kiểm tra trùng → hỏi tạo tài liệu mẫu (Yes/No/Cancel) |
| **Sửa** | Đổi tên → `UpdateSubjectName / UpdateTypeName` → cập nhật toàn bộ tài liệu; confirm trước |
| **Xóa** | ⚠️ **Hard delete** toàn bộ tài liệu thuộc danh mục/loại + **2 lần confirm** |

---

### 3.10 Quản Lý Bộ Sưu Tập

**File**: `Management/CollectionManagementForm.cs` (298 dòng)  
**Layout**: `SplitContainer` — trái `ListView` (Tên · Số tài liệu), phải `DataGridView`

| Nút | Hành động |
|---|---|
| **Tạo mới** | `ModernInputBox` → `CreateCollection(name)` |
| **Xóa bộ sưu tập** | Tài liệu KHÔNG bị xóa + confirm |
| **Xóa khỏi BST** | `RemoveDocumentFromCollection(colId, docId)` + confirm |
| **Mở tất cả** | `Process.Start()` mọi file tồn tại trong collection |
| **Double-click tài liệu** | Mở file trực tiếp |

---

### 3.11 Kiểm Tra Toàn Vẹn File

**File**: `Management/FileIntegrityCheckForm.cs` (338 dòng)

**Quét**: duyệt toàn bộ tài liệu có `duong_dan` → check `File.Exists()` → ProgressBar  
**Kết quả**: `"Hoàn thành! Tìm thấy {M}/{N} file bị thiếu"`

**Bảng**: ID(ẩn) · Tên · Đường dẫn · **[Xử lý]** (link button)

Click **[Xử lý]** → Context menu:

| Option | Hành động |
|---|---|
| Chọn file mới... | `OpenFileDialog` → UPDATE `duong_dan` trong DB |
| Xóa đường dẫn (giữ metadata) | SET `duong_dan = ''` — giữ bản ghi |
| Xóa tài liệu | `DeleteDocument(id)` — hard delete + confirm |

**Nút Xóa tất cả**: hard delete tất cả bản ghi trong bảng + confirm.

---

### 3.12 Thùng Rác

**File**: `Management/RecycleBinForm.cs` (241 dòng) · Size: 850×550, FixedDialog

**Bảng**: Tên · Môn học · Loại · **Ngày xóa** (format `dd/MM/yyyy HH:mm`) · ID (ẩn)

| Nút | Điều kiện | Hành động |
|---|---|---|
| **Khôi phục** | Phải chọn 1 dòng | `RestoreDocument(id)` |
| **Xóa vĩnh viễn** | Phải chọn 1 dòng | `PermanentDeleteDocument(id)` + confirm |
| **Dọn sạch** | Thùng rác có item | `EmptyRecycleBin()` + confirm |

> Ba nút `Enabled = false` khi thùng rác trống.

---

### 3.13 Báo Cáo Thống Kê

**File**: `Reports/Report.cs` (446 dòng)

#### 6 Stat Cards (tải từ `GetDashboardStatistics()`)

| # | Nội dung | Màu |
|---|---|---|
| 1 | Tổng tài liệu | Primary (Teal) |
| 2 | Tài liệu quan trọng | Amber |
| 3 | Tài liệu quá hạn | Đỏ (Danger) |
| 4 | Sắp đến hạn | Info (Lam) |
| 5 | Không có file (đường dẫn trống) | TextMuted |
| 6 | Tổng bộ sưu tập | Secondary |

#### Biểu đồ phân bố (chart trên)

- **Nguồn**: Theo danh mục (`GetStatisticsBySubject`) hoặc Theo loại (`GetStatisticsByType`)
- **ComboBox 5 kiểu**: Cột dọc · Cột ngang · Tròn (Pie %) · Đường · Vùng
- Tooltip: `"Category: N tài liệu"`

#### Biểu đồ timeline (chart dưới) — kiểu Area cố định

| Nút | Query | Label |
|---|---|---|
| **7 ngày qua** | `GetDocumentsByDay(7)` | `ngay_format` |
| **12 tháng qua** | `GetDocumentsByMonth(12)` | `thang_format` |

---

### 3.14 TreeMap Phân Bố

**File**: `Reports/TreeMapForm.cs` (334 dòng) · Size: 900×620, **Resizable**

| Khu vực | Chi tiết |
|---|---|
| **Header** | Nền `PrimaryDark`, subtitle mô tả mode, nút Đóng (góc phải, resize-aware) |
| **Toolbar** | 2 nút toggle: **Danh mục** / **Loại file** — active style `PrimaryDark` |
| **TreeMapPanel** | `Dock=Fill`, vẽ GDI+ thuần, `Padding=12` |
| **Legend Panel** | Dưới cùng, auto-wrap, rounded squares màu |

- Palette **16 màu** chuẩn (Blue/Green/Violet/Orange/Rose/Sky/Amber/Purple/Teal/...)
- **Reload khi resize** (`OnResize` override gọi lại `LoadData`)

---

## 4. Repository

**File**: `Infrastructure/Repositories/DocumentRepository.cs` (216 dòng)  
**Implements**: `IDocumentRepository` · Dùng `DatabaseHelper.ConnectionString`

> Ánh xạ `DataRow → StudyDocument` entity; bọc `DatabaseHelper` để cung cấp API typed cho Presenter.

| Method | Mô tả |
|---|---|
| `GetAll()` | Tất cả tài liệu chưa xóa, sắp xếp `ngay_them DESC` |
| `GetById(id)` | 1 entity hoặc `null` |
| `Search(keyword)` | LIKE trên tên / danh mục / ghi chú |
| `Filter(subject, type)` | Lọc theo danh mục + loại |
| `SearchAdvanced(keyword, subject, type, fromDate, toDate, minSize, maxSize, isImportant)` | Tìm nâng cao 8 tiêu chí |
| `Add(doc)` | → `InsertDocument(...)` |
| `Update(doc)` | → `UpdateDocument(...)` |
| `Delete(id)` | → `DeleteDocument(id)` (**hard delete**) |
| `GetDistinctSubjects()` | `List<string>` |
| `GetDistinctTypes()` | `List<string>` |
| `GetDistinctTags()` | `List<string>` |
| `GetUpcomingDeadlines(days)` | Deadline trong N ngày tới |
| `GetOverdueDocuments()` | Deadline đã qua |

---

## 5. Presenter

**File**: `UI/Presenters/DashboardPresenter.cs` (97 dòng)

> MVP Presenter — không phụ thuộc UI control, chỉ giao tiếp qua `IDashboardView`.

```
Dashboard (View)  ←→  IDashboardView  ←→  DashboardPresenter  ←→  IDocumentRepository  ←→  DocumentRepository
```

| Event (đăng ký trong constructor) | Xử lý |
|---|---|
| `SearchRequested` | `_repository.Search(keyword)` → cập nhật view |
| `FilterApplied` | `_repository.SearchAdvanced(8 params)` → cập nhật view |
| `RefreshRequested` | Gọi lại `Initialize()` |
| `DeleteRequested(id)` | `ConfirmDelete()` → `Delete(id)` → reload list |

`Initialize()`: Load subjects + types vào filter dropdowns, load tất cả tài liệu.

---

## 6. Controls

**Thư mục**: `UI/Controls/`

| Class | File | Mô tả |
|---|---|---|
| `ModernButton` | `ModernButton.cs` | Hover effect, corner radius, glow |
| `ModernTextBox` | `ModernTextBox.cs` (~25KB) | Placeholder text, border animation, focus glow |
| `ModernPanel` | `ModernPanel.cs` | Rounded corners, shadow, gradient background |
| `ModernInputBox` | `ModernInputBox.cs` | `static Show(title, label, default)` — thay `InputBox` |
| `DocumentPreviewPanel` | `DocumentPreviewPanel.cs` | Preview ảnh file inline |
| `TreeMapPanel` | `TreeMapPanel.cs` | GDI+ TreeMap: hover tooltip + click callback |
| `DoubleBufferedTreeView` | `DoubleBufferedTreeView.cs` | TreeView chống flickering |

---

## 7. Toast

**File**: `UI/ToastNotification.cs` · Non-blocking, góc phải màn hình, tự biến mất.

| Method | Màu |
|---|---|
| `ToastNotification.Success(msg)` | Xanh teal |
| `ToastNotification.Error(msg)` | Đỏ |
| `ToastNotification.Warning(msg)` | Cam |
| `ToastNotification.Info(msg)` | Lam |

---

## 8. Theme

**File**: `UI/AppTheme.cs` (~31KB) · Palette **Teal/Emerald** Flat Design

| Loại token | Giá trị |
|---|---|
| **Màu sắc** | `Primary/PrimaryDark/PrimaryLight` · `Secondary` · `Accent(Amber/Sky/Orange)` · `Background(Main/Card/Soft)` · `Text(Primary/Secondary/Muted/White)` · `Status(Success/Error/Warning/Info)` · `Danger` · `Grid(Border/HeaderBg/HeaderFg/RowAlt/RowSelected)` · `Validation(ErrorLight/WarningLight)` · `Input` · `Border(Light/Medium)` |
| **Typography** | `FontFamily` · `FontBody` · `FontSmall` · `FontSmallBold` · `FontCaption` · `FontButton` · `FontInput` |
| **Spacing** | `Space4` · `Space8` · `Space16` · `Space24` |
| **Apply methods** | `ApplyButtonPrimary/Success/Danger/Warning/Secondary()` · `ApplyMenuStripStyle()` · `ApplyToolStripStyle()` · `ApplyStatusStripStyle()` · `ApplyDataGridViewStyle()` · `ApplyComboBoxStyle()` |

---

## 9. Icons

**File**: `UI/IconHelper.cs` (~34KB) · Vẽ thuần GDI+, không cần file icon ngoài

`CreateAddIcon` · `CreateEditIcon` · `CreateDeleteIcon` · `CreateOpenIcon` · `CreateExportIcon` · `CreateRefreshIcon` · `CreateImportIcon` · `CreateRecycleBinIcon` · `CreateChecklistIcon` · `CreateClockIcon` · `CreateBackupIcon` · `CreateDuplicateIcon` · `CreateChartIcon` · `CreateTreeMapIcon` · `CreateUpdateIcon` · `GetDocumentIcon(loai, size, path)`

---

## 10. Update

**Thư mục**: `Services/`

| Class | Chức năng |
|---|---|
| `AppVersion` | `AppVersion.Current` — chuỗi version |
| `UpdateChecker` | `await CheckForUpdateAsync()` → `UpdateInfo { HasUpdate, NewVersion, DownloadUrl, ReleasePageUrl }` |
| `UpdateInstaller` | `DownloadAndInstall(url, version, parentForm)` |

> `toolBtnUpdate` trên Toolbar ẩn mặc định. Hiện + cập nhật text khi `HasUpdate == true`.

---

## 11. Database

**File**: `Data/DatabaseHelper.cs` (~49KB) — toàn bộ SQL, dùng `System.Data.SQLite`

| Nhóm | Methods |
|---|---|
| **CRUD** | `InsertDocument(...)` · `UpdateDocument(...)` · `GetAllDocuments()` · `GetById()` |
| **Tìm / Lọc** | Theo tên, danh mục, loại, ngày, dung lượng, tag, quan trọng |
| **Soft delete** | `SoftDelete(id)` · `BulkSoftDelete(List<int>)` |
| **Hard delete** | `DeleteDocument(id)` · `DeleteDocumentsBySubject(val)` · `DeleteDocumentsByType(val)` |
| **Thùng rác** | `GetDeletedDocuments()` · `GetDeletedDocumentCount()` · `RestoreDocument(id)` · `PermanentDeleteDocument(id)` · `EmptyRecycleBin()` |
| **Bulk** | `BulkToggleImportant(ids, v)` · `BulkUpdateSubject(ids, subject)` |
| **Deadline** | `GetUpcomingDeadlines(days)` · `GetOverdueDocuments()` |
| **Danh mục/Loại** | `GetDistinctSubjects()` · `GetDistinctTypes()` · `GetDistinctTags()` · `UpdateSubjectName(old, new)` · `UpdateTypeName(old, new)` |
| **Bộ sưu tập** | `GetCollections()` · `CreateCollection(name)` · `DeleteCollection(id)` · `GetDocumentsInCollection(id)` · `AddDocumentToCollection(col, doc)` · `RemoveDocumentFromCollection(col, doc)` |
| **Liên kết** | `AddDocumentRelation(from, to, type)` · `GetRelatedDocuments(id)` · `RemoveDocumentRelation(relId)` |
| **Lịch sử** | `AddRecentFile(docId)` · `GetRecentFiles()` · `RemoveRecentFile(docId)` · `ClearRecentFiles()` |
| **Thống kê** | `GetDashboardStatistics()` → `DashboardStats` · `GetStatisticsBySubject()` · `GetStatisticsByType()` · `GetDocumentsByDay(n)` · `GetDocumentsByMonth(n)` |
| **Export** | `ExportToCsv()` |
| **Backup** | `BackupDatabase(path)` · `RestoreDatabase(path)` |

---

## 12. Checklist Chức Năng

> **30 chức năng** đã được verify từ source code.

| # | Chức năng | Form / Module |
|---|---|---|
| 1 | Xem danh sách tài liệu dạng Grid | `Dashboard` |
| 2 | Tìm kiếm realtime (Enter) | `Dashboard` |
| 3 | Bộ lọc đa tiêu chí: text · danh mục · loại · ngày · dung lượng · quan trọng | `Dashboard` |
| 4 | Sidebar cây phân loại + số lượng (badge) | `Dashboard` |
| 5 | Toggle ★ Quan trọng inline trên bảng | `Dashboard` |
| 6 | Color-code Deadline: đỏ (quá hạn) / cam (sắp hạn) | `Dashboard` |
| 7 | Context menu 8 options | `Dashboard` |
| 8 | Preview ảnh file inline (`SplitContainer`) | `Dashboard` |
| 9 | Kéo thả file (Drag & Drop) | `Dashboard` |
| 10 | Thêm / Sửa tài liệu (11 trường, auto-detect loại) | `AddEditForm` |
| 11 | Ghi chú cá nhân theo tài liệu | `PersonalNoteForm` |
| 12 | Import hàng loạt (scan đệ quy, progressbar, 12 loại file) | `BatchImportForm` |
| 13 | Bulk: xóa / đánh dấu / đổi danh mục | `BulkDeleteForm` |
| 14 | Lịch sử 20 file gần đây (có timestamp) | `RecentFilesForm` |
| 15 | Phát hiện file trùng lặp (MD5 hash, async) | `DuplicateDetectionForm` |
| 16 | Tài liệu liên quan (5 loại quan hệ) | `RelatedDocumentsForm` |
| 17 | Quản lý danh mục & loại (2 tab, sửa bulk, xóa 2-confirm) | `CategoryManagementForm` |
| 18 | Bộ sưu tập (tạo mới, xóa, mở tất cả) | `CollectionManagementForm` |
| 19 | Kiểm tra toàn vẹn file (3 action per row + xóa tất cả) | `FileIntegrityCheckForm` |
| 20 | Thùng rác (restore · xóa vĩnh viễn · dọn sạch) | `RecycleBinForm` |
| 21 | Thống kê: 6 stat cards + 5 loại biểu đồ + 2 timeline | `Report` |
| 22 | TreeMap phân bố tương tác (resizable, legend, 16 màu) | `TreeMapForm` |
| 23 | Xuất CSV | `DatabaseHelper.ExportToCsv()` |
| 24 | Backup / Restore Database | `DatabaseHelper` |
| 25 | Filter nhanh: Sắp đến hạn / Quá hạn (từ menu Xem) | `Dashboard` |
| 26 | Toast Notification 4 loại (Success/Error/Warning/Info) | `ToastNotification` |
| 27 | Auto-update async từ GitHub Releases | `UpdateChecker + UpdateInstaller` |
| 28 | Phím tắt 6 shortcuts | `Dashboard` |
| 29 | About Dialog (inline panel, không mở form riêng) | `Dashboard` |
| 30 | Add to Collection từ context menu | `Dashboard` |

---

*Verified 100% từ 35 file source code — Study Document Manager v3.1.2 "Professional Edition"*
