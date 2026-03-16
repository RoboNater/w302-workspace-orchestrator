<#
.SYNOPSIS
    Deploy a project workspace — launch all apps, position windows.

.PARAMETER Project
    Name of the project (matches <name>.workspace.yaml in current dir or ~/.workspaces/).

.EXAMPLE
    .\deploy.ps1 -Project sample-project
#>
param(
    [Parameter(Mandatory)][string]$Project
)

$ErrorActionPreference = "Stop"

# Load libraries (Win32.ps1 is dot-sourced transitively by WindowManager.ps1)
. "$PSScriptRoot\lib\WindowManager.ps1"
. "$PSScriptRoot\lib\VirtualDesktop.ps1"
. "$PSScriptRoot\lib\TerminalLauncher.ps1"

# Find and parse config
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
Write-Host "Deploying: $Project" -ForegroundColor Cyan
Write-Host "Config:    $configPath" -ForegroundColor DarkGray
Write-Host ""

$config = Get-Content $configPath -Raw | ConvertFrom-Yaml

$name = $config.meta.name
$apps = $config.applications

# --- Deploy sequence ---

# Step 1: Virtual desktops
if ($config.virtual_desktops) {
    $primary = $config.virtual_desktops.primary
    Write-Host "[1/4] Setting up virtual desktop $primary..." -ForegroundColor White
    Ensure-DesktopCount -Count $primary | Out-Null
}

# Step 2: VS Code
if ($apps.vscode) {
    Write-Host "[2/4] Launching VS Code..." -ForegroundColor White
    $vscodePath = $apps.vscode.workspace
    $pos = $apps.vscode.window.position
    # Use Code.exe directly; code.cmd requires CMD.EXE as interpreter
    $codeExe = "$env:LOCALAPPDATA\Programs\Microsoft VS Code\Code.exe"
    Start-AndPosition -FilePath $codeExe -ArgumentList @($vscodePath) `
        -ProcessName "Code" -TitlePattern "Visual Studio Code" `
        -X $pos.x -Y $pos.y -Width $pos.width -Height $pos.height | Out-Null
}

# Step 3: Windows Terminal
if ($apps.terminals) {
    Write-Host "[3/4] Launching Windows Terminal..." -ForegroundColor White
    $tabs = $apps.terminals.tabs | ForEach-Object {
        @{
            Title     = $_.title
            Directory = $_.directory
            Command   = $_.run_on_deploy
        }
    }
    # Snapshot existing WT windows so we position the newly launched one
    $existingWtHwnds = @(Get-AllWindows | Where-Object { $_.ProcessName -eq "WindowsTerminal" } | ForEach-Object { $_.Hwnd })

    Start-TerminalWithTabs -Tabs $tabs

    # Position after launch
    Start-Sleep -Seconds 2  # crude wait — P1.1 will refine with polling
    $termWin = Find-WindowByProcess -ProcessName "WindowsTerminal" -TimeoutSeconds 10 -ExcludeHwnds $existingWtHwnds
    if ($termWin) {
        $pos = $apps.terminals.window.position
        Move-WindowTo -Hwnd $termWin[0].Hwnd -X $pos.x -Y $pos.y -Width $pos.width -Height $pos.height | Out-Null
    }
}

# Step 4: File Explorer
if ($apps.explorer) {
    Write-Host "[4/4] Launching File Explorer..." -ForegroundColor White
    $pos = $apps.explorer.window.position
    foreach ($path in $apps.explorer.paths) {
        $folderName = Split-Path $path -Leaf
        Start-Process "explorer.exe" -ArgumentList $path
        # Wait specifically for this folder's window by title
        $explorerWin = Find-WindowByProcess -ProcessName "explorer" `
            -TitlePattern ([regex]::Escape($folderName)) -TimeoutSeconds 10
        if ($explorerWin -and $pos) {
            Move-WindowTo -Hwnd $explorerWin[0].Hwnd `
                -X $pos.x -Y $pos.y -Width $pos.width -Height $pos.height | Out-Null
        }
    }
}

Write-Host ""
Write-Host "Deploy complete: $name" -ForegroundColor Green
Write-Host ""
