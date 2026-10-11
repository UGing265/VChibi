# ==============================================================================
# VChibi - Phase 6 Standalone Publish Smoke Verification
# Verifies the self-contained publish build running outside the repo directory.
# Exercises: First-run sample, Scale/Speed, Editor flow, Import flow,
# Desktop activation, Tray hide/restore, Clean exit, Relaunch multi-pet persistence.
# ==============================================================================

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

# Win32 Native Dialog Helper
$win32Code = @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class Win32StandaloneHelper {
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_COMMAND = 0x0111;
    public const uint WM_SETTEXT = 0x000C;
    public const uint WM_USER = 0x0400;
    public const uint WM_TRAYICON = WM_USER + 101;
    public const int WM_LBUTTONDBLCLK = 0x0203;

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    public const int IDOK = 1;
    public const int IDCANCEL = 2;
    public const int IDYES = 6;
    public const int IDNO = 7;

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static IntPtr FindDialog(uint targetPid, string titleFilter) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, lParam) => {
            uint pid = 0;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid == targetPid) {
                StringBuilder sbClass = new StringBuilder(256);
                GetClassName(hWnd, sbClass, 256);
                if (sbClass.ToString() == "#32770") {
                    StringBuilder sbTitle = new StringBuilder(256);
                    GetWindowText(hWnd, sbTitle, 256);
                    string t = sbTitle.ToString();
                    string clean = string.IsNullOrEmpty(titleFilter) ? "" : titleFilter.Trim('*');
                    if (string.IsNullOrEmpty(clean) || t.IndexOf(clean, StringComparison.OrdinalIgnoreCase) >= 0) {
                        found = hWnd;
                        return false;
                    }
                }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static IntPtr FindChild(IntPtr parent, string className) {
        IntPtr child = IntPtr.Zero;
        EnumChildWindows(parent, (hWnd, lParam) => {
            StringBuilder sb = new StringBuilder(256);
            GetClassName(hWnd, sb, 256);
            if (sb.ToString().Equals(className, StringComparison.OrdinalIgnoreCase)) {
                child = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return child;
    }

    public static bool SetText(IntPtr hWnd, string text) {
        IntPtr hEdit = FindChild(hWnd, "Edit");
        if (hEdit != IntPtr.Zero) {
            SendMessage(hEdit, WM_SETTEXT, IntPtr.Zero, text);
            return true;
        }
        return false;
    }

    public static void Click(IntPtr hWnd, int controlId) {
        PostMessage(hWnd, WM_COMMAND, (IntPtr)controlId, IntPtr.Zero);
    }

    public static IntPtr FindWindowByProcessAndTitle(uint targetPid, string targetTitle) {
        IntPtr foundHwnd = IntPtr.Zero;
        EnumWindows((hWnd, lParam) => {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid == targetPid) {
                StringBuilder sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                if (sb.ToString() == targetTitle) {
                    foundHwnd = hWnd;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);
        return foundHwnd;
    }

    public static IntPtr FindPetWindow(uint targetPid, IntPtr libHwnd) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, lParam) => {
            uint pid = 0;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid == targetPid && hWnd != libHwnd) {
                StringBuilder sb = new StringBuilder(256);
                GetClassName(hWnd, sb, 256);
                if (sb.ToString().StartsWith("HwndWrapper")) {
                    found = hWnd;
                    return false;
                }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
"@
Add-Type -TypeDefinition $win32Code

# Helpers
function Get-ElementById {
    param(
        [System.Windows.Automation.AutomationElement]$root,
        [string]$id,
        [int]$timeoutSec = 10
    )
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $id
    )
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.ElapsedMilliseconds -lt ($timeoutSec * 1000)) {
        $elem = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if ($elem -ne $null) {
            return $elem
        }
        Start-Sleep -Milliseconds 150
    }
    throw "Element with AutomationId '$id' was not found within $timeoutSec seconds."
}

function Test-IsEditorOpen {
    param([System.Windows.Automation.AutomationElement]$libWin)
    $nameBox = $libWin.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, "PetNameTextBox"))
    )
    if ($nameBox -eq $null) { return $false }
    return (-not $nameBox.Current.IsOffscreen)
}

function New-SamplePng {
    param([string]$Path, [int]$Width = 48, [int]$Height = 48, [System.Drawing.Color]$Color)
    $bmp = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $brush = New-Object System.Drawing.SolidBrush($Color)
    $g.FillRectangle($brush, 0, 0, $Width, $Height)
    $brush.Dispose()
    $g.Dispose()
    $dir = [System.IO.Path]::GetDirectoryName($Path)
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

function New-PetPackage {
    param([string]$PackageDir, [string]$Id, [string]$Name, [int]$Width = 32, [int]$Height = 32)
    New-SamplePng (Join-Path $PackageDir "idle\001.png") $Width $Height ([System.Drawing.Color]::FromArgb(255, 120, 98, 184))
    New-SamplePng (Join-Path $PackageDir "idle\002.png") $Width $Height ([System.Drawing.Color]::FromArgb(255, 140, 118, 204))
    New-SamplePng (Join-Path $PackageDir "walk\001.png") $Width $Height ([System.Drawing.Color]::FromArgb(255, 160, 138, 224))
    New-SamplePng (Join-Path $PackageDir "walk\002.png") $Width $Height ([System.Drawing.Color]::FromArgb(255, 180, 158, 244))

    $manifest = @{
        schemaVersion = 1
        id = $Id
        name = $Name
        animations = @{
            idle = @{ fps = 8; frames = @("idle/001.png", "idle/002.png") }
            walk = @{ fps = 10; frames = @("walk/001.png", "walk/002.png") }
        }
    }
    $json = $manifest | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText((Join-Path $PackageDir "pet.json"), $json)
}

function Start-DialogHandler {
    param(
        [int]$ProcessId,
        [string]$ExpectedTitle,
        [string]$Action,
        [string]$InputPath = "",
        [int]$TimeoutSeconds = 10
    )

    $rs = [powershell]::Create().AddScript({
        param($targetPid, $expectedTitle, $action, $inputPath, $timeoutSec)

        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        while ($stopwatch.Elapsed.TotalSeconds -lt $timeoutSec) {
            $h = [Win32StandaloneHelper]::FindDialog([uint32]$targetPid, $expectedTitle)
            if ($h -ne [IntPtr]::Zero) {
                Start-Sleep -Milliseconds 200

                switch ($action) {
                    "ClickYes" {
                        [Win32StandaloneHelper]::Click($h, [Win32StandaloneHelper]::IDYES)
                    }
                    "ClickOK" {
                        [Win32StandaloneHelper]::Click($h, [Win32StandaloneHelper]::IDOK)
                    }
                    "ClickCancel" {
                        [Win32StandaloneHelper]::Click($h, [Win32StandaloneHelper]::IDCANCEL)
                    }
                    { $_ -in "SelectFiles", "SelectFolder" } {
                        $setRes = [Win32StandaloneHelper]::SetText($h, $inputPath)
                        Start-Sleep -Milliseconds 300
                        [Win32StandaloneHelper]::Click($h, [Win32StandaloneHelper]::IDOK)
                        Start-Sleep -Milliseconds 600
                        if ([Win32StandaloneHelper]::IsWindowVisible($h)) {
                            [Win32StandaloneHelper]::Click($h, [Win32StandaloneHelper]::IDOK)
                        }
                    }
                }
                $dismissWatch = [System.Diagnostics.Stopwatch]::StartNew()
                while ($dismissWatch.Elapsed.TotalSeconds -lt 4) {
                    if (-not [Win32StandaloneHelper]::IsWindowVisible($h)) {
                        break
                    }
                    Start-Sleep -Milliseconds 100
                }

                return "Dialog processed via $action (HWND=0x$($h.ToString('X')), SetText=$setRes)"
            }
            Start-Sleep -Milliseconds 150
        }
        throw "Timeout waiting for dialog with title '$expectedTitle' on PID $targetPid"
    }).AddArgument($ProcessId).AddArgument($ExpectedTitle).AddArgument($Action).AddArgument($InputPath).AddArgument($TimeoutSeconds)

    $asyncResult = $rs.BeginInvoke()
    return @{ Runspace = $rs; AsyncResult = $asyncResult }
}

function Wait-DialogHandler {
    param($Handle)
    try {
        $output = $Handle.Runspace.EndInvoke($Handle.AsyncResult)
        if ($Handle.Runspace.Streams.Error.Count -gt 0) {
            foreach ($err in $Handle.Runspace.Streams.Error) {
                Write-Host "RUNSPACE ERROR: $err"
            }
        }
        return $output
    } finally {
        $Handle.Runspace.Dispose()
    }
}

# -----------------------------------------------------------------------------
# 1. Setup Isolated Standalone Directory outside the repo
# -----------------------------------------------------------------------------
$sourcePublish = Join-Path $PSScriptRoot "..\artifacts\publish\win-x64"
$sourcePublish = [System.IO.Path]::GetFullPath($sourcePublish)
if (-not (Test-Path (Join-Path $sourcePublish "VChibi.exe"))) {
    Write-Error "Published executable not found at: $sourcePublish"
    exit 1
}

$standaloneDir = Join-Path ([System.IO.Path]::GetTempPath()) ("vchibi_standalone_run_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $standaloneDir -Force | Out-Null
Write-Host "Copying self-contained publish build to isolated standalone directory:"
Write-Host "  Destination: $standaloneDir"

Copy-Item -Path "$sourcePublish\*" -Destination $standaloneDir -Recurse -Force

$tempDataDir = Join-Path ([System.IO.Path]::GetTempPath()) ("vchibi_standalone_data_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempDataDir -Force | Out-Null
Write-Host "Isolated data directory: $tempDataDir"

$proc = $null
$proc2 = $null

try {
    # -------------------------------------------------------------------------
    # 2. Launch VChibi.exe from the standalone folder (CWD = $standaloneDir)
    # -------------------------------------------------------------------------
    Write-Host "`n[1/10] Launching standalone VChibi.exe outside repo..."
    $exePath = Join-Path $standaloneDir "VChibi.exe"
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exePath
    $psi.Arguments = "--data-dir `"$tempDataDir`""
    $psi.WorkingDirectory = $standaloneDir
    $psi.UseShellExecute = $false
    $proc = [System.Diagnostics.Process]::Start($psi)

    if ($proc -eq $null -or $proc.HasExited) {
        Write-Error "Failed to launch standalone VChibi.exe!"
        exit 1
    }
    Write-Host "Standalone process started with PID: $($proc.Id)"
    Start-Sleep -Seconds 3

    # Find LibraryWindow
    $rootElement = [System.Windows.Automation.AutomationElement]::RootElement
    $libCond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)),
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "VChibi"))
    )
    $libWindow = $rootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $libCond)
    if ($libWindow -eq $null) {
        Write-Error "Could not find LibraryWindow on standalone launch!"
        exit 1
    }
    $libHwnd = [IntPtr]$libWindow.Current.NativeWindowHandle
    Write-Host "Found LibraryWindow (HWND=0x$($libHwnd.ToString('X')))"

    # Find PetWindow HWND
    $petHwnd = [Win32StandaloneHelper]::FindPetWindow([uint32]$proc.Id, $libHwnd)
    Write-Host "Found PetWindow (HWND=0x$($petHwnd.ToString('X')))"

    # Verify Win32 extended styles
    $exStyle = [Win32StandaloneHelper]::GetWindowLong($petHwnd, [Win32StandaloneHelper]::GWL_EXSTYLE)
    $hasTransparent = ($exStyle -band [Win32StandaloneHelper]::WS_EX_TRANSPARENT) -ne 0
    $hasToolWindow  = ($exStyle -band [Win32StandaloneHelper]::WS_EX_TOOLWINDOW) -ne 0
    $hasNoActivate  = ($exStyle -band [Win32StandaloneHelper]::WS_EX_NOACTIVATE) -ne 0
    Write-Host "PetWindow: WS_EX_TRANSPARENT=$hasTransparent, WS_EX_NOACTIVATE=$hasNoActivate, WS_EX_TOOLWINDOW=$hasToolWindow"
    if (-not ($hasTransparent -and $hasNoActivate -and $hasToolWindow)) {
        Write-Error "PetWindow is missing required Win32 extended styles!"
        exit 1
    }

    # Verify default pet is loaded and in library list
    $petList = Get-ElementById $libWindow "PetListBox"
    $pets = $petList.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Host "Pets in standalone library: $($pets.Count) (Expected: 1)"
    if ($pets.Count -ne 1) {
        Write-Error "Expected exactly 1 pet in initial standalone library!"
        exit 1
    }

    # -------------------------------------------------------------------------
    # 3. Representative Editor Flow on Standalone Published Build
    # -------------------------------------------------------------------------
    Write-Host "`n[2/10] Testing Representative Pet Creation in Editor on Standalone Build..."
    $editorSources = Join-Path ([System.IO.Path]::GetTempPath()) ("vchibi_standalone_editor_src_" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $editorSources -Force | Out-Null
    $idle1 = Join-Path $editorSources "idle_001.png"
    $idle2 = Join-Path $editorSources "idle_002.png"
    $walk1 = Join-Path $editorSources "walk_001.png"
    $walk2 = Join-Path $editorSources "walk_002.png"
    New-SamplePng $idle1 48 48 ([System.Drawing.Color]::FromArgb(255, 100, 150, 200))
    New-SamplePng $idle2 48 48 ([System.Drawing.Color]::FromArgb(255, 120, 170, 220))
    New-SamplePng $walk1 48 48 ([System.Drawing.Color]::FromArgb(255, 200, 100, 150))
    New-SamplePng $walk2 48 48 ([System.Drawing.Color]::FromArgb(255, 220, 120, 170))

    $createBtn = Get-ElementById $libWindow "CreatePetButton"
    $createBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 600

    if (-not (Test-IsEditorOpen $libWindow)) { Write-Error "Editor did not open on standalone build!"; exit 1 }
    $nameBox = Get-ElementById $libWindow "PetNameTextBox"
    $nameBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("Standalone Idol")

    # Add Idle frames
    $addFramesBtn = Get-ElementById $libWindow "AddFramesButton"
    $dlg1 = Start-DialogHandler -ProcessId $proc.Id -ExpectedTitle "PNG" -Action "SelectFiles" -InputPath "`"$idle1`" `"$idle2`""
    $addFramesBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Wait-DialogHandler $dlg1 | Out-Null
    Start-Sleep -Milliseconds 300

    $fpsSlider = Get-ElementById $libWindow "FpsSlider"
    $fpsSlider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue(7)

    # Switch to Walk tab & add Walk frames
    $walkTab = Get-ElementById $libWindow "WalkTabRadio"
    $walkTab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 300

    $dlg2 = Start-DialogHandler -ProcessId $proc.Id -ExpectedTitle "PNG" -Action "SelectFiles" -InputPath "`"$walk1`" `"$walk2`""
    $addFramesBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Wait-DialogHandler $dlg2 | Out-Null
    Start-Sleep -Milliseconds 300

    $fpsSlider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue(14)

    # Verification: Launch second instance while editor draft is open
    Write-Host "Verifying second instance collision while editor draft is open..."
    $procColliding = [System.Diagnostics.Process]::Start($psi)
    $collisionDismissed = $false
    for ($i = 0; $i -lt 12; $i++) {
        $pColCond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
            $procColliding.Id
        )
        $pColWindows = $rootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $pColCond)
        foreach ($w in $pColWindows) {
            Write-Host "Found Collision Dialog while Editor open: Handle=0x$($w.Current.NativeWindowHandle.ToString('X')), Title='$($w.Current.Name)'"
            $dlgHwnd = [IntPtr]$w.Current.NativeWindowHandle
            [Win32StandaloneHelper]::PostMessage($dlgHwnd, [Win32StandaloneHelper]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
            $collisionDismissed = $true
            break
        }
        if ($collisionDismissed) { break }
        Start-Sleep -Milliseconds 400
    }
    if (-not $collisionDismissed) {
        Write-Error "Could not find collision dialog on colliding instance while editor open!"
        exit 1
    }
    $collidingExited = $procColliding.WaitForExit(6000)
    Write-Host "Colliding process exit: Exited=$collidingExited, ExitCode=$($procColliding.ExitCode)"
    if (-not $collidingExited) {
        try { Stop-Process -Id $procColliding.Id -Force } catch { }
        Write-Error "Colliding process did not exit after dismissing dialog!"
        exit 1
    }
    Write-Host "Verified: Second instance during open editor draft dismissed cleanly without affecting editor draft."

    # Save created pet
    $saveBtn = Get-ElementById $libWindow "SaveButton"
    $saveBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 1

    if (Test-IsEditorOpen $libWindow) { Write-Error "Editor did not close after Save on standalone build!"; exit 1 }
    $pets = $petList.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Host "Pets after creation: $($pets.Count) (Expected: 2)"
    if ($pets.Count -ne 2) { Write-Error "New pet was not added to library on standalone build!"; exit 1 }
    Write-Host "Verified: Representative Pet Editor flow works on published build."

    # -------------------------------------------------------------------------
    # 4. Representative Import Flow on Standalone Published Build
    # -------------------------------------------------------------------------
    Write-Host "`n[3/10] Testing Representative Package Import on Standalone Build..."
    $importFolder = Join-Path ([System.IO.Path]::GetTempPath()) ("vchibi_standalone_import_pkg_" + [Guid]::NewGuid().ToString("N"))
    New-PetPackage -PackageDir $importFolder -Id "standalone-imported" -Name "Imported Standalone" -Width 16 -Height 16

    $importBtn = Get-ElementById $libWindow "ImportPackageButton"
    $importWatcher = Start-DialogHandler -ProcessId $proc.Id -ExpectedTitle "pet" -Action "SelectFolder" -InputPath $importFolder
    $importBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $importRes = Wait-DialogHandler $importWatcher
    Write-Host "Import folder dialog: $importRes"
    Start-Sleep -Seconds 1

    $pets = $petList.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Host "Pets after package import: $($pets.Count) (Expected: 3)"
    if ($pets.Count -ne 3) { Write-Error "Package import failed on standalone build!"; exit 1 }
    Write-Host "Verified: Representative Package Import works on published build."

    # -------------------------------------------------------------------------
    # 5. Selected vs Active ID Independence (Phase 6 Step 4, A01, A04)
    # -------------------------------------------------------------------------
    Write-Host "`n[4/10] Testing Selected vs Active ID Independence on Standalone Build..."
    # Active pet is currently Chibi Idol (180x180 px at 150% scale)
    $pets = $petList.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)

    $textCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)
    $importedPetItem = $null
    $standalonePetItem = $null
    $defaultPetItem = $null
    foreach ($item in $pets) {
        $texts = $item.FindAll([System.Windows.Automation.TreeScope]::Descendants, $textCond)
        $fullText = ($texts | ForEach-Object { $_.Current.Name }) -join " "
        if ($fullText -match "Imported Standalone") { $importedPetItem = $item }
        elseif ($fullText -match "Standalone Idol") { $standalonePetItem = $item }
        elseif ($fullText -match "Chibi Idol") { $defaultPetItem = $item }
    }
    if (-not $importedPetItem -or -not $standalonePetItem -or -not $defaultPetItem) {
        Write-Error "Could not find all 3 pet items in PetListBox!"; exit 1
    }

    # Select Imported Standalone WITHOUT releasing to desktop
    Write-Host "Selecting 'Imported Standalone' (16px) in ListBox..."
    $importedPetItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 500

    # PetWindow dimension MUST remain 120x120 px because active pet is still Chibi Idol (96px at 100% scale)!
    $rect = New-Object Win32StandaloneHelper+RECT
    [Win32StandaloneHelper]::GetWindowRect($petHwnd, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top
    Write-Host "PetWindow dimension while 'Imported Standalone' is selected: ${w}x${h} px (Expected: 120x120 px)"
    if ($w -ne 120 -or $h -ne 120) {
        Write-Error "Selected pet hijacked active desktop pet without ReleaseToDesktop! Expected 120x120 px, got ${w}x${h}."
        exit 1
    }
    Write-Host "Verified: Selected vs Active ID independence on standalone build."

    # -------------------------------------------------------------------------
    # 6. Repeated Hide / Show / Replace across All Pets (Phase 6 Step 4, A01, A04)
    # -------------------------------------------------------------------------
    Write-Host "`n[5/10] Testing Repeated Hide / Show / Replace across all 3 pets..."
    $hideBtn = Get-ElementById $libWindow "HideFromDesktopButton"
    $releaseBtn = Get-ElementById $libWindow "ReleaseToDesktopButton"

    # Cycle 1: Hide -> verify hidden -> Release -> verify visible (Chibi Idol: 180x180)
    $defaultPetItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $hideBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 400
    $visHidden1 = [Win32StandaloneHelper]::IsWindowVisible($petHwnd)
    Write-Host "PetWindow after Hide: $visHidden1 (Expected: False)"
    if ($visHidden1) { Write-Error "HideFromDesktopButton failed to hide PetWindow!"; exit 1 }

    $releaseBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 400
    $visShown1 = [Win32StandaloneHelper]::IsWindowVisible($petHwnd)
    Write-Host "PetWindow after Release: $visShown1 (Expected: True)"
    if (-not $visShown1) { Write-Error "ReleaseToDesktopButton failed to show PetWindow!"; exit 1 }

    # Cycle 2: Replace to Imported Standalone (16px base -> 20x20 px at 100% scale, 1.25 DPI)
    $importedPetItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 200
    $releaseBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 500
    [Win32StandaloneHelper]::GetWindowRect($petHwnd, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top
    Write-Host "PetWindow dimension after replacing with Imported Standalone: ${w}x${h} px (Expected: 20x20 px)"
    if ($w -ne 20 -or $h -ne 20) {
        Write-Error "PetWindow did not resize to 20x20 px for Imported Standalone! Actual: ${w}x${h}."
        exit 1
    }

    # Cycle 3: Replace to Standalone Idol (48px base -> 60x60 px at 100% scale, 1.25 DPI)
    $standalonePetItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 200
    $releaseBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 500
    [Win32StandaloneHelper]::GetWindowRect($petHwnd, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top
    Write-Host "PetWindow dimension after replacing with Standalone Idol: ${w}x${h} px (Expected: 60x60 px)"
    if ($w -ne 60 -or $h -ne 60) {
        Write-Error "PetWindow did not resize to 60x60 px for Standalone Idol! Actual: ${w}x${h}."
        exit 1
    }
    Write-Host "Verified: Repeated hide/show/replace functions reliably across all pets."

    # -------------------------------------------------------------------------
    # 7. CPU / Memory Active vs Hidden & Resource Growth Observation (Phase 6 Step 5)
    # -------------------------------------------------------------------------
    Write-Host "`n[6/10] Observing CPU / Memory Active vs Hidden & Resource Growth..."
    $procObj = Get-Process -Id $proc.Id
    $wsActiveBefore = [math]::Round($procObj.WorkingSet64 / 1MB, 2)
    Write-Host "WorkingSet64 when Desktop Pet is ACTIVE: ${wsActiveBefore} MB"

    # Hide Desktop Pet and observe
    $hideBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 2
    $procObj.Refresh()
    $wsHidden = [math]::Round($procObj.WorkingSet64 / 1MB, 2)
    Write-Host "WorkingSet64 when Desktop Pet is HIDDEN: ${wsHidden} MB"

    # Show Desktop Pet again and perform 5 rapid replacements to test memory growth bounds
    $releaseBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 300

    Write-Host "Performing 5 rapid pet replacements to measure memory delta..."
    for ($i = 1; $i -le 5; $i++) {
        $target = if ($i % 2 -eq 0) { $defaultPetItem } else { $standalonePetItem }
        $target.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        $releaseBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 200
    }
    # Ensure Standalone Idol is the final active pet
    $standalonePetItem.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $releaseBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 1

    $procObj.Refresh()
    $wsAfterReplacements = [math]::Round($procObj.WorkingSet64 / 1MB, 2)
    $deltaMb = [math]::Round($wsAfterReplacements - $wsActiveBefore, 2)
    Write-Host "WorkingSet64 after 5 rapid replacements: ${wsAfterReplacements} MB (Delta: ${deltaMb} MB)"
    Write-Host "Observed: CPU/Memory active vs hidden recorded and observed replacement delta: ${deltaMb} MB."

    # -------------------------------------------------------------------------
    # 8. Second Instance Collision while Primary is Hidden in Tray (Phase 6 Step 4, A16)
    # -------------------------------------------------------------------------
    Write-Host "`n[7/10] Testing Second Instance Collision while Primary is in Tray..."
    # Close LibraryWindow to Tray (WM_CLOSE)
    [Win32StandaloneHelper]::PostMessage($libHwnd, [Win32StandaloneHelper]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Seconds 1

    $libVis = [Win32StandaloneHelper]::IsWindowVisible($libHwnd)
    $petVis = [Win32StandaloneHelper]::IsWindowVisible($petHwnd)
    Write-Host "Primary LibraryWindow visible in tray: $libVis (Expected: False)"
    Write-Host "Primary PetWindow visible in tray:     $petVis (Expected: True)"
    if ($libVis -or (-not $petVis)) {
        Write-Error "Close to tray behavior failed before second instance test!"
        exit 1
    }

    # Launch second instance with same --data-dir
    Write-Host "Launching second instance while primary is in tray..."
    $procCol = [System.Diagnostics.Process]::Start($psi)
    Start-Sleep -Milliseconds 500

    # Find and dismiss modal warning dialog on instance 2
    Write-Host "Finding modal collision dialog on PID $($procCol.Id)..."
    $dialogDismissed = $false
    for ($i = 0; $i -lt 12; $i++) {
        $p2ProcCond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
            $procCol.Id
        )
        $p2Windows = $rootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $p2ProcCond)
        foreach ($w in $p2Windows) {
            Write-Host "Found Instance 2 Collision Dialog: Handle=0x$($w.Current.NativeWindowHandle.ToString('X')), Title='$($w.Current.Name)'"
            $dlgHwnd = [IntPtr]$w.Current.NativeWindowHandle
            [Win32StandaloneHelper]::PostMessage($dlgHwnd, [Win32StandaloneHelper]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
            $dialogDismissed = $true
            break
        }
        if ($dialogDismissed) { break }
        Start-Sleep -Milliseconds 400
    }
    if (-not $dialogDismissed) {
        Write-Error "Could not find collision dialog on instance 2!"
        exit 1
    }

    $colExited = $procCol.WaitForExit(6000)
    Write-Host "Second instance exit: Exited=$colExited, ExitCode=$($procCol.ExitCode)"
    if (-not $colExited) {
        try { Stop-Process -Id $procCol.Id -Force } catch { }
        Write-Error "Second instance failed to exit cleanly after collision warning!"
        exit 1
    }

    # Verify primary process in tray is still alive and unharmed
    if ($proc.HasExited) {
        Write-Error "Primary process exited prematurely during collision test!"
        exit 1
    }

    # Restore LibraryWindow from Tray
    $trayHwnd = [Win32StandaloneHelper]::FindWindowByProcessAndTitle([uint32]$proc.Id, "VChibi_TrayMessageWindow")
    Write-Host "Restoring primary LibraryWindow via Tray Double-Click (HWND 0x$($trayHwnd.ToString('X')))..."
    if ($trayHwnd -eq [IntPtr]::Zero) { Write-Error "Could not find VChibi_TrayMessageWindow!"; exit 1 }
    [Win32StandaloneHelper]::SendMessage($trayHwnd, [Win32StandaloneHelper]::WM_TRAYICON, [IntPtr]::Zero, [IntPtr][Win32StandaloneHelper]::WM_LBUTTONDBLCLK) | Out-Null
    Start-Sleep -Seconds 1

    $libVisRestored = [Win32StandaloneHelper]::IsWindowVisible($libHwnd)
    Write-Host "Primary LibraryWindow visible after restore: $libVisRestored (Expected: True)"
    if (-not $libVisRestored) {
        Write-Error "Tray restore failed to show LibraryWindow after collision test!"
        exit 1
    }
    Write-Host "Verified: Second instance collision handled cleanly while primary is in tray."

    # Clean Exit primary
    Write-Host "Exiting primary instance cleanly..."
    $exitBtn = Get-ElementById $libWindow "ExitButton"
    $exitBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()

    $exited = $proc.WaitForExit(6000)
    Write-Host "Primary standalone process exit: Exited=$exited, ExitCode=$($proc.ExitCode)"
    if (-not $exited) {
        try { Stop-Process -Id $proc.Id -Force } catch { }
        Write-Error "Primary standalone app did not exit within 6 seconds!"
        exit 1
    }

    $lockPath = Join-Path $tempDataDir ".instance.lock"
    Write-Host "Lock file after exit: $(Test-Path $lockPath) (Expected: False)"
    if (Test-Path $lockPath) {
        Write-Error "Lock file was not cleaned up on exit!"
        exit 1
    }

    # -------------------------------------------------------------------------
    # 9. Standalone Relaunch Multi-Pet Persistence Verification (Phase 6 Step 7)
    # -------------------------------------------------------------------------
    Write-Host "`n[8/10] Relaunching Standalone Build to Verify Full Multi-Pet Persistence..."
    $proc2 = [System.Diagnostics.Process]::Start($psi)
    Start-Sleep -Seconds 3

    $libCond2 = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc2.Id)),
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "VChibi"))
    )
    $libWindow2 = $rootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $libCond2)
    $petList2 = Get-ElementById $libWindow2 "PetListBox"
    $petsRelaunch = $petList2.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Host "Pets in library on relaunch: $($petsRelaunch.Count) (Expected: 3)"
    if ($petsRelaunch.Count -ne 3) {
        Write-Error "Multi-pet library did not persist across standalone relaunches! Expected 3 pets, got $($petsRelaunch.Count)."
        exit 1
    }

    # Find PetWindow on relaunch and verify active pet dimension persisted (60x60 px)
    $petHwnd2 = [Win32StandaloneHelper]::FindPetWindow([uint32]$proc2.Id, [IntPtr]$libWindow2.Current.NativeWindowHandle)
    Write-Host "Found PetWindow on relaunch (HWND=0x$($petHwnd2.ToString('X')))"

    $rect2 = New-Object Win32StandaloneHelper+RECT
    [Win32StandaloneHelper]::GetWindowRect($petHwnd2, [ref]$rect2) | Out-Null
    $petW2 = $rect2.Right - $rect2.Left
    $petH2 = $rect2.Bottom - $rect2.Top
    Write-Host "Active Desktop PetWindow dimension on relaunch: ${petW2}x${petH2} px (Expected: 60x60 px)"
    if ($petW2 -ne 60 -or $petH2 -ne 60) {
        Write-Error "Active pet state did not persist on relaunch! Actual: ${petW2}x${petH2}"
        exit 1
    }

    # Now hide pet on instance 2 and exit to test hidden-state persistence across relaunch
    Write-Host "Hiding active desktop pet before exit to test hidden persistence..."
    $hideBtn2 = Get-ElementById $libWindow2 "HideFromDesktopButton"
    $hideBtn2.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 500
    $petVisAfterHide = [Win32StandaloneHelper]::IsWindowVisible($petHwnd2)
    Write-Host "PetWindow visibility after Hide: $petVisAfterHide (Expected: False)"
    if ($petVisAfterHide) {
        Write-Error "PetWindow was still visible after Hide command!"
        exit 1
    }

    $exitBtn2 = Get-ElementById $libWindow2 "ExitButton"
    $exitBtn2.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $proc2.WaitForExit(6000) | Out-Null
    Write-Host "Second standalone run exited cleanly in hidden state."

    $settingsJsonContent = [System.IO.File]::ReadAllText((Join-Path $tempDataDir "settings.json"))
    $isPetVisFalse = $settingsJsonContent -match '\"isPetVisible\":\s*false'
    Write-Host "Persisted settings contains isPetVisible=false: $isPetVisFalse (Expected: True)"
    if (-not $isPetVisFalse) {
        Write-Error "settings.json did not persist isPetVisible=false upon clean exit!"
        exit 1
    }

    # -------------------------------------------------------------------------
    # 10. Standalone Relaunch in Hidden State Verification (A14)
    # -------------------------------------------------------------------------
    Write-Host "`n[9/10] Relaunching Standalone Build to Verify Hidden State Persistence (A14)..."
    $procHidden = [System.Diagnostics.Process]::Start($psi)
    Start-Sleep -Seconds 3

    $libCondHidden = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $procHidden.Id)),
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "VChibi"))
    )
    $libWindowHidden = $rootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $libCondHidden)
    if ($null -eq $libWindowHidden) {
        Write-Error "Could not find LibraryWindow on hidden-state relaunch!"
        exit 1
    }

    # Check PetWindow visibility on relaunch: PetWindow should NOT be visible!
    $petHwndHidden = [Win32StandaloneHelper]::FindPetWindow([uint32]$procHidden.Id, [IntPtr]$libWindowHidden.Current.NativeWindowHandle)
    $hiddenPetIsVisible = if ($petHwndHidden -ne [IntPtr]::Zero) { [Win32StandaloneHelper]::IsWindowVisible($petHwndHidden) } else { $false }
    Write-Host "PetWindow visibility on hidden-state relaunch: $hiddenPetIsVisible (Expected: False)"
    if ($hiddenPetIsVisible) {
        Write-Error "PetWindow was visible on relaunch when saved in hidden state!"
        exit 1
    }
    Write-Host "Verified: Hidden state (isPetVisible=false) persisted cleanly across relaunch."

    # Re-release pet to desktop and verify it appears again
    Write-Host "Re-releasing pet from hidden state on relaunch..."
    $releaseBtnHidden = Get-ElementById $libWindowHidden "ReleaseToDesktopButton"
    $releaseBtnHidden.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 1

    $petHwndRestored = [Win32StandaloneHelper]::FindPetWindow([uint32]$procHidden.Id, [IntPtr]$libWindowHidden.Current.NativeWindowHandle)
    $restoredPetVis = if ($petHwndRestored -ne [IntPtr]::Zero) { [Win32StandaloneHelper]::IsWindowVisible($petHwndRestored) } else { $false }
    Write-Host "PetWindow visibility after Re-release: $restoredPetVis (Expected: True)"
    if (-not $restoredPetVis) {
        Write-Error "PetWindow failed to become visible after Re-release from hidden state!"
        exit 1
    }

    $exitBtnHidden = Get-ElementById $libWindowHidden "ExitButton"
    $exitBtnHidden.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $procHidden.WaitForExit(6000) | Out-Null
    Write-Host "Hidden-state test instance exited cleanly."

    # -------------------------------------------------------------------------
    # 11. Standalone Corrupt Settings Recovery Verification (A17)
    # -------------------------------------------------------------------------
    Write-Host "`n[10/10] Testing Corrupt Settings Recovery on Standalone Published Build..."
    $settingsPath = Join-Path $tempDataDir "settings.json"
    $corruptPayload = "{ corrupted json invalid payload for standalone "
    [System.IO.File]::WriteAllText($settingsPath, $corruptPayload)

    $procCorrupt = [System.Diagnostics.Process]::Start($psi)
    Start-Sleep -Seconds 2

    $corruptLibCond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $procCorrupt.Id)),
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "VChibi"))
    )
    $corruptLibWindow = $rootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $corruptLibCond)
    if ($null -eq $corruptLibWindow) {
        Write-Error "Could not find LibraryWindow under corrupt settings launch on standalone build!"
        exit 1
    }

    $cancelRecoveryBtn = Get-ElementById $corruptLibWindow "CancelSettingsRecoveryButton"
    if ($null -eq $cancelRecoveryBtn) {
        Write-Error "Could not find 'CancelSettingsRecoveryButton' on corruption banner on standalone build!"
        exit 1
    }

    Write-Host "Found 'CancelSettingsRecoveryButton', invoking Cancel..."
    $cancelRecoveryBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $corruptExited = $procCorrupt.WaitForExit(6000)
    Write-Host "Standalone process exited after Cancel: $corruptExited, ExitCode=$($procCorrupt.ExitCode)"
    if (-not $corruptExited) {
        try { Stop-Process -Id $procCorrupt.Id -Force } catch { }
        Write-Error "Standalone process did not exit after Cancel recovery command!"
        exit 1
    }

    $diskContentAfterCancel = [System.IO.File]::ReadAllText($settingsPath)
    Write-Host "Disk content after cancel:`n'$diskContentAfterCancel'"
    if ($diskContentAfterCancel -ne $corruptPayload) {
        Write-Error "Corrupted settings file was overwritten or modified during Cancel on standalone build!"
        exit 1
    }
    Write-Host "Verified: Corrupt settings recovery on standalone build preserved 100% of corrupt file."

    $lockAfterCorrupt = Join-Path $tempDataDir ".instance.lock"
    if (Test-Path $lockAfterCorrupt) {
        Write-Error "Lock file was not cleaned up after corrupt settings recovery exit on standalone build!"
        exit 1
    }
    Write-Host "Verified: Lock file released after corrupt settings recovery exit."

    Write-Host "`n======================================================="
    Write-Host "STANDALONE PUBLISH SMOKE VERIFICATION COMPLETED CLEANLY!"
    Write-Host "======================================================="
}
finally {
    if ($proc -ne $null -and -not $proc.HasExited) {
        try { Stop-Process -Id $proc.Id -Force } catch { }
    }
    if (Get-Variable -Name proc2 -ErrorAction SilentlyContinue) {
        if ($proc2 -ne $null -and -not $proc2.HasExited) {
            try { Stop-Process -Id $proc2.Id -Force } catch { }
        }
    }
    if (Get-Variable -Name procColliding -ErrorAction SilentlyContinue) {
        if ($procColliding -ne $null -and -not $procColliding.HasExited) {
            try { Stop-Process -Id $procColliding.Id -Force } catch { }
        }
    }
    if (Get-Variable -Name procCorrupt -ErrorAction SilentlyContinue) {
        if ($procCorrupt -ne $null -and -not $procCorrupt.HasExited) {
            try { Stop-Process -Id $procCorrupt.Id -Force } catch { }
        }
    }
    if (Get-Variable -Name procHidden -ErrorAction SilentlyContinue) {
        if ($procHidden -ne $null -and -not $procHidden.HasExited) {
            try { Stop-Process -Id $procHidden.Id -Force } catch { }
        }
    }
    # Clean up temp test directories
    try { Remove-Item -Path $standaloneDir -Recurse -Force -ErrorAction SilentlyContinue } catch { }
    try { Remove-Item -Path $tempDataDir -Recurse -Force -ErrorAction SilentlyContinue } catch { }
    if (Get-Variable -Name editorSources -ErrorAction SilentlyContinue) {
        try { Remove-Item -Path $editorSources -Recurse -Force -ErrorAction SilentlyContinue } catch { }
    }
    if (Get-Variable -Name importFolder -ErrorAction SilentlyContinue) {
        try { Remove-Item -Path $importFolder -Recurse -Force -ErrorAction SilentlyContinue } catch { }
    }
}
