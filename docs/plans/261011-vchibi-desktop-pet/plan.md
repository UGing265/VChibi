---
title: "VChibi — desktop idol pet Windows"
description: "App WPF MVVM light lavender, tạo/import pet PNG bằng UI và một idol đi ngẫu nhiên trên desktop."
status: in-progress
priority: P2
tags: [feature, desktop, wpf, mvvm]
blockedBy: []
blocks: []
created: 2026-10-11
---

# Kế hoạch triển khai VChibi

## Tổng quan

Hoàn thành app Windows dùng C#/.NET 10, WPF + MVVM, WPF UI, nền sáng và accent Lavender. Một pet sprite PNG đi ngẫu nhiên khắp work area màn hình chính; thư viện, tạo/sửa/import bằng UI, lưu cài đặt, tray và bản publish chạy được.

Đây là kế hoạch, chưa có app/code. Không giao pet demo rồi bỏ luồng tạo/import. Không làm múa/nói/hát, kéo pet, nhiều màn hình/pet đồng thời, AI/plugin/backend.

## Tài liệu bắt buộc

- [Toàn bộ yêu cầu và A01–A17](./requirements.md).
- [Kiến trúc, folder, schema và runtime contracts](./architecture.md).
- [Thực thi phase 1 đến 6 không tự ngắt](./execution-contract.md).
- [Bằng chứng nghiên cứu và hiện trạng](./research.md).
- [Rà soát security, failure, assumptions và scope](./review.md): 12 gap kỹ thuật đã bổ sung, chưa là runtime proof.

## Phases

| Phase | Name | Status |
|-------|------|--------|
| 1 | [Project, MVVM và giao diện light lavender](./phase-01-project-theme.md) | Completed |
| 2 | [Gói PNG, kho pet và cài đặt nhất quán](./phase-02-pet-storage.md) | Completed |
| 3 | [Pet desktop, animation, di chuyển và click-through](./phase-03-desktop-runtime.md) | Completed |
| 4 | [Thư viện, tray và vòng đời app](./phase-04-library-lifecycle.md) | In Progress |
| 5 | [Tạo/sửa và import hoàn toàn bằng UI](./phase-05-editor-import.md) | Completed |
| 6 | [Nghiệm thu toàn luồng và publish Windows](./phase-06-verification-publish.md) | Pending |

## Thứ tự và thực thi liên tục

- Dependency: 1 → 2 → 3 → 4 → 5 → 6. Đây là checkpoint nội bộ, không phải sáu lần xin phép.
- Khi được yêu cầu triển khai: hoàn thành phase, cập nhật evidence/checkbox rồi chuyển phase kế ngay; không hỏi “làm tiếp không”.
- User muốn làm xuyên phase 1–n; không đồng nghĩa bỏ dependency hoặc triển khai mọi phase song song.
- Chỉ dừng khi user yêu cầu, toàn bộ hoàn tất, hoặc blocker thật đã thử giải quyết. Không bảo đảm tránh gián đoạn công cụ ngoài ý muốn; resume theo trạng thái bền vững.
- --auto tránh gate duyệt lại, KHÔNG bỏ smoke/test/review cần thiết hoặc tự commit/push.

## Hiện trạng và phụ thuộc

- Repo chưa có code. SDK 10.0.300/WindowsDesktop 10.0.8 đã quan sát; package metadata baseline WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2.
- ck CLI không có; dùng Markdown canonical. Tất cả output tài liệu dưới docs/ theo AGENTS.md.
- Kế hoạch docs-policy đang pending, nhưng AGENTS.md đã hiện diện; không có blocker/code overlap cần cập nhật hai chiều.
- Không có docs/development-rules.md; tuân thủ context hiện có, không bịa convention từ file không tồn tại.
- Font giữ toolkit/default, không thêm font nhúng; định dạng import đầu tiên là thư mục gói hoàn chỉnh. Xem lựa chọn kỹ thuật trong requirements.

## Hoàn thành toàn kế hoạch

- Đạt A01–A17 bằng smoke app thật và logic tests có giá trị; ghi kết quả thực tế trong verification.md khi triển khai.
- Không placeholder/stub, không thiếu luồng UI hoặc silent fake fallback, không process/timer/tray chạy sót.
- Publish self-contained win-x64 chạy ngoài repo, mang asset mẫu và license notices; docs hướng dẫn đúng hành vi thực.
- status chỉ completed khi mọi phase/tiêu chí đã đạt. Không coi build hoặc screenshot đơn lẻ là nghiệm thu.

## Handoff

```text
/ck:cook --auto "D:/AShiroru/ProgramCode/Project/Personal/VChibi/docs/plans/261011-vchibi-desktop-pet/plan.md"
```
