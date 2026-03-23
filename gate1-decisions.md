# Gate 1 Decisions

The plan calls for a gate1-decisions.md document answering 5 questions before starting Phase 2

## 1. Timing reliability — How consistent is window positioning?

The windows are positioned consistently and reliably with no noted timing issues.

## 2. Virtual desktop stability — Reliable? Survived Windows updates?

To date, the virtual desktop approach has been stable. Windows updates have not occurred yet so it is unknown whether they affect stability, but it is not expected to cause issues based on past experience.

## 3. Terminal experience — Is the WT CLI approach sufficient?

The WT CLI approach to launching tabs and injecting commands is sufficient given our limited experience so far. The `wt new-tab --title X -d /path` and `-- pwsh -NoExit -Command "..."` mechanics work well for the launch side.

The broader CLI and manually-generated YAML file setup is not sufficient for a minimum viable product on its own. However, the CLI is useful and lends itself well to many automated or agent-driven workflows, so some form of CLI should be maintained going forward.

The primary terminal pain point is not launch mechanics but **tracking and stow** — reliably identifying which WT window belongs to which project. See question 5 and `design-note-WT-tracking.md`.

## 4. Language decision — Stay PowerShell or migrate to C#?

Migrate to C#. The PowerShell scripts are too limited for the ultimate goals of this project. C# is the language of choice for highly-integrated Windows utility development, with full Win32 API access, proper system tray support, strong typing, and good packaging options.

The existing PowerShell PoC scripts will be kept as reference and as helpful alternative methods until the C# tools fully replace them and they are no longer worth maintaining.

## 5. Scope refinement — What matters most for Phase 2?

Based on experience with the PoC, the most important capability for Phase 2 is **reliable deploy and stow operations in a complex many-application, many-desktop Windows 11 environment**. The WT tracking/stow issue detailed in `design-note-WT-tracking.md` needs to be addressed, either by finding more reliable methods to identify, track, and confirm Windows Terminal tabs/windows, or by switching to a different terminal emulator with a richer API.

### Phase 2 capability priorities

| Capability | Priority | Notes |
|---|---|---|
| P2.1 — Multi-Project Switching | **Critical** | Core feature; blocked by terminal identity problem |
| P2.2 — Browser Profile Integration | Wanted | Still in scope for Phase 2 |
| P2.3 — Window State Snapshots | Lower priority | Desired but may be pushed beyond Phase 2 if it holds up the MVP |
| P2.4 — CLI Interface | Wanted | Needed for daily use |
| P2.5 — Global Hotkeys | Wanted | Still in scope for Phase 2 |
| P2.6 — Robust Error Handling | Wanted | Needed for daily use |
| P2.7 — Office & OneNote | Wanted | Still relevant |

### Terminal emulator investigation

Given the challenges with WT management identified during the PoC, it is worthwhile to **investigate alternatives to Windows Terminal** before spending more time trying to solve the WT-specific tracking problems. Candidates to evaluate include WezTerm, Alacritty, and others that may offer better programmatic window identity and control. This investigation should happen early in Phase 2 before committing to a WT-only approach.
