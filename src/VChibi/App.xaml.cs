using System.Windows;
using System.Windows.Media;
using VChibi.Services;
using VChibi.ViewModels;
using VChibi.Views;
using VChibi.Runtime;
using VChibi.Platform.Windows;
using Wpf.Ui.Appearance;

namespace VChibi;

public partial class App : System.Windows.Application
{
    private AppPaths? _paths;
    private AppLock? _appLock;
    private PetStore? _petStore;
    private SettingsStore? _settingsStore;
    private DesktopPetSession? _desktopSession;
    private LibraryWindow? _libraryWindow;
    private TrayIcon? _trayIcon;
    public DesktopPetSession? DesktopSession => _desktopSession;
    public AppPaths Paths => _paths!;
    public PetStore PetStore => _petStore!;
    public SettingsStore SettingsStore => _settingsStore!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        string? customDataRoot = Environment.GetEnvironmentVariable("VCHIBI_DATA_DIR");
        for (int i = 0; i < e.Args.Length; i++)
        {
            if (e.Args[i] == "--data-dir" && i + 1 < e.Args.Length)
            {
                customDataRoot = e.Args[i + 1];
                break;
            }
        }

        _paths = new AppPaths(customDataRoot);
        _appLock = new AppLock(_paths.LockFilePath);

        if (!_appLock.TryAcquire())
        {
            System.Windows.MessageBox.Show(
                "VChibi đang chạy trên hệ thống (có thể đang ở khay hệ thống).",
                "VChibi",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            Shutdown();
            return;
        }

        // Initialize store and seed default pet if needed
        _petStore = new PetStore(_paths, _appLock);
        _petStore.SeedDefaultPetIfNeeded();

        _settingsStore = new SettingsStore(_paths.SettingsPath);
        var (settings, loadResult, error) = _settingsStore.Load();

        // Apply lavender accent #7862B8
        var lavender = Color.FromRgb(0x78, 0x62, 0xB8);
        ApplicationAccentColorManager.Apply(lavender, ApplicationTheme.Light, false);
        // Initialize desktop session
        _desktopSession = new DesktopPetSession();

        if (e.Args.Contains("--probe-corners"))
        {
            var pet = _petStore.GetPet("default-chibi") ?? _petStore.GetAllPets().FirstOrDefault();
            if (pet != null) _desktopSession.ActivatePet(pet);
            RunCornerProbeAndExit();
            return;
        }

        if (e.Args.Contains("--smoke-runtime"))
        {
            var pet = _petStore.GetPet("default-chibi") ?? _petStore.GetAllPets().FirstOrDefault();
            if (pet != null) _desktopSession.ActivatePet(pet);
            _desktopSession.Show();
            _desktopSession.SetPositionForTest(400, 300);
            var statusFile = System.IO.Path.Combine(_paths.DataRoot, "smoke-runtime-ready.json");
            System.IO.File.WriteAllText(statusFile, $"{{\"hwnd\":\"0x{_desktopSession.Window.Handle:X}\",\"x\":400,\"y\":300,\"width\":{_desktopSession.Window.Width},\"height\":{_desktopSession.Window.Height}}}");

            var liveStatusFile = System.IO.Path.Combine(_paths.DataRoot, "smoke-runtime-status.json");
            var statusTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            statusTimer.Tick += (_, _) =>
            {
                try
                {
                    var status = new
                    {
                        hwnd = $"0x{_desktopSession.Window.Handle:X}",
                        x = _desktopSession.Controller.CurrentX,
                        y = _desktopSession.Controller.CurrentY,
                        action = _desktopSession.Controller.ActionName,
                        facingLeft = _desktopSession.Controller.FacingLeft,
                        flipScaleX = _desktopSession.Window.FlipScaleX
                    };
                    var json = System.Text.Json.JsonSerializer.Serialize(status);
                    System.IO.File.WriteAllText(liveStatusFile, json);
                }
                catch { }
            };
            statusTimer.Start();
            return;
        }

        // Normal startup:
        var viewModel = new LibraryViewModel(_petStore, _settingsStore, _desktopSession, new WindowsFilePickerService());
        viewModel.Initialize();
        _libraryWindow = new LibraryWindow(viewModel);

        // Tray icon setup
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        var icon = System.IO.File.Exists(iconPath) ? new System.Drawing.Icon(iconPath) : System.Drawing.SystemIcons.Application;
        _trayIcon = new TrayIcon(icon, "VChibi — Desktop Idol Pet");
        _trayIcon.OpenLibraryRequested += () => Dispatcher.InvokeAsync(() => _libraryWindow.ShowAndActivate());
        _trayIcon.TogglePetRequested += () => Dispatcher.InvokeAsync(() => viewModel.ToggleDesktopPetCommand.Execute(null));
        _trayIcon.ExitRequested += () => Dispatcher.InvokeAsync(() => viewModel.ExitCommand.Execute(null));
        _trayIcon.IsPetVisibleQuery = () => _desktopSession.IsVisible;

        viewModel.RequestShutdown += ShutdownApp;

        _libraryWindow.Show();
    }

