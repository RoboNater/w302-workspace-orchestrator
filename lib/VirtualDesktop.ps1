<#
.SYNOPSIS
    Virtual desktop management using the MScholtes VirtualDesktop module.

.DESCRIPTION
    Wraps the MScholtes VirtualDesktop module (Install-Module VirtualDesktop).
    All desktop indices are 0-based throughout this module.

    Usage:
        . .\lib\VirtualDesktop.ps1
        Ensure-DesktopCount -Count 3
        Move-WindowToDesktop -Hwnd $hwnd -DesktopIndex 2
        Switch-ToDesktop -DesktopIndex 2
#>

if (-not (Get-Module -ListAvailable -Name VirtualDesktop -ErrorAction SilentlyContinue)) {
    Write-Warning "VirtualDesktop module not found. Install: Install-Module VirtualDesktop -Scope CurrentUser"
} else {
    Import-Module VirtualDesktop -ErrorAction Stop -WarningAction SilentlyContinue
}

function Ensure-DesktopCount {
    <#
    .SYNOPSIS
        Ensure at least $Count virtual desktops exist, creating extras as needed.
    .OUTPUTS
        The resulting desktop count.
    #>
    param([Parameter(Mandatory)][int]$Count)

    $created = 0
    while ((Get-DesktopCount) -lt $Count) {
        New-Desktop | Out-Null
        $created++
    }

    $now = Get-DesktopCount
    if ($created -gt 0) {
        Write-Host "  Created $created virtual desktop(s); total now $now." -ForegroundColor DarkGray
    } else {
        Write-Host "  Virtual desktop count: $now (already >= $Count)." -ForegroundColor DarkGray
    }
    return $now
}

function Move-WindowToDesktop {
    <#
    .SYNOPSIS
        Move a window to the specified virtual desktop (0-indexed).
    .OUTPUTS
        $true on success, $false if the desktop index is out of range.
    #>
    param(
        [Parameter(Mandatory)][IntPtr]$Hwnd,
        [Parameter(Mandatory)][int]$DesktopIndex
    )

    $desktop = Get-Desktop $DesktopIndex
    if (-not $desktop) {
        Write-Warning "Desktop index $DesktopIndex does not exist (count: $(Get-DesktopCount))."
        return $false
    }

    Move-Window -Desktop $desktop -Hwnd $Hwnd | Out-Null
    return $true
}

function Switch-ToDesktop {
    <#
    .SYNOPSIS
        Switch the active virtual desktop by 0-based index.
    .OUTPUTS
        $true on success, $false if the index is out of range.
    #>
    param([Parameter(Mandatory)][int]$DesktopIndex)

    $desktop = Get-Desktop $DesktopIndex
    if (-not $desktop) {
        Write-Warning "Desktop index $DesktopIndex does not exist (count: $(Get-DesktopCount))."
        return $false
    }

    Switch-Desktop -Desktop $desktop -NoAnimation | Out-Null
    return $true
}

function Get-WindowDesktopIndex {
    <#
    .SYNOPSIS
        Return the 0-based virtual desktop index for a given window handle.
    .OUTPUTS
        Integer index, or -1 if the window is not found on any desktop.
    #>
    param([Parameter(Mandatory)][IntPtr]$Hwnd)

    $desktop = Get-DesktopFromWindow -Hwnd $Hwnd
    if (-not $desktop) { return -1 }
    return Get-DesktopIndex -Desktop $desktop
}

function Get-CurrentDesktopIndex {
    <#
    .SYNOPSIS
        Return the 0-based index of the currently active virtual desktop.
    #>
    return Get-DesktopIndex
}

Write-Verbose "VirtualDesktop functions loaded."
