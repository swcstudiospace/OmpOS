---
description: "Index of the Autonogrammer accessibility evidence: the automated WCAG 2.2 AA checks of the web smoke, the conformance statement, the criterion table, the manual keyboard and screen-reader scripts, and the known issues."
---

# Accessibility evidence

Status: Written 2026-10-29 and updated the same day. The automated checks described here ran against a built Demo bundle and a built real bundle (see "How verified"); their final runs report zero error findings, after ten defects of the web head (nine from the first runs, one from a screenshot of the final bundles) and a set of false findings of the checks were fixed. No person has done the manual scripts.

Last verified: 2026-10-29

Applies to: the Blazor web head (`apps/Autonogrammer.Web`) and the shared UI library (`src/Autonogrammer.UI`) at the commit that contains these files.

The program target is WCAG 2.2 level AA. This folder holds the evidence a conformance claim needs and says plainly which parts are not verified.

## Documents

| Document | What it is |
| --- | --- |
| [Accessibility conformance statement](statement.md) | Standard, scope, status per principle, assessment methods, known limitations, how to give feedback. |
| [WCAG 2.2 conformance table](wcag-2.2-conformance.md) | One row for each of the 55 Level A and AA success criteria with a status, an evidence pointer and a note. |
| [Manual keyboard test script](manual-keyboard-test.md) | Keyboard-only steps and expected results for the critical flows. |
| [Screen reader test plan](screen-reader-test-plan.md) | The assistive-technology matrix, flows and expected announcements. The result table is empty: NOT YET RUN. |
| [Reduced motion and forced colors](reduced-motion-and-forced-colors.md) | How to check those modes by hand on Windows, macOS and Linux. |
| [Known issues](known-issues.md) | Defects and risks found while reading the code, with file, line and criterion. |

## The automated checks

They live in `scripts/web-smoke/a11y/` and run at the end of both halves of the web smoke (`scripts/web-smoke/run-smoke.sh`): the Demo bundle (no API, no sign-in) and the real bundle against the in-memory API. Every check returns structured findings; a finding of severity `error` fails the smoke, a `warning` is printed and written to the report. The pinned axe-core 4.14.0 engine and its SHA-256 check are the ones the smoke already used.

| Check group (assertion) | What it asserts | Success criteria |
| --- | --- | --- |
| `routes` (`a11yRoutes`) | For `/`, `/kanban`, `/sessions/<Demo id>`, `/settings`, `/about` and an unknown path (Demo bundle: direct loads): HTTP 200, a non-empty, non-boot, unique title, exactly one visible `h1`, the landmarks once. For in-app navigation with the real controls (both bundles): the title changes, the route announcer text equals the title, focus goes where the design puts it. | 2.4.2, 2.4.3, 1.3.1, 4.1.3 |
| `first-load-focus` (`a11yFirstLoadFocus`) | After a fresh load the first Tab lands on the skip link. | 2.4.1 |
| `keyboard` (`a11yKeyboard`) | Tab through the whole Agents view at 1440x900 and 320x568: every stop has an accessible name and a role, a visible focus indicator (outline of 2 px, 1 px in forced colors, or a box-shadow ring, at 3:1), is inside the viewport and is not covered at its centre; Shift+Tab returns along the same path; no loop; the narrow drawer opens from the keyboard and closes with Escape. | 2.1.1, 2.1.2, 2.4.1, 2.4.3, 2.4.7, 2.4.11, 2.5.3, 4.1.2 |
| `dialog` (`a11yDialog`) | The Settings dialog: focus moves inside, Tab and Shift+Tab stay inside, Escape closes it, focus returns to the opener. | 2.1.2, 2.4.3 |
| `media` (`a11yMedia`) | Under `forced-colors: active` and `prefers-contrast: more` every text field keeps a border and every button a border or a label; under `prefers-reduced-motion: reduce` no transition or animation longer than 0.01 s; under `prefers-color-scheme: dark` the page darkens, body text keeps 4.5:1 and axe is clean. Screenshots for a human reviewer. | 1.4.3, 1.4.11, 2.3.3 (AAA, kept as a design rule), 2.4.7 |
| `reflow` (`a11yReflow`) | No sideways scroll at 320x568, at 320x256 with a device pixel ratio of 4 (a 1280x1024 window at 400 percent) and at 1280 with a ratio of 4; wide content only inside declared scroll regions. | 1.4.10 |
| `text-spacing` (`a11yTextSpacing`) | With line height 1.5, letter spacing 0.12em, word spacing 0.16em and paragraph spacing 2em, no text container starts to clip and no sibling text boxes start to overlap. | 1.4.12 |
| `target-size` (`a11yTargetSize`) | Every visible interactive element is 24x24 CSS px or has 24 px of free space around its centre. | 2.5.8 |
| `axe` (`a11yAxe`) | axe-core with the tags `wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa`, `wcag22aa` and `best-practice` on every route and reachable state: default, running, slash palette, approval pending, completed with Changes, failed, closed, terminal, new conversation, Kanban, About, Settings, Settings Models, unknown session, unknown page. Zero violations of impact serious or critical; the rest are listed with their node target. | 1.x to 4.x (automatable subset) |
| `connect` (`a11yConnect`, real bundle only) | The connect screen of an owner-token deployment, reached by answering `GET /auth/config` and `GET /workspace` inside the browser (the API is untouched): redirect, title, h1 with focus, the labelled password field, Tab order, empty and rejected token announced, the typed value cleared. | 1.3.5, 2.4.3, 3.3.1, 3.3.2, 4.1.2 |

