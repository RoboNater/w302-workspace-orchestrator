# Proof-of-Concept Status

**Date completed:** 2026-03-16
**Phase 1 closed:** 2026-03-18
**Gate 1 completed:** 2026-03-22
**Terminal Investigation completed:** 2026-03-23
**Branch:** main
**Phase:** 1 — Proof of Concept (COMPLETE) | Investigation (COMPLETE)

---

## Summary

All four Phase 1 capabilities are implemented and passing automated tests.

| Capability | Status | Test | Result |
|-----------|--------|------|--------|
| P1.1 — Window discovery & positioning | ✅ Complete | `Test-WindowPositioning.ps1` | 2/2 pass |
| P1.2 — Virtual desktop management | ✅ Complete | `Test-VirtualDesktop.ps1` | 4/4 pass |
| P1.3 — Windows Terminal multi-tab launch | ✅ Complete | `Test-TerminalLaunch.ps1` | 2/2 auto + visual |
| P1.4 — Deploy/stow cycle | ✅ Complete | `Test-DeployStow.ps1` | 6/6 pass |
| Multi-project isolation | ⚠️ Known limitation | `Test-MultiProjectInteraction.ps1` | WT tracking issue discovered (see below) |

---

## Running the Tests

All tests must be run from a **Windows-native PowerShell 7** session (not WSL2), with a live desktop session visible:

```powershell
cd C:\dev\workspace-orchestrator

# P1.1 — Window positioning (fully automated)
pwsh -File .\test\Test-WindowPositioning.ps1

# P1.2 — Virtual desktops (automated; briefly switches your active desktop)
pwsh -File .\test\Test-VirtualDesktop.ps1

# P1.3 — Terminal launch (automated + visual checks)
pwsh -File .\test\Test-TerminalLaunch.ps1

# P1.4 — Full deploy/stow round-trip (launches real apps — see notes below)
pwsh -File .\test\Test-DeployStow.ps1

# Or run deploy/stow manually:
.\deploy.ps1 -Project sample-project-1
.\stow.ps1   -Project sample-project-1
```

---

## Interactive / Visual Verification Required

The following items were confirmed working by automated scripts but have a visual
component that requires you to check with your own eyes:

### P1.2 — Virtual Desktops

The test switches to a new virtual desktop and switches back. Verify:
- [ ] You see your desktop briefly switch to a blank/new desktop
- [ ] It returns to your original desktop automatically
- [ ] The Notepad window (opened by the test) disappears from your desktop while on the other one

### P1.3 — Windows Terminal Tabs

When `Test-TerminalLaunch.ps1` runs, a Windows Terminal window opens. Verify:
- [ ] Two tabs are visible: **WT-Test-Alpha** and **WT-Test-Beta**
- [ ] Tab-Alpha shows: `Hello from Tab-Alpha` in **green**
- [ ] Tab-Beta shows: `Hello from Tab-Beta` in **cyan**
- [ ] Both tabs end in an active `pwsh` prompt (not crashed)
- [ ] The window is positioned at approximately (50, 50), sized 1200×700

### P1.4 — Deploy/Stow Cycle

When `Test-DeployStow.ps1` (or `deploy.ps1 -Project sample-project-1`) runs:
- [ ] All windows open on virtual desktop 2 (not desktop 1)
- [ ] The active desktop switches to desktop 2 after deploy completes
- [ ] VS Code opens at position (0, 0), sized 960×1040
- [ ] Windows Terminal opens at position (960, 0), sized 960×520 with two tabs: **Project** and **Scratch**
- [ ] File Explorer opens at position (960, 520), sized 960×520 showing `C:\dev\workspace-orchestrator\sample-projects\sample-project-1`
- [ ] After stow, all three windows are closed

---

## Sample Project Configs

Two sample projects are included under `sample-projects/`:

- `sample-project-1.workspace.yaml` — Data processing utility (`sample-projects/sample-project-1`)
- `sample-project-2.workspace.yaml` — Web server project (`sample-projects/sample-project-2`)

Edit either YAML to point to your real project paths, then run:
```powershell
.\deploy.ps1 -Project sample-project-1
.\stow.ps1   -Project sample-project-1
```

---

## Known Limitations (Phase 1)

| Issue | Impact | Notes |
|-------|--------|-------|
| WT stow uses `Stop-Process` fallback | Low | WM_CLOSE triggers WT's "close all tabs?" dialog; we force-kill if it doesn't close within 800ms |
| Explorer positioning uses title match | Low | The folder window title must contain the folder name (it does on Windows 11) |
| VS Code window detection uses title "Visual Studio Code" | Low | Matches any VS Code window; if multiple VS Code instances are open, the first found is positioned |
| `Find-WindowByProcess` cannot distinguish a newly launched window from an existing one with the same title | Medium | If you already have VS Code or Explorer open for the same project, deploy re-positions the existing window rather than opening a new one — this is usually the desired behavior |
| Virtual desktop APIs are undocumented COM interfaces | Medium | The MScholtes VirtualDesktop module (v1.5.11) may break after a major Windows update — check for module updates if virtual desktop operations stop working |
| No state persistence | Medium | Stow always uses config file positions; no snapshot of where you moved windows during work |
| WT stow cannot target a specific project's terminal | High | All WT windows share process name `WindowsTerminal`; stow closes the "last" one found, which may be the wrong window. See `design-note-WT-tracking.md` for analysis and Phase 2 options |
| No run_on_deploy command execution in WT (Windows Terminal limitation) | Low | The `-- pwsh -NoExit -Command "..."` injection works; commands run and shell stays open |

