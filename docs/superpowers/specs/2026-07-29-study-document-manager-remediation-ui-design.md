# Study Document Manager: Remediation, Flow, and Academic Utility Design

## Mục tiêu

Khôi phục độ tin cậy của ứng dụng Avalonia trước, sau đó hoàn thiện từng flow theo hướng **Academic utility** mà không thay đổi bố cục Dashboard hiện có.

Dashboard tiếp tục giữ bốn vùng hiện tại:

- bộ lọc phía trên;
- cây phân loại bên trái;
- DataGrid tài liệu ở trung tâm;
- preview tài liệu theo selection.

Phạm vi xử lý gồm regression XAML, CI, integrity SQLite, backup/restore, CSV export, flow chức năng và UI polish. Không thay framework, không thêm kiến trúc phân tán, không thay MVVM, DI hoặc repository boundary hiện có.

## Quyết định đã chốt

| Quyết định | Kết quả |
| --- | --- |
| Bố cục Dashboard | Giữ nguyên |
| Visual direction | Academic utility: nền sáng trung tính, accent xanh dương, viền mảnh, radius nhỏ, ưu tiên đọc dữ liệu |
| Restore sau xóa category/type | Tự khôi phục category/type thiếu trong cùng transaction với document |
| Legacy FK data không hợp lệ | Bảo toàn và chặn khởi tạo, không tự xóa orphan record |
| Phạm vi | Hoàn thành toàn bộ theo slice tuần tự |
| CSV import / lịch sử backup / database redesign | Ngoài phạm vi |

## Baseline và giới hạn

- `old-version/` trong checkout chỉ chứa solution và build artifacts, nên không dùng làm baseline strict.
- Regression P0 đã xác minh: 18 binding XAML dùng property tiếng Việt cũ trong `AddEdit.axaml`, `Dashboard.axaml`, `RelatedDocuments.axaml`.
- Test hiện tại chứng minh dữ liệu/model tốt hơn UI runtime: 614 xUnit tests đã chạy pass tại thời điểm audit, nhưng CI hiện chưa chạy đúng solution hoặc test suite.
- Avalonia runtime binding, lifecycle, dialog ownership và DataGrid interaction chưa có proof tự động.

## Kiến trúc không đổi

```text
Avalonia Views
  -> Models / commands
    -> Core interfaces
      -> Repositories
        -> DatabaseHelper / SQLite
```

Các thay đổi giữ logic UI trong model, data I/O trong repository/helper, và resource UI trong theme/token. Không tạo alias legacy trên entity hoặc ViewModel.

## Slice 1: P0 binding và CI

### Binding migration

Chỉ sửa các path binding đã audit, không thêm alias property, không bật compiled binding toàn cục.

| File | Thay thế |
| --- | --- |
| `StudyDocumentManager/Views/AddEdit.axaml` | `Ten→Name`, `MonHoc→Subject`, `Loai→Type`, `DuongDan→FilePath`, `TacGia→Author`, `GhiChu→Notes`, `QuanTrong→IsImportant` |
| `StudyDocumentManager/Views/Dashboard.axaml` | `Ten→Name`, `MonHoc→Subject`, `Loai→Type`, `NgayThem→CreatedAt`, `KichThuoc→FileSize`, `QuanTrong→IsImportant`, `!QuanTrong→!IsImportant`, `SortMemberPath="Ten"→"Name"` |
| `StudyDocumentManager/Views/RelatedDocuments.axaml` | `Ten→Name`, `MonHoc→Subject` |

### CI

Chỉ sửa `.github/workflows/ci.yml`:

1. Dùng `actions/setup-dotnet@v4` với .NET 9.
2. Restore `StudyDocumentManager.sln`.
3. Build Release bằng `dotnet build --no-restore`.
4. Chạy `dotnet test StudyDocumentManager.Tests/StudyDocumentManager.Tests.csproj -c Release --no-build` trước artifact upload.
5. Upload `StudyDocumentManager/bin/Release/net9.0/` và dùng `if-no-files-found: error`.
6. Giữ Windows runner, trigger, cache và Discord notifications hiện có.

