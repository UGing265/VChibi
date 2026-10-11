using System.Runtime.InteropServices;

namespace VChibi.Platform.Windows;

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct MONITORINFO
{
    public int cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
}

public class WorkAreaInfo
{
    public int Left { get; init; }
    public int Top { get; init; }
    public int Right { get; init; }
    public int Bottom { get; init; }
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public uint Dpi { get; init; } = 96;

    public double DipScale => Dpi / 96.0;

    public int DipToPx(double dip) => (int)Math.Round(dip * DipScale);
    public double PxToDip(int px) => px / DipScale;
}

public static class ScreenWorkspace
{
    private const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
    public static WorkAreaInfo GetPrimaryWorkArea(nint hWnd = default)
    {
        var hMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTOPRIMARY);
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

        if (!GetMonitorInfo(hMonitor, ref mi))
        {
            // Fallback to standard primary screen
            return new WorkAreaInfo
            {
                Left = 0,
                Top = 0,
                Right = (int)System.Windows.SystemParameters.WorkArea.Width,
                Bottom = (int)System.Windows.SystemParameters.WorkArea.Height,
                Dpi = 96
            };
        }

        uint dpi = 96;
        if (hWnd != nint.Zero)
        {
            try
            {
                dpi = GetDpiForWindow(hWnd);
                if (dpi == 0) dpi = 96;
            }
            catch
            {
                dpi = 96;
            }
        }

        return new WorkAreaInfo
        {
            Left = mi.rcWork.Left,
            Top = mi.rcWork.Top,
            Right = mi.rcWork.Right,
            Bottom = mi.rcWork.Bottom,
            Dpi = dpi
        };
    }
}
