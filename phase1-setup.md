# Phase 1 Development Environment Setup

**Project:** Workspace Orchestrator  
**Platform:** Windows 11 (native — not WSL2)  
**Phase:** 1 — Proof of Concept

---

## Why Windows-Native?

This project manipulates the Windows desktop: window handles, virtual desktops, COM objects, the Windows shell. None of this is accessible from WSL2, which runs in a Hyper-V VM with no visibility into the host desktop. All development and execution must happen in a Windows-native context.

---

## Prerequisites

### Already Installed (assumed)

- Windows 11 (22H2 or later)
- Windows Terminal
- VS Code
- Git for Windows
- Node.js (needed for Claude Code)

### Need to Install

| Tool | Install Command | Purpose |
|------|----------------|---------|
| **PowerShell 7+** | `winget install Microsoft.PowerShell` | Runtime — Phase 1 language |
| **Claude Code** | `npm install -g @anthropic-ai/claude-code` | AI-assisted development |
| **powershell-yaml** | `Install-Module powershell-yaml -Scope CurrentUser` | YAML config parsing |
| **PSScriptAnalyzer** | `Install-Module PSScriptAnalyzer -Scope CurrentUser` | Script linting |
| **VirtualDesktop** | `Install-Module VirtualDesktop -Scope CurrentUser` | Virtual desktop management (MScholtes) |

### Optional but Recommended

| Tool | Install Command | Purpose |
|------|----------------|---------|
| **.NET 8 SDK** | `winget install Microsoft.DotNet.SDK.8` | Ready for C# migration at Gate 1 |
| **Pester** | `Install-Module Pester -Scope CurrentUser -Force` | PowerShell test framework |
| **AutoHotkey v2** | `winget install AutoHotkey.AutoHotkey` | Potential hotkey integration |

---

## VS Code Configuration

### Recommended Extensions

- **PowerShell** (`ms-vscode.powershell`) — IntelliSense, debugging, integrated console
- **YAML** (`redhat.vscode-yaml`) — syntax highlighting for workspace configs
- **GitLens** (`eamodio.gitlens`) — useful but not critical

### Workspace Settings

Create `.vscode/settings.json` in the project root:

```json
{
  "powershell.cwd": "${workspaceFolder}",
  "powershell.powerShellDefaultVersion": "PowerShell (x64)",
  "files.associations": {
    "*.workspace.yaml": "yaml"
  },
  "editor.formatOnSave": false,
  "terminal.integrated.defaultProfile.windows": "PowerShell"
}
```

---

## Development Workflow

### Day-to-Day

1. Open VS Code on **virtual desktop 1** (your dev desktop)
2. Run Claude Code in a Windows Terminal tab alongside your editor
3. Test scripts that manipulate windows on **virtual desktop 2+** to avoid disrupting your own workspace
4. Use `pwsh` (PowerShell 7), not `powershell.exe` (5.1), for all execution

### Testing Pattern

Since this project manipulates real windows on your real desktop, testing is inherently interactive. The pattern is:

1. Run a test script (e.g., `Test-WindowPositioning.ps1`)
2. Observe: did Notepad appear at the expected position?
3. The script itself should also verify programmatically (query window rect, compare to expected)
4. Clean up: close any windows the test opened

Automated CI testing is impractical for Phase 1 — the scripts need a live desktop session.

### Debugging P/Invoke

When Win32 calls fail silently (common), use:

```powershell
# Get the last Win32 error after a P/Invoke call
[System.Runtime.InteropServices.Marshal]::GetLastWin32Error()

# Or use the .NET wrapper
$error = [System.ComponentModel.Win32Exception]::new([System.Runtime.InteropServices.Marshal]::GetLastWin32Error())
Write-Host "Win32 Error: $($error.Message)"
```

---

## Project Structure (Phase 1)

```
workspace-orchestrator/
├── .vscode/
│   └── settings.json
├── README.md
├── docs/
│   ├── spec.md                          # from earlier
│   ├── plan.md                          # from earlier
│   └── phase1-setup.md                  # this file
├── lib/
│   ├── Win32.ps1                        # P/Invoke wrappers (EnumWindows, SetWindowPos, etc.)
│   ├── VirtualDesktop.ps1               # Virtual desktop create/switch/move
│   ├── WindowManager.ps1                # High-level: find window, position, wait-for-window
│   └── TerminalLauncher.ps1             # Windows Terminal multi-tab launch + command injection
├── deploy.ps1                           # Main deploy entry point
├── stow.ps1                             # Main stow entry point
├── sample-project.workspace.yaml        # Example project context (hardcoded paths for your machine)
├── test/
│   ├── Test-WindowPositioning.ps1       # P1.1 validation
│   ├── Test-VirtualDesktop.ps1          # P1.2 validation
│   ├── Test-TerminalLaunch.ps1          # P1.3 validation
│   └── Test-DeployStow.ps1             # P1.4 integration test
└── bootstrap.ps1                        # Environment setup script
```

---

## Key Gotchas

### Execution Policy

PowerShell may block script execution by default. Set it once:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Admin vs. Non-Admin

Most window manipulation works without elevation. However:

- Moving windows that belong to elevated processes requires your script to also be elevated
- Virtual desktop COM interop may require elevation on some builds
- **Recommendation:** develop and test without elevation first; escalate only if specific operations fail

### Window Positioning Timing

The single biggest source of bugs will be trying to position a window before it's fully rendered. General rule: after launching a process, poll for its window handle with retries before attempting to move it. A 100ms poll interval with a 10-second timeout is a reasonable starting point.

### Virtual Desktop API Fragility

The `IVirtualDesktopManagerInternal` COM interface is undocumented and its GUIDs change between Windows builds. The MScholtes `VirtualDesktop` module tracks these changes, but it can break after a major Windows update. Always check for module updates after Windows updates. This is a known risk documented in the plan's risk register.

---

## Migration Readiness (C# at Gate 1)

If Phase 1 concludes with a decision to migrate to C#, the transition should be straightforward:

- .NET 8 SDK is already installed (if you followed the optional prereqs)
- P/Invoke declarations translate almost 1:1 from PowerShell `Add-Type` to C# `DllImport`
- The same VS Code + Claude Code workflow applies — just switch to C# files
- `dotnet new console -n WorkspaceOrchestrator` to scaffold
- `dotnet add package YamlDotNet` for YAML parsing
- The Phase 1 PowerShell code serves as working pseudocode for the C# rewrite
