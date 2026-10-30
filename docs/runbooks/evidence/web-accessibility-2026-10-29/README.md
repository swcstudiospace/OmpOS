---
description: "Evidence of the scripted accessibility checks run against the built Demo and real web bundles on 2026-10-29: what ran, the counts, the findings that remained and the findings that led to fixes."
---

# Web accessibility: recorded runs of the scripted checks

Status: Recorded 2026-10-29. Automated checks in headless Chrome 153.0.8010.36 on Linux x64, axe-core 4.14.0 (pinned and hash-checked by `scripts/web-smoke/axe-core-4.14.0.sha256`). No screen reader, no real device, no browser other than Chrome.

Last verified: 2026-10-29

## What ran

Two kinds of runs, on bundles built from the tree of the code under test:

| Run | Commit | What | Files |
|-----|--------|------|-------|
| Final smoke | `d0d0b879` (the final code) | `bash scripts/web-smoke/run-smoke.sh --a11y-report DIR`, the web smoke step of the gate, with every accessibility group on both halves: the Demo bundle (`scripts/vercel/build-demo.sh`) served by `scripts/web-demo/serve-demo.sh`, and the real bundle (`scripts/vercel/build-web.sh`) against the in-memory API in Development. Both halves built fresh by the script. Exit 0 | `final-smoke-d0d0b879/` |
| Earlier runs | `eece6e52` (Demo), `f26f66cb` (real bundle), and older bundles built before `2493ef84` | `scripts/web-smoke/a11y/index.mjs` by hand on the same kinds of bundles, kept as the history that led to the fixes | `demo/`, `api/` |

The groups are described in [`docs/accessibility/README.md`](../../../accessibility/README.md): routes (direct loads and in-app), keyboard (first-load focus, walk, drawer, dialog), axe on every state, media (forced colours, increased contrast, reduced motion, dark scheme), reflow, target size, text spacing, and for the real bundle also the connect screen. The smoke runs them as assertions named `a11y<Group>`.

## Result of the final smoke (commit `d0d0b879`)

| Half | Assertions | Error findings | Warnings | Counts |
|------|------------|----------------|----------|--------|
| Demo (`final-smoke-d0d0b879/demo/a11y-report.json`) | 19 of 19 passed (the 10 legacy Demo assertions and the nine accessibility groups) | 0 | 14: seven `wide-content-in-undeclared-container` (K8), four `icon-button-without-boundary` (K10), one `backward-path-set-differs` (K12), two `app-loaded-on-retry` | 162 keyboard stops; 30 states with 30 axe runs; 16 media states (138 controls under forced colours, 153 under increased contrast); 45 reflow views; 1935 targets measured in 30 states; 28 text-spacing views |
| API (`final-smoke-d0d0b879/api/a11y-report.json`) | 20 of 20 passed | 0 | 13: seven `wide-content-in-undeclared-container`, four `icon-button-without-boundary`, one `backward-path-set-differs`, one `app-loaded-on-retry` | connect screen; 100 keyboard stops; 16 axe states with 18 axe runs; 16 media states (102 controls under forced colours); 26 reflow views; 420 targets in 16 states; 18 text-spacing views |
| Size budget | passed | | | `final-smoke-d0d0b879/size-budget-d0d0b879-2026-10-29.json` |

The Demo half also records nine axe results it could not decide (`axe-incomplete-color-contrast`, information, not a failure of the Demo assertions `demoAxeNoViolations` and `demoAxeNoIncomplete`, which apply to the legacy runs of the states).

Three of the loads of a check group did not render within 150 s and rendered on the second load (all at 1440 by 900, the first load of a group): the smoke reports them as the warning `app-loaded-on-retry`. The host has 8 cores and was shared with other sessions (the load averages read 20.9, 32.8 and 36.3 at 17:26 UTC, just after the fourth gate run); OQ-191 asks whether load is the whole cause. Skipped by design and logged: direct loads of `/kanban`, `/sessions/<id>`, `/settings`, `/about` and an unknown path on the real bundle (its static server has no SPA rewrite, K4).

