// Keyboard checks (WCAG 2.1.1, 2.1.2, 2.4.1, 2.4.3, 2.4.7, 2.4.11).
//
//   first-load focus  after a fresh load the FIRST Tab must reach the skip link. If the page moved focus to the h1 on load, the first Tab starts after
//                     the h1 and the skip link is never reached: that is reported as a defect with the element that holds focus, and the check is not relaxed.
//   walk              Tab through the whole Agents view at 1440x900 and 320x568, one probe per stop (name, role, visible focus, not covered), then
//                     Shift+Tab back along the same path; the walk must end by leaving or wrapping the page, with no loop.
//   drawer            at 320 px the agent rail is a drawer: opened from the keyboard, every stop is probed, Escape closes it and returns focus to its button.
//   dialog            the Settings dialog: focus moves inside, Tab and Shift+Tab stay inside, Escape closes it, focus returns to the opener.

import { analyzeDialog, analyzeStops, analyzeWalk, RAIL_COLUMN_MIN_WIDTH } from "./logic.mjs";
import { finding } from "./report.mjs";
import { activateWithKeyboard } from "./routes.mjs";
import { enterState, loadApp, STATES, VIEWPORTS } from "./states.mjs";

const MAX_STOPS = 400;
const WALK_STATE = { demo: "completed-changes", api: "agents" };

/** After a fresh load, the first Tab must land on the skip link. */
export async function checkFirstLoadFocus(page, report, { baseUrl }) {
  await page.setViewport(1440, 900);
  const ctx = { group: "first-load-focus", viewport: "1440x900" };
  if (!(await loadApp(page, baseUrl, report, ctx))) return;
  // No blur and no scroll: this is exactly what a user finds after the page loads.
  const before = await page.lib("probeRouteState");
  await page.key("Tab");
  const after = await page.lib("probeStop");
  report.count("first-load-focus:loads");
  if (!after.left && after.description.includes("autonogrammer-skip-link")) return;
  const at = { state: "first load", viewport: "1440x900", wcag: "2.4.1" };
  if (before.activeIsH1) {
    report.add(finding("first-load-focus", "h1-focused-on-first-load", "error", `focus is already on the h1 when the page has loaded (${before.active}), so the first Tab starts after it and never reaches the skip link`, { ...at, target: before.active }));
  }
  report.add(finding("first-load-focus", "first-tab-misses-skip-link", "error", `the first Tab after the load focused ${after.left ? "nothing (the page)" : after.description}, not the skip link; before the Tab focus was on ${before.active ?? "nothing"}`, { ...at, target: after.left ? undefined : after.description, detail: { before: before.active, after: after.left ? null : after.description } }));
}

/** Tab from the focused element until the focus leaves the page, wraps to the first stop or repeats a stop; returns the stops and whether the walk ended. */
export async function walk(page, direction, first, limit = MAX_STOPS) {
  const shift = direction === "backward";
  const stops = [first];
  const seen = new Set([first.key]);
  for (let step = 0; step < limit; step += 1) {
    await page.key("Tab", { shift });
    await page.waitForCalm(1000);
    const stop = await page.lib("probeStop");
    if (stop.left) return { stops, ended: true };
    if (seen.has(stop.key)) {
      if (stop.key !== first.key) stops.push(stop);
      return { stops, ended: true };
    }
    seen.add(stop.key);
    stops.push(stop);
  }
  return { stops, ended: false };
}

async function startWalk(page, report, options, viewport) {
  await page.setViewport(viewport.width, viewport.height, viewport.deviceScaleFactor);
  const ctx = { group: "keyboard", viewport: viewport.name, kind: options.kind };
  if (!(await loadApp(page, options.baseUrl, report, ctx))) return null;
  const state = STATES.find((s) => s.name === WALK_STATE[options.kind]);
  await page.spaNavigate("/");
  if (!(await enterState(page, state, report, ctx))) return null;
  return ctx;
}

