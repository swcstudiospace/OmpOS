# Phase 23: Web Head on the Shared Presentation Layer, Summary

**Status: partially complete.** Criteria 1, 4 and 5 are met. Criteria 2 and 3 are blocked on the deployed origin (OMPOS_API_ORIGIN) and on the Railway and Vercel deploys. The web head consumes the shared presentation layer, and its tests pass.

Mac channel: No. The accessibility check and the browser smoke run in headless Chromium on the Linux host.

## Success criteria (ROADMAP Phase 23)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Web head consumes the shared presentation layer; builds and passes tests | Met | OmpOS.Web reaches the presentation layer through OmpOS.UI; web tests pass at gate 9e5913d. SelectedId binding fixed (@_selectedId) |
| 2 | axe and CSP checks pass on the deployed static build | Blocked | Vercel deployed smoke BLOCKED: OMPOS_API_ORIGIN unset; Railway deploys paused |
| 3 | Vercel serves the publish output unchanged | Blocked | Same deployed smoke blocker as criterion 2 |
| 4 | G-WEB measurement recorded and re-runnable from the repository | Met | Re-run 2026-10-24 with tools/gweb/measure.sh (docs/runbooks/evidence/gweb-remeasure-2026-10-24/README.md): roleNodes 4, textboxNodes 0 and namedTextboxNodes 0 for stock and named, the same as 2026-10-22. Correction 2026-10-24: the earlier "the spike project is not in the repo" was wrong. tools/gweb is tracked (commit e598e5e) |
| 5 | G-WEB repeated on each Avalonia release | Met for the pinned release | Dated entries 2026-10-21, 2026-10-22 and 2026-10-24 for the Avalonia version pinned in tools/gweb/stock. No later release has been measured |

## Open items

- Set the production API origin (OMPOS_API_ORIGIN) and resume Railway deploys, then run the axe, CSP and Vercel smoke checks. Owner: owner. Route: docs/runbooks/hosting.md.
- No open item remains for G-WEB reproducibility (criterion 4). The repository command is tools/gweb/measure.sh, re-run on 2026-10-24.
- Avalonia.Browser stays out of the web head until a recorded G-WEB pass. Owner: owner decision. Route: docs/decisions/ADR-0004-cross-platform-ui.md.

## Commits

- 9e5913d: gate-green HEAD (web head on the presentation layer, gate 1596 passed)
