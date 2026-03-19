# Design Note: Windows Terminal Window Tracking

**Date:** 2026-03-18
**Status:** Open — discovery phase
**Relates to:** P2.1 (Multi-Project Context Switching), P2.6 (Robust Error Handling)

---

## The Problem

When multiple projects are deployed simultaneously, `stow.ps1` cannot reliably close
the correct Windows Terminal window. Unlike VS Code and File Explorer — whose window
titles contain the workspace or folder name — WT window titles are transient. They
reflect the active tab's working directory or running command and change as the user
interacts with the terminal.

The current Phase 1 implementation works around this by closing the "last" WT window
found via `Get-AllWindows | Select-Object -Last 1`. This is fragile: it depends on
enumeration order and makes no distinction between project-owned terminals and the
user's own terminal sessions (including, notably, the terminal running the orchestrator
itself).

**Discovered during:** multi-project isolation testing — stowing project-2 closed the
Claude Code terminal instead of project-2's terminal.

---

## Root Cause

Win32 `EnumWindows` provides per-window handles (HWNDs) and the owning process name,
but all Windows Terminal windows share the process name `WindowsTerminal`. There is no
stable, queryable, per-window identifier in the Win32 API that associates a WT window
with the intent behind its creation.

This is a fundamental platform limitation, not a bug in our code.

---

## Approaches Considered

### 1. HWND State File (Deploy Journal)

Record the HWND of each WT window at launch time in a per-project state file. Stow
reads the file and closes only the recorded HWND.

| Pros | Cons |
|------|------|
| Simple to implement | HWNDs are ephemeral — invalidated on close, values recycled by OS |
| No changes to WT launch args | State goes stale if user manually closes the terminal |
| | Cannot detect if the HWND now belongs to a different window |
| | Constrains user behavior (must not close/reopen terminals outside orchestrator) |

**Verdict:** Too fragile for a tool that should be invisible to the user's workflow.

### 2. WT `--title` Convention

Launch WT with a tagged title: `wt --title "[ws:sample-project-1]"`. Stow matches
on the title pattern, the same way it already matches VS Code and Explorer windows.

| Pros | Cons |
|------|------|
| No state file needed — live window query | Title is overwritten by the active tab if the user switches tabs or runs a command |
| Familiar pattern (already used for VS Code/Explorer) | User sees the tag in the title bar (minor cosmetic issue) |
| User can still open/close terminals freely | Requires testing whether `--title` persists across tab switches in current WT versions |
| Lightweight change to deploy.ps1 | |

**Verdict:** Worth prototyping. If the title persists (or can be periodically refreshed),
this is the lowest-friction solution. If WT overwrites it on tab switch, it becomes
unreliable in exactly the same way as the current approach.

### 3. WT Named Window Instances (`--window`)

Newer WT versions support `wt --window myname` to create named instances. The name is
an internal identifier, not tied to the title bar. Subsequent `wt` commands can target
a named window (e.g., `wt --window myname new-tab ...`).

| Pros | Cons |
|------|------|
| Proper identity — not affected by title changes | Requires WT 1.12+ (2022); need to verify user's version |
| Enables future features (add tabs to a running project terminal) | Name is internal to WT — unclear if it's queryable via Win32 APIs for stow |
| Clean separation of project terminals | May require WT settings or JSON config changes |
| No visible UI artifact | Discovery: how does stow find the HWND for a named instance? |

**Verdict:** The "right" solution architecturally, but needs investigation into whether
a named instance's HWND can be discovered externally (via Win32) for positioning and
closing. If it can, this is the long-term answer.

### 4. Deploy Journal as Intent Record (Hybrid)

The deploy journal records *what to look for*, not a specific HWND. For example:
`{ "project": "sample-project-1", "app": "wt", "match": { "title_pattern": "[ws:sample-project-1]" } }`.
Stow searches for windows matching the intent at stow time.

| Pros | Cons |
|------|------|
| Decouples launch from cleanup — no stale HWND problem | Only as good as the matching strategy (title, named instance, etc.) |
| Supports partial-deploy recovery (journal says what *should* exist) | Adds a persistence layer that must be kept in sync |
| Extensible to other apps with similar identity problems | Overkill if approach 2 or 3 solves the problem directly |
| Natural foundation for P2.6 resilience features | |

**Verdict:** Good architecture for Phase 2 regardless of which matching strategy we
choose. The journal answers "what did we intend to launch?" and the matching strategy
answers "how do we find it now?"

---

## Recommendation

**Phase 1 (current):** Accept the limitation. Document it. The PoC goal was to validate
the hard technical problems, and this is exactly the kind of discovery it was designed
to surface.

**Phase 2 investigation:** Prototype approaches 2 and 3 in a side branch or spike project:

1. Test whether `wt --title` persists across tab switches on Windows 11 current builds.
2. Test whether `wt --window <name>` instances can be discovered via `EnumWindows` (and
   if the name is exposed in any queryable window property).
3. Based on findings, choose one as the matching strategy and wrap it in a deploy
   journal (approach 4) for resilience.

This may warrant a standalone spike project, since the investigation involves WT version
behavior, undocumented window properties, and potentially reading WT's internal state
— work that is orthogonal to the main orchestrator feature track.

---

## Open Questions

- Does `wt --title` survive tab switches? If so, on which WT versions?
- Does `wt --window <name>` expose the name in any Win32-queryable window property?
- Can we read WT's internal session state (e.g., via its JSON settings or a named pipe)
  to map window instances to our projects?
- Are there other terminal emulators (Alacritty, WezTerm) that handle window identity
  better, and should the orchestrator support them as alternatives?
