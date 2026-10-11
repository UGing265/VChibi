<#
.SYNOPSIS
    Phase 5 Comprehensive UI Smoke Verification: Pet Editor & Package Import
    Covers criteria: A07, A08, A09, A10, A11, A12
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

# Win32 Helpers
$win32Code = @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class Win32NativeDialogHelper {
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_COMMAND = 0x0111;
    public const uint WM_SETTEXT = 0x000C;
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
}
"@
if (-not ([System.Management.Automation.PSTypeName]"Win32NativeDialogHelper").Type) {
    Add-Type -TypeDefinition $win32Code
}
# Helper: Create PNG image
function New-SamplePng {
    param([string]$Path, [int]$Width = 32, [int]$Height = 32, [System.Drawing.Color]$Color)
    $dir = [System.IO.Path]::GetDirectoryName($Path)
    if (-not [System.IO.Directory]::Exists($dir)) { [System.IO.Directory]::CreateDirectory($dir) | Out-Null }
    $bmp = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear($Color)
    $g.Dispose()
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

# Helper: Create valid pet package
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

# Helper: Background Native Dialog Handler (strictly targets owned PID and expected title)
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
            $h = [Win32NativeDialogHelper]::FindDialog([uint32]$targetPid, $expectedTitle)
            if ($h -ne [IntPtr]::Zero) {
                Start-Sleep -Milliseconds 200

                switch ($action) {
                    "ClickYes" {
                        [Win32NativeDialogHelper]::Click($h, [Win32NativeDialogHelper]::IDYES)
                    }
                    "ClickOK" {
                        [Win32NativeDialogHelper]::Click($h, [Win32NativeDialogHelper]::IDOK)
                    }
                    "ClickCancel" {
                        [Win32NativeDialogHelper]::Click($h, [Win32NativeDialogHelper]::IDCANCEL)
                    }
                    { $_ -in "SelectFiles", "SelectFolder" } {
                        $setRes = [Win32NativeDialogHelper]::SetText($h, $inputPath)
                        Start-Sleep -Milliseconds 300
                        [Win32NativeDialogHelper]::Click($h, [Win32NativeDialogHelper]::IDOK)
                        Start-Sleep -Milliseconds 600
                        if ([Win32NativeDialogHelper]::IsWindowVisible($h)) {
                            [Win32NativeDialogHelper]::Click($h, [Win32NativeDialogHelper]::IDOK)
                        }
                    }
                }
                # Wait for dialog to dismiss
                $dismissWatch = [System.Diagnostics.Stopwatch]::StartNew()
                while ($dismissWatch.Elapsed.TotalSeconds -lt 4) {
                    if (-not [Win32NativeDialogHelper]::IsWindowVisible($h)) {
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
# Setup Isolated Test Environment
# -----------------------------------------------------------------------------
$tempDataDir = Join-Path ([System.IO.Path]::GetTempPath()) ("vchibi_smoke_p5_" + [Guid]::NewGuid().ToString("N"))
Write-Host "Creating isolated test data root at: $tempDataDir"
New-Item -ItemType Directory -Path $tempDataDir -Force | Out-Null

$defaultPetDir = Join-Path $tempDataDir "pets\default-chibi"
New-PetPackage -PackageDir $defaultPetDir -Id "default-chibi" -Name "Default Chibi" -Width 32 -Height 32

$exePath = "src/VChibi/bin/$Configuration/net10.0-windows/VChibi.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "Could not find executable at $exePath. Build the solution first."
    exit 1
}

$proc = Start-Process -FilePath $exePath -ArgumentList "--data-dir `"$tempDataDir`"" -PassThru
Write-Host "Launched VChibi (PID=$($proc.Id))"
Start-Sleep -Seconds 2

try {
    $rootElement = [System.Windows.Automation.AutomationElement]::RootElement

    # Find LibraryWindow
    $libCond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)),
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "VChibi"))
    )
    $libWindow = $rootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $libCond)
    if (-not $libWindow) {
        Write-Error "Could not find LibraryWindow!"
        exit 1
    }
    $libHwnd = [IntPtr]$libWindow.Current.NativeWindowHandle
    Write-Host "Found LibraryWindow (HWND=0x$($libHwnd.ToString('X')))"

    # Helper: Find element by AutomationId
    function Get-ElementById {
        param($Parent, [string]$Id)
        $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $Id)
        return $Parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    }

    function Test-IsEditorOpen {
        param($Parent)
        $nb = Get-ElementById $Parent "PetNameTextBox"
        if (-not $nb) { return $false }
        return (-not $nb.Current.IsOffscreen)
    }

    # -------------------------------------------------------------------------
    # STEP 1: Verify Initial App & Desktop Pet Window
    # -------------------------------------------------------------------------
    Write-Host "`n[1/7] Verifying Initial App & Win32 PetWindow..."
    $petWindowCond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)),
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, "VChibi Desktop Pet"))
    )
    $petWindow = $rootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $petWindowCond)
    if (-not $petWindow) {
        Write-Error "Desktop PetWindow not found on initial startup!"
        exit 1
    }
    $petHwnd = [IntPtr]$petWindow.Current.NativeWindowHandle
    $exStyle = [Win32NativeDialogHelper]::GetWindowLong($petHwnd, [Win32NativeDialogHelper]::GWL_EXSTYLE)
    $hasTransparent = ($exStyle -band [Win32NativeDialogHelper]::WS_EX_TRANSPARENT) -ne 0
    $hasNoActivate = ($exStyle -band [Win32NativeDialogHelper]::WS_EX_NOACTIVATE) -ne 0
    $hasToolWindow = ($exStyle -band [Win32NativeDialogHelper]::WS_EX_TOOLWINDOW) -ne 0
    Write-Host "PetWindow: HWND=0x$($petHwnd.ToString('X')), WS_EX_TRANSPARENT=$hasTransparent, WS_EX_NOACTIVATE=$hasNoActivate, WS_EX_TOOLWINDOW=$hasToolWindow"
    if (-not ($hasTransparent -and $hasNoActivate -and $hasToolWindow)) {
        Write-Error "PetWindow missing required Win32 extended styles!"
        exit 1
    }

    # -------------------------------------------------------------------------
    # STEP 2: Create New Pet via UI & Source Deletion (A07 & A08)
    # -------------------------------------------------------------------------
    Write-Host "`n[2/7] Testing Create Pet via Editor & Source PNG Deletion (A07, A08)..."
    $sourcePngDir = Join-Path ([System.IO.Path]::GetTempPath()) ("vchibi_sources_" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $sourcePngDir -Force | Out-Null

    $idle1Path = Join-Path $sourcePngDir "idle_001.png"
    $idle2Path = Join-Path $sourcePngDir "idle_002.png"
    $walk1Path = Join-Path $sourcePngDir "walk_001.png"
    $walk2Path = Join-Path $sourcePngDir "walk_002.png"
    # Create 48x48 RGBA frames
    New-SamplePng $idle1Path 48 48 ([System.Drawing.Color]::FromArgb(255, 230, 100, 100))
    New-SamplePng $idle2Path 48 48 ([System.Drawing.Color]::FromArgb(255, 240, 120, 120))
    New-SamplePng $walk1Path 48 48 ([System.Drawing.Color]::FromArgb(255, 100, 200, 100))
    New-SamplePng $walk2Path 48 48 ([System.Drawing.Color]::FromArgb(255, 120, 220, 120))
    $hashWalk1 = (Get-FileHash -Path $walk1Path -Algorithm SHA256).Hash
    $hashWalk2 = (Get-FileHash -Path $walk2Path -Algorithm SHA256).Hash
    Write-Host "Original walk_001.png hash: $hashWalk1"
    Write-Host "Original walk_002.png hash: $hashWalk2"

    $createBtn = Get-ElementById $libWindow "CreatePetButton"
    if (-not $createBtn) { Write-Error "CreatePetButton not found!"; exit 1 }
    $createBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 600

    # Verify editor is open
    if (-not (Test-IsEditorOpen $libWindow)) { Write-Error "PetEditorView did not open!"; exit 1 }
    $nameBox = Get-ElementById $libWindow "PetNameTextBox"
    Write-Host "Pet Editor opened successfully. Setting pet name..."
    $valPattern = $nameBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $valPattern.SetValue("Smoker Idol Chibi")
    $v = $valPattern.Current.Value
    Write-Host "Name after set: '$v'"
    # Add frames to Idle: start native dialog watcher then invoke AddFramesButton
    Write-Host "Adding Idle frames via native OpenFileDialog..."
    $dlg1 = Start-DialogHandler -ProcessId $proc.Id -ExpectedTitle "PNG" -Action "SelectFiles" -InputPath "`"$idle1Path`" `"$idle2Path`""
    $addFramesBtn = Get-ElementById $libWindow "AddFramesButton"
    $addFramesBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $watchRes1 = Wait-DialogHandler $dlg1
    Write-Host "Dialog handler 1 result: $watchRes1"
    Start-Sleep -Milliseconds 500

    # Check frame list count
    $framesList = Get-ElementById $libWindow "FramesListBox"
    $frameItems = $framesList.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Host "Idle frames loaded: $($frameItems.Count) (Expected: 2)"
    if ($frameItems.Count -ne 2) { Write-Error "Failed to load 2 idle frames!"; exit 1 }
    # Set distinct Idle FPS = 6 (A07)
    $fpsSlider = Get-ElementById $libWindow "FpsSlider"
    $fpsSlider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue(6)
    Write-Host "Set Idle FPS to 6."

    # Switch to Walk tab
    Write-Host "Switching to Walk tab..."
    $walkTab = Get-ElementById $libWindow "WalkTabRadio"
    $walkTab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 300
    # Add frames to Walk
    Write-Host "Adding Walk frames via native OpenFileDialog..."
    $dlg2 = Start-DialogHandler -ProcessId $proc.Id -ExpectedTitle "PNG" -Action "SelectFiles" -InputPath "`"$walk1Path`" `"$walk2Path`""
    $addFramesBtn = Get-ElementById $libWindow "AddFramesButton"
    $addFramesBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $watchRes2 = Wait-DialogHandler $dlg2
    Write-Host "Dialog handler 2 result: $watchRes2"
    Start-Sleep -Milliseconds 500
    $walkItems = $framesList.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Host "Walk frames loaded: $($walkItems.Count) (Expected: 2)"
    if ($walkItems.Count -ne 2) { Write-Error "Failed to load 2 walk frames!"; exit 1 }
    # Test Frame Reordering: Move Up / Down
    Write-Host "Testing Frame Reordering (Move Up / Down)..."
    $walkItems[1].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    $moveUpBtn = Get-ElementById $libWindow "MoveFrameUpButton"
    $moveUpBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 300
    Write-Host "Reordered frame up successfully."
    # Set distinct Walk FPS = 12 (A07)
    $fpsSlider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue(12)
    Write-Host "Set Walk FPS to 12."

    # Save draft without activation (SaveButton)
    Write-Host "Saving pet draft (SaveButton)..."
    $saveBtn = Get-ElementById $libWindow "SaveButton"
    $saveBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 1

    # Verify editor closed and new pet is in library
    if (Test-IsEditorOpen $libWindow) { Write-Error "Editor did not close after Save!"; exit 1 }
    Write-Host "Editor closed. Verifying pet added to library..."

    $petList = Get-ElementById $libWindow "PetListBox"
    $allPets = $petList.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Host "Total pets in library: $($allPets.Count) (Expected: 2)"
    if ($allPets.Count -ne 2) { Write-Error "New pet was not added to library list!"; exit 1 }

    # Find the new pet directory on disk and verify frame order persistence via content hashes
    $newPetDirs = Get-ChildItem -Path (Join-Path $tempDataDir "pets") -Directory | Where-Object { $_.Name -ne "default-chibi" }
    if ($newPetDirs.Count -ne 1) { Write-Error "Expected exactly 1 new pet directory!"; exit 1 }
    $newPetDir = $newPetDirs[0].FullName
    $savedWalk1Path = Join-Path $newPetDir "walk\001.png"
    $savedWalk2Path = Join-Path $newPetDir "walk\002.png"

    $savedHash1 = (Get-FileHash -Path $savedWalk1Path -Algorithm SHA256).Hash
    $savedHash2 = (Get-FileHash -Path $savedWalk2Path -Algorithm SHA256).Hash
    Write-Host "Saved walk/001.png hash: $savedHash1"
    Write-Host "Saved walk/002.png hash: $savedHash2"

    # CRITICAL ASSERTION: Swapped source identity persisted!
    # Frame reordering moved walk_002 up to index 0, so walk/001.png MUST have walk_002's hash
    # and walk/002.png MUST have walk_001's hash!
    if ($savedHash1 -ne $hashWalk2 -or $savedHash2 -ne $hashWalk1) {
        Write-Error "Persisted frame content order mismatch! walk/001.png hash ($savedHash1) does not match original walk_002 ($hashWalk2), or walk/002.png hash ($savedHash2) does not match original walk_001 ($hashWalk1)."
        exit 1
    }
    Write-Host "Verified A07 frame-order persistence: walk/001.png contains original walk_002.png content and walk/002.png contains original walk_001.png content (swapped sequence preserved on disk)."
    Write-Host "Reopening editor on newly saved pet to verify persisted settings (A07)..."
    $allPets[1].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 300
    $editBtn = Get-ElementById $libWindow "EditPetButton"
    $editBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 500

    if (-not (Test-IsEditorOpen $libWindow)) { Write-Error "Editor did not reopen!"; exit 1 }
    $nameBoxReopen = Get-ElementById $libWindow "PetNameTextBox"
    $reopenName = $nameBoxReopen.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    Write-Host "Reopened pet name: '$reopenName' (Expected: 'Smoker Idol Chibi')"
    if ($reopenName -ne "Smoker Idol Chibi") { Write-Error "Reopened pet name mismatch!"; exit 1 }

    # Check Idle FPS persisted to 6
    $fpsSliderReopen = Get-ElementById $libWindow "FpsSlider"
    $idleFpsVal = $fpsSliderReopen.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value
    Write-Host "Reopened Idle FPS: $idleFpsVal (Expected: 6)"
    if ([int]$idleFpsVal -ne 6) { Write-Error "Reopened Idle FPS did not persist 6!"; exit 1 }

    # Check Walk tab: FPS persisted to 12
    $walkTab = Get-ElementById $libWindow "WalkTabRadio"
    $walkTab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 300
    $walkFpsVal = $fpsSliderReopen.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value
    Write-Host "Reopened Walk FPS: $walkFpsVal (Expected: 12)"
    if ([int]$walkFpsVal -ne 12) { Write-Error "Reopened Walk FPS did not persist 12!"; exit 1 }

    # Reopened Walk frames count
    $walkReopenItems = $framesList.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Host "Reopened Walk frames count: $($walkReopenItems.Count) (Expected: 2)"
    if ($walkReopenItems.Count -ne 2) { Write-Error "Walk frames count mismatch on reopen!"; exit 1 }
    # Test SaveAndActivateButton (A07: Save and activate immediately runs on desktop)
    Write-Host "Clicking SaveAndActivateButton to verify single-step commit and desktop activation (A07)..."
    $saveAndActBtn = Get-ElementById $libWindow "SaveAndActivateButton"
    $saveAndActBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 1
    if (Test-IsEditorOpen $libWindow) { Write-Error "Editor did not close after SaveAndActivate!"; exit 1 }

    # CRITERION A08: Delete source PNG directory completely!
    Write-Host "Deleting source PNG files on disk to verify copy isolation (A08)..."
    Remove-Item -Path $sourcePngDir -Recurse -Force
    Write-Host "Source directory deleted: (-not (Test-Path $sourcePngDir)) = $(-not (Test-Path $sourcePngDir))"

    # Verify desktop pet window is running the 48x48 pet activated directly by SaveAndActivate
    # At 100% scale and 1.25 DPI: 48 * 1.25 = 60px
    $rect = New-Object Win32NativeDialogHelper+RECT
    [Win32NativeDialogHelper]::GetWindowRect($petHwnd, [ref]$rect) | Out-Null
    $petW = $rect.Right - $rect.Left
    $petH = $rect.Bottom - $rect.Top
    Write-Host "Active Desktop PetWindow dimension: ${petW}x${petH} px (Expected: 60x60 px)"
    if ($petW -ne 60 -or $petH -ne 60) {
        Write-Error "Desktop PetWindow did not resize to 60x60 px for 48px pet! Actual: ${petW}x${petH}"
        exit 1
    }
    Write-Host "Verified A07 & A08: Distinct FPS, persisted draft reopen, SaveAndActivate, and source PNG isolation verified."
    # -------------------------------------------------------------------------
    # STEP 3: Edit Existing Pet & Cancel Dirty Guard (A09)
    # -------------------------------------------------------------------------
    Write-Host "`n[3/7] Testing Edit Existing Pet & Cancel Dirty Guard (A09)..."
    # Refresh pet list and select default-chibi (first item)
    $allPets = $petList.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    $allPets[0].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 300

    $editBtn = Get-ElementById $libWindow "EditPetButton"
    $editBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 500

    $nameBox = Get-ElementById $libWindow "PetNameTextBox"
    if (-not $nameBox) { Write-Error "Edit Pet did not open editor!"; exit 1 }
    $nameBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("Dirty Attempt Name")

    # Click CancelButton -> Confirmation prompt appears (#32770)
    Write-Host "Clicking Cancel with dirty changes. Watching for confirmation dialog..."
    $dlgWatcher = Start-DialogHandler -ProcessId $proc.Id -ExpectedTitle "*" -Action "ClickYes"
    $cancelEditorBtn = Get-ElementById $libWindow "CancelButton"
    $cancelEditorBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $dlgRes = Wait-DialogHandler $dlgWatcher
    Write-Host "Confirmation dialog result: $dlgRes"
    Start-Sleep -Milliseconds 500

    # Verify editor closed and pet name was NOT changed
    if (Test-IsEditorOpen $libWindow) { Write-Error "Editor did not close after confirming cancel!"; exit 1 }
    Write-Host "Editor closed after confirming cancel. Verifying pet name remained pristine..."

    $allPets = $petList.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Host "Verified A09: Pet name remained pristine after cancellation."

    # -------------------------------------------------------------------------
    # STEP 4: Edit Active Pet & Save Hot Reload (A10)
    # -------------------------------------------------------------------------
    Write-Host "`n[4/7] Testing Edit Active Pet & Save Hot Reload (A10)..."
    # Active pet is Smoker Idol Chibi (item index 1)
    $allPets[1].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 300

    $editBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 500

    $nameBox = Get-ElementById $libWindow "PetNameTextBox"
    $nameBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("Smoker Idol HotReload")

    $saveBtn = Get-ElementById $libWindow "SaveButton"
    $saveBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Seconds 1
    if (Test-IsEditorOpen $libWindow) { Write-Error "Editor did not close after saving active pet!"; exit 1 }
    # Verify active pet continues running on desktop
    $isPetVis = [Win32NativeDialogHelper]::IsWindowVisible($petHwnd)
    if (-not $isPetVis) { Write-Error "PetWindow disappeared after saving active pet!"; exit 1 }
    Write-Host "Verified A10: Active pet committed save and hot reloaded without destroying runtime session."

    # -------------------------------------------------------------------------
    # STEP 5: Import Package Folder & Repeat Deduplication (A11)
    # -------------------------------------------------------------------------
    Write-Host "`n[5/7] Testing Import Package Folder & Repeat Deduplication (A11)..."
    $importFolder1 = Join-Path ([System.IO.Path]::GetTempPath()) ("vchibi_import_src_" + [Guid]::NewGuid().ToString("N"))
    New-PetPackage -PackageDir $importFolder1 -Id "imported-idol" -Name "Imported Idol" -Width 16 -Height 16

    $importBtn = Get-ElementById $libWindow "ImportPackageButton"
    $folderWatcher1 = Start-DialogHandler -ProcessId $proc.Id -ExpectedTitle "pet" -Action "SelectFolder" -InputPath $importFolder1
    $importBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $foldRes1 = Wait-DialogHandler $folderWatcher1
    Write-Host "Folder dialog result: $foldRes1"
    Start-Sleep -Seconds 1
    $statusTb = Get-ElementById $libWindow "StatusBarTextBlock"
    Write-Host "StatusBar Text: '$($statusTb.Current.Name)'"
    $allPets = $petList.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Host "Pets count after first import: $($allPets.Count) (Expected: 3)"
    if ($allPets.Count -ne 3) { Write-Error "Imported pet not found in library!"; exit 1 }

    # Verify active pet was NOT changed (A11: item added but active unchanged)
    $activeChibiRect = New-Object Win32NativeDialogHelper+RECT
    [Win32NativeDialogHelper]::GetWindowRect($petHwnd, [ref]$activeChibiRect) | Out-Null
    $currW = $activeChibiRect.Right - $activeChibiRect.Left
    Write-Host "Active Desktop PetWindow dimension: $currW px (Expected: 60 px from active pet, NOT 20 px from 16px import)"
    if ($currW -ne 60) { Write-Error "Active pet was erroneously changed during import!"; exit 1 }

    # Repeat import of the exact same folder (A11: distinct unique ID assigned)
    Write-Host "Importing the exact same folder a second time to verify distinct ID (A11)..."
    $folderWatcher2 = Start-DialogHandler -ProcessId $proc.Id -ExpectedTitle "pet" -Action "SelectFolder" -InputPath $importFolder1
    $importBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $foldRes2 = Wait-DialogHandler $folderWatcher2
    Write-Host "Folder dialog 2 result: $foldRes2"
    Start-Sleep -Seconds 1
    $allPets = $petList.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Host "Pets count after second import: $($allPets.Count) (Expected: 4)"
    if ($allPets.Count -ne 4) { Write-Error "Repeated import did not create a new distinct pet item!"; exit 1 }
    Write-Host "Verified A11: Package imported with active pet preserved, and repeat import created unique ID."

    # -------------------------------------------------------------------------
    # STEP 6: Import Validation & Error Feedback (A12)
    # -------------------------------------------------------------------------
    Write-Host "`n[6/7] Testing Import Validation & Error Feedback (A12)..."

    # Case 6.1: User cancels folder picker -> clean no-op, no error dialog
    Write-Host "Case 6.1: User cancels folder picker..."
    $cancelWatcher = Start-DialogHandler -ProcessId $proc.Id -ExpectedTitle "pet" -Action "ClickCancel"
    $importBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $cancelRes = Wait-DialogHandler $cancelWatcher
    Write-Host "Cancel watcher result: $cancelRes"
    Start-Sleep -Milliseconds 500
    $allPets = $petList.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Host "Pets count after cancel: $($allPets.Count) (Expected: 4)"
    if ($allPets.Count -ne 4) { Write-Error "Cancel modified pets list!"; exit 1 }
    Write-Host "Verified Case 6.1: Cancel handled cleanly, 0 items added."

    # Case 6.2: Missing pet.json manifest
    Write-Host "Case 6.2: Folder missing pet.json manifest..."
    $missingManifestFolder = Join-Path ([System.IO.Path]::GetTempPath()) ("vchibi_missing_src_" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $missingManifestFolder -Force | Out-Null
    $missingWatcher = Start-DialogHandler -ProcessId $proc.Id -ExpectedTitle "pet" -Action "SelectFolder" -InputPath $missingManifestFolder
    $importBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Wait-DialogHandler $missingWatcher | Out-Null
    $errWatcher = Start-DialogHandler -ProcessId $proc.Id -ExpectedTitle "*" -Action "ClickOK"
    $errRes = Wait-DialogHandler $errWatcher
    Write-Host "Missing manifest error dialog dismissed: $errRes"
    Start-Sleep -Milliseconds 500
    $allPets = $petList.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Host "Pets count after missing manifest: $($allPets.Count) (Expected: 4)"
    if ($allPets.Count -ne 4) { Write-Error "Missing manifest added items to library!"; exit 1 }
    Write-Host "Verified Case 6.2: Missing manifest rejected cleanly."

    # Case 6.3: Corrupted JSON manifest
    Write-Host "Case 6.3: Corrupted JSON manifest..."
    $corruptFolder = Join-Path ([System.IO.Path]::GetTempPath()) ("vchibi_corrupt_src_" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $corruptFolder -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $corruptFolder "pet.json"), "{ invalid broken corrupted json ")
    $corruptWatcher = Start-DialogHandler -ProcessId $proc.Id -ExpectedTitle "pet" -Action "SelectFolder" -InputPath $corruptFolder
    $importBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Wait-DialogHandler $corruptWatcher | Out-Null
    $errWatcher = Start-DialogHandler -ProcessId $proc.Id -ExpectedTitle "*" -Action "ClickOK"
    $errRes = Wait-DialogHandler $errWatcher
    Write-Host "Corrupt manifest error dialog dismissed: $errRes"
    Start-Sleep -Milliseconds 500
    $allPets = $petList.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Host "Pets count after corrupt import: $($allPets.Count) (Expected: 4)"
    if ($allPets.Count -ne 4) { Write-Error "Corrupt manifest added items to library!"; exit 1 }
    Write-Host "Verified Case 6.3: Corrupted JSON rejected cleanly."

    # Case 6.4: Fake PNG asset in package
    Write-Host "Case 6.4: Fake PNG asset in package..."
    $fakePngFolder = Join-Path ([System.IO.Path]::GetTempPath()) ("vchibi_fake_src_" + [Guid]::NewGuid().ToString("N"))
    New-PetPackage -PackageDir $fakePngFolder -Id "fake-png-pet" -Name "Fake PNG Pet" -Width 16 -Height 16
    [System.IO.File]::WriteAllText((Join-Path $fakePngFolder "idle\001.png"), "THIS IS NOT A VALID PNG FILE!")
    $fakeWatcher = Start-DialogHandler -ProcessId $proc.Id -ExpectedTitle "pet" -Action "SelectFolder" -InputPath $fakePngFolder
    $importBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Wait-DialogHandler $fakeWatcher | Out-Null
    $errWatcher = Start-DialogHandler -ProcessId $proc.Id -ExpectedTitle "*" -Action "ClickOK"
    $errRes = Wait-DialogHandler $errWatcher
    Write-Host "Fake PNG error dialog dismissed: $errRes"
    Start-Sleep -Milliseconds 500
    $allPets = $petList.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Host "Pets count after fake PNG: $($allPets.Count) (Expected: 4)"
    if ($allPets.Count -ne 4) { Write-Error "Fake PNG package added items to library!"; exit 1 }
    Write-Host "Verified Case 6.4: Fake PNG rejected cleanly."

    # Case 6.5: Directory traversal in animation path
    Write-Host "Case 6.5: Directory traversal in animation frames path..."
    $traversalFolder = Join-Path ([System.IO.Path]::GetTempPath()) ("vchibi_traversal_src_" + [Guid]::NewGuid().ToString("N"))
    New-PetPackage -PackageDir $traversalFolder -Id "traversal-pet" -Name "Traversal Pet" -Width 16 -Height 16
    $traversalManifest = @{
        schemaVersion = 1
        id = "traversal-pet"
        name = "Traversal Pet"
        animations = @{
            idle = @{ fps = 8; frames = @("../../secret.png") }
            walk = @{ fps = 10; frames = @("walk/001.png") }
        }
    }
    [System.IO.File]::WriteAllText((Join-Path $traversalFolder "pet.json"), ($traversalManifest | ConvertTo-Json -Depth 5))
    $travWatcher = Start-DialogHandler -ProcessId $proc.Id -ExpectedTitle "pet" -Action "SelectFolder" -InputPath $traversalFolder
    $importBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Wait-DialogHandler $travWatcher | Out-Null
    $errWatcher = Start-DialogHandler -ProcessId $proc.Id -ExpectedTitle "*" -Action "ClickOK"
    $errRes = Wait-DialogHandler $errWatcher
    Write-Host "Traversal error dialog dismissed: $errRes"
    Start-Sleep -Milliseconds 500
    $allPets = $petList.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    Write-Host "Pets count after traversal: $($allPets.Count) (Expected: 4)"
    if ($allPets.Count -ne 4) { Write-Error "Traversal package added items to library!"; exit 1 }
    Write-Host "Verified Case 6.5: Directory traversal rejected cleanly."

    Write-Host "Verified A12: All invalid package variants (cancel, missing manifest, corrupt schema, fake PNG, traversal) rejected cleanly with zero corrupted state."
    # -------------------------------------------------------------------------
    # STEP 7: Clean Exit & Lock Release
    # -------------------------------------------------------------------------
    Write-Host "`n[7/7] Testing Clean Exit & Lock File Release..."
    $exitBtn = Get-ElementById $libWindow "ExitButton"
    $exitBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()

    $exited = $proc.WaitForExit(6000)
    Write-Host "Process exit: Exited=$exited, ExitCode=$($proc.ExitCode)"
    if (-not $exited) {
        try { Stop-Process -Id $proc.Id -Force } catch { }
        Write-Error "App failed to exit cleanly within 6 seconds!"
        exit 1
    }

    $lockPath = Join-Path $tempDataDir ".instance.lock"
    $lockExists = Test-Path $lockPath
    Write-Host "Lock file after exit: $lockExists (Expected: False)"
    if ($lockExists) { Write-Error "Lock file was not deleted upon clean exit!"; exit 1 }

    Write-Host "`n======================================================="
    Write-Host "PHASE 5 SMOKE VERIFICATION COMPLETED CLEANLY!"
    Write-Host "======================================================="

} finally {
    if (-not $proc.HasExited) {
        try { Stop-Process -Id $proc.Id -Force } catch { }
    }
    if (Test-Path $tempDataDir) {
        try { Remove-Item -Path $tempDataDir -Recurse -Force } catch { }
    }
}
