# Nhật ký kiểm chứng thực tế — VChibi

Tài liệu ghi nhận bằng chứng kiểm chứng, lệnh đã chạy và kết quả quan sát thực tế trong quá trình triển khai theo kế hoạch `261011-vchibi-desktop-pet`.

---

## Phase 1 — Project, MVVM và UI light lavender (2026-10-11)

### 1. Project, Package Restore & Build
- **Lệnh**: `dotnet build VChibi.sln`
- **Kết quả**:
  - Target: `net10.0-windows`
  - Packages: `WPF-UI 4.3.0`, `CommunityToolkit.Mvvm 8.4.2` (đã tạo `packages.lock.json`)
  - Build output: `0 Warning(s), 0 Error(s)`.
  - Manifest: PMv2 DPI awareness (`app.manifest`).
  - Lọc ambiguous types giữa WPF và WinForms: gỡ `System.Windows.Forms` và `System.Drawing` khỏi global usings trong `VChibi.csproj`.

### 2. UI & Theme Light Lavender
- **Theme**: Light theme nạp qua `ThemesDictionary` và `ControlsDictionary`.
- **Accent**: Lavender `#7862B8` áp dụng qua `ApplicationAccentColorManager.Apply` (app-local, không đụng system theme).
- **Styles**: `Colors.xaml` và `Styles.xaml` cung cấp button lavender (chữ trắng contrast > 4.5:1), selection light lavender `#EDE9F6`.
- **Controls & Wiring**: Nút chưa có hành vi ở Phase 1 (Import, Tạo pet, Thả desktop, Sửa pet) đặt `IsEnabled="False"` để tránh no-op. Nút `Thoát` nối `ExitCommand` qua MVVM.