The fourth run of the whole gate, on `561a83b5`, ran the same smoke step and failed it: five first loads of a group (four on the Demo half, one on the real bundle) did not render within 150 s, and the checks had no second load then (`../bootstrap-gate-2026-10-29-561a83b5-smoke/`). The checks now load a second time; the final smoke is the run above.

## Earlier runs

| Half | Bundle | Error findings | Notes |
|------|--------|----------------|-------|
| Demo (`demo/a11y-report.json`) | `eece6e52` | 0 | 12 warnings; 162 keyboard stops, 1930 targets |
| Demo (`demo/a11y-report-before-f9.json`) | before `2493ef84` | 0 | the bundle without the F9 change |
| API (`api/a11y-report.json`) | `f26f66cb` | 1 | the not-found page did not render within 20 s in the target-size group, a transient fault; the group re-run on the same bundle is clean (`api/a11y-report-target-size-rerun.json`: 16 states, 321 targets) |
| API (`api/a11y-report-before-f9.json`, `api/a11y-report-routes-rerun-before-f9.json`) | before `2493ef84` | 2 | both errors were a fault of the check (`browser Back: the address did not become /` and `browser Forward`: the check assumed an agent row); the check was corrected and its route group re-run: 0 errors |

After the transient fault the checks give a state one more attempt from the start page and report a second-attempt success as the warning `state-reached-on-retry`; failing twice is still the error `state-unreachable`.

## What the runs found

The first runs reported 69 error findings on the Demo half and 15 on the API half. After triage they were nine defects of the web head (F1 to F9, fixed in `bd42c17a` and `2493ef84`, listed in [known issues](../../../accessibility/known-issues.md), section 1) and a set of false findings of the checks themselves (corrected in `scripts/web-smoke/a11y`, with self-tests): text visually hidden by design read as clipped, a card around a field was not accepted as the field's focus ring and boundary, a focus scroll under the sticky bar read as an undersized target, a control present in one walk direction only read as a different Shift+Tab path, and Back and Forward assumed an agent row.

Two defects were found by looking at screenshots, not by a check. F9: under forced colours a primary button was drawn with a highlight fill and, in a palette whose highlight is not opaque, the browser drew a plate over its label (`screenshots/demo-forced-colors-settings-dialog-before-f9.png`; after: `screenshots/demo-forced-colors-settings-dialog.png`, `screenshots/demo-forced-colors-new-conversation-after-f9.png`). F10: the close control of a selected agent row covered the end of its time (`screenshots/api-forced-colors-selected-row-before-f10.png`, an overlap of 24 by 10 px measured in the browser); the fix is one rule in `rail.css`, measured on an injected rule (`screenshots/api-forced-colors-selected-row-after-f10.png`: no overlap, the row keeps its height) and then seen in the real bundle of the final smoke (`screenshots/api-forced-colors-agents.png`: "3 min ago" is whole, left of the control).

The hosted run against the real omp (`docs/runbooks/evidence/hosted-web-2026-10-29/`) found a product defect no accessibility check covers, an unsupported-content block after every turn; it is fixed and guarded by the browser run. The full gate found a real race of the theme switch: a saved theme read after a click overwrote the click. It showed up as a failing test under load and is fixed with a regression test that fails on the old component.

## Files

| File | What it holds |
|------|---------------|
| `final-smoke-d0d0b879/` | The reports of both halves, the smoke result files and logs, and the size budget result of the final smoke |
| `demo/`, `api/` | The earlier reports named above |
| `screenshots/` | `demo-*` and `api-forced-colors-agents.png`: the final smoke's Demo and real bundles under emulated forced colours (agents, Settings), increased contrast and the dark scheme. The F9 and F10 images named above |

The first-run reports (69 and 15 error findings) were not kept; their findings are the content of section 1 of the known issues.

## What this does not show

- Behaviour with a screen reader, speech input, a switch device, a phone or a tablet.
- A real Windows High Contrast theme (the palette of the emulation differs; K11).
- Contrast measured on rendered pixels: pairs come from tokens and computed styles, and axe reports the ones it cannot measure as incomplete (listed as information in the reports).
- Anything the Demo cannot reach (K5), and anything a real engine prints.
- Overlaps between controls and text: no check compares their boxes (K14); F10 was found by eye.
