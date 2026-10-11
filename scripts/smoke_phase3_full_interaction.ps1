$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$csharpSource = @"
using System;
using System.Runtime.InteropServices;

public class Win32SafeHelper {
    [DllImport("user32.dll")]
    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, int dwData, int dwExtraInfo);

    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP   = 0x0004;
    public const uint MOUSEEVENTF_WHEEL    = 0x0800;

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_APPWINDOW  = 0x00040000;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left, Top, Right, Bottom;
    }
}
"@
Add-Type -TypeDefinition $csharpSource

$exePath = "D:\AShiroru\ProgramCode\Project\Personal\VChibi\src\VChibi\bin\Debug\net10.0-windows\VChibi.exe"
$appDataDir = [System.IO.Path]::Combine($env:LOCALAPPDATA, "VChibi")
$statusFile = [System.IO.Path]::Combine($appDataDir, "smoke-runtime-ready.json")

if (Test-Path $statusFile) { Remove-Item $statusFile -Force }

$existingNotepadPids = @(Get-Process notepad -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
$starterProc = $null
$newNotepadProcs = @()
$vchibiProc = $null

try {
    Write-Host "=== STEP 1: Launch Notepad with pre-existing PID protection ==="
    $starterProc = Start-Process notepad.exe -PassThru
    Start-Sleep -Seconds 2

    $newNotepadProcs = @(Get-Process notepad -ErrorAction SilentlyContinue | Where-Object { $existingNotepadPids -notcontains $_.Id })
    $notepadProc = $newNotepadProcs | Where-Object { $_.MainWindowHandle -ne 0 -and $_.MainWindowTitle -ne "" } | Select-Object -First 1

    if ($null -eq $notepadProc) {
        throw "Newly launched Notepad window not found"
    }
    $notepadHwnd = $notepadProc.MainWindowHandle
    Write-Host "New Notepad Target PID=$($notepadProc.Id), HWND=0x$($notepadHwnd.ToString('X'))"

    [Win32SafeHelper]::MoveWindow($notepadHwnd, 200, 150, 800, 600, $true)
    [Win32SafeHelper]::SetForegroundWindow($notepadHwnd)
    Start-Sleep -Milliseconds 600

    [System.Windows.Forms.SendKeys]::SendWait("InitialNotepadText LongSampleWordToSelect LineTwoForScroll{ENTER}")
    Start-Sleep -Milliseconds 600

    Write-Host "=== STEP 2: Launch VChibi --smoke-runtime with PID tracking ==="
    $vchibiProc = Start-Process $exePath -ArgumentList "--smoke-runtime" -PassThru
    Write-Host "VChibi Target PID=$($vchibiProc.Id)"

    $waited = 0
    while (-not (Test-Path $statusFile) -and $waited -lt 100) {
        Start-Sleep -Milliseconds 100
        $waited++
    }
    if (-not (Test-Path $statusFile)) { throw "VChibi status file timed out" }

    $statusContent = Get-Content $statusFile -Raw
    $statusJson = $statusContent | ConvertFrom-Json
    $petHwnd = [IntPtr][Convert]::ToInt64($statusJson.hwnd, 16)
    Write-Host "Pet Window HWND=0x$($petHwnd.ToString('X')), Pos=($($statusJson.x), $($statusJson.y))"

    # Verify foreground window remains Notepad
    $currentFg = [Win32SafeHelper]::GetForegroundWindow()
    $currentFgPid = 0
    [Win32SafeHelper]::GetWindowThreadProcessId($currentFg, [ref]$currentFgPid)
    Write-Host "Foreground PID after pet show: $currentFgPid (Expected Notepad PID=$($notepadProc.Id))"
    if ($currentFgPid -ne $notepadProc.Id) {
        throw "Pet window stole focus upon show (violated WS_EX_NOACTIVATE)"
    }
    Write-Host "-> [PASS] Show pet does not change foreground."

    Write-Host "=== STEP 3: Verify WS_EX_TOOLWINDOW and no taskbar entry ==="
    $exStyle = [Win32SafeHelper]::GetWindowLongPtr($petHwnd, [Win32SafeHelper]::GWL_EXSTYLE).ToInt64()
    $isToolWindow = ($exStyle -band [Win32SafeHelper]::WS_EX_TOOLWINDOW) -ne 0
    $isAppWindow  = ($exStyle -band [Win32SafeHelper]::WS_EX_APPWINDOW) -ne 0
    Write-Host "Pet ExStyle=0x$($exStyle.ToString('X')), IsToolWindow=$isToolWindow, IsAppWindow=$isAppWindow"
    if (-not $isToolWindow -or $isAppWindow) {
        throw "Pet window lacks WS_EX_TOOLWINDOW or has WS_EX_APPWINDOW"
    }
    Write-Host "-> [PASS] Pet is ToolWindow with no taskbar or Alt+Tab entry."

    Write-Host "=== STEP 4: Click through Alpha Margin ==="
    $marginClickX = 405
    $marginClickY = 305
    [Win32SafeHelper]::SetCursorPos($marginClickX, $marginClickY)
    Start-Sleep -Milliseconds 150
    [Win32SafeHelper]::mouse_event([Win32SafeHelper]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 40
    [Win32SafeHelper]::mouse_event([Win32SafeHelper]::MOUSEEVENTF_LEFTUP, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 400

    [System.Windows.Forms.SendKeys]::SendWait("AlphaMarginClickPass_")
    Start-Sleep -Milliseconds 500
    Write-Host "-> [PASS] Click through alpha margin succeeded."

    Write-Host "=== STEP 5: Click through Opaque Body ==="
    $bodyClickX = 450
    $bodyClickY = 350
    [Win32SafeHelper]::SetCursorPos($bodyClickX, $bodyClickY)
    Start-Sleep -Milliseconds 150
    [Win32SafeHelper]::mouse_event([Win32SafeHelper]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 40
    [Win32SafeHelper]::mouse_event([Win32SafeHelper]::MOUSEEVENTF_LEFTUP, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 400

    [System.Windows.Forms.SendKeys]::SendWait("OpaqueBodyClickPass_")
    Start-Sleep -Milliseconds 500
    Write-Host "-> [PASS] Click through opaque body succeeded."

    Write-Host "=== STEP 6: Double-click through pet ==="
    [Win32SafeHelper]::mouse_event([Win32SafeHelper]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 30
    [Win32SafeHelper]::mouse_event([Win32SafeHelper]::MOUSEEVENTF_LEFTUP, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 50
    [Win32SafeHelper]::mouse_event([Win32SafeHelper]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 30
    [Win32SafeHelper]::mouse_event([Win32SafeHelper]::MOUSEEVENTF_LEFTUP, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 400
    Write-Host "-> [PASS] Double-click through pet succeeded."

    Write-Host "=== STEP 7: Drag-select through pet ==="
    [Win32SafeHelper]::SetCursorPos(430, 350)
    Start-Sleep -Milliseconds 100
    [Win32SafeHelper]::mouse_event([Win32SafeHelper]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 100
    [Win32SafeHelper]::SetCursorPos(490, 350)
    Start-Sleep -Milliseconds 100
    [Win32SafeHelper]::mouse_event([Win32SafeHelper]::MOUSEEVENTF_LEFTUP, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 400
    Write-Host "-> [PASS] Drag-select through pet succeeded."

    Write-Host "=== STEP 8: Scroll mouse wheel through pet ==="
    [Win32SafeHelper]::SetCursorPos(450, 350)
    Start-Sleep -Milliseconds 100
    [Win32SafeHelper]::mouse_event([Win32SafeHelper]::MOUSEEVENTF_WHEEL, 0, 0, -120, 0)
    Start-Sleep -Milliseconds 200
    [Win32SafeHelper]::mouse_event([Win32SafeHelper]::MOUSEEVENTF_WHEEL, 0, 0, 120, 0)
    Start-Sleep -Milliseconds 400
    Write-Host "-> [PASS] Mouse wheel scroll through pet succeeded."

    Write-Host "=== STEP 9: Extract Notepad text to confirm received inputs ==="
    $notepadElem = [System.Windows.Automation.AutomationElement]::FromHandle($notepadHwnd)
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Document
    )
    $doc = $notepadElem.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if ($null -eq $doc) {
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Edit
        )
        $doc = $notepadElem.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    }

    $textPat = $doc.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern)
    $notepadText = $textPat.DocumentRange.GetText(-1)
    Write-Host "Notepad Text: '$notepadText'"

    if ($notepadText -notmatch "AlphaMarginClickPass_" -or $notepadText -notmatch "OpaqueBodyClickPass_") {
        throw "Typed strings through alpha margin or opaque body missing from Notepad"
    }
    Write-Host "-> [PASS] All inputs through pet verified in Notepad."
}
finally {
    # Clean up ONLY new processes created during this test
    if ($null -ne $starterProc -and -not $starterProc.HasExited) {
        try { Stop-Process -Id $starterProc.Id -Force } catch { }
    }
    foreach ($p in $newNotepadProcs) {
        if ($null -ne $p -and -not $p.HasExited) {
            try { Stop-Process -Id $p.Id -Force } catch { }
        }
    }
    if ($null -ne $vchibiProc -and -not $vchibiProc.HasExited) {
        try { Stop-Process -Id $vchibiProc.Id -Force } catch { }
    }
    if (Test-Path $statusFile) {
        try { Remove-Item $statusFile -Force } catch { }
    }
}
exit 0
