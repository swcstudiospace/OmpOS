# Phase 23: Web Head on the Shared Presentation Layer, Summary

**Status: partially complete.** The web head consumes the shared presentation layer and its tests pass. The deployed checks on Vercel, a reproducible G-WEB measurement and the per-release repeat are open.

Mac channel: No. The accessibility check and the browser smoke run in headless Chromium on the Linux host.

## Success criteria (ROADMAP Phase 23)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Web head consumes the shared presentation layer; builds and passes tests | Met | OmpOS.Web reaches the presentation layer through OmpOS.UI; web tests pass at gate 9e5913d. SelectedId binding fixed (@_selectedId) |
| 2 | axe and CSP checks pass on the deployed static build | Blocked | Vercel deployed smoke BLOCKED: OMPOS_API_ORIGIN unset; Railway deploys paused |
| 3 | Vercel serves the publish output unchanged | Blocked | Same deployed smoke blocker as criterion 2 |
| 4 | G-WEB measurement recorded and re-runnable from the repository | Partial | Measurements in docs/runbooks/evidence/avalonia-spike-2026-10-21/ (browser-baseline and browser-named AX summaries, desktop AT-SPI2 files). The spike project is not in the repo, so the run is not repeatable from the repository |
| 5 | G-WEB repeated on each Avalonia release | Partial | One dated entry, 2026-10-21 (docs/runbooks/evidence/avalonia-spike-2026-10-21/README.md). No further entry recorded |

## Open items

- Set the production API origin (OMPOS_API_ORIGIN) and resume Railway deploys, then run the axe, CSP and Vercel smoke checks. Owner: owner. Route: docs/runbooks/hosting.md.
- Add the G-WEB spike project or a repository command that reproduces the three measurements. Owner: engineering. Route: docs/runbooks/evidence/avalonia-spike-2026-10-21/README.md.
- Avalonia.Browser stays out of the web head until a recorded G-WEB pass. Owner: owner decision. Route: docs/decisions/ADR-0004-cross-platform-ui.md.

## Commits

- 9e5913d: gate-green HEAD (web head on the presentation layer, gate 1596 passed)
