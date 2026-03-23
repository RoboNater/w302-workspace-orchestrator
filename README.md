# Workspace Orchestrator

One-click deploy and stow of entire project contexts on Windows 11 — VS Code, terminals, browsers, file explorers, Office docs — each positioned on the right virtual desktop with the right layout.

## Status: Phase 1 Complete ✓ | Gate 1 Complete ✓ | Terminal Investigation Complete ✓ | Phase 2 Ready

**Phase 1 (PoC)** — All capabilities validated:
- [x] P1.1 — Window discovery & positioning (Win32 API)
- [x] P1.2 — Virtual desktop management (COM interop)
- [x] P1.3 — Windows Terminal multi-tab launch
- [x] P1.4 — Basic deploy/stow cycle (6/6 tests passing)

**Gate 1** (2026-03-22) — Design decisions locked:
- [x] Timing reliability — excellent, no flakiness observed
- [x] Virtual desktop stability — confirmed stable
- [x] Terminal CLI approach — validated; stow challenges identified
- [x] Language decision — **migrate to C#** for Phase 2+
- [x] Scope refinement — multi-project switching is the MVP priority

**Terminal Emulator Investigation** (2026-03-23) — ✅ COMPLETE
- [x] Evaluated Windows Terminal, WezTerm, Alacritty for window identity and stow reliability
- [x] **Key Finding:** Windows Terminal's `--window <name>` feature solves the P2.1 blocker
- [x] **Recommendation:** Stay with WT, use `--window ws-<projectname>` naming convention
- [x] Phase 2 blocked feature P2.1 (Multi-Project Context Switching) is now **UNBLOCKED**
- See `terminal-investigation.md`, `phase-2-readiness.md`, and `INVESTIGATION_SUMMARY.md`

## Quick Start (Phase 1 PoC - PowerShell)

The Phase 1 PoC is complete and working. The C# implementation for Phase 2 is in planning.

```powershell
# Test window positioning
pwsh -File .\test\Test-WindowPositioning.ps1

# Test virtual desktops
pwsh -File .\test\Test-VirtualDesktop.ps1

# Test terminal launch
pwsh -File .\test\Test-TerminalLaunch.ps1

# Full deploy/stow cycle
pwsh -File .\test\Test-DeployStow.ps1

# Try a manual deploy
.\deploy.ps1 -Project sample-project-1
.\stow.ps1 -Project sample-project-1
```

See `poc-status.md` for detailed test results and visual verification checklist.

## Documentation

| Document | Purpose |
|---|---|
| [`poc-status.md`](poc-status.md) | Phase 1 summary, test results, known limitations |
| [`workspace-orchestrator-spec.md`](workspace-orchestrator-spec.md) | Feature specification (updated post-PoC) |
| [`workspace-orchestrator-plan.md`](workspace-orchestrator-plan.md) | Development phases and roadmap |
| [`gate1-decisions.md`](gate1-decisions.md) | Gate 1 decisions (language, scope, priorities) |
| [`design-note-WT-tracking.md`](design-note-WT-tracking.md) | Analysis of Windows Terminal window identity problem |
| **Investigation Documents** | |
| [`terminal-investigation-plan.md`](terminal-investigation-plan.md) | Test methodology for terminal emulator evaluation |
| [`terminal-investigation.md`](terminal-investigation.md) | Complete test results, scoring, and recommendation |
| [`phase-2-readiness.md`](phase-2-readiness.md) | Phase 2 implementation checklist and readiness gate |
| [`INVESTIGATION_SUMMARY.md`](INVESTIGATION_SUMMARY.md) | Executive summary of terminal investigation |
| **Reference** | |
| [`lessons-learned-from-poc.md`](lessons-learned-from-poc.md) | PoC findings and architectural implications |
| [`state-of-the-project.20260322t2130.md`](state-of-the-project.20260322t2130.md) | Current state vs. plan assessment |

## Architecture (Phase 1)

