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
