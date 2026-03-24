# Phase 2 — MVP Implementation Plan

**Date:** 2026-03-23
**Status:** IN PROGRESS
**Architect:** Claude Sonnet 4.6
**Inputs:** `poc-status.md`, `workspace-orchestrator-plan.md`, `phase-2-readiness.md`, `gate1-decisions.md`

---

## Objective

Build a daily-drivable CLI tool for managing 2–3 real projects. The user should be able to
`ws deploy myproject`, `ws stow myproject`, and `ws switch otherproject` with reliable
window positioning, virtual desktop management, and terminal identity tracking.

---

## Architecture Decisions

### Language: C# .NET 8
Decided at Gate 1. Provides full Win32 API access, proper system tray (Phase 3), strong
typing, NuGet packaging, and better async support than PowerShell.

### Virtual Desktop Strategy: PowerShell Bridge (Phase 2 → Full COM in Phase 3)
The MScholtes VirtualDesktop PS module (v1.5.11) is proven and stable on this system.
Replicating its undocumented COM interface GUIDs in C# introduces a maintenance burden
(GUIDs change between Windows builds). For Phase 2 MVP:
- Implement a thin PowerShell bridge script (`lib/VirtualDesktopBridge.ps1`)
- `VirtualDesktopService.cs` calls `pwsh.exe` with this bridge script
- Operations are low-frequency (once per deploy/stow), so subprocess overhead is negligible
- Replace with full COM interop in Phase 3 once architecture is stable

### CLI Framework: System.CommandLine
Microsoft's official CLI library. Provides subcommands, help generation, and tab completion.
Lighter than Spectre.Console.Cli for Phase 2; can wrap output with Spectre.Console for colors.

