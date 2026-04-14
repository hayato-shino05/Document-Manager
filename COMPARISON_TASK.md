# 🔍 Checklist So Sánh: Source Cũ (WinForms) vs Source Mới (Avalonia)

> **Mục tiêu**: Xác định chức năng / giao diện nào đã có ở source mới, cái nào còn thiếu.  
> **Cập nhật**: 2026-04-05 | Dựa trên phân tích `FEATURES_OLD.md` vs source `new/src/StudyDocumentManager/`

---

## Legend
- ✅ **Đã có** — Implement đầy đủ, hoạt động
- 🟡 **Có nhưng chưa đủ** — Có cơ bản, còn thiếu 1 số chi tiết
- ❌ **Chưa có** — Cần implement từ đầu

---

## A. Dashboard (Form Chính)

| # | Chức năng | Cũ | Mới | Ghi chú |
|---|---|---|---|---|
| A1 | Danh sách tài liệu (DataGrid) | ✅ | ✅ | `DataGrid` trong `DashboardView.axaml` |
| A2 | Cột: Tên · Danh mục · Loại · Ngày thêm · Kích thước · ★ · Hạn chót | ✅ | ✅ | Đủ 7 cột |
| A3 | Tìm kiếm realtime (Enter) | ✅ | ✅ | `SearchKeyword` binding + `SearchCommand` |
| A4 | Bộ lọc: Danh mục + Loại (ComboBox) | ✅ | 🟡 | ComboBox có, nhưng bind `Name="cboSubject"` từ code-behind (không bind MVVM trực tiếp) |
| A5 | Bộ lọc nâng cao (ngày, dung lượng, quan trọng) | ✅ | ✅ | Collapsible panel đầy đủ |
| A6 | Sidebar phân loại + số lượng (badge) | ✅ | ✅ | `CategoryTreeItems` + Collection nodes |
| A7 | Stat Cards (6 loại) | ✅ | ✅ | 6/6: Tổng · Danh mục · Quan trọng · Quá hạn · Thiếu file · Thùng rác |
| A8 | Color-code Deadline (đỏ/cam theo ngày) | ✅ | ✅ | `DeadlineBrushConverter` + `DeadlineTextConverter` apply vào TemplateColumn |
| A9 | Toggle ★ Quan trọng inline trên DataGrid | ✅ | ✅ | `ToggleImportantInlineCommand` nhận `StudyDocument` param |
| A10 | Preview panel khi chọn tài liệu | ✅ | ✅ | Preview panel metadata đầy đủ dưới DataGrid |
| A11 | Preview ảnh file (hình ảnh thực) | ✅ | 🟡 | Chỉ show icon placeholder, chưa load ảnh thực |
| A12 | Context menu: Mở file | ✅ | ✅ | `OpenFileCommand` |
| A13 | Context menu: Sửa | ✅ | ✅ | `EditDocumentCommand` |
| A14 | Context menu: Xóa (soft) | ✅ | ✅ | `DeleteDocumentCommand` |
| A15 | Context menu: Sao chép đường dẫn | ✅ | ✅ | `CopyPathCommand` |
| A16 | Context menu: Mở thư mục chứa | ✅ | ✅ | `OpenFolderCommand` |
| A17 | Context menu: Toggle Quan trọng | ✅ | ✅ | `ToggleImportantCommand` |
| A18 | Context menu: Ghi chú cá nhân | ✅ | ✅ | `OpenPersonalNoteCommand` |
| A19 | Context menu: Thêm vào bộ sưu tập | ✅ | 🟡 | Có `AddToCollectionCommand` nhưng chọn bằng text input |
| A20 | Drag & Drop file vào app | ✅ | ✅ | `HandleDroppedFile()` trong `MainWindowViewModel` |
| A21 | Filter nhanh: Sắp đến hạn 7 ngày | ✅ | ✅ | `ShowUpcomingDeadlinesCommand` ở status bar |
| A22 | Filter nhanh: Quá hạn | ✅ | ✅ | `ShowOverdueCommand` ở status bar |
| A23 | About dialog | ✅ | ✅ | `ShowAboutCommand` |
| A24 | Phím tắt Ctrl+N / Ctrl+F / Ctrl+E / Ctrl+O / Del / F5 | ✅ | ✅ | `InputGesture` trên MenuItem trong MainWindow |
| A25 | Toolbar (14 nút điều hướng) | ✅ | ✅ | MainWindow toolbar đầy đủ |
| A26 | Menu Bar (File / Xem / Công cụ / Trợ giúp) | ✅ | ✅ | Tệp tin / Chỉnh sửa / Công cụ / Trợ giúp |
| A27 | Status bar text "Tổng: N tài liệu" | ✅ | ✅ | `StatusText` binding |
| A28 | Xuất CSV | ✅ | ✅ | `ExportCsvCommand` với header đầy đủ 12 cột |
| A29 | Backup Database | ✅ | ✅ | `BackupDatabaseCommand` |
| A30 | Restore Database | ✅ | ✅ | `RestoreDatabaseCommand` |
| A31 | Làm mới (Refresh) | ✅ | ✅ | `RefreshCommand` reset toàn bộ filter |

