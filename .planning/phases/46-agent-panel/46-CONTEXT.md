# Phase 46: Agent Panel - Context

> **Amended 2026-10-26 by DR-1** (user direction of 2026-10-26, recorded in `docs/decisions/ADR-0012-agent-loop-plugins.md`, section "Update 2026-10-26"). There is no additional Agent Panel: the default Cursor-style Agent Panel is the agent panel, and it has no inspector. The body below is the original specification and is not rewritten. Where the body describes a region, control or screenshot listed as Superseded in the table, the table governs. The rewritten Phase 46 goal and criteria live in `.planning/ROADMAP.md` and `.planning/REQUIREMENTS.md` (AL-09, AL-12).

**Gathered:** 2026-10-26
**Status:** Ready for planning
**Mode:** Autonomous. No user was available. The program brief and the Agent Loop design (milestone v0.14.0) are the spec. Every choice the sources left open is listed under Assumptions with its default.

## Amendments

| Region, control or item | Status | Reason | Where its content goes |
| --- | --- | --- | --- |
| Inspector region (third column, `InspectorHost`, `InspectorTabs`, 320 px, first-run open or collapsed rule) | Superseded | DR-1: no second agent surface. The default panel has rail, thread and composer only. | Nowhere as a region. Its tab content is placed as noted in the next three rows. |
| Pipeline tab (`Pipeline details`: stage blocks, rows, Effect lines, Retry stage, Run summary) | Superseded | DR-1. | The one-line plugin connection row keeps the per-plugin status and the fix text of an unavailable or degraded plugin (D8 copy). Stage-row detail has no home in the default panel; observability per plugin is verified in Phase 48. |
| Context tab (`Task context details`: task, Kanban status, phase, notes, lessons, warnings) | Superseded | DR-1. | The Kanban card (task context), Phase 39 decisions D39-5 to D39-9. |
| Steering history tab (`Steering history entries`) | Superseded | DR-1. | The Kanban card (steering history), Phase 39 and Phase 49. |
| Inspector toggle (`InspectorToggle`), `Ctrl+Shift+I`, `Ctrl+3`, the inspector step of `F6`, Left and Right and Home and End on the inspector tab header, the inspector focus rules, inspector open and close motion | Superseded | They exist only to operate the inspector. | Removed. `F6` cycles rail and thread only; `Ctrl+1` and `Ctrl+2` are unchanged. |
| Actions that open the inspector: task chip (opens the Context tab), pipeline strip button (opens the Pipeline tab), `Show pipeline` in the panel notice | Superseded (action only) | Their target no longer exists. The chip, the strip and the notice stay. | Whether the task chip navigates to the task's Kanban card is for Phase 39 and the rewritten Phase 46 to decide. |
| Layout measurements and responsive rules for the inspector (three-column grid, 1200 px threshold, `Autonogrammer.Size.InspectorWidth`) | Superseded | No third column. The 960 x 640 window minimum of assumption 1 stays. | Two-column layout, rail and thread. |
| Assumption 9 (Pipeline tab aggregate rows when Substrate emits no per-event rows) | Superseded | The Pipeline tab is gone. | The same question applies to the Kanban card's Substrate fields (Phase 39). |
| Open item "Where do users switch Ultrathink, Swarm and Substrate on?" | Superseded | DR-5(c): the four plugins are always on; no settings item is added. | `docs/open-questions.md`, OQ-80. |
| Success criterion 1 (default window shows rail, thread, pipeline strip, composer and inspector) and AL-09 acceptance (collapsible inspector) | Superseded | DR-1. | Rewritten Phase 46 criterion in `.planning/ROADMAP.md`: the application opens on rail, thread and composer, with no second agent surface and no inspector. |
| Success criterion 3 wording "appears in the steering history" | Superseded | The steering history is on the Kanban card. Live delivery is the requirement (DR-4). | Phase 39 and Phase 49; AL-07 is reworded in `.planning/REQUIREMENTS.md`. |
| Success criterion 6 and AL-12: eight screenshots | Superseded (the inspector ones) | Screenshots 1, 2, 3, 4 and 6 of `46-UI-SPEC.md` show the inspector or the Pipeline tab. | Re-recorded without the inspector. Screenshots 5, 7 and 8 are kept as specified. |
| Session rail (the Agents sidebar), including rows, groups, filter, New agent and the Tools view | Kept | It is Cursor's Agents sidebar. | Unchanged. |
| Thread: header, transcript, banners, notices, confirmation bars | Kept | Part of the default panel. | Unchanged. |
| Composer with Send, Steer, Follow-up and Stop (Cursor-style queue and send now) | Kept | Part of the default panel. Live steering (Phase 49) changes delivery, not the controls. | Unchanged. |
| Header chips: status, task, graph, steering badge | Kept | Part of the default panel header. | Unchanged. |
| One-line plugin connection row (the pipeline strip) directly under the header, with its live region | Kept | Part of the default panel header, not a panel. | Unchanged, apart from the actions listed above. |
| Success criteria 2, 4, 5 and 7 (concurrent sessions, state copy, accessibility and the gate), AL-10 and AL-11 | Kept | Unaffected by DR-1, with every inspector control removed from the accessibility walk. | Unchanged. |

