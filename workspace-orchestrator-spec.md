# Project Workspace Orchestrator — Design Specification

**Version:** 1.1 (updated post-Phase 1 PoC)
**Target Platform:** Windows 11
**Purpose:** One-click deploy/stow of entire project contexts — all associated applications, windows, terminal sessions, browser tabs, and documents — with correct placement across virtual desktops.

---

## 1. Core Concepts

### 1.1 Project Context

A **Project Context** is a declarative configuration file (YAML or JSON) that fully describes the set of applications, windows, files, URLs, terminal sessions, and layout information needed to work on a specific project.

Each project context has two states:

- **Deployed** — all applications launched, windows positioned, terminals in correct directories, browser tabs open
- **Stowed** — all project windows gracefully closed (or optionally minimized/hidden), with current state snapshotted for future re-deployment

### 1.2 Context Switching

A **Context Switch** is an atomic operation:

1. **Stow** the currently active project (snapshot state → close windows)
2. **Deploy** the target project (launch apps → restore state → position windows)

The user triggers this with a single hotkey, CLI command, or system tray menu selection.

### 1.3 Virtual Desktop Mapping

Each project context is assigned to one or more Windows 11 virtual desktops. On deploy, the orchestrator creates/activates the required desktops and places windows accordingly. On stow, it optionally removes empty desktops.

---

## 2. Project Context File Format

```yaml
# ~/.workspaces/myapp.workspace.yaml

meta:
  name: "MyApp"
  description: "Full-stack web application"
  icon: "C:/dev/myapp/icon.png"          # optional, for system tray menu
  hotkey: "Ctrl+Alt+1"                    # optional, direct-deploy hotkey
  tags: ["work", "web", "active"]

virtual_desktops:
  # Which virtual desktops to use (created if they don't exist)
  primary: 2          # main working desktop
  secondary: 3        # optional overflow (e.g., docs/reference)

layout:
  # Window placement strategy
  engine: "manual"    # "manual" | "glazewm" | "fancyzones" | "auto-tile"
  monitor_profile: "home-office"   # named monitor arrangement (see §6)

applications:
  vscode:
    type: "vscode"
    workspace: "C:/dev/myapp/myapp.code-workspace"
    desktop: primary
    window:
      monitor: 0
      position: { x: 0, y: 0, width: 1920, height: 1040 }
      # OR use named zones: zone: "left-half"

  terminals:
    type: "terminal"
    terminal_app: "windows-terminal"      # windows-terminal | wezterm | alacritty (see §5.2)
    desktop: primary
    window:
      monitor: 1
      position: { x: 0, y: 0, width: 960, height: 1040 }
    tabs:
      - title: "Backend"
        directory: "C:/dev/myapp/backend"
        shell: "pwsh"                     # pwsh | cmd | wsl | git-bash
        run_on_deploy: "npm run dev"      # optional command to execute
        env:                              # optional env vars
          NODE_ENV: "development"
      - title: "Frontend"
        directory: "C:/dev/myapp/frontend"
        shell: "pwsh"
        run_on_deploy: "npm start"
      - title: "Git"
        directory: "C:/dev/myapp"
        shell: "pwsh"
      - title: "SSH Tunnel"
        directory: "~"
        shell: "pwsh"
        run_on_deploy: "ssh -L 5432:localhost:5432 staging-server"
        stow_action: "send-sigint"        # gracefully stop on stow

  browser_main:
    type: "browser"
    browser: "chrome"                     # chrome | edge | firefox
    profile: "Default"                    # browser profile name
    desktop: primary
    window:
      monitor: 0
      position: { x: 960, y: 0, width: 960, height: 520 }
    tab_groups:
      - name: "App"
        color: "blue"                     # Chrome tab group color
        urls:
          - "http://localhost:3000"
          - "http://localhost:3000/admin"
      - name: "APIs"
        color: "green"
        urls:
          - "http://localhost:8080/swagger"
          - "https://api-docs.example.com"
    # OR reference a saved session:
    # session_file: "C:/dev/myapp/.browser-session.json"

  browser_reference:
    type: "browser"
    browser: "chrome"
    profile: "Default"
    desktop: secondary
    window:
      monitor: 0
      position: { x: 0, y: 0, width: 1920, height: 1040 }
    tab_groups:
      - name: "Docs"
        urls:
          - "https://react.dev"
          - "https://nodejs.org/docs"
      - name: "Project"
        urls:
          - "https://github.com/me/myapp"
          - "https://jira.company.com/board/MYAPP"
          - "https://figma.com/file/xxxxx"

  explorer:
    type: "file-explorer"
    desktop: primary
    paths:
      - "C:/dev/myapp/backend/src"
      - "C:/dev/myapp/frontend/src"
    window:
      monitor: 1
      position: { x: 960, y: 0, width: 960, height: 520 }

  docs:
    type: "office"
    desktop: secondary
    files:
      - path: "C:/dev/myapp/docs/architecture.docx"
        window:
          position: { x: 0, y: 520, width: 960, height: 520 }
      - path: "C:/dev/myapp/docs/api-spec.xlsx"
        window:
          position: { x: 960, y: 520, width: 960, height: 520 }

  onenote:
    type: "onenote"
    desktop: secondary
    notebook: "MyApp Dev Notes"           # notebook name or URL
    section: "Sprint 42"                  # optional
    window:
      monitor: 0
      position: { x: 0, y: 520, width: 1920, height: 520 }

stow:
  # What to do when stowing this project
  snapshot_browser_tabs: true             # save current tabs to session file
  close_terminals: true                   # vs. just minimize
  close_vscode: false                     # VS Code handles its own state well
  terminal_stow_delay_ms: 2000           # wait for processes to exit
  remove_empty_desktops: false
```

