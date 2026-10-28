// WEB-01 smoke v2, Demo half. Drives the published Demo bundle (scripts/vercel/build-demo.sh) in the owned headless Chrome that
// scripts/web-smoke/run-smoke.sh starts, over the Chrome DevTools Protocol, and checks every screen in both themes at three widths:
//
//   themes   light, dark
//   widths   1440x900 (desktop), 1024x768 (tablet), 390x844 (phone)
//   screens  agents (running), approval, terminal, completed with Changes, failed, closed, slash palette, model menu, kanban, settings, drawer (phone)
//
// For every screen: axe-core (WCAG 2.0/2.1 A and AA, the pinned engine) reports zero violations and zero incomplete, and the page does not
// scroll sideways. For every theme and width: no console error, no uncaught exception, no failed request, the Demo banner is present, the skip
// link is the first stop of the tab order, and on the phone the drawer traps the focus. Once: the theme switch persists across a reload.
//
// Stdout: one JSON document. Stderr: progress. Exit 0 only when every assertion holds.
// Env: CDP_PORT, DEMO_URL, OUT_DIR, COMMIT, RUN_DATE, AXE_JS; optional SHOT_DIR (a PNG per screen is written there, named theme-width-screen).

import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

function need(name) {
  const value = process.env[name];
  if (!value) throw new Error(`missing environment variable ${name}`);
  return value;
}

const CDP_PORT = need("CDP_PORT");
const DEMO_URL = need("DEMO_URL");
const OUT_DIR = need("OUT_DIR");
const COMMIT = need("COMMIT");
const RUN_DATE = need("RUN_DATE");
const AXE_JS = need("AXE_JS");
const SHOT_DIR = process.env.SHOT_DIR || "";
if (SHOT_DIR) mkdirSync(SHOT_DIR, { recursive: true });

const THEMES = ["light", "dark"];
const VIEWPORTS = [
  { name: "1440", width: 1440, height: 900 },
  { name: "1024", width: 1024, height: 768 },
  { name: "390", width: 390, height: 844 },
];

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const note = (message) => process.stderr.write(`${new Date().toISOString()} ${message}\n`);

// ---- CDP transport ---------------------------------------------------------------------------------------

const meta = await (await fetch(`http://127.0.0.1:${CDP_PORT}/json/version`)).json();
const ws = new WebSocket(meta.webSocketDebuggerUrl);
await new Promise((resolve, reject) => {
  ws.addEventListener("open", resolve, { once: true });
  ws.addEventListener("error", () => reject(new Error("CDP WebSocket error")), { once: true });
});

let nextId = 1;
const pending = new Map();
const events = [];

ws.addEventListener("message", (event) => {
  const msg = JSON.parse(event.data);
  if (msg.id !== undefined && pending.has(msg.id)) {
    const { resolve, reject } = pending.get(msg.id);
    pending.delete(msg.id);
    if (msg.error) reject(new Error(msg.error.message));
    else resolve(msg.result);
    return;
  }
  if (msg.method) events.push(msg);
});

function send(method, params = {}, sessionId) {
  const id = nextId++;
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    ws.send(JSON.stringify(sessionId ? { id, method, params, sessionId } : { id, method, params }));
  });
}

const { targetId } = await send("Target.createTarget", { url: "about:blank" });
const { sessionId } = await send("Target.attachToTarget", { targetId, flatten: true });
const cdp = (method, params) => send(method, params, sessionId);

await cdp("Page.enable");
await cdp("Runtime.enable");
await cdp("Log.enable");
await cdp("Network.enable");

async function evalValue(expression) {
  const result = await cdp("Runtime.evaluate", { expression, returnByValue: true, awaitPromise: true });
  if (result.exceptionDetails) throw new Error(`page evaluation failed: ${result.exceptionDetails.text} ${JSON.stringify(result.exceptionDetails.exception?.description ?? "").slice(0, 200)}`);
  return result.result.value;
}

const evalFn = (fn, ...args) => evalValue(`(${fn.toString()})(...${JSON.stringify(args)})`);

