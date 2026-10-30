# Phase 54 Verification: Web Agent Panel and Workspace Screens

How to re-run each proof and what it proves. Run from the repository root. Set the .NET environment first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `54-SUMMARY.md`. Component tests are hermetic bUnit tests with fake hosts. The smoke needs Node and a Chrome binary.

Recorded totals (2026-10-28, per project before the gate; the gate then passed with 12215 total, 12213 succeeded, 0 failed, 2 skipped): UI 595, Web 262 or more passed. No per-class result is claimed; each row is "inside the passing <Project> run". The smoke: 68 axe runs, 0 violations, Wave 5 tree. The full `./bootstrap.sh` gate passed on commit e24e6851 (recorded 2026-10-28, exit 0: `dotnet test Autonogrammer.Linux.slnf` 12215 total, 12213 succeeded, 0 failed, 2 skipped; warnings-as-errors Release build; `dotnet format --verify-no-changes` clean; repository checks, licence check, plugin tests and the web smoke OK); log `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 | `dotnet test tests/Autonogrammer.UI.Tests -c Release -- --filter-class "*ThreadViewTests*" --filter-class "*PipelineStripTests*" --filter-class "*BlockViewTests*" --filter-class "*ComposerPanelTests*" --filter-class "*ToolApprovalTests*" --filter-class "*ChangesBarTests*" --filter-class "*DiffViewTests*" --filter-class "*KanbanViewTests*" --filter-class "*TerminalViewTests*" --filter-class "*WorkbenchViewTests*" --filter-class "*AgentRailTests*"` | Each component renders from its presentation model in its states (empty, streaming, approval pending, change kept or undone, Kanban columns and keyboard moves, terminal unavailable and running) | Inside the passing UI run. Cloud and Remote views: none to test (Partial) |
| 2 | `dotnet test tests/Autonogrammer.UI.Tests -c Release -- --filter-class "*SettingsDialogTests*" --filter-class "*SettingsWebPagesTests*" --filter-class "*SettingsModelsPageTests*" --filter-class "*SettingsPagesTests*"` and `dotnet test tests/Autonogrammer.Web.Tests -c Release -- --filter-class "*SettingsPagesRenderTests*" --filter-class "*WebSettingsPagesTests*" --filter-class "*DemoProvidersRouteTests*"` | The dialog offers the pages in the web order; the key field has no `value`, is replaced after a save attempt and is never echoed; the Demo refuses key writes | Inside the passing UI and Web runs |
| 2 (API) | `dotnet test tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*ProvidersCredentialEndpointTests*"` (needs podman for the suite) | The credential route is off by default (403), refuses a blank or whitespace key with 400 without echoing it, and a captured logger holds no key | `Api.IntegrationTests` 1027 of 1027 passed on the final tree |
| 3 | `scripts/web-smoke/run-smoke.sh` (`SMOKE_TARGETS="api demo"` by default; `DEMO_DIR`, `DEMO_PORT`, `SHOT_DIR` optional) | Every Demo screen in light and dark at 1440, 1024 and 390: axe WCAG 2.0 and 2.1 A and AA clean, no sideways scroll, no console error or failed request, Demo banner present, skip link first in tab order, drawer traps focus on the phone, theme persists across a reload | 68 axe runs, 0 violations (recorded 2026-10-28, Wave 5 tree); the gate re-ran both halves on e24e6851 with the same 68 axe runs and 0 violations (recorded 2026-10-28). Wired by `bootstrap.sh` line 81 and `.github/workflows/ci.yml` line 96 |

## Evidence files

| File | Shows |
|------|-------|
| `docs/runbooks/evidence/web-app-2026-10-28/` (16 images) | Agents, completed with Changes, approval, failed, terminal, slash palette, Kanban, Settings General, drawer, two Models views; see Phase 55 for the count against the matrix |
| `docs/assets/screens/web-agents-light.png`, `web-agents-dark.png`, `web-changes-review-light.png`, `web-kanban-light.png`, `web-mobile-light.png`, `web-settings-models-light.png` | The six web screenshots used by the README and guides |
| `scripts/web-smoke/smoke-demo.mjs`, `scripts/web-smoke/axe-core-4.14.0.sha256` | The Demo smoke and the pinned axe engine hash |

## Known limits of this verification

- The gate on e24e6851 re-ran the smoke (68 runs, 0 violations); the settings step is still one page.
- The smoke opens one Settings page; the 17 others were looked at in headless Chrome by the builder (no console errors, 103 directory cards) but not axe-run.
- The API half of the smoke (`smoke.mjs`) passed inside the gate; no per-screen detail is recorded here.
- GitHub CI failed at 'Start test services' (runs 40758215402 and 40758275399, OQ-168).
- The terminal, steering and a real diff against a live engine need a model turn (OD-07).
