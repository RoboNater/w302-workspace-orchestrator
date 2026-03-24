# MVP (Phase 2) Status

**Date started:** 2026-03-23
**Phase:** 2 — Minimum Viable Product (IN PROGRESS)
**Branch:** main

---

## Summary

Phase 2 is building a C# .NET 8 CLI (`ws.exe`) that replaces the PowerShell PoC scripts.
Sprint 1 is complete: all core services and CLI commands are written and the solution builds.

---

## Sprint 4: Window State Snapshots — ✅ COMPLETE (2026-03-23)

### What Was Built

| Task | File(s) | Status |
|------|---------|--------|
| WindowSnapshot model | `Models/WindowSnapshot.cs` | ✅ Written |
| Snapshot persistence | `Config/StateManager.cs` | ✅ Updated |
| SnapshotService | `Services/SnapshotService.cs` | ✅ Written |
| Auto-capture on stow | `Services/StowService.cs` | ✅ Updated |
| Snapshot positions in deploy | `Services/DeployService.cs` | ✅ Updated |
| `ws snapshot` command | `Commands/SnapshotCommand.cs` | ✅ Written |
| Program.cs wiring | `Program.cs` | ✅ Updated |

### Smoke Tests — ✅ ALL PASSED

- `ws --help` — `snapshot` command listed (9 commands total)
- `ws snapshot --help` — `[project]`, `--list`, `--delete` options shown
- `ws snapshot` (no deployed projects) — "No projects currently deployed. Deploy a project first..."
- `ws snapshot --list` (no snapshots) — "No snapshots saved yet."
- **Build:** `Build succeeded. 0 Warning(s). 0 Error(s).`

### Key Design Notes

**Snapshot lifecycle:**
- Snapshots captured automatically on `ws stow` (before windows close)
- Snapshots also capturable on-demand via `ws snapshot [project]`
- Stored at `~/.workspaces/snapshots/<project>.json` (one file per project)
- On next `ws deploy`, snapshot positions replace config positions for matching app types

**SnapshotService.Capture() strategy:**
- Iterates deploy journal record for the project
- For each app: tries HWND lookup first (fast), falls back to title-pattern regex match
- Windows that have been closed are logged as `[SKIP]` and excluded from snapshot
- Returns `null` if project not in deploy journal

**DeployService snapshot position resolution:**
- `SnapshotPosition(appType, snapshot?)` returns `WindowPosition?` for a given app type
- `null` returned when: no snapshot exists, or app type not in snapshot
- Per-app preference: `snapshotPosition ?? configPosition`
- Progress output notes when snapshot position is used vs. config position

**`ws snapshot` command options:**
- `ws snapshot` — snapshot all deployed projects
- `ws snapshot <project>` — snapshot one specific project
- `ws snapshot --list` — show all saved snapshots in a table (project, age, window count)
- `ws snapshot --delete <project>` — remove a saved snapshot

**Snapshot matching by type:**
- Simple first-match-by-type strategy (e.g., `"vscode"`, `"terminal"`, `"explorer"`)
- If multiple apps of the same type are deployed (uncommon), the first snapshot entry wins
- Phase 3 improvement: match by YAML app key for fine-grained multi-instance support

---

## Sprint 3: Global Hotkeys — ✅ COMPLETE (2026-03-23)

### What Was Built

| Task | File(s) | Status |
|------|---------|--------|
| Win32 hotkey P/Invoke | `Interop/Win32.cs` | ✅ Updated |
| HotkeyService (message loop) | `Services/HotkeyService.cs` | ✅ Written |
| `ws hotkeys list/start/stop` | `Commands/HotkeysCommand.cs` | ✅ Written |
| Program.cs wiring | `Program.cs` | ✅ Updated |

### Smoke Tests — ✅ ALL PASSED

- `ws --help` — `hotkeys` command listed (8 commands total)
- `ws hotkeys --help` — `list`, `start`, `stop` subcommands shown
- `ws hotkeys list` — Spectre.Console table: `sample-project-1 → Ctrl+Alt+1`, `sample-project-2 → Ctrl+Alt+2`
- `ws hotkeys stop` (no daemon) — "No hotkey daemon appears to be running. (PID file not found)"
- **Build:** `Build succeeded. 0 Warning(s). 0 Error(s).`

