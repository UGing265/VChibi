# Rà soát kế hoạch VChibi — 2026-10-11

## Phạm vi và bằng chứng

Bốn reviewer đọc 11 tài liệu kế hoạch theo các góc security, failure modes, assumptions và scope/complexity. Không có code để review/build/test; kết quả là gap của thiết kế, không phải lỗ hổng runtime đã chứng minh. Main agent đối chiếu và bổ sung contract kỹ thuật tối thiểu, không đổi phạm vi sản phẩm hoặc bắt duyệt lại từng phase.

Tổng 12 findings: 0 Critical, 3 High, 9 Medium. Accept 12, Reject 0. Các sửa dưới đây đã ghi vào tài liệu; vẫn cần verification thực khi triển khai.

## Findings và xử lý

| ID | Mức | Gap / tình huống thất bại | Quyết định / sửa tối thiểu | Tài liệu |
|---|---|---|---|---|
| S1 | Medium | Phase 3 yêu cầu smoke bằng controls chỉ nối phase 4, tạo dependency vòng | Accept: phase 3 smoke runtime/input/bounds; controls-driven scale/hide/replace chứng minh phase 4 | Phase 3, 4 |
| S2 | Medium | Policy không chạy script chưa ghi rõ là lựa chọn triển khai | Accept: thêm attribution conservative choice, giữ data-only gói PNG | Requirements §3 |
| SEC-01 | Medium | PNG pixel nhỏ nhưng encoded ancillary bytes lớn có thể làm đầy staging volume | Accept: 32 MiB/file, 128 MiB encoded/gói, bounded stream bytes cả khi source đổi; validate staged snapshot | Architecture, phase 2, 5 |
| F1 | High | Mở exe lần hai tranh store/recovery và hiện pet thứ hai | Accept: exclusive OS-held data-root lock trước recovery, giữ tới cuối shutdown; instance thứ hai thông báo và thoát, không IPC | Requirements, architecture, phase 2, 4, 6 |
| F2 | Medium | Settings JSON parse được nhưng scale/speed/map sai gây lỗi runtime | Accept: semantic validation finite/range/schema/map; invalid vào corrupt UX, không silent clamp/overwrite | Architecture, phase 2, 4 |
| F3 | High | User Cancel settings recovery nhưng Exit vẫn ghi defaults đè file hỏng | Accept: Cancel thoát không ghi, persistence gate chặn mọi autosave/exit-save; Reset xác nhận mới bật lưu | Requirements, architecture, phase 2, 4, 6 |
| F4 | Medium | Hidden active A, selected B nhưng Hiện chỉ Show A | Accept: UI/tray Hiện activate selected B; load lỗi giữ hidden, startup restore-active semantics riêng | Requirements, architecture, phase 4, 6 |
| A1 | High | PNG native 64bpp làm budget width*height*4 sai decode peak | Accept: cache Pbgra32 64 MiB, native/stride/conversion accounted trước materialization, CPU pixel-surface peak 128 MiB/frame-set; không hard-cap process/GPU | Architecture, phase 2, 3, 5 |
| A2 | Medium | Cùng pixel size nhưng PNG metadata DPI khác làm sprite nhảy kích thước | Accept: PixelWidth/PixelHeight canvas, 100%=1 source pixel/DIP, explicit layout bỏ metadata DPI; preview/runtime cùng quy tắc | Architecture, phase 3 |
| A3 | Medium | Clamp position nhưng destination ngoài bounds mới làm walk mắc kẹt biên | Accept: revalidate/clamp/reselect destination, arrival nếu trùng; shrink-bounds test | Architecture, phase 3 |
| A4 | Medium | Blanket Windows 10 1809+ support vượt bằng chứng OS/edition/runtime | Accept: không quảng cáo blanket support; Windows 11 hiện tại là môi trường nghiệm thu dự kiến, release claim cần support-table + smoke | Phase 1 |
| A5 | Medium | Pet random/không kéo nên bốn-corner smoke khó tái lập; probe có thể DPI-virtualized | Accept: temporary deterministic internal driver qua runtime thật, PMv2 probe context, ghi rectangles/DPI/foreground PID, dọn driver | Phase 3 |

## Đối chiếu provenance

- Yêu cầu hiện tại chỉ lập kế hoạch; không tự triển khai.
- Yêu cầu chạy liên tục tương lai có trong lời người dùng: “ghi vô đầy đủ mong muốn và có thể nhảy từ phase 1 -> n cùng lúc luôn tránh AI nó tự ngắt…”.
- execution-contract áp dụng khi cook được gọi, giữ dependency, user stop và blocker thật; không coi yêu cầu trên là quyền triển khai app ngay trong phiên này.
- Metadata/documentation đã tách khỏi runtime proof. WPF UI font family chưa được khẳng định khi chưa kiểm chứng pinned package.
- Folder import, no scripts, click-through whole-window, tốc độ mặc định và giới hạn input là lựa chọn triển khai được ghi rõ, không giả thành yêu cầu số liệu do user chỉ định.

## Nguồn đối chiếu bổ sung

- [WIC PNG native pixel formats](https://learn.microsoft.com/en-us/windows/win32/wic/-wic-codec-native-pixel-formats#png-native-codec).
- [BitmapSource pixel vs DIP dimensions](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.bitmapsource?view=windowsdesktop-10.0).
- [.NET 10 supported OS/editions](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).
- [GetWindowRect DPI virtualization](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowrect).

## Kiểm tra tài liệu trong phiên lập kế hoạch

Script kiểm tra phase count=6, status pending, relative links có đích, A01–A17 hiện diện và mỗi phase có checklist. Contrast #7862B8/chữ trắng tính khoảng 4.96:1; chưa nghiệm thu visual UI/state. Ghi kết quả cuối sau sync docs nếu khác, không gọi đó là runtime app smoke.

## Next

Plan vẫn pending. Handoff cook đọc toàn bộ requirements/architecture/execution-contract và phase files. Không cần một gate hỏi lại “làm tiếp phase sau?”; nếu user đổi yêu cầu thì cập nhật plan trước khi tiếp tục.