async function walkAgentsView(page, report, options, viewport) {
  const ctx = await startWalk(page, report, options, viewport);
  if (!ctx) return;
  const focusedSkip = await page.evalFn(() => {
    window.scrollTo(0, 0);
    const link = document.querySelector(".autonogrammer-skip-link");
    if (!link) return false;
    link.focus();
    return document.activeElement === link;
  });
  if (!focusedSkip) {
    report.add(finding("keyboard", "skip-link-missing", "error", "the page has no skip link that can take focus", { state: "agents view", viewport: viewport.name, wcag: "2.4.1" }));
    return;
  }
  const firstTabbable = await page.evalFn(() => {
    const selector = "a[href], button:not([disabled]), input:not([disabled]):not([type=hidden]), select:not([disabled]), textarea:not([disabled]), [tabindex]";
    const el = [...document.querySelectorAll(selector)].find((e) => e.tabIndex >= 0 && !e.closest("[inert]") && e.getClientRects().length > 0);
    return el ? `${el.tagName.toLowerCase()}.${el.className}` : null;
  });
  if (!String(firstTabbable).includes("autonogrammer-skip-link")) {
    report.add(finding("keyboard", "skip-link-not-first-in-dom", "error", `the first tabbable element in document order is ${firstTabbable}, not the skip link`, { state: "agents view", viewport: viewport.name, wcag: "2.4.1" }));
  }
  await page.waitForCalm(1000);
  const start = await page.lib("probeStop");
  const forward = await walk(page, "forward", start);
  report.count("keyboard:stops", forward.stops.length);
  // Bring the last stop back to focus, then walk backwards from it.
  const last = forward.stops[forward.stops.length - 1];
  const refocused = await page.lib("focusByKey", last.key);
  let backward = { stops: [], ended: false };
  if (refocused) {
    await page.waitForCalm(1000);
    backward = await walk(page, "backward", await page.lib("probeStop"));
    report.count("keyboard:stops", backward.stops.length);
  }
  const wide = viewport.width >= RAIL_COLUMN_MIN_WIDTH;
  const walkCtx = { ...ctx, state: "agents view", forwardEnded: forward.ended, backwardEnded: backward.ended, requiredLandmarks: wide ? ["header", "nav", "main", "footer"] : ["header", "main", "footer"] };
  report.add(analyzeStops([...forward.stops, ...backward.stops], walkCtx));
  report.add(analyzeWalk(forward.stops, backward.stops, walkCtx));
  if (!refocused) report.add(finding("keyboard", "walk-not-reversible", "error", "the last stop of the forward walk could not be focused again to walk backwards", { state: "agents view", viewport: viewport.name }));
  if (!wide) await walkDrawer(page, report, ctx);
}

