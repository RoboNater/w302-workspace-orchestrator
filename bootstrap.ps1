<#
.SYNOPSIS
    Bootstrap script for the Workspace Orchestrator project.
    Verifies prerequisites, installs missing dependencies, and scaffolds the project structure.

.DESCRIPTION
    Run this script from the directory where you want the project created.
    It will create a workspace-orchestrator/ subdirectory with the full Phase 1 structure.

    Usage:
        pwsh -File bootstrap.ps1
        pwsh -File bootstrap.ps1 -SkipInstall    # only scaffold, don't install anything
        pwsh -File bootstrap.ps1 -ProjectRoot "C:\dev\workspace-orchestrator"

.PARAMETER ProjectRoot
    Where to create the project. Defaults to .\workspace-orchestrator in the current directory.

.PARAMETER SkipInstall
    Skip all installation steps — only check and report, then scaffold.
#>

param(
    [string]$ProjectRoot = (Join-Path $PWD "workspace-orchestrator"),
    [switch]$SkipInstall
)

$ErrorActionPreference = "Continue"

# --- Helpers ---

function Write-Status {
    param([string]$Message, [string]$Status, [string]$Color = "White")
    $symbol = switch ($Status) {
        "OK"      { "[OK]";   $Color = "Green"  }
        "MISSING" { "[MISSING]"; $Color = "Yellow" }
        "FAIL"    { "[FAIL]"; $Color = "Red"    }
        "SKIP"    { "[SKIP]"; $Color = "DarkGray" }
        "INSTALL" { "[INSTALLING]"; $Color = "Cyan" }
        default   { "[$Status]" }
    }
    Write-Host "  $symbol " -ForegroundColor $Color -NoNewline
    Write-Host $Message
}

