---
created: 2026-10-11
tags: [docs, planning]
---

# Quy định vị trí tài liệu agent

## Bối cảnh

Người dùng yêu cầu `AGENTS.md` nhắc agent luôn lưu kế hoạch và báo cáo trong `docs/`. Phiên này dùng `ck:plan`, chỉ tạo kế hoạch.

## Kết quả

- Tạo [kế hoạch](../plans/261011-agents-docs-policy/plan.md) và một file giai đoạn dưới `docs/plans/`.
- Chưa tạo `AGENTS.md`; nội dung đề xuất có trong file giai đoạn.

## Quyết định

- `AGENTS.md` đặt ở gốc repo; tài liệu sinh ra nằm trong `docs/`.
- Hiểu `/docs` là thư mục của dự án.
- Không thêm automation, không di chuyển file hiện có.

## Giới hạn

Hướng dẫn agent không phải cơ chế cưỡng chế ghi file. Cần agent đọc và tuân thủ.

## Tiếp theo

Chạy cook theo kế hoạch để thêm `AGENTS.md`.
