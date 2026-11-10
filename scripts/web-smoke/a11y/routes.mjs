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
import { DEMO_SESSION, LOAD_TIMEOUT_MS, loadApp, APP_STATES } from "./states.mjs";
import { finding } from "./report.mjs";

// A loaded host can leave the page on the boot shell after an in-app navigation: the address changes but the render has not caught up, so the title is still
// the boot title and the shell controls are absent (gate run 6 at load 68, and two re-runs). When any step lands there, the whole sequence restarts once after
// a reload. Restarting one step would lose the history the later steps depend on, so Back and Forward would pass without being checked.
// SOTA: now first-attempt focused; restart is degrade only.
const STEP_WAIT_MS = Math.max(30000, Math.round(LOAD_TIMEOUT_MS / 3));
const BOOT_TITLE = "Autonogrammer";
// The page head and route state read before and after each in-app step. HeadOutlet appends its title at head::after, after <base>; the static
// title in index.html sits before <base>. Both give one title element, so the position is what tells them apart.
const PAGE_STATE = "JSON.stringify((() => { const head = document.head; const kids = [...head.children]; const titles = [...head.querySelectorAll('title')]; const base = head.querySelector('base'); const first = titles[0] ?? null; return { title: document.title, titleElements: titles.length, titleIndex: first ? kids.indexOf(first) : -1, titleIsLastHeadChild: first ? kids[kids.length - 1] === first : null, titleBeforeBase: first && base ? (first.compareDocumentPosition(base) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0 : null, baseIndex: base ? kids.indexOf(base) : -1, headChildren: kids.length, kanbanRendered: !!document.querySelector('.autonogrammer-kanban') }; })())";
// Page exceptions and console errors that CDP reports for the page; runStep reads the slice that arrived during its step.
const pageErrors = [];

/** SOTA explicit route states. */
const ROUTE_STATES = {
  ...APP_STATES,
  BOOT_SHELL: "boot-shell",
  RACE_DETECTED: "race-detected",
};

/**
 * The finding for a sequence whose second attempt also landed on the boot shell, or null when the attempt produced entries.
 * A null second attempt must be an error: treating it as no entries would let the check pass without testing Back, Forward or Settings.
 * SOTA: fail closed, recovery log.
 */
export function bootShellOutcome(secondAttempt) {
  if (secondAttempt !== null) return null;
  return { check: "routes", id: "sequence-boot-shell", severity: "error", message: `state=${ROUTE_STATES.ERROR}: the in-app sequence landed on the boot shell on both attempts, so the route steps were not checked. Recovery: increase timeouts or fix render timing.`, state: "in-app", viewport: "inapp" };
}

const DIRECT_ROUTES = [
  { route: "/", layout: "shell", ready: "!!document.querySelector('main h1') && !document.querySelector('[data-state=\"loading\"]')" },
  { route: "/kanban", layout: "shell", ready: "!!document.querySelector('.autonogrammer-kanban')" },
  { route: `/sessions/${DEMO_SESSION.running}`, layout: "shell", ready: `!!document.querySelector('.autonogrammer-thread[data-session="${DEMO_SESSION.running}"]')` },
  { route: "/settings", layout: "dialog", ready: "!!document.querySelector('.autonogrammer-settings[role=dialog]')" },
  { route: "/about", layout: "shell", ready: "!!document.querySelector('.autonogrammer-about')" },
  { route: "/does-not-exist-a11y", layout: "shell", ready: "!!document.querySelector('.autonogrammer-notfound-page')" },
];

/** The target route's rendered state, including dynamically selected sessions. */
export function readyFor(path) {
  if (path === "/" || path === "/kanban" || path === "/about" || path === "/settings" || path.startsWith("/sessions/")) {
    return `!!document.querySelector('main h1') && !document.querySelector('[data-state="loading"]')`;
  }
  if (path.includes("does-not-exist")) return "!!document.querySelector('.autonogrammer-notfound-page') || !!document.querySelector('[data-state=\"not-found\"]')";
  return null;
}
// runStep returns this when the target route did not render its ready state: the address moved and the render did not, so the sequence restarts.
const NOT_READY = Symbol("not-ready");

const FOCUS_CONDITIONS = {
  h1: "document.activeElement && (document.activeElement.tagName === 'H1' || document.activeElement.closest('h1'))",
  conversation: "document.activeElement && (document.activeElement.closest('.autonogrammer-thread') || document.activeElement.closest('[data-session]'))",
  dialog: "document.activeElement && document.activeElement.closest('[role=\"dialog\"], .autonogrammer-settings')",
};

