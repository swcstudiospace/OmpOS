---
description: "Accessibility conformance statement of the Autonogrammer web head: target WCAG 2.2 level AA, partially conformant, how it was assessed, what is not verified, and how to report a problem."
---

# Accessibility conformance statement

Status: Draft for publication; the conformance status below is "partially conformant" and will be revised after the first recorded run of the automated checks and the first screen reader run.

Last verified: 2026-10-29

Date of this statement: 2026-10-29

## Commitment

Autonogrammer is meant to be usable with a keyboard, a screen reader, text zoom up to 400 percent, reduced motion and forced-colors modes, by people who cannot use a mouse or see the screen.

## Standard

Web Content Accessibility Guidelines (WCAG) 2.2, level AA.

## Scope

This statement covers the Blazor WebAssembly web head at the URL where it is deployed and the shared UI library it is built from (`apps/Autonogrammer.Web`, `src/Autonogrammer.UI`, `src/Autonogrammer.Presentation`):

- the connect screen of a deployment that uses an owner token;
- the Agents view (agent list, conversation, composer, tool approvals, Changes review, terminal), the Kanban view, the Settings dialog, About, and the not-found pages;
- the in-browser Demo, which uses the same screens with a simulated engine.

Out of scope: the desktop app (Avalonia, `apps/Autonogrammer.Native`), which has its own checks; the external agent `omp` and anything it prints; the pages of other sites that About and the footer link to; documents in the `docs` folder.

## Conformance status

Partially conformant: some of the content does not yet fully conform, or has not been evaluated. The table follows the four WCAG principles. "Evidence" names the automated check or the recorded result behind the status; the per-criterion detail is in the [WCAG 2.2 conformance table](wcag-2.2-conformance.md).

| Principle | Status | Basis |
| --- | --- | --- |
| Perceivable | Partially conformant | axe-core 4.14.0 (WCAG 2.0 and 2.1 A and AA tags) reported 0 violations on the Demo bundle, 68 runs, recorded 2026-10-28. Colour pairs and focus indicators are held by unit tests in `tests/Autonogrammer.UI.Tests` (`AccessibilityTests`, `StyleAccessibilityTests`). Reflow, text spacing, forced colors, increased contrast and dark scheme have scripted checks (`scripts/web-smoke/a11y`) with no recorded run yet. Time-based media does not occur. Whether colour is the only carrier of a status was not evaluated. |
| Operable | Partially conformant | A scripted keyboard walk, focus-visible and focus-not-obscured test, dialog test, skip-link test, target-size test and route-focus test exist with no recorded run yet. One suspected defect is open: focus may be on the page heading after load, which would make the first Tab skip the skip link ([known issues](known-issues.md), K1). |
| Understandable | Partially conformant | Page language is set (`lang="en"`); titles, headings and the connect form errors have scripted checks with no recorded run yet. Consistency, error prevention and redundant entry were not evaluated. |
| Robust | Partially conformant | axe-core name, role and value rules were clean in the recorded 2026-10-28 run; status messages are live regions covered by unit tests (`LiveRegionTests`, `RouteAnnouncerTests`) and a scripted announcer check. Behaviour with real assistive technology was not tested. |

## Assessment approach

Self-assessment by the project, on 2026-10-29, with these methods:

1. Automated: axe-core 4.14.0 (pinned, hash-checked) on every route and reachable state, with the tags `wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa`, `wcag22aa` and `best-practice`, in headless Chrome driven over the DevTools protocol.
2. Scripted checks written for the criteria axe cannot decide: titles, headings, landmarks, route announcements, the keyboard walk, focus visibility and obscuring, dialogs, reflow, text spacing, target size, forced colors, increased contrast, reduced motion, dark scheme and the connect form (`scripts/web-smoke/a11y`).
3. Code review of the Razor and CSS sources against the criteria.
4. Unit tests of names, live regions, contrast ratios of the colour tokens and the accessibility style sheet.

Not used: testing with a screen reader or any other assistive technology (a [plan](screen-reader-test-plan.md) exists and its result table is empty), testing with users who have disabilities, browsers other than Chrome, a mobile device or the desktop app. The [manual keyboard script](manual-keyboard-test.md) and the [reduced motion and forced colors guide](reduced-motion-and-forced-colors.md) have not been carried out by a person yet.

## Known limitations

- Not tested with NVDA, JAWS, VoiceOver, TalkBack or Orca. Announcements (route changes, live regions, alerts) are verified in the DOM only.
- The automated checks added on 2026-10-29 have no recorded run; until one is recorded, statuses that rest on them are "partially conformant", not "conformant".
- Suspected defect K1: focus on the heading after load, which would defeat the skip link on the first Tab (criterion 2.4.1). To be confirmed by the `first-load-focus` check.
- States the Demo cannot reach are not covered by axe: an empty Kanban board, the Kanban or Agents error state, the loading state of the agent list, and the real engine's output.
- The static server of the real-bundle smoke has no rewrite for deep links, so direct loads of `/kanban`, `/sessions/<id>`, `/settings` and `/about` are checked on the Demo bundle only.
- The terminal (xterm.js, a vendored third-party component) draws into a canvas; axe cannot measure its colour contrast and its screen reader behaviour is unverified.
- Contrast of the colour tokens is computed, not measured on rendered pixels; background images and gradients are not considered by the scripted focus-ring contrast check.
- Language of parts (3.1.2): text the agent writes in other languages is not marked up.
- Criteria not evaluated at all are marked "Not evaluated" in the table; they are not claimed to pass.

## Feedback

If something does not work for you, tell us through the repository's issue tracker: use the bug report form that SUPPORT.md describes, say which page you were on, which browser and assistive technology you use, and what happened. The About view of the app links to the same place. Do not paste tokens, keys or the contents of private files into an issue (see SECURITY.md).

## How verified

- Statuses in the table above were taken from the evidence files named in each row: `docs/runbooks/evidence/web-app-2026-10-28/README.md` for the recorded axe result, the test classes named above (their names were read on 2026-10-29; they were not re-run for this statement), and the check names in `scripts/web-smoke/a11y/`.
- The existence of the scripts and their self-tests was verified on 2026-10-29 (`node --test`, `node --check`, `fixture-selftest.mjs`); see the [index](README.md) for what was and was not run.
- The statement was not reviewed by an accessibility specialist and is not a third-party audit.
