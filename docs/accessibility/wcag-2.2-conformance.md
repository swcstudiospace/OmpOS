---
description: "Criterion-by-criterion table of the 55 WCAG 2.2 Level A and AA success criteria for the Autonogrammer web head: status, evidence pointer and note."
---

# WCAG 2.2 conformance table (Level A and AA)

Status: Draft. No automated check added on 2026-10-29 has a recorded run, so the statuses that depend on one are "Partially supports". Nothing is marked "Supports" unless it is a fact that was read from the source.

Last verified: 2026-10-29

## How to read this table

Statuses:

- Supports: verified as a static fact in the source on 2026-10-29 (the evidence column says where).
- Partially supports: a check, a unit test or a recorded result covers part of the criterion; the note says what is not covered.
- Does not support: a defect is confirmed. None is confirmed at the date above; suspected ones are in [known issues](known-issues.md).
- Not evaluated: no evidence exists yet. This is not a claim that the criterion passes.
- Not applicable: the content the criterion is about does not exist in the web head; the note says how that was established.

Evidence names: `a11y/<group>` is a check group of `scripts/web-smoke/a11y` (see the [index](README.md)); `axe` is axe-core 4.14.0, whose recorded result (Demo bundle, tags `wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa`, 68 runs, 0 violations, 2026-10-28) is in `docs/runbooks/evidence/web-app-2026-10-28/README.md` and whose run now also uses `wcag22aa` and `best-practice` (a11y/axe); other names are test classes in `tests/Autonogrammer.UI.Tests`, read but not re-run for this table. WCAG 2.2 removed criterion 4.1.1 (Parsing), so it is not listed. 55 criteria: 31 at level A and 24 at level AA.

## 1 Perceivable

| SC | Name | Level | Status | Evidence | Note |
| --- | --- | --- | --- | --- | --- |
| 1.1.1 | Non-text Content | A | Partially supports | axe (image-alt, svg-img-alt, button-name); `AccessibilityTests.IconButtonsHaveAccessibleNames`, `A2uiImageAltTests`; a11y/keyboard (`stop-without-name`) | Tools find missing text alternatives, not unhelpful ones. Agent-supplied remote images are not rendered (the smoke counts zero `img` in the probe transcript). |
| 1.2.1 | Audio-only and Video-only (Prerecorded) | A | Not applicable | grep for audio and video elements in `src/Autonogrammer.UI`, `src/Autonogrammer.Presentation`, `apps/Autonogrammer.Web` found none (2026-10-29) | No time-based media. |
| 1.2.2 | Captions (Prerecorded) | A | Not applicable | as 1.2.1 | |
| 1.2.3 | Audio Description or Media Alternative (Prerecorded) | A | Not applicable | as 1.2.1 | |
| 1.2.4 | Captions (Live) | AA | Not applicable | as 1.2.1 | |
| 1.2.5 | Audio Description (Prerecorded) | AA | Not applicable | as 1.2.1 | |
| 1.3.1 | Info and Relationships | A | Partially supports | axe (aria, list, label, th rules); a11y/routes (`h1-count`, `landmark-*`); a11y/connect (field label and description); `ShellRegionTests` | Heading structure below the `h1` and the visual-to-programmatic match of grouped content were not reviewed. |
| 1.3.2 | Meaningful Sequence | A | Not evaluated | | The Tab path is walked (a11y/keyboard); reading order was not reviewed. |
| 1.3.3 | Sensory Characteristics | A | Not evaluated | | |
| 1.3.4 | Orientation | AA | Supports | `apps/Autonogrammer.Web/wwwroot/site.webmanifest` has no `orientation` member and the style sheets lock none (grep 2026-10-29) | Not tested on a rotating device. |
| 1.3.5 | Identify Input Purpose | AA | Partially supports | axe (autocomplete-valid); a11y/connect (`token-field-autocomplete`: `current-password`) | Other inputs (Settings fields, API key fields) were not reviewed for purpose tokens. |
| 1.4.1 | Use of Color | A | Not evaluated | | Status glyphs carry text in the agent list; not reviewed as a whole. |
| 1.4.2 | Audio Control | A | Not applicable | as 1.2.1 | No audio. |
| 1.4.3 | Contrast (Minimum) | AA | Partially supports | axe (color-contrast, recorded clean; pairs axe cannot measure are reported as incomplete and listed as warnings); `AccessibilityTests.TextRolesMeetContrastMinimumOnEverySurface`, `StyleAccessibilityTests`; a11y/media (dark scheme) | The terminal canvas and text over gradients or images are not measured. |
| 1.4.4 | Resize Text | AA | Partially supports | `index.html` viewport meta allows zoom; a11y/reflow at 320x256 with a pixel ratio of 4 | Text-only zoom in a browser was not tested. |
| 1.4.5 | Images of Text | AA | Not evaluated | | |
| 1.4.10 | Reflow | AA | Partially supports | a11y/reflow (320x568, 320x256 at 4x, 1280x1024 at 4x); recorded smoke: no horizontal scroll at 390 px | Code, diffs and the terminal scroll in two dimensions in declared scroll regions, which the criterion allows. |
| 1.4.11 | Non-text Contrast | AA | Partially supports | `AccessibilityTests.FocusRingAndOutlinesMeetNonTextContrastMinimum`, `FilledControlTextPairsMeetContrastMinimum`; a11y/keyboard (focus ring at 3:1); a11y/media (control boundaries); smoke `iconContrastMeetsNonText` | Computed from tokens and from computed styles, not from pixels. |
| 1.4.12 | Text Spacing | AA | Partially supports | a11y/text-spacing | Clipping that exists before the styles (single-line labels with an ellipsis) is reported as a warning. |
| 1.4.13 | Content on Hover or Focus | AA | Not evaluated | | Tooltips (`title`), the model menu and the slash palette are not tested for dismiss, hover and persistence. |

