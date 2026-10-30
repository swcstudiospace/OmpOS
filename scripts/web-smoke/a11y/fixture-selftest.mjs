#!/usr/bin/env node
// Browser self-test of the in-page half of the accessibility checks. It serves one small fixture page that contains known defects (a button without a
// name, a focus indicator removed, 16 px targets side by side, a 600 px wide box, clipped text, a one-second transition, an unlabelled field) and checks
// that the probes of page-lib.mjs and the rules of logic.mjs report exactly those. It proves the harness (CDP client, library injection, Tab walk,
// media emulation); it says nothing about the app. Needs a Chrome or Chromium (CHROME_BIN, chromium, google-chrome, or the Playwright download) and
// prints SKIP with exit code 0 when there is none. Not part of the smoke gate:
//
//   node scripts/web-smoke/a11y/fixture-selftest.mjs

import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { existsSync, mkdtempSync, readdirSync, readFileSync, rmSync } from "node:fs";
import { createServer } from "node:http";
import { homedir, tmpdir } from "node:os";
import { join } from "node:path";
import { Page } from "./cdp.mjs";
import { analyzeAffordances, analyzeMotion, analyzeReflow, analyzeStops, analyzeTargets, analyzeTextSpacing } from "./logic.mjs";
import { walk } from "./keyboard.mjs";

const FIXTURE = `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Fixture - Autonogrammer</title>
<style>
  body { margin: 0; font: 16px/1.4 sans-serif; background: #fff; color: #111; }
  .skip { position: absolute; left: -999px; top: 0; }
  .skip:focus { left: 0; background: #fff; outline: 3px solid #000; padding: 8px; }
  a:focus-visible, button:focus-visible, input:focus-visible { outline: 3px solid #0a4aa8; outline-offset: 2px; }
  #bad:focus-visible { outline: none; }
  .tiny { width: 16px; height: 16px; padding: 0; margin: 0 2px; border: 1px solid #111; background: #fff; }
  .wide { width: 600px; height: 20px; background: #ddd; }
  .clip { width: 50px; overflow: hidden; white-space: nowrap; }
  .spin { transition: opacity 1s; }
  .plain, #noname { border: 0; background: none; padding: 8px 12px; }
  .sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); }
</style></head>
<body>
<a class="skip autonogrammer-skip-link" href="#main">Skip to main content</a>
<header><a href="/">Fixture</a></header>
<nav aria-label="Agents"><button type="button" id="named">Named button</button></nav>
<main id="main" tabindex="-1">
  <h1 class="sr-only">Fixture</h1>
  <button type="button" id="noname"><svg width="10" height="10" aria-hidden="true"><path d="M0 0h10v10H0z"/></svg></button>
  <button type="button" id="bad">Focus removed</button>
  <p><button type="button" class="tiny" aria-label="One"></button><button type="button" class="tiny" aria-label="Two"></button></p>
  <input type="text" id="unlabelled" style="border: 1px solid #111">
  <button type="button" class="plain" id="plain">Text only</button>
  <div class="wide">wide</div>
  <div class="clip" id="clip">A long label that does not fit in fifty pixels</div>
  <div class="spin" id="spin">fades</div>
</main>
<footer><a href="/about">About</a></footer>
</body></html>`;

function findChrome() {
  const candidates = [process.env.CHROME_BIN, "/usr/bin/chromium", "/usr/bin/chromium-browser", "/usr/bin/google-chrome", "/usr/bin/chrome"].filter(Boolean);
  const playwright = join(homedir(), ".cache", "ms-playwright");
  if (existsSync(playwright)) {
    for (const entry of readdirSync(playwright)) candidates.push(join(playwright, entry, "chrome-linux64", "chrome"));
  }
  return candidates.find((candidate) => existsSync(candidate)) ?? null;
}

const chrome = findChrome();
if (!chrome) {
  process.stdout.write("SKIP no Chrome or Chromium found (set CHROME_BIN)\n");
  process.exit(0);
}

const server = createServer((request, response) => {
  response.writeHead(200, { "content-type": "text/html; charset=utf-8" });
  response.end(FIXTURE);
});
await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
const url = `http://127.0.0.1:${server.address().port}/`;
const profile = mkdtempSync(join(tmpdir(), "a11y-fixture-"));
const child = spawn(chrome, ["--headless=new", "--no-sandbox", "--disable-dev-shm-usage", "--remote-debugging-port=0", `--user-data-dir=${profile}`, "about:blank"], { stdio: "ignore" });

