# Project Workspace Orchestrator — Development Plan

**Companion to:** `workspace-orchestrator-spec.md`  
**Target:** Windows 11, developed in Claude Code  
**Language:** PowerShell (Phase 1) → C# .NET (Phase 2-3)

---

## Phase Overview

| Phase | Name | Goal | Duration Estimate |
|-------|------|------|-------------------|
| 1 | **Proof of Concept** | Validate hard technical problems; deploy/stow one project | 1–2 weeks |
| 2 | **Minimum Viable Product** | Daily-drivable for 2–3 real projects | 3–5 weeks |
| 3 | **Initial Release** | Full spec coverage, sustainable architecture, upgrade path | 4–6 weeks |

Each phase ends with a **Feedback Gate** — a structured pause to evaluate what worked, what didn't, and what the next phase should prioritize based on real experience.

---

## Phase 1: Proof of Concept

### Purpose

Retire the biggest technical risks early. If these work, the rest is straightforward engineering. If they don't, we learn what compromises to make before investing heavily.

### Deliverable

A set of PowerShell scripts (no GUI, no system tray) that can deploy and stow a single hardcoded project context.

### Capabilities

**P1.1 — Window Discovery & Positioning**
- Enumerate all windows on the desktop using Win32 `EnumWindows` via P/Invoke
- Given a launched process, reliably find its window handle(s) within a timeout
- Move and resize any window to exact pixel coordinates using `SetWindowPos`
- Detect current monitor arrangement (count, resolution, positions)
- **Success criteria:** Script launches Notepad, VS Code, and Explorer, then positions all three into a predefined layout within 5 seconds

