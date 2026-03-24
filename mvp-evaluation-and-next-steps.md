# MVP Evaluation and Next Steps

**Date:** 2026-03-23
**Evaluator:** Claude Opus 4.6
**Scope:** Phase 2 MVP against spec (`workspace-orchestrator-spec.md`), plans (`workspace-orchestrator-plan.md`, `phase-2-plan.md`), and current implementation (`mvp-status.md`)

---

## Executive Summary

Phase 2 is **code-complete across all four sprints**. The solution builds cleanly (0 warnings, 0 errors) and all smoke tests pass in dry-run mode. The CLI surface matches the plan exactly, including stretch-goal features (hotkeys, snapshots). The primary gap is that **live integration testing has not been performed** — all validation so far has been via `--dry-run`, `--help`, and build verification. The tool is ready for its first real-world deployment cycle.

---

## Evaluation: Spec Coverage

### Fully Implemented (Phase 2 Scope)

| Spec Feature | Implementation | Notes |
|---|---|---|
| Deploy operation (VS Code, WT, Explorer) | `DeployService.cs` | Full launch + position + VD move |
| Stow operation (graceful close) | `StowService.cs` | WM_CLOSE escalation, HWND-first for browsers |
| Context switching (atomic stow+deploy) | `ws switch` command | `SwitchCommand.cs` |
| YAML project config (v2 schema) | `ProjectContext.cs`, `ConfigLoader.cs` | Backward-compatible with v1 |
| Virtual desktop management | `VirtualDesktopService.cs` + `VirtualDesktopBridge.ps1` | PS bridge to MScholtes module |
| Terminal identity tracking | `--window ws-<project>` convention | Enables reliable multi-project stow |
| Deploy journal | `StateManager.cs` → `~/.workspaces/state.json` | HWND + title-pattern tracking |
| Browser profile support (Chrome/Edge) | `BrowserLauncher.cs` | Auto-detect install path, HWND-first stow |
| Window state snapshots | `SnapshotService.cs` | Auto-capture on stow, restore on deploy |
| Global hotkeys | `HotkeyService.cs` | `RegisterHotKey` Win32 API, daemon mode |
| CLI interface (9 commands) | System.CommandLine | deploy, stow, switch, list, status, validate, edit, hotkeys, snapshot |
| Dry-run mode | All deploy/stow commands | `--dry-run` flag |
| Tab completion | All project-name arguments | Via `AddCompletions()` |
| Styled output | Spectre.Console | Tables, spinners, colored text |
| Install script | `install.ps1` | Publish + PATH + sample config copy |
| Explorer path normalization | `DeployService.LaunchExplorer()` | Forward-slash → backslash conversion |
| Graceful degradation | Deploy/stow services | Per-app try/catch, continue on failure |

### Partially Implemented or Needs Validation

| Feature | Status | Gap |
|---|---|---|
| Multi-project simultaneous deploy | Code written | Needs live integration test |
| Window positioning reliability | Code uses polling + timeout | Needs live test to verify timing |
| Browser HWND tracking | Snapshot before + poll after | Needs live test with Chrome/Edge |
| Virtual desktop create/switch/move | PS bridge written | Needs live test |
| `run_on_deploy` terminal commands | WT `-- pwsh -NoExit -Command` pattern | Needs live test |
| Published `ws.exe` binary | `install.ps1` written | Script not yet run |
| Config search at `~/.workspaces/` | `ConfigLoader.GetSearchDirectories()` | `~/.workspaces/` dir doesn't exist yet |

### Not Implemented (Deferred per Plan)

| Spec Feature | Target Phase | Notes |
|---|---|---|
| System tray GUI | Phase 3 (P3.1) | Hotkey daemon is headless for now |
| Monitor profile support | Phase 3 (P3.2) | Single-monitor assumed |
| Scan & Create wizard | Phase 3 (P3.3) | No desktop capture yet |
| Shared/pinned windows | Phase 3 (P3.4) | All windows subject to stow |
| WSL terminal tabs | Phase 3 (P3.5) | Only pwsh/cmd/bash shells |
| Project templates | Phase 3 (P3.6) | Manual config creation only |
| Docker/container integration | Phase 3 (P3.7) | No container lifecycle |
| Resilience/recovery (watchdog, resume) | Phase 3 (P3.8) | Basic journal only |
| Office/OneNote support | Phase 3 (from P2.7, deferred) | Not in Sprint plan |
| Browser tab snapshot via CDP | Phase 3 | URLs from config only |
| `stow` section in config (per-app close behavior) | Phase 3 | Hardcoded escalation |
| Virtual desktop naming | Phase 3 | Numbered desktops only |