## 2 Operable

| SC | Name | Level | Status | Evidence | Note |
| --- | --- | --- | --- | --- | --- |
| 2.1.1 | Keyboard | A | Partially supports | a11y/keyboard (walk), a11y/routes (tab, link, rail row and Settings button activated with Enter); [manual keyboard script](manual-keyboard-test.md) not yet carried out | Kanban card keys, the review panel and the terminal are not walked by a script. |
| 2.1.2 | No Keyboard Trap | A | Partially supports | a11y/keyboard (`focus-trap`, `walk-no-end`, drawer, dialog) | While the slash palette is open, Tab selects an item in the composer instead of moving focus; Escape closes it ([known issues](known-issues.md), K2). |
| 2.1.4 | Character Key Shortcuts | A | Not applicable | `src/Autonogrammer.UI/wwwroot/scripts/autonogrammer-keys.js`: shortcuts need Ctrl or Command; Enter, Escape and arrow keys act only while the composer or a list has focus (read 2026-10-29) | |
| 2.2.1 | Timing Adjustable | A | Not evaluated | | No UI time limit was found, but it was not searched for systematically. |
| 2.2.2 | Pause, Stop, Hide | A | Not evaluated | a11y/media covers reduced motion only | Streaming transcripts can pause automatic scrolling ("Jump to latest"); other moving content was not reviewed. |
| 2.3.1 | Three Flashes or Below Threshold | A | Not evaluated | | |
| 2.4.1 | Bypass Blocks | A | Partially supports | skip link; smoke `demoSkipLinkFirstInTabOrder`; a11y/keyboard (`skip-link-not-first-in-dom`); a11y/first-load-focus | Suspected defect K1: the first Tab after load may start after the heading. |
| 2.4.2 | Page Titled | A | Partially supports | a11y/routes (`title-missing`, `title-is-boot-title`, `title-duplicate`, `title-unchanged`); `RouteAnnouncerTests` | |
| 2.4.3 | Focus Order | A | Partially supports | a11y/keyboard (walk, backward path, dialog focus return, drawer focus return); a11y/routes (focus after navigation) | |
| 2.4.4 | Link Purpose (In Context) | A | Not evaluated | axe (link-name) only | |
| 2.4.5 | Multiple Ways | AA | Not evaluated | | Views are reachable from tabs, the rail, the footer and addresses; not assessed against the criterion. |
| 2.4.6 | Headings and Labels | AA | Not evaluated | a11y/routes checks that one `h1` exists | Whether headings and labels describe their purpose is a judgement not yet made. |
| 2.4.7 | Focus Visible | AA | Partially supports | a11y/keyboard (`focus-not-visible`); `AccessibilityTests.FocusVisibleRuleUsesTheFocusToken`; a11y/media (forced colors walk) | |
| 2.4.11 | Focus Not Obscured (Minimum) | AA | Partially supports | a11y/keyboard (`focus-obscured`, `focus-offscreen` at 1440x900 and 320x568) | Tests the centre of each focused element; sticky bars on other states are not walked. |
| 2.5.1 | Pointer Gestures | A | Not applicable | no multi-point or path-based gesture code; `pointerdown` is used only to detect a click outside a popup (`autonogrammer-keys.js`) | |
| 2.5.2 | Pointer Cancellation | A | Not evaluated | | Controls use the browser click event. |
| 2.5.3 | Label in Name | A | Partially supports | a11y/keyboard (`label-in-name`, reported as a warning) | Rail rows and Kanban cards set an `aria-label` that composes their visible parts. |
| 2.5.4 | Motion Actuation | A | Not applicable | no use of device motion or orientation events (grep 2026-10-29) | |
| 2.5.7 | Dragging Movements | AA | Not applicable | no drag handlers or draggable elements in the UI sources (grep 2026-10-29) | |
| 2.5.8 | Target Size (Minimum) | AA | Partially supports | a11y/target-size; axe (`target-size`, tag `wcag22aa`) | Inline links in text and disabled controls are exempt by the criterion. |