<domain>
## Phase Boundary

The native head opens on a Cursor-class Agent Panel that runs several concurrent sessions through the composed agent loop (Ultrathink, then Swarm, with Substrate steering and GSD context), shows the pipeline stages, takes steering and keeps every state keyboard and screen-reader accessible.

Requirements: AL-09, AL-10, AL-11, AL-12. Depends on: Phase 44 (plugin submodules and launch contract) and Phase 45 (agent loop stage sources, session catalog, task context). Mac channel: No. UI hint: yes.

Success criteria (verbatim from `.planning/ROADMAP.md`, Phase 46):

1. The default window shows the session rail, the thread, the pipeline strip, the composer and the inspector. Verify: headless UI test and a screenshot recorded under Xvfb.
2. Two sessions each own an engine; selecting a session rebinds the thread, composer and approvals, and each row shows its own status. Verify: headless test with two scripted sessions.
3. While a run is active, a message queues as a steer or a follow-up, appears in the steering history and is recorded in the substrate. Verify: UI and presenter tests.
4. Empty, initialising, streaming, error with retry, success, each degraded mode and a resumed long-running session render their copy. Verify: one test per state.
5. Every tab stop has an accessible name, the keyboard map in the UI spec works, stage changes are announced through a polite live region, motion honours reduced motion, and new tokens pass the contrast tests. Verify: the accessibility walk over the opened panel and the contrast tests.
6. Eight screenshots (empty, streaming with pipeline, degraded, error, long-running list, narrow window, dense list, keyboard focus) are recorded under Xvfb. Verify: evidence files in `docs/runbooks/evidence/`.
7. The gate is green. Verify: `./bootstrap.sh`.

Design contract: `46-UI-SPEC.md` (this directory). It is the visual and interaction source of truth for the VIEW and SHELL tasks.

</domain>

<requirements>
## Requirements (from `.planning/REQUIREMENTS.md`)

- **AL-09** The application opens on the Agent Panel, a Cursor-class agent surface.
  - Acceptance: session rail, thread, pipeline strip, composer with Send, Steer, Follow-up and Stop, and a collapsible inspector; every existing tool remains reachable.
  - Verification: headless UI test and a recorded screenshot (Phase 46). The web head parity is Phase 47, deferred.
- **AL-10** Several sessions run concurrently, each with its own engine and status.
  - Acceptance: selecting a session rebinds the thread, composer and approvals; each row shows its own status and last activity; closing a session stops its engine.
  - Verification: headless test with two scripted sessions (Phase 46).
- **AL-11** The panel is operable by keyboard and by screen reader and honours reduced motion.
  - Acceptance: every tab stop has an accessible name; the documented keyboard map works; stage changes use a polite live region and errors an assertive one; new colours pass the contrast tests.
  - Verification: the accessibility walk over the opened panel and the contrast tests (Phase 46).
- **AL-12** Every panel state has its own copy and screenshot evidence.
  - Acceptance: empty, initialising, streaming, error with retry, success, each degraded mode and a resumed session; eight screenshots recorded.
  - Verification: one test per state and the evidence files (Phase 46).

Related, verified in this phase: AL-04 (panel half: a missing plugin shows as Unavailable with the fix text and the session runs as plain omp) and AL-07 (presenter half: steering queues, appears in the history, is recorded without claiming delivery).

</requirements>

<decisions>
## Implementation Decisions (restated standalone from the Agent Loop design, sections 1 and 7)

Locked. None is open for re-discussion in this phase.

