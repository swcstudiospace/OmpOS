// WEB-01 smoke, artifact half (71-06, roadmap criterion 6). Frames the hostile Html and Mermaid fixtures of the Cowork artifact corpus in the
// sandbox that the web head uses, in the owned headless Chrome that scripts/web-smoke/run-smoke.sh starts, and checks three things:
//
//   outbound    no request leaves the browser for an origin other than this page's loopback server (data: and blob: are local)
//   navigation  no frame moves away from the document it was given (a frame navigation after its first load)
//   parent      no frame reaches its parent: contentDocument stays null under the sandbox, which has no allow-same-origin
//
// The fixtures are served under the Demo content-security-policy of scripts/vercel/build-demo.sh (line 23), so a hostile inline script cannot
// run here either. The Mermaid fixtures use the web's srcdoc form: the page's policy is inherited and the bundle loads from lib/mermaid beside
// the page, exactly as ArtifactBody renders them.
//
// Stdout: one JSON summary. Stderr: progress. Writes OUT_DIR/web-smoke-artifact-<COMMIT>-<RUN_DATE>.json. Exit 0 only when every assertion holds.
// Env: CDP_PORT, OUT_DIR, COMMIT, RUN_DATE. Optional: ARTIFACT_FIXTURES (default tests/Autonogrammer.Cowork.Artifacts.Tests/Fixtures/hostile),
// MERMAID_DIR (default src/Autonogrammer.UI/wwwroot/lib/mermaid).

