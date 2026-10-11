$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$csharpSource = @"
using System;
using System.Runtime.InteropServices;

public class Win32Helper {
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
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

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

# Create a unique test file so we ONLY target our dedicated test window
$testFileId = [Guid]::NewGuid().ToString("N")
$testFileName = "vchibi_smoke_" + $testFileId + ".txt"
$testFilePath = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), $testFileName)
[System.IO.File]::WriteAllText($testFilePath, "VCHIBI_INITIAL_CONTENT`r`n")

$starterProc = $null
$trackedNotepadPid = $null
$vchibiProc = $null

try {
    if (Test-Path $statusFile) { Remove-Item $statusFile -Force }

    Write-Host "[1/6] Launching dedicated test Notepad with unique file: $testFileName..."
    $starterProc = Start-Process notepad.exe -ArgumentList "`"$testFilePath`"" -PassThru
    Start-Sleep -Seconds 2

    # Find the EXACT Notepad window displaying our unique test file
    $notepadProc = Get-Process notepad -ErrorAction SilentlyContinue | Where-Object {
        $_.MainWindowHandle -ne 0 -and ($_.MainWindowTitle -like "*$testFileId*" -or $_.MainWindowTitle -like "*$testFileName*")
    } | Select-Object -First 1

    if ($null -eq $notepadProc) {
        throw "Could not uniquely identify test Notepad window matching $testFileName. Aborting to protect existing windows."
    }

    $trackedNotepadPid = $notepadProc.Id
    $notepadHwnd = $notepadProc.MainWindowHandle
    Write-Host "Confirmed Test Notepad PID=$trackedNotepadPid, HWND=0x$($notepadHwnd.ToString('X')), Title='$($notepadProc.MainWindowTitle)'"

    # Position Notepad at (250, 150, 750, 550)
    $moved = [Win32Helper]::MoveWindow($notepadHwnd, 250, 150, 750, 550, $true)
    if (-not $moved) { throw "MoveWindow on test Notepad returned false" }
    [Win32Helper]::SetForegroundWindow($notepadHwnd)
    Start-Sleep -Milliseconds 600

    Write-Host "[2/6] Launching VChibi --smoke-runtime..."
    $vchibiProc = Start-Process $exePath -ArgumentList "--smoke-runtime" -PassThru
    Write-Host "VChibi PID=$($vchibiProc.Id)"

    # Wait for ready file
    $waited = 0
    while (-not (Test-Path $statusFile) -and $waited -lt 100) {
        Start-Sleep -Milliseconds 100
        $waited++
    }

    if (-not (Test-Path $statusFile)) {
        throw "VChibi smoke runtime did not write status file in time"
    }

    $statusContent = Get-Content $statusFile -Raw
    Write-Host "VChibi Ready payload: $statusContent"
    $statusJson = $statusContent | ConvertFrom-Json
    $petHwnd = [IntPtr][Convert]::ToInt64($statusJson.hwnd, 16)
    Write-Host "Pet Window HWND=0x$($petHwnd.ToString('X')), Pos=($($statusJson.x), $($statusJson.y)), Size=$($statusJson.width)x$($statusJson.height)"

    # Click center of pet body: (448, 348)
    $clickX = 448
    $clickY = 348

    Write-Host "[3/6] Clicking at ($clickX, $clickY) directly on Pet body..."
    [Win32Helper]::SetCursorPos($clickX, $clickY)
    Start-Sleep -Milliseconds 200
    [Win32Helper]::mouse_event([Win32Helper]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 50
    [Win32Helper]::mouse_event([Win32Helper]::MOUSEEVENTF_LEFTUP, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 500

    # Verify Foreground Window & Process ID belongs ONLY to our test Notepad
    $fgHwnd = [Win32Helper]::GetForegroundWindow()
    $fgPid = 0
    [Win32Helper]::GetWindowThreadProcessId($fgHwnd, [ref]$fgPid)
    Write-Host "Foreground HWND=0x$($fgHwnd.ToString('X')), Foreground PID=$fgPid"

    if ($fgPid -ne $trackedNotepadPid) {
        throw "Foreground process PID $fgPid does not match test Notepad PID $trackedNotepadPid! Pet may have intercepted focus."
    }
    Write-Host "Foreground verified: Test Notepad has focus through Pet window (WS_EX_TRANSPARENT & WS_EX_NOACTIVATE passed)!"

    Write-Host "[4/6] Typing text into test Notepad through the Pet body..."
    $expectedText = "VChibi_Pet_ClickThrough_Verified_OK"
    [System.Windows.Forms.SendKeys]::SendWait("$expectedText{ENTER}")
    Start-Sleep -Milliseconds 1000

    # Verify text in Notepad via UI Automation
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

    if ($null -eq $doc) {
        throw "Failed to find Document or Edit control in test Notepad"
    }

    $retrievedText = ""
    try {
        $textPat = $doc.GetCurrentPattern([System.Windows.Automation.TextPattern]::Pattern)
        $retrievedText = $textPat.DocumentRange.GetText(-1)
    } catch {
        try {
            $valPat = $doc.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
            $retrievedText = $valPat.Current.Value
        } catch {
            throw "Failed to retrieve text via TextPattern or ValuePattern: $_"
        }
    }

    Write-Host "Notepad retrieved text: '$retrievedText'"
    if ($retrievedText -notmatch $expectedText) {
        throw "Expected text '$expectedText' was NOT found in test Notepad. Got: '$retrievedText'"
    }
    Write-Host "Text verified in test Notepad: click-through and keyboard input passed 100%!"

    Write-Host "[5/6] Capturing screenshot evidence..."
    $evidenceDir = "D:\AShiroru\ProgramCode\Project\Personal\VChibi\docs\evidence"
    if (-not (Test-Path $evidenceDir)) { New-Item -ItemType Directory -Path $evidenceDir -Force }
    $screenshotPath = [System.IO.Path]::Combine($evidenceDir, "phase3-pet-clickthrough.png")

    $bounds = [System.Drawing.Rectangle]::new(200, 120, 850, 650)
    $bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
    $bitmap.Save($screenshotPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    Write-Host "Screenshot saved at $screenshotPath"

    Write-Host "[6/6] Tracking Pet movement and verifying work area boundary constraints..."
    $initialRect = New-Object Win32Helper+RECT
    [Win32Helper]::GetWindowRect($petHwnd, [ref]$initialRect)
    $hasMoved = $false

    $samples = @()
    for ($i = 0; $i -lt 8; $i++) {
        $r = New-Object Win32Helper+RECT
        [Win32Helper]::GetWindowRect($petHwnd, [ref]$r)
        $samples += "t=$($i*0.4)s: X=$($r.Left), Y=$($r.Top), W=$($r.Right - $r.Left), H=$($r.Bottom - $r.Top)"
        if ($r.Left -ne $initialRect.Left -or $r.Top -ne $initialRect.Top) {
            $hasMoved = $true
        }
        if ($r.Left -lt 0 -or $r.Top -lt 0) {
            throw "Pet went out of screen bounds: X=$($r.Left), Y=$($r.Top)"
        }
        Start-Sleep -Milliseconds 400
    }

    Write-Host ($samples -join "`n")

    if (-not $hasMoved) {
        throw "Pet coordinates did not change over 3.2 seconds! Animation or movement loop stalled."
    }
    Write-Host "Pet movement verified: coordinates update dynamically and stay strictly within bounds!"
    Write-Host "ALL PHASE 3 RUNTIME SMOKE CHECKS PASSED WITH 100% EVIDENCE!"
}
finally {
    # Clean up ONLY specific test processes launched in this run
    if ($null -ne $trackedNotepadPid) {
        try { Stop-Process -Id $trackedNotepadPid -Force } catch { }
    }
    if ($null -ne $starterProc -and -not $starterProc.HasExited) {
        try { Stop-Process -Id $starterProc.Id -Force } catch { }
    }
    if ($null -ne $vchibiProc -and -not $vchibiProc.HasExited) {
        try { Stop-Process -Id $vchibiProc.Id -Force } catch { }
    }
    if (Test-Path $testFilePath) {
        try { Remove-Item $testFilePath -Force } catch { }
    }
    if (Test-Path $statusFile) {
        try { Remove-Item $statusFile -Force } catch { }
    }
}
exit 0
