# Phase 4 — Thư viện, cài đặt, tray và vòng đời

## Context

[Plan](./plan.md) · [Requirements](./requirements.md) · [Architecture](./architecture.md) · [Execution contract](./execution-contract.md).
Priority P1. Status In Progress. Dependency phase 3 session/input đã smoke, phase 2 storage/settings. Còn 5 tiêu chí mở tại checklist.

## Mục tiêu và yêu cầu

LibraryWindow sử dụng pet thật, selection/active tách biệt, điều chỉnh scale/speed, nhớ trạng thái, close-to-tray và exit sạch. Startup có pet mẫu hoặc pet lần trước; không giao UI chỉ xem ảnh.

## File liên quan

Root tuyệt đối R = D:/AShiroru/ProgramCode/Project/Personal/VChibi/.
Modify src/VChibi/Views/LibraryWindow.xaml/.xaml.cs; ViewModels/LibraryViewModel.cs; App.xaml/.xaml.cs; Models/AppSettings.cs và PetDisplaySettings.cs; Services/SettingsStore.cs; Runtime/DesktopPetSession.cs khi nối contract.
Create src/VChibi/Controls/SpritePreview.xaml/.xaml.cs; src/VChibi/Platform/Windows/TrayIcon.cs. PetListItemViewModel chỉ tách khi status/selection logic cần.
Không thêm navigation pages/sidebar nhiều tầng; không delete file ngoài scope.

## Kiến trúc

- Một LibraryWindow được hide/show, không recreate mỗi tray command.
- Library selected pet điều khiển preview; active ID session điều khiển trạng thái desktop. Selection không tự ActivatePet.
- Shared SpritePlayer algorithm với playback state riêng; preview chỉ chạy khi view visible/play.
- App giữ shutdown flag, session, tray, cancellation, singleton UI object. Close-to-tray không dùng pet Owner.
- NotifyIcon menu native có Mở quản lý, Hiện/Ẩn, Thoát; không thêm package tray/theme khác.

## Bước triển khai

1. Review fix F1: lấy exclusive ownership lock data root trước mọi recovery/seed/load settings; instance thứ hai báo app đang chạy rồi thoát, không thêm pet/tray/writer. Load store/settings async sau lock; release lock cuối shutdown.
2. Bind danh sách/selected/active status thật, preview idle/walk; natural ordering tên thư viện có tie ID, không dựa tên làm key.
3. Commands Thả ra desktop/Ẩn khỏi desktop/Sửa pet/Tạo pet/Import/Thoát; hai luồng editor/import nối đầy đủ phase 5. Không để command no-op trong deliverable cuối.
4. Activate chỉ khi user bấm, load guard/lỗi giữ active cũ. Review fix F4: khi hidden, Hiện từ UI/tray activate selected ID nếu khác active; load lỗi giữ hidden, không tự hiện active cũ. Selection khi pet đang hiện vẫn chỉ preview.
5. Scale default 100%, input range hữu hạn hợp lý (25–400% nhưng effective fit màn hình); speed default 40 DIP/s, range 5–200 DIP/s. Nếu fit giảm scale, UI phản ánh actual effective size và lý do.
6. Áp dụng scale/speed ngay cho active pet; với selected pet chưa active lưu settings theo ID, không đổi active khác. Không chỉnh FPS animation từ slider tốc độ đi.
7. Persist selected ID, active ID, visibility và settings theo pet. Startup restore active nếu còn hợp lệ; UI selected có thể khác. Snapshot visibility trước shutdown cleanup để lần mở sau không luôn ẩn do Stop.
8. Missing/invalid active package báo rõ, cho chọn pet hợp lệ. Review fix F2/F3: settings sai cú pháp hoặc ngữ nghĩa vào Reset/Cancel; persistence disabled trước quyết định. Reset xác nhận mới bật lưu defaults; Cancel thoát không ghi file gốc, không autosave/exit-save.
9. Close LibraryWindow hide; preview stop, active pet tiếp tục; minimize cũng không ẩn pet. Tray double-click/menu marshaled đúng dispatcher, show/activate UI quản lý được phép.
10. Exit flag chặn hide lại; save snapshot chỉ khi persistence enabled, cancel pending theo commit contract, stop preview/session, dispose tray, đóng UI/Shutdown; release ownership lock sau cùng. Write failure có Retry/Exit không lưu; corrupt-settings Cancel đi thẳng cleanup không save.
11. Exercise thao tác thật sau batch edit; integration owner kiểm tra restore/build/test phù hợp, không tests tautological binding/commands.

## Smoke và tiêu chí thành công

