# Nghiên cứu kỹ thuật và hiện trạng

## Phạm vi bằng chứng

Ngày 2026-10-11. Hai lát nghiên cứu độc lập: toolkit/MVVM/publish và desktop Window/input/DPI/timing. Chỉ đọc tài liệu; chưa restore, build hoặc chạy app. Metadata và documentation không chứng minh runtime đã đúng.

## Hiện trạng quan sát

- Root chỉ có AGENTS.md và docs/; chưa có source/solution/tests.
- docs/ có kế hoạch docs-policy và nhật ký liên quan. Không có docs/development-rules.md/codebase-summary.md/code-standards.md/design-guidelines.md.
- Kế hoạch docs-policy status pending, nhưng AGENTS.md đã hiện diện và được nạp trong context. Không có công việc source chung hoặc blocker cần chờ; không sửa kế hoạch cũ.
- dotnet --info: SDK 10.0.300 và 9.0.301; WindowsDesktop runtime 10.0.8; RID win-x64; Windows build 26100; chưa có global.json.
- Get-Command: node có, ck CLI không tìm thấy. Vì vậy ghi canonical Markdown trực tiếp, không tạo bảng phase qua CLI.

## Package, MVVM, theme

- [WPF-UI 4.3.0](https://www.nuget.org/packages/WPF-UI/4.3.0): stable, MIT, dependency group thực net10.0-windows7.0 theo metadata đã đọc.
- [CommunityToolkit.Mvvm 8.4.2](https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.2): stable, MIT, .NET Standard 2.0/2.1.
- [WPF UI README](https://github.com/lepoco/wpfui): ThemesDictionary Light và ControlsDictionary; control, icon, Gallery.
- [Accent API](https://wpfui.lepo.co/api/Wpf.Ui.Appearance.ApplicationAccentColorManager.html): Apply nhận Color, ApplicationTheme, systemGlassColor/systemAccentColor. Dùng app-local custom lavender, không bật SystemThemeWatcher hoặc ApplySystemAccent.
- [MVVM source generators](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/overview), [ObservableProperty](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/observableproperty), [RelayCommand](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/relaycommand): containing type partial; field-backed syntax tránh preview-language requirement.
- Docs online thường theo main, không phải immutable 4.3.0; kiểm chứng API khi restore/build phiên bản pin.

## Font và license

- [WPF UI icon section](https://github.com/lepoco/wpfui#segoe-fluent-icons) phân biệt Fluent System Icons và Segoe Fluent Icons; không cái nào là font body text.
- Chưa chứng minh tên FontFamily/resource body mặc định chính xác bằng các nguồn đã đọc. Giữ toolkit/default text font; không tự tuyên bố Segoe UI Variable là font nhúng của toolkit.
- [Windows font FAQ](https://learn.microsoft.com/en-us/typography/fonts/font-faq): quyền dùng font hệ thống không đồng nghĩa quyền chép font vào app.
- [Segoe Fluent Icons](https://learn.microsoft.com/en-us/windows/apps/design/iconography/segoe-fluent-icons-font): font hệ thống Windows 11, không mặc định Windows 10.
- [Third-party notices](https://github.com/lepoco/wpfui/blob/main/ThirdPartyNotices.txt): giữ notices của package. README/notices về Segoe có chỗ cần đối chiếu actual pinned package; không kết luận pháp lý từ README.

## Ordinary Window, input và activation

- [AllowsTransparency](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.allowstransparency?view=windowsdesktop-10.0), [Window.cs](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Window.cs): yêu cầu WindowStyle.None; đây là bằng chứng Window, không Popup.
- [ShowActivated](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.showactivated?view=windowsdesktop-10.0): chỉ ngăn activation ban đầu.
- [Extended styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles): WS_EX_NOACTIVATE, WS_EX_TOOLWINDOW; giữ style cũ, dùng pointer-sized API.
- [Layered windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows): alpha-zero pass-through; WS_EX_TRANSPARENT trên layered window bỏ shape và pass mouse xuống bên dưới. Không khái quát cờ này cho mọi Window không layered.
- [WM_NCHITTEST](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest): HTTRANSPARENT chỉ chuyển tới window cùng thread; không chứng minh input xuyên app khác process.
- Topmost không bảo đảm phủ exclusive fullscreen hoặc secure desktop. Không set pet Owner=management window vì minimize owner có thể hide owned window.

## Work area, DPI, timing

- [MONITORINFO](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-monitorinfo): rcWork và primary flag; work area có origin, không chỉ width/height.
- [High DPI](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows), [GetDpiForWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdpiforwindow): chọn một coordinate boundary, không trộn raw px và WPF DIP.
- [SystemParameters.WorkArea](https://learn.microsoft.com/en-us/dotnet/api/system.windows.systemparameters.workarea?view=windowsdesktop-10.0) có primary work area; [WPF issue 10459](https://github.com/dotnet/wpf/issues/10459) báo cache/live-DPI risk ở .NET Framework, không chứng minh .NET 10 cũng lỗi.
- [DispatcherTimer](https://learn.microsoft.com/en-us/dotnet/api/system.windows.threading.dispatchertimer?view=windowsdesktop-10.0): tick có thể muộn, event có thể giữ object, Stop không đồng nghĩa IDisposable.
- [CompositionTarget.Rendering](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-render-on-a-per-frame-interval-using-compositiontarget): frequency thay đổi. Kế hoạch chọn một DispatcherTimer + monotonic elapsed, không ghép hai loop.

## Tray và publish

- [NotifyIcon](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.notifyicon?view=windowsdesktop-10.0): tray icon, menu, DoubleClick, Dispose có sẵn trong WindowsDesktop.
- [Desktop SDK properties](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props-desktop): UseWPF và UseWindowsForms có thể cùng bật.
- [Deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/): self-contained cần khai báo rõ; RID không tự thay cho --self-contained true. Folder publish không cần runtime cài sẵn, single-file là quyết định khác.

## Rủi ro phải chứng minh trong triển khai

1. Light/lavender/font/control thật trên pinned WPF UI; contrast và keyboard focus.
2. Click xuyên cả phần sprite opaque và transparent tới Notepad process khác, không mất foreground focus.
3. Bounds thực tế ở DPI 100/125/150/200%, work area/taskbar thay đổi.
4. Hide/show/replace/exit không nhân timer/window hoặc giữ frame set cũ.
5. Gói PNG/path độc hại không vượt root, không allocate quá giới hạn; save/import lỗi không công bố dữ liệu dở.
6. Self-contained publish mang đủ asset/notices và chạy ngoài source.
