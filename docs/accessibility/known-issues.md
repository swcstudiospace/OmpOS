---
description: "Accessibility defects, suspected defects and coverage gaps found while reading the Autonogrammer web head sources, with file, line and success criterion."
---

# Accessibility known issues

Status: Open. K1 is a suspected defect that the `first-load-focus` check will confirm or clear on its first run; none of these issues was reproduced in a browser by the author.

Last verified: 2026-10-29

These were recorded while writing the checks in `scripts/web-smoke/a11y`. The web code was not changed by that work. Lines refer to the tree at the commit that contains this file.

## Suspected defects

### K1: focus may be on the page heading after load, so the first Tab skips the skip link

- Criterion: 2.4.1 Bypass Blocks (and 2.4.3 Focus Order).
- Where: `apps/Autonogrammer.Web/App.razor:12` renders `<FocusOnNavigate RouteData="@routeData" Selector="h1" />`; the heading it selects is `src/Autonogrammer.UI/Components/Shell/WorkbenchView.razor:51` (`<h1 class="autonogrammer-sr-only" tabindex="-1">`). The same pair exists for the other pages (`ConnectView.razor`, `AboutView.razor`, `NotFoundView.razor`).
- Why it is suspected: Blazor's `FocusOnNavigate` also runs for the first page that renders. A focused heading makes the browser start sequential navigation after it, so the first Tab reaches the first control inside the main region and the skip link (which comes earlier in the document) is never reached.
- How it is detected: `scripts/web-smoke/a11y/keyboard.mjs`, `checkFirstLoadFocus`, which loads the app, presses Tab without blurring or scrolling, and reports `h1-focused-on-first-load` and `first-tab-misses-skip-link` with the element that holds focus. The check is not relaxed: the smoke fails while this stands. The earlier smoke step (`demoSkipLinkFirstInTabOrder`) blurs the active element first and therefore does not show it.
- Not applied: a fix belongs to the web shell (for example, focus the heading only after an in-app navigation, not on the first load).

### K2: while the slash command list is open, Tab does not move focus

- Criterion: 2.1.2 No Keyboard Trap (the exit is Escape, a standard key, so this is a risk to confirm, not a certain failure).
- Where: `src/Autonogrammer.UI/wwwroot/scripts/autonogrammer-keys.js:54` cancels ArrowUp, ArrowDown and Tab in the composer while `data-slash-open="true"`; the list handles the keys in `Composer.razor` (`HandleKeyDown`, `HandleSlashPaletteKey`).
- To confirm: [manual keyboard script](manual-keyboard-test.md), flow 3, step 4: record what Tab does and whether the user is told how to leave (Escape).

## Coverage gaps (not defects)

### K3: the not-found page has no shell landmarks

`apps/Autonogrammer.Web/Pages/NotFound.razor:3-6` renders a `main` with one heading and one link, outside the shell: no banner, navigation, footer or skip link. The routes check expects only `main`, one `h1` and a title for this page. Related criteria for a page set: 3.2.3 Consistent Navigation and 3.2.6 Consistent Help (both "Not evaluated").

### K4: the real-bundle smoke has no SPA rewrite

`scripts/web-smoke/run-smoke.sh:149` serves the published web head with `python3 -m http.server`, which answers 404 for `/kanban`, `/sessions/<id>`, `/settings` and `/about`. The direct-load route checks therefore run on the Demo bundle only (`scripts/web-demo/serve-demo.sh` rewrites to `index.html`). The real bundle is covered for in-app navigation.

### K5: states the Demo cannot reach

axe and the other state checks do not see: an empty Kanban board (`KanbanView.razor`, `BoardState.Empty`), the Kanban and agent-list error states, the loading states, the Settings close prompt, the review panel open, the steering box on a card, and every output of a real engine. They are listed as "Not evaluated" in the [conformance table](wcag-2.2-conformance.md).

### K6: accessible names that replace visible text

`src/Autonogrammer.UI/Components/Rail/AgentRail.razor:97` (`aria-label="@RowName(row)"`) and `src/Autonogrammer.UI/Components/Kanban/KanbanCardView.razor:14` (`aria-label="@card.SpokenName"`) give the button a composed name instead of its visible text. Criterion 2.5.3 Label in Name needs the visible text to be part of the name; `RowName` builds the name from the title, the time and the status, which are the visible parts, so it probably holds. The `label-in-name` check reports a warning when it does not; confirm on a run.

## How verified

- Each location above was read on 2026-10-29 at the line given. Nothing was reproduced in a browser: the web head could not be built in the environment where this was written.
- The Tab-walk machinery that K1 relies on was exercised on the fixture page of `scripts/web-smoke/a11y/fixture-selftest.mjs`; `checkFirstLoadFocus` itself was not run, neither on the fixture nor on the app.
