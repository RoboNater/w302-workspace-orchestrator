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
