# Terminal Emulator Investigation — Summary

**Completed:** 2026-03-23
**Status:** ✅ COMPLETE — Ready for Phase 2 implementation

---

## What Was Investigated

As part of Gate 1 decision-making for Phase 2, the project was blocked on a critical problem: **Windows Terminal window identity tracking**. When multiple projects are deployed simultaneously, `stow` cannot reliably identify which WT window belongs to which project (all WT instances share the process name `WindowsTerminal`).

This investigation evaluated three terminal emulator alternatives to determine if any offered better programmatic control and window identity than WT, or if WT itself had a solution.

---

## Key Findings

### 1. Windows Terminal — ✅ SOLUTION FOUND

**The `--window <name>` feature (WT 1.12+, confirmed working in v1.23) solves the problem:**

```bash
wt new-tab --window ws-project-1 --title "Project" -d C:\dev
```

- **Window title is set to the window name:** `'ws-project-1'`
- **Title is queryable via Win32 APIs:** `EnumWindows()` → `GetWindowText()` → match title pattern
- **Multiple commands target the same window:** `wt --window ws-project-1 new-tab --title "Tab2"` appends to existing window
- **Window responds to positioning:** `SetWindowPos` works as expected
- **Window is closeable:** `WM_CLOSE` + fallback to `Stop-Process`

**Result:** Problem is SOLVED. No need to switch terminals.

### 2. WezTerm — ❌ REJECTED

**Critical blocker discovered through testing:**
- WezTerm windows are **NOT enumerable via Win32 `EnumWindows()`**
- This means:
  - Cannot use `SetWindowPos` for window positioning
  - Cannot use `WM_CLOSE` for graceful window closing
  - Entire window orchestration would require WezTerm-specific CLI (`wezterm cli`)
  - Would create architectural coupling to WezTerm's IPC layer

**Alternative features (workspace naming, rich CLI) are excellent in theory, but useless if the window isn't discoverable from Win32.**

### 3. Alacritty — ❌ REJECTED

**Installation issue:** Exit code 0xC0000135 (STATUS_DLL_NOT_FOUND)
- Suggests missing Visual C++ Redistributable or incompatible build
- Even if fixed: no native tabs (would require tmux/zellij integration, adds complexity)
- Less mature/actively maintained than WT on Windows

---

## Deliverables Created

### 1. `terminal-investigation-plan.md`
- Detailed test plan for all three terminals
- 7 evaluation criteria with rationale
- Execution notes and test cases

### 2. `terminal-investigation.md`
- Complete test results and findings
- Scored summary table comparing all terminals
- Clear recommendation with justification
- Implementation plan for Phase 2
- Risk mitigation strategies

### 3. `phase-2-readiness.md`
- Blocker resolution status (✅ ALL UNBLOCKED)
- Implementation requirements for Phase 2
- Architecture decision record
- Testing strategy for multi-project scenarios
- Risk assessment (LOW risk — WT is stable and proven)

---

## Immediate Impact: Phase 2 Unblocked

**P2.1 (Multi-Project Context Switching)** was blocked on "how do we identify which WT window belongs to which project?"

**This is now solved:** Use `--window ws-<projectname>` convention.

**Changes needed:**
1. Update TerminalLauncher to pass `--window ws-<project>` to all `wt` commands
2. Update stow to find window by title pattern `ws-<projectname>`
3. Implement deploy journal for resilience (already in architecture)
4. Test multi-project deploy/stow cycle

**Effort estimate:** ~2–3 days for core implementation, minimal complexity.

---

## Why Not Switch Terminals?

| Reason | Details |
|--------|---------|
| **WT's native solution works** | `--window <name>` directly addresses the problem; minimal code changes needed |
| **WezTerm windows invisible to Win32** | Cannot position windows or close them gracefully without rewriting the entire window manager |
| **Alacritty broken on this system** | DLL issue + no native tabs (tmux required) = more friction, not less |
| **WT is first-party & stable** | Microsoft-supported, widely used, best-in-class UX on Windows |
| **Low risk of change** | WT's `--window` feature is stable; fallbacks exist if ever needed |

---

## Next Steps

1. **Phase 2 Sprint 1:** Port Phase 1 PoC to C#, implement `--window ws-<project>` convention, test multi-project scenarios
2. **Phase 2 Sprint 2:** CLI interface + global hotkeys
3. **Phase 2 Sprint 3:** Browser profile integration
4. **Phase 3:** GUI, advanced features, polish

**No regressions:** All Phase 1 capabilities remain intact and working.

---

## Files

- `terminal-investigation-plan.md` — Test plan
- `terminal-investigation.md` — Complete results & recommendation
- `phase-2-readiness.md` — Implementation checklist & readiness gate
- This file — Summary & next steps