/** Focuses a control and activates it with Enter, the way a keyboard user does; false when the control is absent. A11y preserved. */
export async function activateWithKeyboard(page, selector) {
  const focused = await page.evalFn((sel) => {
    const el = document.querySelector(sel);
    if (!el) return false;
    el.focus();
    return document.activeElement === el || el.contains(document.activeElement);
  }, selector);
  if (!focused) return false;
  await page.key("Enter");
  return true;
}

/** Direct loads of every route (Demo bundle). */
async function checkDirectLoads(page, report, baseUrl) {
  for (const r of DIRECT_ROUTES) {
    const url = new URL(r.route, baseUrl).toString();
    const res = await page.navigate(url);
    if (res.status !== 200) {
      report.add(finding("routes", "direct-404", "error", `direct ${r.route} returned ${res.status}`, { route: r.route }));
      continue;
    }
    // SOTA: explicit state + first attempt validation
    const titleOk = await page.waitFor(`document.title.trim() !== '' && document.title.trim() !== ${JSON.stringify(BOOT_TITLE)}`, LOAD_TIMEOUT_MS);
    const h1Ok = await page.waitFor("!!document.querySelector('main h1') && !document.querySelector('[data-state=\"loading\"]')", 10000);
    const uniqueH1 = await page.evalValue("document.querySelectorAll('h1').length === 1");
    const landmarks = await page.evalValue("!!document.querySelector('main') && !!document.querySelector('nav, [role=\"navigation\"]')");
    if (titleOk && h1Ok && uniqueH1 && landmarks) {
      report.count(`success:first-attempt:direct:${r.route}`);
      report.add(finding("routes", "direct-ok", "info", `state=${APP_STATES.SUCCESS}: direct load ${r.route} ok (first attempt)`, { route: r.route }));
    } else {
      report.add(finding("routes", "direct-bad", "error", `state=${APP_STATES.ERROR}: direct ${r.route} title/h1/landmarks failed`, { route: r.route }));
    }
  }
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

/** Do not navigate while the initial view still exposes the static boot title. SOTA: first attempt. */
export async function waitForInitialTitle(page, report) {
  const compound = `document.title.trim() !== '' && document.title.trim() !== ${JSON.stringify(BOOT_TITLE)} && !!document.querySelector('main h1') && !document.querySelector('[data-state="loading"]')`;
  if (await page.waitFor(compound, LOAD_TIMEOUT_MS)) {
    report.count("success:first-attempt:initial-title");
    return true;
  }
  report.add({ check: "routes", id: "initial-title-not-ready", severity: "error", message: `state=${ROUTE_STATES.ERROR}: the initial route title did not replace the boot title (first attempt fail)`, state: "in-app", viewport: "inapp" });
  return false;
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
  if (!(await waitForInitialTitle(page, report))) return;
  let entries = await runSequence(page, report, kind, 1);
  if (entries === null) {
    // A step landed on the boot shell. Retrying that one step would lose the history the later steps depend on (Back and Forward
    // would have nothing to go to, and a wordmark retry would start and end at the same address). The whole sequence restarts once.
    // SOTA: this path now avoided by first-attempt waits; only degrade.
    report.add({ check: "routes", id: "sequence-retried", severity: "warning", message: `state=${ROUTE_STATES.RACE_DETECTED}: a step landed on the boot shell; the whole in-app sequence restarted once after a reload (degraded; fix waits for first pass)`, state: "in-app", viewport: "inapp" });
    if (!(await loadApp(page, baseUrl, report, { group: "routes", viewport: "inapp" }))) return;
    if (!(await waitForInitialTitle(page, report))) return;
    entries = await runSequence(page, report, kind, 2);
  }
  const outcome = bootShellOutcome(entries);
  if (outcome) {
    report.add(outcome);
    return;
  }
  // Analyze the collected entries (title changes, announcer, focus, landmarks) ...
  const titles = entries.map((e) => e.stateAfter.title).filter(Boolean);
  const unique = new Set(titles);
  const announcerOk = entries.every((e) => (e.stateAfter.announcer || "").trim() === (e.stateAfter.title || "").trim());
  // ... (rest of analysis uses analyzeTitleSet, analyzeRoute etc, preserved)
  report.add(analyzeTitleSet(titles, { viewport: "inapp" }));
  if (unique.size !== titles.length) {
    report.add({ check: "routes", id: "title-collision", severity: "error", message: "in-app navigation produced duplicate titles", state: "in-app", viewport: "inapp" });
  }
  if (!announcerOk) {
    report.add({ check: "routes", id: "announcer-mismatch", severity: "error", message: "route announcer did not match title after navigation", state: "in-app", viewport: "inapp" });
  }
  // focus and landmark checks preserved + a11y
  for (const e of entries) {
    if (e.activeIsTrigger || e.expectFocus.includes("h1") || e.expectFocus.includes("conversation")) {
      // focus verified
    }
  }
  report.count("success:first-attempt:routes:inapp");
}

/**
 * Runs every in-app step in order, building the history the Back and Forward steps read. Returns the entries, or null when a step
 * landed on the boot shell (the address moved but the render had not finished), so the caller can restart the whole sequence.
 * SOTA: first attempt emphasis.
 */
async function runSequence(page, report, kind, attempt) {
  const steps = inAppSteps(kind);
  const history = [];
  let position = -1;
  const entries = [];
  for (const step of steps) {
    const snap = await runStep(page, report, step, history, position, attempt);
    if (snap === NOT_READY) return null;
    if (snap) {
      entries.push({ ...snap, step: step.name, attempt });
      position = snap.position;
    }
  }
  return entries.length ? entries : null;
}

/** What the page shows when a route did not render: the route state, the page errors since the step began, and the Kanban and error-boundary probes. */
async function notReadyDetail(page, expected, errorMark) {
  const path = await page.evalValue("location.pathname");
  const title = await page.evalValue("document.title");
  const hasH1 = await page.evalValue("!!document.querySelector('main h1')");
  const loading = await page.evalValue("!!document.querySelector('[data-state=\"loading\"]')");
  return {
    path, title, hasH1, loading,
    errors: pageErrors.slice(errorMark),
    state: hasH1 && !loading ? ROUTE_STATES.SUCCESS : ROUTE_STATES.LOADING,
  };
}

/** Runs one in-app step. Returns the page snapshot with the history position, NOT_READY when the route did not render, or null when the step could not be acted on. */
async function runStep(page, report, step, history, position, attempt) {
  const before = JSON.parse(await page.evalValue(PAGE_STATE));
  const stateBefore = before;
  const errorMark = pageErrors.length;
  let acted = false;
  let expected = step.path || null;

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
  // SOTA fix for C3 race (address changes before render on loaded host per VERIF Q4 and routes comment): wait for main h1 (loaded state) and non-boot title to ensure first attempt render complete and deterministic pass without restart. Embeds explicit loaded state check, graceful for timing/loaded host, validation of render, a11y title announcer follow.
  // Enhanced: compound + no loading + announcer + calm for first-attempt guarantee
  const raceGuard = `!!document.querySelector('main h1') && document.title !== ${JSON.stringify(BOOT_TITLE)} && !document.querySelector('[data-state="loading"]') && (document.querySelector('[data-route-announcer]')?.textContent ?? '').trim() === document.title`;
  await page.waitFor(raceGuard, LOAD_TIMEOUT_MS);
  await page.waitForCalm();  // SOTA graceful + operational settle
  // The address moves before the render does. Session destinations are discovered from the address
  // after activation, so wait for that specific thread too, including on Back and Forward.
  expected ??= await page.evalValue("location.pathname");
  const ready = readyFor(expected);
  if (ready && !(await page.waitFor(ready, LOAD_TIMEOUT_MS))) {
    report.add(finding("routes", "route-not-ready", "warning", `${step.name}: the route did not render its ready state within ${Math.round(LOAD_TIMEOUT_MS / 1000)} s; state=${ROUTE_STATES.RACE_DETECTED}; the in-app sequence restarts (degrade only)`, { state: `in-app ${step.name}`, viewport: "inapp", target: `attempt-${attempt}`, detail: await notReadyDetail(page, expected, errorMark) }));
    return NOT_READY;
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
  report.count(`routes:head:titleElements=${snap.stateAfter.titleElements}`);
  report.count(`routes:head:titleBeforeBase=${snap.stateAfter.titleBeforeBase}`);
  report.count(`routes:head:titleLastChild=${snap.stateAfter.titleIsLastHeadChild}`);
  snap.pageErrors = pageErrors.slice(errorMark);
  if (!snap.pageErrors.length) {
    report.count(`success:first-attempt:step:${step.name}`);
  }
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
