---
title: "Quy định lưu kế hoạch và báo cáo trong docs"
description: "Thêm AGENTS.md tại gốc dự án để mọi tài liệu do agent tạo đều nằm trong docs/."
status: pending
priority: P2
tags: [docs]
blockedBy: []
blocks: []
created: 2026-10-11
---

# Quy định lưu tài liệu của agent

## Tổng quan

Tạo `AGENTS.md` ở gốc repo, áp dụng cho toàn dự án. Mọi kế hoạch, báo cáo, nghiên cứu, ghi chú và nhật ký do agent tạo phải nằm dưới `<project-root>/docs/`.

`/docs` trong yêu cầu được hiểu là thư mục dự án, không phải thư mục gốc ổ đĩa. `AGENTS.md` là file cấu hình hướng dẫn agent, nên nằm ở gốc repo để agent nhận diện.

## Hiện trạng và phụ thuộc

- Danh sách gốc dự án hiện chỉ có `docs/`; thư mục này trống trước bước lập kế hoạch.
- Không có `plans/`, kế hoạch chưa hoàn thành hay `docs/development-rules.md` để đối chiếu.
- Không có quan hệ chặn với kế hoạch khác.
- Ưu tiên yêu cầu lưu dưới `docs/` thay cho mặc định `plans/` của skill.
- Đây là kế hoạch; chưa tạo `AGENTS.md`.

## Giai đoạn

| Phase | Name | Status |
|-------|------|--------|
| 1 | [Thêm hướng dẫn lưu tài liệu](./phase-01-agents-docs-policy.md) | Pending |

## Phạm vi

- Tạo `D:/AShiroru/ProgramCode/Project/Personal/VChibi/AGENTS.md`.
- Không sửa code, không di chuyển tài liệu hiện có, không thêm script hay cơ chế cưỡng chế.
- Một giai đoạn; không cần hydrate task triển khai.

## Tiêu chí hoàn thành

- `AGENTS.md` tại gốc repo có quy định bắt buộc lưu tài liệu trong `docs/`.
- Quy định bao gồm kế hoạch và báo cáo, kể cả output của subagent.
- Có ví dụ `docs/plans/`, `docs/reports/`, `docs/journals/`.
- Nêu rõ không tạo `plans/` hoặc `reports/` tại gốc repo.
- Đọc lại file để kiểm tra đường dẫn và nội dung. Không có runtime cần chạy; hướng dẫn không phải cơ chế cưỡng chế filesystem.