---

## B. Thêm / Sửa Tài Liệu (`AddEditView`)

| # | Chức năng | Cũ | Mới | Ghi chú |
|---|---|---|---|---|
| B1 | Form Add mới / Edit theo ID | ✅ | ✅ | `AddEditViewModel` nhận param id? |
| B2 | 11 trường nhập liệu đầy đủ | ✅ | ❓ | **Cần kiểm tra** `AddEditView.axaml` |
| B3 | Auto-fill tên từ tên file | ✅ | ❓ | **Cần kiểm tra** `AddEditViewModel.cs` |
| B4 | Auto-detect loại file khi chọn | ✅ | ❓ | **Cần kiểm tra** `AddEditViewModel.cs` |
| B5 | Auto-calc KichThuoc (MB) từ FileInfo | ✅ | ❓ | **Cần kiểm tra** `AddEditViewModel.cs` |
| B6 | Validate bắt buộc: Tên + Đường dẫn | ✅ | ❓ | **Cần kiểm tra** |
| B7 | Deadline toggle + DatePicker (+7 ngày mặc định) | ✅ | ❓ | **Cần kiểm tra** |

---

## C. Ghi Chú Cá Nhân (`PersonalNoteView`)

| # | Chức năng | Cũ | Mới | Ghi chú |
|---|---|---|---|---|
| C1 | Hiển thị ghi chú theo DocumentId | ✅ | ❓ | **Cần kiểm tra** `PersonalNoteViewModel.cs` |
| C2 | Lưu ghi chú | ✅ | ❓ | **Cần kiểm tra** |

---

## D. Quản Lý Hàng Loạt (`BulkDeleteView`)

| # | Chức năng | Cũ | Mới | Ghi chú |
|---|---|---|---|---|
| D1 | Bảng danh sách + checkbox chọn | ✅ | ❓ | **Cần kiểm tra** `BulkDeleteView.axaml` |
| D2 | Filter realtime (không xóa selection) | ✅ | ❓ | **Cần kiểm tra** |
| D3 | Bulk: Xóa mềm đã chọn | ✅ | ❓ | **Cần kiểm tra** |
| D4 | Bulk: Đánh dấu Quan trọng | ✅ | ❓ | **Cần kiểm tra** |
| D5 | Bulk: Đổi danh mục | ✅ | ❓ | **Cần kiểm tra** |
| D6 | Status "{N} đã chọn (hiển thị M/Total)" | ✅ | ❓ | **Cần kiểm tra** |

---

## E. Import Hàng Loạt (`BatchImportView`)

| # | Chức năng | Cũ | Mới | Ghi chú |
|---|---|---|---|---|
| E1 | Chọn thư mục + quét file | ✅ | ❓ | **Cần kiểm tra** `BatchImportViewModel.cs` |
| E2 | Checkbox kèm thư mục con (mặc định bật) | ✅ | ❓ | **Cần kiểm tra** |
| E3 | Bảng preview với checkbox chọn | ✅ | ❓ | **Cần kiểm tra** |
| E4 | Nhận diện 12 loại file | ✅ | ❓ | **Cần kiểm tra** |
| E5 | ProgressBar khi import | ✅ | ❓ | **Cần kiểm tra** |

