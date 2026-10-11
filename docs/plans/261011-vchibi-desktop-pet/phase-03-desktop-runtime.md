# Phase 3 — Pet desktop, animation, di chuyển và input

## Context

[Plan](./plan.md) · [Requirements](./requirements.md) · [Architecture](./architecture.md) · [Win32 evidence](./research.md) · [Execution contract](./execution-contract.md).
Priority P1. Status Pending. Dependency phase 2: gói/asset thật, phase 1: WPF/manifest.

## Mục tiêu và yêu cầu

Một pet thật đứng/đi ngẫu nhiên toàn work area primary monitor, alpha trong suốt, topmost thông thường, xuyên chuột và không giành focus. Không kéo/click reaction, không 70/30, không chỉ đi đáy màn hình.

## File liên quan

Root tuyệt đối R = D:/AShiroru/ProgramCode/Project/Personal/VChibi/. Create:

- src/VChibi/Views/PetWindow.xaml và PetWindow.xaml.cs.
- src/VChibi/Runtime/DesktopPetSession.cs; PetController.cs; PetMovement.cs; SpritePlayer.cs. PetState/SpriteFrameLoader chỉ tách nếu cần.
- src/VChibi/Platform/Windows/PetWindowInterop.cs; ScreenWorkspace.cs.
- tests/VChibi.Tests/Runtime/*Tests.cs cho bounds/timing/frame selection.
Modify src/VChibi/App.xaml.cs để mở default session thực, src/VChibi/app.manifest nếu DPI cần sửa. Không fake preview/runtime.

## Kiến trúc

- PetWindow ordinary WPF Window không Owner=LibraryWindow, style/hooks theo architecture.
- ScreenWorkspace trả primary rcWork physical px + DPI. Movement state giữ physical px; thông số user là DIP.
- PetController quyết định idle/walk/đích; SpritePlayer trả frame theo elapsed, không điều khiển màn hình quản lý.
- DesktopPetSession sở hữu load/window/timer; API behavior Show/Hide/ActivatePet/SetDisplaySettings/Stop, chỉ một active session.
- Một timer active ~30 Hz + Stopwatch; không tạo task/timer riêng cho mỗi frame.

## Bước triển khai

1. Nạp PNG trước playback, cache Pbgra32/OnLoad/Freeze theo native+conversion budget A1. Review fix A2: canvas theo PixelWidth/PixelHeight, 100% một pixel nguồn = một DIP, bottom-center explicit layout và không theo metadata DPI; effective fit work area.
2. Window properties None/AllowsTransparency/Transparent/Topmost/ShowActivated=false/ShowInTaskbar=false/NoResize. Không chrome Fluent/Mica/Acrylic.
3. SourceInitialized áp dụng native styles giữ cờ cũ, add NOACTIVATE/TRANSPARENT/TOOLWINDOW cho layered HWND, kiểm tra native failure. Không dùng HTTRANSPARENT/IsHitTestVisible như proof cross-process.
4. Query primary monitor rcWork, actual HWND rectangle/DPI; convert tốc độ DIP/s sang px/s, SetWindowPos NOACTIVATE/NOZORDER/NOSIZE. Không trộn Left/Top DIP với placement px.
5. Clamp full footprint/origins; oversize fit rõ. Review fix A3: bounds đổi phải revalidate cả destination, clamp/reselect đích và xử lý arrival khi đích trùng vị trí; không để walk mắc kẹt tại biên.
6. Idle 1–4 giây, chọn destination random X/Y trong feasible rect, đi theo vector tới đích rồi nghỉ; xử lý đích trùng không chia zero. Lật sprite khi đổi hướng X.
7. Frame elapsed*fps và skip deadline quá hạn; delay tick không tăng tốc/giảm tốc sai. Sleep/resume reset baseline, clamp delta, không nhảy ra biên.
8. Session replace prepare trước commit, generation/cancellation guard; old load không hiện pet sau khi selection khác/hide/exit. Giữ bản cũ nếu new load lỗi; không hai HWND pet cùng visible.
9. Hide dừng scheduler; Stop detaches Tick/hook/system events, clear Image.Source/frame refs, hủy load. Không dùng timer tĩnh giữ session.
10. Tests cho nonzero bounds/oversize/zero-vector/arrival, shrink bounds giữa walk, delayed frame time/pause; asset smoke hai PNG cùng pixel size khác metadata DPI vẫn cùng geometry. Seed random deterministic trong tests, runtime bình thường.
11. Chạy app thật với default pet, trước khi mở rộng UI kiểm chứng Notepad và all-edge/DPI smoke dưới đây. Nếu lỗi focus/input/DPI, sửa root cause tại phase này.

## Smoke bắt buộc

- Notepad khác process foreground: show pet không đổi foreground; click/double-click/drag-select/scroll/gõ xuyên cả opaque sprite lẫn alpha margin tới Notepad. Management minimize không ẩn pet; không pet taskbar/Alt+Tab entry.
- Quan sát nhiều chu kỳ đứng/đi: cả X và Y đổi, frame/hướng đúng, không taskbar overlap.
- Review fix A5: dùng temporary internal driver qua session/controller/placement thật để đặt và giữ pet tại bốn legal corners/Notepad input target; không thêm public drag/debug feature. Probe GetWindowRect/rcWork trong cùng PMv2 DPI context, ghi DPI/rectangle/foreground PID và thao tác đã thử. Launch 100/125/150/200% khi có quyền; dọn driver ở phase 6, không chỉ chờ random đi đúng góc.
- Sleep/resume và exit đường có sẵn: không teleport hoặc callback chạy sót. Bounds/oversize được kiểm chứng bằng logic tests và HWND smoke tại kích thước hiện có.
- Review fix S1: thao tác UI tăng scale gần biên, hide/show/replace lặp lại thuộc smoke phase 4, vì controls được nối tại đó. Session API/lifecycle vẫn triển khai trong phase 3; không chặn phase 4 chỉ vì thiếu chính controls của phase 4.

## Checklist

- [x] Frame decode/cache/canvas/flip đúng, không disk I/O mỗi tick.
- [x] Idle/walk ngẫu nhiên X/Y và bounds/time tests đạt.
- [x] Cross-process click-through + no-focus smoke đạt (alpha margin, opaque body, double-click selection, drag selection, scroll wheel verified và recorded).
- [x] Primary work area/DPI/full footprint smoke có evidence (bốn góc đạt 100%, DPI=120, 120x120px physical footprint).
- [x] Taskbar / Alt+Tab absence verified (`WS_EX_TOOLWINDOW=True`, `WS_EX_APPWINDOW=False`, không taskbar button trên Shell_TrayWnd).
- [x] Management minimize không ẩn pet, pet tiếp tục di chuyển; clean exit via ExitButton dọn sạch lock file.
- [x] Hide/replace/stop cleanup và stale-load guard triển khai; stop/exit có smoke, controls-driven lifecycle proof tiếp ở phase 4.
- [ ] Sleep/resume thực tế OS (GIỮ OPEN: SystemEvents.PowerModeChanged hook đã triển khai với Dispatcher marshalling, delta time clamp <=0.2s chứng minh qua unit test và live NtSuspendProcess 3.5s freeze 13.0px step, không teleport. Nhưng ACPI OS Sleep/Resume cấp hệ điều hành chưa chạy thực tế trên máy trạm để tránh đưa máy vật lý vào chế độ Sleep).
- [ ] Ghi A02/A03/A15 đầy đủ ở runtime (GIỮ OPEN: tiêu chí sleep/resume OS vẫn đang mở theo quy tắc không fake pass).

## Rủi ro / an toàn

Không quảng cáo phủ UAC/fullscreen exclusive; topmost có giới hạn OS. Không dùng Win32 để điều khiển app khác. Nếu không có quyền đổi DPI, ghi giới hạn và giữ mục đó chưa kiểm chứng; không fake pass.

## Next

Nối session thật vào [phase 4](./phase-04-library-lifecycle.md); không kết thúc sau demo pet.
