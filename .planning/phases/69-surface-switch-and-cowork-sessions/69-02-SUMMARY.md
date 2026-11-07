---
phase: 69-surface-switch-and-cowork-sessions
plan: 69-02
subsystem: presentation
tags: [cowork, surface-switch, dock, view-model]
requires:
  - phase: 69-01
    provides: CoworkSessionSummary, CoworkSessionStatus, ICoworkSessionCatalog
provides:
  - SurfaceSwitchModel Format/Parse and restore-only-when-available
  - SurfaceActivityModel hidden-surface counts and polite chip text
  - SurfaceChromeModel ordered header-end entries
  - CoworkShellModel rail, conversation, dock and panel registry
  - CoworkSessionListModel grouped, filtered, selected rows
affects: [69-03, 69-04, 71-05, 72-08, 74-06]
actuals:
  tokens: 8000
  tasks: 2
  commits: 1
tech-stack:
  added: []
  patterns:
    - SurfaceSwitchModel.Format/Parse is the only persisted code/cowork pair
    - SurfaceActivityModel reads Code rows through a Func and a change signal
    - CoworkShellModel.RegisterPanel is the dock seam
key-files:
  created:
    - src/Autonogrammer.Presentation/Cowork/SurfaceSwitchModel.cs
    - src/Autonogrammer.Presentation/Cowork/SurfaceActivityModel.cs
    - src/Autonogrammer.Presentation/Cowork/CoworkShellModel.cs
    - src/Autonogrammer.Presentation/Cowork/CoworkSessionListModel.cs
    - src/Autonogrammer.Presentation/Cowork/IDockPanel.cs
  modified: []
key-decisions:
  - "Hidden-surface chip prefers needs-approval, then running count, then failed count."
  - "One registered panel sets ShowTabStrip false; two or more set it true."
  - "Extra types are separate files so MA0048 holds."
patterns-established:
  - "Pattern: both heads bind one SurfaceSwitchModel and one SurfaceChromeModel."
  - "Pattern: IDockPanel has Id, Title and Content and no slot."
requirements-completed: [AG-69-4, AG-69-8, AG-69-12, AG-69-13]
coverage:
  - id: D1
    description: Stored surface restores only when Cowork is available; otherwise Code and the switch stays hidden.
    requirement: AG-69-4
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Presentation.Tests/Cowork/SurfaceSwitchModelTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Hidden-surface chip text is exactly "Cowork, 2 running" and "Code, needs approval", announced as polite status.
    requirement: AG-69-4
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Presentation.Tests/Cowork/SurfaceActivityModelTests.cs
        status: pass
    human_judgment: false
  - id: D3
    description: Rail groups by project, filters, selects one row and shows the 200-task sentence.
    requirement: AG-69-8
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Presentation.Tests/Cowork/CoworkSessionListModelTests.cs
        status: pass
    human_judgment: false
  - id: D4
    description: Shell owns rail, conversation and dock, dock open/close, and a panel registry with no strip for one panel.
    requirement: AG-69-13
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Presentation.Tests/Cowork/CoworkShellModelTests.cs
        status: pass
    human_judgment: false
duration: 40min
completed: 2026-10-30
status: complete
---

# Phase 69: Surface switch and Cowork sessions Summary

**Framework-free Cowork presentation models share one switch, activity chip, header chrome, rail and dock registry for both heads.**

## Performance

- **Duration:** 40 min
- **Started:** 2026-10-30T01:00:00Z
- **Completed:** 2026-10-30T01:40:00Z
- **Tasks:** 2
- **Files modified:** 16

## Accomplishments

- `SurfaceSwitchModel` selects Code or Cowork, hides the switch when Cowork is unavailable, and restores a stored `cowork` value only when available. `Format`/`Parse` own the strings `code` and `cowork`.
- `SurfaceActivityModel` counts Code rows (Running or Queued, NeedsApproval, Failed) and Cowork snapshots (Running, WaitingForDecision, Failed). The hidden chip is `Cowork, 2 running` or `Code, needs approval`. The announcement repeats that status with live setting `Polite`.
- `SurfaceChromeModel` keeps ordered entries (id, label, optional shortcut, invoke). A second add of the same id is ignored.
- `CoworkSessionListModel` groups by project, filters title and project id, tracks one selection, and shows the 200-task sentence at 200 rows.
- `CoworkShellModel` owns rail, conversation and dock, dock open/close, and `RegisterPanel` / `UnregisterPanel` / `Panels` / `SelectedPanelId`. `ShowTabStrip` is false for one panel. `IDockPanel` has no slot.

## Task Commits

1. **Task 1-2: switch, activity, chrome, rail and dock models** - this commit (`feat(69-02)`)

## Files Created/Modified

- `src/Autonogrammer.Presentation/Cowork/SurfaceSwitchModel.cs` - selection, availability, Format/Parse, Restore
- `src/Autonogrammer.Presentation/Cowork/SurfaceActivityModel.cs` - per-surface counts and polite chip
- `src/Autonogrammer.Presentation/Cowork/SurfaceChromeModel.cs` - ordered header-end entries
- `src/Autonogrammer.Presentation/Cowork/SurfaceChromeEntry.cs` - one entry (MA0048)
- `src/Autonogrammer.Presentation/Cowork/CoworkCopy.cs` - shared user strings
- `src/Autonogrammer.Presentation/Cowork/CoworkShellModel.cs` - three parts, dock state, panel registry
- `src/Autonogrammer.Presentation/Cowork/CoworkShellPart.cs` - Rail, Conversation, Dock
- `src/Autonogrammer.Presentation/Cowork/CoworkSessionListModel.cs` - grouped filtered rail
- `src/Autonogrammer.Presentation/Cowork/CoworkSessionRow.cs` - plain-text summary mirror
- `src/Autonogrammer.Presentation/Cowork/CoworkSessionGroup.cs` - one project group
- `src/Autonogrammer.Presentation/Cowork/IDockPanel.cs` - content contract
- `tests/Autonogrammer.Presentation.Tests/Cowork/*` - switch, activity, list and shell tests

## Decisions Made

- Needs-approval wins the hidden chip over a running count, which wins over failed. Failed still has a `{surface}, {n} failed` chip when it is the only signal.
- Code rows are read through `Func<IReadOnlyList<AgentRow>>` plus subscribe/unsubscribe. The model does not name the Code catalog type.
- `ShowTabStrip` lives on the shell so both heads bind the same rule.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Split types so MA0048 passes**
- **Found during:** Task 1
- **Issue:** One file with two public types fails the repo analyzer.
- **Fix:** `SurfaceChromeEntry`, `CoworkShellPart` and `CoworkSessionGroup` are their own files under the owned Cowork folder.
- **Files modified:** those three files
- **Verification:** Release tests of the Cowork presentation classes passed
- **Committed in:** this commit

**Total deviations:** 1 auto-fixed
**Impact on plan:** Analyzer compliance only. No behaviour change.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- 69-03 and 69-04 bind `SurfaceSwitchModel.Format`/`Parse`, `SurfaceActivityModel` and `CoworkShellModel.RegisterPanel`.
- `CoworkSurfaceServices` is not created here. It should carry the one `CoworkShellModel`.
- Conversation transcript, approval queue and provider chips stay in phase 70.

---
*Phase: 69-surface-switch-and-cowork-sessions*
*Completed: 2026-10-30*
