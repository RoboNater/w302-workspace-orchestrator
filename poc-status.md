# Proof-of-Concept Status

**Date completed:** 2026-03-16
**Branch:** master
**Phase:** 1 — Proof of Concept

---

## Summary

All four Phase 1 capabilities are implemented and passing automated tests.

| Capability | Status | Test | Result |
|-----------|--------|------|--------|
| P1.1 — Window discovery & positioning | ✅ Complete | `Test-WindowPositioning.ps1` | 2/2 pass |
| P1.2 — Virtual desktop management | ✅ Complete | `Test-VirtualDesktop.ps1` | 4/4 pass |
| P1.3 — Windows Terminal multi-tab launch | ✅ Complete | `Test-TerminalLaunch.ps1` | 2/2 auto + visual |
| P1.4 — Deploy/stow cycle | ✅ Complete | `Test-DeployStow.ps1` | 6/6 pass |

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
.\deploy.ps1 -Project sample-project
.\stow.ps1   -Project sample-project
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

When `Test-DeployStow.ps1` (or `deploy.ps1 -Project sample-project`) runs:
- [ ] VS Code opens at position (0, 0), sized 960×1040
- [ ] Windows Terminal opens at position (960, 0), sized 960×520 with two tabs: **Project** and **Scratch**
- [ ] File Explorer opens at position (960, 520), sized 960×520 showing `C:\dev\workspace-orchestrator`
- [ ] After stow, all three windows are closed

---

## Customizing the Sample Project Config

Edit `sample-project.workspace.yaml` to point to your real project paths:

```yaml
applications:
  vscode:
    workspace: "C:/your/real/project"       # <- your VS Code workspace or folder

  terminals:
    tabs:
      - title: "Project"
        directory: "C:/your/real/project"   # <- where your main terminal starts
      - title: "Scratch"
        directory: "C:/Users/YourName"      # <- second tab directory

  explorer:
    paths:
      - "C:/your/real/project"              # <- folder to open in Explorer
```

Then run:
```powershell
.\deploy.ps1 -Project sample-project
.\stow.ps1   -Project sample-project
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
| No run_on_deploy command execution in WT (Windows Terminal limitation) | Low | The `-- pwsh -NoExit -Command "..."` injection works; commands run and shell stays open |

---

## Bugs Fixed During This Session

These bugs were found and fixed before the PoC was working:

1. **`$pid` reserved variable** — `Win32.ps1` callback used `$pid` as a local variable name; renamed to `$procId`
2. **`Add-Type` double-load** — `Win32.ps1` was dot-sourced twice (directly and via `WindowManager.ps1`); added type-existence guard
3. **`Add-Type` in function body** — `Win32Msg` type was re-defined on every call to `Close-WindowGracefully`; moved to module level with guard
4. **`$matches` automatic variable conflict** — `Find-WindowByProcess` used `$matches` as a local variable, which was overwritten by the `-match` operator inside `Where-Object`; renamed to `$found`

---

## Next Steps: Gate 1 → Phase 2

Before starting Phase 2, answer the questions from **Feedback Gate 1** in `workspace-orchestrator-plan.md`:

1. **Timing reliability** — How consistent is window positioning across deploys? Any flakiness?
2. **Virtual desktop stability** — Did the desktop switch feel reliable? Did it survive a Windows update?
3. **Terminal experience** — Are the tabs and commands working correctly? Is `-- pwsh -NoExit -Command` sufficient for your workflow?
4. **Language decision** — Stay in PowerShell or migrate to C# for Phase 2?
5. **Scope refinement** — Based on using this PoC, what matters most for Phase 2?

Document your findings in a short `gate1-decisions.md` file before starting Phase 2 work.

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
│   └── Test-DeployStow.ps1          # P1.4 — 6/6 pass
├── deploy.ps1                 # Deploy a project context from YAML config
├── stow.ps1                   # Stow (close) a project context
├── sample-project.workspace.yaml  # Edit this with your real paths
├── poc-status.md              # This file
├── workspace-orchestrator-plan.md
└── workspace-orchestrator-spec.md
```
