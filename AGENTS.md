# Hướng dẫn cho agent

## Vị trí lưu tài liệu

- MUST lưu mọi kế hoạch, báo cáo, nghiên cứu, ghi chú và nhật ký do agent tạo trong `<project-root>/docs/`.
- `docs/` là thư mục của dự án hiện tại, không phải `/docs` tại gốc ổ đĩa.
- Kế hoạch: `docs/plans/<ten-ke-hoach>/plan.md`. File giai đoạn và tài liệu liên quan nằm cùng thư mục kế hoạch.
- Báo cáo: `docs/reports/<ten-bao-cao>.md`.
- Nhật ký: `docs/journals/<ten-nhat-ky>.md`.
- Tài liệu khác: `docs/` hoặc thư mục con phù hợp.
- NEVER tạo `plans/`, `reports/` hoặc báo cáo tại gốc repo hay ngoài dự án.
- Nếu skill hoặc workflow mặc định dùng vị trí khác, MUST đổi đường dẫn output sang bên trong `docs/` trước khi ghi file.
- Khi giao việc cho subagent, MUST truyền đường dẫn output dưới `docs/` và yêu cầu tuân thủ cùng quy định.
- Chỉ tạo thư mục con khi cần. Không tự di chuyển hoặc xóa tài liệu hiện có.
- Quy định này áp dụng cho tài liệu; không thay đổi vị trí code, test, asset hay file cấu hình như `AGENTS.md`.

## Phạm vi công việc

- Yêu cầu đơn giản, rõ ràng: thực hiện trực tiếp. Không tự tạo kế hoạch hoặc báo cáo nếu người dùng không yêu cầu và công việc không cần.