---

## F. Lịch Sử Gần Đây (`RecentFilesView`)

| # | Chức năng | Cũ | Mới | Ghi chú |
|---|---|---|---|---|
| F1 | Danh sách với cột Thời gian mở | ✅ | ❓ | **Cần kiểm tra** `RecentFilesViewModel.cs` |
| F2 | Mở file / xóa mục / xóa toàn bộ | ✅ | ❓ | **Cần kiểm tra** |

---

## G. Phát Hiện Trùng Lặp (`DuplicateDetectionView`)

| # | Chức năng | Cũ | Mới | Ghi chú |
|---|---|---|---|---|
| G1 | Quét MD5 async + ProgressBar | ✅ | ❓ | **Cần kiểm tra** `DuplicateDetectionViewModel.cs` |
| G2 | Bảng nhóm trùng + xóa mềm | ✅ | ❓ | **Cần kiểm tra** |

---

## H. Tài Liệu Liên Quan (`RelatedDocumentsView`)

| # | Chức năng | Cũ | Mới | Ghi chú |
|---|---|---|---|---|
| H1 | Combo autocomplete + 5 loại quan hệ | ✅ | ❓ | **Cần kiểm tra** `RelatedDocumentsView.axaml` |
| H2 | Bảng liên kết + xóa liên kết | ✅ | ❓ | **Cần kiểm tra** |

---

## I. Quản Lý Danh Mục (`CategoryManagementView`)

| # | Chức năng | Cũ | Mới | Ghi chú |
|---|---|---|---|---|
| I1 | 2 Tab (Danh mục + Loại tài liệu) | ✅ | ❓ | **Cần kiểm tra** `CategoryManagementView.axaml` |
| I2 | Thêm (hỏi tạo tài liệu mẫu) | ✅ | ❓ | **Cần kiểm tra** |
| I3 | Sửa (bulk update toàn bộ tài liệu) | ✅ | ❓ | **Cần kiểm tra** |
| I4 | Xóa (hard delete + 2 confirm) | ✅ | ❓ | **Cần kiểm tra** |

---

## J. Quản Lý Bộ Sưu Tập (`CollectionManagementView`)

| # | Chức năng | Cũ | Mới | Ghi chú |
|---|---|---|---|---|
| J1 | ListView + DataGrid (SplitContainer) | ✅ | ❓ | **Cần kiểm tra** `CollectionManagementView.axaml` |
| J2 | Tạo / xóa bộ sưu tập | ✅ | ❓ | **Cần kiểm tra** |
| J3 | Xóa khỏi bộ sưu tập / Mở tất cả | ✅ | ❓ | **Cần kiểm tra** |

---

## K. Kiểm Tra Toàn Vẹn File (`FileIntegrityCheckView`)

| # | Chức năng | Cũ | Mới | Ghi chú |
|---|---|---|---|---|
| K1 | Quét file thiếu + ProgressBar | ✅ | ❓ | **Cần kiểm tra** `FileIntegrityCheckViewModel.cs` |
| K2 | Context menu 3 option per row | ✅ | ❓ | **Cần kiểm tra** |
| K3 | Nút Xóa tất cả | ✅ | ❓ | **Cần kiểm tra** |

---

## L. Thùng Rác (`RecycleBinView`)

| # | Chức năng | Cũ | Mới | Ghi chú |
|---|---|---|---|---|
| L1 | Danh sách tài liệu đã xóa + Ngày xóa | ✅ | ❓ | **Cần kiểm tra** `RecycleBinViewModel.cs` |
| L2 | Khôi phục | ✅ | ❓ | **Cần kiểm tra** |
| L3 | Xóa vĩnh viễn | ✅ | ❓ | **Cần kiểm tra** |
| L4 | Dọn sạch (Empty) | ✅ | ❓ | **Cần kiểm tra** |
| L5 | Disable nút khi trống | ✅ | ❓ | **Cần kiểm tra** |

---

## M. Báo Cáo Thống Kê (`ReportView`)