### 3. Smoke Test UI Automation, Tab Traversal & ExitCommand
- **Lệnh**: Chạy script UI Automation với UIAutomationClient & SendKeys:
  - Cửa sổ: `Window Name: VChibi`, `ClassName: Window`.
  - Screenshot lưu tại vị trí hợp lệ: `docs/evidence/phase1-window.png` (Kích thước: 1150x725).
  - Phân tích màu thực tế: 130 điểm mẫu mang sắc thái Lavender (#7862B8) tại header, primary elements.
  - Kiểm tra điều hướng phím Tab:
    `RadioButton (Id='') -> RadioButton (Id='') -> Button (Id='ExitButton') -> RadioButton (Id='')`.
    Focus chuyển đổi mượt mà qua các control có thể tương tác.
  - Gán focus vào ExitButton: `HasKeyboardFocus: True`.
  - Kích hoạt `InvokePattern` trên ExitButton:
    `Process clean exit: True, ExitCode: 0`. Ứng dụng thoát sạch không để lại process chạy ngầm.

---

## Phase 2 — Gói PNG, kho pet và cài đặt (2026-10-11)

### 1. Data Models, Path Management & AppLock
- Triển khai `PetDefinition`, `AnimationDefinition`, `AppSettings`, `PetDisplaySettings` với `System.Text.Json` (schemaVersion 1).
- `AppPaths`: quản lý `%LOCALAPPDATA%/VChibi/Pets`, `settings.json`, `.staging`, `.instance.lock`. Có tham số `customDefaultPetSourceDirectory` để cách ly hoàn toàn fixture kiểm thử khỏi source gốc.
- `AppLock` (Review fix F1): độc quyền OS-level lock qua `FileStream` với `FileShare.None`. Bắt buộc truyền `AppLock` đã acquire vào `PetStore`; ngăn chặn instance thứ 2 mở ghi dữ liệu. Kiểm thử `Constructor_WithoutAcquiredLock_ThrowsInvalidOperationException` và `SecondInstance_FailsToAcquireLock_WhenFirstInstanceActive` đạt 100%.

### 2. Security & Validation (SEC-01 & A1)
- `PetValidator`:
  - Từ chối path traversal (`..`), absolute path, UNC path, và `ReparsePoint` (symlink/junction) trong package root (`IsSafeRelativePath_RejectsRealReparsePointDirectory`).
  - Giới hạn & Ngân sách bộ nhớ: Hỗ trợ `ValidationBudgets` để kiểm thử ngưỡng nhỏ không cần sinh file khổng lồ:
    - Kiểm thử vượt dung lượng 1 file PNG (`ValidatePackageAssets_RejectsFileOverPngSizeBudget_WithInjectedBudget`).
    - Kiểm thử vượt tổng dung lượng gói PNG (`ValidatePackageAssets_RejectsTotalOverCap_WithInjectedBudget`).
    - Kiểm thử vượt ngân sách cache Pbgra32 (`ValidatePackageAssets_RejectsWhenCacheBudgetExceeded`).
  - Kiểm tra 8-byte PNG magic header (`89 50 4E 47 0D 0A 1A 0A`) và giải mã bằng `PngBitmapDecoder`.
  - Chỉ chấp nhận 2 hành động hợp lệ: `idle` và `walk`.
  - `CopyBoundedStream`: stream copy có giới hạn bytes đọc thực tế để ngăn chặn TOCTOU. Kiểm thử `CopyBoundedStream_ThrowsWhenSourceFileExceedsLimit` ném `InvalidDataException` và dọn temp file khi vượt ngân sách.

### 3. PetStore, Transactional Commit & Recovery
- Staging-based commit: copy asset vào staging trên cùng volume, xác thực staged snapshot, ghi manifest, rồi commit bằng `Directory.Move`.
- Crash-interruption recovery: `RecoverPendingTransactions` chỉ chạy khi đã có `AppLock`, tự động khôi phục bản backup nếu quá trình commit bị gián đoạn giữa chừng, bảo toàn dữ liệu gốc. Chỉ xóa backup khi target đã được xác thực toàn diện (`IsPackageCompletelyValid`). Kiểm thử `RecoverPendingTransactions_WhenInterruptedMidCommit_RestoresOldValidPet` và `RecoverPendingTransactions_WhenCommitSucceededBeforeCrash_CleansUpOrphanedBackup` đạt.
- Báo cáo gói hỏng (A17): `GetAllPackages()` trả về chi tiết các gói bị hỏng thay vì âm thầm bỏ qua.
- Tự động seed DefaultPet nguyên bản (chibi idol với 4 frame idle, 4 frame walk có transparency) khi khởi chạy lần đầu.

### 4. SettingsStore
- Schema version 1, validate dải giá trị: scale 25–400%, speed 5–200 DIP/s.
- Lưu file nguyên tử qua `.tmp` và `File.Move(overwrite: true)`.
- Phát hiện hỏng (`SettingsLoadResult.Corrupt`) cho JSON sai cú pháp, giá trị ngoài khoảng, thuộc tính pet null, hoặc map `pets: null` ở root (`Load_TopLevelNullPetsMap_ReturnsCorruptState`).
- `CorruptedFile_RemainsUntouched_WhenSaveSkippedOrCancelled`: không tự ý ghi đè file hỏng nếu người dùng chọn hủy/persistence bị tắt.

### 5. Kết quả kiểm chứng tự động (Unit & Behavioral Tests)
- **Lệnh**: `dotnet test VChibi.sln`
- **Kết quả**: `Passed! - Failed: 0, Passed: 43, Skipped: 0, Total: 43, Duration: 241 ms`
  - Đạt tiêu chí A08 (xóa ảnh gốc sau khi tạo, pet lưu trữ vẫn hợp lệ).
  - Đạt tiêu chí A11 (import 2 lần cùng gói tạo 2 ID riêng biệt, không collision).
  - Đạt tiêu chí A17 (gói pet lỗi/hỏng được báo cáo rõ ràng với lý do).
  - Đạt tiêu chí crash recovery (khôi phục pet cũ sau khi crash mid-commit).
  - Đạt tiêu chí asset bundling (kiểm tra toàn bộ frame của DefaultPet hợp lệ, không bị contamination).

### 6. Storage Smoke Test Thực tế (Toàn bộ chu kỳ lưu trữ)
- **Lệnh**: `dotnet test VChibi.sln --filter "FullyQualifiedName~StorageSmoke" --logger "console;verbosity=detailed"`
- **Kịch bản thực tế đã chạy**:
  1. `seed`: Chạy `SeedDefaultPetIfNeeded`, kho được khởi tạo default chibi idol trong mock default source cách ly.
  2. `create`: Tạo pet từ ảnh ngoài `smoke_user_downloads`, lưu thành công `Created Smoke Idol`.
  3. `bỏ ảnh source`: Xóa toàn bộ file PNG nguồn gốc bên ngoài.
  4. `reload`: Đọc lại pet từ kho lưu trữ theo ID, xác nhận frame vẫn đầy đủ và hợp lệ.
  5. `import 2 lần`: Import cùng một gói 2 lần, tạo 2 ID độc lập không ghi đè.
  6. `sửa lỗi / hủy`: Thử sửa pet với FPS không hợp lệ (0), thao tác bị từ chối và rollback.
  7. `reload bản cũ`: Tải lại pet ban đầu, xác nhận dữ liệu gốc hoàn toàn nguyên vẹn không bị hỏng.
- **Kết quả**: `Passed VChibi.Tests.Storage.PetStoreTests.StorageSmoke_FullLifecycle_SeedCreateDeleteSourceReloadDuplicateImportEditRollback [157 ms]`

---

## Phase 3 — Pet desktop, animation, di chuyển và click-through (2026-10-11)

### 1. Triển khai PetWindow & Layered Window Interop
- `PetWindow`: WPF Window trong suốt (`WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`, `ShowInTaskbar="False"`, `ShowActivated="False"`, `Topmost="True"`).
- `PetWindowInterop`: áp dụng các cờ native Win32 `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` ngay tại sự kiện `SourceInitialized`.
- Hỗ trợ lật mặt ngang (`FlipTransform.ScaleX = -1` và `FlipTransform.CenterX = frame.PixelWidth / 2.0`) khi quay sang trái (`FacingLeft == true`).

### 2. Runtime Session, Movement & Sprite Player
- `ScreenWorkspace`: trích xuất `WorkArea` và DPI bằng Win32 `MonitorFromWindow`, `GetMonitorInfo`, và `GetDpiForWindow`. Chuyển đổi hai chiều giữa DIP và pixel thực tế.
- `PetMovement`:
  - Giới hạn không gian di chuyển nghiêm ngặt trong `[MinX, MaxX]` và `[MinY, MaxY]`.
  - Hỗ trợ màn hình có origin khác 0 (đa màn hình, taskbar đặt phía trên hoặc bên trái).
  - `StepTowardsTarget`: giải thuật vector di chuyển mượt mà, chống chia cho 0 (`distance < 0.5` hoặc bằng 0), cập nhật hướng `FacingLeft`.
- `PetController`: State machine chuyển đổi giữa `Idle` (1.0 đến 4.0 giây) và `Walk` đến đích ngẫu nhiên trong vùng làm việc. Kẹp `deltaSeconds <= 0.2s` để chống giật/nhảy bước sau khi máy sleep.
- `SpritePlayer`: Giải mã PNG sang `Pbgra32`, tính frame index theo thời gian trôi qua và FPS, fallback mượt mà về `idle` khi gọi action không xác định.
- `DesktopPetSession`: Timer render ~30 Hz, đo thời gian bằng `Stopwatch`, cập nhật tọa độ qua `SetWindowPos` với cờ `SWP_NOACTIVATE | SWP_NOZORDER`.

### 3. Unit & Behavioral Tests
- **Kết quả**: `Passed! - Failed: 0, Passed: 63, Skipped: 0, Total: 63, Duration: 496 ms`
  - `PetMovementTests.UpdateBounds_WithNonZeroOrigin_ClampsCorrectly`
  - `PetMovementTests.StepTowardsTarget_WhenAlreadyAtTarget_ReturnsArrivedWithoutDivisionByZero`
  - `PetMovementTests.StepTowardsTarget_AdvancesCorrectDistanceAlongVector`
  - `PetMovementTests.StepTowardsTarget_WhenDistanceLessThanMaxStep_ArrivesExactly`
  - `PetMovementTests.PetController_PickNewDestination_AlwaysGeneratesCoordinatesInsideBounds`
  - `PetMovementTests.PetController_Update_WithLargeDelta_ClampsDeltaToPreventTeleportation` (chống teleportation khi hệ thống sleep/resume: kẹp delta tối đa 0.2s, quãng đường tối đa 20px).
  - `PetMovementTests.PetMovement_UpdateBounds_WhenBoundsShrinkMidWalk_ClampsPositionAndDestination` (co cụm màn hình giữa lúc đi bộ: tự động kẹp lại vị trí và đích đến vào kích thước mới).
  - `SpritePlayerTests.LoadFromDefinition_LoadsDefaultPetSuccessfully`
  - `SpritePlayerTests.GetFrame_CalculatesFrameIndexByElapsedTimeAndFps`
  - `SpritePlayerTests.GetFrame_WithUnknownAction_FallsBackToIdle`
  - `DesktopPetSessionTests.ActivatePet_LoadsDefaultPetAndInitializesDimensions`
  - `DesktopPetSessionTests.SetDisplaySettings_ClampsValuesWithinSafeLimits`
  - `DesktopPetSessionTests.ShowAndHide_TogglesIsVisibleState`
  - `DesktopPetSessionTests.Stop_CleansUpAndHidesSession`
  - `DesktopPetSessionTests.TryActivatePet_WhenNewLoadFails_PreservesOldPet` (replace guard: khi nạp pet mới thất bại, pet cũ vẫn được bảo toàn nguyên vẹn trên desktop).
  - `DesktopPetSessionTests.Stop_CleansUpWindowFrameAndResetsPlayer` (dọn dẹp tài nguyên: Stop xóa sạch `SpriteImage.Source`, giải phóng `SpritePlayer` và hủy token).
  - `PetControllerTests.PetController_TransitionsFromIdleToWalk_WhenIdleDurationElapsed` (xác nhận chuyển trạng thái từ Idle sang Walk khi hết thời gian idle).
  - `PetControllerTests.PetController_TransitionsFromWalkToIdle_UponArrivalAtDestination` (xác nhận chuyển trạng thái từ Walk sang Idle khi chạm đích).
  - `PetControllerTests.PetController_MultipleCycles_UndergoesRepeatedIdleAndWalkTransitions` (kiểm chứng ít nhất 4 chu kỳ chuyển đổi Idle và Walk liên tục qua mô phỏng 40s).
  - `PetControllerTests.PetController_FacingLeft_ReflectsHorizontalMovementDirection` (kiểm chứng quay mặt trái/phải theo hướng vector di chuyển).

### 4. Bốn góc màn hình & Đo đạc DPI thực tế (`--probe-corners`)
- **Lệnh**: `Start-Process VChibi.exe -ArgumentList '--probe-corners' -Wait`
- **Đặc tả môi trường**: Windows 11 Build 26100, màn hình chính Per-Monitor v2 DPI awareness.
- **Thông số đo đạc**:
  - WorkArea: Left=0, Top=0, Right=1920, Bottom=1020, Width=1920, Height=1020.
  - Dpi: 120 (Tỉ lệ co giãn DipScale = 1.25x).
  - Kích thước window DIP: 96x96 DIP.
  - Kích thước physical thực tế: `96 * 1.25 = 120x120` physical pixels.
- **Kiểm chứng 4 góc (`IsFullyInside` đạt 100%)**:
  - `TopLeft`: Target=(0, 0) -> Actual: Left=0, Top=0, Right=120, Bottom=120 -> `IsFullyInside: true`
  - `TopRight`: Target=(1800, 0) -> Actual: Left=1800, Top=0, Right=1920, Bottom=120 -> `IsFullyInside: true` (1920 - 120 = 1800, mép phải chạm đúng 1920, không lọt ngoài màn hình).
  - `BottomLeft`: Target=(0, 900) -> Actual: Left=0, Top=900, Right=120, Bottom=1020 -> `IsFullyInside: true` (1020 - 120 = 900, mép dưới chạm đúng 1020, không đè taskbar).
  - `BottomRight`: Target=(1800, 900) -> Actual: Left=1800, Top=900, Right=1920, Bottom=1020 -> `IsFullyInside: true`
- File kết quả lưu tại: `%LOCALAPPDATA%/VChibi/corner-probe-results.json`.

### 5. Khởi chạy thông thường (Ordinary Startup & Lifecycle)
- **Lệnh**: Chạy script `scripts/smoke_phase3_ordinary_startup.ps1` với `$ErrorActionPreference = 'Stop'`:
  - Khởi chạy `VChibi.exe` bình thường không có tham số dòng lệnh.
  - Trích xuất cửa sổ qua UI Automation:
    - `LibraryWindow`: HWND `0x850CB6`, Class `Window`, Name `VChibi`.
    - `PetWindow`: HWND `0x9A0E66`, Class `Window`, Name `VChibi Desktop Pet`.
  - Cả 2 cửa sổ cùng hiển thị đồng thời; pet xuất hiện trên desktop.
  - Tọa độ pet chuyển dịch tự nhiên: `Left=0, Top=0` -> sau 2 giây: `Left=33, Top=4`.
  - Gửi `InvokePattern` vào `ExitButton` trên LibraryWindow:
    `Process exited cleanly: True, ExitCode: 0`.
  - Kiểm tra giải phóng tài nguyên: file `.instance.lock` được dọn sạch khi `AppLock` dispose, không còn process treo.

### 6. Smoke Test Click-Through & Input Xuyên Pet
- **Lệnh**: Chạy script `scripts/smoke_phase3_clickthrough.ps1` với `$ErrorActionPreference = 'Stop'`:
  1. Khởi chạy `Notepad.exe` thật (PID=47540, HWND=`0x170EDC`). Đặt vị trí (250, 150, 750, 550).
  2. Khởi chạy `VChibi.exe --smoke-runtime`, hiển thị pet ở (400, 300, 96, 96 DIP -> 120x120 physical px), nằm đè lên vùng soạn thảo Notepad.
  3. Click chuột thẳng vào giữa thân pet tại tọa độ `(448, 348)`.
  4. Kiểm tra cửa sổ nhận focus: `GetForegroundWindow()` trả về `0x170EDC`, PID `47540` (chính xác là Notepad). Khẳng định `WS_EX_TRANSPARENT` và `WS_EX_NOACTIVATE` hoạt động 100%.
  5. Gõ chuỗi ký tự `VChibi_Pet_ClickThrough_Verified_OK` xuyên qua thân pet vào Notepad.
  6. Trích xuất text từ `ControlType.Document` của Notepad qua UI Automation: nhận đúng chuỗi `VChibi_Pet_ClickThrough_Verified_OK\r\n`.
  7. Ảnh chụp màn hình bằng chứng thực tế tại `docs/evidence/phase3-pet-clickthrough.png`.
  8. Theo dõi chuyển động pet qua 8 mốc thời gian (2.8 giây):
     `t=0s: X=414, Y=298` -> `t=2.8s: X=557, Y=273`. Tọa độ chuyển động liên tục, không nhảy bước, không vượt ra ngoài vùng làm việc.

### 7. Nghiệm thu thực tế toàn diện tiêu chí chấp nhận A02, A03, A15 (`smoke_phase3_full_acceptance.ps1` & `smoke_phase3_ordinary_startup.ps1`)
- **Script thực thi**: `scripts/smoke_phase3_full_acceptance.ps1` và `scripts/smoke_phase3_ordinary_startup.ps1`.
- **Kết quả thực nghiệm**:
  1. **A03 (Click-through, double-click, drag, scroll xuyên pet & Taskbar/Alt+Tab absence)**: ĐẠT 100%.
     - **Kiểm tra vắng mặt trên Taskbar & Alt+Tab**: Xác minh Win32 styles `WS_EX_TOOLWINDOW=True`, `WS_EX_APPWINDOW=False`. Duyệt UIAutomation trên taskbar Windows (`Shell_TrayWnd`), khẳng định không có taskbar button cho `PetWindow`.
     - **Alpha Margin Click (Alpha=0)**: Click tại (410, 310) xuyên pet vào Notepad, Notepad giữ foreground, gõ và nhận text thành công.
     - **Opaque Body Click (Alpha=255)**: Click tại (541, 331) xuyên thân pet vào Notepad, Notepad giữ foreground, gõ và nhận text thành công.
     - **Mouse Double-Click xuyên pet**: Thực hiện double-click qua thân pet vào từ trong Notepad. Trích xuất qua UI Automation `TextPattern.GetSelection()`: số lượng selection = 1, nội dung bôi đen chính xác là `'RowNumber'`.
     - **Mouse Drag-Select xuyên pet**: Kéo chuột (LeftDown -> di chuyển 60px -> LeftUp) qua thân pet. UI Automation xác nhận vùng chọn text `'Number'` được bôi đen thành công.
     - **Mouse Wheel Scroll xuyên pet**: Cuộn chuột qua thân pet (12 ticks). Xác minh trạng thái Notepad thay đổi rõ rệt:
       - Nội dung dòng đầu tiên nhìn thấy: `'FirstHeaderLine LongSelectableWordToTestDoubleclic'` -> đổi thành `'Line_45 ContentPaddingForScrolling_RowNumber_45'` (`TextChanged=True`).
       - Giá trị thanh cuộn `ScrollBar.RangeValuePattern.Value`: tăng từ `0` lên `822` (`ScrollBarIncreased=True`).
  2. **A02 (Di chuyển đa chu kỳ Idle & Walk qua máy trạng thái chuyển tiếp, lật mặt `FacingLeft`/`FlipScaleX`)**: ĐẠT 100%.
     - Mẫu theo dõi trực tiếp trong phiên thực tế (`smoke-runtime-status.json`):
       - Chu kỳ đứng 1 (`Episode=idle#1`): `t=0.5s` đến `t=1.6s` (3 mẫu Idle liên tiếp) đứng yên tại `(259, 374)`, ghi nhận `Action=idle, FacingLeft=True, FlipScaleX=-1, Idle=True`.
       - Chu kỳ đi 1 (`Episode=walk#1`, đi sang phải): `t=2.1s` đến `t=21.6s` (39 mẫu Walk liên tiếp) di chuyển từ `(279, 385)` đến `(1150, 822)`, ghi nhận `Action=walk, FacingLeft=False, FlipScaleX=1`.
       - Chu kỳ đứng 2 (`Episode=idle#2`): `t=22.2s` đến `t=22.7s` (2 mẫu Idle liên tiếp) dừng nghỉ tại đích `(1150, 822)`, ghi nhận `Action=idle, FacingLeft=False, FlipScaleX=1, Idle=True`.
       - Chu kỳ đi 2 (`Episode=walk#2`, đảo hướng sang trái): `t=23.2s` tại `(1137, 817)`, ghi nhận `Action=walk, FacingLeft=True, FlipScaleX=-1`. Cột transform ScaleX tự động lật -1 khi di chuyển ngược chiều.
       - Thống kê chuyển tiếp trạng thái: `WalkEpisodes=2 (40 samples), IdleEpisodes=2 (5 samples), FacingLeft=True, FacingRight=True`. Đạt tiêu chí tối thiểu (WalkEpisodes >= 2, IdleEpisodes >= 2, tách biệt bởi các pha chuyển động) và khẳng định hành vi đa chu kỳ thực tế 100%.
       - Mọi tọa độ luôn nằm trong giới hạn màn hình `rcWork` (0, 0, 1920, 1020).
     - **Delta clamping qua đóng băng tiến trình (`NtSuspendProcess` / `NtResumeProcess`) vs ACPI OS Sleep**:
       - **Thử nghiệm đóng băng tiến trình thực tế (`NtSuspendProcess` / `NtResumeProcess`)**:
         - Tọa độ trước khi suspend: `(1137, 817)`.
         - Đóng băng tiến trình VChibi 3.5 giây (`NtSuspendProcess` trả về `0 = STATUS_SUCCESS`).
         - Đánh thức tiến trình (`NtResumeProcess` trả về `0 = STATUS_SUCCESS`).
         - Tọa độ ngay sau khi resume: `(1125, 812)`.
         - Khoảng cách di chuyển qua khoảng ngắt 3.5 giây: `13.0 pixels` (dx=12, dy=5).
         - Khẳng định: Nếu không kẹp delta time, bước nhảy trong 3.5s sẽ là `3.5s * 50px/s = 175 pixels` gây teleport giật cục. Bộ kẹp delta time (`if (deltaSeconds > 0.2) deltaSeconds = 0.2`) đã kẹp chính xác bước nhảy đầu tiên sau khi thức dậy về mức an toàn (13.0px cho 1-2 render ticks). Không có teleportation, không có callback dồn dập.
       - **Logic unit test**: `Update_WithLargeDeltaTime_ClampsToMaxDelta` kiểm chứng bước nhảy thời gian 10s bị kẹp tối đa 0.200s (khoảng cách tối đa 8 DIP).
       - **Trạng thái tiêu chí dòng 49**: TIÊU CHÍ ACPI OS SLEEP/RESUME GIỮ OPEN. Thử nghiệm trên đã chứng minh cơ chế đóng băng tiến trình và delta clamp của app, nhưng sự kiện thông báo ACPI Power Suspend/Resume cấp OS chưa kích hoạt phần cứng để tránh đưa máy trạm phát triển vào chế độ ngủ thật (tuân thủ quy định "không fake pass"). Phase 3 giữ trạng thái In-Progress cho mục này.
  3. **A15 (Scale / DPI / Taskbar / Thu nhỏ cửa sổ quản lý)**: ĐẠT 100%.
     - Per-Monitor v2 DPI=120 (1.25x scale), 120x120px physical footprint. Bốn góc màn hình đều đạt `IsFullyInside: true`.
     - Thu nhỏ `LibraryWindow` (`SW_MINIMIZE`): `IsWindowVisible(petHwnd) == True`, pet tiếp tục di chuyển trên màn hình từ (0, 0) đến (28, 26).
     - Khôi phục `LibraryWindow` (`SW_RESTORE`): giao diện quản lý trở lại bình thường.
     - Thoát ứng dụng qua `ExitButton`: thoát sạch sẽ trong 5 giây, lock file `.instance.lock` được dọn dẹp không để lại process treo.

## Phase 4 — Thư viện, cài đặt, tray và vòng đời (2026-10-11)

### 1. Unit & Behavioral Tests
- **Lệnh**: `dotnet test VChibi.sln`
- **Kết quả**: `Passed! - Failed: 0, Passed: 71, Skipped: 0, Total: 71, Duration: 1 s`
- **Các ca kiểm chứng chính**:
  - `Initialize_SortsPetsNaturallyByNameThenId`: sắp xếp tự nhiên theo tên (không phân biệt hoa thường), tie-break bằng ID.
  - `Selection_DoesNotChangeActivePetOnDesktop`: thay đổi `SelectedPet` trong danh sách không làm đổi `ActivePetId` hay pet đang chạy trên desktop.
  - `ScaleAndSpeedChanges_UpdateSelectedPetSettingsAndAppliesLiveIfActive`: thanh trượt Scale và Speed cập nhật tức thời phiên desktop nếu pet đang được kích hoạt; cập nhật cài đặt lưu trữ độc lập nếu pet chưa kích hoạt.
  - `CorruptSettings_DisablesPersistenceAndAllowsResetOrCancel`: phát hiện `settings.json` lỗi cấu trúc/dữ liệu; vô hiệu hóa lưu tự động (`IsPersistenceEnabled = false`); hủy bỏ (`Cancel`) giữ nguyên bytes gốc của tệp bị lỗi; từ chối xác nhận reset giữ nguyên file lỗi; mô phỏng lỗi ghi đĩa khi reset (`lockStream` độc quyền) tiếp tục khóa recovery; chỉ khi xác nhận và ghi thành công mới ghi đè defaults.
  - `ReleaseToDesktop_WhenHidden_ActivatesSelectedPet`: khi pet đang ẩn, bấm "Thả ra desktop" kích hoạt và hiển thị pet được chọn.
  - `Initialize_WhenNoPetsAvailable_KeepsPetHiddenAndSetsEmptyStatus`: thư viện rỗng giữ pet ẩn và thông báo trạng thái rõ ràng.
  - `Initialize_WhenSavedActivePetMissing_FallsBackAndWarns`: pet đã lưu trước đó không còn tồn tại trong thư viện sẽ tự động lùi về pet khả dụng và cảnh báo người dùng.

### 2. Nghiệm thu thực tế Live Smoke Test (`scripts/smoke_phase4_library_lifecycle.ps1`)
- **Lệnh**: `powershell -ExecutionPolicy Bypass -File scripts/smoke_phase4_library_lifecycle.ps1`
- **Môi trường**: Windows 11 Build 26100, cách ly hoàn toàn qua thư mục dữ liệu tạm thời (`--data-dir %TEMP%\vchibi_smoke_p4_*`).
- **10 bước kiểm chứng thực nghiệm**:
  1. **Khởi chạy thông thường với 2 gói pet & Win32 extended styles**:
     - `LibraryWindow`: HWND `0x5708D6`, Title `VChibi`, `Visible: True`.
     - `PetWindow`: HWND `0x78105C`, Title `VChibi Desktop Pet`, `Visible: True`.
     - Win32 Extended Styles: `WS_EX_TOOLWINDOW: True`, `WS_EX_TRANSPARENT: True`, `WS_EX_NOACTIVATE: True`.
     - Quản lý tệp khóa: `.instance.lock` được xác nhận tồn tại trên đĩa.
  2. **Độc lập Selected vs Active & Per-ID Settings trên Live UI đa pet (Tiêu chí A01, A04, A06, A15)**:
     - Nạp 2 pet trong `ListBox`: Pet 1 (`Chibi Idol`, base 96x96) và Pet 2 (`Second Chibi`, base 16x16).
     - Chỉnh Pet 1: Scale 200%, Speed 100 DIP/s -> `PetWindow` mở rộng thành `240x240px` (gấp đôi kích thước ban đầu 120x120px).
     - **Kiểm chứng độc lập selection**: Chọn Pet 2 trong `ListBox`, `PetWindow` trên desktop VẪN LÀ PET 1 (kích thước giữ nguyên chính xác `240px`, không đổi).
     - Chỉnh Pet 2: Scale 150%, Speed 60 DIP/s.
     - Chọn lại Pet 1: Thanh trượt phục hồi đúng 200% và 100 DIP/s của Pet 1.
     - Chọn lại Pet 2 và bấm "Thả ra desktop": Pet 2 trở thành active pet; kích thước `PetWindow` cập nhật chính xác footprint physical `30x30px` ($16\text{px} \times 1.5 \times 1.25\text{ DPI}$).
  3. **Lệnh Ẩn / Thả Desktop Pet (Tiêu chí A01, A04)**:
     - Bấm `HideFromDesktopButton`: `PetWindow` chuyển sang ẩn (`IsWindowVisible: False`).
     - Bấm `ReleaseToDesktopButton`: `PetWindow` hiển thị trở lại (`IsWindowVisible: True`).
  4. **Bảo vệ va chạm tiến trình thứ 2 (Single-Instance Lock Collision)**:
     - Khởi chạy tiến trình 2 với cùng thư mục dữ liệu.
     - Tiến trình 2 phát hiện `.instance.lock` đã bị giữ, hiển thị hộp thoại cảnh báo modal `VChibi` (#32770).
     - Đóng hộp thoại: tiến trình 2 thoát sạch với mã `ExitCode = 0`. Tiến trình 1 tiếp tục hoạt động nguyên vẹn.
  5. **Hành vi đóng cửa sổ ẩn về khay (Close-to-Hide)**:
     - Gửi `WM_CLOSE` đến `LibraryWindow`: `LibraryWindow` chuyển sang ẩn (`Visible: False`).
     - `PetWindow` tiếp tục hiển thị (`Visible: True`) và hoạt động bình thường trên màn hình desktop.
  6. **Khôi phục cửa sổ quản lý qua thông điệp khay hệ thống (Tray Double-Click)**:
     - Gửi thông điệp shell `WM_TRAYICON (0x0465)` với `WM_LBUTTONDBLCLK (0x0203)` đến `VChibi_TrayMessageWindow` (HWND `0x900A26`).
     - `LibraryWindow` khôi phục trạng thái hiển thị (`Visible: True`), cây visual và UIAutomation nạp đầy đủ controls.
  7. **Thoát sạch và giải phóng khóa ứng dụng (Tiêu chí A14)**:
     - Kích hoạt `ExitButton` (AutomationId: `ExitButton`).
     - Tiến trình 1 thoát sạch sẽ trong thời hạn (ExitCode = 0).
     - Tệp `.instance.lock` tự động giải phóng và biến mất trên đĩa nhờ cờ Win32 `DeleteOnClose`.
  8. **Lưu trữ cài đặt đa pet độc lập (Per-ID Persistence)**:
     - Tệp `settings.json` ghi nhận:
       - `activePetId`: `"pet-second-id"`
       - `pets.["default-chibi"]`: `scalePercent = 200`, `movementSpeed = 100`
       - `pets.["pet-second-id"]`: `scalePercent = 150`, `movementSpeed = 60`
  9. **Khôi phục trạng thái khi mở lại (Relaunch Restore State)**:
     - Khởi động lại ứng dụng từ cùng thư mục cài đặt: `PetWindow` tự động khởi tạo đúng kích thước `30x30px` (khớp chính xác footprint 150% của Pet 2).
     - Thoát tiến trình khởi động lại sạch sẽ.
  10. **Xử lý tệp cài đặt hỏng & Bảo vệ Cancel (Tiêu chí A17)**:
     - Ghi đè `settings.json` bằng chuỗi JSON hỏng (`{ corrupted json invalid payload `).
     - Khởi chạy ứng dụng: ứng dụng phát hiện tệp hỏng, hiển thị banner cảnh báo và tắt lưu tự động.
     - Bấm nút "Thoát không lưu" (`CancelSettingsRecoveryButton`): ứng dụng thoát ngay lập tức.
     - Kiểm tra nội dung `settings.json` trên đĩa: 100% bytes gốc của payload lỗi được giữ nguyên, không bị ghi đè hay mất dữ liệu.

### 3. Giới hạn kiểm chứng & Mục giữ OPEN
- **Mục giữ OPEN**:
  - `Preview FPS invariant thực tế qua đo đạc trực tiếp nhiều mức tốc độ`: Code dùng DispatcherTimer độc lập cho di chuyển và preview, nhưng chưa có script đo FPS render loop trực tiếp ở nhiều mức speed.
  - `Edge/DPI boundary clamping ở mức scale cực đại 400% khi kéo sát biên`: Đã kiểm chứng thuật toán clamp qua unit test, chưa đo đạc trực tiếp tại mép màn hình ở scale 400%.
  - `Minimize SW_MINIMIZE vs Close trong Phase 4`: Đã nghiệm thu ở Phase 3 (`smoke_phase3_ordinary_startup.ps1`), Phase 4 tập trung vào `WM_CLOSE` to hide.
  - `Khởi chạy UI với settings JSON sai ngữ nghĩa (speed âm, scale=0)`: Đã có unit test chặn và clamp, chưa làm smoke script khởi chạy UI riêng cho ca này.
  - `Pending write race condition guard`: Đã kiểm chứng atomic rename + try/catch trong unit test, chưa giả lập live race condition trong kịch bản crash OS.

## Phase 5 — Editor tạo/sửa pet và import gói (2026-10-11)

### 1. Kết quả kiểm thử tự động (Unit Tests)
- **Lệnh**: `dotnet test VChibi.sln`
- **Kết quả**: 91/91 tests Passed (0 failed, 0 skipped, thời gian chạy: ~1.0 giây).
- **Phạm vi kiểm thử mở rộng cho Phase 5**:
  - `PetValidatorTests`:
    - `TryPreflightPngStream_Sec01_RejectsLargeAncillaryChunks_EvenWithSmallPixels`: Kiểm chứng SEC-01 từ chối file PNG có ancillary chunk lớn (dù pixel 16x16 nhỏ) khi vượt ngân sách file size trước khi gọi giải mã native.
    - `TryPreflightPngStream_Calculates16BitRgbaNativePeakCorrectly`: Kiểm chứng tính toán peak bộ nhớ cho ảnh 16-bit RGBA (64 bits/pixel = 8 bytes/pixel, native peak = width * height * 8 * 2 bytes, Pbgra32 cache = width * height * 4 bytes).
    - `ValidatePackageAssets_RejectsManifestWithDirectoryTraversal`: Kiểm chứng từ chối manifest chứa đường dẫn vượt ra ngoài (`../../outside.png`).
    - `ValidatePackageAssets_RejectsOversizeEncodedTotal`, `ValidatePackageAssets_RejectsOversizeDecodedTotal`, `ValidatePackageAssets_RejectsOversizeFrameDimension`, `ValidatePackageAssets_RejectsExcessiveFramesPerAction`.
    - `ValidateManifestJson_RejectsMissingIdleOrWalk`, `ValidateManifestJson_RejectsUnknownActionNames`, `ValidateManifestJson_RejectsFpsOutOfRange`.
  - `PetStoreTests`: Đọc, lưu, sao chép thư mục, sinh ID duy nhất tránh trùng lặp (`-2`, `-3`), xóa pet, cách ly file nguồn.

### 2. Nghiệm thu thực tế Live Smoke Test (`scripts/smoke_phase5_editor_import.ps1`)
- **Lệnh**: `powershell -ExecutionPolicy Bypass -File scripts/smoke_phase5_editor_import.ps1`
- **Kết quả**: Thành công 100% (Mã thoát 0, hoàn tất toàn bộ 7 kịch bản kiểm chứng thực tế).
- **7 bước kiểm chứng thực nghiệm**:
  1. **Khởi chạy thông thường & Win32 PetWindow**:
     - `LibraryWindow`: HWND `0x570914`, Title `VChibi`.
     - `PetWindow`: HWND `0x680866`, xác nhận các cờ Win32 `WS_EX_TRANSPARENT=True`, `WS_EX_NOACTIVATE=True`, `WS_EX_TOOLWINDOW=True`.
  2. **Tạo Pet qua Editor, Đổi FPS riêng biệt, Lưu & Mở lại kiểm tra, Lưu & Thả desktop, Cách ly nguồn ảnh (Tiêu chí A07, A08)**:
     - Mở Editor qua nút `CreatePetButton`.
     - Đặt tên pet: `Smoker Idol Chibi`.
     - Nạp 2 khung hình Idle 48x48 qua hộp thoại native `OpenFileDialog` (`#32770`).
     - Thiết lập Idle FPS = 6 (qua `FpsSlider`).
     - Chuyển sang tab Walk qua `WalkTabRadio`.
     - Nạp 2 khung hình Walk qua `OpenFileDialog`.
     - Đổi thứ tự khung hình với `MoveFrameUpButton`.
     - Thiết lập Walk FPS = 12 (qua `FpsSlider`).
     - Lưu bản nháp bằng `SaveButton`: Editor đóng, danh sách thư viện tăng lên 2 pet.
     - **Kiểm chứng độ bền thứ tự khung hình qua mã băm nội dung SHA256 (A07)**:
       - Tệp gốc `walk_001.png`: hash `E1C8D8C80709B95E72D15CDBBED51E8FB8A59E3800F951F2B6664B557B955511`.
       - Tệp gốc `walk_002.png`: hash `402F9B747327FC0B0BF22F521DC6AF50B6AAC4AF639F1C0DB16FB7313723FB6F`.
       - Khung hình 2 được đảo lên đầu qua `MoveFrameUpButton`.
       - Tệp lưu `walk/001.png` trên đĩa có hash: `402F9B74...` (khớp 100% với tệp gốc `walk_002.png`).
       - Tệp lưu `walk/002.png` trên đĩa có hash: `E1C8D8C8...` (khớp 100% với tệp gốc `walk_001.png`).
       - Xác nhận: Thứ tự khung hình hoán đổi được bảo toàn trọn vẹn trên tệp lưu trữ đĩa vật lý.
     - **Mở lại Editor để kiểm tra tính toàn vẹn (A07)**: Chọn pet vừa tạo, bấm `EditPetButton`.
       - Tên pet hiển thị đúng: `Smoker Idol Chibi`.
       - Idle FPS giữ đúng giá trị đã lưu: `6`.
       - Tab Walk giữ đúng giá trị đã lưu: `12`.
       - Số lượng khung hình Walk hiển thị đủ 2 khung hình đã được đảo thứ tự.
     - **Bấm "Lưu và thả ra desktop" (`SaveAndActivateButton`) (A07)**:
       - Editor đóng ngay lập tức trong 1 thao tác.
       - Kích thước `PetWindow` cập nhật lập tức thành `60x60px` ($48\text{px} \times 1.25\text{ DPI}$) trên desktop mà không cần bước bấm "Thả ra desktop" trung gian.
     - **Xóa sạch thư mục ảnh nguồn trên đĩa (A08)**:
       - Xóa hoàn toàn thư mục `sourcePngDir`. Xác nhận `Test-Path = False`.
       - `PetWindow` vẫn hiển thị và chạy animation 60x60px bình thường trên desktop nhờ cơ chế copy tài nguyên vào kho lưu trữ nội bộ của ứng dụng.
  3. **Chỉnh sửa Pet hiện có & Bảo vệ Dirty Changes khi Cancel (Tiêu chí A09)**:
     - Chọn `default-chibi`, bấm `EditPetButton`.
     - Sửa tên thành `Dirty Attempt Name` (tạo trạng thái IsDirty = True).
     - Bấm nút `CancelButton`: Ứng dụng phát hiện thay đổi chưa lưu, hiển thị hộp thoại xác nhận hủy (#32770).
     - Bấm "Yes" để xác nhận hủy: Editor đóng lại, dữ liệu tên pet gốc `Chibi Idol` được giữ nguyên vẹn 100%, không bị ô nhiễm.
  4. **Chỉnh sửa Pet đang chạy trên Desktop & Hot Reload khi Lưu (Tiêu chí A10)**:
     - Chọn pet đang active (`Smoker Idol Chibi`), bấm `EditPetButton`.
     - Sửa tên thành `Smoker Idol HotReload`, bấm `SaveButton`.
     - Editor đóng lại: Pet đang chạy trên desktop thực hiện hot reload nạp lại định nghĩa mới mà không làm sập hoặc gián đoạn phiên runtime.
  5. **Nhập gói Pet từ thư mục & Tự sinh ID duy nhất khi nhập lặp lại (Tiêu chí A11)**:
     - Bấm `ImportPackageButton`: Tự động điều hướng hộp thoại thư mục native (`IFileOpenDialog` với cờ `FOS_PICKFOLDERS`).
     - Nhập gói pet `imported-idol` (base 16x16px): Thông báo trên thanh trạng thái báo thành công.
     - Tổng số pet tăng lên 3. `PetWindow` vẫn giữ nguyên kích thước 60px của active pet (không bị chiếm quyền hiển thị).
     - Nhập lại đúng thư mục đó lần thứ hai: Ứng dụng tự động sinh ID duy nhất mới (`imported-idol-2`).
     - Tổng số pet tăng lên 4, không gây xung đột ghi đè hay lỗi dữ liệu.
  6. **Kiểm tra tính hợp lệ khi nhập gói & Phản hồi lỗi (Tiêu chí A12)**:
     - **Trường hợp 6.1 (Người dùng Hủy chọn thư mục)**: Bấm `ImportPackageButton`, gửi lệnh Cancel trên dialog -> Không thêm pet, không hiện dialog báo lỗi giả.
     - **Trường hợp 6.2 (Thiếu manifest `pet.json`)**: Chọn thư mục không có `pet.json` -> Ứng dụng hiển thị thông báo lỗi, đóng dialog qua OK, 0 pet được thêm.
     - **Trường hợp 6.3 (JSON manifest hỏng cú pháp)**: Gói có `pet.json` chứa cú pháp hỏng -> Báo lỗi cú pháp, đóng dialog qua OK, 0 pet được thêm.
     - **Trường hợp 6.4 (File PNG giả mạo - Fake PNG)**: Gói có file ảnh text giả PNG -> Báo lỗi định dạng PNG, đóng dialog qua OK, 0 pet được thêm.
     - **Trường hợp 6.5 (Tấn công Path Traversal `../../secret.png`)**: Manifest tham chiếu file ngoài thư mục gói -> Báo lỗi bảo mật đường dẫn không an toàn, đóng dialog qua OK, 0 pet được thêm.
     - Toàn bộ các trường hợp lỗi đều rollback sạch sẽ, số lượng pet trong thư viện được giữ nguyên ở mức 4.
  7. **Thoát ứng dụng sạch sẽ & Giải phóng tệp khóa (Tiêu chí A14)**:
     - Bấm `ExitButton`: Tiến trình thoát sạch (ExitCode = 0).
     - Tệp `.instance.lock` tự động giải phóng và xóa khỏi đĩa.

### 3. Giới hạn kiểm chứng & Mục giữ OPEN
- **Mục giữ OPEN**:
  - `Phát hiện reparse point / symlink trên live Windows file system`: Unit test đã phủ logic `HasReparsePointInPath` và từ chối symlink; tuy nhiên việc tạo symlink vật lý trong script PowerShell yêu cầu quyền Developer Mode hoặc Administrator, do đó giữ OPEN xác minh cấp quyền OS hạn chế.
  - `Drag & drop reordering khung hình bằng chuột`: Giao diện hiện hỗ trợ đầy đủ `MoveFrameUpButton` và `MoveFrameDownButton` đã kiểm chứng bằng UI automation. Thao tác drag & drop chuột trực tiếp chưa có kịch bản automation mô phỏng chuột kéo thả.

## Phase 6 — Nghiệm thu toàn luồng và publish Windows (2026-10-11)

### 1. Kết quả kiểm thử tự động Release (Unit Tests)
- **Lệnh**: `dotnet test VChibi.sln -c Release`
- **Kết quả**: 91/91 tests Passed (0 failed, 0 skipped, thời gian chạy: ~1.0 giây).

### 2. Xuất bản ứng dụng tự cấp (Self-Contained Publish)
- **Cấu hình**: `Windows.pubxml`
- **Lệnh**: `dotnet publish src/VChibi/VChibi.csproj -c Release -f net10.0-windows -r win-x64 --self-contained true -o artifacts/publish/win-x64`
- **Kết quả**:
  - Thư mục xuất bản: `artifacts/publish/win-x64/`
  - Tệp thực thi: `VChibi.exe` (tự chứa toàn bộ .NET runtime và thư viện desktop x64)
  - Biểu tượng ứng dụng: `Assets/app.ico`
  - Gói pet mặc định: `Assets/DefaultPet/pet.json`, `Assets/DefaultPet/idle/*.png`, `Assets/DefaultPet/walk/*.png`
  - Tệp thông cáo bản quyền: `NOTICES.md` (giấy phép MIT của VChibi, WPF-UI, CommunityToolkit.Mvvm, .NET runtime).

### 3. Nghiệm thu thực tế Standalone Ngoài Thư Mục Mã Nguồn (`scripts/smoke_phase6_publish_standalone.ps1`)
- **Lệnh**: `powershell -ExecutionPolicy Bypass -File scripts/smoke_phase6_publish_standalone.ps1`
- **Môi trường thử nghiệm**:
  - Sao chép toàn bộ thư mục `artifacts/publish/win-x64/` sang một thư mục độc lập trong `%TEMP%` (`C:\Users\HP\AppData\Local\Temp\vchibi_standalone_run_...`).
  - Thiết lập thư mục dữ liệu cách ly hoàn toàn qua `--data-dir`.
  - Khởi chạy `VChibi.exe` với thư mục làm việc (Working Directory) trỏ trực tiếp đến thư mục temp ngoài repo, không phụ thuộc vào repo.
- **Kết quả kịch bản smoke**: Kịch bản 10 bước hoàn thành sạch sẽ (Mã thoát 0):
  1. **Khởi chạy độc lập & PetWindow Win32 Styles**:
     - Tiến trình `VChibi.exe` khởi động ngoài repo (PID 29952).
     - `LibraryWindow`: HWND `0x1760B64`.
     - `PetWindow`: HWND `0x960EC6`.
     - Cờ Win32: `WS_EX_TRANSPARENT: True`, `WS_EX_NOACTIVATE: True`, `WS_EX_TOOLWINDOW: True`.
     - Tự động trích xuất và nạp gói `default-chibi` vào thư viện (1 pet).
  2. **Luồng đại diện Pet Editor & Va chạm tiến trình thứ 2 khi bản nháp editor đang mở**:
     - Bấm `CreatePetButton`: Mở editor.
     - Đặt tên `Standalone Idol`.
     - Nạp ảnh PNG 48x48 cho Idle và Walk qua hộp thoại native `OpenFileDialog`.
     - Đặt Idle FPS = 7, Walk FPS = 14.
     - **Kiểm chứng va chạm tiến trình thứ 2 khi bản nháp editor đang mở (chưa bấm Lưu)**:
       - Khởi chạy tiến trình VChibi thứ 2 trong lúc editor đang mở bản nháp chưa lưu.
       - Tiến trình 2 phát hiện `.instance.lock`, hiển thị modal dialog va chạm `VChibi` (#32770, Handle `0x9C06F4`).
       - Gửi `WM_CLOSE` đóng dialog: Tiến trình 2 thoát sạch với `ExitCode = 0`.
       - Tiến trình chính giữ nguyên 100% bản nháp trong editor, không bị mất dữ liệu hay lỗi trạng thái.
     - Bấm `SaveButton`: Editor đóng, bản nháp được lưu thành công, tổng số pet trong thư viện tăng lên 2.
  3. **Luồng đại diện Nhập gói (Package Import) trên bản Publish**:
     - Chuẩn bị gói pet `standalone-imported` (16x16) trong temp.
     - Bấm `ImportPackageButton`: Hộp thoại chọn thư mục native (`SelectFolder`, HWND `0x8B0EDC`) điều hướng tự động.
     - Nhập thành công, tổng số pet trong thư viện tăng lên 3.
  4. **Kiểm chứng tính độc lập giữa Selected ID và Active ID**:
     - Khi pet active là `Chibi Idol` (120x120px ở 100% scale), nhấp chọn `Imported Standalone` (16px base) trong ListBox.
     - Kích thước `PetWindow` trên desktop giữ nguyên `120x120px`, xác nhận việc chọn trong thư viện không tự động cướp hay thay đổi desktop pet.
  5. **Lặp lại Hide / Show / Replace trên toàn bộ các Pet**:
     - Bấm `HideFromDesktopButton`: `PetWindow` ẩn hoàn toàn (`IsWindowVisible: False`).
     - Bấm `ReleaseToDesktopButton`: `PetWindow` hiện trở lại (`IsWindowVisible: True`).
     - Thay thế sang `Imported Standalone` (16px): `PetWindow` cập nhật kích thước `20x20px` ($16\text{px} \times 1.0 \times 1.25\text{ DPI}$).
     - Thay thế sang `Standalone Idol` (48px): `PetWindow` cập nhật kích thước `60x60px` ($48\text{px} \times 1.0 \times 1.25\text{ DPI}$).
  6. **Quan sát CPU / Memory Active vs Hidden & Đo lường tăng trưởng tài nguyên**:
     - WorkingSet64 khi Desktop Pet ACTIVE: `343.77 MB`.
     - WorkingSet64 khi Desktop Pet HIDDEN: `343.34 MB` (hoạt ảnh và rendering tạm dừng).
     - Thực hiện 5 lần thay thế pet liên tiếp: WorkingSet64 đo được `346.17 MB` (Delta tăng trưởng quan sát được: `+2.4 MB`, ghi nhận số liệu quan sát thực tế).
  7. **Khóa va chạm tiến trình thứ hai khi ứng dụng chính đang ở khay hệ thống**:
     - Đóng cửa sổ chính về khay hệ thống (`WM_CLOSE`), `PetWindow` vẫn hiển thị bình thường.
     - Khởi chạy tiến trình `VChibi.exe` thứ hai (PID 29312) với cùng `--data-dir`.
     - Tiến trình 2 hiển thị hộp thoại cảnh báo va chạm đơn tiến trình (`Handle=0xFB0EA8`, Title='VChibi').
     - Đóng hộp thoại qua `WM_CLOSE`: Tiến trình 2 thoát sạch với ExitCode = 0.
     - Tiến trình chính trong khay hệ thống vẫn nguyên vẹn; gửi `WM_TRAYICON / WM_LBUTTONDBLCLK` đến `VChibi_TrayMessageWindow` (HWND `0x19702E0`) khôi phục `LibraryWindow` thành công.
     - Bấm `ExitButton`: Tiến trình chính thoát sạch sẽ với ExitCode = 0, tệp `.instance.lock` tự động giải phóng.
  8. **Khởi chạy lại & Kiểm chứng độ bền toàn diện đa Pet (Trạng thái hiển thị)**:
     - Khởi động lại `VChibi.exe` lần thứ hai từ thư mục standalone.
     - Thư viện nạp đầy đủ toàn bộ 3 pet (mẫu, tạo qua editor, nhập qua gói).
     - Kích thước `PetWindow` trên desktop tiếp tục duy trì chính xác `60x60px` của active pet `Standalone Idol` (HWND `0x980F24`).
     - Bấm `HideFromDesktopButton` ẩn pet trên desktop (`IsWindowVisible: False`).
     - Bấm `ExitButton`: Tiến trình 2 thoát sạch sẽ với mã thoát 0, lưu `isPetVisible: false` vào `settings.json`.
  9. **Kiểm chứng phục hồi trạng thái ẩn khi mở lại ứng dụng (A14)**:
     - Khởi động lại `VChibi.exe` lần thứ ba từ thư mục standalone.
     - Cửa sổ `LibraryWindow` mở bình thường; kiểm tra `PetWindow`: hoàn toàn không hiển thị (`IsWindowVisible: False`).
     - Bấm `ReleaseToDesktopButton`: `PetWindow` hiển thị trở lại bình thường trên desktop với đúng kích thước `60x60px`.
     - Bấm `ExitButton`: Tiến trình 3 thoát sạch sẽ với mã thoát 0.
  10. **Kiểm chứng phục hồi cài đặt hỏng Cancel -> Exit trên bản Publish Độc lập (A17)**:
     - Ghi chuỗi JSON hỏng (`{ corrupted json invalid payload for standalone `) vào `settings.json` trong thư mục dữ liệu standalone.
     - Khởi chạy `VChibi.exe` từ thư mục standalone: Ứng dụng phát hiện file lỗi, hiển thị banner cảnh báo và tắt lưu tự động.
     - Bấm nút "Thoát không lưu" (`CancelSettingsRecoveryButton`): Tiến trình standalone thoát sạch với `ExitCode = 0`.
     - Kiểm tra nội dung `settings.json`: 100% bytes tệp lỗi được bảo toàn nguyên vẹn, không bị ghi đè.
     - Kiểm tra `.instance.lock`: Tệp khóa được giải phóng và dọn dẹp sạch sẽ.
### 4. Bảng tổng hợp đối chiếu tiêu chí chấp nhận A01–A17

| Mã | Nội dung tiêu chí (Requirements canonical) | Trạng thái thực tế | Bằng chứng kiểm chứng thực tế |
|---|---|---|---|
| A01 | Mở lần đầu: UI light lavender, pet mẫu thật hiện và đi | ĐÃ ĐẠT (Phần mềm) | Phase 1: Header/accent Lavender `#7862B8`; Phase 3: Ordinary startup mở cùng lúc LibraryWindow và PetWindow, pet mẫu Chibi Idol tự di chuyển; Phase 6: Standalone ngoài repo tự nạp `default-chibi` |
| A02 | Quan sát di chuyển: Đi ngẫu nhiên X/Y, đứng/đi đúng animation, không ra ngoài work area | ĐÃ ĐẠT (Phần mềm) | Phase 3 step 7: 2 chu kỳ Walk (40 mẫu) và 2 chu kỳ Idle (5 mẫu), đổi hướng lật `FlipScaleX = -1 / 1`, tọa độ trong `rcWork` (0, 0, 1920, 1020); NtSuspendProcess 3.5s resume di chuyển 13.0px nhờ delta clamp 0.2s |
| A03 | Click/gõ vào app phía dưới pet: Click xuyên, không giành focus, không chặn làm việc | ĐÃ ĐẠT | Phase 3 step 6, 7: Notepad bên dưới PetWindow nhận click, focus, gõ text, double-click bôi đen từ, drag-select và cuộn chuột 12 ticks qua thân pet. Không hiện nút trên Taskbar hay Alt+Tab |
| A04 | Chọn pet khác: Preview đổi, active pet chưa đổi | ĐÃ ĐẠT | Phase 4 step 2: Chọn Pet 2 trong ListBox, preview đổi nhưng PetWindow giữ nguyên kích thước 240px của Pet 1; Phase 6 step 4: Standalone chọn pet 16px, PetWindow giữ nguyên 120px của active pet |
| A05 | Thả pet khác: Đổi pet thành công, luôn một cửa sổ pet | ĐÃ ĐẠT | Phase 4 step 2: Bấm ReleaseToDesktopButton đổi sang Pet 2 kích thước 30x30px, luôn duy trì 1 HWND PetWindow duy nhất; Phase 6 step 5: Thay thế luân phiên 3 pet trên desktop mượt mà |
| A06 | Chỉnh scale/tốc độ: Áp dụng ngay, clamp đúng, không thay FPS gói | ĐÃ ĐẠT MỘT PHẦN | Áp dụng tức thì cho active pet và FPS không đổi đã kiểm chứng (Phase 4 step 2); kẹp biên cực đại 400% và đo FPS live giữ OPEN |
| A07 | Tạo pet từ PNG: Đứng/đi, frame order, FPS và tên lưu đúng, không viết JSON | ĐÃ ĐẠT | Phase 5 step 2: Tạo pet bằng UI, nạp PNG qua OpenFileDialog, Idle FPS 6, Walk FPS 12, đảo thứ tự bằng MoveFrameUpButton; hash SHA256 xác nhận đĩa lưu đúng; mở lại editor thấy đủ; SaveAndActivate chạy ngay 60x60px |
| A08 | Xóa ảnh nguồn sau khi tạo: Pet đã lưu vẫn chạy | ĐÃ ĐẠT | Phase 2 step 6: StorageSmoke xóa sạch ảnh nguồn nạp lại vẫn đủ; Phase 5 step 2: Xóa hoàn toàn thư mục ảnh nguồn bên ngoài, PetWindow vẫn tiếp tục hiển thị và phát animation 60x60px |
| A09 | Sửa rồi hủy: Bản lưu/active pet không đổi | ĐÃ ĐẠT | Phase 5 step 3: Sửa tên pet đang có, bấm Hủy -> hiện dialog xác nhận #32770 -> chọn Yes -> dữ liệu gốc Chibi Idol và active pet giữ nguyên vẹn 100% |
| A10 | Sửa rồi lưu pet đang chạy: Bản mới cập nhật, lỗi giữ bản cũ | ĐÃ ĐẠT | Phase 5 step 4: Sửa active pet và bấm Lưu -> hot reload thành công không gián đoạn; unit test DesktopPetSessionTests kiểm chứng lỗi nạp bản mới bảo toàn bản cũ |
| A11 | Import gói hợp lệ: Thêm thư viện, ID mới, chưa đổi active pet | ĐÃ ĐẠT | Phase 5 step 5: Import folder qua native folder dialog, thêm vào thư viện, active pet giữ nguyên; import lần 2 tự sinh ID mới `imported-idol-2`; Phase 6 step 3: Standalone import gói thành 3 pet |
| A12 | Import hỏng/độc hại: Báo rõ, không crash/traversal/mục nửa vời | ĐÃ ĐẠT | Phase 5 step 6: Chặn sạch hủy dialog, thiếu manifest, JSON lỗi, fake PNG, path traversal `../../`; rollback an toàn, 0 pet nửa vời được thêm; PetValidatorTests phủ toàn bộ biên bảo mật |
| A13 | Ẩn/hiện và đóng/mở quản lý: Đúng trạng thái, không timer/window/tray trùng | ĐÃ ĐẠT | Phase 4 step 3, 5, 6 & Phase 6 step 5, 7: Bấm Ẩn/Thả đổi trạng thái; WM_CLOSE ẩn về tray pet vẫn chạy; double-click tray khôi phục LibraryWindow; không trùng lặp window hay timer |
| A14 | Thoát rồi mở lại: Process hết; phục hồi pet, cài đặt, trạng thái hiện/ẩn | ĐÃ ĐẠT (Phần mềm) | Phase 4 step 7, 9 & Phase 6 step 8, 9: ExitButton thoát sạch (ExitCode 0), .instance.lock tự xóa; mở lại phục hồi đủ danh sách 3 pet, active pet (60x60px) và trạng thái ẩn (IsPetVisible=false) không hiện PetWindow. (Pending write race giữ OPEN) |
| A15 | Scale/DPI/taskbar thay đổi: Khung pet trong work area, tốc độ DIP nhất quán | CHƯA KIỂM CHỨNG TOÀN DIỆN | Phase 3 step 4 đã kiểm chứng 4 góc DPI 120 và SW_MINIMIZE; xử lý khi thay đổi taskbar, kiểm chứng tốc độ DIP nhất quán và kẹp biên cực đại 400% giữ OPEN |
| A16 | Chạy bản publish ngoài repo: Asset mẫu đủ, lần đầu dùng được, không cần SDK | ĐÃ ĐẠT MỘT PHẦN | Phase 6 step 1–10 đã chạy standalone ngoài repo, nạp pet mẫu và đầy đủ tính năng; kiểm thử trên máy Windows sạch hoàn toàn không cài SDK giữ OPEN |
| A17 | Gói/settings đã lưu bị hỏng: Báo rõ và có đường phục hồi, không âm thầm giả thành công | ĐÃ ĐẠT | Phase 4 step 10 & Phase 6 step 10: JSON hỏng hiện banner cảnh báo; bấm "Thoát không lưu" thoát sạch, bảo toàn 100% tệp lỗi trên cả dev và standalone; gói pet hỏng báo chi tiết lý do |

### 5. Giới hạn kiểm chứng & Toàn bộ danh sách mục giữ OPEN

#### A. Các mục kiểm chứng Phase 4 giữ OPEN (chưa exercise live)
1. `Preview FPS invariant thực tế qua đo đạc trực tiếp nhiều mức tốc độ`: Code dùng timer độc lập không đổi FPS theo tốc độ di chuyển, nhưng chưa có script đo FPS live trực tiếp.
2. `Edge/DPI boundary clamping ở mức scale cực đại 400% khi kéo sát biên`: Đã unit test hàm clamp, chưa đo đạc kéo sát biên màn hình ở 400% live.
3. `Minimize SW_MINIMIZE vs Close trong Phase 4`: Đã nghiệm thu ở Phase 3 (`smoke_phase3_ordinary_startup.ps1`), kịch bản Phase 4 tập trung vào `WM_CLOSE` to hide.
4. `Khởi chạy UI với settings JSON sai ngữ nghĩa (speed âm, scale=0)`: Unit test đã phủ logic clamp/lùi mặc định; chưa khởi chạy live UI riêng cho ca này.
5. `Pending write race condition guard khi thoát trong lúc ghi đĩa dở dang`: Đã kiểm chứng atomic rename + try/catch trong unit test; chưa giả lập live race condition trong kịch bản crash OS.

#### B. Các mục kiểm chứng Phase 6 giữ OPEN
6. `Va chạm tiến trình thứ hai đúng thời điểm ghi đĩa / commit dở dang (in-flight save/commit)` [Race test phần mềm chưa exercise]: Đã kiểm chứng va chạm khi editor đang mở bản nháp chưa lưu (Step 2); việc giả lập va chạm đúng tích tắc micro-second của async commit dở dang trên đĩa chưa kích hoạt -> giữ OPEN.
7. `Trace kernel ETW chứng minh không decode / đọc đĩa mỗi khung hình` [Hạ tầng kernel trace]: Về kiến trúc, `SpritePlayer` nạp trước toàn bộ ảnh vào mảng bộ nhớ RAM và chỉ đổi con trỏ frame trên `CompositionTarget.Rendering`; kiểm chứng bằng ETW trace đĩa cấp kernel chưa ghi nhận file log -> giữ OPEN.
8. `Kiểm thử trên máy Windows sạch hoàn toàn không có .NET SDK` [Môi trường Clean VM / Sandbox]: Bản build là `--self-contained true` chứa đầy đủ runtime `.dll` và `apphost.exe`. Trên môi trường phát triển hiện tại máy đã cài SDK; việc kiểm chứng trên một máy tính Windows hoàn toàn sạch chưa từng cài bất kỳ runtime nào cần thực hiện thủ công trong môi trường VM hoặc sandbox bên ngoài -> giữ OPEN.
9. `Thử nghiệm ACPI Suspend / Sleep vật lý cấp OS` [Phần cứng OS]: Kiểm chứng bù thời gian trôi qua delta clamping đã được kiểm nghiệm qua mô phỏng dừng tiến trình Win32 `DebugActiveProcess`; việc đưa phần cứng thực tế vào Sleep (S3) và thức dậy cần kiểm chứng thủ công trên máy thật -> giữ OPEN.
10. `Kiểm thử tạo Reparse Point / Symlink vật lý` [Đặc quyền OS / Developer Mode]: Unit test đã phủ logic `HasReparsePointInPath`; việc tạo symlink vật lý trong script smoke đòi hỏi quyền Administrator hoặc Developer Mode của OS -> giữ OPEN.