---

## 3. Deploy Operation (Detailed)

When the user invokes `deploy <project>`:

### 3.1 Pre-Deploy

1. Check if any other project is currently deployed → offer to stow it first
2. Validate the context file (missing files, unreachable paths, etc.)
3. Create virtual desktops if they don't exist
4. Detect current monitor arrangement and match against `monitor_profile`

### 3.2 Launch Sequence

Order matters for window positioning reliability:

| Step | Action | Notes |
|------|--------|-------|
| 1 | Activate/create virtual desktops | Use `IVirtualDesktopManager` COM API |
| 2 | Launch VS Code | `code <workspace>` — fast, handles own session |
| 3 | Launch Windows Terminal with tabs | Via `wt` CLI with split-pane args |
| 4 | Launch browsers with tab groups | Via CLI flags or browser extension API |
| 5 | Open File Explorer windows | Via `explorer.exe` or `Start-Process` |
| 6 | Open Office documents | Via COM automation or `Start-Process` |
| 7 | Open OneNote | Via protocol handler `onenote:` |
| 8 | **Wait for all windows to appear** | Poll for window handles by process + title |
| 9 | Position & size all windows | `SetWindowPos` Win32 API |
| 10 | Move windows to correct virtual desktops | `IVirtualDesktopManager::MoveWindowToDesktop` |
| 11 | Execute terminal `run_on_deploy` commands | Send keystrokes or use WT API |
| 12 | Switch to primary virtual desktop | |

### 3.3 Window Discovery

After launching each application, the orchestrator must locate its window handle(s).

**Primary strategy (validated in PoC):** Before/after HWND delta — snapshot existing HWNDs for the target process before launch, launch the app, then find the new HWND(s) that appeared. This reliably distinguishes newly launched windows from pre-existing ones of the same application.

**Supporting strategies:**

- **Process ID tracking** — launch via `Start-Process -PassThru`, then enumerate windows for that PID
- **Title matching** — wait for a window with expected title text (regex). Useful for VS Code (title contains workspace name) and Explorer (title contains folder name)
- **Class name matching** — use Win32 `FindWindow` / `EnumWindows` with known class names
- **Timeout + retry** — apps like VS Code may take 3-10 seconds to fully render

**PoC finding:** Process name alone is not sufficient for window discovery. Many Windows applications use a single-process, multi-window architecture (Windows Terminal, Chrome, Edge, Explorer). A given process name can own many windows simultaneously, and window discovery must always handle the "app already running" case. See `lessons-learned-from-poc.md` §1.

