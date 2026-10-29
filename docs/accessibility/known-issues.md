---
description: "Accessibility defects the scripted checks found and that were fixed, and the open issues and coverage gaps of the Autonogrammer web head, with the criterion and the evidence for each."
---

# Accessibility known issues

Status: Open. The scripted checks ran against built bundles on 2026-10-29 and found nine defects (F1 to F9, two of them seen in screenshots, not by a check); looking at the screenshots of the final bundles found a tenth (F10). All ten are fixed (section 1). Section 2 lists what is still open. Nothing here was observed with assistive technology.

Last verified: 2026-10-29

Evidence of the recorded runs is in [`docs/runbooks/evidence/web-accessibility-2026-10-29/`](../runbooks/evidence/web-accessibility-2026-10-29/README.md).

## 1 Defects found by the scripted checks and fixed

| ID | Criterion | What was wrong | Fix | Guarded by |
| --- | --- | --- | --- | --- |
| F1 | 4.1.3 Status Messages | After a tab, an agent in the rail or Settings the route announcer said nothing, or the title of the previous view: the browser reports the new address after the render that shows the new title, and the announcer was armed by the address. | `RouteAnnouncer.Arm` is called before the workbench changes the view; the late address change does not arm it again. | a11y/routes (`announcer-mismatch`); `RouteAnnouncerTests`, `WorkbenchRoutingTests` |
| F2 | 4.1.2 Name, Role, Value | The scrollable code of a block, the output of a tool and the before and after text of a file edit took keyboard focus with no name and no role. | Each is a focusable group with a name (`Output of bash`, `Text of the file after the edit`, `csharp code`). | a11y/keyboard (`stop-without-name`); `ToolResultBlockViewTests`, `FileEditBlockViewTests`, `CodeBlockViewTests` |
| F3 | 1.4.12 Text Spacing | Kanban card titles were clamped to two lines, and the conversation title, the slash palette text and the agent titles in the rail were cut with an ellipsis: wider spacing hid words. | They wrap. | a11y/text-spacing (`text-clipped-by-spacing`) |
| F4 | 2.4.7 Focus Visible | The composer drew its focus only as a border colour and a tint. | The card around the prompt takes the same two-pixel ring as every control. | a11y/keyboard (`focus-not-visible`) |
| F5 | 2.4.11 Focus Not Obscured, 1.4.10 Reflow | On a short screen the agents drawer was as tall as its content (`height: auto` on a fixed box with `align-self: start`), so the last agents and Settings lay below the screen. | The drawer has the height between the top bar and the footer and scrolls inside it. | a11y/keyboard (`focus-offscreen`, drawer walk at 320x568) |
| F6 | 2.4.3 Focus Order | The "Jump to latest" control was removed while it had the keyboard focus (focusing it scrolled the view to the end), which dropped the focus to the page and ended the Tab walk before the composer and the footer. | It stays while it has the focus and hands the focus to the transcript when it is used. | a11y/keyboard (landmark reached); `SessionScrollTests` |
| F7 | 2.4.3 Focus Order | After choosing an agent in the rail the focus call ran before the render that shows its thread, so the focus landed on the thread about to be replaced and fell to the page. | The call follows the render. | a11y/routes (`focus-not-moved`); `WorkbenchViewTests` |
| F8 | 4.1.2 Name, Role, Value | axe could only report "needs review" for `aria-controls` next to `aria-haspopup` on the model and option chips and on the composer. | The chips set no `aria-controls`; the textarea sets no `aria-haspopup`. | smoke `demoAxeNoIncomplete`; `ComposerPanelTests` |
| F9 | 1.4.11 Non-text Contrast, 1.4.3 | Under forced colours a primary button was drawn with a highlight fill; in a palette whose highlight is not opaque the browser drew a canvas-coloured plate behind the label and hid it. Found by looking at a screenshot, not by a check. | A thicker highlight border and no fill. | `StyleAccessibilityTests` (system colours only). No check measures label visibility under forced colours: the fix was seen in screenshots of the Demo bundle of the final smoke and of an earlier bundle under emulated forced colours (`docs/runbooks/evidence/web-accessibility-2026-10-29/screenshots/`) |
| F10 | No single criterion; related: 1.4.12 Text Spacing (content hidden by another element), 2.5.8 Target Size (a target over other content) | The 28 px close control of the selected agent row lay over the status line, which is about 16 px high, so with a short title its box covered the end of the relative time. Measured on the real bundle (1440x900): the time's box x 220 to 263, y 278 to 296; the control's x 239 to 267, y 286 to 314; an overlap of 24 by 10 px. Under forced colours, where the control has a visible border, the end of "ago" was hidden; with default colours a click on that part of the time reached the control. Found by looking at a screenshot of the real bundle, not by a check. | The title line of the selected row leaves the room of the close control (`padding-right: var(--autonogrammer-icon-button)`, as the status line already did). Measured with the rule on the real bundle: the time's box x 191 to 235, no overlap, the row keeps its height. | Measured once in the browser with a throwaway script; no check (see K14) |

