// The reachable screens and states of the web head, how to get into each one, and the axe-core run over all of them.
//
// The Demo bundle (apps/Autonogrammer.Web/Demo, scripts/vercel/build-demo.sh) has six fixed sessions with known ids (DemoFixtures.cs): running (d001),
// approval pending (d002), terminal (d003), completed with Changes (d004), failed (d005) and closed (d006). The real bundle against the in-memory
// API has none of them, so its state list is shorter. A state is entered by navigating inside the running app (a clicked link, no reload), so the
// checks that loop over states pay the boot of the Blazor runtime once per viewport.

import { analyzeAxe } from "./logic.mjs";
import { finding } from "./report.mjs";

/** The two viewports of the checks that depend on width: the desktop and the smallest the reflow criterion names. */
export const VIEWPORTS = [
  { name: "1440x900", width: 1440, height: 900, deviceScaleFactor: 1 },
  { name: "320x568", width: 320, height: 568, deviceScaleFactor: 1 },
];

/** The ids of the Demo sessions (DemoFixtures.cs). */
export const DEMO_SESSION = {
  running: "5e1c0001-0000-4000-8000-00000000d001",
  approval: "5e1c0002-0000-4000-8000-00000000d002",
  terminal: "5e1c0003-0000-4000-8000-00000000d003",
  completed: "5e1c0004-0000-4000-8000-00000000d004",
  failed: "5e1c0005-0000-4000-8000-00000000d005",
  closed: "5e1c0006-0000-4000-8000-00000000d006",
};

/** The axe-core tags the program claims: WCAG 2.0 to 2.2 A and AA plus the best-practice rules. */
export const AXE_TAGS = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa", "best-practice"];

const ALL_GROUPS = ["axe", "reflow", "target-size", "text-spacing"];
const BOTH = ["demo", "api"];

const sessionReady = (id) => `!!document.querySelector('.autonogrammer-thread[data-session="${id}"]')`;

/** Puts text in the composer of the open thread the way typing does (focus, value, input event). */
export async function typeInComposer(page, text) {
  return page.evalFn((value) => {
    const area = document.querySelector(".autonogrammer-composer-input, textarea[aria-label='Message']");
    if (!area) return false;
    area.focus();
    area.value = value;
    area.dispatchEvent(new Event("input", { bubbles: true }));
    return true;
  }, text);
}

/**
 * Every state. required states that cannot be entered are errors (the check would otherwise silently cover less); optional ones are warnings.
 * path is the route to navigate to inside the app; prepare runs after it; ready is a page expression that is true once the state is shown.
 */
export const STATES = [
  { name: "agents", kinds: BOTH, required: true, path: "/", ready: "!!document.querySelector('main h1') && !document.querySelector('[data-state=\"loading\"]')", groups: ALL_GROUPS },
  { name: "session-running", kinds: ["demo"], required: true, path: `/sessions/${DEMO_SESSION.running}`, ready: sessionReady(DEMO_SESSION.running), groups: ALL_GROUPS },
  // The palette opens on a session that is not streaming: the running one follows its output to the end on every render, which scrolls the thread header
  // under the sticky top bar and makes axe report the close button as partly covered.
  { name: "slash-palette", kinds: ["demo"], required: true, path: `/sessions/${DEMO_SESSION.completed}`, ready: sessionReady(DEMO_SESSION.completed), prepare: async (page) => {
    await typeInComposer(page, "/");
    return page.waitFor("!!document.querySelector('.autonogrammer-slash-palette')", 8000);
  }, groups: ALL_GROUPS },
  { name: "approval-pending", kinds: ["demo"], required: true, path: `/sessions/${DEMO_SESSION.approval}`, ready: `${sessionReady(DEMO_SESSION.approval)} && !!document.querySelector('.autonogrammer-approval')`, groups: ALL_GROUPS },
  { name: "completed-changes", kinds: ["demo"], required: true, path: `/sessions/${DEMO_SESSION.completed}`, ready: `${sessionReady(DEMO_SESSION.completed)} && !!document.querySelector('.autonogrammer-changes')`, groups: ALL_GROUPS },
  { name: "failed-run", kinds: ["demo"], required: true, path: `/sessions/${DEMO_SESSION.failed}`, ready: sessionReady(DEMO_SESSION.failed), groups: ALL_GROUPS },
  { name: "closed-session", kinds: ["demo"], required: true, path: `/sessions/${DEMO_SESSION.closed}`, ready: `${sessionReady(DEMO_SESSION.closed)} && !!document.querySelector('.autonogrammer-closed-note')`, groups: ALL_GROUPS },
  { name: "terminal", kinds: ["demo"], required: true, path: `/sessions/${DEMO_SESSION.terminal}`, ready: "!!document.querySelector('.autonogrammer-terminal')", groups: ["axe", "reflow", "target-size"] },
  { name: "new-conversation", kinds: BOTH, required: false, path: "/", ready: "!!document.querySelector('.autonogrammer-new-agent')", prepare: async (page) => {
    const before = await page.evalValue("document.querySelector('.autonogrammer-thread')?.getAttribute('data-session') ?? ''");
    await page.evalFn(() => document.querySelector(".autonogrammer-new-agent")?.click());
    return page.waitFor(`(document.querySelector('.autonogrammer-thread')?.getAttribute('data-session') ?? '') !== ${JSON.stringify(before)} && !!document.querySelector('.autonogrammer-composer-input')`, 15000);
  }, groups: ALL_GROUPS },
  { name: "kanban", kinds: BOTH, required: true, path: "/kanban", ready: "!!document.querySelector('.autonogrammer-kanban')", groups: ALL_GROUPS },
  { name: "about", kinds: BOTH, required: true, path: "/about", ready: "!!document.querySelector('.autonogrammer-about')", groups: ALL_GROUPS },
  { name: "settings-dialog", kinds: BOTH, required: true, path: "/settings", ready: "!!document.querySelector('.autonogrammer-settings[role=dialog]')", groups: ALL_GROUPS },
  { name: "settings-models", kinds: BOTH, required: true, path: "/settings/models", ready: "!!document.querySelector('.autonogrammer-settings[role=dialog]') && !!document.querySelector('.autonogrammer-settings-page')", groups: ALL_GROUPS },
  { name: "session-not-found", kinds: BOTH, required: true, path: "/sessions/does-not-exist-a11y", ready: "!!document.querySelector('[data-state=\"not-found\"]')", groups: ALL_GROUPS },
  { name: "page-not-found", kinds: BOTH, required: true, path: "/does-not-exist-a11y", ready: "!!document.querySelector('.autonogrammer-notfound-page')", groups: ALL_GROUPS },
];