### Config Location: ~/.workspaces/ (with fallback to script directory)
Phase 2 establishes the permanent config home at `~/.workspaces/` (i.e., `%USERPROFILE%\.workspaces\`).
Backward-compatible fallback to the script directory for existing Phase 1 YAML files.

### Terminal Identity: `--window ws-<projectname>` Convention
Decided during terminal investigation. All WT launches include this flag so the window
title becomes the project name, enabling reliable multi-project stow.

### Deploy Journal: JSON State File at `~/.workspaces/state.json`
Tracks all deployed projects with their window handles and match patterns. Used by stow to
clean up even after partial deploys or WT renames.

---

## Project Structure

```
workspace-orchestrator/
├── src/
│   ├── WorkspaceOrchestrator.Core/
│   │   ├── WorkspaceOrchestrator.Core.csproj
│   │   ├── Interop/
│   │   │   └── Win32.cs                    # P/Invoke: EnumWindows, SetWindowPos, etc.
│   │   ├── Models/
│   │   │   ├── ProjectContext.cs            # YAML deserialization root
│   │   │   ├── AppConfig.cs                 # Per-app configuration (type, window, tabs)
│   │   │   ├── WindowInfo.cs                # Hwnd + title + position data
│   │   │   └── DeployedProject.cs           # State journal model
│   │   ├── Services/
│   │   │   ├── WindowManager.cs             # Find, position, close windows
│   │   │   ├── VirtualDesktopService.cs     # VD ops via PS bridge subprocess
│   │   │   ├── TerminalLauncher.cs          # WT launch with --window naming
│   │   │   ├── DeployService.cs             # Orchestrate full deploy
│   │   │   └── StowService.cs               # Orchestrate full stow
│   │   └── Config/
│   │       ├── ConfigLoader.cs              # Find + parse .workspace.yaml
│   │       └── StateManager.cs              # Read/write ~/.workspaces/state.json
│   └── WorkspaceOrchestrator.Cli/
│       ├── WorkspaceOrchestrator.Cli.csproj
│       ├── Program.cs                       # System.CommandLine root
│       └── Commands/
│           ├── DeployCommand.cs
│           ├── StowCommand.cs
│           ├── SwitchCommand.cs
│           ├── ListCommand.cs
│           └── StatusCommand.cs
├── lib/
│   ├── VirtualDesktopBridge.ps1             # NEW: PS bridge for VD operations
│   ├── Win32.ps1                            # Phase 1 reference (keep as-is)
│   ├── WindowManager.ps1                    # Phase 1 reference
│   ├── VirtualDesktop.ps1                   # Phase 1 reference
│   └── TerminalLauncher.ps1                 # Phase 1 reference
├── samples/
│   ├── sample-project-1.workspace.yaml      # Updated with schema v2
│   └── sample-project-2.workspace.yaml      # Updated with schema v2
└── [existing Phase 1 files — unchanged]
```

---

## YAML Schema v2

The Phase 2 config schema adds `version`, `hotkey`, and `browser` fields while remaining
backward compatible with Phase 1 configs (missing fields are treated as defaults).

```yaml
version: 2
meta:
  name: "myproject"
  description: "My project description"

virtual_desktops:
  primary: 2          # 1-based desktop number

hotkey: "Ctrl+Alt+1"  # optional global hotkey (Phase 2.5)

applications:
  vscode:
    type: "vscode"
    workspace: "C:/dev/myproject"
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
        directory: "C:/dev/myproject"
        shell: "pwsh"
        run_on_deploy: "npm run dev"   # optional startup command
      - title: "Git"
        directory: "C:/dev/myproject"
        shell: "pwsh"

  explorer:
    type: "file-explorer"
    paths:
      - "C:/dev/myproject"
    window:
      monitor: 0
      position: { x: 960, y: 520, width: 960, height: 520 }

  browser:              # Phase 2.2 — optional
    type: "chrome"      # or "edge"
    profile: "myproject"
    urls:
      - "http://localhost:3000"
      - "https://github.com/user/myproject"
    window:
      monitor: 0
      position: { x: 0, y: 0, width: 1920, height: 1080 }
```

---

## Deploy Journal Schema

Stored at `~/.workspaces/state.json`:

```json
{
  "version": 1,
  "deployedProjects": [
    {
      "project": "myproject",
      "deployedAt": "2026-03-23T10:00:00Z",
      "configPath": "C:/Users/AgentUser01/.workspaces/myproject.workspace.yaml",
      "apps": [
        {
          "type": "terminal",
          "titlePattern": "^ws-myproject$",
          "hwnd": 0
        },
        {
          "type": "vscode",
          "titlePattern": "Visual Studio Code",
          "hwnd": 0
        },
        {
          "type": "explorer",
          "titlePattern": "myproject",
          "hwnd": 0
        }
      ]
    }
  ]
}
```

HWNDs are recorded when found (best-effort) and set to 0 if not captured. Stow uses
titlePattern as the reliable fallback when HWNDs are 0 or stale.

---

## CLI Commands

```
ws deploy <project>       Deploy a project context (launch + position + move to desktop)
ws stow [project]         Stow a project context (close all project windows)
ws switch <project>       Atomic stow-current + deploy-target
ws list                   List all available project configs
ws status                 Show currently deployed projects
ws validate <project>     Validate a project config file
```

All commands support:
- `--dry-run`: show what would be done without doing it
- `--verbose`: extra diagnostic output
- Colored output: green=success, yellow=warning, red=error, cyan=info

---

## Sprint Plan

### Sprint 1: Core Foundation (P2.4 + P2.1 + P2.6)
**Goal:** `ws deploy <project>` and `ws stow <project>` working reliably with multi-project isolation.

Tasks:
1. ✅ Create .NET 8 solution and project files
2. ✅ Implement `Win32.cs` — P/Invoke wrappers
3. ✅ Implement `WindowManager.cs` — Find, position, close
4. ✅ Implement `VirtualDesktopBridge.ps1` — PS bridge script
5. ✅ Implement `VirtualDesktopService.cs` — calls PS bridge
6. ✅ Implement `TerminalLauncher.cs` — WT with --window convention
7. ✅ Implement YAML models (`ProjectContext.cs`, etc.)
8. ✅ Implement `ConfigLoader.cs` — find + parse configs
9. ✅ Implement `StateManager.cs` — deploy journal
10. ✅ Implement `DeployService.cs` — full deploy orchestration
11. ✅ Implement `StowService.cs` — full stow orchestration
12. ✅ Implement CLI with `ws deploy`, `ws stow`, `ws list`, `ws status`
13. ✅ Implement `ws switch` command
14. ✅ Build passes, basic smoke test

### Sprint 2: Polish + Browser (P2.2 + P2.4 finalization)
**Goal:** Browser profile management; tab completion; dry-run; improved error messages.

Tasks:
1. Browser profile lifecycle (Chrome/Edge)
2. `ws validate` command
3. Tab completion for project names
4. `--dry-run` flag on deploy/stow
5. Spectre.Console styled output with progress spinners
6. `ws edit <project>` opens config in VS Code

### Sprint 3: Global Hotkeys (P2.5)
**Goal:** `Ctrl+Alt+1–9` hotkeys for quick project switching.

Tasks:
1. `HotkeyService.cs` — RegisterHotKey/UnregisterHotKey P/Invoke
2. Background listener thread
3. Parse hotkeys from project configs
4. `ws hotkeys start/stop` commands

### Sprint 4: Window State Snapshots (P2.3 — stretch goal)
**Goal:** Stow captures current window positions; deploy restores them.

Tasks:
1. `SnapshotService.cs` — capture all project window positions on stow
2. Persist snapshot alongside state.json
3. `ws snapshot [project]` command
4. Deploy prefers snapshot positions if available

---

## Key Design Decisions

### Window Closing: WM_CLOSE + Process fallback
Same as Phase 1. Send WM_CLOSE; if window survives 800ms (confirmation dialog), send
WM_CLOSE again to dismiss. If still alive after another 800ms, log warning and continue.
Do NOT use Stop-Process except for confirmed test scenarios.

### Multi-Project Isolation: Title-Pattern Stow
Stow finds windows by title pattern from the deploy journal, not by process name.
- WT: title pattern `^ws-<projectname>$` (exact match, the --window name)
- VS Code: title pattern contains the workspace folder name
- Explorer: title pattern contains the folder name
- Browser: process + profile directory match

### Error Handling: Graceful Degradation
If one app fails to launch or position, log the error and continue with the rest.
Stow always cleans up everything it can, even after partial failures.
The deploy journal enables idempotent re-deploy ("already deployed" state).

### Config Search Order
1. `~/.workspaces/<project>.workspace.yaml`
2. `<current-dir>/<project>.workspace.yaml`
3. `<ws.exe-dir>/<project>.workspace.yaml`

### Binary Location
- Development: `dotnet run --project src/WorkspaceOrchestrator.Cli`
- Published: `ws.exe` in `~/.workspaces/bin/` on PATH
- Publish command: `dotnet publish -c Release -r win-x64 --self-contained -o ~/.workspaces/bin/`

---

## Testing Plan

### Unit Tests (WorkspaceOrchestrator.Tests project — Sprint 2)
- `ConfigLoaderTests` — parse valid YAML, missing file, invalid YAML
- `StateManagerTests` — read/write state.json, handle missing file
- `WindowManagerTests` — mock Win32 APIs, test Find logic

### Integration Tests (Phase 1 PS scripts + new C# tests)
- Phase 1 test suite must continue to pass (regression baseline)
- New: deploy + stow two projects simultaneously, verify isolation
- New: `ws switch` test — A→B leaves only B windows open

### Manual Smoke Tests (each sprint)
- `ws list` shows available projects
- `ws deploy sample-project-1` opens correct windows in correct positions
- `ws stow sample-project-1` closes only sample-project-1 windows
- `ws switch sample-project-2` closes project-1, opens project-2

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| VirtualDesktop PS bridge adds startup latency | Low | Bridge is called once per operation; 200-400ms acceptable |
| YamlDotNet schema changes break existing configs | Low | Use `NamingConventions.UnderscoredNamingConvention` + optional fields |
| System.CommandLine API instability | Low | Pin to stable 2.x release |
| Win32 P/Invoke DllImport errors on this system | Low | Already proven in Phase 1 PS inline C# |
| MScholtes VirtualDesktop module version mismatch | Medium | Bridge script uses already-installed module; no version change needed |

---

## Success Criteria for Phase 2 Gate

- [ ] `ws deploy <project>` works for all sample projects
- [ ] `ws stow <project>` closes only that project's windows (not other projects')
- [ ] `ws switch <project>` performs atomic stow+deploy
- [ ] `ws list` and `ws status` display correct information
- [ ] Two projects can be deployed simultaneously on different virtual desktops
- [ ] Deploy journal correctly handles "already deployed" and "partially deployed" cases
- [ ] All Phase 1 tests still pass (no regressions)
- [ ] Binary can be published as a self-contained `ws.exe`
