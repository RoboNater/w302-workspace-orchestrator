<#
.SYNOPSIS
    Multi-project isolation test: deploy two projects simultaneously and verify
    that deploying/stowing one project does not affect the other's windows.

.DESCRIPTION
    Test sequence:
      1. Deploy sample-project-1  (virtual desktop 2)
      2. Deploy sample-project-2  (virtual desktop 3)
      3. ISOLATION: verify project-1 windows survived project-2 deploy
      4. Stow  sample-project-2
      5. ISOLATION: verify project-1 windows survived project-2 stow
      6. Stow  sample-project-1
      7. Verify all project windows are closed

.NOTES
    Launches real applications (VS Code ×2, Windows Terminal ×2, Explorer ×2).
    Save your work before running — VS Code windows matching the sample project
    workspace names will be closed during the test.

    Run from the repo root:
        pwsh -File .\test\Test-MultiProjectInteraction.ps1
#>

$repoRoot = Split-Path $PSScriptRoot -Parent
$proj1    = "sample-project-1"
$proj2    = "sample-project-2"

Write-Host ""
Write-Host "=== Test: Multi-Project Isolation ===" -ForegroundColor Cyan
Write-Host "  Project A: $proj1  (desktop 2)" -ForegroundColor DarkGray
Write-Host "  Project B: $proj2  (desktop 3)" -ForegroundColor DarkGray
Write-Host ""

$pass = 0
$fail = 0

# Helper: record a pass/fail result with a label
function Assert-True {
    param([bool]$Condition, [string]$Label, [string]$Detail = "")
    if ($Condition) {
        Write-Host "  PASS — $Label" -ForegroundColor Green
        if ($Detail) { Write-Host "         $Detail" -ForegroundColor DarkGray }
        $script:pass++
    } else {
        Write-Host "  FAIL — $Label" -ForegroundColor Red
        if ($Detail) { Write-Host "         $Detail" -ForegroundColor DarkGray }
        $script:fail++
    }
}

# Load libs so we can query windows after each phase
. "$repoRoot\lib\WindowManager.ps1"

# -----------------------------------------------------------------------
# PRECONDITION: check no conflicting windows are already open
# -----------------------------------------------------------------------
Write-Host "--- Pre-check: no stale project windows ---" -ForegroundColor White

$staleCode = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "Code" -and (
        $_.Title -match "sample-project-1" -or $_.Title -match "sample-project-2"
    )
}
$staleExplorer = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "explorer" -and (
        $_.Title -match "sample-project-1" -or $_.Title -match "sample-project-2"
    )
}
if ($staleCode -or $staleExplorer) {
    Write-Host "  WARN — Stale project windows detected before test start." -ForegroundColor Yellow
    Write-Host "         Close any VS Code / Explorer windows for sample-project-1 or -2" -ForegroundColor Yellow
    Write-Host "         and re-run the test for clean isolation results." -ForegroundColor Yellow
    Write-Host ""
}

# -----------------------------------------------------------------------
# PHASE 1: DEPLOY project-1
# -----------------------------------------------------------------------
Write-Host "--- DEPLOY: $proj1 ---" -ForegroundColor White
try {
    & "$repoRoot\deploy.ps1" -Project $proj1
} catch {
    Write-Host "  FAIL — deploy.ps1 threw for $proj1`: $_" -ForegroundColor Red
    $fail++
    Write-Host ""
    Write-Host "=== Results: $pass passed, $fail failed ===" -ForegroundColor Red
    exit 1
}

Start-Sleep -Seconds 2

# Snapshot project-1 window handles so we can re-check them later
$p1CodeWins = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "Code" -and $_.Title -match "sample-project-1"
}
$p1ExplorerWins = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "explorer" -and $_.Title -match "sample-project-1"
}
$p1WtWins = Get-AllWindows | Where-Object { $_.ProcessName -eq "WindowsTerminal" }

Write-Host ""
Write-Host "--- VERIFY: $proj1 windows present after deploy ---" -ForegroundColor White
Assert-True ($p1CodeWins.Count -gt 0)    "VS Code (project-1) found" `
    "Title: $($p1CodeWins[0].Title)"
Assert-True ($p1WtWins.Count -gt 0)      "Windows Terminal (project-1) found"
Assert-True ($p1ExplorerWins.Count -gt 0) "File Explorer (project-1) found" `
    "Title: $($p1ExplorerWins[0].Title)"

# -----------------------------------------------------------------------
# PHASE 2: DEPLOY project-2 (while project-1 is still live)
# -----------------------------------------------------------------------
Write-Host ""
Write-Host "--- DEPLOY: $proj2 (project-1 still running) ---" -ForegroundColor White
try {
    & "$repoRoot\deploy.ps1" -Project $proj2
} catch {
    Write-Host "  FAIL — deploy.ps1 threw for $proj2`: $_" -ForegroundColor Red
    $fail++
    # Attempt cleanup before aborting
    & "$repoRoot\stow.ps1" -Project $proj1 -ErrorAction SilentlyContinue
    Write-Host ""
    Write-Host "=== Results: $pass passed, $fail failed ===" -ForegroundColor Red
    exit 1
}

Start-Sleep -Seconds 2

Write-Host ""
Write-Host "--- VERIFY: $proj2 windows present after deploy ---" -ForegroundColor White
$p2CodeWins = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "Code" -and $_.Title -match "sample-project-2"
}
$p2ExplorerWins = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "explorer" -and $_.Title -match "sample-project-2"
}
$allWtAfterP2 = Get-AllWindows | Where-Object { $_.ProcessName -eq "WindowsTerminal" }

