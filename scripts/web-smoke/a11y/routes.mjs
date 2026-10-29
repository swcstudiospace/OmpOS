// Route checks (WCAG 2.4.2, 2.4.3, 1.3.1, 4.1.3).
//
// Direct loads (Demo bundle only: the static server of the real-bundle smoke has no SPA rewrite, so a deep link there is a 404 for a reason that has
// nothing to do with the app): for / , /kanban, /sessions/<Demo id>, /settings, /about and an unknown path the document answers 200, the title is
// non-empty, not the boot title and unique, there is exactly one visible h1, and the landmarks exist once.
//
// In-app navigation (both bundles), with the real controls and the keyboard: the title changes, the route announcer says it, and focus goes where the
// design puts it. Links, Back and Forward move focus to the new view's h1 (or the conversation of a session). A tab keeps focus on itself (the roving
// tablist pattern: the arrow keys continue from it) and the Settings dialog takes focus inside itself; both are listed in
// docs/accessibility/README.md so that nobody mistakes them for a miss.

import { analyzeRoute, analyzeTitleSet } from "./logic.mjs";
import { DEMO_SESSION, loadApp } from "./states.mjs";

const DIRECT_ROUTES = [
  { route: "/", layout: "shell", ready: "!!document.querySelector('main h1') && !document.querySelector('[data-state=\"loading\"]')" },
  { route: "/kanban", layout: "shell", ready: "!!document.querySelector('.autonogrammer-kanban')" },
  { route: `/sessions/${DEMO_SESSION.running}`, layout: "shell", ready: `!!document.querySelector('.autonogrammer-thread[data-session="${DEMO_SESSION.running}"]') && !document.title.startsWith('Session - ')` },
  { route: "/settings", layout: "shell", ready: "!!document.querySelector('.autonogrammer-settings[role=dialog]')" },
  { route: "/about", layout: "shell", ready: "!!document.querySelector('.autonogrammer-about')" },
  { route: "/does-not-exist-a11y", layout: "bare", ready: "!!document.querySelector('.autonogrammer-notfound-page h1')" },
];

const FOCUS_CONDITIONS = {
  h1: "document.activeElement && document.activeElement.tagName === 'H1'",
  conversation: "document.activeElement && document.activeElement.id === 'autonogrammer-conversation'",
  dialog: "!!(document.activeElement && document.activeElement.closest('[role=dialog], [role=alertdialog]'))",
};

/** Focuses a control and activates it with Enter, the way a keyboard user does; false when the control is absent. */
export async function activateWithKeyboard(page, selector) {
  const found = await page.evalFn((sel) => {
    const el = document.querySelector(sel);
    if (!el) return false;
    el.focus();
    return document.activeElement === el;
  }, selector);
  if (found) await page.key("Enter");
  return found;
}

/** Direct loads of every route (Demo bundle). */
async function checkDirectLoads(page, report, baseUrl) {
  await page.setViewport(1440, 900);
  const entries = [];
  for (const spec of DIRECT_ROUTES) {
    const url = new URL(spec.route.slice(1), baseUrl).href;
    const { status } = await page.navigate(url);
    const ready = await page.waitFor(spec.ready, 150000);
    if (ready) await page.waitFor("document.title !== '' && document.title !== 'Autonogrammer'", 10000);
    if (!ready) {
      report.add({ check: "routes", id: "route-not-rendered", severity: "error", message: `${spec.route} did not render within 150 s`, state: `route ${spec.route}`, viewport: "direct" });
      continue;
    }
    await page.settle();
    const snap = await page.lib("probeRouteState");
    entries.push({ route: spec.route, mode: "direct", layout: spec.layout, status, snap });
    report.count("routes:direct");
  }
  for (const entry of entries) report.add(analyzeRoute(entry));
  report.add(analyzeTitleSet(entries));
}

/** The in-app steps. each: how the user moves, where the app should end up, and where focus should be. */
function inAppSteps(kind) {
  const steps = [
    { name: "Kanban tab", selector: "#autonogrammer-tab-kanban", path: "/kanban", expectFocus: ["trigger"], trigger: "autonogrammer-tab-kanban" },
    { name: "About link in the footer", selector: ".autonogrammer-footer-about", path: "/about", expectFocus: ["h1"] },
    { name: "wordmark link", selector: ".autonogrammer-wordmark", path: "/", expectFocus: ["h1"] },
  ];
  const rowSelector = kind === "demo" ? `[data-row-id$="d002"]` : "[data-row-id]";
  steps.push({ name: "agent row in the rail", selector: rowSelector, pathStart: "/sessions/", expectFocus: ["conversation", "h1"], optional: kind !== "demo", session: true });
  steps.push({ name: "browser Back", history: "back", expectFocus: ["h1", "conversation"] });
  steps.push({ name: "browser Forward", history: "forward", expectFocus: ["h1", "conversation"] });
  steps.push({ name: "Settings button in the rail", selector: ".autonogrammer-rail-settings", path: "/settings", expectFocus: ["dialog"] });
  return steps;
}

