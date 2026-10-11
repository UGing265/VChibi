# Phase 6 — Nghiệm thu toàn luồng và publish Windows

## Context

[Plan](./plan.md) · [A01–A17](./requirements.md) · [Architecture](./architecture.md) · [Execution contract](./execution-contract.md).
Priority P1. Status Pending. Dependency tất cả phase 1–5 đạt gate; các lỗi phát hiện phải sửa tại source liên quan, không thêm fake fallback.

## Mục tiêu và yêu cầu

Deliverable là app thực dùng được và bản publish self-contained, không chỉ source compile/demo/screenshot. Ghi bằng chứng thực tế, tài liệu sử dụng, sync trạng thái toàn plan.

## File liên quan

Root tuyệt đối R = D:/AShiroru/ProgramCode/Project/Personal/VChibi/.
Create src/VChibi/Properties/PublishProfiles/Windows.pubxml; docs/user-guide.md; docs/changelog.md; docs/plans/261011-vchibi-desktop-pet/verification.md.
Modify src/VChibi/VChibi.csproj cho assets/notices/publish settings và source/tests đã có khi sửa lỗi có bằng chứng. Cập nhật plan/phase checkboxes sau verification, không tạo report ngoài docs.
Artifact publish ở artifacts/publish/win-x64/ (không phải tài liệu). Không tự commit/push/deploy.

## Bước triển khai

1. Đối chiếu requirements và toàn bộ phase: không command no-op, không hardcode pet ngoài sample, không missing import/editor/tray hoặc silent placeholder.
2. Sau tất cả edits tích hợp, restore/build/test một lần cho batch; sửa lỗi thật rồi chạy lại đúng check affected. Không dùng tests thay smoke desktop.
3. Thực hiện A01–A17 trên app thật với data sandbox tách khỏi pet người dùng hiện có. Fresh-profile first run, normal run, saved-state reopen, invalid-input run đều exercise.
4. Kiểm chứng cross-process input/focus, HWND bounds/DPI, frame order/time, save cancel/failure, repeated hide/show/replace và tray/exit. Thêm mở exe hai lần khi app ở tray/commit pending, Hiện đúng selected ID khi khác active, corrupt-settings Cancel→Exit giữ file gốc.
5. Quan sát CPU/memory bằng công cụ sẵn khi active vs hidden; kiểm tra không decode/read disk mỗi frame và không tăng tài nguyên qua replacement. Không mở benchmarking project, không bịa threshold đã đo.
6. Publish Release win-x64 --self-contained true, giữ trimming/AOT/single-file false. Dùng native app icon, copy default pet manifest/PNG và MIT/third-party notices vào output.
7. Chạy VChibi.exe từ publish folder ngoài source (copy tới temp dedicated folder), không dùng cwd repo; kiểm chứng first-run/sample/import/editor/persist/tray/exit đại diện. Thư mục publish đầy đủ là deliverable, không chỉ một exe.
8. Kiểm tra output thực mang runtime/dependencies; nếu không có máy sạch để chứng minh no-runtime install, ghi limit thay vì tuyên bố đã test clean machine.
9. Hướng dẫn user bằng tiếng Việt: chạy app, tạo pet, PNG đứng/đi, FPS/order, import thư mục, scale/speed, close vs exit, lưu dữ liệu, giới hạn và error recovery. Không bắt JSON thủ công.
10. Changelog ghi chức năng thực đã có, không quảng cáo future voice/dance/plugin. Thu gom license/sample quyền phân phối rõ.
11. Xóa harness/scaffold/throwaway script đã dùng do mình tạo, không xóa dữ liệu user. Giữ regression tests có giá trị; không pin wording/default/source text.
12. Sweep mọi checkbox, ghi verification evidence từng A-ID; nếu chưa exercise case phải để chưa kiểm chứng, không mark whole plan completed. Nếu tất cả đạt, cập nhật status completed và bàn giao source/publish paths + command + observed limits.

## Command dự kiến

```text
dotnet restore VChibi.sln
dotnet build VChibi.sln -c Release --no-restore
dotnet test VChibi.sln -c Release --no-build
dotnet run --project src/VChibi/VChibi.csproj -c Release --no-build
dotnet publish src/VChibi/VChibi.csproj -c Release -f net10.0-windows -r win-x64 --self-contained true -o artifacts/publish/win-x64
```

Tests command chỉ dùng khi project test thật đã được tạo theo behavioral scenarios; không bịa có suite sẵn. Với pinned lock, có thể dùng --locked-mode sau initial restore đã resolve; publish RID restore có thể cập nhật lock cho RID, không giấu restore failure.

## Checklist

- [ ] A01–A17 có evidence thực, lỗi consumer-visible đã sửa (Các luồng người dùng cốt lõi đã có bằng chứng thực nghiệm; còn mở 5 ca kiểm thử live Phase 4 và 5 ca ngoại vi/hạ tầng Phase 6).
- [x] Restore/build và behavioral tests thực đạt.
- [ ] App desktop smoke input/focus/bounds/lifetime/editor/import/persistence đạt (Đã đạt các luồng chính: click/scroll pass-through, editor, import, ẩn/hiện, tray, khôi phục visible & hidden; còn mở ca kẹp biên 400%, live FPS, và các ca biên tại Phase 4).
- [x] Publish self-contained và exe chạy ngoài source với asset/notices đầy đủ.
- [x] User guide/changelog đúng hành vi thật, không JSON thủ công.
- [x] Throwaway artifacts do agent tạo được dọn, source/test không dead scaffolding.
- [ ] Plan/phase sync-back đúng; final chỉ báo verification đã exercise và giới hạn (Giữ Pending theo dependency Phase 4 chưa hoàn tất).

## Tiêu chí done

Không còn acceptance chưa đạt hoặc blocker che giấu. Tất cả luồng được yêu cầu hoạt động end-to-end; process thoát sạch. Metadata/runtime/docs của plan không phải bằng chứng app đã chạy.

## Rủi ro / an toàn

- Test/first-run sandbox không delete hoặc overwrite LOCALAPPDATA của user thật; nếu phải đổi profile/path dùng injection/service config nội bộ cho smoke, không thêm public CLI tính năng mới.
- Publish file copy thiếu content có thể compile vẫn pass; chạy ngoài cwd repo mới bắt được.
- UI runtime không truy cập được: hoàn tất phần reachable, ghi blocker chính xác và giữ incomplete; không gọi screenshot mockup là app proof.

## Next

Chỉ kết thúc khi toàn kế hoạch đạt hoặc blocker thật theo execution-contract; không tự thêm phase cho chức năng ngoài scope.
