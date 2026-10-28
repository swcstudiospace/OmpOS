# Phase 53 Summary: Web Head Foundation and API Surface

Status (recorded 2026-10-28): criteria 1 to 3 are Met: named tests and the web smoke ran inside the `./bootstrap.sh` gate that passed on e24e6851 (recorded 2026-10-28). The web head is styled in White and Purple with the Greptile faces on a shell that mirrors the desktop, and the API serves the endpoints the screens read. Test totals below are per project (before the gate); the gate result is in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log` (12215 total, 12213 succeeded, 0 failed, 2 skipped). No live model turn was run (OD-07), and nothing ran on Windows, macOS or Android.

The record was written after the work. Statuses use: Met, Met (recorded), Partial, Blocked, Not verified.

Recorded totals (2026-10-28, per project before the gate; the gate then ran all of `OmpOS.Linux.slnf`: 12215 total, 12213 succeeded, 0 failed, 2 skipped): UI 595, Web 262 or more, Architecture 46, Contracts 137; `Api.IntegrationTests` 1027 of 1027 passed with throwaway podman services. The web head smoke (`scripts/web-smoke/smoke-demo.mjs`) ran 68 axe runs (light and dark at 1440, 1024 and 390 across every screen) with 0 violations in the Wave 5 tree; the gate's Demo smoke on e24e6851 ran 6 configurations (light and dark at 1440, 1024 and 390), 68 axe runs, 0 violations, and its API half passed.

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Bootstrap and the template CSS gone; tokens, self-hosted woff2 fonts with OFL texts, reduced motion, theme boot script; loading ring, error bar and skip link keep their contract | Met | `apps/OmpOS.Web/wwwroot` has no `lib/` folder; `src/OmpOS.UI/wwwroot/styles/tokens.css`, `fonts.css`, `base.css` (with `prefers-reduced-motion`); `fonts/{DMSans,Anybody,SpaceMono}/*.woff2` with an `OFL.txt` each; `scripts/theme-boot.js`; `css/app.css` kept for the error bar and loading ring. `CssContractTests`, `AccessibilityTests`, `IndexHtmlTests` inside the passing UI and Web runs; 68 smoke axe runs, 0 violations |
| 2 | Shell with top bar, agent rail (groups, status, New, Close), thread region, drawer below 840 px; bUnit landmark and drawer-focus tests; screenshots at three widths | Met | `AppShellTests`, `AgentRailTests`, `ShellRegionTests`, `ShellLayoutTests` inside the passing UI and Web runs. Screenshots `L1440-01-agents.png`, `L1024-01-agents.png`, `L390-01-agents.png`, `L390-02-drawer.png` in `docs/runbooks/evidence/web-app-2026-10-28/`; the smoke walks the three widths. The wider screenshot matrix is Phase 55 criterion 3 |
| 3 | The API serves catalog, stages, context, steering, config options, slash commands, changes, board, lifecycle and workspace endpoints, owner-scoped, rate-limited, in OpenAPI 3.1; integration tests per endpoint (404 other owner, 409, 413, 415) and a contract test against `docs/api/openapi.json` | Met | `docs/api/openapi.json` is `3.1.1`, 60 paths (`/events`, `/board`, `/sessions/{id}/changes`, `/workspace`, `/source`, ...). `CatalogEndpointTests`, `CatalogEventStreamTests`, `StageEndpointTests`, `SteeringEndpointTests`, `SessionConfigEndpointTests`, `ChangeReviewEndpointTests`, `ToolApprovalEndpointTests`, `SessionEndpointTests`, `SourceEndpointTests`, `RateLimitPolicyTests`, the `WorkspaceSettings/` tests; contract side `OpenApiCoverageTests`, `WorkspaceSettingsOpenApiTests`, `GuardTests`. All inside the passing `Api.IntegrationTests` run (1027 of 1027) and the Web run |

## What the phase delivered

- A token CSS set (surfaces, text, accent, focus, status, diff, shadow, fonts, sizes, motion, z-index) with a contract test that fails on an undefined `var()`, a colour literal, or the display face below 20 px or not SemiBold; component classes for buttons, chips, inputs, dialogs, tabs, switches; Light on `:root`, Dark and System by attribute; a blocking `theme-boot.js` (no inline script) and an ES module for theme and motion.
- Self-hosted fonts converted losslessly to woff2 by `scripts/web/make-woff2.sh`: DM Sans Regular, Medium, SemiBold; Anybody SemiBold; Space Mono Regular, Bold.
- `AppShell`, `TopBar`, `ThemeSwitch`, `ShellFooter` (source link and licence), the `ObservableComponent` base, a drawer with focus trap, Escape and scrim close, and `inert` main while open.
- API routes of the route table in `docs/api/openapi.json`: catalog with status, group and last activity; the user event stream with resume and `resync`; stages; context (`available:false` unless a source is registered); steering (`send_now` or `queue`); tool approval list; config options and slash commands; changes with accept and reject (a write only when the file still holds the agent's text, under `Api:Workspace:Root`); board and card details (ten columns); close, reopen, resume; workspace; anonymous `GET /source` (licence, repository, revision). Workspace settings routes (conventions, hooks, runtime, plugins, files, tree) with a second rate-limit policy, an owner allow-list, a secret-file policy and path redaction.

## Deviations

- The brand mark is a CSS background, not an `<img>`: the smoke asserts no `img` in the app.
- `css/app.css` stays (error bar and loading ring only): `WebShellAccessibilityTests` reads it.
- `IUserEventStream` is `IUserEventFeed` (CA1711 forbids the `Stream` suffix).
- In `GET /sessions`, `status` stays the stored `SessionStatus`; the rail status is `rowStatus`.
- Board and rail cards read `Agent <id8>` because the stored session has no title (OQ row proposed in the hand-off).
- `<meta name="theme-color">` follows the OS scheme only; an explicit Light or Dark choice does not recolour the browser chrome.

## Partial

None of the three criteria is Partial. Gaps in the API behind the criteria are in Known limits.

## Blocked (OQ/OD)

None for these criteria. Hook consent and quarantine are not served over the API (see the proposed OQ rows in the hand-off).

## Known limits

- GitHub CI did not reach the tests: main-push run 40758215402 and tag-push run 40758275399 failed at 'Start test services' (rootless podman, OQ-168); the local gate is the gate of record.

- The API starts one engine session per prompt: config options, slash commands and live steering exist only while a run lives; at idle the routes answer `available:false` or 409 (`config_options_unavailable`, `config_option_busy`). Worktree-mode sessions do not forward the control interfaces.
- The closed flag, the change decisions, the stage snapshots and the event history are in memory, per instance, lost on restart.
- `GET /sessions/{id}/context` answers `available:false` because no host ships a task-context source; stages show the engine turn plus explained `Unavailable` plugin stages until a host publishes to `StageHub`.
- `GET /workspace/file` has no portable check for special files on Linux; a residual time-of-check window between path resolution and open exists (UNVERIFIED on Linux: `mkfifo ws/p`, then GET).
- Browser behaviour of the new CSS beyond the 68 axe runs (contrast of every rendered pair) is checked by the token contrast tests only.