- A01/A04/A05: startup default, select pet chưa đổi active; activate đổi đúng, chỉ một HWND pet visible.
- A06/A15: scale/speed áp dụng đúng target; FPS không đổi. Tăng scale gần biên/oversize, đổi work area/DPI khi có thể: clamp đúng footprint, không random interval âm.
- A13: lặp close/open tray, hide/show, replace; một session/scheduler, không stale update. Controls gate chuyển từ phase 3 theo S1. Case active=A, selected=B, hide→Hiện phải hiện B; reopen saved-hidden rồi Hiện cũng dùng selected B. Mở exe lần hai (cả khi commit pending) không thêm pet/writer hoặc chạy recovery trên operation sống.
- A14: exit process kết thúc; reopen phục hồi cả selected/active/visibility và display settings.
- A17: mất package, settings sai cú pháp hoặc JSON hợp lệ nhưng scale=0/speed âm/map null: báo rõ. Corrupt→Cancel→Exit giữ bytes file gốc; Reset chỉ ghi sau xác nhận. Không làm mất thư viện còn hợp lệ.

## Checklist

- [x] Library UI binding & preview commands (Idle/Walk radio, Scale/Speed sliders, Thả/Ẩn desktop pet).
- [x] Selected vs active độc lập trên live UI đa pet: Thực nghiệm `smoke_phase4_library_lifecycle.ps1` chọn Pet 2 trong UI không làm đổi pet đang chạy (Pet 1 giữ nguyên 240px width) cho đến khi bấm Release.
- [x] Scale/Speed settings áp dụng live và persist đúng cho pet hiện hành (Scale 100%->200% mở rộng PetWindow 120x120px -> 240x240px, Speed cập nhật 100 DIP/s, lưu vào settings.json).
- [x] Per-ID switching & activation trên live UI: Pet 1 (200%/100 DIP/s) và Pet 2 (150%/60 DIP/s); đổi chọn cập nhật đúng thanh trượt; kích hoạt Pet 2 co giãn chuẩn footprint physical `30x30px` (16 * 1.5 * 1.25 DPI); khởi động lại khôi phục đúng `30x30px`.
- [x] Settings corruption UX & Cancel guard: phát hiện file lỗi, hiển thị banner cảnh báo, "Thoát không lưu" giữ nguyên 100% bytes gốc không ghi đè.
- [x] Settings reset confirmation: yêu cầu xác nhận trước khi ghi defaults, thất bại ghi đĩa giữ recovery gated (Unit tests phủ nhận/chấp nhận/ghi đĩa lỗi).
- [x] Single-instance lock rejection: tiến trình thứ 2 phát hiện `.instance.lock`, hiển thị thông báo, thoát sạch mã 0, tiến trình chính không bị ảnh hưởng.
- [x] Tray lifecycle & close-to-hide: `WM_CLOSE` ẩn LibraryWindow nhưng PetWindow tiếp tục chạy; `WM_TRAYICON` (double-click) khôi phục LibraryWindow.
- [x] Clean exit & lock release: ExitButton đóng app sạch sẽ, `.instance.lock` tự xóa khi đóng (`DeleteOnClose`).
- [x] Relaunch restore state: khởi động lại nạp đúng `settings.json`, tái tạo PetWindow ở đúng kích thước và trạng thái hiển thị/ẩn đã lưu.
- [ ] Preview FPS invariant thực tế qua đo đạc trực tiếp nhiều mức tốc độ (Mở; code dùng timer độc lập không đổi FPS, nhưng chưa có script đo FPS live).
- [ ] Edge/DPI boundary clamping ở mức scale cực đại 400% khi kéo sát biên (Mở; đã unit test hàm clamp, chưa kéo sát biên live).
- [ ] Minimize SW_MINIMIZE vs Close trong Phase 4 (Mở; đã test ở Phase 3 smoke, Phase 4 mới test close-to-hide).
- [ ] Khởi chạy UI với settings JSON sai ngữ nghĩa (speed âm, scale=0) (Mở; unit tests đã phủ, chưa khởi chạy live UI riêng cho ca này).
- [ ] Pending write race condition guard khi thoát trong lúc ghi đĩa dở dang (Mở; đã test unit test atomic rename, chưa giả lập live race condition).
- [x] Phục hồi trạng thái ẩn (IsPetVisible=false) khi mở lại app: Thực nghiệm `smoke_phase6_publish_standalone.ps1` (bước 9) xác nhận lưu `IsPetVisible=false`, mở lại PetWindow không hiển thị, bấm Thả hiện lại đúng 60x60px.
## Rủi ro / an toàn

- Show/hide event trùng gây scheduler tăng tốc: session có trạng thái idempotent.
- NotifyIcon callback khác thread: marshal dispatcher, dispose events rõ.
- Exit trong lúc save: commit/cancellation point rõ, không xóa dữ liệu committed.
- Không tự auto-start Windows hoặc telemetry. Không giả tray menu native có lavender styling.

## Next

Tiếp tục [phase 5](./phase-05-editor-import.md) để hoàn thiện toàn bộ luồng tạo/sửa/import đã yêu cầu.
