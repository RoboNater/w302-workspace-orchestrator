# Phase 2 Readiness Checklist

**Date:** 2026-03-23
**Related:** `terminal-investigation.md`, `workspace-orchestrator-plan.md`, `gate1-decisions.md`

---

## Investigation Status: ✅ COMPLETE

The terminal emulator investigation (`terminal-investigation.md`) has been completed with hands-on testing of:

- **Windows Terminal** — ✅ Recommended
- **WezTerm** — ❌ Rejected (windows not enumerable by Win32 APIs)
- **Alacritty** — ⚠️ Rejected (DLL compatibility issue + tmux complexity)

**Key Finding:** Windows Terminal's `--window <name>` feature perfectly solves the window identity tracking problem that was blocking Phase 2.1 (Multi-Project Context Switching).

---

## Blocker Resolution

| Blocker | Source | Status | Solution |
|---------|--------|--------|----------|
| WT window identity (P2.1 blocker) | `design-note-WT-tracking.md` | ✅ **RESOLVED** | Use `--window ws-<projectname>` convention |
| Language decision | `gate1-decisions.md` | ✅ **DECIDED** | Migrate to C# for Phase 2 |
| Scope refinement | `gate1-decisions.md` | ✅ **DONE** | Prioritized P2.1, P2.2, P2.4, P2.5, P2.6 |

---

## Phase 2 Implementation Readiness

### What Changes Are Needed

**1. Update Terminal Launch Convention**
- All WT launches must include `--window ws-<projectname>` flag
- This sets the window title to the project name, making it uniquely identifiable
- Format: `wt new-tab --window ws-myproject --title "TabName" -d C:\path`

**2. Update C# `TerminalLauncher` Service**
- Implement a `WindowsTerminalService` class that:
  - Accepts project name as parameter
  - Builds WT command strings with `--window ws-<projectname>` flag
  - Wraps the existing phase 1 PowerShell TerminalLauncher logic
- Key method: `LaunchTerminalWithTabs(string projectName, List<TabConfig> tabs)`

**3. Update Deploy Service (C#)**
- When deploying a project, pass the project name to `TerminalLauncher`
- Ensures WT window is created with project-scoped identity

**4. Update Stow Service (C#)**
- Implement `Find-WindowByTitlePattern("ws-<projectname>")` (port from PowerShell)
- Use existing window closing logic: `WM_CLOSE` + `Stop-Process` fallback
- Gracefully close the WT window after all other project windows

**5. Implement Deploy Journal (P2.6 Resilience)**
- Record intent per project at deploy time:
  ```json
  {
    "project": "sample-project-1",
    "deployed_at": "2026-03-23T23:30:00Z",
    "apps": [
      { "type": "terminal", "match": { "title_pattern": "ws-sample-project-1" } },
      { "type": "vscode", "match": { "title_contains": "workspace-orchestrator" } },
      { "type": "explorer", "match": { "title_contains": "sample-project-1" } }
    ]
  }
  ```
- Use journal at stow time to verify cleanup
- Enables recovery from partial deploys

### What Stays the Same

- Win32 window positioning logic (works great with WT)
- Virtual desktop management (no changes needed)
- File Explorer and VS Code handling (no changes needed)
- Multi-tab launch syntax (just add `--window <name>` prefix)

---

## No Regressions

✅ All Phase 1 capabilities remain intact:
- P1.1 — Window positioning (SetWindowPos still works on WT windows)
- P1.2 — Virtual desktop management (orthogonal)
- P1.3 — Multi-tab launch (enhanced with window naming)
- P1.4 — Deploy/stow cycle (improved with terminal identity)

---

## Phase 2 Capabilities: Now Unblocked

| Capability | Blocker | Status | Note |
|-----------|---------|--------|------|
| P2.1 — Multi-Project Context Switching | WT window identity | ✅ **UNBLOCKED** | Ready to implement |
| P2.2 — Browser Profile Integration | None | ✅ Ready | Independent feature |
| P2.3 — Window State Snapshots | None | ✅ Ready | Lower priority (may defer) |
| P2.4 — CLI Interface | None | ✅ Ready | Independent feature |
| P2.5 — Global Hotkeys | None | ✅ Ready | Independent feature |
| P2.6 — Robust Error Handling | Deploy journal needed | ✅ **Unblocked** | Required for P2.1 reliability |

---

## Next Steps for Phase 2

### Sprint 1: Core Unblock (P2.1 + P2.6)
1. Port C# `TerminalLauncher` with `--window ws-<project>` support
2. Implement deploy journal for resilience
3. Test multi-project deploy/stow cycle (previously fragile)
4. Verify WT window identity tracking works reliably

### Sprint 2: CLI + Global Hotkeys (P2.4 + P2.5)
1. Implement CLI commands: `ws deploy`, `ws stow`, `ws switch`, `ws list`, `ws status`
2. Register global hotkey listener service
3. Add tab completion for project names

### Sprint 3: Browser Integration (P2.2)
1. Chrome/Edge profile lifecycle management
2. Profile detection and restoration

### Later (P2.3 — Optional for MVP)
- Window state snapshots (might defer to Phase 3 if time is tight)

---

## Testing Strategy for Phase 2

1. **Unit tests** for terminal launcher, window finder, deploy journal
2. **Integration test:** Full deploy/stow cycle with 2 simultaneous projects
   - Project A: WT with window title `ws-proj-a`
   - Project B: WT with window title `ws-proj-b`
   - Verify stow closes only Project A, not Project B
3. **Regression test:** Phase 1 test suite still passes
4. **Edge cases:**
   - Stow when project window already closed (journal detects, silent success)
   - Deploy when project already deployed (re-use window vs. create new)
   - User manually closes WT window (detect and handle gracefully)

---

## Risk Assessment: LOW

**Terminal selection risk:** ✅ Mitigated
- WT is stable, widely used, well-supported
- `--window <name>` feature is stable and supported in current version (1.23+)
- Fallback: if ever broken, `--window` can be removed and we fall back to title-based matching

**Window identity risk:** ✅ Mitigated
- Tested and confirmed working
- Relies on stable Win32 APIs that have been unchanged for 20 years

**C# migration risk:** ⚠️ Medium (not related to terminal investigation)
- Covered in gate1-decisions.md
- Recommend phased approach: port Phase 1 PoC as-is, add features incrementally

---

## Architecture Decision Record

**Decision:** Stay with Windows Terminal, use `--window ws-<project>` naming convention for window identity tracking.

**Alternatives Considered:**
1. WezTerm — rich CLI, but windows not enumerable by Win32 → cannot position
2. Alacritty — clean identity model, but DLL issue + requires tmux

**Justification:** WT's `--window` feature directly addresses the tracking problem with minimal implementation burden and no vendor lock-in risk.

**Implementation:** See "Phase 2 Implementation Readiness" section above.

**If This Fails:** See "Terminal Window Naming Fallback" in `design-note-WT-tracking.md` (approach 2: title-pattern matching, approach 4: deploy journal).

---

## Approval Gate

✅ Investigation complete and documented
✅ Recommendation clear and justified
✅ Implementation plan defined
✅ Risk assessment done
✅ No blockers remain for Phase 2 start

**Ready to proceed with Phase 2 development.**
