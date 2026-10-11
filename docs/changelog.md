# Nhật ký thay đổi (Changelog) — VChibi

Tất cả các thay đổi đáng chú ý của dự án VChibi được ghi nhận tại đây theo định dạng [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) và tuân thủ [Semantic Versioning](https://semver.org/).

---

## [Unreleased]

### Đã thêm
- GitHub Actions: PR vào `main` chạy test và đóng gói Windows; push vào `main` tự tạo tag và GitHub Release sau khi build/test/publish thành công.
- ZIP self-contained chứa đầy đủ runtime, assets và thông báo giấy phép. Xem [quy trình phát hành](release.md).

## [1.0.0] — 2026-10-11

### Đã thêm (Added)
- **Kiến trúc Scaffolding & Nền tảng**:
  - Xây dựng ứng dụng hoàn chỉnh trên .NET 10 WPF theo kiến trúc MVVM (`CommunityToolkit.Mvvm`).
  - Giao diện chủ đạo phong cách Light Lavender sử dụng thư viện `WPF-UI 4.3.0` với điểm nhấn màu sắc `#7862B8`.
  - Cơ chế khởi chạy đơn phiên bản duy nhất (Single-Instance) bằng tệp khóa độc quyền `.instance.lock` kèm cờ xóa khi đóng (`DeleteOnClose`).
- **Desktop Runtime & Trải nghiệm không cản trở**:
  - Cửa sổ nhân vật `PetWindow` trong suốt với các cờ Win32 mở rộng: `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`.
  - Cho phép chuột, bàn phím, cuộn chuột và bôi đen văn bản xuyên thấu hoàn toàn xuống các ứng dụng bên dưới mà không cướp focus.
  - Cửa sổ không hiển thị biểu tượng trên thanh tác vụ (Taskbar) hay danh sách chuyển ứng dụng (Alt+Tab).
  - Trình phát hoạt ảnh `SpritePlayer` phát các chuỗi khung hình PNG với tần số quét độc lập dựa trên `CompositionTarget.Rendering`.
  - Cỗ máy trạng thái chuyển đổi tự nhiên giữa `Idle` và `Walk`:
    - Di chuyển ngẫu nhiên đều khắp vùng làm việc màn hình chính (không áp dụng quy tắc 70/30, không theo dõi cửa sổ làm việc).
    - Tự động lật hướng nhân vật (Flip X) theo chiều bước đi.
    - Giới hạn nhân vật an toàn trong biên màn hình (Boundary Clamping) theo DPI thực tế.
    - Cơ chế chặn giật lag / ngủ đông (Delta Clamping) chống tua vọt vị trí khi hệ thống bị chậm hoặc khôi phục sau Sleep.
- **Quản lý Thư viện & Cài đặt Cá nhân hóa**:
  - Cửa sổ quản lý `LibraryWindow` hiển thị danh sách các nhân vật trong kho lưu trữ.
  - Điều chỉnh tỉ lệ hiển thị (Scale từ 25% đến 400%) và tốc độ di chuyển (Speed từ 5 đến 200 DIP/s).
  - Độc lập hoàn toàn giữa pet đang chọn trong danh sách và pet đang chạy ngoài màn hình.
  - Cài đặt được lưu trữ riêng biệt theo mã định danh từng pet (`per-ID settings`) vào `settings.json`.
  - Cơ chế tự phục hồi cài đặt: cảnh báo khi tệp `settings.json` bị hỏng cú pháp với tùy chọn "Thoát không lưu" nhằm bảo toàn nguyên vẹn tệp tin gốc.
- **Trình chỉnh sửa Pet (Pet Editor) Trực quan**:
  - Giao diện tạo mới và chỉnh sửa pet tích hợp sẵn, không yêu cầu viết tệp JSON.
  - Hỗ trợ chọn nhiều tệp ảnh PNG qua hộp thoại hệ thống native `OpenFileDialog`.
  - Tách biệt hai động tác chính: Đứng (Idle) và Đi (Walk) với thanh trượt FPS độc lập (1–60 FPS).
  - Cho phép sắp xếp lại thứ tự khung hình (Lên/Xuống) và xóa khung hình linh hoạt.
  - Khung xem trước chuyển động (SpritePreview) với các nút phát/tạm dừng và lật hướng xem thử.
  - Cơ chế bảo vệ thay đổi chưa lưu (Dirty Guard): cảnh báo khi người dùng bấm Hủy để tránh mất dữ liệu ngoài ý muốn.
  - Hai chế độ lưu: "Lưu thay đổi" (chỉ lưu vào kho) và "Lưu và thả ra desktop" (lưu và kích hoạt chạy ngay).
  - Hỗ trợ Hot Reload khi chỉnh sửa trực tiếp nhân vật đang chạy ngoài màn hình mà không làm gián đoạn phiên runtime.
- **Nhập gói Pet từ Thư mục (Package Import)**:
  - Nhập gói pet có sẵn từ thư mục qua hộp thoại chọn thư mục native (`IFileOpenDialog` với `FOS_PICKFOLDERS`).
  - Kiểm tra tính hợp lệ toàn diện (`PetValidator`):
    - Đọc trước header IHDR 33-byte để xác thực chuẩn PNG và kiểm soát ngân sách bộ nhớ (Draft Memory Budget).
    - Ngăn chặn tấn công Ancillary Chunks phình to bộ nhớ (SEC-01).
    - Tính toán chính xác dung lượng bộ nhớ cho ảnh PNG RGBA 16-bit.
    - Chống tấn công vượt cấp thư mục (Path Traversal) và từ chối các liên kết tượng trưng (Symlinks / Reparse Points).
  - Tự động sinh mã định danh duy nhất (`-2`, `-3`) khi nhập lại cùng một thư mục gói để tránh ghi đè dữ liệu.
  - Tự động sao chép tài nguyên ảnh vào kho lưu trữ nội bộ, cho phép xóa/di chuyển ảnh gốc bên ngoài mà không ảnh hưởng ứng dụng.
- **Tích hợp Khay hệ thống (System Tray)**:
  - Ẩn cửa sổ quản lý về khay hệ thống khi bấm nút đóng (`WM_CLOSE`).
  - Khôi phục cửa sổ quản lý khi nhấp đúp vào biểu tượng khay hệ thống (`WM_TRAYICON / WM_LBUTTONDBLCLK`).
  - Nút "Thoát ứng dụng" đóng sạch sẽ toàn bộ tiến trình, tắt timers và giải phóng tệp khóa hệ thống.
- **Đóng gói Phân phối (Standalone Self-Contained Publish)**:
  - Cấu hình xuất bản `win-x64` self-contained tại `artifacts/publish/win-x64/`.
  - Tích hợp biểu tượng ứng dụng `Assets/app.ico`, gói nhân vật mẫu `Assets/DefaultPet/` và tệp bản quyền bên thứ ba `NOTICES.md`.
  - Đã nghiệm thu thực tế hoạt động độc lập ngoài thư mục mã nguồn qua kịch bản smoke đại diện (tạo pet, nhập gói, độc lập selection, lặp hide/show/replace, phục hồi settings hỏng, va chạm đơn tiến trình trong tray và khi mở bản nháp editor; các ca phần cứng ACPI Sleep, máy Windows sạch không cài SDK và race ghi đĩa dở dang giữ trạng thái giới hạn mở).