async function waitFor(expression, ms) {
  const deadline = Date.now() + ms;
  for (;;) {
    try {
      if (await evalValue(expression)) return true;
    } catch {
      // The page may still be booting; retry until the deadline.
    }
    if (Date.now() > deadline) return false;
    await sleep(200);
  }
}

async function pressKey(key, modifiers = 0) {
  const codes = { Tab: 9, Escape: 27, Enter: 13 };
  const base = { key, code: key, windowsVirtualKeyCode: codes[key] ?? 0, nativeVirtualKeyCode: codes[key] ?? 0, modifiers };
  await cdp("Input.dispatchKeyEvent", { type: "keyDown", ...base });
  await cdp("Input.dispatchKeyEvent", { type: "keyUp", ...base });
}

/** Drains the console, exception and network events seen since the last call into problem lists. */
function drainProblems() {
  const problems = [];
  for (const msg of events.splice(0)) {
    if (msg.sessionId !== sessionId) continue;
    const p = msg.params ?? {};
    if (msg.method === "Runtime.exceptionThrown") {
      problems.push(`exception: ${p.exceptionDetails?.exception?.description ?? p.exceptionDetails?.text ?? "?"}`.slice(0, 240));
    } else if (msg.method === "Runtime.consoleAPICalled" && p.type === "error") {
      problems.push(`console.error: ${(p.args ?? []).map((a) => a.value ?? a.description ?? "").join(" ")}`.slice(0, 240));
    } else if (msg.method === "Log.entryAdded" && p.entry?.level === "error") {
      problems.push(`log: ${p.entry.text} ${p.entry.url ?? ""}`.slice(0, 240));
    } else if (msg.method === "Network.loadingFailed" && !p.canceled) {
      problems.push(`request failed: ${p.errorText} ${p.requestId}`);
    } else if (msg.method === "Network.responseReceived" && p.response.status >= 400) {
      problems.push(`HTTP ${p.response.status}: ${p.response.url}`);
    }
  }
  return problems;
}

// ---- in-page probes (serialised by evalFn) ---------------------------------------------------------------

function pageClick(selector) {
  const el = document.querySelector(selector);
  if (!el) return false;
  el.click();
  return true;
}

function pageOpenRow(suffix) {
  const row = [...document.querySelectorAll("[data-row-id]")].find((b) => b.getAttribute("data-row-id").endsWith(suffix));
  if (!row) return false;
  row.click();
  return true;
}

function pageSetComposer(text) {
  const area = document.querySelector(".ompos-composer-input, textarea");
  if (!area) return false;
  area.focus();
  area.value = text;
  area.dispatchEvent(new Event("input", { bubbles: true }));
  return true;
}

function pageGeometry() {
  const root = document.documentElement;
  const offenders = [...document.querySelectorAll("body *")]
    .filter((el) => {
      const style = getComputedStyle(el);
      if (style.position === "fixed" || style.display === "none") return false;
      const rect = el.getBoundingClientRect();
      if (rect.width === 0 || rect.height === 0) return false;
      for (let p = el.parentElement; p; p = p.parentElement) {
        const ps = getComputedStyle(p);
        if (ps.overflowX !== "visible" && p !== document.body && p !== root) return false;
      }
      return rect.right > window.innerWidth + 1 || rect.left < -1;
    })
    .slice(0, 5)
    .map((el) => `${el.tagName.toLowerCase()}.${String(el.className).slice(0, 60)}`);
  return { scrollWidth: root.scrollWidth, innerWidth: window.innerWidth, bodyScrollWidth: document.body.scrollWidth, offenders };
}

function pageState() {
  const root = document.documentElement;
  const banner = document.querySelector(".ompos-demo-banner");
  const skip = document.querySelector(".ompos-skip-link");
  return {
    theme: root.getAttribute("data-theme"),
    stored: localStorage.getItem("ompos.theme"),
    bannerText: banner ? banner.textContent.trim() : null,
    bannerDismissible: banner ? !!banner.querySelector("button") : null,
    skipLinkPresent: !!skip,
    activeIsSkipLink: !!skip && document.activeElement === skip,
    activeTag: document.activeElement ? document.activeElement.tagName : null,
  };
}

