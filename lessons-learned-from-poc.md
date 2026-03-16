# Lessons Learned from Phase 1 PoC

**Date:** 2026-03-16
**Scope:** Post-PoC bug fixes on branch `fix/wt-window-disambiguation`

These lessons were extracted from issues discovered after the initial PoC was declared
complete and all automated tests were passing. They surfaced only when tests were run
from real working environments (e.g. launching a terminal test from an existing terminal
session) rather than from a clean desktop.

---

## 1. Process identity is not window identity

**What happened:** `Find-WindowByProcess` matched windows by process name alone.
When the test was run from inside Windows Terminal, the function returned the
caller's own window alongside the newly launched one — and operations (move, close)
hit the wrong target.

**Lesson:** On Windows, a single process name can own many windows, and a single
process can host multiple windows (WT, Chrome, Explorer). Any function that discovers
windows must support disambiguating between pre-existing and newly created instances.
Process name is necessary but rarely sufficient.

**Guideline for Phase 2+:** Window discovery should always work in terms of
"find the *new* window I just launched," not "find *a* window for this process."
The before/after snapshot pattern (record existing HWNDs, launch, find the delta)
is a reliable primitive to build on.

---

## 2. Killing a process is not the same as closing a window

**What happened:** To suppress Windows Terminal's "close all tabs?" confirmation
dialog, the initial fix used `Stop-Process` to force-kill the `WindowsTerminal.exe`
process. This killed *every* WT window — including the one the developer was
working in — because multiple WT windows share a single process.

**Lesson:** On modern Windows, many applications use a single-process, multi-window
architecture (WT, Chrome, Edge, Explorer). `Stop-Process` is a process-level
sledgehammer that cannot target individual windows. Always prefer window-level
operations (`WM_CLOSE`, `IsWindowVisible` checks) over process-level ones.

**Guideline for Phase 2+:** The close/stow path should never use `Stop-Process`
as a first resort. The safe escalation ladder is:
1. `WM_CLOSE` to the window
2. Wait and verify via HWND visibility
3. Second `WM_CLOSE` (dismisses confirmation dialogs)
4. `Stop-Process` only as a last resort, and only after warning the user that
   other windows of the same application will be affected

---

## 3. Tests must run in realistic environments, not just clean ones

**What happened:** All PoC tests passed when run on a clean desktop with no
pre-existing instances of the target applications. The window disambiguation
bug only appeared when the test was launched from inside Windows Terminal — the
most common way a developer would actually run it.

**Lesson:** Desktop automation tests are environment-sensitive in ways that
typical unit or integration tests are not. A test that works on a clean desktop
may fail when other instances of the same application are running, when the
test is launched from the application it's testing, or when windows from a
previous failed test run are still open.

**Guideline for Phase 2+:** Always assume the target application is already
running when writing window management code. Test scripts should explicitly
handle the "app already open" case. Consider adding a pre-test inventory step
that logs what's currently running, so failures are easier to diagnose.

---

## 4. Confirmation dialogs break automated close sequences

**What happened:** Windows Terminal's "Do you want to close all tabs?" dialog
intercepted `WM_CLOSE`, leaving the window alive and blocking the cleanup step.

**Lesson:** Many applications present confirmation dialogs on close, especially
when they have multiple documents/tabs/sessions open. Any automated close
operation must account for this — you cannot assume `WM_CLOSE` will succeed
silently.

**Guideline for Phase 2+:** Build a resilient close pattern that detects
whether the window actually closed after `WM_CLOSE`, and has a strategy for
the case where it didn't. For Windows Terminal specifically, a second `WM_CLOSE`
dismisses the dialog. Other applications may need different handling — this
should be configurable per application type in Phase 2.

---

## Summary for Phase 2 Architecture

These issues all stem from the same root assumption: that there is a 1:1
relationship between a process name and "the window we care about." In reality,
the relationship is many-to-many — one process can own many windows, and our
target window exists alongside an unknown number of siblings.

Phase 2 should treat window handles (HWNDs) as the primary identifiers throughout
the deploy/stow lifecycle, not process names or PIDs. The deploy service should
maintain a manifest of HWNDs it created, and stow should close exactly those
handles — nothing more, nothing less.