Focus after an in-app navigation: links and Back or Forward move focus to the new view's `h1`; opening an agent from the rail moves it to the conversation; a view tab keeps focus on the tab (the roving tablist pattern, where the arrow keys continue from it); the Settings dialog takes focus inside itself. The routes check encodes exactly this, so these are not misses.

### Run

```bash
bash scripts/web-smoke/run-smoke.sh --a11y-report output/a11y      # both halves; JSON and screenshots in output/a11y/api and output/a11y/demo
SMOKE_TARGETS=demo DEMO_DIR=output/demo bash scripts/web-smoke/run-smoke.sh --a11y-report output/a11y
node --test scripts/web-smoke/a11y/*.test.mjs                      # the self-tests of the rules; no browser needed
node scripts/web-smoke/a11y/fixture-selftest.mjs                   # the harness against a fixture page with known defects; needs Chrome
```

`AUTONOGRAMMER_SMOKE_A11Y=0` skips the accessibility checks and runs the earlier smoke steps only. To keep a run as evidence, copy the report folder to `docs/runbooks/evidence/` and name the commit in its name.

## How verified

- The 42 Node self-tests of `scripts/web-smoke/a11y/*.test.mjs` pass on 2026-10-29 (`node --test`): colour maths, focus-indicator classification, accessible name and role on a hand-made tree, serialisation of the in-page library, and every rule of the judgement module on hand-made data.
- `scripts/web-smoke/a11y/fixture-selftest.mjs` passed on 2026-10-29 in a local Chrome against its fixture page: the Tab walk, targets, reflow, text-spacing probe, forced-colors and reduced-motion emulation find the defects planted in the fixture.
- Every script passes `node --check`; `run-smoke.sh` passes `bash -n`.
- The checks ran against the built Demo bundle and the built real bundle on 2026-10-29: [`docs/runbooks/evidence/web-accessibility-2026-10-29/`](../runbooks/evidence/web-accessibility-2026-10-29/README.md) holds the final reports and the counts. The selectors that name app elements were exercised, and the first runs needed corrections on both sides: nine defects of the web head ([known issues](known-issues.md), section 1; a tenth was found later in a screenshot) and false findings of the checks.
- NOT verified: anything a person has to judge or hear (screen reader output, meaningful sequence, link purpose, plain language), a real device, a real Windows High Contrast theme, and any browser other than Chrome.
- Recorded earlier result that these checks extend: axe-core 4.14.0 with the tags `wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa` on the Demo bundle, 68 runs, 0 violations, recorded 2026-10-28 in `docs/runbooks/evidence/web-app-2026-10-28/README.md`.
