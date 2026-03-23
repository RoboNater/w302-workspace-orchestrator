# Terminal Emulator Investigation Plan

**Date:** 2026-03-22
**Author:** Claude Sonnet 4.6
**Status:** Active
**Related:** `design-note-WT-tracking.md`, `gate1-decisions.md`, `workspace-orchestrator-plan.md`

---

## Background & Motivation

Phase 1 PoC revealed that Windows Terminal (WT) has a fundamental window identity problem: all WT
windows share the process name `WindowsTerminal`, titles are transient (overwritten by the active
tab's CWD or running command), and there is no stable Win32-queryable identifier linking a WT window
to the project that launched it. This was discovered during multi-project testing and is documented
in detail in `design-note-WT-tracking.md`.

The Gate 1 decision was to **investigate alternative terminal emulators before committing to WT-centric
workarounds** in Phase 2. WezTerm and Alacritty have been installed to the system for evaluation.

---

## Investigation Objective

Determine the best terminal emulator strategy for Phase 2 of the workspace orchestrator, specifically:

1. Whether a terminal emulator exists that has better programmatic window identity than Windows Terminal
2. Whether WT workarounds (named windows, title conventions, deploy journal) are good enough to avoid
   switching emulators
3. What the migration cost would be if we switch from WT

The output is a recommendation in `terminal-investigation.md` with evidence.

---

## Candidates

| # | Terminal | Version to test | Install path |
|---|----------|----------------|--------------|
| 1 | Windows Terminal | Current installed version | `wt.exe` (in PATH) |
| 2 | WezTerm | Latest release | `C:\Program Files\WezTerm\wezterm.exe` |
| 3 | Alacritty | Latest release | `C:\Program Files\Alacritty\alacritty.exe` |
| 4 | Others (Tabby, Hyper) | N/A | Research only (not installed) |

---

## Evaluation Criteria

Each candidate is scored on 7 criteria. Scoring: ✅ Good / ⚠️ Partial / ❌ Poor / ❓ Unknown

| # | Criterion | Why it matters |
|---|-----------|---------------|
| C1 | **Window identity** | Can we launch with a stable, externally-queryable ID and find/close that exact instance later? |
| C2 | **Multi-tab launch** | Can we open N named tabs in specific directories from a single command or scripted sequence? |
| C3 | **Command injection** | Can we run a command in a specific tab on launch and leave the shell open afterward? |
| C4 | **Graceful stow/close** | Can we close one specific instance without affecting other instances of the same terminal? |
| C5 | **Win32 positioning** | Does the window respond to `SetWindowPos` / standard Win32 window management? |
| C6 | **User experience** | Is this a terminal someone would actually want to use daily? |
| C7 | **Maturity & maintenance** | Actively maintained? Stable on Windows 11? Likely to survive OS updates? |

---

## Test Plan

### Section 1 — Environment Baseline

**1.1 — Version inventory**
- WT: `wt --version`
- WezTerm: `wezterm --version`
- Alacritty: `alacritty --version`
- Document exact versions for all candidates

**1.2 — Win32 window class names**
- After launching each terminal, use `EnumWindows` (via the existing `Win32.ps1` lib) to capture
  window class name, title, and PID for each terminal
- This establishes the baseline for what's available from the Win32 perspective

---

### Section 2 — Windows Terminal (Status Quo)

#### C1 — Window Identity

**Test 2.1.A — Named window launch (`--window`)**
```
wt --window ws-project-1 new-tab --title "Project" -d C:\dev
```
- Does it create a window named `ws-project-1`?
- Is the name queryable via `wt` CLI? (`wt` has no `list` command, but check)
- Is the name visible in any Win32 window property (title, class, `GetWindowText`)?
- Can we re-target the same window? `wt --window ws-project-1 new-tab` — does it open in the same window?

**Test 2.1.B — Title persistence (`--title`)**
```
wt --title "[ws:project-1]" new-tab --title "Tab-A" -d C:\dev
```
- Does the WT window title show `[ws:project-1]`?
- Does it change after switching tabs? After running a command in a tab?
- Is there a way to set the *window* title as distinct from the *tab* title?

**Test 2.1.C — Window class inspection**
- What is the Win32 window class for a WT window? (`CASCADIA_HOSTING_WINDOW_CLASS` is the known class)
- Are there named window variants with different class names?
- Is `GetWindowText` the window title or the tab title?

#### C2 — Multi-Tab Launch

**Test 2.2.A — Current approach review**
- Review the `lib/TerminalLauncher.ps1` approach: building a single WT command string with `;` delimiters
- Verify it still works in current WT version
- Note any limitations (max tabs, tab ordering, etc.)

**Test 2.2.B — Named-window multi-tab**
```
wt --window ws-project-1 new-tab --title "Tab-A" -d C:\dev ; new-tab --title "Tab-B" -d C:\dev\foo
```
- Does `--window` combined with multi-tab syntax work?

#### C3 — Command Injection

**Test 2.3 — Command + named window**
- Does `-- pwsh -NoExit -Command "echo hello"` work inside a named window launch?
- Is command injection still reliable?

#### C4 — Graceful Close

**Test 2.4.A — Close named window**
- Is there a `wt` CLI command to close a named window? (e.g., `wt --window ws-project-1 close`)
- If not, can we find the HWND of a named window and send `WM_CLOSE`?
- Does WT still show "close all tabs?" dialog on `WM_CLOSE`?

**Test 2.4.B — Disambiguate multiple WT windows**
- With two named WT windows open, can stow close one without affecting the other?

#### C5 — Win32 Positioning
- Already validated in Phase 1. Confirm still works, note any caveats with named windows.

---

### Section 3 — WezTerm

WezTerm is a GPU-accelerated terminal with a built-in IPC layer (`wezterm cli`) and Lua config.
Its multiplexer model means all panes share a single background server process.

#### C1 — Window Identity

**Test 3.1.A — Launch and list**
```
wezterm start --class "ws-project-1"
wezterm cli list-clients
wezterm cli list
```
- Does `--class` set a queryable identifier for the window?
- What does `wezterm cli list` return? Does it include window IDs, pane IDs, titles, CWDs?
- Can we identify a specific window by class name via Win32 `GetClassNameW`?

**Test 3.1.B — Window ID stability**
- Does WezTerm expose a stable window ID via `wezterm cli list-clients`?
- Can we use that ID to target close/spawn operations?

**Test 3.1.C — Multiple instances**
- WezTerm uses a multiplexer model — by default, new `wezterm start` invocations attach to the
  existing server rather than creating a new window. Is this configurable?
- How do we ensure project-A's WezTerm is distinct from project-B's WezTerm?

#### C2 — Multi-Tab Launch

**Test 3.2.A — Spawn with tabs**
```
wezterm start --class "ws-project-1" -- pwsh -NoExit
wezterm cli spawn --pane-id <id> -- pwsh -NoExit
```
- How do we add tabs to a WezTerm window after launch?
- Is there a way to open N tabs in one command? Or do we spawn sequentially?

**Test 3.2.B — Tab title**
```
wezterm cli set-tab-title --tab-id <id> "MyTabName"
```
- Can we set tab titles via CLI?

#### C3 — Command Injection

**Test 3.3 — Send command to tab**
```
wezterm cli send-text --pane-id <id> "echo hello\r"
```
- Does `send-text` work for injecting commands into a running pane?
- How does this differ from WT's approach (where commands run on launch via args)?
- Does the shell remain open after the command runs?

#### C4 — Graceful Close

**Test 3.4 — Kill specific window**
```
wezterm cli kill-pane --pane-id <id>
# or
wezterm cli close-tab --tab-id <id>
# or
# Send WM_CLOSE to the window HWND
```
- Can we close a specific WezTerm window by ID without affecting other WezTerm windows?
- What happens when all panes in a window are closed — does the window close?

#### C5 — Win32 Positioning

**Test 3.5**
- Is WezTerm's window a standard Win32 window that responds to `SetWindowPos`?
- Does `wezterm cli set-window-workspace` or similar CLI exist for placement?

---

### Section 4 — Alacritty

Alacritty is a minimalist, GPU-accelerated terminal with a strict one-process-per-window model.
It has no built-in tab support — tabs would require an external multiplexer (tmux/zellij).
IPC is limited (no built-in CLI control plane like WezTerm).

#### C1 — Window Identity

**Test 4.1.A — Window class**
```
alacritty --class "ws-project-1,ws-project-1" -e pwsh
```
- Does `--class` set both WM_CLASS instance and class?
- Is `--class` queryable via Win32 `GetClassNameW` for window discovery?
- With two Alacritty instances, can we tell them apart by class?

**Test 4.1.B — Window title**
```
alacritty --title "ws:project-1" -e pwsh
```
- Does `--title` persist as the window title on Windows?
- Does running a shell command change it (shell title sequences)?

#### C2 — Multi-Tab Launch

**Test 4.2 — Tabs model**
- Alacritty has no native tabs. The options are:
  a. Multiple Alacritty windows (one per "tab" role)
  b. Alacritty + tmux inside
  c. Alacritty + Zellij inside
- Evaluate feasibility of option (a) for the orchestrator: multiple named windows
- Evaluate feasibility of option (b/c): does tmux/zellij interact well with the orchestrator?

#### C3 — Command Injection

**Test 4.3**
```
alacritty --class "ws-project-1,ws-project-1" -e pwsh -NoExit -Command "echo hello"
```
- Does `-e` properly pass arguments to the shell?
- Does the shell stay open after the command?

#### C4 — Graceful Close

**Test 4.4 — Close by class**
- With `--class` set uniquely, can we find the HWND via `GetClassNameW` match in `EnumWindows`?
- Does `WM_CLOSE` close Alacritty gracefully (no "close tabs?" dialog since no tabs)?

#### C5 — Win32 Positioning

**Test 4.5**
- Is Alacritty a standard Win32 window? Does `SetWindowPos` work?

---

### Section 5 — Other Candidates (Research Only)

#### Tabby
- Check: GitHub repo, Windows support quality, IPC/CLI capabilities
- Key question: Does it have an external control API?

#### Hyper
- Check: Maintenance status (has had activity slowdowns), Electron-based overhead
- Key question: Is it actively maintained and performant enough?

#### Kitty
- Check: Windows support status (historically Linux-first)
- Key question: Does it have native Windows builds?

---

### Section 6 — Synthesis

**6.1 — Summary table**
Populate the scoring table with findings from all tests.

**6.2 — WT workaround viability**
Based on test results, assess whether the WT named-window approach (`--window <name>`) is
sufficient for Phase 2 — specifically: can a named window's HWND be discovered via Win32 after
the fact (without storing it at launch time)?

**6.3 — Recommendation**
One of:
- **Stick with WT + deploy journal** — named windows give enough identity; journal tracks intent
- **Switch to WezTerm** — IPC CLI gives reliable per-window control; migration cost is acceptable
- **Alacritty + multiple windows** — simplest identity model; tabs-in-tmux is workable
- **Support multiple** — let workspace YAML specify which terminal; implement adapters

**6.4 — Migration cost estimate (if switching)**
- What changes in `deploy.ps1` / `stow.ps1`?
- What changes in `TerminalLauncher.ps1` (or C# equivalent)?
- User experience delta: what does the user lose or gain?

---

## Execution Notes

- All tests run in a Windows-native pwsh 7 session (not WSL2)
- WezTerm and Alacritty are installed but have not been launched yet — first launch may create
  default config files; note these locations
- Stdout capture pattern: redirect to file, then Read (MINGW64 bash suppresses output)
- WT version check before testing: WT behavior around `--window` changed significantly in v1.12
- Document every command run and its exact output in `terminal-investigation.md`
- If a test would leave background processes running, clean them up before the next test

---

## Output Document

Findings are written to: **`terminal-investigation.md`**

Structure:
1. Versions & environment
2. Windows Terminal findings
3. WezTerm findings
4. Alacritty findings
5. Other candidates (brief)
6. Summary scoring table
7. Recommendation
8. Migration plan (if applicable)
