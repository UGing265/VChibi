# VChibi — kiến trúc triển khai

## Context

- [Yêu cầu](./requirements.md), [thực thi liên tục](./execution-contract.md), [nghiên cứu](./research.md).
- Repository quan sát hiện chỉ có AGENTS.md và docs; chưa có source, solution, test, docs/development-rules.md hoặc convention code để tái sử dụng.
- Một project app WPF + một project test logic khi có regression/boundary đáng giữ; project test không là tầng kiến trúc app.
- Kế hoạch docs-policy vẫn pending nhưng AGENTS.md đã tồn tại và quy định được nạp. Không cần chờ thay đổi đó, không có cross-plan blocker; không sửa trạng thái kế hoạch cũ ngoài phạm vi.

## Stack và theme

- Microsoft.NET.Sdk; target net10.0-windows; UseWPF=true.
- Package baseline: WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2; pin exact version, restore/build kiểm chứng ở phase 1. Đây là metadata đã nghiên cứu, chưa là build proof.
- CommunityToolkit: ObservableObject, field-backed ObservableProperty, RelayCommand/AsyncRelayCommand trong partial ViewModel. Không tự viết MVVM framework, không Messenger/Generic Host/DI container mặc định.
- Enable UseWindowsForms=true chỉ để dùng System.Windows.Forms.NotifyIcon; qualify/alias các type trùng WPF. Không đổi app thành WinForms.
- WPF UI ThemesDictionary Light + ControlsDictionary. Accent app-local lavender #7862B8; không theo màu/theme Windows, không thay đổi personalization hệ thống.
- Font chữ giữ default của toolkit/Windows; chưa khẳng định một resource key/font family cụ thể đã được kiểm chứng. Không dùng font icon làm body font, không chép Segoe font vào app. Dùng SymbolIcon có sẵn và giữ MIT/third-party notices.
- Publish thư mục self-contained win-x64; không trimming, AOT, single-file hoặc installer trong kế hoạch này.

## Folder và chủ sở hữu trách nhiệm

Root source tuyệt đối: D:/AShiroru/ProgramCode/Project/Personal/VChibi/src/VChibi/.
Mọi đường dẫn source dưới đây được nối với root này; không phải đề nghị ghi tài liệu ngoài docs.

```text
VChibi/
├── VChibi.sln
├── src/VChibi/
│   ├── VChibi.csproj
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── app.manifest
│   ├── Views/
│   │   ├── LibraryWindow.xaml + .xaml.cs
│   │   ├── PetEditorView.xaml + .xaml.cs
│   │   └── PetWindow.xaml + .xaml.cs
│   ├── ViewModels/
│   │   ├── LibraryViewModel.cs
│   │   └── PetEditorViewModel.cs
│   ├── Models/
│   │   ├── PetDefinition.cs
│   │   ├── AnimationDefinition.cs
│   │   ├── AppSettings.cs
│   │   └── PetDisplaySettings.cs
│   ├── Services/
│   │   ├── PetStore.cs
│   │   ├── SettingsStore.cs
│   │   └── FilePickerService.cs
│   ├── Runtime/
│   │   ├── DesktopPetSession.cs
│   │   ├── PetController.cs
│   │   ├── PetMovement.cs
│   │   └── SpritePlayer.cs
│   ├── Platform/Windows/
│   │   ├── PetWindowInterop.cs
│   │   ├── ScreenWorkspace.cs
│   │   └── TrayIcon.cs
│   ├── Controls/
│   │   └── SpritePreview.xaml + .xaml.cs
│   ├── Resources/
│   │   ├── Colors.xaml
│   │   └── Styles.xaml
│   ├── Assets/
│   │   ├── app.ico
│   │   └── DefaultPet/{pet.json,idle/*.png,walk/*.png}
│   └── Properties/PublishProfiles/Windows.pubxml
├── tests/VChibi.Tests/                 # Logic regression/boundary cần thiết
└── docs/
```

Chỉ tách PetValidator/PetImportService/AppPaths/SpriteFrameLoader/AnimationEditorViewModel/PetListItemViewModel khi logic đủ lớn và có trách nhiệm độc lập. Validation/import có thể ở PetStore ban đầu; frame loader có thể ở SpritePlayer; không tạo hàng loạt class rỗng.

## Luồng phụ thuộc

- Views binding ViewModels; event code-behind giới hạn file picker/focus/lifecycle thuộc View.
- ViewModels gọi Services và DesktopPetSession để thực hiện command. Constructor injection bằng App composition đơn giản, không service locator.
- Models không tham chiếu View/ViewModel/Windows.
- Runtime xử lý logic và tạo snapshot/frame/position; Platform adapter chịu HWND/DPI.
- PetWindow có thể code-behind cho SourceInitialized/size/closing; không bắt mọi HWND operation qua ViewModel.
- App quản lý cả LibraryWindow lẫn session/tray. PetWindow KHÔNG set Owner=LibraryWindow: minimize UI không được ẩn pet theo owned-window behavior.

