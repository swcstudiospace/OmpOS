// Captures screenshots of the OmpOS web head with the owned headless Chrome over the DevTools protocol (no browser library needed).
//
// It serves a published bundle on 127.0.0.1 (scripts/web-demo/serve-demo.sh), loads it, and records one PNG per screen:
//   agents, completed (Changes bar), approval, kanban, settings pages, command palette, narrow drawer.
// The Demo build answers every API route inside the browser, so the screens show scripted data: every caption must say so.
//
// Usage:  node scripts/web-demo/capture.mjs
// Env:    BUNDLE (default output/demo), OUT (default /tmp/ompos-shots), THEME light|dark (default light), W and H (default 1440x900),
//         DEMO_PORT (default 5190), CHROME_BIN (default google-chrome), PREFIX (file name prefix, default empty),
//         URL (capture an already hosted deployment instead of serving BUNDLE locally)
import { spawn } from "node:child_process";
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "../..");
const PORT = process.env.DEMO_PORT || "5190";
const REMOTE = process.env.URL || "";
const BUNDLE = resolve(process.env.BUNDLE || join(root, "output/demo"));
const OUT = resolve(process.env.OUT || "/tmp/ompos-shots");
const THEME = process.env.THEME || "light";
const W = Number(process.env.W || 1440);
const H = Number(process.env.H || 900);
const PREFIX = process.env.PREFIX || "";
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
mkdirSync(OUT, { recursive: true });

const server = REMOTE ? null : spawn("bash", [join(root, "scripts/web-demo/serve-demo.sh"), BUNDLE], { env: { ...process.env, DEMO_PORT: PORT }, stdio: "ignore" });
const profile = mkdtempSync(join(tmpdir(), "ompos-shots-"));
const chrome = spawn(process.env.CHROME_BIN || "google-chrome", [
  "--headless=new", "--no-sandbox", "--disable-dev-shm-usage", "--remote-debugging-port=0", `--user-data-dir=${profile}`,
  `--window-size=${W},${H}`, "--hide-scrollbars", "--force-device-scale-factor=1", "about:blank",
], { stdio: "ignore" });
const cleanup = () => { try { chrome.kill(); } catch { /* already gone */ } try { server?.kill(); } catch { /* already gone */ } };
process.on("exit", cleanup);

let devtoolsPort;
for (let i = 0; i < 60 && !devtoolsPort; i++) { try { devtoolsPort = readFileSync(join(profile, "DevToolsActivePort"), "utf8").split("\n")[0]; } catch { await sleep(500); } }
for (let i = 0; i < 60 && !REMOTE; i++) { try { if ((await fetch(`http://127.0.0.1:${PORT}/`)).ok) break; } catch { /* not up yet */ } await sleep(500); }
const targets = await (await fetch(`http://127.0.0.1:${devtoolsPort}/json/list`)).json();
const page = targets.find((t) => t.type === "page");
const ws = new WebSocket(page.webSocketDebuggerUrl);
await new Promise((r) => ws.addEventListener("open", r, { once: true }));
let nextId = 1; const pending = new Map(); const events = [];
ws.addEventListener("message", (e) => {
  const m = JSON.parse(e.data);
  if (m.id && pending.has(m.id)) { const { res, rej } = pending.get(m.id); pending.delete(m.id); if (m.error) rej(new Error(m.error.message)); else res(m.result); } else { events.push(m); }
});
const send = (method, params = {}) => new Promise((res, rej) => { const id = nextId++; pending.set(id, { res, rej }); ws.send(JSON.stringify({ id, method, params })); });
const evalJs = async (expression) => {
  const r = await send("Runtime.evaluate", { expression, returnByValue: true, awaitPromise: true });
  if (r.exceptionDetails) throw new Error(JSON.stringify(r.exceptionDetails).slice(0, 300));
  return r.result.value;
};
const wait = async (expr, ms = 20000) => { for (let t = 0; t < ms; t += 250) { try { if (await evalJs(expr)) return true; } catch { /* page still booting */ } await sleep(250); } return false; };
const click = (sel) => evalJs(`(() => { const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return false; e.click(); return true; })()`);
const shot = async (name) => { const r = await send("Page.captureScreenshot", { format: "png" }); writeFileSync(join(OUT, `${PREFIX}${name}.png`), Buffer.from(r.data, "base64")); };

await send("Page.enable"); await send("Runtime.enable"); await send("Log.enable"); await send("Network.enable");
await send("Emulation.setDeviceMetricsOverride", { width: W, height: H, deviceScaleFactor: 1, mobile: W < 700 });
// The theme choice is read by theme-boot.js before first paint.
await send("Page.addScriptToEvaluateOnNewDocument", { source: `localStorage.setItem('ompos.theme', ${JSON.stringify(THEME)});` });
await send("Page.navigate", { url: REMOTE || `http://127.0.0.1:${PORT}/` });
const report = { theme: THEME, size: `${W}x${H}`, shots: [] };
report.ready = await wait("document.querySelectorAll('.ompos-agent-row').length > 0 || document.querySelectorAll('[role=navigation] li').length > 0", 120000);
await sleep(2500);
const take = async (name) => { await sleep(600); await shot(name); report.shots.push(name); };

