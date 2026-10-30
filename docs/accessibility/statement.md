---
description: "Accessibility conformance statement of the Autonogrammer web head: target WCAG 2.2 level AA, partially conformant, how it was assessed, what is not verified, and how to report a problem."
---

# Accessibility conformance statement

Status: Draft for publication. The conformance status below is "partially conformant". The automated checks ran on built bundles on 2026-10-29 and found nine defects; looking at the screenshots of the final bundles found a tenth. All ten are fixed, and the final runs report no error finding; the first screen reader run, a real device and a Windows High Contrast check have not happened.

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
| Perceivable | Partially conformant | axe-core 4.14.0 (WCAG 2.0 to 2.2 A and AA tags and best practices) reported no violation on the 30 Demo states and the 16 states of the real bundle in the final runs of 2026-10-29 ([evidence](../runbooks/evidence/web-accessibility-2026-10-29/README.md)). Reflow at 320 px and 400 percent, text spacing, forced colours, increased contrast, reduced motion and the dark scheme have scripted checks with zero error findings in those runs; wider spacing no longer hides text, and warnings remain for a Kanban board that scrolls sideways (K8) and icon-only buttons without a border under increased contrast (K10). Colour pairs are computed from tokens, not measured on pixels; forced colours were seen through emulation only (K11). Time-based media does not occur. Whether colour is the only carrier of a status was not evaluated. |
| Operable | Partially conformant | A scripted keyboard walk, a focus-visible and focus-not-obscured test, a dialog test, a skip-link and first-load-focus test, a target-size test and a route-focus test ran with zero error findings after five defects were fixed: focus dropped when "Jump to latest" disappeared, focus lost after choosing an agent, an agents drawer taller than a short screen, an unnamed scrollable text region, a composer focus indicator drawn only as a colour (known issues F4 to F7, F2). Open: K2 (Tab while the slash list is open, not yet confirmed), K9 (the Stop button goes away when a run ends), K12. The [manual keyboard script](manual-keyboard-test.md) has not been carried out. |
| Understandable | Partially conformant | Page language is set (`lang="en"`); titles, headings, the connect form errors and the route announcements were checked (an announcer that lagged one navigation behind was fixed, F1). Consistency, error prevention and redundant entry were not evaluated. |
| Robust | Partially conformant | axe name, role and value rules are clean in the final runs; one pair of attributes that axe can only report as "needs review" was removed (F8); status messages are live regions covered by unit tests and a scripted announcer check. Behaviour with real assistive technology was not tested. |

## Assessment approach

Self-assessment by the project, on 2026-10-29, with these methods:

1. Automated: axe-core 4.14.0 (pinned, hash-checked) on every route and reachable state, with the tags `wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa`, `wcag22aa` and `best-practice`, in headless Chrome driven over the DevTools protocol.
2. Scripted checks written for the criteria axe cannot decide: titles, headings, landmarks, route announcements, the keyboard walk, focus visibility and obscuring, dialogs, reflow, text spacing, target size, forced colors, increased contrast, reduced motion, dark scheme and the connect form (`scripts/web-smoke/a11y`).
3. Code review of the Razor and CSS sources against the criteria.
4. Unit tests of names, live regions, contrast ratios of the colour tokens and the accessibility style sheet.

Not used: testing with a screen reader or any other assistive technology (a [plan](screen-reader-test-plan.md) exists and its result table is empty), testing with users who have disabilities, browsers other than Chrome, a mobile device or the desktop app. The [manual keyboard script](manual-keyboard-test.md) and the [reduced motion and forced colors guide](reduced-motion-and-forced-colors.md) have not been carried out by a person yet.

## Known limitations

- Not tested with NVDA, JAWS, VoiceOver, TalkBack or Orca. Announcements (route changes, live regions, alerts) are verified in the DOM only.
- Checks are automated and ran in headless Chrome 153 on Linux; the gate repeats them, but no person has used the web head with a keyboard only, a screen reader, a magnifier or speech input for this statement.
- Open issues: K2, K3, K7 to K12 ([known issues](known-issues.md)); K1 is cleared.
- States the Demo cannot reach are not covered by axe: an empty Kanban board, the Kanban or Agents error state, the loading state of the agent list, and the real engine's output.
- The static server of the real-bundle smoke has no rewrite for deep links, so direct loads of `/kanban`, `/sessions/<id>`, `/settings` and `/about` are checked on the Demo bundle only.
- The terminal (xterm.js, a vendored third-party component) draws into a canvas; axe cannot measure its colour contrast and its screen reader behaviour is unverified.
- Contrast of the colour tokens is computed, not measured on rendered pixels; background images and gradients are not considered by the scripted focus-ring contrast check.
- Language of parts (3.1.2): text the agent writes in other languages is not marked up.
- Criteria not evaluated at all are marked "Not evaluated" in the table; they are not claimed to pass.

## Feedback

If something does not work for you, tell us through the repository's issue tracker: use the bug report form that SUPPORT.md describes, say which page you were on, which browser and assistive technology you use, and what happened. The About view of the app links to the same place. Do not paste tokens, keys or the contents of private files into an issue (see SECURITY.md).

## How verified

- The final runs of the scripted checks on the Demo and the real bundle on 2026-10-29 are kept as reports with counts in [`docs/runbooks/evidence/web-accessibility-2026-10-29/`](../runbooks/evidence/web-accessibility-2026-10-29/README.md): zero error findings on both halves in their final runs, with 12 and 11 warnings listed there. The conformance table cites each check by name; unit tests of names, live regions, colour pairs and the accessibility style sheet ran in the same session (`tests/Autonogrammer.UI.Tests`).
- The first runs found nine defects and a set of false findings of the checks; both are listed in the [known issues](known-issues.md) and the evidence README, with the fix and the guard of each.
- The statement was not reviewed by an accessibility specialist and is not a third-party audit.