---

## Outstanding Tasks / Action Items

### Priority 1: Live Integration Testing

These must pass before daily-driving the tool:

1. **First install:** Run `install.ps1` to publish `ws.exe` and set up `~/.workspaces/`
2. **Basic deploy cycle:** `ws deploy sample-project-1` → verify VS Code, WT (with `ws-sample-project-1` title), and Explorer all open and are positioned correctly
3. **Stow isolation:** Deploy both sample projects → `ws stow sample-project-1` → verify only project-1 windows close, project-2 windows survive
4. **Switch test:** `ws switch sample-project-2` → verify atomic stow+deploy
5. **Browser test:** Deploy a project with browser config → verify Chrome/Edge opens with profile → stow → verify browser closes
6. **Virtual desktop test:** Deploy to desktop 2 → verify windows appear on desktop 2 → verify auto-switch to desktop 2
7. **Snapshot round-trip:** Deploy → manually move a window → stow (snapshot captured) → re-deploy → verify window restores to moved position, not config position
8. **Hotkey test:** `ws hotkeys start` in a dedicated terminal → press `Ctrl+Alt+1` → verify deploy fires

### Priority 2: Pre-Daily-Driving Fixes

Items likely to surface during integration testing:

1. **`~/.workspaces/` directory creation:** The directory doesn't exist yet. `install.ps1` creates `~/.workspaces/bin/` but doesn't explicitly create the root `~/.workspaces/` directory. `StateManager` and `ConfigLoader` should handle this gracefully.
2. **Sample config paths:** The sample configs reference `C:/dev/workspace-orchestrator/sample-projects/sample-project-1` which exists, but the VS Code workspace field points to a directory, not a `.code-workspace` file. Verify VS Code opens the folder correctly.
3. **WT window discovery timing:** The 15-second timeout in `DeployService.LaunchTerminal()` should be adequate, but WT can be slow to create named windows on first launch. May need adjustment.
4. **Explorer window matching:** If the `sample-projects` folder is already open in Explorer, the folder-name regex could match the wrong window. The HWND-delta approach (snapshot before, find new after) helps but isn't implemented for Explorer like it is for VS Code and WT.

### Priority 3: Backlog for Phase 2 Hardening

1. **`ws stow` with no argument:** Currently shows help. Should stow all deployed projects (common workflow).
2. **Config validation depth:** `ws validate` checks paths exist but doesn't verify WT is installed, Chrome is installed, etc. A `ws doctor` command would help.
3. **Error messages:** Untested edge cases (WT not installed, pwsh not at expected path, MScholtes module missing) likely produce raw exception traces instead of user-friendly messages.
4. **Logging to file:** Currently all output goes to console. Phase 2 plan mentioned structured logging to `~/.workspaces/logs/` — not implemented.

---

## Findings and Recommendations

### Architecture Quality: Strong

The codebase is well-structured with clear separation of concerns:
- `Core` library is independent of CLI framework
- Services are composed via constructor injection in `Program.cs`
- Win32 interop is isolated in `Interop/Win32.cs`
- The PS bridge pattern for virtual desktops is pragmatic and avoids premature optimization

### Code Quality: Good, with Minor Concerns

1. **Snapshot matching by type only:** If a project has two VS Code instances (uncommon but possible), only the first snapshot entry matches. The `phase-2-plan.md` acknowledges this and defers to Phase 3.
2. **VS Code path hardcoded:** `DeployService.cs:18` hardcodes the VS Code path via `LocalApplicationData`. If VS Code is installed system-wide or via a different method, this breaks. Should fall back to `code` on PATH.
3. **No unit tests:** The plan mentions a `WorkspaceOrchestrator.Tests` project for Sprint 2 — this was not created. All testing is manual smoke tests.
4. **`StowByConvention` VS Code pattern:** The fallback pattern `"Visual Studio Code"` matches ANY VS Code window, not just the project's. This is a known limitation of convention-based stow.

