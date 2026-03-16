# Workspace Orchestrator

One-click deploy and stow of entire project contexts on Windows 11 — VS Code, terminals, browsers, file explorers, Office docs — each positioned on the right virtual desktop with the right layout.

## Status: Phase 1 — Proof of Concept

Currently validating core technical capabilities:
- [x] Project scaffolded
- [ ] P1.1 — Window discovery & positioning (Win32 API)
- [ ] P1.2 — Virtual desktop management (COM interop)
- [ ] P1.3 — Windows Terminal multi-tab launch
- [ ] P1.4 — Basic deploy/stow cycle

## Quick Start

```powershell
# Run the bootstrap script (installs deps, creates project structure)
pwsh -File bootstrap.ps1

# Edit the sample config with your real paths
code workspace-orchestrator/sample-project.workspace.yaml

# Run the first test
cd workspace-orchestrator
.\test\Test-WindowPositioning.ps1

# Try a deploy
.\deploy.ps1 -Project sample-project

# Stow it
.\stow.ps1 -Project sample-project
```

## Docs

- [Design Spec](docs/spec.md)
- [Development Plan](docs/plan.md)
- [Phase 1 Setup Guide](docs/phase1-setup.md)