/** At a narrow width: open the rail drawer from the keyboard, probe its stops, close it with Escape and check where focus goes. */
async function walkDrawer(page, report, ctx) {
  const state = "agents drawer";
  const opened = await activateWithKeyboard(page, "button[aria-controls='autonogrammer-rail']");
  const open = opened && (await page.waitFor("document.querySelector('.autonogrammer-shell')?.getAttribute('data-drawer') === 'open'", 5000));
  if (!open) {
    report.add(finding("keyboard", "drawer-not-opened", "error", "the Agents panel button did not open the drawer from the keyboard", { state, viewport: ctx.viewport, wcag: "2.1.1" }));
    return;
  }
  await page.waitFor("!!document.activeElement && !!document.activeElement.closest('#autonogrammer-rail')", 3000);
  await page.waitForCalm(1000);
  const first = await page.lib("probeStop");
  const stops = first.left ? [] : [first];
  const seen = new Set(stops.map((s) => s.key));
  let leaked = null;
  for (let step = 0; step < 80; step += 1) {
    await page.key("Tab");
    await page.waitForCalm(1000);
    const stop = await page.lib("probeStop");
    if (stop.left || seen.has(stop.key)) break;
    seen.add(stop.key);
    stops.push(stop);
    if (stop.landmark === "main") leaked = stop.description;
  }
  report.count("keyboard:stops", stops.length);
  report.add(analyzeStops(stops, { ...ctx, state }));
  if (leaked) report.add(finding("keyboard", "drawer-focus-leaks", "error", `focus reached ${leaked} behind the open drawer`, { state, viewport: ctx.viewport, wcag: "2.4.3" }));
  await page.key("Escape");
  const closed = await page.waitFor("document.querySelector('.autonogrammer-shell')?.getAttribute('data-drawer') === 'closed'", 5000);
  if (!closed) {
    report.add(finding("keyboard", "drawer-escape-does-not-close", "error", "Escape did not close the drawer", { state, viewport: ctx.viewport, wcag: "2.1.1" }));
    return;
  }
  const returned = await page.waitFor("document.activeElement && document.activeElement.getAttribute('aria-controls') === 'autonogrammer-rail'", 3000);
  if (!returned) {
    report.add(finding("keyboard", "drawer-focus-not-returned", "error", "after Escape focus did not return to the button that opened the drawer", { state, viewport: ctx.viewport, wcag: "2.4.3" }));
  }
}

/** The Settings dialog as a keyboard user meets it. */
async function checkSettingsDialog(page, report, options) {
  const viewport = VIEWPORTS[0];
  const ctx = await startWalk(page, report, options, viewport);
  if (!ctx) return;
  const opener = ".autonogrammer-rail-settings";
  const dialogSelector = ".autonogrammer-settings[role=dialog]";
  const observed = { opened: false, focusInside: false, stayedInside: true, leaks: [], escapeClosed: false, returnedToOpener: false };
  await activateWithKeyboard(page, opener);
  observed.opened = await page.waitFor(`!!document.querySelector(${JSON.stringify(dialogSelector)})`, 10000);
  if (observed.opened) {
    const inside = `!!(document.activeElement && document.activeElement.closest(${JSON.stringify(dialogSelector)}))`;
    observed.focusInside = await page.waitFor(inside, 5000);
    observed.activeAtOpen = (await page.lib("probeStop")).description ?? "the page";
    const count = await page.evalValue(`document.querySelectorAll(${JSON.stringify(`${dialogSelector} a[href], ${dialogSelector} button:not([disabled]), ${dialogSelector} input:not([disabled]), ${dialogSelector} select:not([disabled]), ${dialogSelector} textarea:not([disabled]), ${dialogSelector} [tabindex]:not([tabindex="-1"])`)}).length`);
    for (const shift of [false, true]) {
      for (let step = 0; step < Math.min(count + 4, 90); step += 1) {
        await page.key("Tab", { shift });
        if (!(await page.evalValue(inside))) {
          observed.stayedInside = false;
          observed.leaks.push((await page.lib("probeStop")).description ?? "the page");
          break;
        }
      }
    }
    report.count("dialog:tab-presses", Math.min(count + 4, 90) * 2);
    await page.key("Escape");
    observed.escapeClosed = await page.waitFor(`!document.querySelector(${JSON.stringify(dialogSelector)})`, 10000);
    if (observed.escapeClosed) {
      observed.returnedToOpener = await page.waitFor(`document.activeElement && document.activeElement.matches(${JSON.stringify(opener)})`, 3000);
      observed.activeAfterClose = (await page.lib("probeStop")).description ?? "the page";
    }
  }
  report.add(analyzeDialog(observed, { ...ctx, state: "settings dialog" }));
}

/** All keyboard checks of a smoke kind. */
export async function checkKeyboard(page, report, options) {
  await checkFirstLoadFocus(page, report, options);
  for (const viewport of VIEWPORTS) await walkAgentsView(page, report, options, viewport);
  await checkSettingsDialog(page, report, options);
}
