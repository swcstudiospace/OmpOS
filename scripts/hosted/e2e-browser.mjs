#!/usr/bin/env node
// Browser end-to-end check of a hosted Autonogrammer web app, driven over the Chrome DevTools Protocol (CDP).
//
//   AUTONOGRAMMER_E2E_TOKEN=<owner token> node scripts/hosted/e2e-browser.mjs --url https://app.example/
//
// It launches Chrome headless with a fresh temporary profile (as scripts/web-smoke/run-smoke.sh does), and then:
//   1. opens the app and waits for the connect screen ("Access token" field) or, without one, the workbench;
//   2. runs axe-core on the connect screen;
//   3. types the token into the field labelled "Access token" and presses "Connect";
//   4. waits for the workbench, checks there is NO Demo banner, and runs axe-core on it;
//   5. starts a new conversation, sends a prompt in the composer and waits for the streamed answer;
//   6. checks that no credential was written to localStorage, sessionStorage or cookies;
//   7. checks that the whole run produced no console error, uncaught exception, failed or 4xx/5xx request;
//   8. presses "Sign out", expects the connect screen, reloads, and expects the connect screen again (the credential is
//      held in memory only).
// Elements are found by accessible role and name (aria-label, label, text), never by CSS class, except the Demo banner.
// The answer check expects the scripted fake model's default echo ("You said: <prompt>"); see README.md.
//
// Screenshots (PNG) and report.json are written to --out (default: a new directory under the system temp directory).
// The token is typed through CDP, is never put in a URL, a screenshot or the report, and is redacted from messages.
// Exit status: 0 every step passed, 1 a step failed, 2 bad usage or no browser.

