# Phase 55 Summary: Showing the Web Application

Status (recorded 2026-10-28): criterion 1 met as recorded runs of named tests; criteria 2 and 3 are Partial. The Demo build exists and is hosted at https://ompos-web.vercel.app (Vercel production alias of project `ompos-web` in team `swcstudiospace`; static Demo bundle, simulated engine, no backend, no secret; the repository homepage points to it), recorded 2026-10-28 by the orchestrator and captured with headless Chrome from the live URL with no console errors and no failed requests. The tree records neither the commit nor the Vercel build id, and the evidence folder has 16 of the 68 screenshots of the matrix and no README. No live model turn was run (OD-07).

The record was written after the work. Statuses use: Met, Met (recorded), Partial, Blocked, Not verified.

Recorded totals (2026-10-28, final tree before the last wiring): Web 262 or more passed. The full `./bootstrap.sh` gate on the final commit was running; its result is recorded by the orchestrator in `docs/runbooks/evidence/bootstrap-gate-2026-10-28-*.log`.

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | A Demo build replaces the HTTP handler with an in-browser simulated API answering the real routes, shows a permanent banner, needs no network and keeps `connect-src 'self'`; fixtures validate against `docs/api/openapi.json`; `RenderCspScriptTests` | Met (recorded) | `apps/OmpOS.Web/Demo/DemoApiHandler.cs` and its fixtures; `DemoBanner`; `DemoApiHandlerTests`, `DemoFixturesContractTests` (bodies deserialise strictly into the contract types), `OpenApiCoverageTests` (every operation the client uses is in `docs/api/openapi.json`), `DemoExclusionTests` (Demo types exist only in a Demo build), `RenderCspScriptTests` (`scripts/vercel/render-csp.sh`), all inside the passing Web run. Caveat: fixtures are checked against the contract types and the routes against the OpenAPI paths, not against the JSON schemas of the document |
| 2 | A hosted preview is reachable and recorded with its URL, commit and build id; `curl` of the URL and a headless Chrome screenshot of the hosted page | Partial | URL https://ompos-web.vercel.app recorded 2026-10-28 (orchestrator fact 1: captured with headless Chrome from the live URL, no console errors, no failed requests). The commit and the build id are not recorded anywhere in the tree (no match in `docs/`, `scripts/` or `vercel.ts`): UNVERIFIED: `vercel inspect ompos-web.vercel.app` and `git rev-parse HEAD` of the deployed tree. No `curl` output and no hosted-page screenshot are committed; `scripts/web-demo/capture.mjs` with `URL=https://ompos-web.vercel.app` would write one |
| 3 | A runbook shows how to run the web application locally against the real API; screenshots of every screen in both themes at 1440, 1024 and 390 px committed under `docs/runbooks/evidence/web-app-2026-10-27/`; the evidence README names the method and what is simulated | Partial | `docs/runbooks/web-demo.md` exists (real API, Demo, deployment, tests). The folder is `web-app-2026-10-28` (the ROADMAP names `-10-27`). It holds 16 images: 14 follow the `<L or D><width>-<nn>-<screen>.png` pattern and two are Models views (`M-11-models-cn-coding-plans.png`, `M-12-models-eu.png`). Light 1440: 8 (agents, completed-changes, approval, failed, terminal, slash-palette, kanban, settings-00-general); Dark 1440: 3 (agents, completed-changes, kanban); Light 1024: 1 (agents); Dark 1024: 0; Light 390: 2 (agents, drawer); Dark 390: 0. The smoke records 68 axe runs (every Demo screen in light and dark at three widths), and 14 images are committed against them; the 68 smoke images exist only under `/tmp/shots` and are not in the tree. No README in the folder. The runbook's status line still says "no public deployment exists" |

## What the phase delivered

- A Demo build of the same web head: six scripted agents (one streaming markdown, code, a tool call and a diff; one waiting for approval; one completed with three changed files; one failed; one closed; one omp terminal), a ten-column Kanban board, an 18-page Settings dialog with the 103-row provider directory, steering and changes; the Content-Security-Policy allows only the page's own origin.
- `scripts/vercel/build-demo.sh`, `render-csp.sh`, `vercel.ts` (builds the Demo when `OMPOS_DEMO=1`), `scripts/web-demo/serve-demo.sh` (loopback) and `capture.mjs`.
- A runbook; the hosted Demo; six README screenshots at `docs/assets/screens/web-*.png`.

## Deviations

- The evidence folder is dated 2026-10-28, not 2026-10-27.
- The Demo is the hosted artefact; a hosted real API stays blocked on the sign-in authority (OD-03, ADR-0017).
- Changing the model during a running turn is refused in the Demo with the same 409 the API gives.
- Boot, error and empty rail states cannot be reached in the Demo and are not screenshotted.

## Partial

- Criterion 2: commit and build id not recorded; no curl output or hosted screenshot committed.
- Criterion 3: 14 of the 68 smoke-run screens committed as images (plus two Models images), no evidence README, stale status line in the runbook.

## Blocked (OQ/OD)

- OQ-99 (a labelled Demo hosted publicly on Vercel): the default was taken and the Demo is hosted; the owner has not answered.
- A hosted real API: OD-03.

## Known limits

- The Demo is scripted: no model, no files, nothing leaves the page.
- The 68-run smoke and the first screenshots come from the Wave 5 tree; the hosted bundle was built by the orchestrator from a tree this record cannot name.
- Nothing ran in Firefox or Safari, on a phone, Windows, macOS or Android.
