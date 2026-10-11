# Phase 2 — Gói PNG, kho pet và cài đặt

## Context

[Plan](./plan.md) · [Requirements](./requirements.md) · [Architecture/schema](./architecture.md) · [Execution contract](./execution-contract.md).
Priority P1. Status Pending. Dependency phase 1.

## Mục tiêu và yêu cầu

- Kho pet đọc/lưu/tạo/import thật; không buộc user viết JSON.
- Asset mẫu thật có idle/walk, đủ để runtime phase 3 mở pet.
- Lưu theo ID, copy asset, validation/path/memory bounds và recovery commit nhất quán.
- Những command UI sẽ nối ở phase 4/5; storage được exercise bằng smoke harness tạm, không fake runtime.

## File liên quan

Root tuyệt đối R = D:/AShiroru/ProgramCode/Project/Personal/VChibi/. Create:

- src/VChibi/Models/PetDefinition.cs; AnimationDefinition.cs; AppSettings.cs; PetDisplaySettings.cs.
- src/VChibi/Services/PetStore.cs; SettingsStore.cs. Có thể tách PetValidator/AppPaths khi cần, không bắt buộc.
- src/VChibi/Assets/DefaultPet/pet.json, idle/*.png, walk/*.png; src/VChibi/Assets/app.ico.
- tests/VChibi.Tests/VChibi.Tests.csproj và Storage/*Tests.cs chỉ cho invariants đáng giữ.
Modify src/VChibi/VChibi.csproj cho copy/publish assets, src/VChibi/App.xaml.cs cho ownership lock trước storage, VChibi.sln nếu thêm tests. Không xóa file ngoài scope.

## Kiến trúc / contract

- System.Text.Json, schemaVersion=1, ID/name, animations idle/walk với FPS và ordered frame paths.
- PetStore phụ trách list/read/save/import/seed; không điều khiển HWND. Caller cung cấp draft/ảnh nguồn, service trả pet đã commit hoặc lỗi có ngữ cảnh.
- Settings chứa selectedPetId, activePetId nếu khác selection, isPetVisible và display settings theo ID. Selection và active identity không gộp để startup đúng.
- Import ID mới; không dùng ID/tên ngoại lai tạo path ghi. Thư mục source phải chứa pet.json, không import JSON riêng.
- Biên input, canonical paths, decoded budget theo architecture; giới hạn là engineering policy, lỗi thông báo dễ hiểu.
- Review fix F1: store/recovery chỉ hoạt động sau App có ownership lock của data root; trong store serialize write/commit để hai commands không tranh cùng package.

## Bước triển khai

1. Models/data serialization rõ, không đưa BitmapSource/Window/command vào JSON model. Review fix F1: triển khai App data-root ownership lock tại phase này trước storage/recovery; phase 4 dùng lại. Test/temp harness có root độc lập và cùng ownership discipline.
2. Đọc manifest với size cap, kiểm tra schema/version/name/idle/walk/FPS/frame paths. Path boundary không dùng StartsWith(root) thiếu separator.
3. Kiểm tra path/reparse và precheck header/encoded bytes; cap 32 MiB/PNG, 128 MiB encoded/gói, đếm actual stream bytes (SEC-01). Review fix A1: cache Pbgra32 cap 64 MiB; native PNG 64bpp và conversion/copy đồng thời phải tính checked stride/height trước materialization, peak CPU pixel surfaces 128 MiB/frame-set. Không dùng *4 cho native decode.
4. Save/import prepare ở staging cùng volume; bounded-copy đúng referenced file với tên nội bộ do app cấp, validate/decode snapshot staged rồi write manifest sau cùng; không công bố package lỗi. Không decode lặp lại cùng snapshot vô ích; source đổi sau precheck phải vẫn bị bound và kiểm tra.
5. Edit commit: chuẩn bị bản mới, giữ backup bản cũ, có dấu commit/recovery hoặc cách rollback rõ khi move/replace lỗi. Startup giải quyết interruption ở mỗi bước; không giả multi-step rename là atomic transaction.
6. Xóa staging/backup của chính operation khi an toàn; không xóa source user hoặc backup duy nhất còn recoverable. Cancel trước commit giữ bản cũ.
7. Review fix F2/F3: settings schema/map/ID và range finite scale 25–400%, speed 5–200 DIP/s validate sau deserialize, không chỉ UI input. Trường thiếu dùng default rõ; sai hiện diện trả corrupt-state. Write temp/replace giữ file cũ khi lỗi; persistence gate không cho autosave/exit-save sau user Cancel phục hồi.
8. Tạo default pet chibi nguyên bản đơn giản nhưng hoàn chỉnh: nhiều frame idle và walk nhìn có chuyển động, PNG alpha đúng, manifest hợp lệ. Không dùng asset idol có bản quyền chưa rõ phép; không placeholder.
9. Seed default một lần vào local store khi cần; copy/publish asset path dựa AppContext.BaseDirectory, không current working directory.
10. Behavioral tests cho path escape/reparse, PNG lỗi, encoded cap dù pixel nhỏ, nguồn tăng size, PNG RGBA 16-bit/native stride/conversion peak, frame order/FPS, settings range/cancel/recovery và import collision. Dùng temp/ngưỡng inject nhỏ, không file khổng lồ hoặc LOCALAPPDATA thật.
11. Sau batch chạy tests; throwaway storage smoke: seed → create từ PNG → bỏ ảnh source → reload → import cùng gói hai lần → sửa lỗi/hủy → reload bản cũ. Xóa harness tạm sau evidence.

## Checklist

- [x] Manifest/models/settings contracts triển khai đúng.
- [x] Seed/read/save/import và asset copy độc lập source chạy thật.
- [x] Validation giới hạn/path escape và lỗi PNG rõ.
- [x] Cancel/commit interruption không mất bản hợp lệ hoặc công bố dữ liệu dở.
- [x] Pet mẫu/ico có quyền dùng, được copy vào build/publish.
- [x] Behavioral tests và storage smoke có evidence; chuyển phase 3.

## Thành công và kiểm chứng

A08/A11/A12/A17 ở tầng storage. Không test chỉ asserts JSON/property wiring. Khi crash simulation, reopen store cho thấy đúng trạng thái committed/recoverable, không chỉ kiểm tra method không throw.

## Rủi ro / an toàn

- Input local vẫn untrusted: path escape, pixel bombs, IDs colliding.
- Hủy async sau commit không được báo “chưa lưu” dù thực tế đã commit; commit point rõ.
- Tests dùng unique temp directory và dọn chính dữ liệu đã tạo; không đọc settings/pets thật.

## Next

Cung cấp package thật cho [phase 3](./phase-03-desktop-runtime.md); tiếp tục ngay khi gate đạt.