| # | Chức năng | Cũ | Mới | Ghi chú |
|---|---|---|---|---|
| M1 | 6 Stat Cards | ✅ | ❓ | **Cần kiểm tra** `ReportViewModel.cs` |
| M2 | Biểu đồ phân bố (5 kiểu chart) | ✅ | ❓ | **Cần kiểm tra** |
| M3 | Toggle nguồn: Danh mục / Loại | ✅ | ❓ | **Cần kiểm tra** |
| M4 | Biểu đồ Timeline 7 ngày / 12 tháng | ✅ | ❓ | **Cần kiểm tra** |

---

## N. TreeMap Phân Bố (`TreeMapView`)

| # | Chức năng | Cũ | Mới | Ghi chú |
|---|---|---|---|---|
| N1 | TreeMap GDI+ / Canvas | ✅ | ❓ | **Cần kiểm tra** `TreeMapView.axaml` + `TreeMapViewModel.cs` |
| N2 | Toggle Danh mục / Loại file | ✅ | ❓ | **Cần kiểm tra** |
| N3 | Legend panel + Resizable | ✅ | ❓ | **Cần kiểm tra** |

---

## O. Services & Infrastructure

| # | Chức năng | Cũ | Mới | Ghi chú |
|---|---|---|---|---|
| O1 | Auto-update từ GitHub Releases | ✅ | ✅ | `UpdateChecker.cs` + `UpdateService.cs` |
| O2 | Toast Notification 4 loại | ✅ | ✅ | `ToastService.cs` |
| O3 | Dialog Service (Confirm/Input/Save/Open) | ✅ | ✅ | `DialogService.cs` + `IDialogService` |
| O4 | Navigation Service | N/A | ✅ | `NavigationService.cs` — pattern mới |
| O5 | Theme / ColorTokens | ✅ | ✅ | `ColorTokens.axaml` + `AppTheme.axaml` + `SharedStyles.axaml` |
| O6 | Converter: DeadlineBrushConverter | ✅ | ✅ | `Converters/DeadlineBrushConverter.cs` |
| O7 | AddDocumentDialog (dialog riêng) | N/A | ✅ | `AddDocumentDialog.axaml` — tính năng mới |

---

## Tổng Hợp Ưu Tiên

### 🔴 Cần làm ngay (Dashboard — còn dùng hàng ngày)

| Hạng | Chức năng | File cần sửa |
|---|---|---|
| 1 | Stat card "Không có file" + "Bộ sưu tập" | `DashboardView.axaml`, `DashboardViewModel.cs` |
| 2 | Toggle ★ inline click được trên DataGrid | `DashboardView.axaml`, `DashboardViewModel.cs` |
| 3 | Color-code row Deadline (apply converter vào DataGrid) | `DashboardView.axaml` |
| 4 | Sidebar: thêm node Bộ sưu tập | `DashboardViewModel.cs` |
| 5 | Phím tắt (KeyBinding `Ctrl+N/F/E/O`, `Del`, `F5`) | `DashboardView.axaml` |
| 6 | Drag & Drop file | `DashboardView.axaml.cs` |

### 🟡 Cần verify từng ViewModel (chưa đọc chi tiết)

> Các mục B → N đánh dấu ❓ cần đọc source và cập nhật lại bảng này.

**Thứ tự verify tiếp theo:**

1. `AddEditViewModel.cs` + `AddEditView.axaml` → Mục B
2. `BulkDeleteViewModel.cs` + `BulkDeleteView.axaml` → Mục D
3. `RecycleBinViewModel.cs` + `RecycleBinView.axaml` → Mục L
4. `RecentFilesViewModel.cs` + `RecentFilesView.axaml` → Mục F
5. `BatchImportViewModel.cs` + `BatchImportView.axaml` → Mục E
6. `DuplicateDetectionViewModel` → Mục G
7. `RelatedDocumentsView.axaml` → Mục H
8. `CategoryManagementView.axaml` → Mục I
9. `CollectionManagementView.axaml` → Mục J
10. `FileIntegrityCheckViewModel.cs` → Mục K
11. `ReportViewModel.cs` + `ReportView.axaml` → Mục M
12. `TreeMapViewModel.cs` + `TreeMapView.axaml` → Mục N
13. `PersonalNoteViewModel.cs` → Mục C

---

*Tạo: 2026-04-05 | Cập nhật khi verify từng module*
