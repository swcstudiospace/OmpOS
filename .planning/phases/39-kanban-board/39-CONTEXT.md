# Phase 39 Context: Kanban Board

Scope: ROADMAP.md Phase 39 success criterion 4 (column names) and the status vocabulary the other three criteria rely on. Requirement: VX-04.
Depends on: Phases 36 (complete and verified) and 37.
Date recorded: 2026-10-26. This file records decisions and defaults only. No Phase 39 plan or code exists yet.

Update 2026-10-26, after the build: the board, its views, the tabs and the head sources now exist. The sentence above ("No Phase 39 plan or code exists yet") and the "Status of the criteria" table below are the snapshot before the build and are kept as written. What was built, the criteria status and the real closing rule (OQ-79) are in `39-01-PLAN.md`, `39-SUMMARY.md` and `39-VERIFICATION.md`.

## Default column names (criterion 4)

Until the owner confirms the final names, the board uses these defaults. They are CONTEXT defaults, not owner decisions. Changing a name later changes this table, the view label and nothing else: the status enumeration in code does not change.

| Order | Column name | Kind | Source of cards | Accepts drag |
| --- | --- | --- | --- | --- |
| 1 | Backlog | user-settable status | the user places a card here | yes |
| 2 | Queued | runtime status `RuntimeStatus.Queued` | runtime signal `RunQueued` | no |
| 3 | Running | runtime status `RuntimeStatus.Running` | runtime signal `RunStarted` | no |
| 4 | Awaiting input | runtime status `RuntimeStatus.AwaitingInput` | runtime signal `InputRequested` | no |
| 5 | Needs attention | runtime status `RuntimeStatus.NeedsAttention` | runtime signal `ApprovalRequested` | no |
| 6 | Paused | runtime status `RuntimeStatus.Paused` | runtime signal `RunPaused` | no |
| 7 | Completed | runtime status `RuntimeStatus.Completed` | runtime signal `RunCompleted` | no |
| 8 | Failed | runtime status `RuntimeStatus.Failed` | runtime signal `RunFailed` | no |
| 9 | Unreachable | runtime status `RuntimeStatus.Unreachable` | runtime signal `SessionUnreachable` | no |
| 10 | Archived | user-settable status | the user places a card here | yes |

Provenance of each default:

- Backlog and Archived as the user-settable defaults: ROADMAP.md Phase 39 criterion 2 ("default Backlog and Archived").
- The eight runtime status names and the signal that sets each: `src/Autonogrammer.Presentation/Kanban/RuntimeStatus.cs`, `RuntimeStatusMap.cs` and the mapping table in `.planning/phases/36-contracts-theme-foundation/36-CONTEXT.md` (D36-6).
- The spaced labels "Awaiting input" and "Needs attention" are the enum names `AwaitingInput` and `NeedsAttention` written as words. The column order is a default of this record. Neither is stated in the ROADMAP or the requirements.

Stale is not a column and not a status. A card with a stale heartbeat keeps its status and shows the stale indicator with a screen-reader label (criterion 3; D36-7 in 36-CONTEXT.md: stale after 45 seconds, three missed beats of 15 seconds).

## Decisions in force

| ID | Decision | Source |
| --- | --- | --- |
| D39-1 | Statuses come only from runtime signals through `RuntimeStatusMap.StatusOf`. The board never sets a runtime status. | VX-04; D36-6 |
| D39-2 | Runtime status cards refuse drag. Only the user-settable statuses accept drag. | ROADMAP criterion 2 |
| D39-3 | A card shows title, active model and provider, last activity time and status. | ROADMAP criterion 1 |
| D39-4 | When the Agent Panel session catalogue exists (Phase 46, `SessionCatalogModel`), the board builds its cards from it instead of a second source. | Phase 46 context; roadmap scout 2026-10-26 |
| D39-5 | The Kanban tab exists and is reachable from the application navigation. It is the only product addition besides provider settings (DR-3, DR-6). It hosts the board; the default Agent Panel gains no inspector (DR-1), so the task context and the steering history that Phase 46 first placed in an inspector are shown on the card. | User direction 2026-10-26 (DR-1, DR-3); requirements AL-17 and AL-18 |
| D39-6 | Each opened session, whether an Agent Panel chat or an omp terminal, appears as exactly one card. Opening a session creates its card, a second open of the same session creates no second card, and selecting a card opens or focuses its session. Card status mirrors the session lifecycle status through the columns above (DR-5a, default; [OQ-78](../../../docs/open-questions.md)), and updates live without a refresh. A terminal session must reach the same catalogue as a chat (D39-4); how it enters it is a Phase 48 implementation point. | G6; DR-3, DR-5a |
| D39-7 | Closing or ending a session moves its card to its terminal column: Completed, Failed or Archived. A card is never removed silently (DR-5b, default; [OQ-79](../../../docs/open-questions.md)). A terminal that dropped is shown as disconnected, which is distinct from Failed; its default home is the Unreachable column. | G6; DR-5b |
| D39-8 | A card shows title, model and provider, last activity, status and the stale indicator. When Substrate is reachable it also shows task, graph, lessons count and lease holder. Without Substrate those four show as unavailable and are never invented. Lesson and note bodies stay untrusted plain text, capped, and the card shows only their count (ADR-0012 D9). The card also carries the task context and the steering history that DR-1 moved out of the Phase 46 inspector. States: loading skeleton, empty with a way to start a session, error with retry, populated, and disconnected. | G6; DR-1; ADR-0012 D9 |
| D39-9 | A running session can be steered from its card. The card reuses the existing Steer intent (`RunInputScheduler`, `PromptOptions.Steer`) and the delivery of Phase 49. Steering a session that ended, disconnected or never started fails with a clear message and leaves the session state unchanged. Substrate records the action; delivery is never attributed to Substrate. The delivery mechanism is open: [OQ-81](../../../docs/open-questions.md). | G7; DR-4 |