function Test-CommandExists {
    param([string]$Command)
    $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

# --- Banner ---

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Workspace Orchestrator — Bootstrap" -ForegroundColor Cyan
Write-Host "  Phase 1: Proof of Concept" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# --- Environment Check ---

Write-Host "Checking environment..." -ForegroundColor White
Write-Host ""

# Verify we're on Windows
if ($env:OS -ne "Windows_NT") {
    Write-Host "ERROR: This project requires Windows. WSL2/Linux is not supported." -ForegroundColor Red
    Write-Host "The Win32 APIs this project uses are only available on the Windows desktop." -ForegroundColor Red
    exit 1
}

# Verify we're running PowerShell 7+
$psVersion = $PSVersionTable.PSVersion
if ($psVersion.Major -ge 7) {
    Write-Status "PowerShell $psVersion" "OK"
} else {
    Write-Status "PowerShell $psVersion — version 7+ required" "FAIL"
    if (-not $SkipInstall) {
        Write-Status "Installing PowerShell 7..." "INSTALL"
        winget install Microsoft.PowerShell --accept-package-agreements --accept-source-agreements
        Write-Host ""
        Write-Host "PowerShell 7 installed. Please restart this script using 'pwsh -File bootstrap.ps1'" -ForegroundColor Yellow
        exit 0
    }
}

# Windows version
$winVer = [System.Environment]::OSVersion.Version
$winBuild = $winVer.Build
if ($winBuild -ge 22000) {
    Write-Status "Windows 11 (build $winBuild)" "OK"
} elseif ($winBuild -ge 19041) {
    Write-Status "Windows 10 (build $winBuild) — Windows 11 recommended but may work" "OK"
} else {
    Write-Status "Windows build $winBuild — Windows 11 (22000+) required" "FAIL"
}

# --- Required Tools ---

Write-Host ""
Write-Host "Checking required tools..." -ForegroundColor White
Write-Host ""

# Windows Terminal
if (Test-CommandExists "wt") {
    $wtVersion = (wt --version 2>$null) -join ""
    Write-Status "Windows Terminal $wtVersion" "OK"
} else {
    Write-Status "Windows Terminal — not found" "MISSING"
    if (-not $SkipInstall) {
        Write-Status "Installing Windows Terminal..." "INSTALL"
        winget install Microsoft.WindowsTerminal --accept-package-agreements --accept-source-agreements
    }
}

# VS Code
if (Test-CommandExists "code") {
    $codeVersion = (code --version 2>$null | Select-Object -First 1)
    Write-Status "VS Code $codeVersion" "OK"
} else {
    Write-Status "VS Code — not found (recommended but not required)" "MISSING"
}

# Git
if (Test-CommandExists "git") {
    $gitVersion = (git --version 2>$null)
    Write-Status "$gitVersion" "OK"
} else {
    Write-Status "Git — not found" "MISSING"
    if (-not $SkipInstall) {
        Write-Status "Installing Git..." "INSTALL"
        winget install Git.Git --accept-package-agreements --accept-source-agreements
    }
}

# Node.js (needed for Claude Code)
if (Test-CommandExists "node") {
    $nodeVersion = (node --version 2>$null)
    Write-Status "Node.js $nodeVersion" "OK"
} else {
    Write-Status "Node.js — not found (needed for Claude Code)" "MISSING"
    if (-not $SkipInstall) {
        Write-Status "Installing Node.js LTS..." "INSTALL"
        winget install OpenJS.NodeJS.LTS --accept-package-agreements --accept-source-agreements
    }
}

# Claude Code
if (Test-CommandExists "claude") {
    Write-Status "Claude Code — installed" "OK"
} else {
    Write-Status "Claude Code — not found" "MISSING"
    if (-not $SkipInstall) {
        Write-Status "Installing Claude Code..." "INSTALL"
        npm install -g @anthropic-ai/claude-code
    }
}

# --- PowerShell Modules ---

Write-Host ""
Write-Host "Checking PowerShell modules..." -ForegroundColor White
Write-Host ""

$requiredModules = @(
    @{ Name = "powershell-yaml";   Description = "YAML config parsing" }
    @{ Name = "PSScriptAnalyzer";  Description = "Script linting" }
)

$optionalModules = @(
    @{ Name = "VirtualDesktop";    Description = "Virtual desktop management (MScholtes)" }
    @{ Name = "Pester";            Description = "Test framework" }
)

foreach ($mod in $requiredModules) {
    $installed = Get-Module -ListAvailable -Name $mod.Name -ErrorAction SilentlyContinue
    if ($installed) {
        $ver = ($installed | Select-Object -First 1).Version
        Write-Status "$($mod.Name) $ver — $($mod.Description)" "OK"
    } else {
        Write-Status "$($mod.Name) — $($mod.Description)" "MISSING"
        if (-not $SkipInstall) {
            Write-Status "Installing $($mod.Name)..." "INSTALL"
            Install-Module $mod.Name -Scope CurrentUser -Force -AllowClobber
        }
    }
}

foreach ($mod in $optionalModules) {
    $installed = Get-Module -ListAvailable -Name $mod.Name -ErrorAction SilentlyContinue
    if ($installed) {
        $ver = ($installed | Select-Object -First 1).Version
        Write-Status "$($mod.Name) $ver — $($mod.Description)" "OK"
    } else {
        Write-Status "$($mod.Name) — $($mod.Description) (optional)" "MISSING"
        if (-not $SkipInstall) {
            Write-Status "Installing $($mod.Name)..." "INSTALL"
            Install-Module $mod.Name -Scope CurrentUser -Force -AllowClobber
        }
    }
}

# --- Optional: .NET SDK ---

Write-Host ""
Write-Host "Checking optional tools (C# migration readiness)..." -ForegroundColor White
Write-Host ""

if (Test-CommandExists "dotnet") {
    $dotnetVersion = (dotnet --version 2>$null)
    Write-Status ".NET SDK $dotnetVersion" "OK"
} else {
    Write-Status ".NET SDK — not installed (optional, needed if migrating to C# at Gate 1)" "SKIP"
}

# --- Execution Policy ---

Write-Host ""
Write-Host "Checking execution policy..." -ForegroundColor White
Write-Host ""

$policy = Get-ExecutionPolicy -Scope CurrentUser
if ($policy -in @("RemoteSigned", "Unrestricted", "Bypass")) {
    Write-Status "Execution policy: $policy" "OK"
} else {
    Write-Status "Execution policy: $policy — scripts may be blocked" "MISSING"
    if (-not $SkipInstall) {
        Write-Status "Setting execution policy to RemoteSigned for current user..." "INSTALL"
        Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force
    }
}

# --- Scaffold Project ---

Write-Host ""
Write-Host "Scaffolding project structure..." -ForegroundColor White
Write-Host ""

if (Test-Path $ProjectRoot) {
    Write-Status "Directory $ProjectRoot already exists — skipping scaffold" "SKIP"
    Write-Host ""
    Write-Host "Bootstrap complete." -ForegroundColor Green
    exit 0
}

# Create directory structure
$dirs = @(
    $ProjectRoot
    "$ProjectRoot/.vscode"
    "$ProjectRoot/docs"
    "$ProjectRoot/lib"
    "$ProjectRoot/test"
)

foreach ($dir in $dirs) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

Write-Status "Created directory structure" "OK"

# --- .vscode/settings.json ---

@'
{
  "powershell.cwd": "${workspaceFolder}",
  "powershell.powerShellDefaultVersion": "PowerShell (x64)",
  "files.associations": {
    "*.workspace.yaml": "yaml"
  },
  "editor.formatOnSave": false,
  "terminal.integrated.defaultProfile.windows": "PowerShell",
  "editor.tabSize": 4,
  "files.eol": "\r\n"
}
'@ | Set-Content "$ProjectRoot/.vscode/settings.json" -Encoding UTF8

Write-Status "Created .vscode/settings.json" "OK"

# --- .gitignore ---

@'
# State files (machine-specific)
.state/

# PowerShell module cache
**/Modules/

# User-specific configs with real paths
*.local.yaml

# Editor
.vscode/launch.json
*.code-workspace
'@ | Set-Content "$ProjectRoot/.gitignore" -Encoding UTF8

Write-Status "Created .gitignore" "OK"

# --- lib/Win32.ps1 ---

@'
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

# Add Win32 types via inline C#
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

        $pid = [uint32]0
        [Win32Window]::GetWindowThreadProcessId($hwnd, [ref]$pid) | Out-Null

        $proc = Get-Process -Id $pid -ErrorAction SilentlyContinue

        $rect = [Win32Window+RECT]::new()
        [Win32Window]::GetWindowRect($hwnd, [ref]$rect) | Out-Null

        # Use script-scope variable to collect results from the callback
        $script:windowCollector.Add([PSCustomObject]@{
            Hwnd        = $hwnd
            Title       = $title
            ProcessId   = $pid
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
'@ | Set-Content "$ProjectRoot/lib/Win32.ps1" -Encoding UTF8

Write-Status "Created lib/Win32.ps1 — P/Invoke wrappers" "OK"

# --- lib/WindowManager.ps1 (stub) ---

@'
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
'@ | Set-Content "$ProjectRoot/lib/WindowManager.ps1" -Encoding UTF8

Write-Status "Created lib/WindowManager.ps1 — high-level window ops" "OK"

# --- lib/VirtualDesktop.ps1 (stub) ---

@'
<#
.SYNOPSIS
    Virtual desktop management wrapper.
    Uses the MScholtes VirtualDesktop module if available,
    otherwise provides guidance on manual installation.
#>

$vdModule = Get-Module -ListAvailable -Name VirtualDesktop -ErrorAction SilentlyContinue

if (-not $vdModule) {
    Write-Warning @"
VirtualDesktop module not found.
Install with: Install-Module VirtualDesktop -Scope CurrentUser
See: https://github.com/MScholtes/VirtualDesktop
"@
}

function Ensure-DesktopCount {
    <#
    .SYNOPSIS
        Ensure at least N virtual desktops exist, creating extras as needed.
    #>
    param([Parameter(Mandatory)][int]$Count)

    # TODO: Implement using VirtualDesktop module or COM interop
    # This is the P1.2 deliverable
    Write-Host "  [TODO] Ensure $Count virtual desktops exist" -ForegroundColor Yellow
}

function Move-WindowToDesktop {
    <#
    .SYNOPSIS
        Move a window handle to a specific virtual desktop (0-indexed).
    #>
    param(
        [Parameter(Mandatory)][IntPtr]$Hwnd,
        [Parameter(Mandatory)][int]$DesktopIndex
    )

    # TODO: Implement using VirtualDesktop module or COM interop
    Write-Host "  [TODO] Move window to desktop $DesktopIndex" -ForegroundColor Yellow
}

function Switch-ToDesktop {
    <#
    .SYNOPSIS
        Switch the active virtual desktop.
    #>
    param([Parameter(Mandatory)][int]$DesktopIndex)

    # TODO: Implement
    Write-Host "  [TODO] Switch to desktop $DesktopIndex" -ForegroundColor Yellow
}

Write-Verbose "VirtualDesktop functions loaded."
'@ | Set-Content "$ProjectRoot/lib/VirtualDesktop.ps1" -Encoding UTF8

Write-Status "Created lib/VirtualDesktop.ps1 — virtual desktop stub" "OK"

# --- lib/TerminalLauncher.ps1 (stub) ---

@'
<#
.SYNOPSIS
    Windows Terminal multi-tab launch and command injection.
#>

function Start-TerminalWithTabs {
    <#
    .SYNOPSIS
        Launch Windows Terminal with multiple named tabs in specific directories.
    .PARAMETER Tabs
        Array of hashtables: @{ Title="name"; Directory="path"; Command="optional cmd" }
    .EXAMPLE
        Start-TerminalWithTabs -Tabs @(
            @{ Title="Backend"; Directory="C:\dev\myapp\backend" },
            @{ Title="Frontend"; Directory="C:\dev\myapp\frontend"; Command="npm start" }
        )
    #>
    param(
        [Parameter(Mandatory)][hashtable[]]$Tabs,
        [string]$Shell = "pwsh"
    )

    if ($Tabs.Count -eq 0) { return }

    # Build the wt command line
    # First tab
    $first = $Tabs[0]
    $wtArgs = @("new-tab", "-p", $Shell, "-d", $first.Directory, "--title", $first.Title)

    # Additional tabs
    for ($i = 1; $i -lt $Tabs.Count; $i++) {
        $tab = $Tabs[$i]
        $wtArgs += ";"
        $wtArgs += @("new-tab", "-p", $Shell, "-d", $tab.Directory, "--title", $tab.Title)
    }

    Write-Host "  Launching Windows Terminal with $($Tabs.Count) tabs..." -ForegroundColor DarkGray
    $argString = $wtArgs -join " "
    Write-Verbose "  wt $argString"

    Start-Process "wt" -ArgumentList $wtArgs

    # TODO P1.3: After launch, send commands to specific tabs
    # This requires finding the terminal window and using SendKeys or WT API
    foreach ($tab in $Tabs) {
        if ($tab.Command) {
            Write-Host "  [TODO] Send command to tab '$($tab.Title)': $($tab.Command)" -ForegroundColor Yellow
        }
    }
}

Write-Verbose "TerminalLauncher functions loaded."
'@ | Set-Content "$ProjectRoot/lib/TerminalLauncher.ps1" -Encoding UTF8

Write-Status "Created lib/TerminalLauncher.ps1 — terminal orchestration stub" "OK"

# --- sample-project.workspace.yaml ---

@'
# Sample project context — edit paths to match your machine
# This is what deploy.ps1 reads to set up your workspace.

meta:
  name: "sample-project"
  description: "Example workspace for testing the orchestrator"

virtual_desktops:
  primary: 2

applications:
  vscode:
    type: "vscode"
    workspace: "C:/Users/YOU/projects/sample"  # EDIT THIS
    window:
      monitor: 0
      position: { x: 0, y: 0, width: 960, height: 1040 }

  terminals:
    type: "windows-terminal"
    window:
      monitor: 0
      position: { x: 960, y: 0, width: 960, height: 520 }
    tabs:
      - title: "Project"
        directory: "C:/Users/YOU/projects/sample"  # EDIT THIS
        shell: "pwsh"
      - title: "Scratch"
        directory: "C:/Users/YOU"  # EDIT THIS
        shell: "pwsh"

  explorer:
    type: "file-explorer"
    paths:
      - "C:/Users/YOU/projects/sample"  # EDIT THIS
    window:
      monitor: 0
      position: { x: 960, y: 520, width: 960, height: 520 }
'@ | Set-Content "$ProjectRoot/sample-project.workspace.yaml" -Encoding UTF8

Write-Status "Created sample-project.workspace.yaml" "OK"

# --- deploy.ps1 (skeleton) ---

@'
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

# Load libraries
. "$PSScriptRoot\lib\Win32.ps1"
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
    Ensure-DesktopCount -Count $primary
}

# Step 2: VS Code
if ($apps.vscode) {
    Write-Host "[2/4] Launching VS Code..." -ForegroundColor White
    $vscodePath = $apps.vscode.workspace
    $pos = $apps.vscode.window.position
    Start-AndPosition -FilePath "code" -ArgumentList @($vscodePath) `
        -ProcessName "Code" -TitlePattern "Visual Studio Code" `
        -X $pos.x -Y $pos.y -Width $pos.width -Height $pos.height
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
    Start-TerminalWithTabs -Tabs $tabs

    # Position after launch
    Start-Sleep -Seconds 2  # crude wait — P1.1 will refine with polling
    $termWin = Find-WindowByProcess -ProcessName "WindowsTerminal" -TimeoutSeconds 10
    if ($termWin) {
        $pos = $apps.terminals.window.position
        Move-WindowTo -Hwnd $termWin[0].Hwnd -X $pos.x -Y $pos.y -Width $pos.width -Height $pos.height
    }
}

# Step 4: File Explorer
if ($apps.explorer) {
    Write-Host "[4/4] Launching File Explorer..." -ForegroundColor White
    foreach ($path in $apps.explorer.paths) {
        Start-Process "explorer.exe" -ArgumentList $path
    }
    Start-Sleep -Seconds 1
    $explorerWins = Find-WindowByProcess -ProcessName "explorer" -TimeoutSeconds 5
    if ($explorerWins -and $apps.explorer.window) {
        $pos = $apps.explorer.window.position
        # Position the most recently opened explorer window
        $latest = $explorerWins | Select-Object -Last 1
        Move-WindowTo -Hwnd $latest.Hwnd -X $pos.x -Y $pos.y -Width $pos.width -Height $pos.height
    }
}

Write-Host ""
Write-Host "Deploy complete: $name" -ForegroundColor Green
Write-Host ""
'@ | Set-Content "$ProjectRoot/deploy.ps1" -Encoding UTF8

Write-Status "Created deploy.ps1 — main deploy script" "OK"

# --- stow.ps1 (skeleton) ---

@'
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

. "$PSScriptRoot\lib\Win32.ps1"
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
    # TODO: More targeted — close only the project's terminal window, not all terminals
    # For Phase 1, we close the most recent terminal window
    $termWins = Get-AllWindows | Where-Object { $_.ProcessName -eq "WindowsTerminal" }
    if ($termWins) {
        Close-WindowGracefully -Hwnd $termWins[-1].Hwnd
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
'@ | Set-Content "$ProjectRoot/stow.ps1" -Encoding UTF8

Write-Status "Created stow.ps1 — main stow script" "OK"

# --- test/Test-WindowPositioning.ps1 ---

@'
<#
.SYNOPSIS
    Phase 1.1 validation: Can we launch, find, and position windows?
#>

. "$PSScriptRoot\..\lib\Win32.ps1"
. "$PSScriptRoot\..\lib\WindowManager.ps1"

Write-Host ""
Write-Host "=== Test: Window Positioning ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: Enumerate current windows
Write-Host "Test 1: Enumerating all visible windows..." -ForegroundColor White
$allWindows = Get-AllWindows
Write-Host "  Found $($allWindows.Count) visible windows." -ForegroundColor Green

# Test 2: Launch Notepad and position it
Write-Host ""
Write-Host "Test 2: Launch Notepad at (100, 100) 600x400..." -ForegroundColor White
$notepad = Start-AndPosition -FilePath "notepad.exe" `
    -ProcessName "notepad" `
    -X 100 -Y 100 -Width 600 -Height 400

if ($notepad) {
    # Verify position
    Start-Sleep -Milliseconds 500
    $rect = Get-WindowRect -Hwnd $notepad.Hwnd
    $posOk = [Math]::Abs($rect.X - 100) -le 10 -and [Math]::Abs($rect.Y - 100) -le 10
    $sizeOk = [Math]::Abs($rect.Width - 600) -le 20 -and [Math]::Abs($rect.Height - 400) -le 20

    if ($posOk -and $sizeOk) {
        Write-Host "  PASS — Window at ($($rect.X), $($rect.Y)) $($rect.Width)x$($rect.Height)" -ForegroundColor Green
    } else {
        Write-Host "  FAIL — Expected (100,100) 600x400, got ($($rect.X),$($rect.Y)) $($rect.Width)x$($rect.Height)" -ForegroundColor Red
    }

    # Clean up
    Close-WindowGracefully -Hwnd $notepad.Hwnd
    Write-Host "  Cleaned up Notepad." -ForegroundColor DarkGray
} else {
    Write-Host "  FAIL — Could not find Notepad window." -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Tests complete ===" -ForegroundColor Cyan
Write-Host ""
'@ | Set-Content "$ProjectRoot/test/Test-WindowPositioning.ps1" -Encoding UTF8

Write-Status "Created test/Test-WindowPositioning.ps1" "OK"

# --- test/Test-VirtualDesktop.ps1 (stub) ---

@'
<#
.SYNOPSIS
    Phase 1.2 validation: Can we create, switch, and move windows between virtual desktops?
    TODO: Implement after P1.2 development.
#>

Write-Host ""
Write-Host "=== Test: Virtual Desktops ===" -ForegroundColor Cyan
Write-Host "  [TODO] Not yet implemented — complete P1.2 first." -ForegroundColor Yellow
Write-Host ""
'@ | Set-Content "$ProjectRoot/test/Test-VirtualDesktop.ps1" -Encoding UTF8

Write-Status "Created test/Test-VirtualDesktop.ps1 — stub" "OK"

# --- test/Test-TerminalLaunch.ps1 (stub) ---

@'
<#
.SYNOPSIS
    Phase 1.3 validation: Can we launch Windows Terminal with configured tabs?
    TODO: Implement after P1.3 development.
#>

Write-Host ""
Write-Host "=== Test: Terminal Launch ===" -ForegroundColor Cyan
Write-Host "  [TODO] Not yet implemented — complete P1.3 first." -ForegroundColor Yellow
Write-Host ""
'@ | Set-Content "$ProjectRoot/test/Test-TerminalLaunch.ps1" -Encoding UTF8

Write-Status "Created test/Test-TerminalLaunch.ps1 — stub" "OK"

# --- README.md ---

@'
# Workspace Orchestrator

One-click deploy and stow of entire project contexts on Windows 11 — VS Code, terminals, browsers, file explorers, Office docs — each positioned on the right virtual desktop with the right layout.

## Status: Phase 1 — Proof of Concept

Currently validating core technical capabilities:
- [x] Project scaffolded
- [ ] P1.1 — Window discovery & positioning (Win32 API)
- [ ] P1.2 — Virtual desktop management (COM interop)
- [ ] P1.3 — Windows Terminal multi-tab launch
- [ ] P1.4 — Basic deploy/stow cycle

## Quick Start

```powershell
# Run the bootstrap script (installs deps, creates project structure)
pwsh -File bootstrap.ps1

# Edit the sample config with your real paths
code workspace-orchestrator/sample-project.workspace.yaml

# Run the first test
cd workspace-orchestrator
.\test\Test-WindowPositioning.ps1

# Try a deploy
.\deploy.ps1 -Project sample-project

# Stow it
.\stow.ps1 -Project sample-project
```

## Docs

- [Design Spec](docs/spec.md)
- [Development Plan](docs/plan.md)
- [Phase 1 Setup Guide](docs/phase1-setup.md)
'@ | Set-Content "$ProjectRoot/README.md" -Encoding UTF8

Write-Status "Created README.md" "OK"

# --- Final Summary ---

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Bootstrap complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Project created at: $ProjectRoot" -ForegroundColor White
Write-Host ""
Write-Host "  Next steps:" -ForegroundColor White
Write-Host "    1. cd $ProjectRoot" -ForegroundColor DarkGray
Write-Host "    2. Copy spec.md and plan.md into docs/" -ForegroundColor DarkGray
Write-Host "    3. Edit sample-project.workspace.yaml with your real paths" -ForegroundColor DarkGray
Write-Host "    4. Run: .\test\Test-WindowPositioning.ps1" -ForegroundColor DarkGray
Write-Host "    5. If that passes, start working on P1.2 (virtual desktops)" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Open in VS Code:" -ForegroundColor White
Write-Host "    code $ProjectRoot" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Open in Claude Code:" -ForegroundColor White
Write-Host "    cd $ProjectRoot && claude" -ForegroundColor Cyan
Write-Host ""