### Acceptance criteria

- Add/Edit nạp và lưu đúng mọi field core.
- Dashboard hiển thị, sort theo name, và toggle star theo property thật.
- Candidate list trong Related Documents hiển thị name/subject.
- CI dùng active paths, build .NET 9 và fail khi test fail hoặc artifact thiếu.

## Slice 2: SQLite integrity và recycle-bin contract

### Connection contract

`DatabaseHelper` sở hữu một đường mở connection dùng chung cho mọi helper operation; `DatabaseMigrator` nhận cùng connection string contract.

- Connection string bật foreign-key enforcement cho **mọi** `SqliteConnection`, kể cả pooled connection.
- Không dùng `PRAGMA foreign_keys` một lần trong `InitializeDatabase()` hoặc bên trong transaction.
- Mỗi database init chạy `PRAGMA foreign_key_check` sau schema validation/migration.

### Legacy schema validation and migration

Preflight là **read-only** và phải chạy trước mọi `CREATE`, `ALTER`, seed category/type, normalize file type hoặc neutralize label. Nếu preflight thất bại, không được tạo bảng, thêm cột hoặc cập nhật dữ liệu.

Schema child tables yêu cầu toàn bộ FK endpoint sau:

- `collection_items.collection_id → collections.id` và `collection_items.document_id → documents.id`;
- `personal_notes.document_id → documents.id`;
- `recent_files.document_id → documents.id`;
- `document_relations.doc_id_1 → documents.id` và `document_relations.doc_id_2 → documents.id`.

Mỗi endpoint phải có `ON DELETE CASCADE`. Preflight kiểm tra `sqlite_master`, `foreign_key_list`, index/unique constraint, trigger, required column và anti-join orphan cho từng endpoint, sau đó chạy `foreign_key_check` trên schema đã có.

Với bảng cũ không có FK:

1. chỉ hỗ trợ các legacy layout đã nhận diện rõ trong migrator; schema có column, index, unique constraint hoặc trigger chưa được hỗ trợ phải dừng khởi tạo với diagnostic actionable;
2. nếu có orphan, integrity failure hoặc schema không được hỗ trợ, dừng khởi tạo mà không thay đổi schema hoặc rows;
3. nếu dữ liệu hợp lệ, rebuild child table trong một migration transaction, copy nguyên rows hợp lệ, tái tạo mọi required index, unique constraint, trigger và cascade đã kiểm kê, rồi chạy `foreign_key_check` lần nữa trước commit;
4. fixture lỗi phải chứng minh rows và schema không đổi sau khởi tạo thất bại;
5. không coi `CREATE TABLE IF NOT EXISTS` là migration constraint hợp lệ và không nuốt `SqliteException` ngoài trường hợp duplicate-column đã được xác định rõ.

`AddRecentFile` đổi từ API `void` sang kết quả failure có kiểm soát tại repository boundary. Document không tồn tại hoặc đã soft-delete phải bị từ chối trước khi insert; UI không được nuốt provider exception hoặc biểu diễn recent entry không tồn tại.

### Recycle Bin state transitions

| Operation | Điều kiện | Kết quả |
| --- | --- | --- |
| Restore | `id` tồn tại và `is_deleted = 1` | khôi phục document và catalog trong cùng transaction |
| Restore active/nonexistent | không thỏa điều kiện | trả `false`, không đổi state |
| Permanent delete | `id` tồn tại và `is_deleted = 1` | hard-delete document, cascade child rows |
| Permanent delete active/nonexistent | không thỏa điều kiện | trả `false`, không mất dữ liệu |
| Empty Trash | chỉ `is_deleted = 1` | hard-delete toàn bộ deleted docs và cascade rows |