import { createServer } from "node:http";
import { readdirSync, readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, extname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

function need(name) {
  const value = process.env[name];
  if (!value) throw new Error(`missing environment variable ${name}`);
  return value;
}

const here = dirname(fileURLToPath(import.meta.url));
const repo = resolve(here, "..", "..");
const CDP_PORT = need("CDP_PORT");
const OUT_DIR = need("OUT_DIR");
const COMMIT = need("COMMIT");
const RUN_DATE = need("RUN_DATE");
const FIXTURES = resolve(process.env.ARTIFACT_FIXTURES || join(repo, "tests", "Autonogrammer.Cowork.Artifacts.Tests", "Fixtures", "hostile"));
const MERMAID_DIR = resolve(process.env.MERMAID_DIR || join(repo, "src", "Autonogrammer.UI", "wwwroot", "lib", "mermaid"));

// The Demo policy of scripts/vercel/build-demo.sh, applied to every document this page serves.
const DEMO_CSP = "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; connect-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";

const CONTENT_TYPES = { ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".txt": "text/plain; charset=utf-8", ".mmd": "text/plain; charset=utf-8", ".md": "text/plain; charset=utf-8" };

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const note = (message) => process.stderr.write(`${new Date().toISOString()} ${message}\n`);

// ---- loopback server ------------------------------------------------------------------------------------------------

function listFiles(dir) {
  return readdirSync(dir, { withFileTypes: true }).filter((entry) => entry.isFile()).map((entry) => entry.name).sort();
}

const hostileHtml = listFiles(join(FIXTURES, "html")).filter((name) => name.endsWith(".html"));
const hostileMermaid = listFiles(join(FIXTURES, "mermaid")).filter((name) => name.endsWith(".mmd"));

const server = createServer((request, response) => {
  const url = new URL(request.url || "/", "http://127.0.0.1");
  const headers = { "Content-Security-Policy": DEMO_CSP, "X-Content-Type-Options": "nosniff", "Referrer-Policy": "no-referrer" };
  if (url.pathname === "/") {
    response.writeHead(200, { ...headers, "Content-Type": "text/html; charset=utf-8" });
    response.end(hostPage());
    return;
  }
  const fixture = url.pathname.match(/^\/fixtures\/(html|mermaid)\/([A-Za-z0-9._-]+)$/);
  if (fixture) {
    const path = join(FIXTURES, fixture[1], fixture[2]);
    if (!(fixture[1] === "html" ? hostileHtml : hostileMermaid).includes(fixture[2])) return notFound(response);
    response.writeHead(200, { ...headers, "Content-Type": CONTENT_TYPES[extname(fixture[2])] || "text/plain; charset=utf-8" });
    response.end(readFileSync(path));
    return;
  }
  const bundle = url.pathname.match(/^\/lib\/mermaid\/([A-Za-z0-9._-]+\.js)$/);
  if (bundle) {
    const path = join(MERMAID_DIR, bundle[1]);
    response.writeHead(200, { ...headers, "Content-Type": CONTENT_TYPES[".js"] });
    response.end(readFileSync(path));
    return;
  }
  notFound(response);
});

function notFound(response) {
  response.writeHead(404, { "Content-Type": "text/plain; charset=utf-8", "Content-Security-Policy": DEMO_CSP });
  response.end("not found");
}

// The host page holds no script: the driver below creates every frame through CDP, which the page policy does not restrict.
function hostPage() {
  return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Artifact smoke</title><base href=\"/\"></head><body><main id=\"stage\"></main></body></html>";
}

await new Promise((resolveListen) => server.listen(0, "127.0.0.1", resolveListen));
const origin = `http://127.0.0.1:${server.address().port}`;

// ---- CDP transport --------------------------------------------------------------------------------------------------

const meta = await (await fetch(`http://127.0.0.1:${CDP_PORT}/json/version`)).json();
const ws = new WebSocket(meta.webSocketDebuggerUrl);
await new Promise((resolveOpen, rejectOpen) => {
  ws.addEventListener("open", resolveOpen, { once: true });
  ws.addEventListener("error", () => rejectOpen(new Error("CDP WebSocket error")), { once: true });
});

let nextId = 1;
const pending = new Map();
const events = [];

ws.addEventListener("message", (event) => {
  const msg = JSON.parse(event.data);
  if (msg.id !== undefined && pending.has(msg.id)) {
    const { resolve: done, reject } = pending.get(msg.id);
    pending.delete(msg.id);
    if (msg.error) reject(new Error(msg.error.message));
    else done(msg.result);
    return;
  }
  if (msg.method) events.push(msg);
});

function send(method, params = {}, sessionId) {
  const id = nextId++;
  return new Promise((resolveSend, rejectSend) => {
    pending.set(id, { resolve: resolveSend, reject: rejectSend });
    ws.send(JSON.stringify(sessionId ? { id, method, params, sessionId } : { id, method, params }));
  });
}

const { targetId } = await send("Target.createTarget", { url: "about:blank" });
const { sessionId } = await send("Target.attachToTarget", { targetId, flatten: true });
const cdp = (method, params) => send(method, params, sessionId);

await cdp("Page.enable");
await cdp("Runtime.enable");
await cdp("Network.enable");

async function evalValue(expression) {
  const result = await cdp("Runtime.evaluate", { expression, returnByValue: true, awaitPromise: true });
  if (result.exceptionDetails) throw new Error(`page evaluation failed: ${result.exceptionDetails.text}`);
  return result.result.value;
}

// ---- the run --------------------------------------------------------------------------------------------------------

const assertions = {};
const failures = [];
const frames = [];
const outbound = [];
const navigations = [];
const firstUrl = new Map();
const result = { commit: COMMIT, runDate: RUN_DATE, origin, startedAt: new Date().toISOString(), frames: [] };

function hold(name) {
  assertions[name] = true;
}

function fail(name, detail) {
  assertions[name] = false;
  failures.push(`${name}: ${detail}`);
}

/** Drains the network and frame events seen since the last call. */
function drainEvents() {
  for (const msg of events.splice(0)) {
    if (msg.sessionId !== sessionId) continue;
    if (msg.method === "Network.requestWillBeSent") {
      const requestUrl = msg.params.request.url;
      if (!requestUrl.startsWith(origin) && !requestUrl.startsWith("data:") && !requestUrl.startsWith("blob:") && !requestUrl.startsWith("about:")) {
        outbound.push(requestUrl.slice(0, 200));
      }
    }
    if (msg.method === "Page.frameNavigated") {
      const frame = msg.params.frame;
      if (!frame.parentId) continue;
      const previous = firstUrl.get(frame.id);
      if (previous === undefined || previous === "about:blank") {
        firstUrl.set(frame.id, frame.url);
      } else if (previous !== frame.url) {
        navigations.push(`${firstUrl.get(frame.id).slice(0, 120)} -> ${frame.url.slice(0, 120)}`);
        firstUrl.set(frame.id, frame.url);
      }
    }
  }
}

try {
  note("loading the host page");
  await cdp("Page.navigate", { url: `${origin}/` });
  await sleep(500);

  for (const name of hostileHtml) {
    note(`framing html fixture ${name}`);
    const src = `/fixtures/html/${name}`;
    await evalValue(`(() => {
      const frame = document.createElement('iframe');
      frame.setAttribute('sandbox', 'allow-scripts');
      frame.setAttribute('referrerpolicy', 'no-referrer');
      frame.setAttribute('allow', '');
      frame.setAttribute('title', ${JSON.stringify(name)});
      frame.src = ${JSON.stringify(src)};
      document.getElementById('stage').appendChild(frame);
      return true;
    })()`);
    await sleep(1500);
    drainEvents();
    frames.push({ kind: "html", fixture: name });
  }

  for (const name of hostileMermaid) {
    note(`framing mermaid fixture ${name}`);
    const text = readFileSync(join(FIXTURES, "mermaid", name), "utf8");
    const srcdoc = `<pre class="mermaid">${text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")}</pre><script src="lib/mermaid/mermaid.min.js"></script><script src="lib/mermaid/artifact-mermaid.js"></script>`;
    await evalValue(`(() => {
      const frame = document.createElement('iframe');
      frame.setAttribute('sandbox', 'allow-scripts');
      frame.setAttribute('referrerpolicy', 'no-referrer');
      frame.setAttribute('allow', '');
      frame.setAttribute('title', ${JSON.stringify(name)});
      frame.srcdoc = ${JSON.stringify(srcdoc)};
      document.getElementById('stage').appendChild(frame);
      return true;
    })()`);
    await sleep(2500);
    drainEvents();
    frames.push({ kind: "mermaid", fixture: name });
  }

  // Parent access: under the sandbox the frame has no same-origin document, so its contentDocument must be null for every frame.
  const parentAccess = await evalValue(`[...document.querySelectorAll('#stage iframe')].filter((frame) => {
    try { return frame.contentDocument !== null; } catch (error) { return false; }
  }).length`);

  drainEvents();
  await sleep(500);
  drainEvents();

  result.frames = frames;
  result.counts = { frames: frames.length, outboundRequests: outbound.length, frameNavigations: navigations.length, parentAccesses: parentAccess };
  result.outbound = outbound;
  result.navigations = navigations;

  if (frames.length === hostileHtml.length + hostileMermaid.length && frames.length > 0) hold("artifactFixturesFramed");
  else fail("artifactFixturesFramed", `framed ${frames.length} of ${hostileHtml.length + hostileMermaid.length}`);

  if (outbound.length === 0) hold("artifactNoOutboundRequest");
  else fail("artifactNoOutboundRequest", outbound.slice(0, 5).join(" | "));

  if (navigations.length === 0) hold("artifactNoFrameNavigation");
  else fail("artifactNoFrameNavigation", navigations.slice(0, 5).join(" | "));

  if (parentAccess === 0) hold("artifactNoParentAccess");
  else fail("artifactNoParentAccess", `${parentAccess} frame(s) exposed a document to the page`);
} catch (error) {
  result.error = error instanceof Error ? error.message : String(error);
  fail("artifactRun", result.error);
} finally {
  result.finishedAt = new Date().toISOString();
  result.assertions = assertions;
  result.failures = failures;
  result.passed = Object.keys(assertions).length > 0 && Object.values(assertions).every((value) => value === true) && !result.error;
  mkdirSync(OUT_DIR, { recursive: true });
  writeFileSync(join(OUT_DIR, `web-smoke-artifact-${COMMIT}-${RUN_DATE}.json`), `${JSON.stringify(result, null, 2)}\n`);
  ws.close();
  server.close();
  process.stdout.write(`${JSON.stringify({ passed: result.passed, assertions, failures, error: result.error ?? null }, null, 2)}\n`);
  process.exit(result.passed ? 0 : 1);
}
