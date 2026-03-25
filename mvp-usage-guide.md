# Workspace Orchestrator — MVP Usage Guide

**Version:** Phase 2 MVP (2026-03-23)
**Platform:** Windows 11
**Prerequisites:** .NET 8 SDK, PowerShell 7+, Windows Terminal, VirtualDesktop PS module

---

## Table of Contents

1. [Installation](#installation)
2. [Quick Start](#quick-start)
3. [Working with the Sample Projects](#working-with-the-sample-projects)
4. [Command Reference](#command-reference)
5. [Creating a New Project from Scratch](#creating-a-new-project-from-scratch)
6. [Window Positioning Tips](#window-positioning-tips)
7. [Global Hotkeys](#global-hotkeys)
8. [Window Snapshots](#window-snapshots)
9. [Troubleshooting](#troubleshooting)
10. [Phase 3: Smarter Config Population](#phase-3-smarter-config-population)

---

## Installation

### Prerequisites

Ensure these are installed before proceeding:

```powershell
# .NET 8 SDK
winget install Microsoft.DotNet.SDK.8

# PowerShell 7+
winget install Microsoft.PowerShell

# Windows Terminal (usually pre-installed on Windows 11)
winget install Microsoft.WindowsTerminal

# MScholtes VirtualDesktop module (for virtual desktop management)
Install-Module -Name VirtualDesktop -Scope CurrentUser
```

### Build and Install

From the repository root (`C:\dev\workspace-orchestrator`):

```powershell
# Option A: Install ws.exe to ~/.workspaces/bin/ (recommended)
pwsh -File .\install.ps1

# Option B: Run directly without installing
& "C:\Program Files\dotnet\dotnet.exe" run --project src\WorkspaceOrchestrator.Cli -- <command>
```

The install script will:
1. Build and publish `ws.exe` as a self-contained binary
2. Place it in `~/.workspaces/bin/`
3. Add that directory to your user PATH (restart your shell afterward)
4. Copy sample project configs to `~/.workspaces/`
5. Copy the VirtualDesktop bridge script alongside the binary

After installation, verify:

```powershell
ws --help
ws list
```

---

## Quick Start

```powershell
# See what projects are available
ws list

# Preview what a deploy would do (no windows opened)
ws deploy sample-project-1 --dry-run

# Deploy for real — opens VS Code, Terminal, Explorer, positions them
ws deploy sample-project-1

# Check what's currently deployed
ws status

# Close everything for that project
ws stow sample-project-1

# Switch between projects (atomic stow + deploy)
ws switch sample-project-2
```

---

## Working with the Sample Projects

The repository includes two sample projects with pre-configured workspace files:

### Sample Project 1: Data Processor

**Config:** `samples/sample-project-1.workspace.yaml`
**Source:** `sample-projects/sample-project-1/` (Python data processor with CSV analysis)

What `ws deploy sample-project-1` does:
- Opens VS Code at `sample-projects/sample-project-1/`
- Opens Windows Terminal with 2 tabs:
  - "Project" tab in the project directory
  - "Scratch" tab in your home directory
- Opens File Explorer at the project directory
- Optionally opens Chrome with a `sample-project-1` browser profile
- Moves all windows to **virtual desktop 2**
- Positions windows in a left/right split layout

**Window layout:**
```
┌──────────────────┬──────────────────┐
│                  │   Terminal       │
│   VS Code        │   (960×520)     │
│   (960×1040)     ├──────────────────┤
│                  │   Explorer       │
│                  │   (960×520)     │
└──────────────────┴──────────────────┘
```

### Sample Project 2: Web Server

**Config:** `samples/sample-project-2.workspace.yaml`
**Source:** `sample-projects/sample-project-2/` (Node.js REST API with HTML frontend)

What `ws deploy sample-project-2` does:
- Opens VS Code at `sample-projects/sample-project-2/`
- Opens Windows Terminal with 2 tabs:
  - "Dev Server" tab with `run_on_deploy: "echo 'Starting dev server...'"` (replace with your actual start command)
  - "Git" tab in the project directory
- Opens File Explorer at the project directory
- Optionally opens Edge with a `sample-project-2` browser profile
- Moves all windows to **virtual desktop 3**

### Self-Referencing Config: workspace-orchestrator

**Config:** `workspace-orchestrator.workspace.yaml` (in repo root)

This config deploys the orchestrator project itself — useful for dogfooding. It opens VS Code, a PowerShell terminal tab, a Bash terminal tab, and File Explorer, all pointed at the repo root on virtual desktop 1.

### Multi-Project Workflow

You can have multiple projects deployed simultaneously on different virtual desktops:

```powershell
# Deploy project 1 to virtual desktop 2
ws deploy sample-project-1

# Deploy project 2 to virtual desktop 3 (project 1 stays open)
ws deploy sample-project-2

# Check both are deployed
ws status

# Stow only project 1 — project 2 windows are untouched
ws stow sample-project-1

# Or use switch for an atomic stow-all + deploy-one
ws switch sample-project-1
```

---

## Command Reference

### `ws deploy <project> [--dry-run] [-v]`

Launches all applications defined in the project config, positions them, and moves them to the configured virtual desktop.

- Uses saved snapshot positions if available (from a previous stow), falling back to config positions
- Records the deployment in the journal (`~/.workspaces/state.json`) for reliable stow
- `--dry-run` shows the step-by-step plan without opening any windows

### `ws stow [project] [--dry-run] [-v]`

Closes all windows belonging to the specified project. Uses the deploy journal to identify windows by HWND (for browsers) or by title pattern (for VS Code, Terminal, Explorer).

- Automatically captures a window position snapshot before closing (for next deploy)
- If no journal entry exists, falls back to convention-based matching

### `ws switch <project> [--dry-run] [-v]`

Atomic stow-all + deploy. Stows all currently deployed projects, then deploys the target.

### `ws list`

Displays a table of all available project configs found in the search path:
1. `~/.workspaces/*.workspace.yaml`
2. Current working directory
3. Directory containing `ws.exe`

### `ws status`

Shows currently deployed projects with deployment timestamps.

### `ws validate <project>`

Validates a project config file:
- Checks that referenced directories exist
- Verifies VS Code workspace path is valid
- Validates browser type and install path
- Reports any issues found

### `ws edit <project>`

Opens the project's `.workspace.yaml` file in VS Code for editing.

### `ws hotkeys list|start|stop`

Manages the global hotkey daemon:
- `list` — shows hotkey assignments from all project configs
- `start` — starts a blocking hotkey listener (run in a dedicated terminal)
- `stop` — kills the running daemon by PID file

### `ws snapshot [project] [--list] [--delete <project>]`

Manages window position snapshots:
- `ws snapshot` — captures positions for all deployed projects
- `ws snapshot <project>` — captures positions for one project
- `ws snapshot --list` — shows all saved snapshots
- `ws snapshot --delete <project>` — deletes a saved snapshot

---

## Creating a New Project from Scratch

### Step 1: Create the Config File

Create a file named `<your-project>.workspace.yaml` in one of:
- `~/.workspaces/` (recommended — shared across working directories)
- Your project directory
- The directory containing `ws.exe`

### Step 2: Define the Config

Start with this template and customize:

```yaml
version: 2
meta:
  name: "my-project"
  description: "Brief description of your project"

virtual_desktops:
  primary: 2          # Which virtual desktop to use (1-based)

hotkey: "Ctrl+Alt+3"  # Optional: global hotkey for quick deploy

applications:
  vscode:
    type: "vscode"
    workspace: "C:/path/to/your/project"   # Folder or .code-workspace file
    window:
      monitor: 0
      position: { x: 0, y: 0, width: 960, height: 1040 }

  terminals:
    type: "windows-terminal"
    window:
      monitor: 0
      position: { x: 960, y: 0, width: 960, height: 520 }
    tabs:
      - title: "Dev"
        directory: "C:/path/to/your/project"
        shell: "pwsh"                           # pwsh, cmd, or bash
        run_on_deploy: "npm run dev"            # Optional startup command
      - title: "Git"
        directory: "C:/path/to/your/project"
        shell: "pwsh"

  explorer:
    type: "file-explorer"
    paths:
      - "C:/path/to/your/project/src"
    window:
      monitor: 0
      position: { x: 960, y: 520, width: 960, height: 520 }

  # Optional: browser with a dedicated profile
  browser:
    type: "chrome"          # or "edge"
    profile: "my-project"   # Chrome creates this profile automatically
    urls:
      - "http://localhost:3000"
      - "https://github.com/you/my-project"
    window:
      monitor: 0
      position: { x: 0, y: 0, width: 1920, height: 1080 }
```

### Step 3: Validate

```powershell
ws validate my-project
```

This checks that all referenced paths exist and the config is syntactically valid.

### Step 4: Test with Dry Run

```powershell
ws deploy my-project --dry-run
```

Review the step-by-step plan. Make sure the apps and paths look correct.

### Step 5: Deploy

```powershell
ws deploy my-project
```

### Step 6: Adjust Positions

If windows don't land where you want them:

1. Manually arrange the windows to your liking
2. Run `ws snapshot my-project` to capture the current positions
3. On the next deploy, the snapshot positions will be used instead of the config positions

Alternatively, update the `position: { x, y, width, height }` values in your config file. See [Window Positioning Tips](#window-positioning-tips) for how to find the right coordinates.

### Supported Application Types

| Type | YAML `type` value | What it does |
|---|---|---|
| VS Code | `"vscode"` | Opens folder or `.code-workspace` file |
| Windows Terminal | `"windows-terminal"` or `"terminal"` | Opens WT with named tabs, per-tab directories, optional startup commands |
| File Explorer | `"file-explorer"` or `"explorer"` | Opens one Explorer window per path |
| Chrome | `"chrome"` | Opens Chrome with a dedicated profile and URLs |
| Edge | `"edge"` | Opens Edge with a dedicated profile and URLs |

### Config Tips

- **Paths:** Use forward slashes (`C:/dev/...`) in YAML for readability — the tool converts to backslashes for Windows APIs that need it
- **Virtual desktops:** Desktop numbers are 1-based. Desktop 1 is your default desktop. Don't assign projects to desktop 1 unless you want them on your main workspace.
- **Terminal tabs:** Each tab gets its own shell and working directory. Use `run_on_deploy` for dev servers, watchers, or any command that should start automatically.
- **Browser profiles:** Chrome/Edge auto-create profiles on first use. The profile preserves tabs, history, and extensions independently.
- **Multiple explorer paths:** Each path in the `paths` list opens a separate Explorer window. All share the same position config (they'll stack).

---

## Window Positioning Tips

### Finding Window Coordinates

To determine the exact `{x, y, width, height}` values for your layout:

**Method 1: Use the Snapshot Feature**
1. Manually arrange your windows where you want them
2. Deploy the project (even with approximate positions)
3. Rearrange windows manually
4. Run `ws snapshot my-project`
5. Check `~/.workspaces/snapshots/my-project.json` for the captured coordinates
6. Copy those values back into your config if you want them as the default

**Method 2: Use Windows Settings**
- Right-click the desktop → Display settings → note your screen resolution
- A typical 1920x1080 monitor with the taskbar at the bottom has usable space of approximately 1920x1040
- Common splits:
  - Left half: `{ x: 0, y: 0, width: 960, height: 1040 }`
  - Right half: `{ x: 960, y: 0, width: 960, height: 1040 }`
  - Right top: `{ x: 960, y: 0, width: 960, height: 520 }`
  - Right bottom: `{ x: 960, y: 520, width: 960, height: 520 }`
  - Full screen: `{ x: 0, y: 0, width: 1920, height: 1040 }`

**Method 3: PowerShell (advanced)**
```powershell
# Get the position of a specific window
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinPos {
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
$proc = Get-Process -Name "Code" | Select-Object -First 1
$rect = New-Object WinPos+RECT
[WinPos]::GetWindowRect($proc.MainWindowHandle, [ref]$rect)
"x: $($rect.Left), y: $($rect.Top), width: $($rect.Right - $rect.Left), height: $($rect.Bottom - $rect.Top)"
```

---

## Global Hotkeys

### Setup

1. Add a `hotkey` field to each project config:

```yaml
hotkey: "Ctrl+Alt+1"    # Project 1
hotkey: "Ctrl+Alt+2"    # Project 2
```

2. View registered hotkeys:

```powershell
ws hotkeys list
```

3. Start the hotkey daemon in a dedicated terminal:

```powershell
ws hotkeys start
```

The daemon blocks the terminal — it listens for hotkey events in a Win32 message loop. Press the configured hotkey combination from any application to trigger a deploy of that project (stowing all others first).

4. Stop the daemon:

```powershell
ws hotkeys stop       # From another terminal
# Or press Ctrl+C in the daemon terminal
```

### Supported Key Combinations

- **Modifiers:** `Ctrl`, `Alt`, `Shift`, `Win`
- **Keys:** `A`–`Z`, `0`–`9`, `F1`–`F24`, `Space`, `Enter`, `Esc`, `Tab`, arrow keys, `Backspace`, `Delete`, `Home`, `End`, `PageUp`, `PageDown`
- **Examples:** `Ctrl+Alt+1`, `Ctrl+Shift+F5`, `Win+Alt+P`

---

## Window Snapshots

Snapshots save the current positions of all windows for a deployed project. On the next deploy, snapshot positions take priority over config positions — so if you've manually rearranged your layout, the tool remembers.

### Automatic Snapshots

Every time you `ws stow` a project, the tool automatically captures a snapshot of all project window positions before closing them. No action needed.

### Manual Snapshots

```powershell
# Capture current positions without stowing
ws snapshot my-project

# List all saved snapshots
ws snapshot --list

# Delete a snapshot (next deploy will use config positions)
ws snapshot --delete my-project
```

### How It Works

- Snapshots are stored at `~/.workspaces/snapshots/<project>.json`
- Each snapshot records: app type, window position (x, y, width, height), and capture timestamp
- On deploy: for each app, the tool checks for a snapshot position first, then falls back to the config position
- Snapshots are overwritten on each stow — only the most recent layout is preserved

---

## Troubleshooting

### `ws list` shows no projects

The tool searches for `*.workspace.yaml` files in:
1. `~/.workspaces/` (i.e., `C:\Users\<you>\.workspaces\`)
2. The current working directory
3. The directory containing `ws.exe`

Make sure your config files are in one of these locations. Run `install.ps1` to copy sample configs to `~/.workspaces/`.

### Deploy opens apps but doesn't position them

Window positioning depends on the tool finding the window handle after launch. If the app takes too long to create its window, the polling timeout (15–20 seconds) may expire. Check `-v` (verbose) output for `[WARN]` messages.

### Stow closes the wrong windows

Stow uses title-pattern matching from the deploy journal. If you deployed with a different tool (e.g., the Phase 1 PowerShell scripts), there's no journal entry and stow falls back to convention-based matching, which is less precise. Re-deploy with `ws deploy` to create a proper journal entry.

### Terminal window not found during stow

Windows Terminal windows are identified by the `--window ws-<project>` naming convention. If WT renames the window (e.g., after a tab change), the title pattern may not match. The deploy journal records the HWND as a fallback.

### Virtual desktop operations fail

Ensure the MScholtes VirtualDesktop PowerShell module is installed:
```powershell
Get-Module -ListAvailable VirtualDesktop
# If not installed:
Install-Module -Name VirtualDesktop -Scope CurrentUser
```

Also verify PowerShell 7 is at the expected path:
```powershell
& "C:\Program Files\PowerShell\7\pwsh.exe" -Version
```

### NuGet package restore fails (NU1100 errors)

If `install.ps1` fails with errors like `Unable to resolve 'YamlDotNet'` or `Unable to resolve 'Spectre.Console'`, the NuGet package source is not configured. This is common on fresh .NET SDK installs.

```powershell
# Check configured NuGet sources
dotnet nuget list source

# If nuget.org is missing, add it:
dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org

# Then re-run the install
pwsh -File .\install.ps1
```

If nuget.org is listed but restore still fails, the machine may be behind a firewall or proxy that blocks outbound HTTPS to `api.nuget.org`. Check with your network administrator.

### Build errors

```powershell
# Verify .NET 8 SDK
& "C:\Program Files\dotnet\dotnet.exe" --list-sdks
# Should show 8.0.x

# Clean rebuild
& "C:\Program Files\dotnet\dotnet.exe" clean WorkspaceOrchestrator.sln
& "C:\Program Files\dotnet\dotnet.exe" build WorkspaceOrchestrator.sln
```

---

## Phase 3: Smarter Config Population

One of the biggest friction points in the MVP is hand-writing the `.workspace.yaml` files. You need to know exact window positions, correct paths, app types, and terminal configurations. Phase 3 plans several features to make this dramatically easier.

### Scan & Create Wizard (P3.3)

The most impactful improvement: a `ws scan` command that captures your current desktop state and generates a draft config file.

**How it would work:**
1. Arrange your desktop exactly how you want it — open VS Code, terminals, Explorer, browser
2. Run `ws scan my-new-project`
3. The tool enumerates all visible windows via `EnumWindows`
4. For each window, it identifies:
   - Application type (VS Code, WT, Chrome, Explorer, etc.) by process name and window class
   - Current position and size via `GetWindowRect`
   - Current virtual desktop assignment
   - VS Code: workspace path from the window title
   - Explorer: folder path from the window title
   - Terminal: current working directory per tab (if queryable)
   - Browser: open tab URLs via Chrome DevTools Protocol (if launched with `--remote-debugging-port`)
5. Generates `my-new-project.workspace.yaml` with all discovered data pre-filled
6. User reviews, tweaks, and saves

This transforms project creation from a 10-minute manual process to a 30-second scan + review.

### Automatic Position Discovery

The current snapshot feature already captures positions on stow. Phase 3 could extend this to:

- **Initial position learning:** Deploy with approximate positions, manually adjust, and the tool automatically updates the config file (not just a separate snapshot)
- **Position suggestions:** When creating a new config, suggest common layouts based on monitor resolution (e.g., "left-half / right-top / right-bottom" for 1920x1080)

### Monitor Profile Auto-Detection (P3.2)

Currently, each config has hardcoded `{x, y, width, height}` values that only work for one monitor arrangement. Phase 3's monitor profiles would:

- Auto-detect your current monitors (count, resolution, arrangement) via `EnumDisplayMonitors`
- Match against named profiles (e.g., "home-office", "laptop-only", "office-docked")
- Select the right set of window positions for the current setup
- Enable one config to work across laptop, desk, and multi-monitor setups

Config evolution:
```yaml
# Current MVP: one fixed layout
window:
  position: { x: 0, y: 0, width: 960, height: 1040 }

# Phase 3: per-profile layouts
window:
  home-office: { monitor: 0, position: { x: 0, y: 0, width: 1920, height: 1040 } }
  laptop-only: { monitor: 0, position: { x: 0, y: 0, width: 960, height: 540 } }
```

### Project Templates (P3.6)

Instead of starting from a blank file:

```powershell
ws new my-api --template web-fullstack
```

This would scaffold a config with sensible defaults for common project types:
- **web-fullstack:** VS Code + 3 terminal tabs (backend, frontend, git) + Chrome with localhost URLs + Explorer
- **data-science:** VS Code + Jupyter terminal + Explorer + Chrome for docs
- **documentation:** VS Code + terminal + browser for preview
- **devops:** VS Code + 4 terminal tabs (kubectl, docker, logs, ssh) + browser for dashboards

Users could also create custom templates from their existing configs:
```powershell
ws template save my-project --name "my-custom-layout"
ws new another-project --template my-custom-layout
```

### Smart Defaults and Auto-Completion

Future config improvements could include:

- **Path auto-expansion:** `~` expands to home directory, `$PROJECT` expands to the config's base directory
- **Relative paths:** Paths relative to the project root instead of absolute paths everywhere
- **Shell detection:** Auto-detect available shells (pwsh, bash, cmd, wsl) and offer them in completions
- **Port scanning:** For `run_on_deploy` commands that start servers, auto-detect the port and pre-fill browser URLs
- **Git integration:** Auto-detect the GitHub/GitLab URL from the project's `.git/config` and add it to browser URLs

### Browser Tab Discovery

The MVP's browser support launches URLs from the config, but doesn't capture what tabs the user has open. Phase 3 could:

- Launch Chrome with `--remote-debugging-port=9222` and use the Chrome DevTools Protocol to query open tabs
- Integrate with browser extensions (Session Buddy, Tab Session Manager) for tab export/import
- On stow: save all open tab URLs to the snapshot
- On re-deploy: restore exactly the tabs from the last session, not just the config defaults

These improvements would collectively transform the config authoring experience from "measure and type coordinates" to "arrange your desktop once and let the tool remember everything."