## Contract dữ liệu gói v1

Ví dụ cấu hình DO APP TẠO, người dùng không phải chỉnh tay:

```json
{
  "schemaVersion": 1,
  "id": "stable-local-id",
  "name": "Idol A",
  "animations": {
    "idle": { "fps": 8, "frames": ["idle/001.png", "idle/002.png"] },
    "walk": { "fps": 8, "frames": ["walk/001.png", "walk/002.png"] }
  }
}
```

- ID do app quản lý; import cấp ID mới, không tin ID ngoại lai để quyết định đường dẫn ghi.
- idle/walk bắt buộc; mỗi danh sách ít nhất một frame; thứ tự JSON là playback order, không sort lại lúc nạp.
- Khi chọn ảnh lần đầu, dùng natural filename sort rồi hiển thị thứ tự để người dùng sửa. Không hứa biết thứ tự animation chỉ từ tên file.
- FPS hữu hạn, nguyên 1–60; tên trim không trống. Scale/tốc độ đi thuộc settings theo ID, không nằm trong animation.
- Phải decode được PNG thật, không chỉ kiểm tra extension. Pixel alpha giữ nguyên.
- Review fix A2: canvas lấy max PixelWidth/PixelHeight cả pet; scale 100% nghĩa một source pixel = một DIP. Đặt frame bottom-center với kích thước layout explicit, bỏ qua PNG metadata DPI cho geometry; preview/runtime cùng quy tắc, không sửa file gốc.
- Review fix A1: tối đa 256 frame/action, cạnh 2048 px, cache chuẩn Pbgra32 tối đa 64 MiB/frame-set. PNG native có thể 64bpp: trước decode dùng checked stride/height và native bit depth hoặc conservative 8 bytes/pixel, cộng các conversion/copy buffers đồng thời; decode từng frame, peak CPU pixel-surface budget 128 MiB/frame-set. Width*height*4 chỉ tính cache chuẩn, không native decode peak. Không gọi policy này là hard cap toàn process/GPU/WPF overhead.
- Review fix SEC-01: cap encoded PNG 32 MiB/file và tổng referenced PNG 128 MiB/gói; cap pet.json 1 MiB. Reject size trước copy/decode, đồng thời đếm bytes thực đọc/chép bằng bounded stream để source đổi trong operation không vượt budget. Header/pixel budget vẫn kiểm tra riêng; không giải nén ZIP hoặc tải URL.
- Resolve path tương đối bằng canonical full path và kiểm tra nằm trong package root với boundary separator; từ chối absolute/UNC/traversal/reparse escape. Chỉ chép file được manifest tham chiếu.

## Kho pet và tính nhất quán

- %LOCALAPPDATA%/VChibi/Pets/<id>/ và settings.json.
- Review fix F1: App giữ exclusive OS-held ownership lock trong data root (FileStream FileShare.None) trước recovery/seed/settings; giữ handle tới cuối shutdown. Lần mở exe thứ hai báo app đang ở tray rồi thoát, không load/store/session mới; không cần IPC. Serialize commit trong instance, không cho recovery đụng operation còn sống.
- Tạo/import: precheck path/encoded size/header, chuẩn bị staging cùng volume, bounded-copy PNG, validate/decode chính staged snapshot rồi ghi manifest cuối; công bố final khi tất cả thành công. Hủy/lỗi xóa staging do app tạo, không công bố mục thiếu file. Validation source không thay cho validation snapshot sẽ commit.
- Sửa: giữ bản cũ cho tới lúc bản mới chuẩn bị xong; commit bằng bước thay thế có recovery rõ, không ghi từng frame trực tiếp lên gói đang chạy. Cần backup ngắn hạn để rollback nếu thao tác filesystem cuối lỗi.
- App startup chỉ enumerate package committed, bỏ staging; xử lý recovery pending trước khi mở pet. Không gọi một chuỗi Directory.Move là transaction nguyên tử; phase 2 phải xác định thứ tự/recovery.
- Review fix F2/F3: Settings đọc bounded schema v1, map/IDs hợp lệ, scale hữu hạn 25–400%, speed hữu hạn 5–200 DIP/s; trường thiếu có default đã định, giá trị hiện diện sai trả corrupt-state. Write-temp/replace chỉ khi persistence được phép. Corrupt Reset xác nhận mới cho ghi; Cancel thoát không ghi, kể cả exit handler/autosave, giữ file gốc. Không âm thầm clamp rồi overwrite.
- DefaultPet là asset tự tạo/được phép dùng, không lấy idol có bản quyền mà không biết quyền phân phối. Có frame/animation thật; không placeholder trong bản phát hành.
- Startup chỉ seed default khi chưa có library/default; không copy lại hoặc overwrite pet người dùng mỗi lần mở.

## Runtime, chuột và HWND

