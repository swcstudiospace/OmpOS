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
import { DEMO_SESSION, LOAD_TIMEOUT_MS, loadApp } from "./states.mjs";

// A loaded host can leave the page on the boot shell after an in-app navigation: the address changes but the render has not caught up, so the title is still
// the boot title and the shell controls are absent (gate run 6 at load 68, and two re-runs). When any step lands there, the whole sequence restarts once after
// a reload. Restarting one step would lose the history the later steps depend on, so Back and Forward would pass without being checked.
const STEP_WAIT_MS = Math.max(30000, Math.round(LOAD_TIMEOUT_MS / 3));
const BOOT_TITLE = "Autonogrammer";
// The page head and route state read before and after each in-app step. HeadOutlet appends its title at head::after, after <base>; the static
// title in index.html sits before <base>. Both give one title element, so the position is what tells them apart.
const PAGE_STATE = "JSON.stringify((() => { const head = document.head; const kids = [...head.children]; const titles = [...head.querySelectorAll('title')]; const base = head.querySelector('base'); const first = titles[0] ?? null; return { title: document.title, titleElements: titles.length, titleIndex: first ? kids.indexOf(first) : -1, titleIsLastHeadChild: first ? kids[kids.length - 1] === first : null, titleBeforeBase: first && base ? (first.compareDocumentPosition(base) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0 : null, baseIndex: base ? kids.indexOf(base) : -1, headChildren: kids.length, kanbanRendered: !!document.querySelector('.autonogrammer-kanban') }; })())";
// Page exceptions and console errors that CDP reports for the page; runStep reads the slice that arrived during its step.
const pageErrors = [];

/**
 * The finding for a sequence whose second attempt also landed on the boot shell, or null when the attempt produced entries.
 * A null second attempt must be an error: treating it as no entries would let the check pass without testing Back, Forward or Settings.
 */
export function bootShellOutcome(secondAttempt) {
  if (secondAttempt !== null) return null;
  return { check: "routes", id: "sequence-boot-shell", severity: "error", message: "the in-app sequence landed on the boot shell on both attempts, so the route steps were not checked", state: "in-app", viewport: "inapp" };
}