/** In-app navigation with the keyboard (both bundles). */
async function checkInAppNavigation(page, report, { baseUrl, kind }) {
  await page.setViewport(1440, 900);
  if (!(await loadApp(page, baseUrl, report, { group: "routes", viewport: "inapp" }))) return;
  const entries = [];
  // The browser's session history as the steps build it. Back and Forward lead to the address the history holds, and that differs between the bundles: the
  // in-memory API has no agent, so the agent row step adds no entry and Back leads to the page before it.
  const history = [await page.evalValue("location.pathname")];
  let position = 0;
  for (const step of inAppSteps(kind)) {
    const before = await page.lib("probeRouteState");
    let expected = step.path;
    let acted;
    if (step.history) {
      const target = position + (step.history === "back" ? -1 : 1);
      if (target < 0 || target >= history.length) {
        report.skip("routes", `${step.name}: the history has no entry to go ${step.history} to`);
        continue;
      }
      position = target;
      expected = history[target];
      await page.evalValue(`history.${step.history}(); true`);
      acted = true;
    } else {
      acted = await activateWithKeyboard(page, step.selector);
    }
    if (!acted) {
      if (step.optional) report.skip("routes", `${step.name}: the control is not on the page (the in-memory API has no session yet)`);
      else report.add({ check: "routes", id: "control-missing", severity: "error", message: `${step.name}: ${step.selector} was not found or could not take focus`, state: `in-app ${step.name}`, viewport: "inapp" });
      continue;
    }
    const arrived = await page.waitFor(expected ? `location.pathname === ${JSON.stringify(expected)}` : `location.pathname.startsWith(${JSON.stringify(step.pathStart)})`, 10000);
    if (!arrived) {
      report.add({ check: "routes", id: "route-not-reached", severity: "error", message: `${step.name}: the address did not become ${expected ?? `${step.pathStart}…`}`, state: `in-app ${step.name}`, viewport: "inapp" });
      continue;
    }
    if (!step.history) {
      history.length = position + 1;
      history.push(await page.evalValue("location.pathname"));
      position = history.length - 1;
    }
    // The title is set by a component after the render; the announcer follows it; focus is moved after both.
    await page.waitFor(`document.title !== ${JSON.stringify(before.title)}${step.session || expected?.startsWith("/sessions/") ? " && !document.title.startsWith('Session - ')" : ""}`, 10000);
    await page.waitFor("(document.querySelector('[data-route-announcer]')?.textContent ?? '').trim() === document.title", 5000);
    const focusExpression = step.expectFocus.map((name) => (name === "trigger" ? `document.activeElement && document.activeElement.id === ${JSON.stringify(step.trigger ?? "")}` : FOCUS_CONDITIONS[name])).map((c) => `(${c})`).join(" || ");
    await page.waitFor(focusExpression, 5000);
    const snap = await page.lib("probeRouteState");
    snap.activeIsTrigger = step.trigger ? await page.evalValue(`!!document.activeElement && document.activeElement.id === ${JSON.stringify(step.trigger)}`) : false;
    entries.push({ route: snap.path, mode: "inapp", layout: "shell", snap, previousTitle: before.title, expectFocus: step.expectFocus, stepName: step.name });
    report.count("routes:inapp");
  }
  for (const entry of entries) {
    for (const f of analyzeRoute(entry)) report.add({ ...f, state: `in-app ${entry.stepName} -> ${entry.route}` });
  }
}

/** All route checks of a smoke kind. */
export async function checkRoutes(page, report, options) {
  if (options.kind === "demo") {
    await checkDirectLoads(page, report, options.baseUrl);
  } else {
    report.skip("routes", "direct loads of /kanban, /sessions/<id>, /settings, /about and an unknown path: the static server of the real-bundle smoke has no SPA rewrite (covered by the Demo half)");
  }
  await checkInAppNavigation(page, report, options);
}