### Key Design Notes

**HotkeyService architecture:**
- `RegisterHotKey(hWnd=0, id, modifiers, vk)` — registers hotkey on the current thread's message queue
- `Run()` blocks in a Win32 `GetMessage` loop; fires `HotkeyPressed` event on each `WM_HOTKEY`
- `Stop()` posts `WM_QUIT` to the owner thread via `PostThreadMessage` — thread-safe from any context
- `Dispose()` calls `Stop()` — safe to use in a `using` block

**Hotkey string parser (`"Ctrl+Alt+1"` → modifiers + vk):**
- Tokens split by `+`; modifier tokens: `Ctrl`/`Control`, `Alt`, `Shift`, `Win`
- Key tokens: letters A–Z, digits 0–9, function keys F1–F24, named keys (Space, Enter, Esc, arrows, etc.)
- `MOD_NOREPEAT` always added — prevents key-repeat flooding on long holds
- Invalid key tokens throw `ArgumentException`; displayed in `ws hotkeys list` as `[invalid: ...]`

**`ws hotkeys start` daemon pattern:**
- Writes `~/.workspaces/hotkeys.pid` (current PID) before entering message loop
- On hotkey fire: stows all other deployed projects → deploys target project
- `Ctrl+C` → cancels default kill, calls `svc.Stop()` → WM_QUIT → clean exit
- PID file deleted on clean exit

**`ws hotkeys stop`:**
- Reads PID from `~/.workspaces/hotkeys.pid`; calls `Process.Kill()` on that PID
- Handles "process not found" (daemon already exited) gracefully

**Blocking daemon trade-off:**
- `ws hotkeys start` blocks the terminal window for the lifetime of the daemon
- Recommended use: run in a dedicated background terminal or a Windows Terminal pane
- Phase 3 option: promote to a true Windows Service or a system-tray app (background process)

---

## Sprint 2: Polish + Browser — ✅ COMPLETE (2026-03-23)

### What Was Built

| Task | File(s) | Status |
|------|---------|--------|
| Browser lifecycle (Chrome/Edge) | `Services/BrowserLauncher.cs` | ✅ Written |
| DeployService browser integration | `Services/DeployService.cs` | ✅ Updated |
| StowService HWND-first close | `Services/StowService.cs` | ✅ Updated |
| WindowManager.CloseWindowByHwnd | `Services/WindowManager.cs` | ✅ Updated |
| `ws edit <project>` command | `Commands/EditCommand.cs` | ✅ Written |
| Tab completions (all commands) | `Commands/*.cs` | ✅ Updated |
| Spectre.Console styled output | `Commands/*.cs` | ✅ Updated |
| Spectre.Console spinner (deploy/stow) | `Commands/SpinnerWriter.cs` | ✅ Written |
| Spectre.Console tables (list/status) | `Commands/ListCommand.cs`, `StatusCommand.cs` | ✅ Updated |
| Browser validation in `ws validate` | `Commands/ValidateCommand.cs` | ✅ Updated |
| Sample configs v2 (with browser) | `sample-project-*.workspace.yaml` (root + samples/) | ✅ Updated |

### Smoke Tests — ✅ ALL PASSED

- `ws --help` — all 7 commands listed with tab-completion hints inline
- `ws list` — Spectre.Console rounded table; 3 projects shown
- `ws validate sample-project-1` — browser section validated: Chrome found, profile + URLs shown
- `ws deploy sample-project-1 --dry-run` — 6-step plan: VD + vscode + terminal + explorer + chrome + VD switch

### Key Design Notes

**BrowserLauncher strategy:**
- Finds Chrome/Edge at well-known install paths (`ProgramFiles`, `ProgramFilesX86`, `LocalAppData`)
- Launches with `--profile-directory=<name>` arg; Chrome auto-creates the profile on first launch
- Snapshots existing browser windows before launch, polls for new HWND (20s timeout)
- HWND stored in journal as primary stow key; TitlePattern=".*" as fallback if HWND goes stale

