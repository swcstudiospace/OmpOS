# Phase 54 Summary: Web Agent Panel and Workspace Screens

Status (recorded 2026-10-28): criterion 2 met as recorded runs of named tests; criteria 1 and 3 are Partial. The web head renders the thread, composer, approvals, Changes bar, Kanban, terminal view and a Settings dialog of 18 pages from the shared presentation models, but it has no Cloud or Remote view beyond Settings information pages, and the smoke visits one Settings page. No live model turn was run (OD-07); the screens show the scripted Demo or the in-memory echo harness. The full `./bootstrap.sh` gate on the final commit was running; its result is recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`.

The record was written after the work. Statuses use: Met, Met (recorded), Partial, Blocked, Not verified.

Recorded totals (2026-10-28, final tree before the last wiring): UI 595, Web 262 or more. The web head smoke (`scripts/web-smoke/smoke-demo.mjs`) ran 68 axe runs (light and dark at 1440, 1024 and 390 across every screen) with 0 violations in the Wave 5 tree.

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Thread header with plugin connection row, the block views, composer with model, mode and thinking chips and slash palette, steering, tool approvals, Changes bar (Keep, Undo, Review), Kanban, omp terminal view, Cloud and Remote views, from the shared presentation models; bUnit tests per component and state | Partial | All items except Cloud and Remote have a component and a test class inside the passing UI run: `ThreadViewTests`, `PipelineStripTests` (the plugin connection row), `BlockViewTests`, `ComposerPanelTests` (chips, slash palette, steering), `ToolApprovalTests`, `ChangesBarTests`, `DiffViewTests`, `KanbanViewTests`, `TerminalViewTests`, `WorkbenchViewTests`. Missing: a Cloud view and a Remote view in the web workbench (`ViewTabs` offers Agents and Kanban; Cloud agents and Remote environments appear only as Settings information pages with a desktop notice). The terminal view shows Unavailable against the real API (no stream endpoint); it runs on the Demo's scripted transport |
| 2 | Settings covers General, Runtime, Agents, Models and providers, Tab, Rules, Skills, Commands, Hooks, Plugins, Network and Keyboard; no page renders a stored key; bUnit tests and a no-key-rendered-or-logged test | Met (recorded) | `WebSettingsOrder` registers the 12 named pages and six more (Run mode, MCP, Workspaces, Remote environments, Cloud agents, Privacy). Inside the passing UI and Web runs: `SettingsDialogTests`, `SettingsWebPagesTests`, `SettingsPagesRenderTests` (`TheKeyFieldIsDisabledByTheOperatorAndNeverHoldsAValue`), `SettingsModelsPageTests` (`KeyFieldIsWriteOnlyAndTheSavedKeyIsNeverShownAgain`, `RefusedKeyIsNeverEchoedAndTheInputIsReplacedByAnEmptyOne`), `DemoProvidersRouteTests` (`KeyWritesAreRefusedAndNeverEchoed`); API side `ProvidersCredentialEndpointTests` with a captured logger. Caveat: Agents, Run mode, Workspaces, Remote, Cloud and Privacy are information pages; Tab says it is not available in the web head; Hooks, MCP and Runtime are read-only |
| 3 | Smoke v2 drives every screen in both themes at three widths with axe clean; `scripts/web-smoke/run-smoke.sh` in the gate | Partial | `smoke-demo.mjs` covers agents (running), approval, terminal, completed with Changes, failed, closed, slash palette, model menu, Kanban, Settings and the phone drawer in light and dark at 1440, 1024 and 390: 68 axe runs, 0 violations (recorded 2026-10-28, Wave 5 tree). `run-smoke.sh` is called by `bootstrap.sh` (line 81) and by `.github/workflows/ci.yml` (line 96). Gaps: the smoke opens the Settings dialog once (its first page); the other Settings pages were captured but not axe-run; a gate result on the final commit is not recorded here |

## What the phase delivered

- A workbench page: rail (groups, status glyphs, New, Close), tabs Agents and Kanban, thread with header, pipeline strip, notes and task context, blocks, composer with chips, slash palette, steering history and a Stop confirm bar, tool approvals, Changes bar with a unified diff and a review panel, Kanban board with ten columns, omp terminal view, shortcuts (Ctrl or Cmd with Shift+A, Shift+K and comma).
- A Settings dialog with 18 pages, a provider directory (103 cards in the Demo, EU, US and CN plans) with write-only key entry and an operator-disabled state, backed by the settings routes of Phase 53.
- Smoke v2 for the Demo bundle and `run-smoke.sh` with targets `api demo`; a capture script (Phase 55).

## Deviations

- The slash palette keeps a textarea with `aria-autocomplete`, `aria-controls` and `aria-activedescendant` and a `listbox` (a textarea cannot be a `combobox`).
- Blazor cannot cancel key defaults, so `ompos-keys.js` prevents newline and arrow scrolling; Ctrl+N is not bound (the browser reserves it).
- Kanban moves cards to Backlog or Archived by buttons, not drag and drop (WCAG 2.5.7 is met by the buttons; the owner has not confirmed that this is enough).
- The new conversation button keeps the label "New conversation" (the older smoke selector and label-in-name rule).
- `DiffPlaceholderView` and `PermissionBlockView` stay for shared parity text; the interactive approvals and the real diff are separate components.
- Hooks consent and quarantine are not shown (the API does not send them); automations are served by the route but have no web page.
- Display, Worktrees and Ignore are not offered in the web Settings.
- xterm uses the DOM renderer (no WebGL addon).

## Partial

- Criterion 1: Cloud and Remote views are not built in the web workbench; the terminal view has no real stream endpoint.
- Criterion 3: one of 18 Settings pages is walked by the smoke; no gate result on the final commit.

## Blocked (OQ/OD)

- The real terminal and live steering in the web head need a stream endpoint and a persistent engine session per API session (OQ proposed in the Phase 53 hand-off).
- Anything that needs a model turn (streaming, a real approval, a real diff) is OD-07.

## Known limits

- The smoke's incomplete results: the builder's last run reported one `aria-valid-attr-value` item on the model chip in the composer-menu step; the smoke tolerates `color-contrast` incompletes and the xterm `aria-posinset` case as warnings. The recorded fact is 0 violations.
- Slash palette at 390 px squeezes the description column to about 60 px; the slash list and each Kanban lane are keyboard-focusable scroll regions (one extra tab stop each).
- The boot screen, the rail error and empty states and the Retry state were restyled through tokens but not screenshotted (the Demo cannot reach them).
- `SettingsWindowModel` cannot reopen after close: the web head builds a new model per open.
- General and Network page wording owned by Presentation is desktop-worded ("OmpOS sends nothing anywhere itself"); a web variant is missing.
- Catalogue rows that are not omp-native cannot take a key over the API (409 operator-managed).
- Browser behaviour in Firefox, Safari and on a phone is not run.
