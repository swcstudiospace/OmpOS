# Phase 53 Verification: Web Head Foundation and API Surface

How to re-run each proof and what it proves. Run from the repository root. Set the .NET environment first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `53-SUMMARY.md`. Tests are hermetic (bUnit, in-process API host). Only the web smoke uses a browser.

Recorded totals (2026-10-28, per project before the gate; the gate then passed with 12215 total, 12213 succeeded, 0 failed, 2 skipped): UI 595, Web 262 or more, Architecture 46, Contracts 137; `Api.IntegrationTests` 1027 of 1027 with throwaway podman services. No per-class result is claimed: each row below is "inside the passing <Project> run". The full `./bootstrap.sh` gate passed on commit e24e6851 (recorded 2026-10-28, exit 0: `dotnet test Autonogrammer.Linux.slnf` 12215 total, 12213 succeeded, 0 failed, 2 skipped; warnings-as-errors Release build; `dotnet format --verify-no-changes` clean; repository checks, licence check, plugin tests and the web smoke OK); log `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log`.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 | `dotnet test tests/Autonogrammer.UI.Tests -c Release -- --filter-class "*CssContractTests*" --filter-class "*AccessibilityTests*"` and `dotnet test tests/Autonogrammer.Web.Tests -c Release -- --filter-class "*IndexHtmlTests*"` and `dotnet test tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*WebShellAccessibilityTests*"` | Every CSS file is imported, uses only defined tokens, has no colour literal, and uses the display face only at 20 px or more and SemiBold; the page head links the Autonogrammer stylesheet and no Bootstrap, scripts are files and the theme boot runs before the stylesheets, body fonts are preloaded; the error bar and loading ring rules the architecture test reads are present | Inside the passing UI, Web and Architecture runs |
| 1 (smoke) | `scripts/web-smoke/run-smoke.sh` (targets `api demo`; Chrome and node needed) | The page loads, the shell and loading ring behave, and axe reports no violation | 68 axe runs, 0 violations (Wave 5 tree); the gate ran both halves on e24e6851: API half OK, Demo half 6 configurations, 68 axe runs, 0 violations (recorded 2026-10-28). `run-smoke.sh` is called by `bootstrap.sh` (line 81) and `.github/workflows/ci.yml` (line 96) |
| 2 | `dotnet test tests/Autonogrammer.UI.Tests -c Release -- --filter-class "*AppShellTests*" --filter-class "*AgentRailTests*" --filter-class "*ShellRegionTests*" --filter-class "*ThemeSwitchTests*" --filter-class "*ShellFooterTests*"` and `dotnet test tests/Autonogrammer.Web.Tests -c Release -- --filter-class "*ShellLayoutTests*"` | Landmarks (`header`, `nav`, `main`, footer), the skip link, the drawer opening and closing with focus returned to the opener, rail rows with status and close | Inside the passing UI and Web runs. Screenshots: see the evidence table |
| 3 | `dotnet test tests/Autonogrammer.Api.IntegrationTests -c Release` (needs podman for the Postgres and Coordination tests) | Each route: owner scope (404 for another owner or an absent session), 409 states, 413 and 415 on bodies, rate limit, OpenAPI shape | `Api.IntegrationTests` 1027 of 1027 passed on the final tree |
| 3 (contract) | `dotnet build services/Autonogrammer.Api` (regenerates `docs/api/openapi.json`), then `dotnet test tests/Autonogrammer.Web.Tests -c Release -- --filter-class "*OpenApiCoverageTests*"` | Every route the web client calls exists in the committed OpenAPI document | Inside the passing Web run |

## Evidence files

| File | Shows |
|------|-------|
| `docs/runbooks/evidence/web-app-2026-10-28/L1440-01-agents.png`, `L1024-01-agents.png`, `L390-01-agents.png` | The shell with the rail at the three widths, Light |
| `docs/runbooks/evidence/web-app-2026-10-28/L390-02-drawer.png` | The drawer open at 390 px |
| `docs/api/openapi.json` | OpenAPI 3.1.1, 60 paths |

## Known limits of this verification

- The Dark screenshots of the shell exist only at 1440 (`D1440-01-agents.png`); the full matrix is in Phase 55.
- The API half of the smoke (`smoke.mjs`) passed inside the gate (recorded 2026-10-28); the 68-run figure is the Demo half.
- `GET /workspace/file` against a FIFO is UNVERIFIED on Linux: `mkfifo ws/p`, then `GET /workspace/file` for `p`.
- GitHub CI failed at 'Start test services' (runs 40758215402 and 40758275399, OQ-168); the local gate is the gate of record.
- Nothing ran in Firefox, Safari or on a phone; the three widths are headless Chrome viewports.