## Status of the criteria on 2026-10-26

| # | Criterion | Status |
| --- | --- | --- |
| 1 | One card per chat with title, model, provider, last activity and runtime status | PARTIAL: the enumeration, the mapping and `KanbanCardState` exist and are tested (`RuntimeStatusMapTests`, `KanbanCardStateTests`). No board model, no card record with title, model, provider and last activity, and no feed from session events exist. |
| 2 | Runtime statuses not draggable; only user-settable statuses accept drag | NOT STARTED: no user-settable status type and no drag model exist. |
| 3 | Stale indicator with a screen-reader label; unreachable shows the error state | PARTIAL: `KanbanCardState.IsStale`, `WithStale` and `RuntimeStatus.Unreachable` exist. No view, no label and no UI test exist. |
| 4 | Column names are the defaults recorded in CONTEXT until the owner confirms | This file records them. The owner confirmation is open: OQ-77. |

Update 2026-10-26: the status text of rows 1 and 2 predates the untracked Presentation files `KanbanBoardModel.cs`, `KanbanCard.cs`, `KanbanColumn.cs`, `UserSettableStatus.cs` and `KanbanMoveOutcome.cs` (under `src/Autonogrammer.Presentation/Kanban/`) and `tests/Autonogrammer.Presentation.Tests/Kanban/KanbanBoardModelTests.cs`. Those files are in the working tree; they have not been run or reviewed by this task, so rows 1 and 2 are left as written until a run records their status.

Update 2026-10-26 (criteria added by the Kanban tab direction, DR-3 and DR-5; the numbering follows this table, and the rewritten Phase 39 in `.planning/ROADMAP.md` carries its own). Status words below describe the repository on that date and were not verified by a run of this documentation task.

| # | Criterion | Status |
| --- | --- | --- |
| G6-1 | The Kanban tab exists and is reachable from the application navigation (D39-5) | NOT STARTED: no Kanban view, tab or navigation entry exists under `src/Autonogrammer.Native.Views`. |
| G6-2 | Each opened session, chat or omp terminal, is exactly one card; opening creates it; closing or ending moves it to its terminal column (D39-6, D39-7) | NOT STARTED for terminals. `src/Autonogrammer.Presentation/Kanban/KanbanBoardModel.cs` builds cards from `SessionCatalogModel`; the file and its tests are untracked in the working tree and unrun here. Terminal sessions and the terminal-column move are not covered. |
| G6-3 | Status mirrors the lifecycle status, selecting a card opens or focuses its session, updates are live (D39-6) | PARTIAL at most: `KanbanBoardModel.Apply` and `KanbanCard` exist as untracked files; no session-focus command and no view exist. |
| G6-4 | A card shows title, model, provider, last activity, status, stale indicator, and with Substrate task, graph, lessons count, lease holder; unavailable otherwise (D39-8) | PARTIAL at most: `KanbanCard` has the first set (title, model, provider, last activity, state, stale). No Substrate fields exist on it. |
| G6-5 | States: loading skeleton, empty with start-a-session, error with retry, populated, disconnected distinct from failed (D39-8) | NOT STARTED: no view exists. |
| G6-6 | Keyboard navigation with visible focus, accessible labels, status never by colour alone, live updates honour reduced motion; only Backlog and Archived accept drag | PARTIAL at most: `KanbanBoardModel.CanDrop` and `UserSettableStatus` exist (untracked); `KanbanCard.AccessibleName` and `StaleLabel` exist; no view, so no keyboard or reduced-motion test exists. |
| G6-7 | Steering from the card (D39-9, Phase 49) | NOT STARTED: depends on the delivery mechanism, OQ-81. |

## Owner decision (non-blocking)

Final column names. The defaults above stand until the owner confirms or changes them. Route: `docs/open-questions.md`, OQ-77.

Update 2026-10-26: three product questions have defaults under DR-5, all non-blocking and changeable by the user: the column mapping ([OQ-78](../../../docs/open-questions.md), which links OQ-77), closing a session ([OQ-79](../../../docs/open-questions.md)) and plugin enable or disable controls ([OQ-80](../../../docs/open-questions.md); the board has none). The live-steer delivery mechanism is [OQ-81](../../../docs/open-questions.md).

## Known limits

- Hosting the board in the native shell edits the Agent Panel files (`MainView`, `ViewMapping`), which Phase 46 rewrites. The board model and view code are separate files; the hosting waits for Phase 46.
- `Harness.Herdr` has no emitter of `RuntimeSignalKind`. A real feed from herdr is also blocked on the owner's restart-semantics decision (OQ-76).
- Update 2026-10-26: the Agent Panel has no inspector (DR-1), so the hosting edit above touches rail, thread and composer files only; the board's task context and steering history replace the former Phase 46 inspector tabs.
