// WEB-01 browser smoke driver. Speaks the Chrome DevTools Protocol to the owned headless Chrome
// that scripts/web-smoke/run-smoke.sh starts with a fresh --user-data-dir. It repeats the steps of
// docs/runbooks/local-dev.md ("Browser smoke" and "Re-run of axe-core"). It evaluates no page code
// that the runbook does not name, and it never navigates away from the app origin.
//
// Stdout: one JSON document. Stderr: progress lines. Exit 0 only when every assertion holds.

import { readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

function need(name) {
  const value = process.env[name];
  if (!value) throw new Error(`missing environment variable ${name}`);
  return value;
}

const CDP_PORT = need("CDP_PORT");
const APP_URL = need("APP_URL");
const API_ORIGIN = need("API_ORIGIN");
const OUT_DIR = need("OUT_DIR");
const COMMIT = need("COMMIT");
const RUN_DATE = need("RUN_DATE");
const AXE_JS = need("AXE_JS");
const STAMP = `${COMMIT}-${RUN_DATE}`;
const PROBE = "**bold** and [site](https://example.com/) and ![pic](https://evil.example/x.png) and <script>alert(1)</script>";

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const note = (message) => process.stderr.write(`${new Date().toISOString()} ${message}\n`);

// ---- CDP transport -------------------------------------------------------------------------

const meta = await (await fetch(`http://127.0.0.1:${CDP_PORT}/json/version`)).json();
const ws = new WebSocket(meta.webSocketDebuggerUrl);
await new Promise((resolve, reject) => {
  ws.addEventListener("open", resolve, { once: true });
  ws.addEventListener("error", () => reject(new Error("CDP WebSocket error")), { once: true });
});

let nextId = 1;
const pending = new Map();
let listeners = [];

ws.addEventListener("message", (event) => {
  const msg = JSON.parse(event.data);
  if (msg.id !== undefined && pending.has(msg.id)) {
    const { resolve, reject } = pending.get(msg.id);
    pending.delete(msg.id);
    if (msg.error) reject(new Error(`${msg.error.message}`));
    else resolve(msg.result);
    return;
  }
  if (msg.method) for (const listener of [...listeners]) listener(msg);
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

function waitForEvent(method, ms) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`timed out waiting for ${method}`)), ms);
    const listener = (msg) => {
      if (msg.sessionId !== sessionId || msg.method !== method) return;
      clearTimeout(timer);
      listeners = listeners.filter((l) => l !== listener);
      resolve(msg.params);
    };
    listeners.push(listener);
  });
}

// ---- Page-side error capture (Runtime exceptions, console errors, log entries) ------------

const exceptions = [];
const consoleErrors = [];
const logErrors = [];

function describeRemote(object) {
  if (!object) return "";
  if (object.value !== undefined) return String(object.value);
  return object.description ?? object.unserializableValue ?? "";
}

listeners.push((msg) => {
  if (msg.sessionId !== sessionId) return;
  if (msg.method === "Runtime.exceptionThrown") {
    const details = msg.params.exceptionDetails;
    exceptions.push(describeRemote(details.exception) || details.text);
  } else if (msg.method === "Runtime.consoleAPICalled" && msg.params.type === "error") {
    consoleErrors.push(msg.params.args.map(describeRemote).join(" "));
  } else if (msg.method === "Log.entryAdded" && msg.params.entry.level === "error") {
    const entry = msg.params.entry;
    logErrors.push(`${entry.source}: ${entry.text} ${entry.url ?? ""}`.trim());
  }
});

await cdp("Page.enable");
await cdp("Runtime.enable");
await cdp("Log.enable");
await cdp("Emulation.setDeviceMetricsOverride", { width: 1280, height: 900, deviceScaleFactor: 1, mobile: false });

// ---- helpers ---------------------------------------------------------------------------------

async function openPage(url) {
  const loaded = waitForEvent("Page.loadEventFired", 60000);
  const nav = await cdp("Page.navigate", { url });
  if (nav.errorText) throw new Error(`navigation to ${url} failed: ${nav.errorText}`);
  await loaded;
}

async function evalValue(expression) {
  const result = await cdp("Runtime.evaluate", { expression, returnByValue: true, awaitPromise: true });
  if (result.exceptionDetails) {
    throw new Error(`page evaluation failed: ${result.exceptionDetails.exception?.description ?? result.exceptionDetails.text}`);
  }
  return result.result.value;
}

