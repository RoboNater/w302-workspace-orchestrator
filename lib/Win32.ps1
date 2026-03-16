<#
.SYNOPSIS
    Win32 P/Invoke wrappers for window management.

.DESCRIPTION
    Provides managed access to EnumWindows, SetWindowPos, GetWindowRect,
    GetWindowThreadProcessId, and related APIs.

    Usage:
        . .\lib\Win32.ps1
        $windows = Get-AllWindows
        Move-WindowTo -Hwnd $hwnd -X 100 -Y 100 -Width 800 -Height 600
#>

# Add Win32 types via inline C# (guard against re-loading in the same session)
if (-not ([System.Management.Automation.PSTypeName]'Win32Window').Type) {
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;

public class Win32Window {
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hwnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(
        IntPtr hwnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // SetWindowPos flags
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_NOACTIVATE = 0x0010;

    // ShowWindow commands
    public const int SW_HIDE = 0;
    public const int SW_SHOWNORMAL = 1;
    public const int SW_SHOWMINIMIZED = 2;
    public const int SW_SHOWMAXIMIZED = 3;
    public const int SW_RESTORE = 9;
}
"@
} # end Add-Type guard

function Get-AllWindows {
    <#
    .SYNOPSIS
        Enumerate all visible top-level windows.
    .OUTPUTS
        Array of objects with Hwnd, Title, ProcessId, ProcessName, Rect properties.
    #>
    $windows = [System.Collections.Generic.List[PSObject]]::new()

    $callback = [Win32Window+EnumWindowsProc]{
        param([IntPtr]$hwnd, [IntPtr]$lParam)

        if (-not [Win32Window]::IsWindowVisible($hwnd)) { return $true }

        $length = [Win32Window]::GetWindowTextLength($hwnd)
        if ($length -eq 0) { return $true }

        $sb = [System.Text.StringBuilder]::new($length + 1)
        [Win32Window]::GetWindowText($hwnd, $sb, $sb.Capacity) | Out-Null
        $title = $sb.ToString()

        $procId = [uint32]0
        [Win32Window]::GetWindowThreadProcessId($hwnd, [ref]$procId) | Out-Null

        $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue

        $rect = [Win32Window+RECT]::new()
        [Win32Window]::GetWindowRect($hwnd, [ref]$rect) | Out-Null

        # Use script-scope variable to collect results from the callback
        $script:windowCollector.Add([PSCustomObject]@{
            Hwnd        = $hwnd
            Title       = $title
            ProcessId   = $procId
            ProcessName = $proc.ProcessName
            Rect        = @{
                X      = $rect.Left
                Y      = $rect.Top
                Width  = $rect.Right - $rect.Left
                Height = $rect.Bottom - $rect.Top
            }
        })

        return $true
    }

    $script:windowCollector = $windows
    [Win32Window]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null

    return $windows
}

function Find-WindowByProcess {
    <#
    .SYNOPSIS
        Find windows belonging to a specific process, with retry/timeout.
    .PARAMETER ProcessName
        Process name (without .exe).
    .PARAMETER TitlePattern
        Optional regex to match against window title.
    .PARAMETER TimeoutSeconds
        How long to wait for the window to appear.
    .PARAMETER PollIntervalMs
        How often to check (milliseconds).
    #>
    param(
        [Parameter(Mandatory)][string]$ProcessName,
        [string]$TitlePattern = ".*",
        [int]$TimeoutSeconds = 10,
        [int]$PollIntervalMs = 200
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        $all = Get-AllWindows
        $matches = $all | Where-Object {
            $_.ProcessName -eq $ProcessName -and $_.Title -match $TitlePattern
        }

        if ($matches) {
            return $matches
        }

        Start-Sleep -Milliseconds $PollIntervalMs
    }

    Write-Warning "Timed out waiting for window: process=$ProcessName title=$TitlePattern"
    return $null
}

function Move-WindowTo {
    <#
    .SYNOPSIS
        Move and resize a window to exact coordinates.
    #>
    param(
        [Parameter(Mandatory)][IntPtr]$Hwnd,
        [Parameter(Mandatory)][int]$X,
        [Parameter(Mandatory)][int]$Y,
        [Parameter(Mandatory)][int]$Width,
        [Parameter(Mandatory)][int]$Height
    )

    # Restore if minimized
    [Win32Window]::ShowWindow($Hwnd, [Win32Window]::SW_RESTORE) | Out-Null

    $flags = [Win32Window]::SWP_NOZORDER -bor [Win32Window]::SWP_SHOWWINDOW
    $result = [Win32Window]::SetWindowPos($Hwnd, [IntPtr]::Zero, $X, $Y, $Width, $Height, $flags)

    if (-not $result) {
        $err = [System.Runtime.InteropServices.Marshal]::GetLastWin32Error()
        Write-Warning "SetWindowPos failed for hwnd $Hwnd — Win32 error: $err"
    }

    return $result
}

function Get-WindowRect {
    <#
    .SYNOPSIS
        Get the current position and size of a window.
    #>
    param([Parameter(Mandatory)][IntPtr]$Hwnd)

    $rect = [Win32Window+RECT]::new()
    [Win32Window]::GetWindowRect($Hwnd, [ref]$rect) | Out-Null

    return @{
        X      = $rect.Left
        Y      = $rect.Top
        Width  = $rect.Right - $rect.Left
        Height = $rect.Bottom - $rect.Top
    }
}

Write-Verbose "Win32 window management functions loaded."