- PetWindow là WPF Window thường: AllowsTransparency=true, Background=Transparent, WindowStyle=None, Topmost=true, ShowActivated=false, ShowInTaskbar=false, ResizeMode=NoResize.
- SourceInitialized: giữ style cũ, thêm WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW bằng pointer-sized Win32 API; kiểm tra lỗi return/last-error đúng.
- WS_EX_TRANSPARENT click-through dựa trên contract layered window do WPF quản lý. Không lấy Popup.SetHitTestable, IsHitTestVisible=false hoặc HTTRANSPARENT làm bằng chứng xuyên tới app khác process.
- Không gọi Activate/Focus/SetForegroundWindow/DragMove cho pet. Không manually UpdateLayeredWindow/SetLayeredWindowAttributes để tranh quyền alpha với WPF.
- Topmost chỉ trên cửa sổ thông thường; không cam kết phủ fullscreen exclusive, UAC secure desktop hoặc mọi system surface.

## Tọa độ và DPI

- Manifest PerMonitorV2. Một ScreenWorkspace query primary monitor và GetMonitorInfo.rcWork mới; không dựa riêng SystemParameters.WorkArea cache.
- Runtime movement position ở physical pixels; tốc độ và kích thước người dùng ở DIP, chuyển bằng DPI hiện tại/96.
- Window placement qua SetWindowPos với NOACTIVATE/NOZORDER/NOSIZE; không cùng lúc drive Window.Left/Top bằng phép tính DIP khác.
- Bounds tính theo actual HWND footprint: x ∈ [work.Left, work.Right-widthPx], y ∈ [work.Top, work.Bottom-heightPx]. Giữ origin, không giả Left/Top=0.
- Review fix A3: khi bounds/DPI/size đổi, đợi layout đúng rồi revalidate cả position và destination: clamp/reselect đích hợp lệ, destination trùng position xử lý như arrival. Effective fit giữ pet trong work area, không khoảng random âm hoặc walk mắc kẹt ở biên.
- Chỉ primary monitor; không thêm multi-monitor roaming.

## Scheduler và tài nguyên

- Một DispatcherTimer khoảng 30 Hz cho active pet, dùng Stopwatch/monotonic elapsed time. Vị trí dựa elapsed, frame dựa elapsed*fps; không cộng một frame/tick.
- idle duration ngẫu nhiên khoảng 1–4 giây; chọn destination trong toàn feasible rectangle, không 70/30. Đây là default hành vi, không thêm UI chỉnh personality.
- Movement dùng normalized vector, clamp, dừng chính xác tại đích; zero-distance xử lý không NaN. Clamp elapsed movement khi stall/resume và reset baseline để không teleport.
- SpritePlayer nạp frame trước playback với BitmapCacheOption.OnLoad, Freeze khi phù hợp để bỏ lock file/chia sẻ an toàn. Không decode/allocate bitmap mỗi tick; chỉ đổi Image.Source khi index đổi.
- Preview có timer riêng chỉ khi view hiển thị và đang play; editor/library preview không chạy ngầm khi rời màn hình. Thuật toán selection chung với runtime, cursor state riêng.
- Async load/copy hỗ trợ cancellation/generation guard. Bản load cũ không được cập nhật session/view đã thay/đóng.
- Thay pet: prepare validated frame set trước, commit session trên UI thread; tránh hai cửa sổ cùng hiện và giữ bản cũ nếu load thất bại.
- Hide/close: Stop timer, detach handler/hooks/system events, clear frame refs/cancel loads khi thích hợp. DispatcherTimer không IDisposable; chỉ Stop không đủ nếu event subscription vẫn giữ object.

## Vòng đời app và UI

- ShutdownMode.OnExplicitShutdown, App là owner logic.
- Close quản lý: hide, không shutdown; minimize không ảnh hưởng pet.
- Tray dùng NotifyIcon; menu native Mở quản lý, Hiện/Ẩn pet, Thoát. Menu native không cam kết style lavender như WPF UI.
- Review fix F4: Hiện từ UI/tray là activation của selected ID nếu khác active; load lỗi giữ hidden và báo rõ, không tự hiện active cũ thay thế. Startup visibility=true phục hồi active ID đã lưu, khác với thao tác Hiện chủ động.
- Exit command: set shutdown flag, save settings chỉ khi persistence enabled, stop preview/session, Dispose tray, close windows, Application.Shutdown, release ownership lock cuối cùng; Close handler không hide lại khi shutdown.
- Nếu lưu settings thất bại khi thoát, báo rõ và cho Retry/Thoát không lưu, không treo process vô hạn.
- Editor giữ draft riêng. Validation vào từng field/action, save command disabled trong lúc pending. Cancel/save/active replacement phân biệt.

## Kiểm chứng

- Phases có smoke cụ thể. Phase 6 giữ bằng chứng A01–A17, publish app chạy thật ngoài repo.
- Tests chỉ cho rủi ro logic: bounds nonzero origin/oversize, elapsed/frame timing, schema/path/memory validation, save/import cancel/failure. Không test static XAML/source text/default copies.
- Không gọi metadata/package docs là runtime proof; không tuyên bố click-through đúng trước khi thao tác xuyên sprite vào Notepad thật.