`AddRecentFile` với document không tồn tại phải bị từ chối rõ ràng. Contract chọn ưu tiên controlled failure ở repository/helper, không nuốt `SqliteException` và không tạo recent row mồ côi.

### Restore catalog transaction

Restore dùng một connection và transaction duy nhất:

1. đọc document mục tiêu đang `is_deleted = 1`;
2. với `Subject`/`Type` không rỗng, upsert catalog tương ứng bằng `INSERT OR IGNORE` theo uniqueness semantics hiện tại;
3. cập nhật document với predicate `id = @id AND is_deleted = 1`;
4. commit khi tất cả bước thành công; rollback toàn bộ khi bất kỳ bước nào lỗi.

Mọi command trong flow này phải bind cùng explicit transaction. Không tái sử dụng `AddSubject`/`AddType` vì các helper hiện tại mở connection độc lập. Không tạo catalog rỗng. Không sửa value document để ép casing mới; catalog được tạo từ value document hiện có để giữ nguyên metadata đã khôi phục.

Proof bắt buộc gồm fault injection sau subject upsert, sau type upsert và tại conditional document update. Mỗi failure phải để document còn soft-deleted và không để lại category/type vừa tạo.

### UI failure contract

`RecycleBinModel` chỉ reload/show success khi repository trả `true` hoặc count hợp lệ. Khi false:

- giữ selection/list hiện tại;
- không hiển thị success;
- hiển thị localized error actionable.

Permanent delete thành công có feedback rõ ràng, không kết thúc im lặng.

### Acceptance criteria

- FK enforcement xác minh được trên connection mở độc lập.
- Permanent delete và Empty Trash xóa thật child rows ở collection, note, recent, relation ở cả hai endpoint.
- Active document không bị permanent delete; active document không bị restore lần hai.
- Restore tạo lại category/type thiếu và không ảnh hưởng catalog không liên quan.
- Legacy schema không an toàn bị chặn không phá dữ liệu.
- Không có success giả trong Recycle Bin.

## Slice 3: backup, restore, và CSV export

### Backup/restore contract

Data layer thêm một restore operation có result có cấu trúc tại repository boundary. `DatabaseBackupService` chỉ điều phối file picker, confirmation và feedback; không raw-copy trực tiếp database đang live.

Backup dùng `SqliteConnection.BackupDatabase` để tạo snapshot nhất quán. Flow backup mở source và destination qua data-layer-owned connection, ghi vào staged destination, validate candidate, sau đó publish atomically. Helper/service phải trả failure khi source bằng destination, destination đã tồn tại theo policy overwrite chưa được xác nhận, snapshot/validation thất bại hoặc publish thất bại. Không được để file partial hoặc report success khi helper trả false.

Restore phải:

1. cho chọn source backup;
2. validate source ở staged location trước confirmation: source phải là SQLite readable, `integrity_check` và `foreign_key_check` sạch, có schema/version application được hỗ trợ và các bảng required như `documents`; empty hoặc unrelated SQLite file bị từ chối;
3. yêu cầu destructive confirmation không default qua Enter;
4. vào maintenance/quiesce state tại data layer, đóng/clear pooled connections trước swap;
5. tạo rollback copy nhất quán của database hiện tại, atomically swap candidate đã validate; bất kỳ failure trước/sau swap đều khôi phục database hiện tại và trả failure;
6. mở fresh connection, reinitialize/validate schema sau swap, rồi yêu cầu application restart có kiểm soát để loại cached singleton/model state cũ, bao gồm setting language đã persist;
7. chỉ success khi fresh helper đọc được dữ liệu của backup và application lifecycle restart đã được chấp nhận.

Không thêm hệ versioned backup. Bản sao tạm phục vụ snapshot, validation và rollback của một operation không phải tính năng lịch sử backup.

Proof bắt buộc: A → online backup → mutate B → staged restore → fresh helper chỉ đọc A; cancel, source thiếu, random/empty SQLite, valid SQLite không có schema ứng dụng, source=destination, destination publish lỗi, failure trước swap và failure sau swap đều giữ logical state database hiện tại không đổi.