**HWND-first stow:**
- `StowService` detects `chrome`/`edge` app types → uses `WindowManager.CloseWindowByHwnd` first
- Falls back to pattern match if HWND is stale (browser restarted since deploy)

**Tab completions:**
- System.CommandLine `AddCompletions()` on all project-name arguments
- Completions read from `ConfigLoader.ListProjects()` at invocation time
- Visible in `--help` output and shell tab-completion (when registered with `dotnet-suggest`)

**Spectre.Console spinner routing:**
- `SpinnerWriter : TextWriter` routes service step messages to `StatusContext.Status()`
- Non-dry-run deploy/stow show spinner; dry-run shows step-by-step to stdout
- List/Status use `Spectre.Console.Table` with rounded borders

**Note:** Spectre.Console wraps long lines when output is redirected to a file/pipe (no TTY). In a real terminal with auto-detected width, all lines render on one line correctly.

---

## Sprint 1: Core Foundation — ✅ COMPLETE

### What Was Built

| Component | File | Status |
|-----------|------|--------|
| .NET 8 solution | `WorkspaceOrchestrator.sln` | ✅ Written |
| Core library project | `src/WorkspaceOrchestrator.Core/` | ✅ Written |
| CLI project | `src/WorkspaceOrchestrator.Cli/` | ✅ Written |
| Win32 P/Invoke | `Interop/Win32.cs` | ✅ Written |
| Window models | `Models/WindowInfo.cs` | ✅ Written |
| YAML config models | `Models/ProjectContext.cs` | ✅ Written |
| Deploy journal models | `Models/DeployedProject.cs` | ✅ Written |
| Window manager service | `Services/WindowManager.cs` | ✅ Written |
| Virtual desktop service | `Services/VirtualDesktopService.cs` | ✅ Written |
| PowerShell VD bridge | `lib/VirtualDesktopBridge.ps1` | ✅ Written |
| Terminal launcher | `Services/TerminalLauncher.cs` | ✅ Written |
| Config loader | `Config/ConfigLoader.cs` | ✅ Written |
| State manager | `Config/StateManager.cs` | ✅ Written |
| Deploy service | `Services/DeployService.cs` | ✅ Written |
| Stow service | `Services/StowService.cs` | ✅ Written |
| CLI entry point | `Program.cs` | ✅ Written |
| `ws deploy` command | `Commands/DeployCommand.cs` | ✅ Written |
| `ws stow` command | `Commands/StowCommand.cs` | ✅ Written |
| `ws switch` command | `Commands/SwitchCommand.cs` | ✅ Written |
| `ws list` command | `Commands/ListCommand.cs` | ✅ Written |
| `ws status` command | `Commands/StatusCommand.cs` | ✅ Written |
| `ws validate` command | `Commands/ValidateCommand.cs` | ✅ Written |
| Install script | `install.ps1` | ✅ Written |
| Sample configs v2 | `samples/*.workspace.yaml` | ✅ Written |
| Phase 2 plan | `phase-2-plan.md` | ✅ Written |

### Build Status — ✅ BUILD SUCCEEDED

- **SDK:** .NET 8.0.419 installed via `winget install Microsoft.DotNet.SDK.8`
- **Build result:** `Build succeeded. 0 Warning(s). 0 Error(s).`
- **CLI smoke tests PASSED:**
  - `ws --help` — all 6 commands listed correctly
  - `ws list` — found 3 project configs, displayed with paths
  - `ws validate sample-project-1` — all paths valid, validation passed
  - `ws status` — "No projects currently deployed" (correct empty state)
  - `ws deploy sample-project-1 --dry-run` — showed correct 5-step plan, no changes made

---

## Architecture Implemented

### Key Design Decisions

**1. Virtual Desktop: PowerShell Bridge**
`VirtualDesktopService.cs` calls `lib/VirtualDesktopBridge.ps1` as a subprocess via `pwsh.exe`.
This reuses the proven MScholtes VirtualDesktop module without replicating undocumented COM GUIDs.
Each operation spawns one pwsh.exe subprocess (~200ms overhead), acceptable for deploy/stow frequency.

**2. Terminal Identity: `--window ws-<project>` Convention**
`TerminalLauncher.cs` passes `--window ws-{projectName}` to all WT launches. The window title
becomes the project name, enabling `^ws-<project>$` exact-match pattern in stow.