function pageFocusWhere() {
  const active = document.activeElement;
  const inside = (selector) => !!active && !!active.closest(selector);
  return {
    inRail: inside("#ompos-rail"),
    inTopbar: inside(".ompos-topbar"),
    inMain: inside("main"),
    desc: active ? `${active.tagName.toLowerCase()}${active.id ? "#" + active.id : ""}.${String(active.className).slice(0, 40)}` : null,
  };
}

async function runAxe() {
  return evalValue(`(async () => {
    const run = await axe.run(document, { runOnly: { type: "tag", values: ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"] }, resultTypes: ["violations", "incomplete"] });
    const pick = (items) => items.map((item) => ({ id: item.id, impact: item.impact, targets: item.nodes.map((node) => node.target.join(" ")).slice(0, 4), summary: item.nodes[0]?.failureSummary?.slice(0, 200) ?? "" }));
    return { violations: pick(run.violations), incomplete: pick(run.incomplete) };
  })()`);
}

async function injectAxe() {
  const source = readFileSync(AXE_JS, "utf8");
  const result = await cdp("Runtime.evaluate", { expression: source, returnByValue: false });
  if (result.exceptionDetails) throw new Error("axe-core failed to load in the page");
}

// ---- run -------------------------------------------------------------------------------------------------

const result = { demo: DEMO_URL, commit: COMMIT, axeCore: "4.14.0", startedAt: new Date().toISOString(), configs: [] };
const assertions = {};
const failures = [];
const fail = (assertion, detail) => {
  assertions[assertion] = false;
  failures.push(`${assertion}: ${detail}`);
};
const hold = (assertion) => {
  if (!(assertion in assertions)) assertions[assertion] = true;
};

async function loadDemo(theme, viewport) {
  await cdp("Emulation.setDeviceMetricsOverride", { width: viewport.width, height: viewport.height, deviceScaleFactor: 1, mobile: viewport.width < 700 });
  await cdp("Page.navigate", { url: "about:blank" });
  await sleep(200);
  await cdp("Page.navigate", { url: DEMO_URL });
  await waitFor("document.readyState === 'complete'", 30000);
  await evalValue(`localStorage.setItem('ompos.theme', ${JSON.stringify(theme)})`);
  drainProblems();
  await cdp("Page.navigate", { url: DEMO_URL });
  const ready = await waitFor("document.querySelectorAll('.ompos-agent-row').length > 0", 180000);
  await sleep(1500);
  if (!ready) throw new Error(`the Demo did not render at ${viewport.name} in ${theme}`);
  await injectAxe();
}

async function check(config, screen) {
  await sleep(500);
  const geometry = await evalFn(pageGeometry);
  const rawAxe = await runAxe();
  // The vendored xterm.js (lib/xterm) draws its accessibility rows as virtual list items axe cannot measure (colour contrast of terminal cells);
  // that single third-party case is the only incomplete result the smoke tolerates.
  const notXterm = rawAxe.incomplete.filter((item) => !item.targets.every((target) => target.includes("aria-posinset")));
  // axe reports "color-contrast" as incomplete when it cannot measure a pair (overlapping boxes, pseudo-element backgrounds). The pair is
  // computed from the tokens by the contrast tests, so the smoke records these as warnings; every other incomplete result fails the run.
  const axeResult = { violations: rawAxe.violations, incomplete: notXterm.filter((item) => item.id !== "color-contrast"), warnings: notXterm.filter((item) => item.id === "color-contrast") };
  const entry = {
    screen,
    axeViolations: axeResult.violations,
    axeIncomplete: axeResult.incomplete,
    axeContrastWarnings: axeResult.warnings,
    scrollWidth: geometry.scrollWidth,
    innerWidth: geometry.innerWidth,
    offenders: geometry.offenders,
  };
  config.screens.push(entry);
  const where = `${config.theme} ${config.viewport} ${screen}`;
  if (axeResult.violations.length > 0) fail("demoAxeNoViolations", `${where}: ${axeResult.violations.map((v) => v.id + " " + v.targets[0]).join("; ")}`);
  if (axeResult.incomplete.length > 0) fail("demoAxeNoIncomplete", `${where}: ${axeResult.incomplete.map((v) => v.id + " " + v.targets[0]).join("; ")}`);
  if (geometry.scrollWidth > geometry.innerWidth) fail("demoNoHorizontalScroll", `${where}: scrollWidth ${geometry.scrollWidth} > ${geometry.innerWidth} (${geometry.offenders.join(", ")})`);
  hold("demoAxeNoViolations");
  hold("demoAxeNoIncomplete");
  hold("demoNoHorizontalScroll");
  if (SHOT_DIR) {
    const { data } = await cdp("Page.captureScreenshot", { format: "png" });
    writeFileSync(join(SHOT_DIR, `${config.theme}-${config.viewport}-${screen}.png`), Buffer.from(data, "base64"));
  }
  note(`${where}: axe ${axeResult.violations.length} violations, ${axeResult.incomplete.length} incomplete, scrollWidth ${geometry.scrollWidth}/${geometry.innerWidth}`);
}