```
lib/
  ├── Win32.ps1              # P/Invoke: EnumWindows, SetWindowPos, GetWindowRect
  ├── WindowManager.ps1      # Start-AndPosition, Find-WindowByProcess, Close-WindowGracefully
  ├── VirtualDesktop.ps1     # Ensure-DesktopCount, Move-WindowToDesktop, Switch-ToDesktop
  └── TerminalLauncher.ps1   # Start-TerminalWithTabs (multi-tab + command injection)

deploy.ps1, stow.ps1         # Main entry points; read YAML via powershell-yaml module

test/
  ├── Test-WindowPositioning.ps1  (P1.1 — 2/2 pass)
  ├── Test-VirtualDesktop.ps1     (P1.2 — 4/4 pass)
  ├── Test-TerminalLaunch.ps1     (P1.3 — 2/2 pass)
  ├── Test-DeployStow.ps1         (P1.4 — 6/6 pass)
  └── Test-MultiProjectInteraction.ps1

sample-projects/            # Two test projects (sample-project-1, sample-project-2)
```

## Known Limitations (Phase 1)

| Issue | Impact | Status | Notes |
|---|---|---|---|
| Windows Terminal stow unreliable | **Was High** | ✅ **RESOLVED** | Phase 2 will use `--window ws-<projectname>` convention; window name is set as title and is reliably queryable. |
| VS Code window detection matches any VS Code | Low | Ongoing | If multiple VS Code instances open, first found is positioned |
| Explorer positioning uses title match | Low | Ongoing | Requires folder name to match; works on Windows 11 |
| No state persistence | Medium | P2.3 Feature | Stow always uses config positions; snapshots will fix this in Phase 2 |
| Virtual desktop APIs undocumented | Medium | Ongoing | MScholtes VirtualDesktop module (v1.5.11) may break on major Windows update |

See `poc-status.md` for full limitations list.

## What's Next

**Phase 2: Minimum Viable Product (C#)** — Ready to start!

With terminal investigation complete and Windows Terminal's `--window <name>` feature validated, all Phase 2 blockers are resolved. Priorities (from Gate 1):

1. **P2.1 — Multi-Project Context Switching** (Previously blocked, NOW UNBLOCKED)
   - Use `--window ws-<projectname>` naming convention
   - Implement deploy journal for resilience

2. **P2.4 — CLI Interface** (`ws` command with `deploy`, `stow`, `switch`, `list`, `status`)

3. **P2.5 — Global Hotkeys** (Ctrl+Alt+1/2/3 for quick project switching)

4. **P2.6 — Robust Error Handling** (Deploy journal, retry logic, graceful degradation)

5. **P2.2 — Browser Profile Integration** (Chrome/Edge profile management)

See `workspace-orchestrator-plan.md` for full roadmap and `phase-2-readiness.md` for implementation checklist.

## Prerequisites (Phase 1 PoC)

- Windows 11 with a live desktop session
- PowerShell 7+ (`C:\Program Files\PowerShell\7\pwsh.exe`)
- Windows Terminal (latest) — `wt` command available
- VS Code — `code` command available
- PowerShell modules:
  - `powershell-yaml` (0.4.12+)
  - `VirtualDesktop` (1.5.11)
  - `Pester` (3.4.0+) for running tests

Install modules:
```powershell
Install-Module powershell-yaml -Scope CurrentUser
Install-Module VirtualDesktop -Scope CurrentUser
Install-Module Pester -Scope CurrentUser
```

## Contributing

The Phase 1 PoC is in reference/archive mode. Active development is planned in Phase 2 with a C# rewrite.

For now, the PowerShell code is worth studying as a reference for:
- Win32 P/Invoke patterns in PowerShell
- Window discovery and positioning via COM/P/Invoke
- Virtual desktop management (MScholtes module)
- Windows Terminal integration

See `HUMAN.md` for developer notes and gotchas from the PoC.