---

## 4. Stow Operation (Detailed)

When the user invokes `stow <project>` (or it's triggered by a context switch):

### 4.1 Snapshot Phase

1. **Browser tabs** — query running browser via:
   - Chrome DevTools Protocol (CDP) on `localhost:9222` if launched with `--remote-debugging-port`
   - Browser extension with native messaging (more reliable)
   - Session Buddy / Tab Session Manager export API
2. **Terminal state** — capture:
   - Current working directory per tab (`Get-Location` or `/proc/self/cwd` in WSL)
   - Running foreground process (to know what to re-launch)
   - NOT command history (PSReadLine already persists this globally)
3. **Window positions** — enumerate all project windows, record current geometry
4. **Write snapshot** to `~/.workspaces/.state/myapp.snapshot.yaml`

### 4.2 Close Phase

1. Send `Ctrl+C` / SIGINT to long-running terminal processes (dev servers, tunnels)
2. Wait `terminal_stow_delay_ms` for graceful shutdown
3. Close terminal windows (see escalation ladder below)
4. Close browser windows (unless shared with another project)
5. Close File Explorer and Office windows
6. Optionally close or leave VS Code running (it persists its own state)
7. Optionally remove now-empty virtual desktops

**Window close escalation ladder (from PoC):**

Applications may present confirmation dialogs on close (e.g., WT's "close all tabs?" dialog), and many modern apps share a single process across multiple windows. The close path must operate at the *window* level, not the process level:

1. Send `WM_CLOSE` to the specific window handle (HWND)
2. Wait and verify via HWND visibility check (`IsWindowVisible`)
3. Send a second `WM_CLOSE` (dismisses confirmation dialogs in most apps)
4. `Stop-Process` only as a last resort, with a warning that other windows of the same application will be affected

**Never use `Stop-Process` as a first resort** — it kills the entire process, which may include other project terminals, the user's own terminal, or the terminal running the orchestrator itself. See `lessons-learned-from-poc.md` §2, §4.

---

## 5. Application-Specific Integration Details

### 5.1 VS Code

- **Deploy:** `code --folder-uri <path>` or `code <workspace-file>`
- **Stow:** VS Code can be left running; it auto-saves session state
- **State:** VS Code handles workspace restoration natively — this is the easiest app
- **Multi-instance:** Each workspace opens a separate window; title includes workspace name

### 5.2 Terminal Emulator

> **Note:** The Phase 1 PoC used Windows Terminal exclusively. A terminal emulator investigation is planned before Phase 2 to evaluate whether WT, WezTerm, Alacritty, or another terminal offers the best combination of programmatic control and user experience. See `workspace-orchestrator-plan.md` (Investigation: Terminal Emulator Selection) and `design-note-WT-tracking.md`. The spec below documents what was validated with WT and what challenges remain.

#### 5.2.1 Windows Terminal (PoC-validated)

- **Deploy:** The `wt` CLI supports complex multi-tab layouts. All tab arguments must be passed as a single string to `Start-Process` — passing them as an array causes `;` delimiters to be individually quoted, which WT doesn't recognize:
  ```
  wt new-tab -p "PowerShell" -d C:\dev\myapp\backend --title Backend ; new-tab -p "PowerShell" -d C:\dev\myapp\frontend --title Frontend
  ```
- **run_on_deploy:** The `-- pwsh -NoExit -Command "..."` pattern works — the command runs and the shell stays open. SendKeys is not needed for initial command injection.
- **Working directory:** The `wt` `-d` flag handles this on deploy
- **Session persistence:** Windows Terminal has built-in session restore (`"firstWindowPreference": "persistedWindowLayout"` in settings.json) — but it's global, not per-project
- **History:** PSReadLine persists history globally — no per-project action needed
- **Stow — known challenge:** All WT windows share the process name `WindowsTerminal` and window titles are transient (they reflect the active tab's working directory or running command). There is no stable, externally queryable identifier to associate a WT window with the project that launched it. This makes it impossible to reliably close the correct WT window when multiple projects are deployed. See `design-note-WT-tracking.md` for candidate solutions (`--title` convention, `--window` named instances, deploy journal).
- **Close behavior:** WT presents a "close all tabs?" confirmation dialog on `WM_CLOSE`. A second `WM_CLOSE` dismisses the dialog. `Stop-Process` must be avoided — it kills all WT windows, not just the target one.

#### 5.2.2 Alternative Terminals (Under Investigation)

The terminal emulator investigation will evaluate alternatives against these requirements:
- Stable, externally queryable window identity per instance
- Multi-tab launch with named tabs and per-tab directories via CLI
- Command injection at launch time
- Graceful per-instance close without affecting other instances
- Standard Win32 window management (responds to `SetWindowPos`)
- Good daily-driver UX (appearance, performance, shell integration)

### 5.3 Browser (Chrome/Edge)

This is the hardest integration. Options ranked by reliability:

| Method | Reliability | Setup Complexity | Notes |
|--------|-------------|------------------|-------|
| **Dedicated browser profile per project** | ★★★★★ | Medium | Best approach. Each profile has its own window, tabs, session. `chrome.exe --profile-directory="Project-MyApp"` |
| **Browser extension + native messaging** | ★★★★ | High | Extension saves/restores tab groups. Native messaging host lets orchestrator trigger save/restore. |
| **Chrome DevTools Protocol** | ★★★ | Medium | Launch with `--remote-debugging-port=9222`. Can query and manipulate tabs programmatically. Port conflicts between projects. |
| **Session manager extension (manual)** | ★★ | Low | User manually saves/restores sessions via extension UI. Not automatable. |
| **CLI URL launch** | ★ | Low | `chrome.exe url1 url2 url3` — opens tabs but can't restore groups, loses position in stow. |

**Recommended approach: Dedicated browser profiles.**

- Create a Chrome/Edge profile per project
- On deploy: `chrome.exe --profile-directory="Profile-MyApp" --new-window`
- On stow: The profile's session is auto-saved by Chrome on close
- On re-deploy: Chrome restores the profile's last session automatically
- Tab groups are preserved within the profile

### 5.4 File Explorer

- **Deploy:** `explorer.exe "C:\path"` opens a new window at that location
- **Path normalization (PoC finding):** `explorer.exe` silently opens the wrong folder (typically "Documents") when given forward-slash paths like `C:/dev/foo`. All paths must be normalized to backslashes before passing to explorer. The YAML config may use forward slashes for readability, but the deploy layer must convert. See `lessons-learned-from-poc.md` §5.
- **Window title:** Explorer window titles follow the pattern `"<FolderName> - File Explorer"`. The `-match` operator does substring matching, so matching on just the folder name works.
- **Position:** Must wait for window to fully render before calling `SetWindowPos`
- **Stow:** Close by window handle; no state to save (paths are in the config)

### 5.5 Microsoft Office (Word, Excel, PowerPoint)

- **Deploy:** `Start-Process "C:\path\to\file.docx"` — opens in the associated application
- **Multi-instance:** Excel runs in a single instance by default; multiple workbooks share one window. Word opens separate windows per document.
- **Position:** Locate window by document name in title bar
- **Stow:** Save & close via COM automation (`$word.Documents | ForEach { $_.Save(); $_.Close() }`) or just close the window and rely on auto-recover

### 5.6 OneNote

- **Deploy:** `onenote:///notebook/section` protocol handler, or `Start-Process "onenote:..."` 
- **Stow:** OneNote auto-saves; just close the window

---

## 6. Monitor Profiles

Since many developers use different monitor arrangements (home vs. office, docked vs. undocked), the orchestrator should support **named monitor profiles** with fallback behavior.

```yaml
# ~/.workspaces/monitors.yaml

profiles:
  home-office:
    monitors:
      - index: 0
        resolution: "3840x2160"
        position: { x: 0, y: 0 }
        description: "Main 4K"
      - index: 1
        resolution: "2560x1440"
        position: { x: 3840, y: 0 }
        description: "Side QHD"

  laptop-only:
    monitors:
      - index: 0
        resolution: "1920x1080"
        position: { x: 0, y: 0 }
        description: "Built-in"

  office-docked:
    monitors:
      - index: 0
        resolution: "2560x1440"
        position: { x: 0, y: 0 }
      - index: 1
        resolution: "2560x1440"
        position: { x: 2560, y: 0 }
      - index: 2
        resolution: "1920x1080"
        position: { x: -1920, y: 0 }
```

**Behavior:**

- On deploy, detect current monitor arrangement
- If it matches a known profile → use that profile's window positions
- If not → fall back to "auto-tile" or a default single-monitor layout
- Project context files can define `window.position` per monitor profile:

```yaml
  vscode:
    window:
      home-office: { monitor: 0, position: { x: 0, y: 0, width: 1920, height: 1040 } }
      laptop-only: { monitor: 0, position: { x: 0, y: 0, width: 1920, height: 1080 } }
      office-docked: { monitor: 1, position: { x: 0, y: 0, width: 2560, height: 1400 } }
```

---

## 7. User Interface

### 7.1 System Tray

- Icon in system tray showing currently deployed project (or "none")
- Right-click menu:
  - List of all project contexts (deploy on click)
  - "Stow Current" option
  - "Edit <project> config" → opens YAML in editor
  - "Scan & Create" → wizard to snapshot current desktop into a new context file
  - "Settings"

### 7.2 CLI

```
workspace deploy myapp            # deploy project
workspace stow                    # stow current project  
workspace switch myapp            # atomic stow + deploy
workspace list                    # list all projects
workspace status                  # show current state
workspace snapshot myapp          # save current window state as new context
workspace edit myapp              # open context file in editor
workspace validate myapp          # check context file for errors
```

### 7.3 Global Hotkeys

- `Ctrl+Alt+<N>` — quick-switch to project N
- `Ctrl+Alt+0` — stow all / show clean desktop
- `Ctrl+Alt+Backtick` — toggle between last two projects
- All customizable in a global config file

---

## 8. State Management

### 8.1 Persistent State

```
~/.workspaces/
├── monitors.yaml                     # monitor profile definitions
├── settings.yaml                     # global settings, hotkeys
├── myapp.workspace.yaml              # project context file
├── other-project.workspace.yaml
└── .state/
    ├── active-project.txt            # currently deployed project name
    ├── myapp.snapshot.yaml           # last stow snapshot
    └── other-project.snapshot.yaml
```

### 8.2 Snapshot File Format

```yaml
# Auto-generated by stow operation
timestamp: "2026-03-10T14:30:00"
monitor_profile: "home-office"

windows:
  - app: "vscode"
    pid: 12345
    title: "myapp.code-workspace - Visual Studio Code"
    hwnd: 0x001A0B2C
    desktop: 2
    rect: { x: 0, y: 0, width: 1920, height: 1040 }
    monitor: 0

  - app: "windows-terminal"
    pid: 67890
    title: "Windows Terminal"
    hwnd: 0x003C0D4E
    desktop: 2
    rect: { x: 0, y: 0, width: 960, height: 1040 }
    monitor: 1
    tabs:
      - title: "Backend"
        cwd: "C:/dev/myapp/backend"
        foreground_process: "node"
      - title: "Frontend"
        cwd: "C:/dev/myapp/frontend"
        foreground_process: "node"
      - title: "Git"
        cwd: "C:/dev/myapp"
        foreground_process: "pwsh"

  - app: "chrome"
    pid: 11111
    profile: "Profile-MyApp"
    desktop: 2
    rect: { x: 960, y: 0, width: 960, height: 520 }
    monitor: 0
    tabs:
      - url: "http://localhost:3000"
        title: "MyApp - Home"
      - url: "http://localhost:3000/admin"
        title: "MyApp - Admin"
      # ... (auto-captured from browser)
```

---

## 9. Implementation Technology Options

| Approach | Pros | Cons |
|----------|------|------|
| **PowerShell + Win32 API (P/Invoke)** | Native Windows, no dependencies, scriptable | Verbose, limited GUI, P/Invoke is tedious |
| **C# / .NET WPF** | Full Win32 API access, proper system tray, strong typing | Heavier development, compilation step |
| **Rust + windows-rs** | Fast, safe, single binary, good Win32 bindings | Steeper learning curve |
| **Python + pywin32 + ctypes** | Rapid prototyping, easy to iterate | Requires Python runtime, slower |
| **AutoHotkey v2** | Built for window manipulation, hotkeys are native | Limited for complex orchestration, quirky language |
| **Hybrid: AHK for hotkeys/window mgmt + PowerShell for orchestration** | Best of both worlds | Two languages, IPC overhead |
| **Electron/Tauri app** | Nice GUI, cross-platform potential | Heavy for a utility, overkill |

**Phase 1 (completed):** PowerShell for prototyping — validated P/Invoke feasibility, WT CLI integration, and virtual desktop management.

**Phase 2+ (decided at Gate 1):** C# .NET — single language, full Win32 access, proper system tray integration, async support, strong typing, can package as MSIX. PowerShell PoC scripts retained as reference.

---

## 10. Key Win32 APIs Required

| API | Purpose |
|-----|---------|
| `EnumWindows` / `FindWindow` | Discover windows by class/title |
| `GetWindowThreadProcessId` | Map window handle to PID |
| `SetWindowPos` | Move, resize, set Z-order |
| `ShowWindow` | Minimize, maximize, restore |
| `MoveWindow` | Alternative to SetWindowPos |
| `IVirtualDesktopManager` | COM interface for virtual desktop operations |
| `IVirtualDesktopManagerInternal` | Undocumented — create/remove/switch desktops |
| `SetForegroundWindow` | Bring window to front |
| `SendInput` / `SendMessage` | Send keystrokes to terminal tabs |
| `EnumDisplayMonitors` | Detect monitor arrangement |
| `GetMonitorInfo` | Get monitor geometry |
| `Shell_NotifyIcon` | System tray integration |

**Note on virtual desktop APIs:** Microsoft's official `IVirtualDesktopManager` only supports checking/moving windows. Creating, deleting, and switching desktops requires the *undocumented* `IVirtualDesktopManagerInternal` COM interface, which changes between Windows builds. Libraries like [MScholtes/VirtualDesktop](https://github.com/MScholtes/VirtualDesktop) wrap this.

---

## 11. Edge Cases & Challenges

### 11.1 Single-Instance / Multi-Window Applications (Confirmed in PoC)
- **Windows Terminal** shares a single process across all windows — `Stop-Process` kills everything. Multiple WT windows are indistinguishable by process name. This is the most critical challenge discovered in the PoC.
- **Chrome/Edge** — same single-process, multi-window architecture
- **Explorer** — same single-process architecture
- **Excel** shares one process for all workbooks — can't position per-workbook easily
- **OneNote** is single-instance — must navigate to the right notebook/section, not just launch
- **Outlook** — similar constraints

**PoC conclusion:** The orchestrator must track and operate on window handles (HWNDs), never process names or PIDs, as the primary identifier throughout the deploy/stow lifecycle. See `lessons-learned-from-poc.md` §1, §2.

### 11.2 Shared Resources
- A browser window might be "shared" between projects (e.g., email). The orchestrator needs a concept of **pinned/global windows** that survive stow operations.

### 11.3 Timing & Race Conditions
- Applications take variable time to launch and render
- Window positioning must wait until the window is fully created
- Terminal tabs take time to initialize before commands can be sent
- Strategy: **poll with exponential backoff**, max timeout per app
- **PoC finding:** Window positioning timing was reliable in practice with modest delays. No flakiness observed.

### 11.4 Multi-Project Deploy (Confirmed in PoC)
- User may want 2+ projects deployed simultaneously on different virtual desktops
- Stow should support stowing just one project while others remain active
- **PoC finding:** This scenario exposed the terminal window identity problem — stowing one project closed the wrong terminal window. This is the primary blocker for multi-project support. See `design-note-WT-tracking.md`.

### 11.5 Dirty State
- User opens extra tabs/windows not in the config — stow should snapshot these too
- User rearranges windows manually — snapshot should update positions

### 11.6 Crash Recovery
- If the orchestrator crashes mid-deploy, some apps are running without proper positioning
- Solution: write a deploy journal; on next launch, check for incomplete deploys and offer to clean up or continue

### 11.7 Confirmation Dialogs (Discovered in PoC)
- Many applications present confirmation dialogs on close (e.g., WT's "close all tabs?" dialog), which intercept `WM_CLOSE` and leave the window alive
- Automated close operations must verify the window actually closed and have a fallback strategy
- Per-application close behavior should be configurable. See §4.2 close escalation ladder.

### 11.8 Path Format Sensitivity (Discovered in PoC)
- `explorer.exe` silently opens the wrong folder when given forward-slash paths
- The YAML config permits forward slashes for readability, but the deploy layer must normalize all paths to backslashes before passing to native Windows executables
- Other apps (VS Code, WT) tolerate forward slashes. See `lessons-learned-from-poc.md` §5.

---

## 12. Stretch Goals / Future Features

- **"Scan & Create" wizard** — arrange your desktop how you like it, run a command, and the orchestrator reverse-engineers a context file from the current window layout
- **Project templates** — "web-fullstack", "data-science", "writing" base templates to fork
- **Time tracking integration** — log time per project context automatically
- **Focus mode** — block distracting apps/sites while a project is deployed
- **Cloud sync** — sync context files via OneDrive/Dropbox for use across machines
- **GlazeWM integration** — use GlazeWM workspaces instead of Windows virtual desktops for more powerful tiling
- **WSL integration** — launch WSL terminal sessions with correct distro and directory
- **Docker/container orchestration** — start/stop project Docker Compose stacks as part of deploy/stow
- **Notification grouping** — route Slack/Teams notifications to the correct virtual desktop based on project channels

---

## 13. Open Questions

1. **Should stow save dynamic state (new tabs, moved windows) back to the context file, or to a separate snapshot?** — Proposed: separate snapshot, context file is the "canonical" layout. PoC used config-only (no snapshots). Snapshot support is planned for Phase 2 (P2.3) but at lower priority.

2. **How to handle browser tabs that the user opens during a session that weren't in the config?** — Proposed: snapshot captures all tabs; re-deploy uses snapshot if available, falls back to config.

3. **Should the orchestrator manage virtual desktop names?** — Windows 11 supports renaming desktops; naming them after projects would be helpful. PoC used numbered desktops only.

4. **Per-project environment variables?** — Some projects need specific env vars (API keys, ports). Should these be in the context file or delegated to `.env` files?

5. **Integration with existing tools?** — Should this wrap SmartWindows/GlazeWM, or be standalone? Wrapping adds dependency risk but reduces development effort.

6. **(New, from PoC) Which terminal emulator should the orchestrator target?** — Windows Terminal has proven challenging for window identity and stow targeting in multi-project scenarios. An investigation of alternatives (WezTerm, Alacritty, others) is planned before Phase 2. The orchestrator may need to support multiple terminal emulators. See `workspace-orchestrator-plan.md` (Investigation: Terminal Emulator Selection).

7. **(New, from PoC) Should the deploy service maintain a manifest of launched HWNDs?** — The PoC established that window handles are the only reliable way to target specific windows for positioning and closing. A deploy manifest/journal that records HWNDs at launch time would enable reliable stow, partial-deploy recovery, and post-deploy validation. This is closely related to the deploy journal concept in §11.6 and approach #4 in `design-note-WT-tracking.md`.

---

## 14. Revision History

| Date | Version | Changes |
|------|---------|---------|
| 2026-03-10 | 1.0 | Initial draft |
| 2026-03-22 | 1.1 | Post-Phase 1 PoC updates: added terminal emulator abstraction (§2, §5.2), window discovery via HWND delta (§3.3), close escalation ladder (§4.2), Explorer path normalization (§5.4), confirmed technology decision (§9), new edge cases from PoC (§11.7, §11.8), promoted single-process/multi-window from edge case to confirmed challenge (§11.1, §11.4), added open questions §13.6 and §13.7 |