K1 (suspected: focus on the heading after load makes the first Tab skip the skip link) is cleared: the `first-load-focus` check passed on every recorded run.

## 2 Open issues and coverage gaps

### K2: while the slash command list is open, Tab does not move focus

- Criterion: 2.1.2 No Keyboard Trap (the exit is Escape, a standard key, so this is a risk to confirm, not a certain failure).
- Where: `src/Autonogrammer.UI/wwwroot/scripts/autonogrammer-keys.js` cancels ArrowUp, ArrowDown and Tab in the composer while `data-slash-open="true"`; the list handles the keys in `Composer.razor`.
- To confirm: [manual keyboard script](manual-keyboard-test.md), flow 3, step 4.

### K3: the not-found page has no shell landmarks

`apps/Autonogrammer.Web/Pages/NotFound.razor` renders a `main` with one heading and one link, outside the shell: no banner, navigation, footer or skip link. The routes check expects only `main`, one `h1` and a title for this page. Related criteria for a page set: 3.2.3 Consistent Navigation and 3.2.6 Consistent Help (both "Not evaluated").

### K4: the real-bundle smoke has no SPA rewrite

`scripts/web-smoke/run-smoke.sh` serves the published web head with `python3 -m http.server`, which answers 404 for `/kanban`, `/sessions/<id>`, `/settings` and `/about`. The direct-load route checks therefore run on the Demo bundle only. The real bundle is covered for in-app navigation.

### K5: states the Demo cannot reach

axe and the other state checks do not see: an empty Kanban board, the Kanban and agent-list error states, the loading states, the Settings close prompt, the review panel open, the steering box on a card, and every output of a real engine. They are "Not evaluated" in the [conformance table](wcag-2.2-conformance.md).

### K7: a cold start that the auth gate sends to `/connect`

The first load moves no focus (ADR-0021 D6: the skip link stays the first Tab stop), so a user who opens a gated address lands on `/connect` with the focus on the document and hears the page announced by the browser. Open question OQ-190.

### K8: the Kanban board scrolls sideways in a region that is not a named, focusable region

At 400 percent zoom the columns reach up to 3168 px inside `div.autonogrammer-kanban-columns` (`role="group"`, named), which scrolls sideways. A two-dimensional board may scroll, and its cards are focusable, so a keyboard user can scroll it, but the container is not a `region` with a tab stop. The reflow check reports it as a warning at every width.

### K9: when a run ends, the focused Stop button goes away

The steering controls (Queue, Stop, Send now) exist only while a run is active. A keyboard user who has the focus on one of them when the run ends loses it to the page (Chrome continues from the place the element was, so the next Tab is not lost, but nothing says so). Not guarded by a check: the walk uses a session that is not running.

### K10: icon-only buttons have no border under increased contrast

The close buttons of the agent rows and a few other icon-only buttons draw no border under `prefers-contrast: more` and rely on the icon and the focus ring (warning `icon-button-without-boundary`). WCAG 1.4.11 asks for a boundary only where it is needed to identify the control.

### K11: forced colours were looked at through emulation only

The forced-colors and increased-contrast runs use the browser's emulation, whose palette differs from a Windows High Contrast theme. F9 was visible in a screenshot and in no computed value. Nobody has looked at the web head in a real Windows High Contrast theme.

### K12: the Shift+Tab path is not the Tab path while "Jump to latest" is shown

The control exists only while the view is scrolled away from the end, so the walk forward meets it and the walk backward from the footer does not (warning `backward-path-set-differs`). The order of the stops both walks meet is the same.

### K13: accessible names that replace visible text

`AgentRail.razor` and `KanbanCardView.razor` set an `aria-label` that composes the visible parts of a row or a card. No `label-in-name` finding appeared in the recorded runs, so the visible text is part of each name (criterion 2.5.3); this is a note, not a defect.

### K14: no check compares the boxes of two controls or of a control and text

F10 (a control covering the end of a time) was found by looking at a screenshot of the real bundle. The checks measure each control's size, name, focus ring and contrast, and the page's reflow and text spacing; none compares the box of an interactive control with the boxes of other controls or of text, so an overlap of that kind can only be found by eye. A check that does it needs a rule for what is allowed to overlap (a control inside a field, a badge on an icon), which is why it was not written in this run.

## 3 How verified

- Section 1: each defect was found by the first runs of `scripts/web-smoke/a11y` against built Demo and real bundles on 2026-10-29 (the earlier reports are kept under the evidence folder named above), fixed in commits `bd42c17a` and `2493ef84`, and absent from the web smoke of the final commit, which ran every group on both halves with zero error findings. F9 and F10 were found by looking at screenshots; their fixes were checked the same way, in the emulation, not in a Windows High Contrast theme.
- Section 2: each item was read from the sources or from the reports of the final runs. K2 is not reproduced; K9 and K12 were reproduced with the diagnosis scripts of the session (a focus event log of the Tab press that left the page), not by a kept check.
- What was not done: no screen reader, no real device, no Windows High Contrast, no browser other than Chrome.