// Serialises an in-page function so the exact source the driver reviews is the source that runs.
function evalFn(fn, ...args) {
  return evalValue(`(${fn.toString()})(${args.map((a) => JSON.stringify(a)).join(",")})`);
}

async function waitFor(expression, ms) {
  const deadline = Date.now() + ms;
  for (;;) {
    try {
      if (await evalValue(expression)) return true;
    } catch {
      // The page may still be rendering; retry until the deadline.
    }
    if (Date.now() > deadline) return false;
    await sleep(250);
  }
}

async function shot(name, format = "png") {
  const { data } = await cdp("Page.captureScreenshot", { format });
  const file = `${name}-${STAMP}.${format}`;
  writeFileSync(join(OUT_DIR, file), Buffer.from(data, "base64"));
  note(`screenshot ${file}`);
  return file;
}

async function injectAxe() {
  const source = readFileSync(AXE_JS, "utf8");
  const result = await cdp("Runtime.evaluate", { expression: source, returnByValue: false });
  if (result.exceptionDetails) throw new Error("axe-core failed to load in the page");
  const version = await evalValue("typeof axe === 'object' ? axe.version : null");
  if (!version) throw new Error("axe global missing after injection");
  return version;
}

async function pressTab() {
  const key = { key: "Tab", code: "Tab", windowsVirtualKeyCode: 9, nativeVirtualKeyCode: 9 };
  await cdp("Input.dispatchKeyEvent", { type: "keyDown", ...key });
  await cdp("Input.dispatchKeyEvent", { type: "keyUp", ...key });
}

async function pressKey(key) {
  await cdp("Input.dispatchKeyEvent", { type: "keyDown", ...key });
  await cdp("Input.dispatchKeyEvent", { type: "keyUp", ...key });
}

// ---- in-page probes (serialised by evalFn) ----------------------------------------------------

function pageFirstRender() {
  const shell = document.querySelector(".autonogrammer-shell");
  const bar = document.querySelector("#blazor-error-ui");
  return {
    rendered: !!shell,
    errorBarVisible: !!bar && getComputedStyle(bar).display !== "none",
    errorBarText: bar ? bar.textContent.trim().slice(0, 200) : "",
  };
}

function pageSkipLink() {
  const link = document.querySelector(".autonogrammer-skip-link");
  if (!link) return { present: false };
  const rect = link.getBoundingClientRect();
  const style = getComputedStyle(link);
  const active = document.activeElement;
  // The target is whatever the link points at: the shell targets the conversation while one is open and the main region otherwise.
  const href = link.getAttribute("href") || "";
  const targetId = href.startsWith("#") ? href.slice(1) : "";
  return {
    present: true,
    width: rect.width,
    height: rect.height,
    clip: style.clip,
    visibility: style.visibility,
    activeIsSkip: active === link,
    active: active ? active.tagName : null,
    targetExists: targetId.length > 0 && !!document.getElementById(targetId),
  };
}

function pageMarkup() {
  const app = document.querySelector("#app");
  if (!app) return { appPresent: false };
  const text = app.textContent || "";
  const announcement = document.querySelector(".autonogrammer-announcement");
  return {
    appPresent: true,
    strong: app.querySelectorAll("strong").length,
    img: app.querySelectorAll("img").length,
    script: app.querySelectorAll("script").length,
    unsupported: text.includes("Unsupported content"),
    userTurnEscaped: text.includes("<script>alert(1)</script>"),
    links: Array.from(app.querySelectorAll("a")).map((a) => ({ href: a.getAttribute("href"), text: a.textContent })),
    announcement: announcement ? announcement.textContent : "",
    errorMessages: Array.from(app.querySelectorAll(".autonogrammer-error-message")).map((e) => e.textContent),
  };
}

function pageContrast() {
  const button = document.querySelector("button.autonogrammer-icon-btn");
  if (!button) return null;
  const parse = (value) => {
    const match = value.match(/rgba?\(([^)]+)\)/);
    if (!match) return null;
    const parts = match[1].split(/[\s,/]+/).filter(Boolean).map(Number);
    return { r: parts[0], g: parts[1], b: parts[2], a: parts.length > 3 ? parts[3] : 1 };
  };
  const luminance = (c) => {
    const channel = (v) => {
      const x = v / 255;
      return x <= 0.03928 ? x / 12.92 : Math.pow((x + 0.055) / 1.055, 2.4);
    };
    return 0.2126 * channel(c.r) + 0.7152 * channel(c.g) + 0.0722 * channel(c.b);
  };
  const style = getComputedStyle(button);
  const foreground = parse(style.color);
  const own = parse(style.backgroundColor);
  let background = own && own.a > 0 ? own : null;
  for (let el = button.parentElement; !background && el; el = el.parentElement) {
    const parsed = parse(getComputedStyle(el).backgroundColor);
    if (parsed && parsed.a > 0) background = parsed;
  }
  if (!background) background = { r: 255, g: 255, b: 255, a: 1 };
  const l1 = luminance(foreground);
  const l2 = luminance(background);
  return {
    color: style.color,
    ownBackground: style.backgroundColor,
    effectiveBackground: `rgb(${background.r}, ${background.g}, ${background.b})`,
    contrastRatio: Math.round(((Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05)) * 100) / 100,
    outerHTML: button.outerHTML.slice(0, 200),
  };
}

