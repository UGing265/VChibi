# Phase 1 — Project, MVVM và UI light lavender

## Context

[Plan](./plan.md) · [Requirements](./requirements.md) · [Architecture](./architecture.md) · [Research](./research.md) · [Execution contract](./execution-contract.md).
Priority P1. Status Pending. Không dependency source; chưa có code để tái sử dụng.

## Mục tiêu và yêu cầu

- Có project WPF thật, chạy UI quản lý light/lavender bằng WPF UI và binding MVVM.
- Một project app; không Generic Host/DI container/backend.
- Phase này không phải deliverable cuối; tiếp tục ngay phase 2, không giao khung UI như app hoàn chỉnh.

## File liên quan

Root tuyệt đối R = D:/AShiroru/ProgramCode/Project/Personal/VChibi/. Các path sau nối với R, action Create:

- VChibi.sln; src/VChibi/VChibi.csproj; src/VChibi/App.xaml; src/VChibi/App.xaml.cs; src/VChibi/app.manifest.
- src/VChibi/Views/LibraryWindow.xaml và LibraryWindow.xaml.cs.
- src/VChibi/ViewModels/LibraryViewModel.cs.
- src/VChibi/Resources/Colors.xaml và Styles.xaml.
- src/VChibi/packages.lock.json khi restore.
- Không delete file hiện có. Không tạo các folder/class trống còn lại.

## Kiến trúc

- App composition trực tiếp, constructor injection đơn giản.
- WPF UI theme resources tại App; custom resource chỉ cho style app cần, không viết lại control toolkit.
- ObservableObject/RelayCommand qua CommunityToolkit.Mvvm; field-based generation, partial ViewModel, không ngôn ngữ preview.
- PMv2 manifest cho coordinate contract. Review fix A4: nghiệm thu trên Windows 11 máy hiện tại; không tự quảng cáo mọi Windows 10 1809+ được hỗ trợ. OS/edition release claim phải đối chiếu bảng .NET 10 và pinned toolkit, có smoke riêng trước claim.

## Bước triển khai

1. Tạo solution/project net10.0-windows, UseWPF=true; bật UseWindowsForms=true để tray native ở phase 4. Tránh type ambiguity bằng aliases/qualification.
2. Pin WPF-UI 4.3.0 và CommunityToolkit.Mvvm 8.4.2, restore lock. Nếu package/API khác docs main, đối chiếu pinned assembly/docs; sửa cấu hình, không đổi toolkit vì convenience.
3. Nạp ThemesDictionary Theme=Light và ControlsDictionary theo thứ tự tài liệu.
4. Áp dụng lavender #7862B8 bằng ApplicationAccentColorManager app-local, không SystemThemeWatcher/SystemAccent. Kiểm tra contrast các state; nhạt chỉ làm selection background, không body text.
5. Giữ default text font của toolkit; icon dùng toolkit. Không chép font Microsoft từ máy vào repo.
6. Bố cục LibraryWindow gồm header, thư viện bên trái, preview giữa, cài đặt phải. Chưa có pet thì hiển thị empty/loading state thật, không fake item.
7. Chỉ wiring commands có hành vi thật trong phase; chưa có command thì không tạo no-op giả có thể dùng. Các phần còn thiếu hoàn thành liên tục ở phase 4/5.
8. Tạo app.manifest PMv2; set ShutdownMode.OnExplicitShutdown, thêm đường thoát thật trước khi chạy để không để process UI sót.
9. Restore/build sau batch edit; mở app thật bằng dotnet run --project src/VChibi/VChibi.csproj. Quan sát control/theme/font/keyboard Tab/exit.

## Checklist

- [x] Project target/package được pin và restore/build thành công.
- [x] App thật mở được, control light/lavender và font đúng nguồn mặc định.
- [x] UI binding MVVM hoạt động, không fake dữ liệu.
- [x] Manifest và exit ban đầu hợp lệ; không copy font không rõ quyền.
- [x] Ghi evidence phase 1 rồi chuyển phase 2 ngay.

## Thành công và kiểm chứng

- UI không exception khi nạp dictionaries; không dùng màu accent Windows thay Lavender khi hệ thống đổi màu.
- Text normal tối trên nền sáng, button primary chữ contrast ít nhất 4.5:1, focus thấy rõ.
- Ghi observed package/API/font behavior, command và screenshot nếu môi trường cho phép. Không nói pet chạy ở phase này.

## Rủi ro / an toàn

- Docs main vs NuGet pin: build/runtime thật là gate.
- Font/icon license: giữ notices, không embed Segoe thủ công.
- WinForms/WPF namespace trùng: chỉ tray dùng WinForms, không thay control WPF.

## Next

Đánh phase hoàn tất khi gate đạt; tiếp tục [phase 2](./phase-02-pet-storage.md) trong cùng luồng, không hỏi duyệt lại.