### CSV contract

`CsvExportService` là production source of truth cho encoding/escaping:

- UTF-8 output;
- header và field order cố định;
- RFC-style quote escaping cho comma, quote, `\r`, `\n`;
- `FileSize` dùng `InvariantCulture`;
- date/datetime dùng format invariant, không mơ hồ theo locale;
- mọi text field do người dùng kiểm soát có prefix `=`, `+`, `-`, `@` được neutralize để tránh spreadsheet formula execution;
- cancel không tạo file và không phải error;
- write failure trả failure result có error an toàn, không lộ raw exception path.

Test bao gồm comma, quote, standalone `\r`, CRLF, null, formula prefix trên mọi text column, decimal-comma culture và invariant date/number output.

`CsvExportService` export exactly list passed by caller; repository/Dashboard chịu trách nhiệm không truyền soft-deleted document.

### Acceptance criteria

- Backup SQLite temp database rồi restore round-trip: dữ liệu A được khôi phục sau khi database hiện tại đã đổi thành B.
- Cancel, invalid path, source thiếu, copy error, repository false, database live unavailable không báo success hoặc corrupt DB.
- CSV test production service cho comma/quote/CRLF/null, decimal-comma culture, formula-like user metadata, header, field order, UTF-8 và write failure.

## Slice 4: flow polish theo chức năng

### Dashboard

Giữ nguyên layout. Cải thiện flow bằng trạng thái rõ ràng:

- `Thêm tài liệu` là primary action duy nhất.
- Open/Edit/Delete là selection-context actions; khi không ở Dashboard, action Dashboard-only bị disable hoặc ẩn thay vì no-op.
- Tạo canonical filter summary bằng chips cho collection/deadline/filter đang áp dụng.
- Có `Clear all filters` tái dùng reset behavior đã có.
- Giữ keyword cần explicit Search/Enter, category/type/collection/deadline quick filter áp dụng ngay và luôn hiển thị applied chip.
- Missing-file open phải giải thích lỗi và dẫn tới File Integrity.
- Destructive action tách khỏi primary action, không dùng color là tín hiệu duy nhất.

### Add/Edit và drag-drop

- Sửa bindings trước mọi UI polish.
- Ctrl+S chỉ dành cho Save; Report shortcut đổi hoặc bỏ để không conflict.
- Validation required Name hiển thị inline, focus field lỗi; dialog dành cho save failure thật.
- Subject/type có affordance nhập hoặc đi tới quản lý catalog, nhất quán với save auto-create hiện có.
- Một file drop đi qua metadata dialog; nhiều file drop chuyển sang Batch Import thay vì silent immediate import.

### Collections

- Khi chưa có collection, flow Add-to-collection tạo collection rồi gắn document đã chọn trong một workflow.
- Hide description khi rỗng; không thêm editor description nếu chưa có nhu cầu.
- Collection Management là điểm vào multi-document membership; Dashboard giữ thao tác one-document.
- Cần expose rõ action add/remove membership trong UI hiện có.

### Recycle Bin

- Restore/Permanent Delete disable khi không có selection.
- Nhãn date mô tả chính xác là `Ngày thêm` nếu hiển thị `CreatedAt`.
- Restore, permanent delete, empty trash có pending/success/error rõ ràng và confirmation descriptive.
- Destructive confirm không đặt destructive button làm default Enter action.

### Batch and bulk tools

- Batch Import hiển thị scanning/importing progress, success/failure count, danh sách file thất bại.
- Default subject Batch Import được đảm bảo trong category catalog một lần trước import.
- Bulk UI hiển thị selected count; non-destructive bulk operation giữ selection theo document ID sau reload.
- Bulk delete feedback có link/action tới Recycle Bin.
- Duplicate screen gọi đúng là `Tên trùng`, không suy diễn content duplicate.
- File Integrity hiển thị progress và feedback nhất quán cho relink, clear path, delete.

