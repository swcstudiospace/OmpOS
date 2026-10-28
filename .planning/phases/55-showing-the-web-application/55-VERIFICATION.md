# Phase 55 Verification: Showing the Web Application

How to re-run each proof and what it proves. Run from the repository root. Set the .NET environment first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `55-SUMMARY.md`. The Demo tests are hermetic (a manual clock, no browser, no network). The hosted check and the captures need Chrome and network access to the hosted URL.

Recorded totals (2026-10-28, orchestrator facts): Web 262 or more passed on the final tree before the last wiring (no per-class result is claimed; each row is "inside the passing Web run"). The Demo was deployed at https://ompos-web.vercel.app and captured with headless Chrome from the live URL: no console errors, no failed requests. The full `./bootstrap.sh` gate on the final commit was running; its result is recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 | `dotnet build services/OmpOS.Api -c Release` (regenerates `docs/api/openapi.json`), then `dotnet test tests/OmpOS.Web.Tests -c Release -- --filter-class "*DemoApiHandlerTests*" --filter-class "*DemoFixturesContractTests*" --filter-class "*OpenApiCoverageTests*" --filter-class "*DemoExclusionTests*" --filter-class "*RenderCspScriptTests*"` | The handler answers the client's routes; fixture bodies deserialise strictly into the contract types; every operation the client uses is in the OpenAPI document; Demo types exist only in a Demo build; `render-csp.sh` prints the exact `connect-src` line and fails closed | Inside the passing Web run |
| 1 (production bundle) | `dotnet publish apps/OmpOS.Web -c Release -o /tmp/ompos-prod`, then `grep -c DemoApiHandler /tmp/ompos-prod/wwwroot/_framework/OmpOS.Web*` (every count 0) and `test ! -e /tmp/ompos-prod/wwwroot/appsettings.demo.json` | A normal build holds no Demo code | no recorded run of the commands as written |
| 2 | `curl -sI https://ompos-web.vercel.app` and `URL=https://ompos-web.vercel.app OUT=/tmp/ompos-hosted node scripts/web-demo/capture.mjs` | The hosted page answers and renders | Headless Chrome capture of the live URL, no console errors, no failed requests (orchestrator, 2026-10-28). No curl output or capture is committed. Commit and build id: UNVERIFIED: `vercel inspect ompos-web.vercel.app` |
| 3 | `scripts/web-demo/serve-demo.sh` after `scripts/vercel/build-demo.sh`, then `node scripts/web-demo/capture.mjs` with `THEME=light` or `dark` and `W` and `H` set to 1440x900, 1024x768 and 390x844 | The screens of the Demo in both themes at three widths | 16 images committed (see below); the smoke's 68 images were written to `/tmp/shots` and not committed |

## Evidence files

| File | Shows |
|------|-------|
| `docs/runbooks/evidence/web-app-2026-10-28/L1440-01-agents.png`, `L1440-03-completed-changes.png`, `L1440-04-approval.png`, `L1440-05-failed.png`, `L1440-06-terminal.png`, `L1440-08-slash-palette.png`, `L1440-09-kanban.png`, `L1440-10-settings-00-general.png` | Light, 1440 (8) |
| `D1440-01-agents.png`, `D1440-03-completed-changes.png`, `D1440-09-kanban.png` | Dark, 1440 (3) |
| `L1024-01-agents.png` | Light, 1024 (1); Dark 1024: none |
| `L390-01-agents.png`, `L390-02-drawer.png` | Light, 390 (2); Dark 390: none |
| `M-11-models-cn-coding-plans.png`, `M-12-models-eu.png` | Models page, CN coding plans and EU providers |
| `docs/assets/screens/web-agents-light.png`, `web-agents-dark.png`, `web-changes-review-light.png`, `web-kanban-light.png`, `web-mobile-light.png`, `web-settings-models-light.png` | The README set |
| `docs/runbooks/web-demo.md`, `scripts/web-demo/capture.mjs`, `scripts/web-demo/serve-demo.sh`, `scripts/vercel/build-demo.sh`, `scripts/vercel/render-csp.sh`, `vercel.ts` | Method and build |

## Known limits of this verification

- No README exists in the evidence folder; the method is in the header of `capture.mjs` and in the runbook.
- The folder is dated 2026-10-28; the ROADMAP names `web-app-2026-10-27`.
- Missing from the matrix of every screen in both themes at three widths: Dark at 1024 and 390 entirely; at 1440 Dark lacks approval, failed, terminal, slash palette and Settings; Light at 1024 and 390 lack every screen except agents and the drawer.
- `docs/runbooks/web-demo.md` says "no public deployment exists" in its status line; the Demo is hosted (orchestrator fact).
- The hosted bundle's commit and the Vercel build id are not in the tree.
