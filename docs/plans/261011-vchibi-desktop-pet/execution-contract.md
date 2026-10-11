# Hợp đồng thực thi — phase 1 đến phase 6 liên tục

## Mục đích

Người dùng yêu cầu AI triển khai xuyên suốt kế hoạch, không tự ngắt sau một phase và hỏi có làm tiếp hay không. Tài liệu này là chỉ dẫn thực thi; không phải tuyên bố app đã hoàn thành.

## Quy tắc bắt buộc

1. Khi người dùng gọi triển khai kế hoạch, đọc plan.md, requirements.md, architecture.md và toàn bộ phase trước khi sửa code.
2. Nếu kế hoạch mới: triển khai phase 1 → 2 → 3 → 4 → 5 → 6 trong cùng luồng công việc. Nếu resume: đọc checkbox/evidence, tiếp tục phase chưa hoàn tất sớm nhất; không làm lại phần đã xác minh.
3. Hoàn thành phase là checkpoint nội bộ, không phải điểm kết thúc lượt. Cập nhật checkbox/trạng thái và tự chuyển phase tiếp theo ngay.
4. Không hỏi “làm tiếp không”, không xin duyệt lại stack, layout, folder hoặc phạm vi đã chốt. Không kết thúc bằng “phase tiếp theo sẽ làm…”.
5. Không đánh đồng “cùng lúc” với chạy mọi phase song song bất chấp phụ thuộc. Các phase có dependency thật phải đi theo thứ tự. Có thể giao các lát công việc độc lập sau khi chốt contract/file owner; main agent chịu trách nhiệm tích hợp.
6. Không tự cắt tạo/sửa/import, nhớ cài đặt, tray, click-through hoặc publish để giao một pet mẫu đơn lẻ. Toàn bộ ma trận A01–A17 phải được xử lý.
7. Không dùng stub, dữ liệu giả, hardcode tên/ảnh ngoài pet mẫu, placeholder, TODO implement, hoặc bắt người dùng viết JSON để thay luồng UI.
8. Lỗi thực tế: tìm nguyên nhân, sửa, kiểm chứng rồi tiếp tục. Không báo xong chỉ vì build thành công.
9. Quyết định kỹ thuật nhỏ: chọn phương án bảo thủ đã ghi trong architecture/phase; ghi quyết định, không hỏi người dùng thông tin lấy được từ repo/tool/docs.
10. Không tự thêm chức năng ngoài requirements.md hoặc đổi WPF/MVVM/WPF UI. Thay đổi bắt buộc làm lệch yêu cầu sản phẩm mới cần hỏi.
11. Trong một đợt thay đổi đang thực hiện, không cho subagent chạy build/lint/formatter/test cạnh nhau. Integration owner chạy kiểm chứng sau khi tập hợp thay đổi, và chạy app thật tại checkpoint cần thiết.
12. Mọi kế hoạch, nghiên cứu, ghi chú, báo cáo, nhật ký và bằng chứng dạng tài liệu nằm dưới docs/. Không tạo plans/ hoặc reports/ ở root repo. Source/test/asset theo cấu trúc riêng.

## Được dừng khi nào

- Người dùng yêu cầu dừng hoặc đổi hướng.
- Blocker thật: thiếu quyền/thông tin/asset/runtime không thể lấy bằng công cụ hiện có, sau khi đã thử các đường hợp lý.
- Toàn bộ kế hoạch hoàn tất và có bằng chứng nghiệm thu.

Không dừng vì kết thúc phase, nhiều file, thời lượng dự đoán, hoặc cần “duyệt tiếp”. Không bịa bằng chứng nếu môi trường không cho kiểm chứng UI.

Khi blocker thật: hoàn tất các phần không bị chặn, ghi mục bị chặn, nguyên nhân, đã thử gì, prerequisite chính xác, giữ phase pending/in-progress phù hợp. Không đánh completed toàn plan. Hợp đồng này không thể bảo đảm một công cụ/model sẽ không bị ngắt ngoài ý muốn; checkbox bền vững cho phép resume đúng vị trí.

## Trạng thái bền vững

- plan.md bắt đầu status: pending; khi triển khai đổi in-progress.
- Checkbox mỗi phase chỉ đánh x khi hành vi/đầu ra đã có và kiểm chứng phù hợp.
- Ghi evidence tại docs/plans/261011-vchibi-desktop-pet/verification.md trong lúc triển khai; không tạo bản “đã pass” trước.
- Evidence gồm command/scenario, kết quả thực tế, ngày chạy, giới hạn; không chỉ chép tên test.
- Nếu kiểm tra thất bại, giữ trạng thái mở, sửa trước khi đi tiếp phần phụ thuộc. Phần độc lập vẫn có thể tiếp tục.
- Cuối cùng sweep tất cả phase, đối chiếu A01–A17, cập nhật plan completed chỉ khi không còn tiêu chí chưa đạt.

## Tiêu chuẩn bằng chứng

- Build/package restore: chạy command thật; không giả log.
- UI WPF: mở app thật, thao tác và quan sát. Screenshot là bổ trợ, không thay kiểm chứng click-through/focus/process shutdown.
- Desktop: thao tác xuyên pet tới Notepad hoặc app khác process, không chỉ sample cùng thread.
- DPI/work area: đối chiếu rectangle thực tế, không chỉ suy luận từ code.
- Permanent tests dành cho logic có lỗi consumer-visible: bounds/time/frame selection, validation/traversal, persistence failure/cancel. Không test XAML/source text/wiring/mock echoes.
- Nếu smoke không thực hiện được, ghi rõ chưa kiểm chứng; không tự tuyên bố đạt.

## Lệnh bàn giao

```text
/ck:cook --auto "D:/AShiroru/ProgramCode/Project/Personal/VChibi/docs/plans/261011-vchibi-desktop-pet/plan.md"
```

--auto dùng để tránh checkpoint hỏi duyệt lại; vẫn phải tuân thủ mọi smoke gate, xử lý lỗi và blocker thật. Không tự stage/commit/push/deploy nếu người dùng chưa yêu cầu.

## Task hydration khi chuyển từ lập kế hoạch sang triển khai

Sáu task phase đã hydrate nhưng blocked vì phiên này chỉ lập kế hoạch. Khi user gọi cook, gỡ blocker “chờ yêu cầu triển khai” cho phase có dependency đã đạt, rồi start; không coi trạng thái blocked của phiên planning là blocker kỹ thuật vĩnh viễn. Sau mỗi phase gỡ blocker của phase kế ngay. Session mới đọc checkbox phase files và tạo lại task; không phụ thuộc task ID cũ.