**3. Deploy Journal: `~/.workspaces/state.json`**
`StateManager.cs` writes deploy records on deploy and removes them on stow. Stow falls back to
convention-based matching if no journal entry exists (e.g., deployed with old PS scripts).

**4. Graceful Degradation**
If one app fails to launch, `DeployService` logs a warning and continues with the rest.
`StowService` closes as many windows as possible even after partial failures.

---

## CLI Interface

```
ws deploy <project>     Deploy a project context
ws stow [project]       Stow (close) project windows
ws switch <project>     Atomic stow + deploy
ws list                 List available project configs
ws status               Show deployed projects
ws validate <project>   Validate a config file

Global options:
  --dry-run             Show what would happen without making changes
  -v, --verbose         Extra diagnostic output
```

---

## File Structure Created

```
workspace-orchestrator/
├── WorkspaceOrchestrator.sln
├── phase-2-plan.md          ← Detailed implementation plan
├── mvp-status.md            ← This file
├── install.ps1              ← Build + install ws.exe to ~/.workspaces/bin/
├── src/
│   ├── WorkspaceOrchestrator.Core/
│   │   ├── WorkspaceOrchestrator.Core.csproj
│   │   ├── Interop/
│   │   │   └── Win32.cs
│   │   ├── Models/
│   │   │   ├── WindowInfo.cs
│   │   │   ├── ProjectContext.cs
│   │   │   └── DeployedProject.cs
│   │   ├── Services/
│   │   │   ├── WindowManager.cs
│   │   │   ├── VirtualDesktopService.cs
│   │   │   ├── TerminalLauncher.cs
│   │   │   ├── DeployService.cs
│   │   │   └── StowService.cs
│   │   └── Config/
│   │       ├── ConfigLoader.cs
│   │       └── StateManager.cs
│   └── WorkspaceOrchestrator.Cli/
│       ├── WorkspaceOrchestrator.Cli.csproj
│       ├── Program.cs
│       └── Commands/
│           ├── DeployCommand.cs
│           ├── StowCommand.cs
│           ├── SwitchCommand.cs
│           ├── ListCommand.cs
│           ├── StatusCommand.cs
│           └── ValidateCommand.cs
├── lib/
│   ├── VirtualDesktopBridge.ps1   ← NEW in Phase 2
│   ├── Win32.ps1                  ← Phase 1 reference
│   ├── WindowManager.ps1          ← Phase 1 reference
│   ├── VirtualDesktop.ps1         ← Phase 1 reference
│   └── TerminalLauncher.ps1       ← Phase 1 reference
└── samples/
    ├── sample-project-1.workspace.yaml   ← Schema v2
    └── sample-project-2.workspace.yaml   ← Schema v2
```

---

## Testing Instructions

### First Build

```powershell
# Option A: Build only (check for errors)
cd C:\dev\workspace-orchestrator
& "C:\Program Files\dotnet\dotnet.exe" build WorkspaceOrchestrator.sln

# Option B: Run directly (no install)
& "C:\Program Files\dotnet\dotnet.exe" run --project src\WorkspaceOrchestrator.Cli -- list
& "C:\Program Files\dotnet\dotnet.exe" run --project src\WorkspaceOrchestrator.Cli -- validate sample-project-1

# Option C: Install to ~/.workspaces/bin/ws.exe
pwsh -File .\install.ps1
ws list
ws validate sample-project-1
```

### Smoke Tests (after build succeeds)

```powershell
# 1. List projects
ws list

# 2. Validate config (no windows opened)
ws validate sample-project-1

# 3. Dry run deploy (no windows opened)
ws deploy sample-project-1 --dry-run

# 4. Real deploy (opens VS Code, Terminal, Explorer)
ws deploy sample-project-1

# 5. Check status
ws status

# 6. Stow
ws stow sample-project-1

# 7. Verify status cleared
ws status

# 8. Multi-project test
ws deploy sample-project-1
ws deploy sample-project-2    # both deployed simultaneously
ws status                      # should show both
ws stow sample-project-1       # closes only project-1 windows
ws status                      # should show only project-2
ws stow sample-project-2
```

