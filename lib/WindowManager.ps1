<#
.SYNOPSIS
    High-level window management: launch, find, position, close.
    Builds on Win32.ps1 primitives.
#>

. "$PSScriptRoot\Win32.ps1"

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
    Move-WindowTo -Hwnd $win.Hwnd -X $X -Y $Y -Width $Width -Height $Height

    Write-Host "  Positioned $ProcessName at ($X, $Y) ${Width}x${Height}" -ForegroundColor DarkGray

    return $win
}

function Close-WindowGracefully {
    <#
    .SYNOPSIS
        Send WM_CLOSE to a window handle.
    #>
    param([Parameter(Mandatory)][IntPtr]$Hwnd)

    # WM_CLOSE = 0x0010
    Add-Type @"
    using System;
    using System.Runtime.InteropServices;
    public class Win32Msg {
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hwnd, uint Msg, IntPtr wParam, IntPtr lParam);
        public const uint WM_CLOSE = 0x0010;
    }
"@

    [Win32Msg]::SendMessage($Hwnd, [Win32Msg]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
}

Write-Verbose "WindowManager functions loaded."
