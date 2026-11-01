---
phase: 76-cowork-parity-plugins-auto-isolation
plan: 05
subsystem: ui
tags: [avalonia, acp, subagents, accessibility]

requires:
  - phase: 70
    provides: ToolCallEvent and ToolCallUpdateEvent on the ACP harness stream
provides:
  - SubagentPanelModel over ACP task-call updates with elapsed time, running count and plain text
  - AcpTaskToolActivitySource and SubagentsPanelView
affects: [76-06]

actuals:
  tokens: 9000
  tasks: 2
  commits: 1

tech-stack:
  added: []
  patterns:
    - Presentation activity updates stay free of harness event types
    - Agent output is plain text with no opening link

key-files:
  created:
    - src/Autonogrammer.Presentation/Subagents/SubagentPanelModel.cs
    - src/Autonogrammer.Native.Views/Sessions/AcpTaskToolActivitySource.cs
    - src/Autonogrammer.Native.Views/Views/Subagents/SubagentsPanelView.axaml
  modified: []

key-decisions:
  - "Task calls are recognised when the event name, title or kind is task; the agent name is read from rawInput agent, name, subagent, agentName or agent_name."
  - "Row updates are SubagentActivityUpdateEventArgs so the source event can be EventHandler<T>."

patterns-established:
  - "Unnamed task calls read Sub-agent N and set SubagentNamesUnavailable."
  - "The panel states Sub-agent view limited to ACP."

requirements-completed: [AG-76-11]

coverage:
  - id: D1
    description: Panel lists ACP task calls with status, elapsed time and running count
    requirement: AG-76-11
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Presentation.Tests/Subagents/SubagentPanelModelTests.cs#NamedFramesProduceRowsWithElapsedTime
        status: pass
    human_judgment: false
  - id: D2
    description: Unnamed rows read Sub-agent N and agent output stays plain text
    requirement: AG-76-11
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Presentation.Tests/Subagents/SubagentPanelModelTests.cs#AgentOutputStaysPlainText
        status: pass
    human_judgment: false
  - id: D3
    description: Activity source and accessible panel view
    requirement: AG-76-11
    verification:
      - kind: automated_ui
        ref: tests/Autonogrammer.Native.Tests/Subagents/SubagentsPanelViewTests.cs
        status: unknown
    human_judgment: true
    rationale: Native.Tests could not be run while sibling projects failed to compile.

duration: 40min
completed: 2026-10-30
status: complete
---

# Phase 76 Plan 05 Summary

**ACP task-call panel with status, elapsed time, running count, plain-text output and an ACP-only notice.**

## Performance

- **Duration:** 40 min
- **Started:** 2026-10-30
- **Completed:** 2026-10-30
- **Tasks:** 2
- **Files modified:** 16

## Accomplishments

- `SubagentPanelModel` aggregates task-call updates with injected `TimeProvider`, running count, `Sub-agent N` fallback and `SubagentNamesUnavailable`.
- `AcpTaskToolActivitySource` maps `ToolCallEvent` and `ToolCallUpdateEvent` and ignores other harness events.
- `SubagentsPanelView` shows rows, a polite live region and the notice "Sub-agent view limited to ACP." ViewMapping stays with 76-06.

## Files Created/Modified

- `src/Autonogrammer.Presentation/Subagents/SubagentPanelModel.cs` - row aggregation
- `src/Autonogrammer.Native.Views/Sessions/AcpTaskToolActivitySource.cs` - ACP consumer
- `src/Autonogrammer.Native.Views/Views/Subagents/SubagentsPanelView.axaml` - accessible panel
- `tests/Autonogrammer.Presentation.Tests/Subagents/RecordedTaskFrames.cs` - presentation scripts
- `tests/Autonogrammer.Native.Tests/Sessions/RecordedTaskEvents.cs` - recorded payloads

## Decisions Made

- Agent name keys are `agent`, `name`, `subagent`, `agentName` and `agent_name` because OQ-459 is unverified on this host.
- Updates are `SubagentActivityUpdateEventArgs` so the source can raise `EventHandler<T>`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Event args cannot be a record**
- **Found during:** Task 1
- **Issue:** A record cannot inherit `EventArgs`, and a custom delegate failed CA1003.
- **Fix:** `SubagentActivityUpdateEventArgs` is a class.
- **Files modified:** `src/Autonogrammer.Presentation/Subagents/SubagentActivityUpdateEventArgs.cs`
- **Verification:** Presentation panel tests passed.
- **Committed in:** the plan commit

**Total deviations:** 1 auto-fixed
**Impact on plan:** Naming only. No scope change.

## Issues Encountered

`Autonogrammer.Native.Tests` did not compile while sibling edits left `AcpStdioTransport._logger` unresolved and isolation analyzer errors in the graph. Presentation `SubagentPanelModelTests` passed (4).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

76-06 can register `SubagentsPanelView` and tee ACP events into `AcpTaskToolActivitySource.Observe`.

---
*Phase: 76-cowork-parity-plugins-auto-isolation*
*Completed: 2026-10-30*
