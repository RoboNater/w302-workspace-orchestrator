<#
.SYNOPSIS
    Stow a project workspace — close all project windows gracefully.

.PARAMETER Project
    Name of the project to stow.

.EXAMPLE
    .\stow.ps1 -Project sample-project
#>
param(
    [Parameter(Mandatory)][string]$Project
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\lib\WindowManager.ps1"

Import-Module powershell-yaml -ErrorAction Stop

$configPath = Join-Path $PSScriptRoot "$Project.workspace.yaml"
if (-not (Test-Path $configPath)) {
    $configPath = Join-Path "$HOME\.workspaces" "$Project.workspace.yaml"
}
if (-not (Test-Path $configPath)) {
    Write-Error "Project config not found: $Project.workspace.yaml"
    exit 1
}

Write-Host ""
Write-Host "Stowing: $Project" -ForegroundColor Cyan
Write-Host ""

$config = Get-Content $configPath -Raw | ConvertFrom-Yaml
$apps = $config.applications

# Close in reverse order of importance (least critical first)

# File Explorer
if ($apps.explorer) {
    Write-Host "  Closing File Explorer windows..." -ForegroundColor DarkGray
    foreach ($path in $apps.explorer.paths) {
        $folderName = Split-Path $path -Leaf
        $wins = Get-AllWindows | Where-Object {
            $_.ProcessName -eq "explorer" -and $_.Title -match [regex]::Escape($folderName)
        }
        foreach ($w in $wins) {
            Close-WindowGracefully -Hwnd $w.Hwnd
        }
    }
}

# Windows Terminal
if ($apps.terminals) {
    Write-Host "  Closing Windows Terminal..." -ForegroundColor DarkGray
    # Phase 1: close the most recent WT window. WM_CLOSE may trigger a
    # "close all tabs?" confirmation in WT, so fall back to Stop-Process
    # after a short grace period if the window is still alive.
    $termWins = Get-AllWindows | Where-Object { $_.ProcessName -eq "WindowsTerminal" }
    if ($termWins) {
        $win = $termWins | Select-Object -Last 1
        Close-WindowGracefully -Hwnd $win.Hwnd -Force
    }
}

# VS Code — optionally leave running (it handles its own state)
if ($apps.vscode) {
    $workspaceName = Split-Path $apps.vscode.workspace -Leaf
    Write-Host "  Closing VS Code ($workspaceName)..." -ForegroundColor DarkGray
    $codeWins = Get-AllWindows | Where-Object {
        $_.ProcessName -eq "Code" -and $_.Title -match [regex]::Escape($workspaceName)
    }
    foreach ($w in $codeWins) {
        Close-WindowGracefully -Hwnd $w.Hwnd
    }
}

Write-Host ""
Write-Host "Stow complete: $($config.meta.name)" -ForegroundColor Green
Write-Host ""