async function runAxeInPage() {
  const run = await axe.run(document, {
    runOnly: { type: "tag", values: ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"] },
    resultTypes: ["violations", "incomplete"],
  });
  const pick = (items) => items.map((item) => ({
    id: item.id,
    impact: item.impact,
    targets: item.nodes.map((node) => node.target.join(" ")),
  }));
  return { axeVersion: axe.version, violations: pick(run.violations), incomplete: pick(run.incomplete) };
}

function clickSelector(selector) {
  const el = document.querySelector(selector);
  if (!el) return false;
  el.click();
  return true;
}

function clickConversation(index) {
  const items = document.querySelectorAll(".autonogrammer-agent-row");
  if (!items[index]) return false;
  items[index].click();
  return true;
}

function focusSelector(selector) {
  const el = document.querySelector(selector);
  if (!el) return false;
  el.focus();
  return true;
}

function pageErrorBannerShape() {
  const bar = document.querySelector("#blazor-error-ui");
  const dismiss = bar ? bar.querySelector(".dismiss") : null;
  return {
    present: !!bar,
    role: bar ? bar.getAttribute("role") : null,
    dismissTag: dismiss ? dismiss.tagName : null,
    dismissType: dismiss ? dismiss.getAttribute("type") : null,
    dismissName: dismiss ? (dismiss.getAttribute("aria-label") || "").trim() : "",
  };
}

function showErrorBanner() {
  const bar = document.querySelector("#blazor-error-ui");
  if (!bar) return false;
  bar.style.display = "block";
  window.__dismissClicks = 0;
  const dismiss = bar.querySelector(".dismiss");
  if (dismiss) dismiss.addEventListener("click", () => { window.__dismissClicks += 1; });
  return true;
}

function pageDismissState() {
  const dismiss = document.querySelector("#blazor-error-ui .dismiss");
  if (!dismiss) return { activeIsDismiss: false, outlineStyle: "none", outlineWidth: 0, clicks: 0 };
  const style = getComputedStyle(dismiss);
  return {
    activeIsDismiss: document.activeElement === dismiss,
    outlineStyle: style.outlineStyle,
    outlineWidth: parseFloat(style.outlineWidth) || 0,
    clicks: window.__dismissClicks ?? 0,
  };
}

function hideErrorBanner() {
  const bar = document.querySelector("#blazor-error-ui");
  if (bar) bar.style.display = "";
  return !!bar;
}

function blurActive() {
  if (document.activeElement && document.activeElement !== document.body) document.activeElement.blur();
  return document.activeElement ? document.activeElement.tagName : null;
}

function promptVisible(probe) {
  return `document.querySelector('textarea[aria-label="Message"]')?.value === ${JSON.stringify(probe)}`;
}

// ---- smoke run -------------------------------------------------------------------------------

const startedAt = new Date().toISOString();
const result = { app: APP_URL, apiOrigin: API_ORIGIN, commit: COMMIT, axeCore: "4.14.0", startedAt };
const assertions = {};

try {
  // Step 1: first render (runbook "Browser smoke", step 1).
  note("step 1: load app");
  await openPage(APP_URL);
  const shellReady = await waitFor(`!!document.querySelector(".autonogrammer-shell")`, 60000);
  await sleep(1000);
  const firstRender = await evalFn(pageFirstRender);
  const reflectionHits = (list) => list.filter((s) => s.includes("JsonSerializerIsReflectionDisabled")).length;
  result.firstRender = {
    rendered: firstRender.rendered && shellReady,
    errorBarVisible: firstRender.errorBarVisible,
    exceptionCount: exceptions.length,
    consoleErrorCount: consoleErrors.length,
    reflectionDisabledHits: reflectionHits([...exceptions, ...consoleErrors, firstRender.errorBarText]),
    exceptionSample: exceptions.slice(0, 5),
    consoleErrorSample: consoleErrors.slice(0, 5),
    logErrorSample: logErrors.slice(0, 5),
  };
  result.skipLinkInitial = await evalFn(pageSkipLink);
  result.shotInitial = await shot("web-first-render");

  // Axe, empty state (runbook "Re-run of axe-core", steps 5 to 7).
  note("step 2: axe, empty state");
  result.skipLinkBeforeAxeEmpty = await evalFn(pageSkipLink);
  await injectAxe();
  result.axeEmptyState = await evalValue(`(${runAxeInPage.toString()})()`);
  result.iconButtonContrastEmptyState = await evalFn(pageContrast);
  result.shotAxeEmpty = await shot("web-axe-empty");

  // Step 2 and 3: start a conversation, type the probe prompt, send it.
  note("step 3: send probe prompt");
  const errorsBeforePrompt = consoleErrors.length;
  const clickedNew = await evalFn(clickSelector, 'button[aria-label="New conversation"]');
  const composerReady = await waitFor(`!!document.querySelector('textarea[aria-label="Message"]')`, 30000);
  await evalFn(focusSelector, 'textarea[aria-label="Message"]');
  await cdp("Input.insertText", { text: PROBE });
  const typed = await waitFor(promptVisible(PROBE), 10000);
  const clickedSend = await evalFn(clickSelector, 'button[aria-label="Send message"]');
  const completed = await waitFor(
    `(document.querySelector(".autonogrammer-announcement")?.textContent ?? "").includes("Response complete.")`,
    120000,
  );
  await sleep(500);
  result.newConversationClicked = clickedNew;
  result.composerReady = composerReady;
  result.promptTyped = typed;
  result.sendClicked = clickedSend;
  result.responseCompleted = completed;
  result.probeChecks = await evalFn(pageMarkup);
  result.errorBarAfterPrompt = (await evalFn(pageFirstRender)).errorBarVisible;
  result.consoleErrorsAfterPrompt = consoleErrors.slice(errorsBeforePrompt);

  // Axe after the streamed response (runbook "Re-run of axe-core", steps 5 to 7).
  note("step 4: axe, after response");
  result.skipLinkBeforeAxeAfter = await evalFn(pageSkipLink);
  result.axeAfterResponse = await evalValue(`(${runAxeInPage.toString()})()`);
  result.iconButtonContrastAfterResponse = await evalFn(pageContrast);
  result.shotAxeAfterResponse = await shot("web-axe-after-response");
  result.shotVerticalSlice = await shot("web-vertical-slice", "webp");

  // Step 4: reload and open the stored conversation.
  note("step 5: reload and reopen conversation");
  await openPage(APP_URL);
  await waitFor(`!!document.querySelector(".autonogrammer-shell")`, 60000);
  const errorsBeforeReload = consoleErrors.length;
  await waitFor(`!!document.querySelector(".autonogrammer-agent-row")`, 30000);
  const conversationItems = await evalValue(`document.querySelectorAll(".autonogrammer-agent-row").length`);
  // Index.razor selects the first listed conversation on load, which can be an empty session created on
  // first load. Open each listed conversation until one renders its stored history.
  let openedIndex = -1;
  for (let index = 0; index < conversationItems && openedIndex < 0; index++) {
    await evalFn(clickConversation, index);
    if (await waitFor(`document.querySelectorAll("#app strong").length >= 1`, 10000)) openedIndex = index;
  }
  const historyRendered = openedIndex >= 0;
  await sleep(500);
  result.reload = {
    conversationItems,
    openedIndex,
    historyRendered,
    ...(await evalFn(pageMarkup)),
    errorBarVisible: (await evalFn(pageFirstRender)).errorBarVisible,
    consoleErrorsSinceReload: consoleErrors.slice(errorsBeforeReload),
  };

  // Skip link: on a fresh load, one Tab from the body focuses the skip link (runbook "Skip link").
  note("step 6: skip link on fresh load");
  await openPage(APP_URL);
  await waitFor(`!!document.querySelector(".autonogrammer-skip-link")`, 60000);
  await evalFn(blurActive);
  await pressTab();
  await sleep(300);
  result.skipLinkAfterTabFocus = await evalFn(pageSkipLink);
  result.shotSkipLinkFocused = await shot("web-skip-link-focused");

  // Error banner (phase 25.7, M-26): the dismiss is a named button, one Tab from the Reload link reaches it with a visible focus
  // outline, Enter and Space both activate it, and axe finds no violation while the banner is shown. Blazor binds its own dismiss
  // handler only when it shows the banner, so this step counts the click events a native button raises, which is what Enter
  // and Space must produce; it does not exercise the framework handler.
  note("step 7: error banner, keyboard and axe");
  const bannerShape = await evalFn(pageErrorBannerShape);
  await evalFn(showErrorBanner);
  await evalFn(focusSelector, "#blazor-error-ui .reload");
  await pressTab();
  await sleep(200);
  const tabReachesDismiss = await evalFn(pageDismissState);
  await pressKey({ key: "Enter", code: "Enter", windowsVirtualKeyCode: 13, nativeVirtualKeyCode: 13, text: "\r" });
  await pressKey({ key: " ", code: "Space", windowsVirtualKeyCode: 32, nativeVirtualKeyCode: 32, text: " " });
  await sleep(200);
  const clicksAfterEnterAndSpace = (await evalFn(pageDismissState)).clicks;
  await injectAxe();
  const axeShown = await evalValue(`(${runAxeInPage.toString()})()`);
  result.shotErrorBanner = await shot("web-error-banner");
  await evalFn(hideErrorBanner);
  result.errorBanner = { shape: bannerShape, tabReachesDismiss, clicksAfterEnterAndSpace, axeShown };

  // Assertions mirror the runbook's expected results.
  const probe = result.probeChecks;
  const reload = result.reload;
  // axe reports color-contrast as incomplete when it cannot measure a pair (overlapping boxes, pseudo-element backgrounds). The pairs are
  // computed from the tokens by the contrast tests and the incomplete entries stay in the JSON report, so only another incomplete rule
  // fails the run (the same rule as smoke-demo.mjs).
  const axeClean = (axeResult) => axeResult.violations.length === 0 && axeResult.incomplete.every((item) => item.id === "color-contrast");
  const contrastOk = (c) => c !== null && c.contrastRatio >= 3;
  assertions.firstRenderClean =
    result.firstRender.rendered &&
    !result.firstRender.errorBarVisible &&
    result.firstRender.exceptionCount === 0 &&
    result.firstRender.consoleErrorCount === 0 &&
    result.firstRender.reflectionDisabledHits === 0;
  assertions.responseCompleted = result.responseCompleted === true;
  assertions.probeMarkupAsExpected =
    probe.appPresent && probe.strong >= 1 && probe.img === 0 && probe.script === 0 && !probe.unsupported && probe.userTurnEscaped;
  assertions.noErrorBarOrConsoleErrorsAfterPrompt =
    !result.errorBarAfterPrompt && result.consoleErrorsAfterPrompt.length === 0;
  assertions.reloadHistoryRendersSame =
    reload.historyRendered && reload.img === 0 && reload.script === 0 && !reload.unsupported && !reload.errorBarVisible;
  assertions.axeEmptyClean = axeClean(result.axeEmptyState);
  assertions.axeAfterResponseClean = axeClean(result.axeAfterResponse);
  assertions.iconContrastMeetsNonText = contrastOk(result.iconButtonContrastEmptyState) && contrastOk(result.iconButtonContrastAfterResponse);
  assertions.skipLinkFocusedOnTab = result.skipLinkAfterTabFocus.activeIsSkip === true && result.skipLinkAfterTabFocus.targetExists === true;
  const banner = result.errorBanner;
  assertions.errorBannerAnnouncesAndDismissesByKeyboard =
    banner.shape.present &&
    banner.shape.role === "alert" &&
    banner.shape.dismissTag === "BUTTON" &&
    banner.shape.dismissType === "button" &&
    banner.shape.dismissName.length > 0 &&
    banner.tabReachesDismiss.activeIsDismiss === true &&
    banner.tabReachesDismiss.outlineStyle !== "none" &&
    banner.tabReachesDismiss.outlineWidth > 0 &&
    banner.clicksAfterEnterAndSpace === 2 &&
    banner.axeShown.violations.length === 0;
} catch (error) {
  result.error = error instanceof Error ? error.message : String(error);
  note(`smoke failed: ${result.error}`);
} finally {
  result.finishedAt = new Date().toISOString();
  result.assertions = assertions;
  result.passed = Object.keys(assertions).length > 0 && Object.values(assertions).every((v) => v === true) && !result.error;
  ws.close();
  process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
  process.exit(result.passed ? 0 : 1);
}
