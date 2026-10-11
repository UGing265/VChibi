$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$csharpSource = @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class Win32Helper {
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);
    private delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

    public static System.Collections.Generic.List<IntPtr> GetThreadWindows(uint threadId) {
        var list = new System.Collections.Generic.List<IntPtr>();
        EnumThreadWindows(threadId, (h, l) => { list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }

    public static string GetWindowTextStr(IntPtr hWnd) {
        var sb = new StringBuilder(256);
        GetWindowText(hWnd, sb, 256);
        return sb.ToString();
    }

    public static string GetClassNameStr(IntPtr hWnd) {
        var sb = new StringBuilder(256);
        GetClassName(hWnd, sb, 256);
        return sb.ToString();
    }

    public const int GWL_EXSTYLE       = -20;
    public const int WS_EX_TOOLWINDOW  = 0x00000080;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_NOACTIVATE  = 0x08000000;

    public const uint WM_CLOSE    = 0x0010;
    public const uint WM_USER     = 0x0400;
    public const uint WM_TRAYICON = WM_USER + 101;
    public const int WM_LBUTTONDBLCLK = 0x0203;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left, Top, Right, Bottom;
    }
}
"@
Add-Type -TypeDefinition $csharpSource

function New-TestPetPackage($dir, $id, $name) {
    New-Item -ItemType Directory -Path (Join-Path $dir "idle") -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $dir "walk") -Force | Out-Null

    $bmp = New-Object System.Drawing.Bitmap 16, 16
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(255, 100, 180, 240))
    $bmp.Save((Join-Path $dir "idle\001.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Save((Join-Path $dir "idle\002.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Save((Join-Path $dir "walk\001.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Save((Join-Path $dir "walk\002.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()

    $manifest = @{
        schemaVersion = 1
        id = $id
        name = $name
        animations = @{
            idle = @{ fps = 8; frames = @("idle/001.png", "idle/002.png") }
            walk = @{ fps = 8; frames = @("walk/001.png", "walk/002.png") }
        }
    }
    $json = $manifest | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText((Join-Path $dir "pet.json"), $json)
}

Write-Host "=========================================================="
Write-Host "VChibi Phase 4: Full Multi-Pet Lifecycle Smoke Test"
Write-Host "=========================================================="

# 1. Pre-flight check: NEVER kill pre-existing instances
$existingVChibi = @(Get-Process VChibi -ErrorAction SilentlyContinue)
if ($existingVChibi.Count -gt 0) {
    $pids = ($existingVChibi | Select-Object -ExpandProperty Id) -join ", "
    Write-Error "A pre-existing VChibi process (PID: $pids) is already running. Aborting safely."
    exit 1
}

$exePath = "D:\AShiroru\ProgramCode\Project\Personal\VChibi\src\VChibi\bin\Debug\net10.0-windows\VChibi.exe"

# Use isolated temporary test data directory so user's real %LOCALAPPDATA%\VChibi is never touched
$testDataDir = Join-Path ([System.IO.Path]::GetTempPath()) ("vchibi_smoke_p4_" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testDataDir -Force | Out-Null
Write-Host "Using isolated test data directory: $testDataDir"

# Seed two pet packages in Pets folder:
$petsDir = Join-Path $testDataDir "Pets"
New-Item -ItemType Directory -Path $petsDir -Force | Out-Null

 $defaultPetDest = Join-Path $petsDir "default-chibi"
 Copy-Item -Path "D:\AShiroru\ProgramCode\Project\Personal\VChibi\src\VChibi\bin\Debug\net10.0-windows\Assets\DefaultPet" -Destination $defaultPetDest -Recurse

 $secondPetDest = Join-Path $petsDir "pet-second-id"
 New-TestPetPackage $secondPetDest "pet-second-id" "Second Chibi"

Write-Host "Seeded 2 pet packages: 'Chibi Idol' and 'Second Chibi'"

$lockFile = Join-Path $testDataDir ".instance.lock"
$settingsFile = Join-Path $testDataDir "settings.json"

$p = $null
$p2 = $null
$pReopen = $null

try {
    # -------------------------------------------------------------------------
    # STEP 1: Launch normal VChibi instance
    # -------------------------------------------------------------------------
    Write-Host "`n[1/10] Launching VChibi instance 1 with 2 seeded pets..."
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exePath
    $psi.Arguments = "--data-dir `"$testDataDir`""
    $psi.UseShellExecute = $false
    $p = [System.Diagnostics.Process]::Start($psi)

    if ($null -eq $p) {
        Write-Error "Failed to launch VChibi process."
        exit 1
    }
    Write-Host "Instance 1 started with PID: $($p.Id)"

    Start-Sleep -Seconds 2

    $rootElement = [System.Windows.Automation.AutomationElement]::RootElement
    $processCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $p.Id
    )
    $appElements = $rootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $processCondition)
    Write-Host "Found $($appElements.Count) top-level UI elements for PID $($p.Id)"

    $libraryElement = $null
    $petElement = $null
    $libraryHwnd = [IntPtr]::Zero
    $petHwnd = [IntPtr]::Zero

    foreach ($el in $appElements) {
        $name = $el.Current.Name
        $class = $el.Current.ClassName
        Write-Host " - UI Element: Name='$name', Class='$class', Handle=0x$($el.Current.NativeWindowHandle.ToString('X'))"
        if ($name -eq "VChibi") {
            $libraryElement = $el
            $libraryHwnd = [IntPtr]$el.Current.NativeWindowHandle
        } elseif ($name -eq "VChibi Desktop Pet" -or $name -eq "") {
            $petElement = $el
            $petHwnd = [IntPtr]$el.Current.NativeWindowHandle
        }
    }

    if ($libraryHwnd -eq [IntPtr]::Zero) {
        Write-Error "Could not find LibraryWindow HWND for VChibi process."
        exit 1
    }
    Write-Host "LibraryWindow HWND: 0x$($libraryHwnd.ToString('X')) (Visible: $([Win32Helper]::IsWindowVisible($libraryHwnd)))"

    $trayMsgHwnd = [IntPtr]::Zero
    $p.Refresh()
    foreach ($thread in $p.Threads) {
        $hwnds = [Win32Helper]::GetThreadWindows($thread.Id)
        foreach ($h in $hwnds) {
            $txt = [Win32Helper]::GetWindowTextStr($h)
            if ($txt -eq "VChibi_TrayMessageWindow") {
                $trayMsgHwnd = $h
            }
        }
    }
    Write-Host "VChibi_TrayMessageWindow HWND: 0x$($trayMsgHwnd.ToString('X'))"
    if ($trayMsgHwnd -eq [IntPtr]::Zero) {
        Write-Error "Could not find VChibi_TrayMessageWindow!"
        exit 1
    }

    if ($petHwnd -ne [IntPtr]::Zero) {
        $exStyle = [Win32Helper]::GetWindowLongPtr($petHwnd, [Win32Helper]::GWL_EXSTYLE).ToInt64()
        $isToolWindow = ($exStyle -band [Win32Helper]::WS_EX_TOOLWINDOW) -ne 0
        $isTransparent = ($exStyle -band [Win32Helper]::WS_EX_TRANSPARENT) -ne 0
        $isNoActivate = ($exStyle -band [Win32Helper]::WS_EX_NOACTIVATE) -ne 0
        Write-Host "PetWindow HWND: 0x$($petHwnd.ToString('X')) (Visible: $([Win32Helper]::IsWindowVisible($petHwnd)))"
        Write-Host "PetWindow Styles -> WS_EX_TOOLWINDOW: $isToolWindow, WS_EX_TRANSPARENT: $isTransparent, WS_EX_NOACTIVATE: $isNoActivate"

        if (-not $isToolWindow -or -not $isTransparent -or -not $isNoActivate) {
            Write-Error "PetWindow is missing required Win32 extended styles."
            exit 1
        }
    }

    # Verify .instance.lock is held
    if (-not (Test-Path $lockFile)) {
        Write-Error ".instance.lock file does not exist!"
        exit 1
    }
    Write-Host "Verified: .instance.lock is present on disk."

    # -------------------------------------------------------------------------
    # STEP 2: Live Multi-Pet Selection Independence & Per-ID Settings (A01, A04, A06, A15)
    # -------------------------------------------------------------------------
    Write-Host "`n[2/10] Testing live multi-pet selection vs active independence & per-ID settings..."
    $rectInitial = New-Object Win32Helper+RECT
    [Win32Helper]::GetWindowRect($petHwnd, [ref]$rectInitial) | Out-Null
    $initialWidth = $rectInitial.Right - $rectInitial.Left
    $initialHeight = $rectInitial.Bottom - $rectInitial.Top
    Write-Host "Initial PetWindow size at 100% scale: ${initialWidth}x${initialHeight}px"

    # Find pet ListBox items
    $petListItems = $libraryElement.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::ListItem
        ))
    )
    Write-Host "Found $($petListItems.Count) pets in Library ListBox (Expected: 2)"
    if ($petListItems.Count -lt 2) {
        Write-Error "Expected at least 2 pet items in Library list!"
        exit 1
    }

    $item1 = $petListItems[0]
    $item2 = $petListItems[1]

    # Find Scale & Speed sliders
    $scaleSlider = $libraryElement.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
            "ScaleSlider"
        ))
    )
    $speedSlider = $libraryElement.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
            "SpeedSlider"
        ))
    )
    $scaleRangePattern = $scaleSlider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)
    $speedRangePattern = $speedSlider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern)

    # 1. Modify Pet 1 (Chibi Idol) Scale -> 200%, Speed -> 100
    Write-Host "Setting Pet 1 Scale to 200% and Speed to 100 DIP/s..."
    $scaleRangePattern.SetValue(200.0)
    $speedRangePattern.SetValue(100.0)
    Start-Sleep -Milliseconds 500

    $rectScaled = New-Object Win32Helper+RECT
    [Win32Helper]::GetWindowRect($petHwnd, [ref]$rectScaled) | Out-Null
    $scaledWidth = $rectScaled.Right - $rectScaled.Left
    Write-Host "PetWindow size after Pet 1 scaled to 200%: ${scaledWidth}px (Scaled up: $($scaledWidth -gt $initialWidth))"
    if ($scaledWidth -le $initialWidth) {
        Write-Error "PetWindow width did not increase when Scale was adjusted to 200%!"
        exit 1
    }

    # 2. Select Pet 2 ("Second Chibi") in ListBox
    Write-Host "Selecting Pet 2 ('Second Chibi') in ListBox..."
    $selPattern2 = $item2.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $selPattern2.Select()
    Start-Sleep -Milliseconds 500

    # SELECTION INDEPENDENCE ASSERTION:
    # Desktop pet MUST STILL be Pet 1 (at 200% scale)!
    $rectDuringSel2 = New-Object Win32Helper+RECT
    [Win32Helper]::GetWindowRect($petHwnd, [ref]$rectDuringSel2) | Out-Null
    $widthDuringSel2 = $rectDuringSel2.Right - $rectDuringSel2.Left
    Write-Host "PetWindow width while Pet 2 selected in UI: ${widthDuringSel2}px (Expected: $scaledWidth px)"
    if ($widthDuringSel2 -ne $scaledWidth) {
        Write-Error "Selecting Pet 2 modified active desktop pet size without Release command!"
        exit 1
    }
    Write-Host "Verified Selection Independence: Selecting Pet 2 did NOT change active desktop pet."

    # Pet 2 ScaleSlider should show default 100%
    Write-Host "Pet 2 current ScaleSlider value: $($scaleRangePattern.Current.Value)%"
    # Set Pet 2 Scale to 150%, Speed to 60 DIP/s
    Write-Host "Setting Pet 2 Scale to 150% and Speed to 60 DIP/s..."
    $scaleRangePattern.SetValue(150.0)
    $speedRangePattern.SetValue(60.0)
    Start-Sleep -Milliseconds 500

    # 3. Switch back to Pet 1: Sliders MUST restore Pet 1 settings (200%, 100)
    Write-Host "Switching selection back to Pet 1..."
    $selPattern1 = $item1.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $selPattern1.Select()
    Start-Sleep -Milliseconds 500
    Write-Host "Pet 1 restored ScaleSlider value: $($scaleRangePattern.Current.Value)% (Expected: 200)"
    Write-Host "Pet 1 restored SpeedSlider value: $($speedRangePattern.Current.Value) DIP/s (Expected: 100)"
    if ($scaleRangePattern.Current.Value -ne 200.0 -or $speedRangePattern.Current.Value -ne 100.0) {
        Write-Error "Per-ID settings failed: Pet 1 settings not restored upon re-selection!"
        exit 1
    }

    # 4. Switch back to Pet 2 and Activate Pet 2 onto Desktop
    Write-Host "Switching selection back to Pet 2 and activating to desktop..."
    $selPattern2.Select()
    Start-Sleep -Milliseconds 500

    $releaseBtn = $libraryElement.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
            "ReleaseToDesktopButton"
        ))
    )
    $releaseBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 800

    # PetWindow should now reflect Pet 2's physical scale:
    # Pet 2 has native sprite dimensions 16x16px.
    # At 120 DPI (1.25x scale) and 150% user scale: 16 * 1.5 * 1.25 = 30px width, 30px height!
    $rectPet2Active = New-Object Win32Helper+RECT
    [Win32Helper]::GetWindowRect($petHwnd, [ref]$rectPet2Active) | Out-Null
    $widthPet2Active = $rectPet2Active.Right - $rectPet2Active.Left
    $heightPet2Active = $rectPet2Active.Bottom - $rectPet2Active.Top
    $expectedPet2Px = [int][Math]::Round(16 * 1.5 * 1.25)
    Write-Host "PetWindow size after Pet 2 activated: ${widthPet2Active}x${heightPet2Active}px (Expected: ${expectedPet2Px}x${expectedPet2Px}px)"
    if ($widthPet2Active -ne $expectedPet2Px -or $heightPet2Active -ne $expectedPet2Px) {
        Write-Error "PetWindow did not scale to exact Pet 2 physical dimensions (${expectedPet2Px}x${expectedPet2Px}px)! Actual: ${widthPet2Active}x${heightPet2Active}px"
        exit 1
    }
    Write-Host "Verified Per-ID Activation: Pet 2 is active on desktop scaled to exact 150% footprint (${expectedPet2Px}x${expectedPet2Px}px)."
    # -------------------------------------------------------------------------
    # STEP 3: Hide and Show desktop pet commands (Requirements A01, A04)
    # -------------------------------------------------------------------------
    Write-Host "`n[3/10] Testing Hide and Show desktop pet commands..."
    $hideBtn = $libraryElement.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
            "HideFromDesktopButton"
        ))
    )
    $hideBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 600

    $petVisibleAfterHide = [Win32Helper]::IsWindowVisible($petHwnd)
    Write-Host "PetWindow visibility after Hide command: $petVisibleAfterHide (Expected: False)"
    if ($petVisibleAfterHide) {
        Write-Error "PetWindow failed to hide after HideFromDesktop command!"
        exit 1
    }

    $releaseBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 600

    $petVisibleAfterRelease = [Win32Helper]::IsWindowVisible($petHwnd)
    Write-Host "PetWindow visibility after Release command: $petVisibleAfterRelease (Expected: True)"
    if (-not $petVisibleAfterRelease) {
        Write-Error "PetWindow failed to show after ReleaseToDesktop command!"
        exit 1
    }
    Write-Host "Verified: PetWindow toggled hidden and visible cleanly."

    # -------------------------------------------------------------------------
    # STEP 4: Second instance lock test (Review fix F1 / Requirement A13)
    # -------------------------------------------------------------------------
    Write-Host "`n[4/10] Testing second instance lock rejection..."
    $psi2 = New-Object System.Diagnostics.ProcessStartInfo
    $psi2.FileName = $exePath
    $psi2.Arguments = "--data-dir `"$testDataDir`" --probe-corners"
    $psi2.UseShellExecute = $false
    $p2 = [System.Diagnostics.Process]::Start($psi2)

    # Instance 2 encounters locked file and displays modal MessageBox
    Start-Sleep -Seconds 1
    $dialogDismissed = $false
    for ($i = 0; $i -lt 10; $i++) {
        $p2ProcCond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
            $p2.Id
        )
        $p2Windows = $rootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $p2ProcCond)
        foreach ($w in $p2Windows) {
            Write-Host "Found Instance 2 Dialog: Handle=0x$($w.Current.NativeWindowHandle.ToString('X')), Class='$($w.Current.ClassName)'"
            $dlgHwnd = [IntPtr]$w.Current.NativeWindowHandle
            [Win32Helper]::PostMessage($dlgHwnd, [Win32Helper]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
            $dialogDismissed = $true
            break
        }
        if ($dialogDismissed) { break }
        Start-Sleep -Milliseconds 500
    }

    $exitedCleanly = $p2.WaitForExit(6000)
    Write-Host "Instance 2 exit status: Exited=$exitedCleanly, ExitCode=$($p2.ExitCode)"
    if (-not $exitedCleanly) {
        try { Stop-Process -Id $p2.Id -Force } catch { }
        Write-Error "Instance 2 did not reject lock and exit within timeout!"
        exit 1
    }

    # Verify Instance 1 is still alive and running
    $p.Refresh()
    if ($p.HasExited) {
        Write-Error "Instance 1 died unexpectedly after Instance 2 was launched!"
        exit 1
    }
    Write-Host "Verified: Instance 1 continues running undisturbed."

    # -------------------------------------------------------------------------
    # STEP 5: Close LibraryWindow to Hide (Review fix: close cancels and hides)
    # -------------------------------------------------------------------------
    Write-Host "`n[5/10] Testing LibraryWindow close-to-hide behavior..."
    [Win32Helper]::PostMessage($libraryHwnd, [Win32Helper]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Seconds 1

    $libVisible = [Win32Helper]::IsWindowVisible($libraryHwnd)
    Write-Host "LibraryWindow visibility after WM_CLOSE: $libVisible (Expected: False)"
    if ($libVisible) {
        Write-Error "LibraryWindow failed to hide on close!"
        exit 1
    }

    if ($petHwnd -ne [IntPtr]::Zero) {
        $petVisible = [Win32Helper]::IsWindowVisible($petHwnd)
        Write-Host "PetWindow visibility while Library is hidden: $petVisible (Expected: True)"
        if (-not $petVisible) {
            Write-Error "PetWindow was hidden when LibraryWindow was closed!"
            exit 1
        }
    }
    Write-Host "Verified: LibraryWindow hid cleanly, PetWindow remained active."

    # -------------------------------------------------------------------------
    # STEP 6: Restore LibraryWindow via Tray Double-Click message (WM_TRAYICON)
    # -------------------------------------------------------------------------
    Write-Host "`n[6/10] Restoring LibraryWindow via Tray Double-Click message..."
    Write-Host "Sending WM_TRAYICON (0x0465) with WM_LBUTTONDBLCLK (0x0203) to VChibi_TrayMessageWindow (0x$($trayMsgHwnd.ToString('X')))..."
    [Win32Helper]::SendMessage($trayMsgHwnd, [Win32Helper]::WM_TRAYICON, [IntPtr]::Zero, [IntPtr][Win32Helper]::WM_LBUTTONDBLCLK) | Out-Null
    Start-Sleep -Seconds 1
    $libVisibleAgain = [Win32Helper]::IsWindowVisible($libraryHwnd)
    Write-Host "LibraryWindow visibility after restore: $libVisibleAgain (Expected: True)"
    if (-not $libVisibleAgain) {
        Write-Error "LibraryWindow could not be restored via Tray double click!"
        exit 1
    }

    # -------------------------------------------------------------------------
    # STEP 7: Clean Exit via UIAutomation Exit Button (Requirement A14)
    # -------------------------------------------------------------------------
    Write-Host "`n[7/10] Testing clean exit via Exit Button..."
    $libraryElement = [System.Windows.Automation.AutomationElement]::FromHandle($libraryHwnd)
    $exitButton = $libraryElement.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
            "ExitButton"
        ))
    )

    if ($null -ne $exitButton) {
        Write-Host "Found Exit Button, invoking..."
        $invokePattern = $exitButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $invokePattern.Invoke()
    } else {
        Write-Error "Could not find ExitButton with AutomationId=ExitButton!"
        exit 1
    }

    $pExited = $p.WaitForExit(5000)
    Write-Host "Instance 1 exited cleanly: $pExited"
    if (-not $pExited) {
        Write-Error "Instance 1 did not exit within timeout."
        exit 1
    }

    # Verify lock file is released (DeleteOnClose deletes file on process exit)
    Start-Sleep -Milliseconds 500
    $lockReleased = $false
    if (-not (Test-Path $lockFile)) {
        Write-Host "Lock file was deleted on exit (DeleteOnClose), lock cleanly released."
        $lockReleased = $true
    } else {
        try {
            $stream = [System.IO.File]::Open($lockFile, [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
            $lockReleased = $true
            $stream.Close()
            $stream.Dispose()
        } catch {
            $lockReleased = $false
        }
    }
    Write-Host "Lock file released and acquirable: $lockReleased"
    if (-not $lockReleased) {
        Write-Error "Lock file was not released after process exit!"
        exit 1
    }

    # -------------------------------------------------------------------------
    # STEP 8: Verify Settings Persistence on Disk for BOTH Pets (Requirement A14)
    # -------------------------------------------------------------------------
    Write-Host "`n[8/10] Verifying saved settings in settings.json for both pets..."
    if (-not (Test-Path $settingsFile)) {
        Write-Error "settings.json does not exist after exit!"
        exit 1
    }
    $settingsJson = Get-Content $settingsFile -Raw
    Write-Host "Settings content:`n$settingsJson"
    $parsedSettings = ConvertFrom-Json $settingsJson

    # Assert ActivePetId is Pet 2 ("pet-second-id")
    Write-Host "Saved ActivePetId: '$($parsedSettings.activePetId)' (Expected: 'pet-second-id')"
    if ($parsedSettings.activePetId -ne "pet-second-id") {
        Write-Error "ActivePetId in settings.json did not save activated 'pet-second-id'!"
        exit 1
    }

    # Assert Pet 2 scale is 150%, speed is 60
    $pet2Settings = $parsedSettings.pets."pet-second-id"
    Write-Host "Saved Pet 2 Settings -> Scale: $($pet2Settings.scalePercent)%, Speed: $($pet2Settings.movementSpeed) DIP/s"
    if ($pet2Settings.scalePercent -ne 150 -or $pet2Settings.movementSpeed -ne 60) {
        Write-Error "Pet 2 settings not persisted correctly!"
        exit 1
    }

    # -------------------------------------------------------------------------
    # STEP 9: Relaunch State Restore (Requirement A14)
    # -------------------------------------------------------------------------
    Write-Host "`n[9/10] Relaunching VChibi to verify restored state..."
    $pReopen = [System.Diagnostics.Process]::Start($psi)
    Start-Sleep -Seconds 2

    # Discover reopened windows
    $reopenElements = $rootElement.FindAll([System.Windows.Automation.TreeScope]::Children, (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $pReopen.Id
    )))
    $reopenPetHwnd = [IntPtr]::Zero
    $reopenLibHwnd = [IntPtr]::Zero
    $reopenLibEl = $null
    foreach ($el in $reopenElements) {
        if ($el.Current.Name -eq "VChibi") {
            $reopenLibHwnd = [IntPtr]$el.Current.NativeWindowHandle
            $reopenLibEl = $el
        } elseif ($el.Current.Name -eq "VChibi Desktop Pet" -or $el.Current.Name -eq "") {
            $reopenPetHwnd = [IntPtr]$el.Current.NativeWindowHandle
        }
    }

    if ($reopenPetHwnd -ne [IntPtr]::Zero) {
        $rectReopen = New-Object Win32Helper+RECT
        [Win32Helper]::GetWindowRect($reopenPetHwnd, [ref]$rectReopen) | Out-Null
        $reopenWidth = $rectReopen.Right - $rectReopen.Left
        $reopenHeight = $rectReopen.Bottom - $rectReopen.Top
        $expectedPet2Px = [int][Math]::Round(16 * 1.5 * 1.25)
        Write-Host "Reopened PetWindow size: ${reopenWidth}x${reopenHeight}px (Expected: ${expectedPet2Px}x${expectedPet2Px}px)"
        if ($reopenWidth -ne $expectedPet2Px -or $reopenHeight -ne $expectedPet2Px) {
            Write-Error "Reopened PetWindow dimensions (${reopenWidth}x${reopenHeight}px) did not match persisted Pet 2 scale (${expectedPet2Px}x${expectedPet2Px}px)!"
            exit 1
        }
    }

    # Exit reopened instance cleanly
    if ($null -ne $reopenLibEl) {
        $reopenExitBtn = $reopenLibEl.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
                "ExitButton"
            ))
        )
        if ($null -ne $reopenExitBtn) {
            $reopenExitBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
            $pReopen.WaitForExit(5000) | Out-Null
        }
    }
    Write-Host "Verified: Reopen cleanly restored persisted pet settings."

    # -------------------------------------------------------------------------
    # STEP 10: Settings Corruption & Cancel Recovery Guard (Requirement A17)
    # -------------------------------------------------------------------------
    Write-Host "`n[10/10] Testing settings corruption recovery..."
    $backupSettings = Get-Content $settingsFile -Raw
    $corruptPayload = "{ corrupted json invalid payload "
    [System.IO.File]::WriteAllText($settingsFile, $corruptPayload)

    Write-Host "Launching VChibi with corrupt settings..."
    $psiCorrupt = New-Object System.Diagnostics.ProcessStartInfo
    $psiCorrupt.FileName = $exePath
    $psiCorrupt.Arguments = "--data-dir `"$testDataDir`""
    $psiCorrupt.UseShellExecute = $false
    $p = [System.Diagnostics.Process]::Start($psiCorrupt)
    Start-Sleep -Seconds 2

    # Discover UI
    $corruptRoot = [System.Windows.Automation.AutomationElement]::RootElement
    $corruptProcCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $p.Id
    )
    $corruptElements = $corruptRoot.FindAll([System.Windows.Automation.TreeScope]::Children, $corruptProcCondition)
    $corruptLib = $null
    foreach ($el in $corruptElements) {
        if ($el.Current.Name -eq "VChibi") {
            $corruptLib = $el
            break
        }
    }

    if ($null -eq $corruptLib) {
        Write-Error "Could not find LibraryWindow under corrupt settings launch!"
        exit 1
    }

    # Find "Thoát không lưu" button by AutomationId
    $cancelButton = $corruptLib.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
            "CancelSettingsRecoveryButton"
        ))
    )

    if ($null -eq $cancelButton) {
        Write-Error "Could not find 'CancelSettingsRecoveryButton' on corruption banner!"
        exit 1
    }

    Write-Host "Found 'Thoát không lưu' button, invoking Cancel..."
    $cancelPattern = $cancelButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $cancelPattern.Invoke()

    $corruptExited = $p.WaitForExit(5000)
    Write-Host "Process exited after Cancel: $corruptExited"
    if (-not $corruptExited) {
        Write-Error "Process did not exit after Cancel recovery command!"
        exit 1
    }

    # Verify corrupt bytes remain untouched on disk!
    $diskContent = [System.IO.File]::ReadAllText($settingsFile)
    Write-Host "Disk content after cancel:`n'$diskContent'"
    if ($diskContent -ne $corruptPayload) {
        Write-Error "Corrupted settings file was overwritten or modified during Cancel!"
        exit 1
    }
    Write-Host "Verified: Corrupt settings file was preserved untouched."

    Write-Host "`n=========================================================="
    Write-Host "ALL 10 Phase 4 Smoke Checks PASSED Cleanly & Completely!"
    Write-Host "=========================================================="
}
finally {
    if ($null -ne $p -and -not $p.HasExited) {
        try { Stop-Process -Id $p.Id -Force } catch { }
    }
    if ($null -ne $p2 -and -not $p2.HasExited) {
        try { Stop-Process -Id $p2.Id -Force } catch { }
    }
    if ($null -ne $pReopen -and -not $pReopen.HasExited) {
        try { Stop-Process -Id $pReopen.Id -Force } catch { }
    }
    if (Test-Path $testDataDir) {
        try { Remove-Item -Path $testDataDir -Recurse -Force } catch { }
    }
}