## 3 Understandable

| SC | Name | Level | Status | Evidence | Note |
| --- | --- | --- | --- | --- | --- |
| 3.1.1 | Language of Page | A | Supports | `apps/Autonogrammer.Web/wwwroot/index.html` line 2: `<html lang="en">`; axe (html-has-lang) | |
| 3.1.2 | Language of Parts | AA | Not evaluated | | Agent output in another language is not marked. |
| 3.2.1 | On Focus | A | Not evaluated | | |
| 3.2.2 | On Input | A | Not evaluated | | |
| 3.2.3 | Consistent Navigation | AA | Not evaluated | | One shell serves every view; not compared across pages. |
| 3.2.4 | Consistent Identification | AA | Not evaluated | | |
| 3.2.6 | Consistent Help | A | Not evaluated | | The problem-report link is in About and About is linked from the footer; the not-found page has no footer. |
| 3.3.1 | Error Identification | A | Partially supports | a11y/connect (empty and rejected token: alert, `aria-invalid`, focus stays in the field); `ConnectViewTests` | Only the connect form is covered; Settings forms are not. |
| 3.3.2 | Labels or Instructions | A | Partially supports | a11y/connect (label, `aria-required`, description); axe (label) | |
| 3.3.3 | Error Suggestion | AA | Not evaluated | | |
| 3.3.4 | Error Prevention (Legal, Financial, Data) | AA | Not evaluated | | Confirmation bars for stopping and closing an agent and the unsaved-changes prompt of Settings exist; not tested here. |
| 3.3.7 | Redundant Entry | A | Not evaluated | | |
| 3.3.8 | Accessible Authentication (Minimum) | AA | Partially supports | a11y/connect (password field, `autocomplete="current-password"`) | No cognitive function test is used. That pasting into the token field works was not tested. |

## 4 Robust

| SC | Name | Level | Status | Evidence | Note |
| --- | --- | --- | --- | --- | --- |
| 4.1.2 | Name, Role, Value | A | Partially supports | axe (aria-*, button-name, input rules); a11y/keyboard (`stop-without-name`, `stop-without-role`); a11y/connect | Names are computed by a simplified algorithm in the script, not by the browser's accessibility tree. |
| 4.1.3 | Status Messages | AA | Partially supports | a11y/routes (announcer equals title); a11y/connect (alert); `LiveRegionTests`, `RouteAnnouncerTests`; smoke "Response complete." | Announcements are verified in the DOM, not heard. |

## How verified

- The 55 rows were written on 2026-10-29 from the WCAG 2.2 list of Level A and AA criteria (31 A and 24 AA, with 4.1.1 removed) and checked against the sources named in the evidence column.
- "Supports" rows rest on a direct read of the named file. "Not applicable" rows rest on a search of the UI sources for the element or event named in the evidence column; the search was a text search, not a review of runtime content.
- Check names were verified to exist in `scripts/web-smoke/a11y/logic.mjs` (their rules are covered by `logic.test.mjs`). The checks themselves have not run against a built bundle, so no row claims a pass of a scripted check.
- The test classes were read by name; they were not executed for this table.
- Not verified: everything marked "Not evaluated", all assistive-technology behaviour, and any browser other than Chrome.