    private void ShutdownApp()
    {
        _libraryWindow?.AllowClose();
        _libraryWindow?.Close();

        _trayIcon?.Dispose();
        _desktopSession?.Stop();
        _appLock?.Dispose();

        Shutdown(0);
    }

    private void RunCornerProbeAndExit()
    {
        if (_desktopSession == null)
        {
            Shutdown(1);
            return;
        }

        _desktopSession.Show();
        var hwnd = _desktopSession.Window.Handle;
        var workArea = ScreenWorkspace.GetPrimaryWorkArea(hwnd);

        int minX = _desktopSession.Controller.Movement.MinX;
        int maxX = _desktopSession.Controller.Movement.MaxX;
        int minY = _desktopSession.Controller.Movement.MinY;
        int maxY = _desktopSession.Controller.Movement.MaxY;

        var corners = new (string Name, int X, int Y)[]
        {
            ("TopLeft", minX, minY),
            ("TopRight", maxX, minY),
            ("BottomLeft", minX, maxY),
            ("BottomRight", maxX, maxY)
        };

        var results = new System.Collections.Generic.List<object>();
        bool allCornersInside = true;

        foreach (var (name, targetX, targetY) in corners)
        {
            _desktopSession.SetPositionForTest(targetX, targetY);
            Platform.Windows.RECT rect;
            PetWindowInterop.GetWindowRect(hwnd, out rect);

            bool insideLeft = rect.Left >= workArea.Left;
            bool insideTop = rect.Top >= workArea.Top;
            bool insideRight = rect.Right <= workArea.Right;
            bool insideBottom = rect.Bottom <= workArea.Bottom;
            bool isFullyInside = insideLeft && insideTop && insideRight && insideBottom;
            if (!isFullyInside) allCornersInside = false;
            results.Add(new
            {
                Corner = name,
                TargetX = targetX,
                TargetY = targetY,
                ActualLeft = rect.Left,
                ActualTop = rect.Top,
                ActualRight = rect.Right,
                ActualBottom = rect.Bottom,
                PhysicalWidth = rect.Width,
                PhysicalHeight = rect.Height,
                IsFullyInside = isFullyInside
            });
        }

        var probeData = new
        {
            WorkArea = new
            {
                workArea.Left,
                workArea.Top,
                workArea.Right,
                workArea.Bottom,
                workArea.Width,
                workArea.Height,
                workArea.Dpi,
                workArea.DipScale
            },
            WindowDipWidth = _desktopSession.Window.Width,
            WindowDipHeight = _desktopSession.Window.Height,
            Corners = results
        };

        var json = System.Text.Json.JsonSerializer.Serialize(probeData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var dataRoot = _paths?.DataRoot ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VChibi");
        var resultPath = System.IO.Path.Combine(dataRoot, "corner-probe-results.json");
        System.IO.File.WriteAllText(resultPath, json);
        if (!allCornersInside)
        {
            Shutdown(1);
            return;
        }

        Shutdown(0);
    }


    protected override void OnExit(ExitEventArgs e)
    {
        _desktopSession?.Stop();
        _appLock?.Dispose();
        base.OnExit(e);
    }
}
