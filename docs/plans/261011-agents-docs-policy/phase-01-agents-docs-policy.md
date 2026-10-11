# Giai đoạn 1: Thêm hướng dẫn lưu tài liệu

## Bối cảnh

- [Kế hoạch](./plan.md)
- Trạng thái: Pending.
- Yêu cầu: agent luôn lưu kế hoạch và báo cáo dưới `docs/` của dự án.

## Thiết kế và file ảnh hưởng

Tạo duy nhất `D:/AShiroru/ProgramCode/Project/Personal/VChibi/AGENTS.md`. File ở gốc repo để áp dụng toàn dự án; không đặt chính file hướng dẫn này trong `docs/`.

Nội dung đề xuất:

```markdown
# Hướng dẫn cho agent

## Vị trí lưu tài liệu

- MUST lưu mọi kế hoạch, báo cáo, nghiên cứu, ghi chú và nhật ký do agent tạo trong `<project-root>/docs/`.
- `docs/` là thư mục thuộc dự án hiện tại, không phải `/docs` tại gốc ổ đĩa.
- Kế hoạch: `docs/plans/<ten-ke-hoach>/plan.md`; file giai đoạn và nghiên cứu liên quan nằm cùng thư mục kế hoạch.
- Báo cáo: `docs/reports/<ten-bao-cao>.md`.
- Nhật ký: `docs/journals/<ten-nhat-ky>.md`.
- Tài liệu khác: `docs/` hoặc thư mục con phù hợp.
- NEVER tạo `plans/`, `reports/` hay tài liệu báo cáo tại gốc repo hoặc ngoài dự án.
- Khi giao việc cho subagent, MUST truyền đường dẫn output dưới `docs/` và yêu cầu tuân thủ cùng quy định.
- Nếu workflow mặc định dùng `plans/` hoặc vị trí khác, đổi đường dẫn sang `docs/` trước khi ghi file.
- Chỉ tạo thư mục con khi cần. Không tự di chuyển hoặc xóa tài liệu hiện có.
- Quy định này áp dụng cho tài liệu; không thay đổi vị trí code, test, asset hay file cấu hình như `AGENTS.md`.
```

## Các bước

1. Kiểm tra trực tiếp `AGENTS.md` trước khi ghi. Nếu đã xuất hiện, bổ sung mục trên và giữ nguyên hướng dẫn khác; không ghi đè.
2. Tạo hoặc cập nhật file tại gốc repo theo nội dung đề xuất.
3. Đọc lại file, xác nhận cả kế hoạch, báo cáo và output subagent đều chịu quy định.
4. Kiểm tra ví dụ đường dẫn đều thuộc `docs/`, không hiểu nhầm thành đường dẫn tuyệt đối `/docs`.

## Checklist

- [ ] Thêm hướng dẫn tại gốc repo.
- [ ] Bao phủ kế hoạch, báo cáo, nghiên cứu, nhật ký và subagent.
- [ ] Xác nhận các đường dẫn và không ghi đè hướng dẫn hiện có.

## Rủi ro và kiểm chứng

- Skill có thể mặc định ghi vào `plans/`: hướng dẫn yêu cầu đổi output trước khi ghi.
- `AGENTS.md` chỉ hướng dẫn agent; không đảm bảo mọi công cụ hoặc agent đều đọc và tuân thủ.
- Không có thay đổi runtime, bảo mật hay hiệu năng. Kiểm chứng bằng nội dung file; không thêm test kiểm tra văn bản.

## Bước tiếp theo

Thực hiện giai đoạn này bằng lệnh cook trỏ tới kế hoạch.