import { spawn, execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { homedir, tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const AXE_VERSION = "4.14.0";
const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
const AXE_DIGEST_FILE = join(SCRIPT_DIR, "..", "web-smoke", `axe-core-${AXE_VERSION}.sha256`);
const AXE_TAGS = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"];

class Usage extends Error {}
class Failure extends Error {}
/** A failure a polling predicate raises to stop waiting at once. */
class Abort extends Failure {}

function parseArgs(argv) {
  const options = {
    url: process.env.APP_URL ?? "",
    token: process.env.AUTONOGRAMMER_E2E_TOKEN ?? "",
    out: "",
    chrome: process.env.CHROME_BIN ?? "",
    axeJs: process.env.AXE_JS ?? "",
    prompt: "Hello from the hosted end-to-end run",
    answerTimeoutMs: 120_000,
    loadTimeoutMs: 90_000,
    skipAxe: false,
    skipSignOut: false,
    keepProfile: false,
    allowInsecure: false,
  };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    const value = () => {
      if (i + 1 >= argv.length) throw new Usage(`${arg} needs a value`);
      return argv[++i];
    };
    const integer = () => {
      const parsed = Number(value());
      if (!Number.isInteger(parsed) || parsed <= 0) throw new Usage(`${arg} needs a positive integer`);
      return parsed;
    };
    if (arg === "--url") options.url = value();
    else if (arg === "--token") options.token = value();
    else if (arg === "--out") options.out = value();
    else if (arg === "--chrome") options.chrome = value();
    else if (arg === "--axe-js") options.axeJs = value();
    else if (arg === "--prompt") options.prompt = value();
    else if (arg === "--answer-timeout-ms") options.answerTimeoutMs = integer();
    else if (arg === "--load-timeout-ms") options.loadTimeoutMs = integer();
    else if (arg === "--skip-axe") options.skipAxe = true;
    else if (arg === "--skip-sign-out") options.skipSignOut = true;
    else if (arg === "--keep-profile") options.keepProfile = true;
    else if (arg === "--allow-insecure") options.allowInsecure = true;
    else if (arg === "--help" || arg === "-h") throw new Usage("help");
    else throw new Usage(`unknown argument ${arg}`);
  }
  if (!options.url) throw new Usage("the app URL is required: --url <url> or APP_URL");
  let url;
  try {
    url = new URL(options.url);
  } catch {
    throw new Usage(`--url is not a URL: ${options.url}`);
  }
  if (!["http:", "https:"].includes(url.protocol)) throw new Usage("--url must be an http(s) URL");
  if (url.protocol === "http:" && !["127.0.0.1", "localhost", "[::1]"].includes(url.hostname) && !options.allowInsecure) {
    throw new Usage("refusing to type a credential into a plain http page on a non-loopback host; use https or --allow-insecure");
  }
  if (url.search || url.hash || url.username || url.password) throw new Usage("the app URL must carry no query, fragment or credentials");
  return options;
}

const HELP = `usage: node scripts/hosted/e2e-browser.mjs --url <app url> [--token <t>] [--out <dir>] [--chrome <bin>] [--axe-js <axe.min.js>]
       [--prompt <text>] [--answer-timeout-ms 120000] [--load-timeout-ms 90000] [--skip-axe] [--skip-sign-out] [--keep-profile]
environment: APP_URL, AUTONOGRAMMER_E2E_TOKEN, CHROME_BIN, AXE_JS`;

let options;
try {
  options = parseArgs(process.argv.slice(2));
} catch (error) {
  if (!(error instanceof Usage)) throw error;
  process.stderr.write(error.message === "help" ? `${HELP}\n` : `e2e-browser: ${error.message}\n${HELP}\n`);
  process.exit(error.message === "help" ? 0 : 2);
}

const secrets = [options.token].filter((secret) => secret.length >= 8);
const redact = (text) => secrets.reduce((acc, secret) => acc.split(secret).join("[redacted]"), String(text));
const note = (message) => process.stderr.write(`${new Date().toISOString()} ${redact(message)}\n`);
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const check = (condition, message) => {
  if (!condition) throw new Failure(message);
};

const outDir = options.out || mkdtempSync(join(tmpdir(), "autonogrammer-e2e-browser-"));
mkdirSync(outDir, { recursive: true });

// ---- axe-core, pinned by digest -------------------------------------------------------------------------------------

function loadAxe() {
  const expected = readFileSync(AXE_DIGEST_FILE, "utf8").trim().split(/\s+/)[0];
  let file = options.axeJs;
  if (!file) {
    const cache = join(tmpdir(), `autonogrammer-axe-${AXE_VERSION}`);
    file = join(cache, "package", "axe.min.js");
    if (!existsSync(file)) {
      mkdirSync(cache, { recursive: true });
      note(`downloading axe-core@${AXE_VERSION} (the pinned version of scripts/web-smoke)`);
      execFileSync("npm", ["pack", `axe-core@${AXE_VERSION}`, "--silent"], { cwd: cache, stdio: ["ignore", "ignore", "pipe"] });
      execFileSync("tar", ["-xzf", `axe-core-${AXE_VERSION}.tgz`, "package/axe.min.js"], { cwd: cache });
    }
  }
  const source = readFileSync(file);
  const actual = createHash("sha256").update(source).digest("hex");
  if (actual !== expected) throw new Usage(`axe-core digest mismatch: ${file} is ${actual}, scripts/web-smoke pins ${expected}`);
  return source.toString("utf8");
}

// ---- Chrome ---------------------------------------------------------------------------------------------------------

function findChrome() {
  if (options.chrome) return options.chrome;
  for (const name of ["chromium", "chromium-browser", "google-chrome", "chrome"]) {
    try {
      return execFileSync("which", [name], { encoding: "utf8" }).trim();
    } catch {
      // try the next name
    }
  }
  const cache = join(homedir(), ".cache", "ms-playwright");
  if (existsSync(cache)) {
    for (const dir of readdirSafe(cache).filter((name) => name.startsWith("chromium-")).sort().reverse()) {
      const candidate = join(cache, dir, "chrome-linux64", "chrome");
      if (existsSync(candidate)) return candidate;
    }
  }
  throw new Usage("no Chromium binary found; install one or set CHROME_BIN / --chrome");
}

function readdirSafe(dir) {
  try {
    return execFileSync("ls", ["-1", dir], { encoding: "utf8" }).split("\n").filter(Boolean);
  } catch {
    return [];
  }
}

async function launchChrome() {
  const chrome = findChrome();
  const profile = mkdtempSync(join(tmpdir(), "autonogrammer-e2e-profile-"));
  const child = spawn(chrome, ["--headless", "--no-sandbox", "--disable-dev-shm-usage", "--remote-debugging-port=0", `--user-data-dir=${profile}`, "--window-size=1280,900", "about:blank"], {
    stdio: ["ignore", "ignore", "ignore"],
  });
  const portFile = join(profile, "DevToolsActivePort");
  for (let i = 0; i < 120 && !(existsSync(portFile) && readFileSync(portFile, "utf8").trim() !== ""); i++) await sleep(250);
  if (!existsSync(portFile)) {
    child.kill("SIGKILL");
    throw new Failure(`Chrome exposed no DevTools port (${chrome})`);
  }
  const port = readFileSync(portFile, "utf8").split("\n")[0].trim();
  const stop = async () => {
    const exited = new Promise((resolve) => child.once("exit", resolve));
    child.kill("SIGKILL");
    await Promise.race([exited, sleep(5000)]);
    if (options.keepProfile) return;
    try {
      rmSync(profile, { recursive: true, force: true, maxRetries: 10, retryDelay: 200 });
    } catch {
      note(`could not remove the temporary Chrome profile ${profile}`);
    }
  };
  return { port, stop, chrome };
}

// ---- CDP ------------------------------------------------------------------------------------------------------------

async function connectCdp(port) {
  const meta = await (await fetch(`http://127.0.0.1:${port}/json/version`)).json();
  const ws = new WebSocket(meta.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    ws.addEventListener("open", resolve, { once: true });
    ws.addEventListener("error", () => reject(new Failure("CDP WebSocket error")), { once: true });
  });
  let nextId = 1;
  const pending = new Map();
  let listeners = [];
  ws.addEventListener("message", (event) => {
    const msg = JSON.parse(event.data);
    if (msg.id !== undefined && pending.has(msg.id)) {
      const { resolve, reject } = pending.get(msg.id);
      pending.delete(msg.id);
      if (msg.error) reject(new Error(msg.error.message));
      else resolve(msg.result);
      return;
    }
    if (msg.method) for (const listener of [...listeners]) listener(msg);
  });
  const send = (method, params = {}, sessionId) =>
    new Promise((resolve, reject) => {
      const id = nextId++;
      pending.set(id, { resolve, reject });
      ws.send(JSON.stringify(sessionId ? { id, method, params, sessionId } : { id, method, params }));
    });
  const { targetId } = await send("Target.createTarget", { url: "about:blank" });
  const { sessionId } = await send("Target.attachToTarget", { targetId, flatten: true });
  return {
    cdp: (method, params) => send(method, params, sessionId),
    sessionId,
    on(listener) {
      listeners.push(listener);
    },
    waitForEvent(method, ms) {
      return new Promise((resolve, reject) => {
        const timer = setTimeout(() => reject(new Failure(`timed out waiting for ${method}`)), ms);
        const listener = (msg) => {
          if (msg.sessionId !== sessionId || msg.method !== method) return;
          clearTimeout(timer);
          listeners = listeners.filter((l) => l !== listener);
          resolve(msg.params);
        };
        listeners.push(listener);
      });
    },
    close: () => ws.close(),
  };
}

