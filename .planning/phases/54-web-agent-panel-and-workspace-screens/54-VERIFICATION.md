# Phase 54 Verification: Web Agent Panel and Workspace Screens

How to re-run each proof and what it proves. Run from the repository root. Set the .NET environment first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `54-SUMMARY.md`. Component tests are hermetic bUnit tests with fake hosts. The smoke needs Node and a Chrome binary.

Recorded totals (2026-10-28, orchestrator facts, final tree before the last wiring): UI 595, Web 262 or more passed. No per-class result is claimed; each row is "inside the passing <Project> run". The smoke: 68 axe runs, 0 violations, Wave 5 tree. The full `./bootstrap.sh` gate on the final commit was running; its result is recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 | `dotnet test tests/OmpOS.UI.Tests -c Release -- --filter-class "*ThreadViewTests*" --filter-class "*PipelineStripTests*" --filter-class "*BlockViewTests*" --filter-class "*ComposerPanelTests*" --filter-class "*ToolApprovalTests*" --filter-class "*ChangesBarTests*" --filter-class "*DiffViewTests*" --filter-class "*KanbanViewTests*" --filter-class "*TerminalViewTests*" --filter-class "*WorkbenchViewTests*" --filter-class "*AgentRailTests*"` | Each component renders from its presentation model in its states (empty, streaming, approval pending, change kept or undone, Kanban columns and keyboard moves, terminal unavailable and running) | Inside the passing UI run. Cloud and Remote views: none to test (Partial) |
| 2 | `dotnet test tests/OmpOS.UI.Tests -c Release -- --filter-class "*SettingsDialogTests*" --filter-class "*SettingsWebPagesTests*" --filter-class "*SettingsModelsPageTests*" --filter-class "*SettingsPagesTests*"` and `dotnet test tests/OmpOS.Web.Tests -c Release -- --filter-class "*SettingsPagesRenderTests*" --filter-class "*WebSettingsPagesTests*" --filter-class "*DemoProvidersRouteTests*"` | The dialog offers the pages in the web order; the key field has no `value`, is replaced after a save attempt and is never echoed; the Demo refuses key writes | Inside the passing UI and Web runs |
| 2 (API) | `dotnet test tests/OmpOS.Api.IntegrationTests -c Release -- --filter-class "*ProvidersCredentialEndpointTests*"` (needs podman for the suite) | The credential route is off by default (403), refuses a blank or whitespace key with 400 without echoing it, and a captured logger holds no key | `Api.IntegrationTests` 1027 of 1027 passed on the final tree |
| 3 | `scripts/web-smoke/run-smoke.sh` (`SMOKE_TARGETS="api demo"` by default; `DEMO_DIR`, `DEMO_PORT`, `SHOT_DIR` optional) | Every Demo screen in light and dark at 1440, 1024 and 390: axe WCAG 2.0 and 2.1 A and AA clean, no sideways scroll, no console error or failed request, Demo banner present, skip link first in tab order, drawer traps focus on the phone, theme persists across a reload | 68 axe runs, 0 violations (recorded 2026-10-28, Wave 5 tree). Wired by `bootstrap.sh` line 81 and `.github/workflows/ci.yml` line 96 |

## Evidence files

| File | Shows |
|------|-------|
| `docs/runbooks/evidence/web-app-2026-10-28/` (16 images) | Agents, completed with Changes, approval, failed, terminal, slash palette, Kanban, Settings General, drawer, two Models views; see Phase 55 for the count against the matrix |
| `docs/assets/screens/web-agents-light.png`, `web-agents-dark.png`, `web-changes-review-light.png`, `web-kanban-light.png`, `web-mobile-light.png`, `web-settings-models-light.png` | The six web screenshots used by the README and guides |
| `scripts/web-smoke/smoke-demo.mjs`, `scripts/web-smoke/axe-core-4.14.0.sha256` | The Demo smoke and the pinned axe engine hash |

## Known limits of this verification

- The 68-run result is from the Wave 5 tree; the smoke was not recorded again on the final commit by this record.
- The smoke opens one Settings page; the 17 others were looked at in headless Chrome by the builder (no console errors, 103 directory cards) but not axe-run.
- The API half of the smoke (`smoke.mjs`, real API with the echo harness) has no recorded result for this phase.
- The terminal, steering and a real diff against a live engine need a model turn (OD-07).