await take("01-agents");
const narrow = W < 840;
if (narrow) {
  report.drawerOpen = await click("button[aria-controls][aria-expanded], .ompos-drawer-toggle, button[aria-label*='agents' i]");
  await take("02-drawer");
  await evalJs("document.dispatchEvent(new KeyboardEvent('keydown', {key: 'Escape', bubbles: true}))");
}
const rowIds = await evalJs("[...document.querySelectorAll('[data-row-id]')].map(e => e.getAttribute('data-row-id'))");
report.rows = rowIds;
const open = async (suffix) => { const id = rowIds.find((r) => r.endsWith(suffix)); if (!id) return false; if (narrow) { await click("button[aria-label*='agents' i], .ompos-drawer-toggle"); await sleep(400); } return click(`[data-row-id='${id}']`); };
if (await open("d004")) { await sleep(1800); await take("03-completed-changes"); }
if (await open("d002")) { await sleep(1800); await take("04-approval"); }
if (await open("d005")) { await sleep(1800); await take("05-failed"); }
if (await open("d003")) { await sleep(2200); await take("06-terminal"); }
if (await open("d001")) { await sleep(2500); await take("07-running"); }

// Slash palette in the composer of the running session.
await evalJs(`(() => { const t = document.querySelector('textarea'); if (!t) return; t.focus(); t.value = '/'; t.dispatchEvent(new Event('input', {bubbles: true})); })()`);
await sleep(900); await take("08-slash-palette");
await evalJs(`(() => { const t = document.querySelector('textarea'); if (!t) return; t.value = ''; t.dispatchEvent(new Event('input', {bubbles: true})); })()`);

// Kanban.
if (!narrow) {
  if (await click("#ompos-tab-kanban")) { await sleep(1200); await take("09-kanban"); await click("#ompos-tab-agents"); await sleep(500); }
}

// Settings: every nav item.
if (await click(".ompos-rail-settings")) {
  await wait("!!document.querySelector('.ompos-settings')", 8000);
  await sleep(1200);
  const items = await evalJs("[...document.querySelectorAll('.ompos-settings-nav-item')].map((b, i) => ({ i, t: b.textContent.trim() }))");
  report.settingsPages = items.map((x) => x.t);
  for (const it of items) {
    await evalJs(`document.querySelectorAll('.ompos-settings-nav-item')[${it.i}].click()`);
    await sleep(700);
    await take(`10-settings-${String(it.i).padStart(2, "0")}-${it.t.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "")}`);
  }
  // Models page with the region and kind filters narrowed: the provider coverage the owner asked for.
  const modelsIndex = items.findIndex((x) => /^models/i.test(x.t));
  if (modelsIndex >= 0) {
    const pick = (labelText, optionPrefix) => evalJs(`(() => { const sel = [...document.querySelectorAll('select')].find((s) => { const l = s.id && document.querySelector('label[for="' + s.id + '"]'); return (l && l.textContent.includes(${JSON.stringify(labelText)})) || (s.getAttribute('aria-label') || '').includes(${JSON.stringify(labelText)}); }); if (!sel) return false; const o = [...sel.options].find((x) => x.text.startsWith(${JSON.stringify(optionPrefix)})); if (!o) return false; sel.value = o.value; sel.dispatchEvent(new Event('change', { bubbles: true })); return true; })()`);
    await evalJs(`document.querySelectorAll('.ompos-settings-nav-item')[${modelsIndex}].click()`);
    await sleep(900);
    report.modelsFilterCn = (await pick("region", "China")) && (await pick("kind", "Coding plans"));
    await sleep(700);
    await evalJs("[...document.querySelectorAll('h2,h3,h4')].find((h) => h.textContent.includes('Provider directory'))?.scrollIntoView({ block: 'start' })");
    await sleep(400);
    await take("11-models-cn-coding-plans");
    report.modelsFilterEu = (await pick("region", "Europe")) && (await pick("kind", "All"));
    await sleep(700);
    await evalJs("[...document.querySelectorAll('h2,h3,h4')].find((h) => h.textContent.includes('Provider directory'))?.scrollIntoView({ block: 'start' })");
    await sleep(400);
    await take("12-models-eu");
  }
  await evalJs("document.querySelector('.ompos-settings')?.dispatchEvent(new KeyboardEvent('keydown', {key: 'Escape', bubbles: true}))");
}

report.errors = events.filter((e) => e.method === "Runtime.exceptionThrown" || (e.method === "Log.entryAdded" && e.params.entry.level === "error")).map((e) => JSON.stringify(e.params).slice(0, 200));
report.failedRequests = events.filter((e) => e.method === "Network.loadingFailed").length;
writeFileSync(join(OUT, `${PREFIX}report.json`), JSON.stringify(report, null, 2));
console.log(JSON.stringify(report, null, 2));
cleanup();
process.exit(0);
