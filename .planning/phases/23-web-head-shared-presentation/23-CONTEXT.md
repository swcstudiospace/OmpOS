# Phase 23: Web Head on the Shared Presentation Layer, discuss record

Recorded 2026-10-24 by the gsd-autonomous run (graph ut-mvm12su0-1fae51f6). Milestone v0.10.0. Status: BLOCKED on external items. Criteria 1, 4 and 5 are met.
Depends on Phases 20 and 21 (complete). Mac channel: no. UI hint: yes.

## Goal and criteria (ROADMAP.md lines 442 to 453)

Goal: the Blazor WebAssembly web head runs on the shared presentation layer and keeps Vercel static hosting, with the G-WEB measurement recorded and repeated on each Avalonia release.

1. The web head consumes the shared presentation layer, and it builds and passes its tests in the gate.
2. The axe run and the CSP check pass on the deployed static build. Verify: an axe run in the gate, and a browser console check on the Vercel URL with zero CSP violations.
3. Vercel serves the Blazor WebAssembly publish output unchanged, as static files.
4. The G-WEB re-measurement is recorded with its three measurements, and it is re-runnable from the repository.
5. G-WEB is repeated on each Avalonia release. One dated entry per release.

## Current evidence

- Criterion 1: met. The web tests pass in the gate (2245 passed on 8e0c5e2).
- Criterion 2: blocked on the deployed origin. The axe evidence exists from the web smoke (scripts/web-smoke, axe-core 4.14.0). It is not in the dotnet gate.
- Criterion 3: blocked on the same deploy.
- Criterion 4: met. Re-run 2026-10-24 with tools/gweb/measure.sh. Counts match 2026-10-22 (docs/runbooks/evidence/gweb-remeasure-2026-10-24/README.md).
- Criterion 5: met for the pinned release. Dated entries 2026-10-21, 2026-10-22 and 2026-10-24.

## Verified paths

- apps/OmpOS.Web/
- scripts/web-smoke/smoke.mjs (axe run and browser smoke)
- scripts/vercel/ (build-time CSP and origin handling)
- tools/gweb/measure.sh and tools/gweb/cdp-ax.mjs (tracked)
- docs/runbooks/hosting.md (deploy checklist, OMPOS_API_ORIGIN)
- docs/runbooks/local-dev.md
- docs/decisions/ADR-0004-cross-platform-ui.md

## Edge cases

- The origin is unset: the deploy and CSP checks cannot run, and they must be recorded as blocked, not passed.
- CSP connect-src allows the production origin only, with no wildcard (ROADMAP Phase 14 criterion 1).

## Grey areas

- G23-1, material (adds Node and Chrome to the gate). Question: run the axe check as a gate step, so criterion 2 "in the gate" is met? Default: no. The dotnet gate stays unchanged, and scripts/web-smoke is the recorded axe evidence. Accepted: a gate or CI step runs axe, and it requires Node 22 or later and Chrome on the runner.
- G23-2, decided, immaterial: criterion 4 evidence is the 2026-10-24 run. The README records the command.

## Automated and manual

- Criterion 2 axe run: automated through scripts/web-smoke. A gate step is automated only if G23-1 is accepted.
- Criterion 2 console check and criterion 3: external and manual, on the deployed URL.

## Pauses

- GREY_AREA: G23-1, batched with the other phases.
- BLOCKER: criteria 2 and 3. Minimal action: set the production API origin (OMPOS_API_ORIGIN) and resume Railway deploys (docs/runbooks/hosting.md), or record the deploy as out of scope.
