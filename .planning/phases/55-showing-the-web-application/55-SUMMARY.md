# Phase 55 Summary: Showing the Web Application

Status (recorded 2026-10-28): criterion 1 met as recorded runs of named tests; criteria 2 and 3 are Partial. The Demo build exists and is hosted at https://ompos-web.vercel.app (Vercel production alias of project `ompos-web` in team `swcstudiospace`; static Demo bundle, simulated engine, no backend, no secret; the repository homepage points to it), recorded 2026-10-28 by the orchestrator and captured with headless Chrome from the live URL with no console errors and no failed requests. `docs/runbooks/web-demo.md` now records the project id `prj_5clbARYWIOrfgkMSK2Fvw2p0iXAc` and the deployment `ompos-px32dymvw-swcstudiospace.vercel.app`, and the evidence folder has a README and `live-vercel-L1440-01-agents.png` with `live-vercel-report.json`; the commit the bundle was built from is not recorded, and the folder holds 16 of the 68 screenshots of the matrix (Dark at 1024 and 390 are absent). The local gate of record passed on e24e6851 (see totals). No live model turn was run (OD-07).

The record was written after the work. Statuses use: Met, Met (recorded), Partial, Blocked, Not verified.

Recorded totals (2026-10-28): the full `./bootstrap.sh` gate on a clean worktree of commit e24e6851 exited 0 (12215 tests, 12213 succeeded, 0 failed, 2 skipped; web smoke API half and Demo half with axe-core 4.14.0), recorded by the orchestrator; release head 9d4e0075 is tagged v0.15.0, later commits are documentation only. The Demo tests are inside that run; the 68 axe runs of the Demo smoke are inside it, their images are not committed.

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | A Demo build replaces the HTTP handler with an in-browser simulated API answering the real routes, shows a permanent banner, needs no network and keeps `connect-src 'self'`; fixtures validate against `docs/api/openapi.json`; `RenderCspScriptTests` | Met (recorded) | `apps/OmpOS.Web/Demo/DemoApiHandler.cs` and its fixtures; `DemoBanner`; `DemoApiHandlerTests`, `DemoFixturesContractTests` (bodies deserialise strictly into the contract types), `OpenApiCoverageTests` (every operation the client uses is in `docs/api/openapi.json`), `DemoExclusionTests` (Demo types exist only in a Demo build), `RenderCspScriptTests` (`scripts/vercel/render-csp.sh`), all inside the passing Web run. Caveat: fixtures are checked against the contract types and the routes against the OpenAPI paths, not against the JSON schemas of the document |
| 2 | A hosted preview is reachable and recorded with its URL, commit and build id; `curl` of the URL and a headless Chrome screenshot of the hosted page | Partial | URL https://ompos-web.vercel.app, project `ompos-web` (`prj_5clbARYWIOrfgkMSK2Fvw2p0iXAc`) and deployment `ompos-px32dymvw-swcstudiospace.vercel.app` are recorded in `docs/runbooks/web-demo.md` (the deployment is the build identifier; its URL answers 302 behind Deployment Protection, the alias is public). The hosted-page screenshot is committed: `docs/runbooks/evidence/web-app-2026-10-28/live-vercel-L1440-01-agents.png` and `live-vercel-report.json` (`ready: true`, `errors: []`, `failedRequests: 0`, 18 Settings pages). Missing: the commit the bundle was built from (no match for a commit hash in `web-demo.md` or the evidence README; it was built with `scripts/vercel/build-demo.sh` by the orchestrator from a tree not named) and a committed `curl` output. UNVERIFIED: `vercel inspect ompos-px32dymvw-swcstudiospace.vercel.app` for the source commit |
| 3 | A runbook shows how to run the web application locally against the real API; screenshots of every screen in both themes at 1440, 1024 and 390 px committed under `docs/runbooks/evidence/web-app-2026-10-28/`; the evidence README names the method and what is simulated | Partial | `docs/runbooks/web-demo.md` exists (real API, Demo, deployment, tests; its status line now names the hosted Demo). The evidence README exists and names the method (build, `capture.mjs`, viewports) and what is simulated (everything: scripted fixtures, no backend, no model). The folder is `web-app-2026-10-28` (the ROADMAP's `-10-27` was a typo of the planning). Committed images: 16 plus the live capture: Light 1440 8 (agents, completed-changes, approval, failed, terminal, slash-palette, kanban, settings-00-general), Dark 1440 3 (agents, completed-changes, kanban), Light 1024 1 (agents), Dark 1024 0, Light 390 2 (agents, drawer), Dark 390 0, two Models views. The matrix of every screen in both themes at three widths is NOT committed: the 68-run smoke images exist only under `/tmp/shots`. Dark 1024 and Dark 390 are entirely absent |

## What the phase delivered

- A Demo build of the same web head: six scripted agents (one streaming markdown, code, a tool call and a diff; one waiting for approval; one completed with three changed files; one failed; one closed; one omp terminal), a ten-column Kanban board, an 18-page Settings dialog with the 103-row provider directory, steering and changes; the Content-Security-Policy allows only the page's own origin.
- `scripts/vercel/build-demo.sh`, `render-csp.sh`, `vercel.ts` (builds the Demo when `OMPOS_DEMO=1`), `scripts/web-demo/serve-demo.sh` (loopback) and `capture.mjs`.
- A runbook; the hosted Demo; six README screenshots at `docs/assets/screens/web-*.png`.

## Deviations

- The evidence folder is dated 2026-10-28 (the planning text said 2026-10-27).
- The Demo is the hosted artefact; a hosted real API stays blocked on the sign-in authority (OD-03, ADR-0017).
- Changing the model during a running turn is refused in the Demo with the same 409 the API gives.
- Boot, error and empty rail states cannot be reached in the Demo and are not screenshotted.

## Partial

- Criterion 2: the source commit of the hosted bundle is not recorded; no curl output committed (the deployment id and the live screenshot are).
- Criterion 3: the evidence README exists; the matrix of both themes at three widths is incomplete (Dark 1024 and Dark 390 absent; most screens exist only at Light 1440).

## Blocked (OQ/OD)

- OQ-99 (a labelled Demo hosted publicly on Vercel): the default was taken and the Demo is hosted; the owner has not answered.
- A hosted real API: OD-03.

## Known limits

- The Demo is scripted: no model, no files, nothing leaves the page.
- The 68-run smoke and the first screenshots come from the Wave 5 tree; the hosted bundle was built by the orchestrator from a tree this record cannot name; its images are not committed.
- Nothing ran in Firefox or Safari, on a phone, Windows, macOS or Android.