**P1.2 — Virtual Desktop Management**
- Create, switch, and delete virtual desktops programmatically
- Move a window handle to a specific virtual desktop
- Query which desktop a window is currently on
- **Approach:** Use [MScholtes/VirtualDesktop](https://github.com/MScholtes/VirtualDesktop) PowerShell module, or raw COM interop
- **Success criteria:** Script creates desktop #3, moves a window to it, switches to it, switches back

**P1.3 — Windows Terminal Multi-Tab Launch**
- Launch Windows Terminal with multiple named tabs, each in a specific directory
- Send a command to a specific tab after launch (e.g., `npm run dev`)
- Close a specific Windows Terminal window gracefully
- **Success criteria:** Script launches WT with 3 tabs (backend, frontend, git) in correct directories, runs `echo "hello"` in the first tab

**P1.4 — Basic Deploy/Stow Cycle**
- Read a simplified YAML config file (project name, apps, positions)
- `deploy.ps1` — launch all apps, position windows, move to virtual desktops
- `stow.ps1` — close all project windows gracefully
- **Success criteria:** Full round-trip: deploy a project with VS Code + Terminal + Explorer, stow it, deploy again — all windows land in correct positions

### What Phase 1 Explicitly Skips

- Browser tab management (use manual profiles for now)
- Snapshot/state persistence (deploy always uses the config file)
- Multiple projects / context switching
- System tray / GUI
- Hotkeys
- Monitor profiles
- Office/OneNote integration
- Error handling beyond basic retries

### Technical Decisions to Make During Phase 1

| Decision | Options | Decide By |
|----------|---------|-----------|
| Primary language going forward | Stay PowerShell vs. migrate to C# | End of Phase 1 |
| Virtual desktop library | MScholtes module vs. raw COM vs. other | P1.2 |
| YAML parser | `powershell-yaml` module vs. JSON instead | P1.4 |
| Terminal command injection | `wt` CLI args vs. SendKeys vs. WT settings API | P1.3 |
| Window discovery strategy | PID tracking vs. title regex vs. class name | P1.1 |

### File Structure (Phase 1)

```
workspace-orchestrator/
├── README.md
├── lib/
│   ├── Win32.ps1              # P/Invoke wrappers for window APIs
│   ├── VirtualDesktop.ps1     # Virtual desktop management
│   ├── WindowManager.ps1      # Find, position, move windows
│   └── TerminalLauncher.ps1   # Windows Terminal orchestration
├── deploy.ps1                 # Main deploy script
├── stow.ps1                   # Main stow script
├── sample-project.yaml        # Example project context
└── test/
    ├── Test-WindowPositioning.ps1
    ├── Test-VirtualDesktop.ps1
    └── Test-TerminalLaunch.ps1
```

---

### Feedback Gate 1 → 2

**When:** After P1.4 is working reliably for one project.

**Evaluate:**

1. **Timing reliability** — How consistent is window positioning? Do we need longer delays? Smarter polling? What's the total deploy time?
2. **Virtual desktop stability** — Do the undocumented APIs work reliably? Do they survive Windows updates?
3. **Terminal experience** — Does the WT CLI approach work, or do we need a different strategy for tab management and command injection?
4. **Language decision** — Is PowerShell sufficient for Phase 2, or should we migrate to C# now? Key factors: P/Invoke ergonomics, async support, packaging, system tray capability.
5. **Scope refinement** — Based on using the PoC, what capabilities matter more/less than originally thought? What pain points emerged?

**Deliverable:** A short decision document (~1 page) capturing answers and any spec changes before starting Phase 2.

**Gate 1 completed:** 2026-03-22 — see `gate1-decisions.md`

---

## Investigation: Terminal Emulator Selection — ✅ COMPLETE (2026-03-23)

### Summary

A comprehensive investigation evaluated Windows Terminal, WezTerm, and Alacritty for their ability to solve the P2.1 blocking issue: reliable programmatic window identity for multi-project scenarios.

**Result:** ✅ **Stay with Windows Terminal** using the `--window <name>` feature.

### Key Findings

| Terminal | Result | Critical Finding |
|----------|--------|------------------|
| **Windows Terminal** | ✅ **Recommended** | `--window ws-<projectname>` sets the window title to the name, making it reliably queryable via Win32 APIs. Problem solved. |
| **WezTerm** | ❌ Rejected | Windows NOT enumerable via Win32 `EnumWindows()`. Cannot use `SetWindowPos` or `WM_CLOSE`. Architectural incompatibility. |
| **Alacritty** | ❌ Rejected | DLL_NOT_FOUND error on this system (compatibility issue). Even if fixed: no native tabs, would require tmux/zellij integration. |

### WT Solution Details

```bash
wt new-tab --window ws-project-1 --title "Tab" -d C:\dev
```

- Window title is automatically set to `'ws-project-1'`
- Title is queryable via Win32 `EnumWindows()` → `GetWindowText()`
- Multiple commands with same `--window <name>` append to same window (no duplicates)
- Window responds to `SetWindowPos` for positioning
- `WM_CLOSE` + fallback to `Stop-Process` for graceful closure
- **Impact:** Phase 2 P2.1 (Multi-Project Context Switching) is now UNBLOCKED

### Deliverables

- `terminal-investigation.md` — Complete test results, scoring table, recommendation
- `phase-2-readiness.md` — Implementation checklist for Phase 2
- `INVESTIGATION_SUMMARY.md` — Executive summary
- `terminal-investigation-plan.md` — Test methodology (for reference)

### WT Naming Convention for Phase 2 Implementation

All WT launches in Phase 2 must include the `--window ws-<projectname>` flag:

```powershell
# C# example (Phase 2)
$projectName = "myproject"
$wtArgs = @(
    "new-tab",
    "--window", "ws-$projectName",
    "--title", "Project Console",
    "-d", "C:\dev\$projectName"
)
Start-Process -FilePath "wt.exe" -ArgumentList $wtArgs

# Stow: Find and close by title pattern
$windows = Get-AllWindows | Where-Object { $_.Title -match "^ws-$projectName`$" }
foreach ($w in $windows) {
    Close-WindowGracefully -Hwnd $w.Hwnd
}
```

This ensures multi-project isolation without requiring a deploy journal hack or alternative terminals.

---

## Phase 2: Minimum Viable Product

### Purpose

Build something you can actually use every day for your real projects. Prioritize the daily workflow loop: switch to a project, work, switch to another project.

### Architecture Shift (Decided at Gate 1: Migrate to C#)

- Create a .NET 8 console application (CLI-first, GUI later)
- Port P/Invoke wrappers to proper C# interop classes
- Use `YamlDotNet` for config parsing
- Structure as a class library + CLI host (so GUI host can be added in Phase 3)
- Keep existing PowerShell PoC scripts as reference until C# tools fully replace them

### Capabilities

**P2.1 — Multi-Project Context Switching** ✅ NOW UNBLOCKED
- Support N project context files in `~/.workspaces/`
- Track which project(s) are currently deployed (state file)
- `switch` command: atomic stow-current + deploy-target
- Handle "no project deployed" as a valid state
- Support multiple projects deployed simultaneously on different virtual desktops
- **Terminal Identity:** Use `--window ws-<projectname>` convention in all WT launches
  - Window title automatically set to project name
  - Stow finds window by title pattern: `ws-<projectname>`
  - Enables reliable multi-project terminal management (this was the P2.1 blocker)

**P2.2 — Browser Profile Integration**
- On first `deploy`, create a Chrome/Edge profile for the project if it doesn't exist
- Launch browser with correct profile and initial URLs from config
- On stow, close the profile's window (Chrome auto-saves session)
- On re-deploy, Chrome restores the profile's last session automatically
- Document the one-time setup steps for the user (profile creation)

**P2.3 — Window State Snapshots**
- On stow, capture current positions/sizes of all project windows
- On deploy, prefer snapshot positions over config positions (if snapshot exists)
- `snapshot` command: save current state without stowing
- Snapshot captures browser tab URLs (via CDP if available, else skip gracefully)

**P2.4 — CLI Interface**
- Proper command-line interface with subcommands:
  ```
  ws deploy <project>
  ws stow [project]
  ws switch <project>
  ws list
  ws status
  ws snapshot [project]
  ws validate <project>
  ws edit <project>
  ```
- Tab completion for project names
- Colored output with progress indicators
- `--dry-run` flag on deploy/stow

**P2.5 — Global Hotkeys**
- Register global hotkeys for quick switching
- Default: `Ctrl+Alt+1` through `Ctrl+Alt+9` for projects
- `Ctrl+Alt+0` for stow-all
- Hotkey registration via `RegisterHotKey` Win32 API
- Runs as a background process (or integrate with system tray in Phase 3)

**P2.6 — Robust Error Handling & Timing**
- Configurable per-app launch timeout
- Retry logic for window discovery (exponential backoff)
- Graceful degradation: if one app fails to launch, continue with the rest and report
- Deploy journal: log what was launched so stow can clean up even after partial deploys
- Handle "app already running" scenarios (reuse window vs. launch new)

**P2.7 — Office & OneNote Support**
- Launch Office documents by path; position their windows
- OneNote via protocol handler
- On stow, save & close via COM automation (best-effort; fall back to window close)

### What Phase 2 Explicitly Skips

- System tray GUI (hotkey process is headless)
- Monitor profile switching (assumes one monitor setup)
- "Scan & Create" wizard
- Docker/container orchestration
- Cloud sync
- GlazeWM integration
- Project templates

### File Structure (Phase 2 — C# version)

```
workspace-orchestrator/
├── README.md
├── docs/
│   ├── spec.md
│   └── plan.md
├── src/
│   ├── WorkspaceOrchestrator.Core/        # Class library
│   │   ├── Models/
│   │   │   ├── ProjectContext.cs           # YAML deserialization models
│   │   │   ├── Snapshot.cs
│   │   │   └── MonitorProfile.cs
│   │   ├── Services/
│   │   │   ├── WindowManager.cs           # Win32 window operations
│   │   │   ├── VirtualDesktopManager.cs   # Virtual desktop COM interop
│   │   │   ├── TerminalLauncher.cs        # Windows Terminal orchestration
│   │   │   ├── BrowserLauncher.cs         # Chrome/Edge profile management
│   │   │   ├── OfficeLauncher.cs          # Office COM automation
│   │   │   ├── DeployService.cs           # Orchestrates full deploy
│   │   │   ├── StowService.cs             # Orchestrates full stow
│   │   │   └── SnapshotService.cs         # State capture/restore
│   │   ├── Interop/
│   │   │   ├── Win32.cs                   # P/Invoke declarations
│   │   │   └── VirtualDesktopCom.cs       # COM interface definitions
│   │   └── Config/
│   │       ├── ConfigLoader.cs
│   │       └── StateManager.cs
│   ├── WorkspaceOrchestrator.Cli/         # CLI host
│   │   ├── Program.cs
│   │   └── Commands/
│   │       ├── DeployCommand.cs
│   │       ├── StowCommand.cs
│   │       ├── SwitchCommand.cs
│   │       ├── ListCommand.cs
│   │       └── StatusCommand.cs
│   └── WorkspaceOrchestrator.Hotkeys/     # Background hotkey listener
│       └── HotkeyService.cs
├── tests/
│   ├── WindowManagerTests.cs
│   └── DeployStowIntegrationTests.cs
└── samples/
    ├── fullstack-web.workspace.yaml
    ├── data-science.workspace.yaml
    └── writing.workspace.yaml
```

---

### Feedback Gate 2 → 3

**When:** After using the MVP daily for at least 1–2 weeks with 2–3 real projects.

**Evaluate:**

1. **Daily workflow friction** — Where do you lose time? What's the most annoying manual step that remains? What breaks most often?
2. **Config file ergonomics** — Is the YAML format right? Too verbose? Missing fields? Are you editing configs often or are they stable?
3. **Context switch speed** — How long does a full switch take? Is it fast enough to use reflexively, or do you hesitate because it's slow?
4. **Browser approach** — Is the dedicated-profile strategy working? Do you miss tab group fidelity? Are there profile management headaches?
5. **Virtual desktop experience** — Is the API stable? Do you actually use multiple desktops, or has your workflow evolved?
6. **Snapshot reliability** — Are snapshots accurate? Do you prefer snapshot state or config state on re-deploy?
7. **Missing capabilities** — What do you reach for that isn't there? What from the Phase 3 list matters most?
8. **Architecture assessment** — Is the codebase maintainable? Are there performance bottlenecks? What needs refactoring before Phase 3?

**Deliverable:** A prioritized punch list for Phase 3, plus any spec revisions. Rank each Phase 3 feature by actual need (not theoretical desire).

---

## Phase 3: Initial Release

### Purpose

Address all pain points from MVP daily-driving. Build a polished, sustainable tool with a proper GUI and an architecture that supports future extension.

### Capabilities

**P3.1 — System Tray Application**
- Persistent system tray icon showing current project (color-coded or with project icon)
- Right-click menu: project list, stow, status, settings, edit config, quit
- Left-click: quick-switch popup (searchable project list)
- Toast notifications for deploy/stow completion or errors
- Runs on Windows startup (optional)
- Built with WinUI 3 or WPF NotifyIcon

**P3.2 — Monitor Profile Support**
- Define named monitor profiles in `monitors.yaml`
- Auto-detect current monitor arrangement on deploy
- Match against known profiles; select correct window positions
- Fallback layout strategy when no profile matches (e.g., auto-tile on primary monitor)
- Per-application, per-profile position overrides in project context files

**P3.3 — "Scan & Create" Wizard**
- Command: `ws scan` or tray menu option
- Captures current desktop state: all windows, positions, virtual desktop assignments
- Identifies applications and infers their type (VS Code, Terminal, Chrome, etc.)
- For terminals: captures current working directory per tab
- For browsers: captures open tab URLs (via CDP or extension)
- Generates a draft `.workspace.yaml` file for the user to review and edit
- Dramatically lowers the barrier to creating new project contexts

**P3.4 — Shared / Pinned Windows**
- Global config for windows that should survive stow operations:
  ```yaml
  # ~/.workspaces/settings.yaml
  pinned:
    - process: "Spotify"
    - process: "Discord"
    - process: "ms-teams"
    - title_contains: "Outlook"
  ```
- Stow skips these windows; deploy doesn't touch them
- Optional: move pinned windows to a dedicated "always-on" virtual desktop

**P3.5 — Advanced Terminal Features**
- Support WSL tabs (distro selection, Linux paths)
- Support SSH sessions with auto-reconnect on deploy
- Named terminal window per project (so multiple WT windows can coexist)
- Configurable stow behavior per tab: send SIGINT, send specific command, just close
- Support for split panes (not just tabs) via `wt` pane layout args

**P3.6 — Project Templates**
- Bundled templates: `web-fullstack`, `data-science`, `documentation`, `devops`
- `ws new myproject --template web-fullstack` scaffolds a context file with sensible defaults
- User can create custom templates from existing project contexts

**P3.7 — Docker / Container Integration**
- Optional `containers` section in project context:
  ```yaml
  containers:
    docker_compose:
      file: "C:/dev/myapp/docker-compose.yml"
      services: ["postgres", "redis"]
      deploy_action: "up -d"
      stow_action: "stop"
  ```
- On deploy: `docker compose up -d` for project services
- On stow: `docker compose stop` (not `down`, to preserve data)
- Status check: verify containers are running before marking deploy complete

**P3.8 — Resilience & Recovery**
- Deploy journal with rollback capability
- Watchdog: detect if project windows are closed externally, update state
- "Resume" command: re-deploy a project that was partially stowed or crashed
- Automatic backup of config files before edits
- Health check command: verify all project windows are still running and positioned correctly

**P3.9 — Configuration Validation & Linting**
- `ws validate` checks for: missing files/paths, port conflicts between projects, invalid YAML, unknown application types, unreachable URLs
- Warnings for likely issues: same hotkey on two projects, overlapping window positions, missing monitor profile for current setup
- Suggestions: "You have 3 terminals in this project but no `run_on_deploy` — did you mean to add commands?"

**P3.10 — Documentation & Onboarding**
- `ws help` with detailed subcommand help
- `ws doctor` — diagnose common issues (WT version, Chrome path, virtual desktop API availability)
- README with quickstart guide
- Sample configs for common project types
- Troubleshooting guide for known issues (window positioning race conditions, Chrome profile quirks, etc.)

### What Phase 3 Defers to Future Releases

- Cloud sync of configs
- GlazeWM deep integration (alternative to virtual desktops)
- Time tracking
- Focus mode / distraction blocking
- Notification routing
- Multi-machine support
- Plugin/extension API

---

## Cross-Cutting Concerns (All Phases)

### Logging

- Phase 1: `Write-Host` / `Write-Verbose` output
- Phase 2: Structured logging to file (`~/.workspaces/logs/`)
- Phase 3: Log levels (debug/info/warn/error), log rotation, `ws logs` command

### Testing Strategy

- **Phase 1:** Manual test scripts that launch, position, and verify windows
- **Phase 2:** Integration tests that exercise deploy/stow cycles; mock Win32 APIs for unit tests on business logic
- **Phase 3:** Automated regression tests; "golden snapshot" tests comparing expected vs. actual window state

### Configuration Versioning

- Include a `version` field in context files from Phase 2 onward
- Migrations: when the schema changes, auto-upgrade old configs (or warn)
- Keep backward compatibility within a major version

---

## Risk Register

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Virtual desktop COM API breaks on Windows update | High | Medium | Pin to known-good Windows builds; abstract behind interface; test on Insider builds |
| Chrome profile approach has UX friction | Medium | Medium | Document setup clearly; consider browser extension if profiles don't work well |
| Window positioning race conditions | Medium | High | Generous timeouts; retry logic; user-configurable delays |
| `wt` CLI doesn't support needed features | Medium | Low | Fall back to SendKeys; monitor WT release notes for new capabilities |
| PowerShell → C# migration is costly | Medium | Medium | Keep Phase 1 modular; treat it as throwaway prototyping code |
| Office COM automation is brittle | Low | High | Make Office support best-effort; fall back to simple `Start-Process` |
| Project scope creep | High | High | Feedback gates enforce scope decisions; defer aggressively to future phases |

---

## Getting Started Checklist (Phase 1, Day 1)

1. Create `workspace-orchestrator/` repo
2. Install prerequisites:
   - PowerShell 7+
   - Windows Terminal (latest)
   - `powershell-yaml` module (`Install-Module powershell-yaml`)
   - [VirtualDesktop](https://github.com/MScholtes/VirtualDesktop) module
3. Write `lib/Win32.ps1` — P/Invoke wrappers for `EnumWindows`, `SetWindowPos`, `GetWindowRect`
4. Write `Test-WindowPositioning.ps1` — launch Notepad, find its window, move it to (100, 100, 800, 600)
5. If that works → proceed to P1.2 (virtual desktops)
6. If P/Invoke is painful → evaluate switching to C# immediately

---

## Revision History

| Date | Change |
|------|--------|
| 2026-03-10 | Initial draft |
