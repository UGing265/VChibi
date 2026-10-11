# Phase 5 — Tạo/sửa pet và import bằng UI

## Context

[Plan](./plan.md) · [Requirements](./requirements.md) · [Architecture](./architecture.md) · [Execution contract](./execution-contract.md).
Priority P1. Status Completed. Dependency phase 4 UI/session, phase 2 store, phase 3 SpritePlayer.

## Mục tiêu và yêu cầu

Người dùng tạo pet từ PNG, cấu hình đứng/đi/FPS/frame order, preview, lưu/hủy, và import gói hoàn chỉnh. Không phải viết/chỉnh JSON, không cần biết code. Không bỏ tính năng này để giao một pet mẫu.

## File liên quan

Root tuyệt đối R = D:/AShiroru/ProgramCode/Project/Personal/VChibi/. Create:

- src/VChibi/Views/PetEditorView.xaml/.xaml.cs.
- src/VChibi/ViewModels/PetEditorViewModel.cs.
- src/VChibi/Services/FilePickerService.cs.
AnimationEditorViewModel chỉ tách nếu draft/validation từng action đủ lớn; không buộc thêm model wrappers.
Modify LibraryWindow/LibraryViewModel/App composition, Controls/SpritePreview, Services/PetStore cho contract hoàn chỉnh. Có thể tách PetImportService/PetValidator khi thực sự cần.
Tests chỉ bổ sung draft/cancel/frame-order/save boundary chưa được coverage; không asserts XAML/source text.

## Kiến trúc

- Editor là nội dung thứ hai trong cùng cửa sổ quản lý, có Quay lại; không biến thành nhiều modal chồng nhau.
- Draft riêng gồm tên và ordered frames/FPS từng action; sửa draft không mutation PetDefinition đã lưu hoặc frame set active.
- File picker native Windows multi-select PNG, folder picker import; không cần thư viện drag-drop.
- Preview phát action đang chọn; hướng preview thể hiện flip X. Preview original draft không ghi file cho tới Save.
- Store commit trước runtime activate. Command busy/cancellation/validation có trạng thái thật, UI vẫn responsive.

## Bước triển khai

1. Dựng editor light/lavender theo layout đã bàn: trường tên, tab Đứng/Đi, Choose PNG, thumbnails/filename/order, Trước/Sau/Bỏ, FPS, preview/play/pause, Hủy/Lưu/Lưu và thả.
2. Open new draft rỗng; edit existing draft copy dữ liệu/asset references, không chỉnh store trực tiếp.
3. Chọn PNG: validate/decode async dưới cùng memory budget, natural initial ordering. Fail một file hiển thị cụ thể và không giả mọi file đã nạp; cho user bỏ/chọn lại.
4. Frame chọn hiện highlight/index/name; move left/right bằng buttons giữ selection, bỏ frame giải phóng refs khi không còn dùng. Chuyển action giữ draft trước đó.
5. Update FPS 1–60 và preview đúng elapsed, validate tên/idle/walk. FPS riêng từng action, không gộp speed movement.
6. Save chỉ enabled khi valid/not busy. Snapshot draft → PetStore prepare/commit → cập nhật thư viện/selection theo ID → feedback đã lưu.
7. Save không thả pet mới; Save and activate commit trước rồi session replace. Nếu lưu thành công nhưng activate thất bại, báo hai trạng thái riêng, không nói cả thao tác thất bại/mất pet.
8. Edit pet đang chạy: commit lưu rồi prepare reload/replace; lỗi reload giữ active frame set cũ, báo “đã lưu, chưa cập nhật pet đang chạy”. Không phá runtime trước save thành công.
9. Hủy/back: hỏi khi dirty, không save ngầm. Stop preview/cancel load, bỏ draft khi user xác nhận; library/runtime giữ nguyên.
10. Import UI chọn folder, state đang kiểm tra/chép; store cấp ID mới. Thành công thêm mục/select để xem, không auto-activate; lỗi rõ, không item nửa vời. Import repeated không overwrite.
11. Editor pause/hidden/save/close: clear image/frame resources khi hợp lý, generation guard tránh stale async update.
12. Chạy app và thao tác tất cả đường tạo/sửa/import bằng UI thật; không dùng sửa pet.json bằng tay thay bằng chứng UI.

## Smoke bắt buộc

- A07: chọn PNG thứ tự có thể sửa, FPS idle/walk khác nhau, tên; save, reopen editor và thấy đúng. Save and activate chạy đúng pet/frame order.
- A08: xóa/move ảnh nguồn sau save; pet reload vẫn chạy nhờ asset đã copy.
- A09: sửa tên/frame/FPS rồi Cancel; reopen thấy bản cũ, active không đổi.
- A10: sửa active pet rồi Save; mới chạy sau commit; lỗi decode/save/activate báo đúng, không phá bản đang chạy.
- A11: import complete folder; item thêm nhưng active giữ nguyên; import lần hai ID riêng.
- A12: missing/schema/PNG giả/traversal/encoded hoặc decoded oversize/cancel báo rõ, không crash/escape/item dở. Include ancillary bytes lớn nhưng pixel nhỏ (SEC-01), PNG RGBA 16-bit và native/conversion budget (A1); không chỉ test 8-bit PNG.
- During load/copy: UI responsive, double save/import không gây duplicate commit unintended; busy commands disabled.

## Checklist

- [x] UI đủ tên/actions/frames/order/FPS/preview, không JSON thủ công.
- [x] Draft isolation và Cancel/back dirty đúng.
- [x] Save vs Save-and-activate và active edit failure semantics đúng.
- [x] Import folder UI hoàn chỉnh, collision/path/budget/cancel xử lý.
- [x] Async/resource lifecycle và error UX thật.
- [x] A07–A12 UI smoke có evidence; chuyển phase 6 ngay.

## Rủi ro / an toàn

- Save success nhưng activate fail không được rollback dữ liệu hợp lệ hoặc giả không lưu.
- Thumbnails và preview có thể double decoded memory: chia sẻ immutable BitmapSource trong draft khi được, budget tính tổng ownership thật; không giữ previous draft sau close.
- Không crop/lật file gốc; flip chỉ transform hiển thị, path/source user không bị ghi sửa.

## Next

Tiếp tục [phase 6](./phase-06-verification-publish.md), không dừng ở “UI đã xong” trước end-to-end publish proof.
