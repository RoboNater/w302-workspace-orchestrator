<#
.SYNOPSIS
    PowerShell bridge for virtual desktop operations, called by VirtualDesktopService.cs.

.DESCRIPTION
    Wraps the MScholtes VirtualDesktop module so that C# code can perform virtual desktop
    operations without replicating the module's undocumented COM interface GUIDs.
    All output is plain text (integer or nothing). Exit code 0 = success, 1 = error.

.PARAMETER Op
    Operation name: EnsureDesktopCount | MoveWindowToDesktop | SwitchToDesktop |
                    GetWindowDesktopIndex | GetDesktopCount

.PARAMETER Count
    Used by EnsureDesktopCount. Number of desktops to ensure.

.PARAMETER Hwnd
    Window handle (integer) for MoveWindowToDesktop and GetWindowDesktopIndex.

.PARAMETER DesktopIndex
    0-based desktop index for MoveWindowToDesktop and SwitchToDesktop.

.EXAMPLE
    pwsh -NonInteractive -NoProfile -File VirtualDesktopBridge.ps1 -Op GetDesktopCount
    # Output: 3

    pwsh -NonInteractive -NoProfile -File VirtualDesktopBridge.ps1 -Op EnsureDesktopCount -Count 3

    pwsh -NonInteractive -NoProfile -File VirtualDesktopBridge.ps1 -Op MoveWindowToDesktop -Hwnd 123456 -DesktopIndex 1

    pwsh -NonInteractive -NoProfile -File VirtualDesktopBridge.ps1 -Op SwitchToDesktop -DesktopIndex 1

    pwsh -NonInteractive -NoProfile -File VirtualDesktopBridge.ps1 -Op GetWindowDesktopIndex -Hwnd 123456
    # Output: 1
#>
param(
    [Parameter(Mandatory)][string]$Op,
    [int]$Count = 1,
    [long]$Hwnd = 0,
    [int]$DesktopIndex = 0
)

$ErrorActionPreference = "Stop"

# Load VirtualDesktop module (already installed: Install-Module VirtualDesktop)
if (-not (Get-Module -ListAvailable -Name VirtualDesktop -ErrorAction SilentlyContinue)) {
    Write-Error "VirtualDesktop module not found. Install with: Install-Module VirtualDesktop -Scope CurrentUser"
    exit 1
}
Import-Module VirtualDesktop -ErrorAction Stop -WarningAction SilentlyContinue

# Load Win32.ps1 for IntPtr conversion helpers (bridge is in lib/, Win32.ps1 is also in lib/)
$win32Script = Join-Path $PSScriptRoot "Win32.ps1"
if (Test-Path $win32Script) {
    . $win32Script
}

function Get-DesktopSafe {
    param([int]$Index)
    $desktop = Get-Desktop $Index
    if (-not $desktop) {
        Write-Error "Desktop index $Index does not exist (count: $(Get-DesktopCount))."
        exit 1
    }
    return $desktop
}

switch ($Op) {
    "GetDesktopCount" {
        Write-Output (Get-DesktopCount)
    }

    "EnsureDesktopCount" {
        while ((Get-DesktopCount) -lt $Count) {
            New-Desktop | Out-Null
        }
        # no output needed
    }

    "MoveWindowToDesktop" {
        $hwndPtr = [IntPtr]$Hwnd
        $desktop = Get-DesktopSafe -Index $DesktopIndex
        Move-Window -Desktop $desktop -Hwnd $hwndPtr | Out-Null
    }

    "SwitchToDesktop" {
        $desktop = Get-DesktopSafe -Index $DesktopIndex
        Switch-Desktop -Desktop $desktop -NoAnimation | Out-Null
    }

    "GetWindowDesktopIndex" {
        $hwndPtr = [IntPtr]$Hwnd
        $desktop = Get-DesktopFromWindow -Hwnd $hwndPtr
        if (-not $desktop) {
            Write-Output "-1"
        } else {
            Write-Output (Get-DesktopIndex -Desktop $desktop)
        }
    }

    default {
        Write-Error "Unknown operation: $Op"
        exit 1
    }
}

exit 0
