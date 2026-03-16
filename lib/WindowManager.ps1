<#
.SYNOPSIS
    High-level window management: launch, find, position, close.
    Builds on Win32.ps1 primitives.
#>

. "$PSScriptRoot\Win32.ps1"

if (-not ([System.Management.Automation.PSTypeName]'Win32Msg').Type) {
    Add-Type @"
    using System;
    using System.Runtime.InteropServices;
    public class Win32Msg {
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hwnd, uint Msg, IntPtr wParam, IntPtr lParam);
        public const uint WM_CLOSE = 0x0010;
    }
"@
}

function Start-AndPosition {
    <#
    .SYNOPSIS
        Launch a process, wait for its window, and position it.
    #>
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$ArgumentList = @(),
        [string]$ProcessName,            # expected process name for window discovery
        [string]$TitlePattern = ".*",
        [int]$X = 0,
        [int]$Y = 0,
        [int]$Width = 800,
        [int]$Height = 600,
        [int]$TimeoutSeconds = 15
    )

    # Use process name from file path if not specified
    if (-not $ProcessName) {
        $ProcessName = [System.IO.Path]::GetFileNameWithoutExtension($FilePath)
    }

    Write-Host "  Launching $ProcessName..." -ForegroundColor DarkGray

    $proc = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -PassThru

    Write-Host "  Waiting for window (PID $($proc.Id))..." -ForegroundColor DarkGray

    $windows = Find-WindowByProcess -ProcessName $ProcessName -TitlePattern $TitlePattern -TimeoutSeconds $TimeoutSeconds

    if (-not $windows) {
        Write-Warning "Could not find window for $ProcessName"
        return $null
    }

    $win = $windows | Select-Object -First 1
    Move-WindowTo -Hwnd $win.Hwnd -X $X -Y $Y -Width $Width -Height $Height | Out-Null

    Write-Host "  Positioned $ProcessName at ($X, $Y) ${Width}x${Height}" -ForegroundColor DarkGray

    return $win
}

function Close-WindowGracefully {
    <#
    .SYNOPSIS
        Send WM_CLOSE to a window handle, with optional force-kill fallback.
    .DESCRIPTION
        Sends WM_CLOSE and optionally waits to verify the window actually closed.
        If the window is still visible after GracePeriodMs (e.g. a "close all tabs?"
        dialog appeared), sends a second WM_CLOSE to dismiss the confirmation dialog.
        Uses HWND visibility check rather than Stop-Process so that other windows
        sharing the same process (e.g. multiple WT windows) are not affected.
    .PARAMETER Hwnd
        Window handle to close.
    .PARAMETER Force
        If set, verify the window closed and send a second WM_CLOSE if it didn't.
    .PARAMETER GracePeriodMs
        Milliseconds to wait before the follow-up check. Default: 800.
    #>
    param(
        [Parameter(Mandatory)][IntPtr]$Hwnd,
        [switch]$Force,
        [int]$GracePeriodMs = 800
    )

    [Win32Msg]::SendMessage($Hwnd, [Win32Msg]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null

    if ($Force) {
        Start-Sleep -Milliseconds $GracePeriodMs
        if ([Win32Window]::IsWindowVisible($Hwnd)) {
            # Window survived WM_CLOSE (likely a confirmation dialog appeared).
            # Send WM_CLOSE again to dismiss the dialog.
            [Win32Msg]::SendMessage($Hwnd, [Win32Msg]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        }
    }
}

Write-Verbose "WindowManager functions loaded."
