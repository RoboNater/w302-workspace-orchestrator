<#
.SYNOPSIS
    Install the workspace orchestrator 'ws' CLI tool.

.DESCRIPTION
    Builds the .NET CLI project, copies it to ~/.workspaces/bin/, and adds that
    directory to the user PATH if it isn't already there.

    Prerequisites:
    - .NET 8 SDK (winget install Microsoft.DotNet.SDK.8)
    - PowerShell 7+ (already installed at C:\Program Files\PowerShell\7\pwsh.exe)

.EXAMPLE
    pwsh -File .\install.ps1
#>

$ErrorActionPreference = "Stop"
$RepoRoot   = $PSScriptRoot
$BinDir     = Join-Path $HOME ".workspaces\bin"
$WorkspaceDir = Join-Path $HOME ".workspaces"

Write-Host ""
Write-Host "Workspace Orchestrator — Install" -ForegroundColor Cyan
Write-Host ""

# 1. Ensure ~/.workspaces/bin exists
if (-not (Test-Path $BinDir)) {
    New-Item -ItemType Directory -Path $BinDir -Force | Out-Null
    Write-Host "  Created $BinDir" -ForegroundColor DarkGray
}

# 2. Build and publish ws.exe
$cliProject = Join-Path $RepoRoot "src\WorkspaceOrchestrator.Cli\WorkspaceOrchestrator.Cli.csproj"
if (-not (Test-Path $cliProject)) {
    Write-Error "CLI project not found: $cliProject"
    exit 1
}

# 2a. Ensure nuget.org package source is configured (fresh SDK installs may lack it)
$nugetSources = & dotnet nuget list source 2>&1
if ($nugetSources -notmatch 'nuget\.org') {
    Write-Host "  Adding nuget.org package source..." -ForegroundColor Yellow
    & dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Could not add nuget.org source automatically. If the build fails, run:"
        Write-Warning "  dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org"
    } else {
        Write-Host "  Added nuget.org package source" -ForegroundColor DarkGray
    }
}

Write-Host "  Building ws.exe..." -ForegroundColor White
& dotnet publish $cliProject `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $BinDir `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "  Build failed. Common causes:" -ForegroundColor Red
    Write-Host "    1. .NET 8 SDK not installed — run: winget install Microsoft.DotNet.SDK.8" -ForegroundColor Red
    Write-Host "    2. NuGet package restore failed — check network/proxy access to nuget.org" -ForegroundColor Red
    Write-Host "       Run 'dotnet nuget list source' to verify nuget.org is configured." -ForegroundColor Red
    Write-Host "       If missing: dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org" -ForegroundColor Red
    Write-Host "    3. Scroll up for the detailed error output from 'dotnet publish'." -ForegroundColor Red
    exit 1
}

Write-Host "  Published to $BinDir" -ForegroundColor DarkGray

# 3. Copy VirtualDesktopBridge.ps1 to bin so ws.exe can find it
$bridgeSrc = Join-Path $RepoRoot "lib\VirtualDesktopBridge.ps1"
$bridgeDst = Join-Path $BinDir "lib\VirtualDesktopBridge.ps1"
New-Item -ItemType Directory -Path (Join-Path $BinDir "lib") -Force | Out-Null
Copy-Item $bridgeSrc $bridgeDst -Force
Write-Host "  Copied VirtualDesktopBridge.ps1" -ForegroundColor DarkGray

# 4. Also copy Win32.ps1 (required by bridge)
$win32Src = Join-Path $RepoRoot "lib\Win32.ps1"
$win32Dst = Join-Path $BinDir "lib\Win32.ps1"
Copy-Item $win32Src $win32Dst -Force
Write-Host "  Copied Win32.ps1" -ForegroundColor DarkGray

# 5. Add to PATH (user scope)
$currentPath = [System.Environment]::GetEnvironmentVariable("PATH", "User")
if ($currentPath -notlike "*$BinDir*") {
    [System.Environment]::SetEnvironmentVariable("PATH", "$currentPath;$BinDir", "User")
    Write-Host "  Added $BinDir to user PATH" -ForegroundColor DarkGray
    Write-Host "  Restart your shell or run: `$env:PATH += ';$BinDir'" -ForegroundColor Yellow
} else {
    Write-Host "  $BinDir already in PATH" -ForegroundColor DarkGray
}

# 6. Copy sample configs to ~/.workspaces/ if none exist there yet
$samplesDir = Join-Path $RepoRoot "samples"
if (Test-Path $samplesDir) {
    foreach ($sample in Get-ChildItem $samplesDir -Filter "*.workspace.yaml") {
        $dst = Join-Path $WorkspaceDir $sample.Name
        if (-not (Test-Path $dst)) {
            Copy-Item $sample.FullName $dst
            Write-Host "  Copied sample config: $($sample.Name)" -ForegroundColor DarkGray
        }
    }
}

Write-Host ""
Write-Host "Install complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Usage:" -ForegroundColor Cyan
Write-Host "  ws list                     # List available projects"
Write-Host "  ws deploy <project>         # Deploy a project context"
Write-Host "  ws stow <project>           # Stow (close) a project"
Write-Host "  ws switch <project>         # Switch to a different project"
Write-Host "  ws status                   # Show deployed projects"
Write-Host "  ws validate <project>       # Validate a project config"
Write-Host ""
Write-Host "Project configs: $WorkspaceDir\*.workspace.yaml" -ForegroundColor DarkGray
Write-Host ""
