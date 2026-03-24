Notes for human developers

- To start a new Claude Code session on the proof-of-concept:

Read poc-status.md and workspace-orchestrator-plan.md to get context, then let's work on X


- Full notes from Claude Code on resume
``` claude-code
in the future, when I start a new claude code session to continue development on this project, what file(s) should I ask claude to read to
get context?

● For the most efficient context handoff, in priority order:

  1. poc-status.md — start here every time. It has the current state, test results, known limitations, and next steps. I'll keep this updated.
  2. workspace-orchestrator-plan.md — only needed if discussing phase/architecture decisions (Gate 1, Phase 2 planning, etc.).
  3. workspace-orchestrator-spec.md — only needed if you're asking about feature requirements or debating what something should do.
  4. The specific lib/*.ps1 file(s) you're about to change — always read before editing.

  So a typical session opener would be:
  ▎ "Please read poc-status.md and workspace-orchestrator-plan.md — we're continuing development on the workspace orchestrator."

  And if you're jumping straight into a specific task:
  ▎ "Please read poc-status.md and lib/VirtualDesktop.ps1 — I want to add X."

  ---
  Let me save that note to my persistent memory so I remember this project's conventions:

● Recalled 1 memory, wrote 1 memory (ctrl+o to expand)

● Memory saved. To summarize the context strategy:

  Every new session, just say:
  ▎ "Read poc-status.md and workspace-orchestrator-plan.md to get context, then let's work on X."

  That's all that's needed. poc-status.md gives the current state and test results; the plan gives the phase structure. I'll also have my
  persistent memory loaded automatically, which has the key environment details and gotchas already captured — so you won't need to re-explain
  the tmp_out.txt workaround or any of the other quirks we discovered this session.

```

Past session-starting prompts:
2026-03-23 Investigation of WT challenges and alternative terminals.
```
 Read poc-status.md and workspace-orchestrator-plan.md to get context, then start on the "Investigation: Terminal Emulator Selection". Make a detailed plan
for the investigation, write it to a new markdown file, then execute as much as you can of the plan, documenting your findings and recommendations in a new
investigation results markdown file as you go. Note that the latest releases of wezterm and alacrity have been installed to the system but have not been
launched yet.
```

INVESTIGATION COMPLETE:
2026-03-23 23:20 Terminal Emulator Investigation DONE
- Windows Terminal's --window <name> feature solves the P2.1 blocker
- WT window name becomes the title, making it queryable via Win32 APIs
- Phase 2 is now unblocked and ready to start
- See terminal-investigation.md, phase-2-readiness.md, INVESTIGATION_SUMMARY.md

Done
2026-03-23 Start Phase 2 implementation
```
Read poc-status.md, workspace-orchestrator-plan.md, and phase-2-readiness.md to get context, then begin Phase 2: Minimum Viable Product implementation. Make a detailed plan for the phase, write it to a new markdown file, then execute as much as you can of the plan, documenting your findings and recommendations in a new mvp-status.md markdown file as you go.
```

Done:
2026-03-23 Start Phase 2 Sprint 2 "Polish + Browser"
```
Read workspace-orchestrator-plan.md, phase-2-plan.md, and mvp-status.md to get context, then begin Phase 2 Sprint 2 "Polish + Browser". Execute the tasks per the plan, documenting task status as well as any findings or recommendations in the mvp-status.md markdown file as you go.  At the end of sprint, commit your work to git.
```

Done:
2026-03-23 Start Phase 2 Sprint 3 "Polish + Browser"
```
Read workspace-orchestrator-plan.md, phase-2-plan.md, and mvp-status.md to get context, then begin Phase 2 Sprint 3 "Global Hotkeys". Execute the tasks per the plan, documenting task status as well as any findings or recommendations in the mvp-status.md markdown file as you go.  At the end of sprint, commit your work to git.
```


Done:
2026-03-23 Start Phase 2 Sprint 4 "Window State Snapshots"
```
Read workspace-orchestrator-plan.md, phase-2-plan.md, and mvp-status.md to get context, then begin Phase 2 Sprint 4 "Window State Snapshots". Execute the tasks per the plan, documenting task status as well as any findings or recommendations in the mvp-status.md markdown file as you go.  At the end of sprint, commit your work to git.
```