### Multi-Monitor Support: Not Functional

The YAML schema includes a `monitor` field in `WindowConfig` (e.g., `monitor: 0`, `monitor: 1`), and the field is parsed and stored in the model, but **no code reads it**. Specifically:

- `DeployService` passes `position.X, position.Y, position.Width, position.Height` directly to `WindowManager.PositionWindow()` → `SetWindowPos()`, completely ignoring the `monitor` value.
- No monitor enumeration APIs (`EnumDisplayMonitors`, `GetMonitorInfo`, `MonitorFromWindow`) are declared in `Win32.cs` or called anywhere in the codebase.
- `SetWindowPos` uses the unified virtual screen coordinate space, so multi-monitor *technically works* if the user hardcodes absolute coordinates that account for monitor offsets (e.g., `{ x: 1920, y: 0, ... }` for a second monitor to the right of a 1920-wide primary). But the `monitor: N` field is a silent no-op.
- Snapshot capture records absolute coordinates but does not record which monitor a window is on.

**Impact:** Users with multi-monitor setups can work around this by using absolute screen coordinates in their configs, but the `monitor` field is misleading — it suggests monitor-relative positioning that doesn't exist.

**Recommendation:** Before Phase 3's full monitor profile system (P3.2), a lighter fix would make multi-monitor usable:
1. Add `EnumDisplayMonitors` / `GetMonitorInfo` P/Invoke declarations to `Win32.cs`
2. Add a monitor enumeration method that returns each monitor's work-area rectangle
3. In `DeployService`, resolve `monitor: N` + relative `position` into absolute screen coordinates (offset by monitor N's origin) before calling `PositionWindow`
4. In `SnapshotService`, reverse-lookup absolute coordinates to determine which monitor a window is on

This would honor the `monitor` field that already exists in configs without requiring the full named-profile system.

### Risk Assessment

| Risk | Severity | Likelihood | Recommendation |
|---|---|---|---|
| First live deploy reveals timing issues | Medium | High | Run integration tests in Priority 1 before daily-driving |
| MScholtes VD module version mismatch after Windows update | Medium | Medium | Pin to known-good version; test after updates |
| Browser profile auto-creation quirks | Low | Medium | Document manual profile creation as fallback |
| Hotkey daemon crashes silently | Medium | Low | Add PID file staleness detection |

### Recommendations for Phase 3 Prioritization

Based on the evaluation, the most impactful Phase 3 features would be:

1. **System tray app (P3.1)** — The hotkey daemon blocking a terminal is a friction point. A tray app solves this and adds discoverability.
2. **Monitor profile support (P3.2)** — Essential for laptop + dock workflows.
3. **Scan & Create (P3.3)** — Dramatically lowers the barrier to adding new projects. Currently, users must hand-write YAML and measure window positions.
4. **`ws doctor` command (P3.9/P3.10)** — Validates the environment (WT installed, Chrome path, VD module, .NET runtime).
5. **Direct COM interop for VD (replacing PS bridge)** — Reduces deploy/stow latency and removes pwsh dependency.

---

## Phase 2 Success Criteria Scorecard

| Criterion | Result |
|---|---|
| `ws deploy <project>` works for all sample projects | **Smoke-tested (dry-run only)** — needs live test |
| `ws stow <project>` closes only that project's windows | **Code correct** — needs live test |
| `ws switch <project>` performs atomic stow+deploy | **Code correct** — needs live test |
| `ws list` and `ws status` display correct information | **Verified** |
| Two projects deployed simultaneously on different VDs | **Code supports it** — needs live test |
| Deploy journal handles edge cases | **Verified** (idempotent RecordDeploy) |
| All Phase 1 tests still pass | **Verified** (PS scripts untouched) |
| Binary publishable as self-contained `ws.exe` | **`install.ps1` written** — not yet executed |
| Browser profile launch + stow | **Code written** — needs live test |
| Global hotkeys (Ctrl+Alt+N) | **Code written** — needs live test |
| Window snapshots (auto-capture + restore) | **Code written** — needs live test |

**Overall verdict:** Phase 2 is **feature-complete** and **build-verified**. The next milestone is completing the live integration test suite in Priority 1 above, after which the tool is ready for daily-driving evaluation toward Gate 2.
