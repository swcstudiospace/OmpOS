---
description: "Evidence of the scripted accessibility checks run against the built Demo and real web bundles on 2026-10-29: what ran, the counts, the findings that remained and the findings that led to fixes."
---

# Web accessibility: recorded runs of the scripted checks

Status: Recorded 2026-10-29. Automated checks in headless Chrome 153.0.8010.36 on Linux x64, axe-core 4.14.0 (pinned and hash-checked by `scripts/web-smoke/axe-core-4.14.0.sha256`). No screen reader, no real device, no browser other than Chrome.

Last verified: 2026-10-29

## What ran

`scripts/web-smoke/a11y/index.mjs`, every check group, against two built bundles. The Demo half ran on a bundle built from commit `eece6e52` (`scripts/vercel/build-demo.sh`, 2026-10-29 10:17 UTC), after an earlier run on a bundle built before commit `2493ef84` (`demo/a11y-report-before-f9.json`); the two bundles differ in `a11y.css` only (the primary button under forced colours, defect F9; `thread.css`, `composer.css`, `kanban.css`, `rail.css` and `layout.css` were compared and are identical). The API half ran on a real bundle built from commit `f26f66cb` (`scripts/vercel/build-web.sh`, 13:43 UTC), after earlier runs on a bundle built before `2493ef84` (`api/*-before-f9.json`). The commits after `eece6e52` and `f26f66cb` change the theme switch (a race), one rail rule (F10, below), the checks' state retry, tests and documents. The full web smoke of the gate (`./bootstrap.sh`) builds both bundles from the final commit and repeats every group:

| Half | Bundle and server | Groups |
|------|-------------------|--------|
| Demo | the Demo bundle (`scripts/vercel/build-demo.sh`) served by `scripts/web-demo/serve-demo.sh` | routes (direct loads and in-app), keyboard (first-load focus, walk, drawer, dialog), axe on every state, media (forced colours, increased contrast, reduced motion, dark scheme), reflow, target size, text spacing |
| API | the real bundle (`scripts/vercel/build-web.sh`) against the in-memory API in Development | connect screen, routes (in-app), keyboard, axe, media, reflow, target size, text spacing |

The groups are described in [`docs/accessibility/README.md`](../../../accessibility/README.md). The full web smoke of the gate (`./bootstrap.sh`, `scripts/web-smoke/run-smoke.sh`) runs the same groups as assertions named `a11y<Group>`.

## Result of the final runs

| Half | Error findings | Warnings | Counts |
|------|----------------|----------|--------|
| Demo (`demo/a11y-report.json`) | 0 | 12: one `backward-path-set-differs` (K12), four `icon-button-without-boundary` (K10), seven `wide-content-in-undeclared-container` (K8) | 6 direct and 7 in-app route steps; 162 keyboard stops and 60 dialog Tab presses; 30 states with 30 axe runs; 16 media states (138 controls under forced colours, 154 under increased contrast); 45 reflow views; 1930 targets measured in 30 states; 28 text-spacing views |
| API (`api/a11y-report.json`), bundle of `f26f66cb` | 1: `state-unreachable` for the not-found page in the target-size group, "its screen did not render", a transient fault (see below) | 11: four `icon-button-without-boundary`, seven `wide-content-in-undeclared-container` | connect screen; 6 in-app route steps; 57 keyboard stops and 60 dialog Tab presses; 16 axe states with 18 axe runs; 16 media states (94 controls under forced colours); 26 reflow views; 404 targets in 15 states; 18 text-spacing views |
| API target size, re-run (`api/a11y-report-target-size-rerun.json`) | 0 | 0 | 16 states, 321 targets |

The one error of the API half: the not-found page did not render within 20 s after an in-app navigation, in the target-size group only; the same state rendered in the other groups of the same run and in the re-run of the group, which is clean. It looks like a navigation lost on a loaded host. The checks now give a state one more attempt from the start page and report a second-attempt success as the warning `state-reached-on-retry`, so the failure would not stop a gate run and would still show in the report; failing twice is still the error `state-unreachable`. The first runs of the API half, on the older bundle, had two errors of the check itself (`browser Back: the address did not become /` and `browser Forward`: the check assumed an agent row); the check was corrected and its route group re-run (`api/a11y-report-routes-rerun-before-f9.json`).

Skipped by design and logged: direct loads of `/kanban`, `/sessions/<id>`, `/settings`, `/about` and an unknown path on the real bundle (its static server has no SPA rewrite, K4), and the agent row step on the API half.

## What the runs found

The first runs reported 69 error findings on the Demo half and 15 on the API half. After triage they were nine defects of the web head (fixed in `bd42c17a` and `2493ef84`, listed in [known issues](../../../accessibility/known-issues.md), section 1) and a set of false findings of the checks themselves (corrected in `scripts/web-smoke/a11y`, with self-tests): text visually hidden by design read as clipped, a card around a field was not accepted as the field's focus ring and boundary, a focus scroll under the sticky bar read as an undersized target, a control present in one walk direction only read as a different Shift+Tab path, and Back and Forward assumed an agent row. Looking at the screenshots of the final bundles found a tenth defect, F10: the close control of a selected agent row covers the end of its time (`screenshots/api-forced-colors-selected-row-before-f10.png`, 24 by 10 px of overlap measured in the browser); the fix is one rule in `rail.css`, and `screenshots/api-forced-colors-selected-row-after-f10.png` and the measurement (no overlap, the row keeps its height) were made by injecting that rule into the page of the real bundle, not from a rebuilt bundle.

Between the first and the final run the hosted run against the real omp (`docs/runbooks/evidence/hosted-web-2026-10-29/`) found a product defect no accessibility check covers, an unsupported-content block after every turn; it is fixed and guarded by the browser run.

## Files

| File | What it holds |
|------|---------------|
| `demo/a11y-report.json` | Final Demo run on the bundle of commit `eece6e52`: findings, counters |
| `demo/a11y-report-before-f9.json` | The earlier Demo run, on the bundle built before the F9 change (also 0 errors) |
| `api/a11y-report.json` | API run on the bundle of `f26f66cb`: findings, counters (includes the one transient error) |
| `api/a11y-report-target-size-rerun.json` | Re-run of the target-size group on the same bundle: clean |
| `api/a11y-report-before-f9.json`, `api/a11y-report-routes-rerun-before-f9.json` | The earlier API run on the older bundle and the re-run of its route group |
| `screenshots/` | `demo-*`: the Demo bundle of `eece6e52` under emulated forced colours (agents, Settings), increased contrast and the dark scheme. `demo-forced-colors-new-conversation-after-f9.png`: a close-up of the New conversation button after the F9 fix. `demo-forced-colors-settings-dialog-before-f9.png`: the Settings view before the fix, where the primary buttons show a plate over the label. `api-forced-colors-agents.png`: the real bundle of `f26f66cb` under forced colours (the selected row shows the overlap of F10). `api-forced-colors-selected-row-before-f10.png` and `-after-f10.png`: the selected agent row before and with the F10 rule |

The first-run reports (69 and 15 error findings) were not kept; their findings are the content of section 1 of the known issues.

## What this does not show

- Behaviour with a screen reader, speech input, a switch device, a phone or a tablet.
- A real Windows High Contrast theme (the palette of the emulation differs; K11).
- Contrast measured on rendered pixels: pairs come from tokens and computed styles, and axe reports the ones it cannot measure as incomplete (listed as information in the reports).
- Anything the Demo cannot reach (K5), and anything a real engine prints.