// ---- in-page helpers: find by role and accessible name --------------------------------------------------------------

const PAGE_HELPERS = `(() => {
  if (window.__e2e) return window.__e2e;
  const visible = (el) => {
    const rect = el.getBoundingClientRect();
    const style = getComputedStyle(el);
    return rect.width > 0 && rect.height > 0 && style.visibility !== "hidden" && style.display !== "none";
  };
  const text = (el) => (el.textContent || "").replace(/\\s+/g, " ").trim();
  const nameOf = (el) => {
    const by = el.getAttribute("aria-labelledby");
    if (by) {
      const joined = by.split(/\\s+/).map((id) => document.getElementById(id)).filter(Boolean).map(text).join(" ").trim();
      if (joined) return joined;
    }
    const aria = el.getAttribute("aria-label");
    if (aria && aria.trim()) return aria.trim();
    if (el.labels && el.labels.length) {
      const joined = [...el.labels].map(text).join(" ").trim();
      if (joined) return joined;
    }
    if (el.tagName === "BUTTON" || el.tagName === "A" || el.getAttribute("role") === "button") {
      const inner = text(el);
      if (inner) return inner;
    }
    if (el.tagName === "INPUT" && ["button", "submit"].includes(el.type)) return el.value || "";
    return el.getAttribute("title") || el.getAttribute("placeholder") || "";
  };
  const selectors = {
    button: 'button,[role="button"],input[type="button"],input[type="submit"]',
    textbox: 'input:not([type="hidden"]):not([type="checkbox"]):not([type="radio"]):not([type="button"]):not([type="submit"]),textarea,[role="textbox"]',
    alert: '[role="alert"]',
    status: '[role="status"]',
  };
  const find = (role, name) => {
    const want = String(name).trim().toLowerCase();
    for (const el of document.querySelectorAll(selectors[role])) {
      if (visible(el) && nameOf(el).trim().toLowerCase() === want) return el;
    }
    return null;
  };
  window.__e2e = {
    state(role, name) {
      const el = find(role, name);
      if (!el) return { found: false, enabled: false };
      return { found: true, enabled: !el.disabled && el.getAttribute("aria-disabled") !== "true" };
    },
    center(role, name) {
      const el = find(role, name);
      if (!el) return null;
      el.scrollIntoView({ block: "center", inline: "center" });
      const rect = el.getBoundingClientRect();
      return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2, width: rect.width, height: rect.height };
    },
    mask(role, name) {
      const el = find(role, name);
      if (el) el.style.webkitTextSecurity = "disc";
      return !!el;
    },
    focus(role, name) {
      const el = find(role, name);
      if (!el) return false;
      el.focus();
      return document.activeElement === el;
    },
    names(role) {
      return [...document.querySelectorAll(selectors[role])].filter(visible).map((el) => text(el) || nameOf(el));
    },
    bodyText: () => document.body ? document.body.innerText : "",
    demoBanner() {
      const byClass = document.querySelector(".autonogrammer-demo-banner");
      if (byClass) return text(byClass);
      for (const el of document.querySelectorAll(selectors.status)) if (visible(el) && /^Demo:/.test(text(el))) return text(el);
      return null;
    },
    storage(needle) {
      const hits = [];
      const scan = (label, store) => {
        for (let i = 0; i < store.length; i++) {
          const key = store.key(i);
          if (String(key).includes(needle) || String(store.getItem(key)).includes(needle)) hits.push(label);
        }
      };
      scan("localStorage", localStorage);
      scan("sessionStorage", sessionStorage);
      if (document.cookie.includes(needle)) hits.push("cookie");
      return hits;
    },
  };
  return window.__e2e;
})()`;

