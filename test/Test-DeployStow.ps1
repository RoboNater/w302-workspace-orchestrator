<#
.SYNOPSIS
    Phase 1.4 validation: full deploy/stow round-trip for sample-project.

.NOTES
    This test launches real applications (VS Code, Windows Terminal, Explorer).
    If VS Code is already open, the stow step will close windows matching the
    workspace-orchestrator title — save your work first.

    Run from the repo root:
        pwsh -File .\test\Test-DeployStow.ps1
#>

$repoRoot = Split-Path $PSScriptRoot -Parent
$project  = "sample-project"

Write-Host ""
Write-Host "=== Test: Deploy / Stow Round-Trip ===" -ForegroundColor Cyan
Write-Host "  Project: $project" -ForegroundColor DarkGray
Write-Host ""

$pass = 0
$fail = 0

# -----------------------------------------------------------------------
# PHASE: DEPLOY
# -----------------------------------------------------------------------
Write-Host "--- DEPLOY ---" -ForegroundColor White
try {
    & "$repoRoot\deploy.ps1" -Project $project
} catch {
    Write-Host "  FAIL — deploy.ps1 threw: $_" -ForegroundColor Red
    $fail++
    Write-Host ""
    Write-Host "=== Results: $pass passed, $fail failed ===" -ForegroundColor Red
    exit 1
}

# Load libs so we can query windows (deploy already loaded them, but we need
# them in this script scope too)
. "$repoRoot\lib\WindowManager.ps1"

# Give all processes a moment to fully render
Start-Sleep -Seconds 2

# -----------------------------------------------------------------------
# VERIFY: VS Code window
# -----------------------------------------------------------------------
Write-Host "--- VERIFY: VS Code ---" -ForegroundColor White
$codeWins = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "Code" -and $_.Title -match "Visual Studio Code"
}
if ($codeWins) {
    $w = $codeWins[0]
    Write-Host "  PASS — VS Code found: '$($w.Title)'" -ForegroundColor Green
    Write-Host "         Position: ($($w.Rect.X), $($w.Rect.Y)) $($w.Rect.Width)x$($w.Rect.Height)" -ForegroundColor DarkGray
    $pass++
} else {
    Write-Host "  FAIL — No VS Code window found." -ForegroundColor Red
    $fail++
}

# -----------------------------------------------------------------------
# VERIFY: Windows Terminal window
# -----------------------------------------------------------------------
Write-Host ""
Write-Host "--- VERIFY: Windows Terminal ---" -ForegroundColor White
$wtWins = Get-AllWindows | Where-Object { $_.ProcessName -eq "WindowsTerminal" }
if ($wtWins) {
    $w = $wtWins[0]
    Write-Host "  PASS — Windows Terminal found: '$($w.Title)'" -ForegroundColor Green
    Write-Host "         Position: ($($w.Rect.X), $($w.Rect.Y)) $($w.Rect.Width)x$($w.Rect.Height)" -ForegroundColor DarkGray
    $pass++
} else {
    Write-Host "  FAIL — No WindowsTerminal window found." -ForegroundColor Red
    $fail++
}

# -----------------------------------------------------------------------
# VERIFY: File Explorer window
# -----------------------------------------------------------------------
Write-Host ""
Write-Host "--- VERIFY: File Explorer ---" -ForegroundColor White
$explorerWins = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "explorer" -and $_.Title -match "workspace-orchestrator"
}
if ($explorerWins) {
    $w = $explorerWins[0]
    Write-Host "  PASS — Explorer found: '$($w.Title)'" -ForegroundColor Green
    Write-Host "         Position: ($($w.Rect.X), $($w.Rect.Y)) $($w.Rect.Width)x$($w.Rect.Height)" -ForegroundColor DarkGray
    $pass++
} else {
    Write-Host "  FAIL — No Explorer window for 'workspace-orchestrator' found." -ForegroundColor Red
    $fail++
}

# -----------------------------------------------------------------------
# PHASE: STOW
# -----------------------------------------------------------------------
Write-Host ""
Write-Host "--- STOW ---" -ForegroundColor White
try {
    & "$repoRoot\stow.ps1" -Project $project
} catch {
    Write-Host "  FAIL — stow.ps1 threw: $_" -ForegroundColor Red
    $fail++
}

Start-Sleep -Seconds 2

# -----------------------------------------------------------------------
# VERIFY: Windows closed after stow
# -----------------------------------------------------------------------
Write-Host "--- VERIFY: Cleanup after stow ---" -ForegroundColor White

$codeAfter = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "Code" -and $_.Title -match "workspace-orchestrator"
}
if (-not $codeAfter) {
    Write-Host "  PASS — VS Code (workspace-orchestrator) no longer visible." -ForegroundColor Green
    $pass++
} else {
    Write-Host "  FAIL — VS Code window still open after stow." -ForegroundColor Red
    $fail++
}

$wtAfter = Get-AllWindows | Where-Object { $_.ProcessName -eq "WindowsTerminal" }
if (-not $wtAfter) {
    Write-Host "  PASS — Windows Terminal no longer visible." -ForegroundColor Green
    $pass++
} else {
    Write-Host "  FAIL — Windows Terminal still open after stow." -ForegroundColor Red
    $fail++
}

$explorerAfter = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "explorer" -and $_.Title -match "workspace-orchestrator"
}
if (-not $explorerAfter) {
    Write-Host "  PASS — Explorer (workspace-orchestrator) no longer visible." -ForegroundColor Green
    $pass++
} else {
    Write-Host "  FAIL — Explorer window still open after stow." -ForegroundColor Red
    $fail++
}

# -----------------------------------------------------------------------
# SUMMARY
# -----------------------------------------------------------------------
Write-Host ""
$color = if ($fail -eq 0) { "Green" } else { "Red" }
Write-Host "=== Results: $pass passed, $fail failed ===" -ForegroundColor $color
Write-Host ""
