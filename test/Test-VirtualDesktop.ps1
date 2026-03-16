<#
.SYNOPSIS
    Phase 1.2 validation: create, switch, and move windows between virtual desktops.

.NOTES
    This test WILL briefly switch your active virtual desktop and switch back.
    Run it on a desktop where a momentary switch is acceptable.
#>

. "$PSScriptRoot\..\lib\WindowManager.ps1"
. "$PSScriptRoot\..\lib\VirtualDesktop.ps1"

Write-Host ""
Write-Host "=== Test: Virtual Desktops ===" -ForegroundColor Cyan
Write-Host ""

$pass = 0
$fail = 0

# --- Baseline ---
$startCount = Get-DesktopCount
$startIndex = Get-CurrentDesktopIndex
Write-Host "Baseline: $startCount desktop(s), currently on desktop $startIndex (0-based)." -ForegroundColor White
Write-Host ""

# Test 1: Ensure-DesktopCount creates a new desktop
$targetCount = $startCount + 1
Write-Host "Test 1: Ensure-DesktopCount creates desktop (need $targetCount total)..." -ForegroundColor White
Ensure-DesktopCount -Count $targetCount | Out-Null
$afterCount = Get-DesktopCount
if ($afterCount -ge $targetCount) {
    Write-Host "  PASS — Desktop count is now $afterCount" -ForegroundColor Green
    $pass++
} else {
    Write-Host "  FAIL — Expected >= $targetCount, got $afterCount" -ForegroundColor Red
    $fail++
}

# Test 2: Move a Notepad window to the new desktop
$newDesktopIndex = $startCount   # 0-based; old count == new last index
Write-Host ""
Write-Host "Test 2: Launch Notepad and move it to desktop $newDesktopIndex..." -ForegroundColor White
$notepad = Start-AndPosition -FilePath "notepad.exe" -ProcessName "notepad" `
    -X 300 -Y 300 -Width 500 -Height 350
if ($notepad) {
    Start-Sleep -Milliseconds 500
    $moved = Move-WindowToDesktop -Hwnd $notepad.Hwnd -DesktopIndex $newDesktopIndex
    if ($moved) {
        $actualIdx = Get-WindowDesktopIndex -Hwnd $notepad.Hwnd
        if ($actualIdx -eq $newDesktopIndex) {
            Write-Host "  PASS — Notepad confirmed on desktop $actualIdx" -ForegroundColor Green
            $pass++
        } else {
            Write-Host "  FAIL — Expected desktop $newDesktopIndex, window reports desktop $actualIdx" -ForegroundColor Red
            $fail++
        }
    } else {
        Write-Host "  FAIL — Move-WindowToDesktop returned false" -ForegroundColor Red
        $fail++
    }
} else {
    Write-Host "  FAIL — Could not launch Notepad" -ForegroundColor Red
    $fail++
}

# Test 3: Switch to new desktop, verify, switch back
Write-Host ""
Write-Host "Test 3: Switch to desktop $newDesktopIndex and back..." -ForegroundColor White
Switch-ToDesktop -DesktopIndex $newDesktopIndex | Out-Null
Start-Sleep -Milliseconds 600

$nowOn = Get-CurrentDesktopIndex
if ($nowOn -eq $newDesktopIndex) {
    Write-Host "  PASS — Active desktop is $nowOn" -ForegroundColor Green
    $pass++
} else {
    Write-Host "  FAIL — Expected desktop $newDesktopIndex, active is $nowOn" -ForegroundColor Red
    $fail++
}

Switch-ToDesktop -DesktopIndex $startIndex | Out-Null
Start-Sleep -Milliseconds 600

$backOn = Get-CurrentDesktopIndex
if ($backOn -eq $startIndex) {
    Write-Host "  PASS — Returned to starting desktop $backOn" -ForegroundColor Green
    $pass++
} else {
    Write-Host "  FAIL — Expected desktop $startIndex, active is $backOn" -ForegroundColor Red
    $fail++
}

# --- Cleanup ---
Write-Host ""
Write-Host "Cleaning up..." -ForegroundColor DarkGray
if ($notepad) {
    Close-WindowGracefully -Hwnd $notepad.Hwnd
    Write-Host "  Closed Notepad." -ForegroundColor DarkGray
}
Start-Sleep -Milliseconds 400
Remove-Desktop -Desktop (Get-Desktop $newDesktopIndex)
Write-Host "  Removed desktop $newDesktopIndex. Count now: $(Get-DesktopCount)" -ForegroundColor DarkGray

# --- Summary ---
Write-Host ""
$color = if ($fail -eq 0) { "Green" } else { "Red" }
Write-Host "=== Results: $pass passed, $fail failed ===" -ForegroundColor $color
Write-Host ""