- **D1 Plugins run inside omp.** The four plugins (claude-ultrathink, agent-swarm, agent-substrate, gsd-core-omp) are omp plugins. Autonogrammer resolves them from pinned submodules under `plugins/`, launches `omp acp` with explicit loading flags and a hermetic environment (nothing from the user's global omp configuration), observes them through side channels (files, HTTP), projects what it observes into stage events, and renders those events. No sibling source is copied or edited. The panel therefore never calls a plugin; it only renders stage events.
- **D2 Layout.** `plugins/claude-ultrathink`, `plugins/agent-swarm` and `plugins/agent-substrate` are git submodules (https remote URLs, pinned to commits on each remote's default branch). `plugins/gsd-core-omp` is a tracked Autonogrammer-owned thin plugin, because GSD-Core upstream has no omp runtime. The panel names plugins by these folder names in its fix text.
- **D3 Steering is Autonogrammer-owned.** Operator text is queued over ACP with the existing `RunInputScheduler` (queue after the active turn). Substrate on `main` has no live delivery, so Autonogrammer only records a steering action to Substrate as an event `note` with `payload.action = "steer"` when Substrate is reachable. The UI never claims that Substrate delivered a steer. It may say a steer was recorded.
- **D4 Stage events are data.** They live in `Autonogrammer.Contracts.Agents` (`StageEvent`, `StageSource`, `StageStatus`, and the session and task types). There is no new `StreamEvent` kind and no contract version bump.
- **D5 One engine per session.** One `omp acp` child per session; `SessionEngineSet` in the native head owns N engines. Closing a session stops its engine.
- **D6 Substrate access.** The omp child reaches Substrate through an HTTP entry in `session/new.mcpServers`; the host reaches it through `SubstrateGateway`. Health is checked first, because omp fails `session/new` when any MCP server is unreachable. An unhealthy Substrate is omitted and shown as a Degraded or Unavailable stage. Tokens are passed only as environment variable names, never in argv, logs, persisted state or UI text.
- **D7 Hermetic session state.** Each session has its own state directory for Swarm and Claude state; Swarm does not write `.planning/STATE.md`; `SWARM_AGENT` and `SWARM_TASK_ID` are never exported; Ultrathink uplift is on by default and comes from settings.
- **D8 Degradation.** Each of these is a stage event with status Degraded or Unavailable plus a plain-text reason, and none crashes the panel or the run: Ultrathink off or failed (the original prompt flows unplanned); Swarm missing or failed (single-agent run); Substrate unavailable (no steering record, no shared memory, indicated in the UI); GSD tools missing (task context shows the phase as unknown); a submodule that is not initialised (typed `SubmoduleNotInitializedException` whose message names the plugin, the path and the exact fix `git submodule update --init --recursive plugins/<name>`).
- **D9 Facts the UI must not invent.** There is no live Ultrathink progress: the session record is written once planning finishes, so the Ultrathink stage is Running from prompt send until its session file appears, then Succeeded with the graph. Swarm state comes from `<SWARM_DIR>/.swarm/live.json` and `events.jsonl`. Substrate panels never show a ledger `summary` or `payload` text, only `ts`, `surface`, `kind`, `actor` and `payload.action`, `payload.item`, `payload.outcome` (docs/panel-event-contract.md in the Substrate repository). Memory and lesson note bodies are untrusted plain text, length capped, never markup.

### Presentation and view rules that apply to this phase (design sections 6 and 7)

- Presentation types live in `src/Autonogrammer.Presentation/AgentPanel/` and reference no UI framework: `StageTimelineModel`, `SessionCatalogModel`, `TaskContextModel`, `SteeringInputModel`, `AgentSessionViewModel`, `AgentPanelViewModel`. They reuse `SessionViewModel`, `ComposerViewModel`, `RunInputScheduler`, `RuntimeStatus`, `RuntimeStatusMap`, `KanbanCardState`, `ToolApprovalCoordinator`. They do not duplicate them.
- Views live in `src/Autonogrammer.Native.Views/Views/AgentPanel/*`. Tokens only (`Autonogrammer.*` resources, no literal colours: `ColourLiteralTests`). Every tab-stop control has `AutomationProperties.Name`. Live regions are Polite for streaming and stage status and Assertive for errors. Targets are at least `Autonogrammer.Size.TargetMin`. Reduced-motion classes are honoured. Keyboard order equals XAML order. `ViewMapping.Create` has an explicit case per view model.
- Composition (SHELL task): `SessionEngineSet` creates one `InProcessEngineHost` per session from a per-session `OmpLaunchPlan`. If plugins are not initialised the panel still opens, the plugin stages show Unavailable with the fix text, and sessions run as plain omp.

</decisions>

<assumptions>
## Assumptions (defaults chosen without a user; each is changeable by product review)

1. **Window size.** The window opens at 1280 x 800 and its minimum becomes 960 x 640 (it is 640 x 480 today, `apps/Autonogrammer.Native/MainWindow.axaml`). No test pins 640 or 480 (checked with grep over `tests/Autonogrammer.Native.Tests`).
2. **The Agents rail replaces `ConversationListView`** as the left column of `MainView`. That list is always empty today and carries no status. Tests that pin its name `Conversations` (`AccessibilityNameTests`, `ShellHarness`) are updated by the VIEW and SHELL tasks to the rail names in the UI spec.
3. **Tool toggles keep their names and `x:Name` values.** Only their place changes (the rail's Tools view). Existing tests that look them up by name keep working.
4. **Enter keeps its present meaning while a run is active** (steer, or queue behind waiting follow-ups). Ctrl+Enter is added as an explicit steer, and Ctrl+Shift+Enter as an explicit follow-up. The user-requested map (Enter sends, Ctrl+Enter steers while running) is honoured without breaking the existing composer tests.
5. **Esc stops only after confirmation.** The Stop button stops at once (an explicit pointer or keyboard action on that control). Esc in the composer first asks. This changes the Phase 20 behaviour "Esc stops immediately" and the `ComposerViewModel.HandleKeyAsync("Escape")` contract is kept; the confirmation lives in the view.
6. **Colours.** No new colour token is added. Status is drawn with the existing `StatusSuccess`, `StatusWarning`, `StatusError` text roles, `AccentPurple` for the accent and focus, and the three surfaces. New tokens are sizes only.
7. **Single Light theme in the shell.** `App.axaml` pins `RequestedThemeVariant="Light"`. The tokens also define Dark and every new element uses tokens only, so a Dark switch needs no panel change.
8. **No OS high-contrast or reduced-motion reader.** Avalonia 12.1.4 exposes neither (OQ in Phase 20, AvaloniaUI/Avalonia#19405). `MotionPreference.System` resolves to Full. The panel meets high-contrast needs by never relying on colour alone.
9. **Per-event Substrate rows may not exist.** If the Phase 45 `SubstrateStageObserver` emits only aggregate stages (brief, leases, steering record), the Pipeline tab shows those aggregate rows and omits the event list. UNKNOWN: `grep -n "ParentStageId" src/Autonogrammer.Harness.AgentLoop/Substrate/*.cs` after Phase 45 lands.
10. **Stage retry means re-check.** The Retry control on a failed, degraded or unavailable stage re-probes that stage's source (health check or observer restart). It never re-runs a model turn. A failed model turn is retried by the existing transcript Retry button.
11. **Long-running and stale thresholds belong to `SessionCatalogModel`.** The UI spec only draws the two flags.

</assumptions>

<deferred>
## Deferred Ideas (out of scope for Phase 46)

- **Web head parity: Phase 47 (deferred).** The Blazor web head does not get the Agent Panel in this phase. The hosted API runs the InMemory harness, Railway deploys are paused, and the web head cannot spawn processes or read plugin state, so a faithful panel needs new API endpoints for stage events. Resume when the hosted routed harness (v0.9.2 Phase 13) is unblocked. AL-09's web half stays open under Phase 47.
- Third-party panels contributed through `PanelContribution`. The Agent Panel is a first-party view; a matching `PanelContribution` descriptor (`id: "autonogrammer.agent-panel"`) makes it discoverable, and a real panel host is deferred.
- Deleting a session from the catalog, renaming a session, and pinning a session. The rail only closes and reopens.
- Live Ultrathink progress and live Substrate delivery of steering. Neither exists upstream (D3, D9).
- A settings screen for Ultrathink, Swarm and Substrate toggles. The panel only shows their state.
- Worktree-per-session isolation and Herdr-backed remote sessions (the dossier lists them as natural fits, none is required by AL-09 to AL-12).
- Diff review and terminal embedded in the thread. They stay as tools that replace the thread.

</deferred>

<canonical_refs>
## Canonical References

- `.planning/phases/46-agent-panel/46-UI-SPEC.md` - the design contract.
- `.planning/phases/20-avalonia-foundation/20-UI-SPEC.md` - format and the baseline contract (names, focus, motion, contrast) this phase extends.
- `src/Autonogrammer.Native.Views/Themes/AutonogrammerTokens.axaml` - the only token source.
- `src/Autonogrammer.Native.Views/MainView.axaml` - the layout this phase replaces.
- `apps/Autonogrammer.Native/App.axaml` - shared control styles, focus styles, reduced-motion styles.
- `tests/Autonogrammer.Native.Tests/AccessibilityNameTests.cs`, `ThemeContrastTests.cs`, `ReducedMotionTests.cs`, `KeyboardFocusTests.cs` - the enforced rules.
- `docs/plans/cursor-parity-research.md` - what Cursor documents about its agent surface (Agents Window, one active run per agent, cancel, live events, diff review in the Agents Window).
- `src/Autonogrammer.Contracts/Agents/*` - the stage, session and task contracts the panel renders.

</canonical_refs>
