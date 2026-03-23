# Project Assessment: Current State vs. Plan

**Date:** 2026-03-22T21:30
**Assessed by:** Claude (from poc-status.md, workspace-orchestrator-plan.md, git history)

---

## Phase 1 — Proof of Concept: COMPLETE

All 4 planned capabilities were delivered and are passing automated tests:

| Capability | Plan | Status |
|---|---|---|
| P1.1 — Window Discovery & Positioning | EnumWindows, SetWindowPos, find handles by PID/title | Done (2/2 tests) |
| P1.2 — Virtual Desktop Management | Create, switch, move windows between desktops | Done (4/4 tests) |
| P1.3 — Windows Terminal Multi-Tab Launch | Named tabs, per-tab directories, command injection | Done (2/2 + visual) |
| P1.4 — Deploy/Stow Cycle | YAML config, launch all apps, position, close gracefully | Done (6/6 tests) |

The file structure matches the plan closely. All 4 library modules (`Win32.ps1`, `WindowManager.ps1`, `VirtualDesktop.ps1`, `TerminalLauncher.ps1`) plus `deploy.ps1`/`stow.ps1` entry points are implemented. The git history shows clean feature branches merged with `--no-ff` as planned.

---

## Technical Decisions Resolved in Phase 1

| Decision | Plan asked | Outcome |
|---|---|---|
| Virtual desktop library | MScholtes vs raw COM | MScholtes VirtualDesktop 1.5.11 |
| YAML parser | powershell-yaml vs JSON | powershell-yaml 0.4.12 |
| Terminal command injection | wt CLI args vs SendKeys | wt CLI with `-- pwsh -NoExit -Command` |
| Window discovery strategy | PID vs title vs class | Hybrid: PID + title matching |

---

## Gate 1 Status: NOT YET COMPLETED

The plan calls for a `gate1-decisions.md` document answering 5 questions before starting Phase 2. This file does not yet exist. The questions are:

1. **Timing reliability** — How consistent is window positioning?
2. **Virtual desktop stability** — Reliable? Survived Windows updates?
3. **Terminal experience** — Is the WT CLI approach sufficient?
4. **Language decision** — Stay PowerShell or migrate to C#?
5. **Scope refinement** — What matters most for Phase 2?

---

## Known Issues Carried Forward

The biggest issue discovered is the **WT window tracking problem** (documented in `design-note-WT-tracking.md`). All WT windows share the `WindowsTerminal` process name, so stow can't reliably target a specific project's terminal when multiple projects are deployed. This directly impacts **P2.1 (Multi-Project Context Switching)**, which is the cornerstone of Phase 2.

Full list of known limitations from poc-status.md:

| Issue | Impact |
|---|---|
| WT stow uses `Stop-Process` fallback | Low |
| Explorer positioning uses title match | Low |
| VS Code window detection matches any VS Code window | Low |
| `Find-WindowByProcess` can't distinguish new vs existing windows with same title | Medium |
| Virtual desktop APIs are undocumented COM interfaces | Medium |
| No state persistence (stow always uses config positions) | Medium |
| WT stow cannot target a specific project's terminal | **High** |

---

## Phase 2 Gap Analysis

None of Phase 2 has been started. Here's what's on deck:

| P2 Capability | Dependency on Gate 1 | Notes |
|---|---|---|
| P2.1 — Multi-Project Switching | High — needs WT tracking fix | Core feature; blocked by WT identity problem |
| P2.2 — Browser Profile Integration | Low | New capability |
| P2.3 — Window State Snapshots | Low | New capability |
| P2.4 — CLI Interface (`ws` command) | Medium — depends on language decision | PowerShell module vs C# console app |
| P2.5 — Global Hotkeys | Medium — depends on language decision | RegisterHotKey needs C# or AHK |
| P2.6 — Robust Error Handling | Low | Enhancement to existing code |
| P2.7 — Office & OneNote | Low | New capability |

---

## Bottom Line

The project is at the **Gate 1 checkpoint**. Phase 1 is fully delivered, but the structured pause hasn't happened yet. The critical blocker is the **language decision** (PowerShell vs C#) — it determines the architecture for everything in Phase 2. The **WT tracking problem** also needs a design decision before multi-project support can work.

**Recommended next step:** Complete Gate 1 by writing `gate1-decisions.md`.