### Reports, navigation, dialogs

- Report bỏ `SelectedTab` không dùng hoặc UI tab thực, không để state chết; theo default giữ scroll layout hiện tại và xóa state không dùng.
- Mỗi chart time-series có empty-state rõ; report nói rõ phạm vi toàn bộ repository, không phải Dashboard filter.
- `Go Back` đổi nhãn `Về Dashboard`; không thêm history stack trong scope này.
- Dialog xác định direct callbacks trên button được tạo, không phụ thuộc first/last visual child.
- Owner-null/cancel là state explicit, không silent success.

## Slice 5: Academic utility visual system

### Visual rules

- Dashboard layout bất biến: filter top, category tree left, DataGrid center, preview selection giữ nguyên.
- Background paper-light, surface trắng, thin border, compact radius 4–6px.
- Blue chỉ dành primary action, navigation/selection/focus; green là success; amber/red chỉ dành warning/deadline/error.
- Giữ DataGrid dense nhưng readable; tăng hit area action/icon thường dùng lên ít nhất 44×44 DIPs, không tăng toàn bộ row density.
- Giảm row sáu stat card hiện tại xuống ba stat surfaces có action thật: total documents, overdue, missing file. Important/category/recycle count chuyển vào preview, category tree, report hoặc contextual status, không tạo layout Dashboard mới.
- Toolbar ở min width giữ nhóm Add/Open/Refresh; action ít dùng về menu hiện có để tránh label CJK tràn mà không tạo responsive system mới.
- Warning/success solid button dùng foreground/background đạt AA; `TextSecondary` dùng cho metadata cần đọc, `TextMuted` chỉ dùng placeholder/decorative text.

### Accessibility, localization and high contrast

- Form labels liên kết input bằng `AutomationProperties.LabeledBy` hoặc accessible name tương đương.
- Icon-only controls có localized `AutomationProperties.Name`, tooltip và hit target rõ, gồm star toggle và remove relation.
- Filter enable checkboxes có label text hoặc accessible name.
- Theme có focus style tokenized và đủ tương phản cho button, TextBox, ComboBox, DataGrid, nav item và menu item.
- Thêm HighContrast theme variant/token dictionary; không claim high-contrast support trước khi test variant này.
- Mọi user-visible copy/units được localize, bao gồm Import, TreeMap, Copy Path, About, MB, window title, total/docs, star headers và automation names.
- Recycle Bin bind availability/pending state: Restore/Permanent Delete disabled nếu không có selection; Empty Trash disabled khi list rỗng.
- Report có empty state cho cả subject/type và 7-day/12-month charts.

### Theme ownership

- `ColorTokens.axaml` là owner duy nhất của color, spacing, radius, typography token.
- `SharedStyles.axaml` là owner duy nhất của primitive selector `primary`, `secondary`, `danger`, `success`, `card`, `stat-card`, `field-label`, `info-banner`.
- `AppTheme.axaml` chỉ giữ icon/resource theme cần thiết, không duplicate component selector.
- Loại duplicate selector và compatibility style không còn active, không phụ thuộc load order để quyết định visual outcome.

### State verification

- Dashboard/AddEdit/Report/Batch/Recycle có explicit loading, empty, error, retry và disabled/pending states phù hợp.
- Validate ở 960×640, 1280×800, 150% scaling, Japanese/Chinese locale, keyboard Tab/Escape và HighContrast theme variant.
- Avalonia headless setup thêm package cùng version 11.2.7, one-time application builder, `[AvaloniaFact]`/`[AvaloniaTheory]`.
- Dashboard headless không dùng sleep cho `AttachedToVisualTree` + timer. Slice binding chỉ kiểm tra XAML load/control binding deterministically; lifecycle interaction ở Dashboard cần explicit readiness seam riêng hoặc manual smoke.
- Cập nhật `docs/TEST_MATRIX.md` và các tài liệu verification liên quan trong cùng slice: trước implementation ghi status planned/in-progress trung thực, sau implementation ghi exact test class, CI command và proof thực tế.