const DIRECT_ROUTES = [
  { route: "/", layout: "shell", ready: "!!document.querySelector('main h1') && !document.querySelector('[data-state=\"loading\"]')" },
  { route: "/kanban", layout: "shell", ready: "!!document.querySelector('.autonogrammer-kanban')" },
  { route: "/cowork", layout: "shell", ready: "!!document.querySelector('.autonogrammer-cowork')" },
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
    const ready = await page.waitFor(spec.ready, LOAD_TIMEOUT_MS);
    if (ready) await page.waitFor("document.title !== '' && document.title !== 'Autonogrammer'", 10000);
    if (!ready) {
      report.add({ check: "routes", id: "route-not-rendered", severity: "error", message: `${spec.route} did not render within ${Math.round(LOAD_TIMEOUT_MS / 1000)} s`, state: `route ${spec.route}`, viewport: "direct" });
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
  // The surface switch (the Code and Cowork tabs) renders only where Cowork is available. The Demo is never available (WebCoworkAccess), so its half leaves these two steps out.
  if (kind !== "demo") {
    steps.splice(1, 0,
      { name: "Cowork tab", selector: "#autonogrammer-tab-cowork", path: "/cowork", expectFocus: ["trigger"], trigger: "autonogrammer-tab-cowork" },
      { name: "Code tab", selector: "#autonogrammer-tab-code", path: "/", expectFocus: ["trigger"], trigger: "autonogrammer-tab-code" });
  }
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
  pageErrors.length = 0;
  page.on("Runtime.exceptionThrown", (params) => pageErrors.push(`exception: ${params.exceptionDetails?.exception?.description ?? params.exceptionDetails?.text ?? "?"}`.slice(0, 240)));
  page.on("Runtime.consoleAPICalled", (params) => {
    if (params.type === "error") pageErrors.push(`console.error: ${(params.args ?? []).map((a) => a.value ?? a.description ?? "").join(" ")}`.slice(0, 240));
  });
  if (!(await loadApp(page, baseUrl, report, { group: "routes", viewport: "inapp" }))) return;
  let entries = await runSequence(page, report, kind);
  if (entries === null) {
    // A step landed on the boot shell. Retrying that one step would lose the history the later steps depend on (Back and Forward
    // would have nothing to go to, and a wordmark retry would start and end at the same address). The whole sequence restarts once.
    report.add({ check: "routes", id: "sequence-retried", severity: "warning", message: "a step landed on the boot shell; the whole in-app sequence restarted once after a reload", state: "in-app", viewport: "inapp" });
    if (!(await loadApp(page, baseUrl, report, { group: "routes", viewport: "inapp" }))) return;
    entries = await runSequence(page, report, kind);
    const outcome = bootShellOutcome(entries);
    if (outcome) report.add(outcome);
  }
  for (const entry of entries ?? []) {
    for (const f of analyzeRoute(entry)) report.add({ ...f, state: `in-app ${entry.stepName} -> ${entry.route}` });
  }
}

/**
 * Runs every in-app step in order, building the history the Back and Forward steps read. Returns the entries, or null when a step
 * landed on the boot shell (the address moved but the render had not finished), so the caller can restart the whole sequence.
 */
async function runSequence(page, report, kind) {
  const entries = [];
  const history = [await page.evalValue("location.pathname")];
  let position = 0;
  for (const step of inAppSteps(kind)) {
    const snap = await runStep(page, report, step, history, position);
    if (snap && snap.title.trim() === BOOT_TITLE) {
      const detail = JSON.stringify({ previousTitle: snap.previousTitle, before: snap.stateBefore, after: snap.stateAfter, pageErrors: snap.pageErrors });
      report.add({ check: "routes", id: "boot-title-after-step", severity: "warning", message: `${step.name}: the title read the boot title at ${snap.path}, so the in-app sequence restarts; detail ${detail}`, state: `in-app ${step.name}`, viewport: "inapp" });
      return null;
    }
    if (!snap) continue;
    position = snap.position;
    entries.push({ route: snap.path, mode: "inapp", layout: "shell", snap, previousTitle: snap.previousTitle, expectFocus: step.expectFocus, stepName: step.name });
    report.count("routes:inapp");
  }
  return entries;
}

/** Runs one in-app step. Returns the page snapshot with the history position, or null when the step could not be acted on. */
async function runStep(page, report, step, history, position) {
  const before = await page.lib("probeRouteState");
  const errorMark = pageErrors.length;
  const stateBefore = JSON.parse(await page.evalValue(PAGE_STATE));
  let expected = step.path;
  let acted;
  if (step.history) {
    const target = position + (step.history === "back" ? -1 : 1);
    if (target < 0 || target >= history.length) {
      report.skip("routes", `${step.name}: the history has no entry to go ${step.history} to`);
      return null;
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
    return null;
  }
  const arrived = await page.waitFor(expected ? `location.pathname === ${JSON.stringify(expected)}` : `location.pathname.startsWith(${JSON.stringify(step.pathStart)})`, STEP_WAIT_MS);
  if (!arrived) {
    report.add({ check: "routes", id: "route-not-reached", severity: "error", message: `${step.name}: the address did not become ${expected ?? `${step.pathStart}…`}`, state: `in-app ${step.name}`, viewport: "inapp" });
    return null;
  }
  if (!step.history) {
    history.length = position + 1;
    history.push(await page.evalValue("location.pathname"));
    position = history.length - 1;
  }
  // The title is set by a component after the render; the announcer follows it; focus is moved after both.
  // While a page title is unmounted the static boot title shows, so the wait must not accept it: a step that stays on the boot title times out and restarts the sequence.
  await page.waitFor(`document.title !== ${JSON.stringify(before.title)} && document.title !== ${JSON.stringify(BOOT_TITLE)}`, STEP_WAIT_MS);
  await page.waitFor("(document.querySelector('[data-route-announcer]')?.textContent ?? '').trim() === document.title", STEP_WAIT_MS);
  const focusExpression = step.expectFocus.map((name) => (name === "trigger" ? `document.activeElement && document.activeElement.id === ${JSON.stringify(step.trigger ?? "")}` : FOCUS_CONDITIONS[name])).map((c) => `(${c})`).join(" || ");
  await page.waitFor(focusExpression, STEP_WAIT_MS);
  const snap = await page.lib("probeRouteState");
  snap.activeIsTrigger = step.trigger ? await page.evalValue(`!!document.activeElement && document.activeElement.id === ${JSON.stringify(step.trigger)}`) : false;
  snap.previousTitle = before.title;
  snap.position = position;
  snap.stateBefore = stateBefore;
  snap.stateAfter = JSON.parse(await page.evalValue(PAGE_STATE));
  snap.pageErrors = pageErrors.slice(errorMark);
  return snap;
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
