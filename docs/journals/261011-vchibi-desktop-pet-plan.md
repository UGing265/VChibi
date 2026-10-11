---
title: "Chốt yêu cầu và kế hoạch VChibi"
date: 2026-10-11
status: planning-only
plan: ../plans/261011-vchibi-desktop-pet/plan.md
---

# Nhật ký — kế hoạch desktop idol pet

## Context

Người dùng đã brainstorm sản phẩm, chọn MVVM/folder theo loại, WPF UI nền sáng, accent Lavender, rồi yêu cầu ghi đầy đủ mong muốn và chỉ dẫn AI đi xuyên phase 1–n, không tự dừng ở từng phase. Phiên này chỉ lập kế hoạch; chưa được yêu cầu viết app.

## Đã thực hiện

- Quét docs/plans/ theo AGENTS.md, không tạo root plans/. Kế hoạch docs-policy pending nhưng AGENTS.md đã tồn tại; không có source overlap/blocker.
- Quan sát dotnet --info: SDK 10.0.300, runtime WindowsDesktop 10.0.8, Windows x64; repo chưa có code.
- Nghiên cứu độc lập toolkit/MVVM/font/tray/publish và ordinary Window/input/DPI/timer; ghi nguồn và phân biệt metadata với runtime proof.
- Ghi plan pending, requirements A01–A17, architecture/schema, execution contract và sáu phase chi tiết tại docs/plans/261011-vchibi-desktop-pet/.
- Bốn reviewer chỉ đọc tài liệu; đối chiếu 12 gap (3 High, 9 Medium), bổ sung ownership/store recovery, settings Cancel/range, native PNG budget, geometry/DPI, destination invariant và vị trí smoke gate. Chi tiết trong review.md, không biến review thành runtime proof.
- Hydrate sáu task phase nhưng giữ blocked chờ yêu cầu triển khai; phiên này chỉ hoàn thành tài liệu kế hoạch.
- Kiểm tra tài liệu bằng script: sáu phase/checklist, status pending, relative links có đích, A01–A17 có mặt. Lavender #7862B8 với chữ trắng tính được contrast khoảng 4.96:1; chưa phải visual/UI runtime proof.
- Gọi set-active-plan helper; CK_SESSION_ID không được đặt, helper chỉ báo would-set, active plan không persist. Handoff dùng đường dẫn tuyệt đối, không phụ thuộc session hook.

## Quyết định

- PNG sprite 2D, một pet hiện, một primary screen, random movement toàn work area trừ taskbar.
- Không 70/30, không kéo pet, không múa/nói/hát trong scope này.
- Tạo/sửa bằng UI, import thư mục gói hoàn chỉnh do app đọc/ghi cấu hình. Không bắt user viết JSON.
- Một project WPF + MVVM, không backend/DI framework lớn/plugin; test project chỉ khi có behavioral boundaries cần giữ.
- WPF UI light/Lavender, font toolkit/default; không chép Microsoft font hoặc coi icon font là body font.
- Thực thi tương lai liên tục theo dependency 1→6; checkpoint không phải điểm xin phép. Vẫn tôn trọng user stop/blocker thật và không bịa pass.

## Reflection / giới hạn

Kế hoạch đầy đủ không bảo đảm model/tool sẽ không bị gián đoạn; trạng thái bền vững giúp resume. Click-through/DPI/asset lifecycle phải kiểm chứng bằng app thật khi triển khai, không lấy screenshot mockup hoặc docs làm bằng chứng.

## Next

Duyệt/điều chỉnh nội dung nếu cần, sau đó gọi cook với plan tuyệt đối. Không khởi chạy triển khai tự động trong phiên lập kế hoạch.
