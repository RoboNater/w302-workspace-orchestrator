<#
.SYNOPSIS
    Windows Terminal multi-tab launch and command injection.

.DESCRIPTION
    Builds and executes a 'wt' command line to open multiple named tabs, each
    in a specific directory with an optional startup command.

    Command injection uses the '--' separator which passes remaining arguments
    to the shell, e.g.:
        wt new-tab -d "C:\path" --title "Tab" -- pwsh -NoExit -Command "echo hi"

    Usage:
        . .\lib\TerminalLauncher.ps1
        Start-TerminalWithTabs -Tabs @(
            @{ Title="API";      Directory="C:\dev\app\api";      Command="npm run dev" },
            @{ Title="Frontend"; Directory="C:\dev\app\frontend"; Command="npm start"   },
            @{ Title="Git";      Directory="C:\dev\app"                                  }
        )
#>

function Start-TerminalWithTabs {
    <#
    .SYNOPSIS
        Launch Windows Terminal with multiple named tabs in specific directories.
    .PARAMETER Tabs
        Array of hashtables with keys:
          Title     - (required) tab title
          Directory - (required) starting directory
          Command   - (optional) command to run on launch (shell stays open via -NoExit)
          Shell     - (optional) shell profile name, defaults to DefaultShell
    .PARAMETER DefaultShell
        Profile name to use when a tab doesn't specify its own. Default: "pwsh".
    .EXAMPLE
        Start-TerminalWithTabs -Tabs @(
            @{ Title="Backend";  Directory="C:\dev\app\api";      Command="npm run dev" },
            @{ Title="Frontend"; Directory="C:\dev\app\frontend"; Command="npm start"   }
        )
    #>
    param(
        [Parameter(Mandatory)][hashtable[]]$Tabs,
        [string]$DefaultShell = "pwsh"
    )

    if ($Tabs.Count -eq 0) { return }

    # Build a single argument string for wt.
    # IMPORTANT: pass as a single string (not an array) so that ';' delimiters
    # are not individually quoted by Start-Process, which would break wt parsing.
    $parts = @()
    foreach ($tab in $Tabs) {
        $shell = if ($tab.Shell) { $tab.Shell } else { $DefaultShell }
        $dir   = $tab.Directory
        $title = $tab.Title

        $part = "new-tab -p `"$shell`" -d `"$dir`" --title `"$title`""

        if ($tab.Command) {
            # Inject startup command; -NoExit keeps the shell open after it runs
            $part += " -- $shell -NoExit -Command `"$($tab.Command)`""
        }

        $parts += $part
    }

    $argString = $parts -join " ; "

    Write-Host "  Launching Windows Terminal with $($Tabs.Count) tab(s)..." -ForegroundColor DarkGray
    Write-Verbose "  wt $argString"

    Start-Process "wt" -ArgumentList $argString
}

Write-Verbose "TerminalLauncher functions loaded."
