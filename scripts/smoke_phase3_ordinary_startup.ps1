$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms

$csharpSource = @"
using System;
using System.Runtime.InteropServices;

public class Win32Helper {
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public const int SW_MINIMIZE = 6;
    public const int SW_RESTORE  = 9;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left, Top, Right, Bottom;
    }
}
"@
Add-Type -TypeDefinition $csharpSource

# Pre-flight check: NEVER kill pre-existing instances
$existingVChibi = @(Get-Process VChibi -ErrorAction SilentlyContinue)
if ($existingVChibi.Count -gt 0) {
    $pids = ($existingVChibi | Select-Object -ExpandProperty Id) -join ", "
    Write-Error "A pre-existing VChibi process (PID: $pids) is already running. Aborting smoke test safely without terminating user's session."
    exit 1
}

$exePath = "D:\AShiroru\ProgramCode\Project\Personal\VChibi\src\VChibi\bin\Debug\net10.0-windows\VChibi.exe"
$p = $null

try {
    Write-Host "1. Launching VChibi normally (ordinary startup, NO CLI arguments)..."
    $p = Start-Process $exePath -PassThru
    Start-Sleep -Seconds 2

    if ($p.HasExited) {
        throw "VChibi exited prematurely with code $($p.ExitCode)"
    }

    # 2. Inspect open windows via UIAutomation
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $p.Id
    )
    $windows = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $cond)
    Write-Host "Found $($windows.Count) window(s) for VChibi process PID $($p.Id):"

    $libraryElem = $null
    $petElem = $null

    foreach ($w in $windows) {
        Write-Host " - Window Name: '$($w.Current.Name)', Class: '$($w.Current.ClassName)', HWND: 0x$($w.Current.NativeWindowHandle.ToString('X'))"
        if ($w.Current.Name -eq "VChibi") {
            $libraryElem = $w
        } elseif ($w.Current.ClassName -eq "Window" -or $w.Current.Name -like "*Pet*") {
            $petElem = $w
        }
    }

    if ($null -eq $libraryElem) {
        throw "LibraryWindow not found!"
    }

    if ($null -eq $petElem) {
        throw "PetWindow not found in ordinary startup!"
    }

    $libraryHwnd = [IntPtr]$libraryElem.Current.NativeWindowHandle
    $petHwnd = [IntPtr]$petElem.Current.NativeWindowHandle

    Write-Host "Ordinary startup verified: BOTH LibraryWindow AND Desktop PetWindow are active on desktop!"

    # 3. Observe initial Pet position
    $rectInitial = New-Object Win32Helper+RECT
    [Win32Helper]::GetWindowRect($petHwnd, [ref]$rectInitial)
    Write-Host "Initial Pet Rect: Left=$($rectInitial.Left), Top=$($rectInitial.Top), Right=$($rectInitial.Right), Bottom=$($rectInitial.Bottom)"

    # 4. Management Window Minimize: Minimizing LibraryWindow must NOT hide PetWindow
    Write-Host "4. Minimizing LibraryWindow (management window)..."
    [Win32Helper]::ShowWindow($libraryHwnd, [Win32Helper]::SW_MINIMIZE)
    Start-Sleep -Milliseconds 600

    $petStillVisible = [Win32Helper]::IsWindowVisible($petHwnd)
    Write-Host "PetWindow visible while LibraryWindow is minimized: $petStillVisible"
    if (-not $petStillVisible) {
        throw "PetWindow was incorrectly hidden when LibraryWindow was minimized!"
    }
    Write-Host "-> [PASS] PetWindow remains visible when LibraryWindow is minimized."

    # Observe movement while minimized over 2 seconds
    Start-Sleep -Seconds 2
    $rectMinimized = New-Object Win32Helper+RECT
    [Win32Helper]::GetWindowRect($petHwnd, [ref]$rectMinimized)
    Write-Host "Pet Rect while Library minimized: Left=$($rectMinimized.Left), Top=$($rectMinimized.Top), Right=$($rectMinimized.Right), Bottom=$($rectMinimized.Bottom)"

    # Restore LibraryWindow
    Write-Host "5. Restoring LibraryWindow..."
    [Win32Helper]::ShowWindow($libraryHwnd, [Win32Helper]::SW_RESTORE)
    Start-Sleep -Milliseconds 500

    # 6. Trigger clean exit via ExitButton on LibraryWindow
    Write-Host "6. Invoking ExitCommand on LibraryWindow..."
    $btnCond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        "ExitButton"
    )
    $exitBtn = $libraryElem.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $btnCond)
    if ($null -ne $exitBtn) {
        $invokePat = $exitBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $invokePat.Invoke()
    } else {
        Write-Host "ExitButton not found, closing main window..."
        $winPat = $libraryElem.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
        $winPat.Close()
    }

    $cleanExit = $p.WaitForExit(5000)
    Write-Host "Process exited cleanly: $cleanExit, ExitCode: $($p.ExitCode)"

    if (-not $cleanExit) {
        throw "Process did not exit cleanly within 5 seconds!"
    }

    # 7. Check AppLock file was cleaned up on exit
    $lockFile = [System.IO.Path]::Combine($env:LOCALAPPDATA, "VChibi", ".instance.lock")
    if (Test-Path $lockFile) {
        $lockStream = [System.IO.File]::Open($lockFile, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $lockStream.Dispose()
        Write-Host "AppLock file lock verified released cleanly!"
    } else {
        Write-Host "AppLock file verified cleaned up on exit!"
    }

    Write-Host "ALL ORDINARY STARTUP, MANAGEMENT MINIMIZE, AND LIFECYCLE CHECKS PASSED!"
}
finally {
    if ($null -ne $p -and -not $p.HasExited) {
        try { Stop-Process -Id $p.Id -Force } catch { }
    }
}
exit 0
