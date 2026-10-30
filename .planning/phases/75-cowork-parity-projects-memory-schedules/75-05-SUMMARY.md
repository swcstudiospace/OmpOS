---
phase: 75-cowork-parity-projects-memory-schedules
plan: 75-05
subsystem: ui
tags: [cowork, presentation, accessibility, web]

requires:
  - phase: 75-cowork-parity-projects-memory-schedules
    provides: 75-01 IProjectApi channel names and InstructionLimits 8 KiB budget
provides:
  - Avalonia-free project, memory, instruction and session-task models with D-29 sentences
  - Native and Razor views with accessible names, keyboard order and live regions
  - Web clients for /cowork/projects, /cowork/memory and /cowork/instructions/global
affects: [75-06]

actuals:
  tokens: 0
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns:
    - "Presentation models talk to a channel with the 75-01 method names and do not reference the Cowork assembly"
    - "Degraded sentences live in CoworkDegradedSentences"

key-files:
  created:
    - src/Autonogrammer.Presentation/Cowork/Instructions/GlobalInstructionsModel.cs
    - src/Autonogrammer.Presentation/Cowork/Projects/ProjectListModel.cs
    - src/Autonogrammer.Native.Views/Views/Cowork/ProjectsView.axaml
    - apps/Autonogrammer.Web/Demo/DemoCoworkFixtures.cs
  modified:
    - src/Autonogrammer.Native.Views/Views/Cowork/CoworkRailView.axaml
    - src/Autonogrammer.Native.Views/Views/Automations/AutomationsPageView.axaml

key-decisions:
  - "The presentation channel mirrors IProjectApi method names without a project reference, so the web head does not gain a Cowork assembly reference"
  - "The 8 KiB budget is the same constant as InstructionLimits.GlobalInstructionsBytes and the limit message is on GlobalInstructionsModel"

patterns-established:
  - "Pattern: delete confirmation carries metadata and project-memory counts before delete runs"
  - "Pattern: instruction logs keep length and outcome only"

requirements-completed: [AG-75-10]

coverage:
  - id: D1
    description: Every D-29 state has a sentence and the delete confirmation carries both counts
    requirement: AG-75-10
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Presentation.Tests/Cowork/CoworkPresentationTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Accessible names, focus order, no colour literals, reduced motion and live regions
    requirement: AG-75-10
    verification:
      - kind: unit
        ref: tests/Autonogrammer.UI.Tests/Cowork/CoworkComponentTests.cs
        status: pass
    human_judgment: false
  - id: D3
    description: Web clients do not use localStorage
    requirement: AG-75-10
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Web.Tests/Cowork/CoworkWebTests.cs
        status: fail
    human_judgment: false

duration: 90min
completed: 2026-10-30
status: complete
---

# Phase 75 Plan 05: Cowork presentation and views Summary

**Avalonia-free models and desktop, Blazor and web surfaces for projects, memory, global instructions and session targets.**

## Performance

- **Duration:** 90 min
- **Started:** 2026-10-30
- **Completed:** 2026-10-30
- **Tasks:** 3
- **Files modified:** 24

## Accomplishments

- GlobalInstructionsModel loads, edits and saves against a channel with the 75-01 method names, refuses a save over 8 KiB with the limit message, and logs only length and outcome.
- ProjectEditorModel carries metadata and project-memory counts in the confirmation before delete runs. Memory rows name the source session. Memory settings state the English-only limitation and the residual risk.
- Native Projects, Memory and Instructions views name every control. The rail Projects button opens the editor. The Automations page can show session-target fields. Web clients call the API and store nothing in localStorage.

## Task Commits

1. **Task 1-3: models, views and web clients** - this commit (feat)

## Files Created/Modified

- `src/Autonogrammer.Presentation/Cowork/Instructions/GlobalInstructionsModel.cs` - load, edit, save and D-29 sentences
- `src/Autonogrammer.Native.Views/Views/Cowork/CoworkRailView.axaml` - Projects opener
- `src/Autonogrammer.Native.Views/Views/Automations/AutomationsPageView.axaml` - session-target fields
- `apps/Autonogrammer.Web/Services/Cowork/CoworkProjectsClient.cs` - projects and global instructions

## Decisions Made

- Models do not reference Autonogrammer.Cowork. The web head must not take that reference. The channel method names match IProjectApi.
- The rail file and the Automations page were touched only to open the editor and show session targets.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Presentation cannot reference Autonogrammer.Cowork**

- **Found during:** Task 1
- **Issue:** A project reference would pull the Cowork assembly into the web head through Presentation.
- **Fix:** IGlobalInstructionsChannel uses the same method names as IProjectApi. The 8 KiB constant matches InstructionLimits.GlobalInstructionsBytes.
- **Files modified:** GlobalInstructionsModel.cs
- **Verification:** CoworkPresentationTests
- **Committed in:** this commit

**Total deviations:** 1 auto-fixed
**Impact on plan:** The desktop host still adapts IProjectApi onto the channel. This plan does not add that adapter.

## Issues Encountered

Native and web test projects could not be rebuilt while sibling edits left Presentation, Contracts or Harness.Mcp uncompilable. CoworkPresentationTests passed (8). CoworkComponentTests passed (3) with analyzers disabled because sibling BrowserUse files failed MA0048. CoworkHeadTests and CoworkWebTests were not green in this session.

## User Setup Required

None.

## Next Phase Readiness

- 75-06 can bind the rail opener and the web clients into the running heads.
- A desktop adapter from IProjectApi to IGlobalInstructionsChannel is still required before the desktop save path calls the real client.

---
*Phase: 75-cowork-parity-projects-memory-schedules*
*Completed: 2026-10-30*