Assert-True ($p2CodeWins.Count -gt 0)    "VS Code (project-2) found" `
    "Title: $($p2CodeWins[0].Title)"
Assert-True ($allWtAfterP2.Count -ge 2)  "At least 2 Windows Terminal windows open (one per project)"
Assert-True ($p2ExplorerWins.Count -gt 0) "File Explorer (project-2) found" `
    "Title: $($p2ExplorerWins[0].Title)"

# -----------------------------------------------------------------------
# ISOLATION CHECK A: project-1 windows still intact after project-2 deploy
# -----------------------------------------------------------------------
Write-Host ""
Write-Host "--- ISOLATION CHECK A: $proj1 windows not disturbed by $proj2 deploy ---" -ForegroundColor White

$p1CodeStillOpen = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "Code" -and $_.Title -match "sample-project-1"
}
$p1ExplorerStillOpen = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "explorer" -and $_.Title -match "sample-project-1"
}

Assert-True ($p1CodeStillOpen.Count -gt 0) `
    "VS Code (project-1) still open after project-2 was deployed"
Assert-True ($p1ExplorerStillOpen.Count -gt 0) `
    "File Explorer (project-1) still open after project-2 was deployed"

# -----------------------------------------------------------------------
# PHASE 3: STOW project-2
# -----------------------------------------------------------------------
Write-Host ""
Write-Host "--- STOW: $proj2 ---" -ForegroundColor White
try {
    & "$repoRoot\stow.ps1" -Project $proj2
} catch {
    Write-Host "  FAIL — stow.ps1 threw for $proj2`: $_" -ForegroundColor Red
    $fail++
}

Start-Sleep -Seconds 2

Write-Host ""
Write-Host "--- VERIFY: $proj2 windows closed after stow ---" -ForegroundColor White

$p2CodeAfterStow = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "Code" -and $_.Title -match "sample-project-2"
}
$p2ExplorerAfterStow = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "explorer" -and $_.Title -match "sample-project-2"
}
$wtAfterP2Stow = Get-AllWindows | Where-Object { $_.ProcessName -eq "WindowsTerminal" }

Assert-True ($p2CodeAfterStow.Count -eq 0)    "VS Code (project-2) closed after stow"
Assert-True ($p2ExplorerAfterStow.Count -eq 0) "File Explorer (project-2) closed after stow"
# stow.ps1 closes the last WT window (project-2's), one WT should remain for project-1
Assert-True ($wtAfterP2Stow.Count -lt $allWtAfterP2.Count) `
    "Windows Terminal count decreased after project-2 stow" `
    "Was $($allWtAfterP2.Count), now $($wtAfterP2Stow.Count)"

# -----------------------------------------------------------------------
# ISOLATION CHECK B: project-1 windows still intact after project-2 stow
# -----------------------------------------------------------------------
Write-Host ""
Write-Host "--- ISOLATION CHECK B: $proj1 windows not disturbed by $proj2 stow ---" -ForegroundColor White

$p1CodeAfterP2Stow = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "Code" -and $_.Title -match "sample-project-1"
}
$p1ExplorerAfterP2Stow = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "explorer" -and $_.Title -match "sample-project-1"
}

Assert-True ($p1CodeAfterP2Stow.Count -gt 0) `
    "VS Code (project-1) still open after project-2 was stowed"
Assert-True ($p1ExplorerAfterP2Stow.Count -gt 0) `
    "File Explorer (project-1) still open after project-2 was stowed"
Assert-True ($wtAfterP2Stow.Count -gt 0) `
    "At least one Windows Terminal still open (project-1's)"

# -----------------------------------------------------------------------
# PHASE 4: STOW project-1
# -----------------------------------------------------------------------
Write-Host ""
Write-Host "--- STOW: $proj1 ---" -ForegroundColor White
try {
    & "$repoRoot\stow.ps1" -Project $proj1
} catch {
    Write-Host "  FAIL — stow.ps1 threw for $proj1`: $_" -ForegroundColor Red
    $fail++
}

Start-Sleep -Seconds 2

# -----------------------------------------------------------------------
# VERIFY: all project windows gone
# -----------------------------------------------------------------------
Write-Host ""
Write-Host "--- VERIFY: all project windows closed ---" -ForegroundColor White

$p1CodeFinal = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "Code" -and $_.Title -match "sample-project-1"
}
$p1ExplorerFinal = Get-AllWindows | Where-Object {
    $_.ProcessName -eq "explorer" -and $_.Title -match "sample-project-1"
}
$wtFinal = Get-AllWindows | Where-Object { $_.ProcessName -eq "WindowsTerminal" }

Assert-True ($p1CodeFinal.Count -eq 0)    "VS Code (project-1) closed after final stow"
Assert-True ($p1ExplorerFinal.Count -eq 0) "File Explorer (project-1) closed after final stow"
Assert-True ($wtFinal.Count -eq 0)         "All Windows Terminal windows closed"

# -----------------------------------------------------------------------
# SUMMARY
# -----------------------------------------------------------------------
Write-Host ""
$color = if ($fail -eq 0) { "Green" } else { "Red" }
Write-Host "=== Results: $pass passed, $fail failed ===" -ForegroundColor $color
Write-Host ""