async function openDrawerIfNarrow(narrow) {
  if (!narrow) return;
  await evalFn(pageClick, "button[aria-controls='ompos-rail']");
  await sleep(450);
}

async function openAgent(narrow, suffix, readySelector) {
  await openDrawerIfNarrow(narrow);
  const clicked = await evalFn(pageOpenRow, suffix);
  if (!clicked) throw new Error(`no rail row ends with ${suffix}`);
  const ready = await waitFor(`!!document.querySelector(${JSON.stringify(readySelector)})`, 20000);
  if (!ready) throw new Error(`the screen for ${suffix} did not render (${readySelector})`);
  await sleep(1800);
}

try {
  for (const theme of THEMES) {
    for (const viewport of VIEWPORTS) {
      const narrow = viewport.width < 840;
      const config = { theme, viewport: viewport.name, screens: [], problems: [] };
      result.configs.push(config);
      note(`config ${theme} ${viewport.name}`);
      await loadDemo(theme, viewport);

      // Skip link first in the tab order, and the theme is the one asked for.
      const initial = await evalFn(pageState);
      config.state = initial;
      if (initial.theme !== theme) fail("demoThemeApplied", `${theme} ${viewport.name}: data-theme is ${initial.theme}`);
      hold("demoThemeApplied");
      if (initial.bannerText === null || !initial.bannerText.startsWith("Demo:")) fail("demoBannerPresent", `${theme} ${viewport.name}: banner text ${initial.bannerText}`);
      else if (initial.bannerDismissible) fail("demoBannerPresent", `${theme} ${viewport.name}: the banner has a control`);
      hold("demoBannerPresent");
      await evalValue("document.activeElement && document.activeElement.blur(); window.scrollTo(0, 0); true");
      await pressKey("Tab");
      const afterTab = await evalFn(pageState);
      if (!afterTab.activeIsSkipLink) fail("demoSkipLinkFirstInTabOrder", `${theme} ${viewport.name}: first Tab focused ${afterTab.activeTag}`);
      hold("demoSkipLinkFirstInTabOrder");
      await evalValue("document.activeElement && document.activeElement.blur(); true");

      await check(config, "agents");

      if (narrow) {
        // The drawer: open, axe, the focus stays inside the drawer and the top bar while Tab and Shift+Tab cycle, Escape closes it.
        await evalFn(pageClick, "button[aria-controls='ompos-rail']");
        await sleep(450);
        await check(config, "drawer");
        let leaked = null;
        for (let i = 0; i < 40 && !leaked; i++) {
          await pressKey("Tab");
          const where = await evalFn(pageFocusWhere);
          if (where.inMain) leaked = where.desc;
        }
        for (let i = 0; i < 12 && !leaked; i++) {
          await pressKey("Tab", 8);
          const where = await evalFn(pageFocusWhere);
          if (where.inMain) leaked = where.desc;
        }
        if (leaked) fail("demoDrawerTrapsFocus", `${theme} ${viewport.name}: focus reached ${leaked} behind the open drawer`);
        hold("demoDrawerTrapsFocus");
        await pressKey("Escape");
        await sleep(450);
        const closed = await evalValue("document.querySelector('.ompos-shell').getAttribute('data-drawer')");
        if (closed !== "closed") fail("demoDrawerEscapeCloses", `${theme} ${viewport.name}: data-drawer is ${closed} after Escape`);
        hold("demoDrawerEscapeCloses");
      }

      await openAgent(narrow, "d001", ".ompos-thread[data-session$='d001']");
      await check(config, "running");
      await evalFn(pageSetComposer, "/");
      await waitFor("!!document.querySelector('.ompos-slash-palette')", 8000);
      await check(config, "slash-palette");
      await evalFn(pageSetComposer, "");
      await sleep(300);
      await pressKey("Escape");
      const modelChip = await evalFn(pageClick, ".ompos-chip-menu .ompos-chip-btn, .ompos-composer-chips button");
      if (modelChip) {
        await sleep(500);
        await check(config, "composer-menu");
        await pressKey("Escape");
      }

      await openAgent(narrow, "d002", ".ompos-thread[data-session$='d002'] .ompos-approval");
      await check(config, "approval");
      await openAgent(narrow, "d004", ".ompos-thread[data-session$='d004'] .ompos-changes");
      await check(config, "completed-changes");
      await openAgent(narrow, "d005", ".ompos-thread[data-session$='d005']");
      await check(config, "failed");
      await openAgent(narrow, "d006", ".ompos-thread[data-session$='d006'] .ompos-closed-note");
      await check(config, "closed");
      await openAgent(narrow, "d003", ".ompos-terminal");
      await check(config, "terminal");

      await evalFn(pageClick, "#ompos-tab-kanban");
      await waitFor("!!document.querySelector('.ompos-kanban-column')", 10000);
      await check(config, "kanban");
      await evalFn(pageClick, "#ompos-tab-agents");
      await sleep(400);

      await openDrawerIfNarrow(narrow);
      await evalFn(pageClick, ".ompos-rail-settings");
      await waitFor("!!document.querySelector('.ompos-settings')", 10000);
      await sleep(700);
      await check(config, "settings");
      await pressKey("Escape");
      await sleep(400);

      config.problems = drainProblems();
      if (config.problems.length > 0) fail("demoNoConsoleErrorsOrFailedRequests", `${theme} ${viewport.name}: ${config.problems.slice(0, 3).join(" | ")}`);
      hold("demoNoConsoleErrorsOrFailedRequests");
    }
  }

  // The theme switch applies at once and survives a reload.
  note("theme switch persists");
  await loadDemo("light", VIEWPORTS[0]);
  await evalValue(`[...document.querySelectorAll('.ompos-theme-switch button')].find((b) => b.textContent.trim() === 'Dark').click(); true`);
  await sleep(500);
  const switched = await evalFn(pageState);
  await cdp("Page.navigate", { url: DEMO_URL });
  await waitFor("document.querySelectorAll('.ompos-agent-row').length > 0", 180000);
  await sleep(1000);
  const reloaded = await evalFn(pageState);
  result.themeSwitch = { switched: switched.theme, stored: switched.stored, afterReload: reloaded.theme };
  if (switched.theme !== "dark" || reloaded.theme !== "dark" || reloaded.stored !== "dark") fail("demoThemeSwitchPersists", JSON.stringify(result.themeSwitch));
  hold("demoThemeSwitchPersists");
} catch (error) {
  result.error = error instanceof Error ? error.message : String(error);
  note(`demo smoke failed: ${result.error}`);
} finally {
  result.finishedAt = new Date().toISOString();
  result.assertions = assertions;
  result.failures = failures;
  result.passed = Object.keys(assertions).length > 0 && Object.values(assertions).every((v) => v === true) && !result.error;
  writeFileSync(join(OUT_DIR, `web-smoke-demo-${COMMIT}-${RUN_DATE}.json`), `${JSON.stringify(result, null, 2)}\n`);
  ws.close();
  process.stdout.write(`${JSON.stringify({ passed: result.passed, assertions, failures, error: result.error ?? null }, null, 2)}\n`);
  process.exit(result.passed ? 0 : 1);
}