---

## Bugs Fixed During This Session

These bugs were found and fixed before the PoC was working:

1. **`$pid` reserved variable** — `Win32.ps1` callback used `$pid` as a local variable name; renamed to `$procId`
2. **`Add-Type` double-load** — `Win32.ps1` was dot-sourced twice (directly and via `WindowManager.ps1`); added type-existence guard
3. **`Add-Type` in function body** — `Win32Msg` type was re-defined on every call to `Close-WindowGracefully`; moved to module level with guard
4. **`$matches` automatic variable conflict** — `Find-WindowByProcess` used `$matches` as a local variable, which was overwritten by the `-match` operator inside `Where-Object`; renamed to `$found`
5. **Explorer forward-slash paths** — `explorer.exe` silently opens "Documents" when given forward-slash paths like `C:/dev/foo`; deploy now converts YAML paths to backslashes before passing to explorer
6. **Virtual desktop windows not moved** — `deploy.ps1` ensured desktops existed but never called `Move-WindowToDesktop` or `Switch-ToDesktop`; deploy now moves each window to the target desktop and switches to it

---

## Design Notes

- [`design-note-WT-tracking.md`](design-note-WT-tracking.md) — Windows Terminal window identity problem.
  Discovered during multi-project testing: stow cannot reliably distinguish which WT
  window belongs to which project. Documents four candidate approaches (HWND state file,
  `--title` convention, `--window` named instances, intent-based deploy journal) with
  pros/cons. May warrant a standalone spike project in Phase 2.

---

## Investigation: Terminal Emulator Selection (2026-03-23) — ✅ COMPLETE

The Phase 1 PoC identified a critical issue: **Windows Terminal window identity tracking for multi-project scenarios**. See `design-note-WT-tracking.md` for analysis.

A comprehensive investigation evaluated three terminal emulators:

| Terminal | Result | Key Finding |
|----------|--------|------------|
| Windows Terminal | ✅ **Recommended** | `--window <name>` feature sets window title to name, making it reliably queryable via Win32 APIs |
| WezTerm | ❌ Rejected | Windows not enumerable by Win32; cannot integrate with window manager for positioning/closing |
| Alacritty | ❌ Rejected | DLL compatibility issue on this system; no native tabs (would require tmux) |

**Solution:** Use `--window ws-<projectname>` convention in all WT launches. Window name becomes the title, enabling reliable identification for multi-project stow operations.

**Impact:** Phase 2's P2.1 (Multi-Project Context Switching) is now **UNBLOCKED**.

See `terminal-investigation.md`, `phase-2-readiness.md`, and `INVESTIGATION_SUMMARY.md` for full details.

---

## Next Steps: Gate 1 → Phase 2

**Gate 1 decisions** (2026-03-22) — ✅ LOCKED:
- [x] Timing reliability — excellent
- [x] Virtual desktop stability — confirmed stable
- [x] Terminal experience — CLI approach sufficient; WT tracking problem solved
- [x] Language decision — **Migrate to C#**
- [x] Scope refinement — Multi-project switching is the MVP priority

See `gate1-decisions.md` for detailed answers.

**Phase 2 is ready to start.** All blockers resolved. See `phase-2-readiness.md` for implementation checklist.

---

## Repository Structure

```
workspace-orchestrator/
├── lib/
│   ├── Win32.ps1              # P/Invoke: EnumWindows, SetWindowPos, GetWindowRect
│   ├── WindowManager.ps1      # Start-AndPosition, Find-WindowByProcess, Close-WindowGracefully
│   ├── VirtualDesktop.ps1     # Ensure-DesktopCount, Move-WindowToDesktop, Switch-ToDesktop
│   └── TerminalLauncher.ps1   # Start-TerminalWithTabs (multi-tab + command injection)
├── test/
│   ├── Test-WindowPositioning.ps1   # P1.1 — 2/2 pass
│   ├── Test-VirtualDesktop.ps1      # P1.2 — 4/4 pass
│   ├── Test-TerminalLaunch.ps1      # P1.3 — 2/2 auto + visual
│   ├── Test-DeployStow.ps1          # P1.4 — 6/6 pass (uses sample-project-1)
│   └── Test-MultiProjectInteraction.ps1  # Multi-project isolation test
├── sample-projects/
│   ├── sample-project-1/      # Data processing utility (used by tests)
│   └── sample-project-2/      # Web server project
├── deploy.ps1                 # Deploy a project context from YAML config
├── stow.ps1                   # Stow (close) a project context
├── sample-project-1.workspace.yaml
├── sample-project-2.workspace.yaml
├── workspace-orchestrator.workspace.yaml
├── design-note-WT-tracking.md # WT window identity problem analysis
├── poc-status.md              # This file
├── workspace-orchestrator-plan.md
└── workspace-orchestrator-spec.md
```
