<#
.SYNOPSIS
    Phase 1.3 validation: launch Windows Terminal with multiple tabs and command injection.

.NOTES
    Automated checks: window found, window positioned correctly.
    Visual checks (tab titles, command output) are noted but require human verification.
#>

. "$PSScriptRoot\..\lib\WindowManager.ps1"
. "$PSScriptRoot\..\lib\TerminalLauncher.ps1"

Write-Host ""
Write-Host "=== Test: Terminal Launch ===" -ForegroundColor Cyan
Write-Host ""

$pass = 0
$fail = 0

# Test 1: Launch WT with 2 tabs, one with a startup command
Write-Host "Test 1: Launch Windows Terminal with 2 tabs (one with command injection)..." -ForegroundColor White

$tabs = @(
    @{
        Title     = "WT-Test-Alpha"
        Directory = "C:\dev\workspace-orchestrator"
        Command   = 'Write-Host "Hello from Tab-Alpha" -ForegroundColor Green'
    },
    @{
        Title     = "WT-Test-Beta"
        Directory = "C:\Users\AgentUser01"
        Command   = 'Write-Host "Hello from Tab-Beta" -ForegroundColor Cyan'
    }
)

# Snapshot existing WT windows so we can identify the newly launched one
$existingWtHwnds = @(Get-AllWindows | Where-Object { $_.ProcessName -eq "WindowsTerminal" } | ForEach-Object { $_.Hwnd })

Start-TerminalWithTabs -Tabs $tabs

# Test 2: Verify the WT window appears
Write-Host ""
Write-Host "Test 2: Wait for Windows Terminal window to appear..." -ForegroundColor White
$wtWin = Find-WindowByProcess -ProcessName "WindowsTerminal" -TimeoutSeconds 15 -ExcludeHwnds $existingWtHwnds

if ($wtWin) {
    Write-Host "  PASS — Found WT window: '$($wtWin[0].Title)'" -ForegroundColor Green
    $pass++
} else {
    Write-Host "  FAIL — No WindowsTerminal window found within 15s timeout." -ForegroundColor Red
    $fail++
}

# Test 3: Position the WT window
Write-Host ""
Write-Host "Test 3: Position WT window at (50, 50) 1200x700..." -ForegroundColor White
if ($wtWin) {
    Move-WindowTo -Hwnd $wtWin[0].Hwnd -X 50 -Y 50 -Width 1200 -Height 700 | Out-Null
    Start-Sleep -Milliseconds 400
    $rect = Get-WindowRect -Hwnd $wtWin[0].Hwnd

    $posOk  = [Math]::Abs($rect.X - 50)   -le 15 -and [Math]::Abs($rect.Y - 50)   -le 15
    $sizeOk = [Math]::Abs($rect.Width - 1200) -le 30 -and [Math]::Abs($rect.Height - 700) -le 30

    if ($posOk -and $sizeOk) {
        Write-Host "  PASS — WT at ($($rect.X), $($rect.Y)) $($rect.Width)x$($rect.Height)" -ForegroundColor Green
        $pass++
    } else {
        Write-Host "  FAIL — Expected (50,50) 1200x700, got ($($rect.X),$($rect.Y)) $($rect.Width)x$($rect.Height)" -ForegroundColor Red
        $fail++
    }
} else {
    Write-Host "  SKIP — No WT window to position." -ForegroundColor Yellow
}

# Visual verification notes (cannot be automated without WT API/accessibility)
Write-Host ""
Write-Host "--- Visual verification required ---" -ForegroundColor Yellow
Write-Host "  [ ] Two tabs visible: 'WT-Test-Alpha' and 'WT-Test-Beta'" -ForegroundColor Yellow
Write-Host "  [ ] Tab-Alpha shows: Hello from Tab-Alpha (green text)" -ForegroundColor Yellow
Write-Host "  [ ] Tab-Beta shows:  Hello from Tab-Beta (cyan text)" -ForegroundColor Yellow
Write-Host "  [ ] Both tabs are in a pwsh prompt (not crashed)" -ForegroundColor Yellow

# Cleanup
Write-Host ""
Write-Host "Cleaning up in 5 seconds..." -ForegroundColor DarkGray
Start-Sleep -Milliseconds 5000
if ($wtWin) {
    Close-WindowGracefully -Hwnd $wtWin[0].Hwnd -Force
    Write-Host "  Closed WT window." -ForegroundColor DarkGray
}

Write-Host ""
$color = if ($fail -eq 0) { "Green" } else { "Red" }
Write-Host "=== Results: $pass passed, $fail failed (plus visual checks above) ===" -ForegroundColor $color
Write-Host ""