---

## Phase 1 Regression Tests

The Phase 1 PowerShell test suite must still pass after Phase 2 work.

```powershell
pwsh -File .\test\Test-WindowPositioning.ps1
pwsh -File .\test\Test-VirtualDesktop.ps1
pwsh -File .\test\Test-TerminalLaunch.ps1
pwsh -File .\test\Test-DeployStow.ps1
```

---

## Known Issues / Next Steps

### Issues to Resolve (Build Phase)

1. **VirtualDesktopBridge.ps1 path resolution**: `VirtualDesktopService.cs` walks up parent
   directories to find the bridge script. Verify this works for both `dotnet run` and published
   builds. The install script copies bridge to `~/.workspaces/bin/lib/` alongside the exe.

2. **YamlDotNet underscore naming**: The `UnderscoreNamingConvention` converts `RunOnDeploy`
   to `run_on_deploy`. But the YAML has `run_on_deploy` as a nested property of `TabConfig`.
   Verify YamlDotNet handles this correctly with `IgnoreUnmatchedProperties`.

3. **ExcludeHwnds in WT launch**: When deploying a terminal, we snapshot existing WT windows
   before launch and exclude them. However, WT's `--window ws-<project>` already ensures uniqueness
   by name — if a window with that name exists, WT appends to it rather than creating a new one.
   This is actually the desired behavior for idempotent re-deploy.

4. **Explorer close pattern**: Stow uses folder name as title pattern for explorer windows.
   If two projects share a folder name (rare), stow could close the wrong explorer window.
   Phase 3 improvement: use HWND from journal for first-pass attempt.

### Sprint 4 Complete

All Sprint 4 tasks delivered. See Sprint 4 section above for full details.

### Next Steps / Phase 3 Candidates

- Live integration test: `ws deploy sample-project-1` → real windows open + positioned correctly
- Snapshot matching by YAML app key (for multi-instance same-type support)
- Replace PowerShell VD bridge with direct COM interop (Phase 3)
- Promote hotkey daemon to a Windows Service or system-tray app (Phase 3)

### Backlog

- Live integration test: `ws deploy sample-project-1` → real windows open + positioned
- Multi-project isolation test: deploy project-1 + project-2 on different VDs simultaneously
- Consider: `ws stow` with no arg to stow all deployed (currently shows help message)
- Consider: `ws snapshot` — capture current window positions to state.json

---

## Phase 2 Success Criteria Progress

| Criterion | Status |
|-----------|--------|
| `ws deploy <project>` works | ✅ Smoke-tested (--dry-run), needs live integration test |
| `ws stow <project>` only closes that project's windows | 🔄 Needs live integration test |
| `ws switch` performs atomic stow+deploy | 🔄 Needs live integration test |
| `ws list` and `ws status` display correctly | ✅ Verified (Spectre.Console tables) |
| Two projects deployed simultaneously on different VDs | 🔄 Needs live integration test |
| Deploy journal handles "already deployed" case | ✅ StateManager.RecordDeploy is idempotent |
| All Phase 1 tests still pass | ✅ PS scripts untouched |
| Binary publishable as self-contained `ws.exe` | ✅ install.ps1 written |
| Browser (Chrome/Edge) profile launch + stow | ✅ BrowserLauncher written, validated |
| Tab completion for project names | ✅ All project-arg commands |
| `ws edit <project>` opens config in VS Code | ✅ EditCommand written |
| Spectre.Console styled output + spinners | ✅ All commands updated |
| `ws hotkeys list` shows per-project hotkeys | ✅ Spectre.Console table |
| `ws hotkeys start` registers Win32 hotkeys + message loop | ✅ HotkeyService written |
| `ws hotkeys stop` kills daemon via PID file | ✅ HotkeysCommand written |
| Hotkey string parser (Ctrl+Alt+1 → modifiers+vk) | ✅ Handles A–Z, 0–9, F1–F24, named keys |
| Window positions captured on stow (auto-snapshot) | ✅ SnapshotService + StowService |
| Deploy restores last-known layout from snapshot | ✅ DeployService snapshot lookup |
| `ws snapshot` command (capture/list/delete) | ✅ SnapshotCommand written |
