# Study Document Manager

<div align="center">

![Study Document Manager](study-document-manager/assets/logo/hero-banner.png)

> **Quản lý tài liệu cá nhân — Đơn giản, Hiệu quả, Riêng tư**

[![.NET Framework](https://img.shields.io/badge/.NET_Framework-4.8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![SQLite](https://img.shields.io/badge/SQLite-Local_DB-003B57?style=for-the-badge&logo=sqlite&logoColor=white)](https://www.sqlite.org/)
[![C#](https://img.shields.io/badge/C%23-Windows_Forms-239120?style=for-the-badge&logo=csharp&logoColor=white)](https://docs.microsoft.com/dotnet/desktop/winforms/)
[![Windows](https://img.shields.io/badge/Platform-Windows-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![AI-Ready](https://img.shields.io/badge/AI-MiMo_Ready-FF6B35?style=for-the-badge&logo=robot&logoColor=white)]()

[![Version](https://img.shields.io/badge/Version-3.1.2-14B8A6?style=for-the-badge&logo=semver&logoColor=white)](https://github.com/hayato-shino05/study-document-manager/releases)
[![Downloads](https://img.shields.io/github/downloads/hayato-shino05/study-document-manager/total?style=for-the-badge&color=10B981&logo=github&logoColor=white&label=Downloads)](https://github.com/hayato-shino05/study-document-manager/releases)
[![License](https://img.shields.io/badge/License-MIT-F59E0B?style=for-the-badge&logo=opensourceinitiative&logoColor=white)](LICENSE)
[![Stars](https://img.shields.io/github/stars/hayato-shino05/study-document-manager?style=for-the-badge&color=EF4444&logo=github&logoColor=white)](https://github.com/hayato-shino05/study-document-manager)

</div>

---

## Câu chuyện đằng sau dự án

Tôi nhận ra rằng việc quản lý tài liệu học tập — bài giảng, đề thi, bài thực hành, tài liệu tham khảo — thường rất lộn xộn. Tôi cần một công cụ đủ mạnh để tổ chức hàng trăm file, đủ đơn giản để chạy ngay mà không cần cài SQL Server, và đủ thông minh để tìm đúng tài liệu trong vài giây.

Vì vậy, **Study Document Manager** ra đời — một ứng dụng Windows Forms thuần túy, hoạt động hoàn toàn offline, mang đến trải nghiệm quản lý tài liệu chuyên nghiệp ngay trên máy tính Windows của bạn.

Và từ những ngày đầu, tôi đã thiết kế để ứng dụng này **sẵn sàng cho AI** — kiến trúc rõ ràng, data layer tách biệt, và interface được chuẩn bị để tích hợp các mô hình AI mạnh mẽ như Xiaomi MiMo.

---

## Tính năng nổi bật

- 🚀 **Portable & Offline**: Chạy ngay không cần cài đặt database server. Dữ liệu lưu trong file `.db` cục bộ.
- 📂 **Quản lý tài liệu**: Thêm, sửa, xóa, tìm kiếm nhanh theo tên, danh mục, loại.
- 🎨 **Giao diện hiện đại**: Theme Teal/Emerald phẳng, đẹp mắt, Toast Notification mượt mà.
- 🏷️ **Phân loại thông minh**: Sắp xếp theo Danh mục (Subject), Loại file (PDF, Word...), Bộ sưu tập (Collections).
- 🌳 **Cây phân loại (Sidebar)**: Sidebar dạng cây cho phép duyệt tài liệu theo Danh mục, Loại file, Bộ sưu tập, Quan trọng — hiển thị số lượng từng nhóm, click để lọc nhanh.
- ⭐ **Đánh dấu quan trọng**: Ghim các tài liệu ưu tiên.
- 🔍 **Bộ lọc mạnh mẽ**: Lọc theo ngày, dung lượng, trạng thái, từ khóa.
- 📊 **Thống kê trực quan**: Biểu đồ phân bố tài liệu, timeline hoạt động.
- 🗺️ **TreeMap**: Hiển thị phân bố tài liệu theo danh mục hoặc loại file dưới dạng bản đồ TreeMap trực quan, hỗ trợ hover và click tương tác.
- 📤 **Xuất dữ liệu**: Xuất danh sách tài liệu ra file CSV.
- 📝 **Ghi chú cá nhân**: Thêm ghi chú và trạng thái riêng cho từng tài liệu.
- ⏰ **Quản lý Deadline**: Theo dõi tài liệu sắp đến hạn và quá hạn.
- 🔄 **Tự động cập nhật**: Kiểm tra phiên bản mới từ GitHub Releases.
- 🧹 **Kiểm tra file rác**: Tự động phát hiện các liên kết file bị hỏng (file đã xóa khỏi ổ cứng).
- 📥 **Import hàng loạt**: Chọn thư mục → quét tất cả file hỗ trợ → tự động điền thông tin (tên, loại, dung lượng) → import vào DB cùng lúc. Hỗ trợ lọc theo loại file và xem trước danh sách trước khi import.
- 🗑️ **Thùng rác (Recycle Bin)**: Xóa mềm (soft delete) tài liệu, không ảnh hưởng file thật trên ổ cứng. Hỗ trợ khôi phục từng tài liệu hoặc xóa vĩnh viễn, dọn sạch thùng rác.
- ⌨️ **Phím tắt**: `Ctrl+N` thêm mới, `Ctrl+F` tìm kiếm, `Del` xóa, `F5` làm mới, `Ctrl+E` xuất CSV, `Ctrl+O` mở file.
- ☑️ **Quản lý hàng loạt**: Form riêng biệt cho phép tìm kiếm, lọc theo danh mục/loại, chọn nhiều tài liệu bằng checkbox → xóa hàng loạt / đổi danh mục / đánh dấu quan trọng cùng lúc.
- 📄 **Xem trước**: Panel xem trước nội dung file ngay trong ứng dụng (hỗ trợ hình ảnh). Không cần mở ứng dụng ngoài để kiểm tra nhanh.
- 🕐 **Lịch sử mở gần đây**: Tự động ghi nhận file đã mở, hiển thị danh sách 20 file gần nhất. Hỗ trợ mở lại nhanh hoặc xóa lịch sử.
- 💾 **Backup & Restore Database**: Sao lưu toàn bộ database ra file `.db` và khôi phục khi cần. Bảo vệ dữ liệu trước rủi ro mất mát.
- 🔗 **Liên kết tài liệu liên quan**: Tạo liên kết giữa các tài liệu có nội dung liên quan. Dễ dàng tìm thấy tài liệu tham khảo từ tài liệu hiện tại.
- 🔍 **Phát hiện file trùng lặp**: Quét và phát hiện các tài liệu có cùng tên hoặc đường dẫn file trùng nhau. Hỗ trợ xử lý nhanh (xóa, giữ lại).
- 📎 **Drag & Drop**: Kéo thả file trực tiếp vào bảng danh sách để thêm tài liệu nhanh chóng.

---

## Tại sao dự án này đáng được hỗ trợ?

### Vấn đề thực tế

Học sinh, sinh viên, giáo viên và nhân viên văn phòng mỗi ngày đối mặt với hàng trăm tài liệu số — bài giảng, đề thi, slide, tài liệu tham khảo, ghi chú cá nhân. Tìm đúng file vào đúng lúc là thách thức thực sự. Các giải pháp hiện tại hoặc quá phức tạp (cần server, đăng nhập), hoặc quá đơn giản (chỉ đổi tên file), và hầu như không có công cụ nào thực sự **hiểu** nội dung tài liệu của bạn.

### Giải pháp của tôi

**Study Document Manager** giải quyết bài toán này bằng cách kết hợp:

1. **Tổ chức có hệ thống** — Phân loại, gắn thẻ, tạo bộ sưu tập, theo dõi deadline
2. **Tìm kiếm thông minh** — Filter đa chiều, tìm kiếm nhanh, phát hiện trùng lặp
3. **Trực quan hóa dữ liệu** — Biểu đồ, TreeMap, timeline để hiểu rõ bộ sưu tập của mình
4. **Sẵn sàng cho AI** — Kiến trúc MVP + Repository Pattern, data layer tách biệt, sẵn sàng tích hợp AI để phân loại tự động, gợi ý thông minh, và tìm kiếm ngữ nghĩa

### Tầm nhìn AI

Tôi đang lên kế hoạch tích hợp **Xiaomi MiMo API** để mang đến những khả năng mới:

- **Phân loại tự động bằng AI** — Thay vì tự gắn thẻ, AI sẽ phân tích nội dung file và đề xuất danh mục phù hợp
- **Tìm kiếm ngữ nghĩa** — Tìm tài liệu bằng câu hỏi tự nhiên thay vì từ khóa chính xác
- **Tóm tắt tài liệu tự động** — Tạo abstract ngắn cho mỗi tài liệu để nhanh chóng nắm bắt nội dung
- **Dự đoán deadline** — AI học từ thói quen học tập của bạn để nhắc nhở thời điểm ôn tập tối ưu
- **Gợi ý tài liệu liên quan** — Khi mở một tài liệu, AI gợi ý các file có nội dung bổ trợ nhau

Với **Xiaomi MiMo 100T Token Creator Incentive Plan**, tôi có đủ token để biến những ý tưởng này thành hiện thực — hoàn toàn miễn phí, hoàn toàn phi lợi nhuận, vì lợi ích của cộng đồng học tập.

---

## Giao diện & Trải nghiệm

![Dashboard chính](study-document-manager/assets/example/1.png)

![Hiển thị Preview](study-document-manager/assets/example/2.png)

### Dashboard chính
- Menu bar và Toolbar truy cập nhanh (Thêm, Sửa, Xóa, Mở file, Import, Thùng rác).
- Danh sách tài liệu dạng lưới (Grid) với icon trực quan theo loại file.
- Sidebar cây phân loại: duyệt theo Danh mục, Loại file, Bộ sưu tập, Quan trọng — hiển thị badge số lượng.
- Panel tìm kiếm và bộ lọc (Filter) tiện lợi bên trái.
- Phím tắt nhanh: `Ctrl+N`, `Ctrl+F`, `Del`, `F5`, `Ctrl+E`.
- Truy cập nhanh "Quản lý hàng loạt" qua menu Công cụ.

### Quản lý hàng loạt
- Form riêng biệt với bảng danh sách checkbox.
- Tìm kiếm theo tên, lọc theo danh mục và loại tài liệu.
- Chọn tất cả / Bỏ chọn tất cả nhanh chóng.
- Các thao tác: Xóa hàng loạt, Đánh dấu quan trọng, Đổi danh mục.

### Thêm/Sửa tài liệu
- Tự động điền tên và tính dung lượng file khi chọn file từ máy tính.
- Gắn thẻ (Tag), chọn danh mục, thêm ghi chú cá nhân.

### Thống kê (Reports)
- Tổng quan số lượng tài liệu.
- Biểu đồ tròn (Pie Chart) phân bố theo danh mục/loại.
- Biểu đồ cột (Bar Chart) timeline thêm tài liệu.
- TreeMap phân bố tài liệu tương tác (hover, click).

### Notification System
- Hệ thống thông báo **Toast** hiện đại, không làm gián đoạn công việc (Non-blocking).
- 4 trạng thái: Success (Xanh), Error (Đỏ), Warning (Cam), Info (Lam).

---

## Công nghệ sử dụng

- **Ngôn ngữ**: C# (.NET Framework 4.8)
- **UI Framework**: Windows Forms (WinForms)
- **Database**: SQLite (System.Data.SQLite)
- **Biểu đồ**: System.Windows.Forms.DataVisualization
- **Architecture**: MVP (Model-View-Presenter), Repository Pattern
- **AI Integration**: Ready for Xiaomi MiMo API

---

## Cài đặt và Chạy

### Yêu cầu hệ thống
- Windows 7/8/10/11.
- .NET Framework 4.8 Runtime.

### Hướng dẫn chạy (Run)
1. **Clone repository**:
   ```bash
   git clone https://github.com/hayato-shino05/study-document-manager.git
   cd study-document-manager
   ```
2. **Mở project**:
   - Mở file `study-document-manager.sln` bằng Visual Studio 2019/2022.
3. **Build & Run**:
   - Nhấn `F5` hoặc nút **Start**.
   - Database SQLite sẽ tự động được khởi tạo tại `bin/Debug/data/study_documents.db`.

---

## Lộ trình phát triển

- [x] MVP — Quản lý tài liệu cơ bản
- [x] Import hàng loạt & Quản lý hàng loạt
- [x] Thống kê & Trực quan hóa (Pie Chart, Bar Chart, TreeMap)
- [x] Phát hiện trùng lặp & Kiểm tra file rác
- [x] Backup & Restore Database
- [ ] **Tích hợp Xiaomi MiMo API** — Phân loại tự động bằng AI
- [ ] **Tìm kiếm ngữ nghĩa** — AI-powered search
- [ ] **Tóm tắt tài liệu tự động**
- [ ] **Gợi ý tài liệu liên quan** — Smart recommendations
- [ ] **Dự đoán deadline** — Deadline prediction

---

## Đóng góp

Mọi đóng góp đều được chào đón!
1. Fork dự án.
2. Tạo branch mới (`git checkout -b feature/AmazingFeature`).
3. Commit thay đổi (`git commit -m 'Add some AmazingFeature'`).
4. Push lên branch (`git push origin feature/AmazingFeature`).
5. Tạo Pull Request.

---

## Tác giả

**hayato-shino05**
- Email: [hayatoshino05@gmail.com](mailto:hayatoshino05@gmail.com)
- GitHub: [@hayato-shino05](https://github.com/hayato-shino05)

---

<div align="center">
Made with ❤️ by hayato-shino05 | © 2025
</div>