let failed = false;
async function test(name, fn) {
  try {
    await fn();
    process.stdout.write(`PASS ${name}\n`);
  } catch (error) {
    failed = true;
    process.stdout.write(`FAIL ${name}\n  ${String(error?.stack ?? error).split("\n").join("\n  ")}\n`);
  }
}

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
let page = null;
try {
  const portFile = join(profile, "DevToolsActivePort");
  for (let i = 0; i < 120 && !existsSync(portFile); i += 1) await sleep(250);
  const cdpPort = readFileSync(portFile, "utf8").split("\n")[0];
  page = await Page.open({ cdpPort, width: 1280, height: 800 });
  const loaded = await page.navigate(url);
  assert.equal(loaded.status, 200);

  await test("Tab walk finds the skip link first, the unnamed button and the removed focus indicator", async () => {
    await page.evalFn(() => document.querySelector(".autonogrammer-skip-link").focus());
    const { stops, ended } = await walk(page, "forward", await page.lib("probeStop"));
    assert.equal(ended, true);
    assert.ok(stops[0].description.includes("autonogrammer-skip-link"));
    const findings = analyzeStops(stops, { state: "fixture", viewport: "1280x800" });
    const got = findings.map((f) => `${f.id}:${f.target?.split(" ")[0]}`);
    assert.ok(got.includes("stop-without-name:button#noname"), got.join(", "));
    assert.ok(got.includes("focus-not-visible:button#bad"), got.join(", "));
    assert.ok(!got.some((id) => id.startsWith("focus-not-visible:button#named")), "the named button shows its outline");
    assert.ok(!got.some((id) => id.includes("focus-obscured")), got.join(", "));
    assert.ok(stops.some((s) => s.landmark === "footer"), "the footer is reachable");
  });

  await test("targets: the two 16 px buttons side by side fail, the 24 px ones do not", async () => {
    const findings = analyzeTargets(await page.lib("collectTargets"), { state: "fixture", viewport: "1280x800" });
    assert.deepEqual(findings.map((f) => f.detail.neighbour.split(" ")[0]).sort(), ["button.tiny", "button.tiny"]);
  });

  await test("route state: one h1, title, landmarks", async () => {
    const snap = await page.lib("probeRouteState");
    assert.equal(snap.title, "Fixture - Autonogrammer");
    assert.equal(snap.headings.length, 1);
    assert.deepEqual(snap.landmarks.map((l) => l.role).sort(), ["banner", "contentinfo", "main", "navigation"]);
  });

  await test("reflow at 320 px reports the 600 px box and the page scroll", async () => {
    await page.setViewport(320, 568);
    await page.settle();
    const findings = analyzeReflow(await page.lib("collectReflow"), { state: "fixture", viewport: "320x568" });
    const got = findings.map((f) => f.id);
    assert.ok(got.includes("page-scrolls-sideways"), got.join(", "));
    assert.ok(findings.some((f) => f.id === "element-wider-than-viewport" && f.target.includes("wide")), got.join(", "));
    await page.setViewport(1280, 800);
  });

  await test("text spacing clips the fixed-width label", async () => {
    const measure = async () => ({ clipping: await page.lib("collectTextClipping"), boxes: await page.lib("collectTextBoxes") });
    const before = await measure();
    assert.ok(before.clipping.some((c) => c.description.includes("#clip")), "the label is already truncated");
    const findings = analyzeTextSpacing(before, before, { state: "fixture", viewport: "1280x800" });
    assert.ok(findings.some((f) => f.id === "text-clipped" && f.severity === "warning"));
  });

  await test("forced colours and reduced motion are emulated and measured", async () => {
    await page.setMedia([{ name: "forced-colors", value: "active" }]);
    await page.settle();
    const controls = await page.lib("collectControlAffordances");
    assert.equal(controls.forcedColors, true);
    const findings = analyzeAffordances(controls, "forced-colors", { state: "fixture", viewport: "1280x800" });
    assert.ok(findings.some((f) => f.id === "icon-button-without-boundary" || f.id === "button-without-affordance"), JSON.stringify(findings.map((f) => f.id)));
    await page.setMedia([{ name: "prefers-reduced-motion", value: "reduce" }]);
    await page.settle();
    const motion = await page.lib("collectMotion");
    assert.equal(motion.reducedMotion, true);
    assert.ok(analyzeMotion(motion, { state: "fixture", viewport: "1280x800" }).some((f) => f.id === "motion-not-reduced"), "the one-second transition is still there");
    await page.setMedia([]);
  });

  await test("the pinned axe-core runs in the page when its file is given", async () => {
    const axe = process.env.AXE_JS;
    if (!axe) return;
    await page.ensureAxe(axe);
    const version = await page.evalValue("axe.version");
    assert.equal(version, "4.14.0");
  });
} finally {
  if (page) await page.close();
  child.kill();
  server.close();
  rmSync(profile, { recursive: true, force: true });
}
process.exit(failed ? 1 : 0);
