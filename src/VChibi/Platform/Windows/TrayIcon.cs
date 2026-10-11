using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace VChibi.Platform.Windows;

public class TrayIcon : IDisposable
{
    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 101;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP     = 0x0205;
    private const uint NIM_ADD    = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON    = 0x00000002;
    private const uint NIF_TIP     = 0x00000004;

    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD   = 0x0100;

    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;

    private const int CMD_OPEN_LIBRARY   = 1001;
    private const int CMD_TOGGLE_PET     = 1002;
    private const int CMD_EXIT           = 1003;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(nint hMenu, uint uFlags, nuint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(nint hMenu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(nint hMenu, uint uFlags, int x, int y, nint hwnd, nint lptpm);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private readonly HwndSource _msgSource;
    private readonly nint _hIcon;
    private bool _isAdded = false;

    public event Action? OpenLibraryRequested;
    public event Action? TogglePetRequested;
    public event Action? ExitRequested;

    public Func<bool>? IsPetVisibleQuery { get; set; }

    public TrayIcon(Icon icon, string tooltip = "VChibi — Desktop Idol Pet")
    {
        _hIcon = icon.Handle;

        var parameters = new HwndSourceParameters("VChibi_TrayMessageWindow")
        {
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
            ParentWindow = nint.Zero
        };

        _msgSource = new HwndSource(parameters);
        _msgSource.AddHook(WndProc);

        var nid = CreateNotifyIconData(tooltip);
        _isAdded = Shell_NotifyIcon(NIM_ADD, ref nid);
    }

    private NOTIFYICONDATA CreateNotifyIconData(string tooltip)
    {
        return new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _msgSource.Handle,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip
        };
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            int mouseMsg = (int)lParam.ToInt64();
            if (mouseMsg == WM_LBUTTONDBLCLK)
            {
                OpenLibraryRequested?.Invoke();
                handled = true;
            }
            else if (mouseMsg == WM_RBUTTONUP)
            {
                ShowContextMenu();
                handled = true;
            }
        }
        return nint.Zero;
    }
    private void ShowContextMenu()
    {
        var hMenu = CreatePopupMenu();
        if (hMenu == nint.Zero) return;

        try
        {
            AppendMenu(hMenu, MF_STRING, CMD_OPEN_LIBRARY, "Mở quản lý");

            bool petVisible = IsPetVisibleQuery?.Invoke() ?? true;
            string toggleText = petVisible ? "Ẩn khỏi desktop" : "Hiện lên desktop";
            AppendMenu(hMenu, MF_STRING, CMD_TOGGLE_PET, toggleText);

            AppendMenu(hMenu, MF_SEPARATOR, 0, null);
            AppendMenu(hMenu, MF_STRING, CMD_EXIT, "Thoát");

            GetCursorPos(out var pt);
            SetForegroundWindow(_msgSource.Handle);

            int cmd = TrackPopupMenuEx(hMenu, TPM_RIGHTBUTTON | TPM_RETURNCMD, pt.X, pt.Y, _msgSource.Handle, nint.Zero);

            switch (cmd)
            {
                case CMD_OPEN_LIBRARY:
                    OpenLibraryRequested?.Invoke();
                    break;
                case CMD_TOGGLE_PET:
                    TogglePetRequested?.Invoke();
                    break;
                case CMD_EXIT:
                    ExitRequested?.Invoke();
                    break;
            }
        }
        finally
        {
            DestroyMenu(hMenu);
        }
    }

    public void Dispose()
    {
        if (_isAdded)
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _msgSource.Handle,
                uID = 1
            };
            Shell_NotifyIcon(NIM_DELETE, ref nid);
            _isAdded = false;
        }

        _msgSource.RemoveHook(WndProc);
        _msgSource.Dispose();
        GC.SuppressFinalize(this);
    }
}