## Verification strategy

### Test layers

| Layer | Proof |
| --- | --- |
| SQLite migration/integrity | temp legacy schema fixtures, orphan detection, valid rebuild, `foreign_key_check`, direct child row assertions |
| Repository/data | state-transition boundaries, transaction rollback, cascade, taxonomy restore, invalid FK write rejection |
| Services | backup/restore round-trip, CSV security/locale/escaping/write errors |
| ViewModels | false-success paths, filter state, bulk selection retention, feedback states |
| Avalonia headless | Add/Edit binding, Dashboard columns/sort/star, Related Documents candidate list, disabled/destructive actions, focus/keyboard behavior |
| Manual smoke | Add/Edit, category delete/restore, Recycle Bin, CSV, backup/restore, language switch, desktop scaling |
| CI | restore/build/test/artifact job executes from active paths |

Avalonia headless tests use the documented xUnit headless platform and matching Avalonia version. They target specific regressions, not broad screenshot testing.

### Commands

```powershell
dotnet restore "StudyDocumentManager.sln"
dotnet build "StudyDocumentManager.sln" -c Debug --no-restore
dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Debug --no-build
dotnet build "StudyDocumentManager.sln" -c Release
dotnet test "StudyDocumentManager.Tests\StudyDocumentManager.Tests.csproj" -c Release --no-build
```

## Delivery order

1. Slice 1 binding fix and CI correction.
2. Slice 2 connection/FK legacy migration, recycle transitions, catalog restore and tests.
3. Slice 3 backup/restore and CSV production tests/fixes.
4. Slice 4 flow improvements in bounded feature groups.
5. Slice 5 visual token/style consolidation and Academic utility polish.
6. Run full verification after each slice and final end-to-end smoke.

Slice 2 blocks every later destructive or restore flow. Slice 1 and CI can proceed independently, but CI must be corrected before data safety merges.

## Non-goals

- No Dashboard layout redesign.
- No global compiled-binding migration.
- No new UI framework or component library beyond the Avalonia Headless test package matching the existing Avalonia version.
- No CQRS, event bus, microservices, database replacement or broad schema redesign.
- No automatic deletion/repair of invalid legacy orphan data.
- No CSV import, duplicate hash scanner, versioned backup product or navigation history stack.
- Restore uses a one-operation staged rollback copy and controlled restart only; this is not a persistent backup-history feature.

## Spec self-review

- No placeholders or unresolved migration policy remain.
- Legacy preflight is read-only before any DDL/DML; invalid/orphan/unsupported legacy data blocks safely and remains unchanged.
- Data-loss handling is explicit: destructive operations are state-restricted, FK-validated and cascade-tested at every child endpoint.
- Restore taxonomy and backup restore use explicit transaction/lifecycle contracts with rollback proof.
- Restore candidate must be a supported application database, not merely readable SQLite.
- UI direction preserves the Dashboard layout and Academic utility visual stance.
- Flow scope and visual scope remain separated from persistence contracts.
- Every slice has evidence, exact documentation updates and a verification boundary.

## Review revision record

This revision incorporates independent data-safety, proof/CI and UI/accessibility reviews. Implementation remains blocked until this revised specification is approved.

Key review-driven changes:

- preflight before all legacy migration DDL/DML;
- enumerated FK endpoints plus schema-preserving rebuild policy;
- data-layer online backup and staged restore result contract;
- explicit connection/pool lifecycle and controlled restart after restore;
- controlled `AddRecentFile` failure contract;
- rollback fault-injection requirements;
- valid-but-unrelated SQLite backup rejection;
- Avalonia Headless setup and deterministic Dashboard test boundary;
- exact TEST_MATRIX/verification-document update requirement;
- contrast, high-contrast, localization, accessibility and stat-card constraints.