// ---- the run --------------------------------------------------------------------------------------------------------

const report = { app: options.url, startedAt: new Date().toISOString(), outDir, mode: "unknown", steps: [], screenshots: [], axe: {}, problems: {} };
const consoleErrors = [];
const exceptions = [];
const logErrors = [];
const failedRequests = [];
let browser = null;

function writeReport(code) {
  report.finishedAt = new Date().toISOString();
  report.result = code === 0 ? "passed" : "failed";
  const text = `${redact(JSON.stringify(report, null, 2))}\n`;
  writeFileSync(join(outDir, "report.json"), text);
  process.stdout.write(text);
}

async function main() {
  const axeSource = options.skipAxe ? null : loadAxe();
  browser = await launchChrome();
  note(`chrome ${browser.chrome}; output ${outDir}`);
  const page = await connectCdp(browser.port);
  const { cdp } = page;

  const describe = (object) => (object ? (object.value !== undefined ? String(object.value) : (object.description ?? object.unserializableValue ?? "")) : "");
  const requests = new Map();
  page.on((msg) => {
    if (msg.sessionId !== page.sessionId) return;
    if (msg.method === "Runtime.exceptionThrown") {
      const details = msg.params.exceptionDetails;
      exceptions.push(describe(details.exception) || details.text);
    } else if (msg.method === "Runtime.consoleAPICalled" && msg.params.type === "error") {
      consoleErrors.push(msg.params.args.map(describe).join(" "));
    } else if (msg.method === "Log.entryAdded" && msg.params.entry.level === "error") {
      const entry = msg.params.entry;
      logErrors.push(`${entry.source}: ${entry.text} ${entry.url ?? ""}`.trim());
    } else if (msg.method === "Network.requestWillBeSent") {
      requests.set(msg.params.requestId, { method: msg.params.request.method, url: msg.params.request.url });
    } else if (msg.method === "Network.responseReceived") {
      const { response, requestId } = msg.params;
      if (response.status >= 400) failedRequests.push(`${response.status} ${requests.get(requestId)?.method ?? ""} ${response.url}`.replace(/\?.*$/, ""));
    } else if (msg.method === "Network.loadingFailed" && !msg.params.canceled) {
      failedRequests.push(`failed ${requests.get(msg.params.requestId)?.url ?? msg.params.requestId}: ${msg.params.errorText}`.replace(/\?.*$/, ""));
    }
  });
  await cdp("Page.enable");
  await cdp("Runtime.enable");
  await cdp("Log.enable");
  await cdp("Network.enable");
  await cdp("Emulation.setDeviceMetricsOverride", { width: 1280, height: 900, deviceScaleFactor: 1, mobile: false });

  const evalValue = async (expression) => {
    const result = await cdp("Runtime.evaluate", { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw new Failure(`page evaluation failed: ${result.exceptionDetails.exception?.description ?? result.exceptionDetails.text}`);
    return result.result.value;
  };
  const helper = (method, ...args) => evalValue(`${PAGE_HELPERS}.${method}(${args.map((arg) => JSON.stringify(arg)).join(",")})`);
  const waitFor = async (label, predicate, ms) => {
    const deadline = Date.now() + ms;
    for (;;) {
      try {
        const value = await predicate();
        if (value) return value;
      } catch (error) {
        if (error instanceof Abort) throw error;
        // the page may still be rendering
      }
      if (Date.now() > deadline) throw new Failure(`timed out after ${ms} ms waiting for ${label}`);
      await sleep(250);
    }
  };
  const waitForControl = (role, name, ms) => waitFor(`${role} "${name}"`, async () => (await helper("state", role, name)).found, ms);
  const shot = async (name) => {
    // A screenshot must never show a typed credential, even if the field is not a password input.
    await helper("mask", "textbox", "Access token").catch(() => false);
    const { data } = await cdp("Page.captureScreenshot", { format: "png" });
    const file = `${name}.png`;
    writeFileSync(join(outDir, file), Buffer.from(data, "base64"));
    report.screenshots.push(file);
    note(`screenshot ${file}`);
  };
  const click = async (role, name) => {
    await waitFor(`${role} "${name}" enabled`, async () => (await helper("state", role, name)).enabled, 30_000);
    const point = await helper("center", role, name);
    check(point !== null, `${role} "${name}" disappeared before the click`);
    check(point.width >= 24 && point.height >= 24, `${role} "${name}" is ${Math.round(point.width)}x${Math.round(point.height)} CSS px, below the 24x24 target size`);
    const base = { x: point.x, y: point.y, button: "left", clickCount: 1 };
    await cdp("Input.dispatchMouseEvent", { type: "mouseMoved", x: point.x, y: point.y });
    await cdp("Input.dispatchMouseEvent", { type: "mousePressed", ...base });
    await cdp("Input.dispatchMouseEvent", { type: "mouseReleased", ...base });
  };
  const type = async (role, name, text) => {
    await waitForControl(role, name, 30_000);
    check(await helper("focus", role, name), `${role} "${name}" could not take focus`);
    await cdp("Input.insertText", { text });
  };
  const runAxe = async (label) => {
    if (axeSource === null) return { axe: "skipped (--skip-axe)" };
    const injected = await cdp("Runtime.evaluate", { expression: axeSource, returnByValue: false });
    check(!injected.exceptionDetails, "axe-core failed to load in the page");
    const result = await evalValue(`axe.run(document, { runOnly: { type: "tag", values: ${JSON.stringify(AXE_TAGS)} }, resultTypes: ["violations", "incomplete"] }).then((run) => ({
      version: axe.version,
      violations: run.violations.map((item) => ({ id: item.id, impact: item.impact, targets: item.nodes.map((node) => node.target.join(" ")) })),
      incomplete: run.incomplete.map((item) => ({ id: item.id, impact: item.impact, targets: item.nodes.map((node) => node.target.join(" ")) })),
    }))`);
    report.axe[label] = result;
    check(result.violations.length === 0, `axe found ${result.violations.length} violation(s) on ${label}: ${result.violations.map((v) => `${v.id} (${v.impact}) at ${v.targets.slice(0, 3).join(" | ")}`).join("; ")}`);
    return { axeVersion: result.version, incomplete: result.incomplete.length };
  };
  const problemsNow = () => ({ consoleErrors: [...consoleErrors], exceptions: [...exceptions], logErrors: [...logErrors], failedRequests: [...failedRequests] });
  const assertNoProblems = (when) => {
    const now = problemsNow();
    report.problems[when] = now;
    const lines = Object.entries(now).flatMap(([kind, list]) => list.map((entry) => `${kind}: ${redact(entry).slice(0, 200)}`));
    check(lines.length === 0, `${lines.length} browser problem(s) ${when}: ${lines.slice(0, 5).join(" || ")}`);
  };
  const openPage = async (url) => {
    const loaded = page.waitForEvent("Page.loadEventFired", options.loadTimeoutMs);
    const navigation = await cdp("Page.navigate", { url });
    check(!navigation.errorText, `navigation to ${url} failed: ${navigation.errorText}`);
    await loaded;
  };

  const step = async (name, fn) => {
    const started = Date.now();
    try {
      const detail = (await fn()) ?? {};
      if (detail.skipped) {
        report.steps.push({ name, status: "skipped", detail: detail.skipped });
        note(`SKIP ${name}: ${detail.skipped}`);
        return true;
      }
      report.steps.push({ name, status: "passed", ms: Date.now() - started, ...detail });
      note(`PASS ${name} (${Date.now() - started} ms)`);
      return true;
    } catch (error) {
      const message = redact(error instanceof Failure ? error.message : `${error?.name ?? "Error"}: ${error?.message ?? error}`);
      report.steps.push({ name, status: "failed", ms: Date.now() - started, detail: message });
      note(`FAIL ${name}: ${message}`);
      try {
        await shot(`failure-${name}`);
      } catch {
        // the page may be gone
      }
      return false;
    }
  };

  const bannerOf = () => helper("demoBanner");
  let needsToken = false;

  const flow = [
    ["load", async () => {
      await openPage(options.url);
      const found = await waitFor('the connect screen or the workbench', async () => {
        if ((await helper("state", "textbox", "Access token")).found) return "connect";
        if ((await helper("state", "button", "New conversation")).found) return "workbench";
        return null;
      }, options.loadTimeoutMs);
      needsToken = found === "connect";
      report.mode = needsToken ? "owner-token" : "no-token-screen";
      return { screen: found };
    }],
    ["connect-screen", async () => {
      if (!needsToken) return { skipped: "no Access token field: the deployment needs no sign-in" };
      check(await bannerOf() === null, "the connect screen shows the Demo banner");
      await shot("connect");
      return runAxe("connect");
    }],
    ["connect", async () => {
      if (!needsToken) return { skipped: "no Access token field" };
      check(options.token !== "", "the deployment asks for an access token: set AUTONOGRAMMER_E2E_TOKEN");
      await type("textbox", "Access token", options.token);
      await click("button", "Connect");
      await waitFor('the workbench after Connect', async () => {
        if ((await helper("state", "button", "New conversation")).found) return true;
        const alerts = await helper("names", "alert");
        const message = alerts.map((text) => text.trim()).filter(Boolean).join(" / ");
        if (message) throw new Abort(`Connect was refused: ${message}`);
        return false;
      }, 60_000).catch((error) => {
        throw error instanceof Failure && error.message.startsWith("Connect was refused") ? error : new Failure(`the workbench did not appear after Connect: ${error.message}`);
      });
      check((await helper("state", "button", "Sign out")).found, 'the workbench has no "Sign out" button');
      return {};
    }],
    ["workbench", async () => {
      const banner = await bannerOf();
      check(banner === null, `the workbench shows a Demo banner: "${banner}"`);
      await shot("workbench");
      return runAxe("workbench");
    }],
    ["prompt-and-answer", async () => {
      await click("button", "New conversation");
      await waitForControl("textbox", "Message", 30_000);
      await type("textbox", "Message", options.prompt);
      await click("button", "Send message");
      await waitFor("the streamed answer", async () => {
        const body = await helper("bodyText");
        return body.includes("You said:") && body.includes(options.prompt.slice(0, 40));
      }, options.answerTimeoutMs);
      const announced = await waitFor('the "Response complete." announcement', () => evalValue(`Array.from(document.querySelectorAll('[role="status"], [aria-live]')).map((el) => el.textContent).join(" ").includes("Response complete.")`), 20_000).then(() => true, () => false);
      check(await bannerOf() === null, "a Demo banner appeared while answering");
      await shot("answer");
      const axe = await runAxe("answer");
      return { responseCompleteAnnounced: announced, ...axe };
    }],
    ["credential-storage", async () => {
      if (!needsToken) return { skipped: "no token was used" };
      const hits = await helper("storage", options.token);
      check(hits.length === 0, `the access token was found in ${hits.join(", ")}: a credential must be held in memory only`);
      return {};
    }],
    ["no-browser-problems", async () => {
      assertNoProblems("before sign-out");
      return {};
    }],
    ["sign-out", async () => {
      if (!needsToken || options.skipSignOut) return { skipped: needsToken ? "--skip-sign-out" : "no sign-in" };
      await click("button", "Sign out");
      await waitForControl("textbox", "Access token", 30_000);
      await openPage(options.url);
      await waitForControl("textbox", "Access token", options.loadTimeoutMs);
      check((await helper("storage", options.token)).length === 0, "the token is in browser storage after sign-out");
      return {};
    }],
  ];

  let ok = true;
  for (const [name, fn] of flow) {
    if (!ok) {
      report.steps.push({ name, status: "skipped", detail: "an earlier step failed" });
      continue;
    }
    ok = await step(name, fn);
  }

  page.close();
  return report.steps.every((entry) => entry.status !== "failed") ? 0 : 1;
}

let code = 1;
try {
  code = await main();
} catch (error) {
  if (error instanceof Usage) {
    process.stderr.write(`e2e-browser: ${error.message}\n`);
    code = 2;
  } else {
    const message = redact(error instanceof Failure ? error.message : `${error?.name ?? "Error"}: ${error?.message ?? error}`);
    report.steps.push({ name: "harness", status: "failed", detail: message });
    note(`FAIL harness: ${message}`);
    code = 1;
  }
} finally {
  await browser?.stop();
}
if (code !== 2) writeReport(code);
process.exit(code);