/** The states of a smoke kind ("demo" or "api") that belong to a check group. */
export function statesFor(kind, group) {
  return STATES.filter((s) => s.kinds.includes(kind) && s.groups.includes(group));
}

/** Loads the app and waits until the workbench (or the not-found page) is rendered and nothing is loading. */
export async function loadApp(page, url, report, ctx) {
  await page.navigate(url);
  const ready = await page.waitFor("!!document.querySelector('main h1') && !document.querySelector('[data-state=\"loading\"]') && !document.querySelector('.loading-status')", 150000);
  if (!ready) {
    report.add(finding(ctx.group, "app-not-rendered", "error", `the app did not render at ${url} within 150 s`, { viewport: ctx.viewport }));
    return false;
  }
  await page.settle();
  return true;
}

/** Enters a state; false (after a finding) when it cannot be reached. */
export async function enterState(page, state, report, ctx) {
  const where = { state: state.name, viewport: ctx.viewport };
  if (state.path) {
    const moved = await page.spaNavigate(state.path);
    if (!moved) {
      report.add(finding(ctx.group, "state-unreachable", state.required ? "error" : "warning", `could not navigate to ${state.path} inside the app`, where));
      return false;
    }
  }
  const ready = await page.waitFor(state.ready, 20000);
  let prepared = true;
  if (ready && state.prepare) prepared = await state.prepare(page);
  if (!ready || !prepared) {
    report.add(finding(ctx.group, "state-unreachable", state.required ? "error" : "warning", `the state "${state.name}" was not reached (${!ready ? "its screen did not render" : "its preparation failed"})`, where));
    return false;
  }
  await page.settle();
  await page.waitForCalm();
  // Every state is observed from the top of the page. A focus move (a session chosen in the rail, the composer) may have scrolled the page so that the sticky top bar
  // covers part of a control; that is a property of the scroll position, not of the control, and axe reports it as an undersized target.
  await page.evalFn(() => window.scrollTo({ top: 0, left: 0, behavior: "instant" }));
  await page.waitForCalm();
  report.count(`${ctx.group}:states`);
  return true;
}

/**
 * Runs visit(ctx) in every state of a group at every viewport. The app is loaded once per viewport; each state is entered by navigating inside it.
 * After a state that changed the route the next one starts from "/" so a dialog or a palette of the previous state is gone.
 */
export async function visitStates(page, report, { kind, baseUrl, group, viewports = VIEWPORTS }, visit) {
  for (const viewport of viewports) {
    await page.setViewport(viewport.width, viewport.height, viewport.deviceScaleFactor);
    const ctx = { group, viewport: viewport.name, kind };
    if (!(await loadApp(page, baseUrl, report, ctx))) continue;
    for (const state of statesFor(kind, group)) {
      await page.spaNavigate("/");
      if (!(await enterState(page, state, report, ctx))) continue;
      await visit({ ...ctx, state: state.name, definition: state });
    }
  }
}

/**
 * Runs axe-core in the current document with the program's tags and returns plain results. A target-size result that says a control is partly obscured
 * describes the scroll position at the moment of the run (the sticky top bar over a thread header after the transcript followed its output), not the
 * control, so it is left out; the keyboard walk reports a focused control that is covered, and the target-size group measures size and spacing itself.
 */
export async function runAxe(page, axeJs) {
  await page.ensureAxe(axeJs);
  return page.evalValue(`(async () => {
    const run = await axe.run(document, { runOnly: { type: "tag", values: ${JSON.stringify(AXE_TAGS)} }, resultTypes: ["violations", "incomplete"] });
    const obscured = (node) => node.any.concat(node.all, node.none).some((check) => check.data && check.data.messageKey === "partiallyObscured");
    const pick = (items) => items
      .map((item) => ({ item, nodes: item.id === "target-size" ? item.nodes.filter((node) => !obscured(node)) : item.nodes }))
      .filter(({ nodes }) => nodes.length > 0)
      .map(({ item, nodes }) => ({
        id: item.id, impact: item.impact, help: item.help, tags: item.tags,
        nodes: nodes.map((node) => ({ target: node.target.join(" "), html: String(node.html).slice(0, 160) })),
      }));
    return { violations: pick(run.violations), incomplete: pick(run.incomplete) };
  })()`);
}

/** Runs axe on the current view and adds the findings. */
export async function probeAxe(page, report, ctx, axeJs) {
  const run = await runAxe(page, axeJs);
  report.add(analyzeAxe(run, ctx));
  report.count("axe:runs");
}

/** Axe on every reachable state at both viewports: zero serious or critical violations; the others are listed. */
export async function checkAxe(page, report, options) {
  await visitStates(page, report, { ...options, group: "axe" }, (ctx) => probeAxe(page, report, ctx, options.axeJs));
}
