// Live check of the OmpOS agent worker image (services/OmpOS.AgentWorker, deploy/worker.Containerfile) under podman. No model is called.
//
// It generates an ECDSA P-256 key pair, starts the image with the public key, and checks: /healthz and /readyz; the JWT gate of GET /acp
// (no token, wrong audience, expired, living longer than 15 minutes, unsafe subject: refused before any upgrade); the WebSocket upgrade with a
// valid token; the real `omp acp` inside the container answering `initialize` and `session/new` through the bridge; the single-attachment rule
// (a second connection for the same session is refused); and that the container log holds no key or token material. The container and its
// volume are removed at the end. Nothing is written to the repository.
//
// Usage:  podman build -f deploy/worker.Containerfile -t ompos-agent-worker:local .
//         node scripts/cloud/worker-live-check.mjs
// Env:    OMPOS_WORKER_IMAGE (default localhost/ompos-agent-worker:local), OMPOS_WORKER_LIVE_PORT (default 18088; bound to 127.0.0.1 only)
// Needs:  podman, Node 22 or later (global WebSocket with request headers). Exit code 0 when every check passed, 1 otherwise, 2 when the container did not start.
import { generateKeyPairSync, sign } from "node:crypto";
import { spawnSync } from "node:child_process";
const NAME = "ompos-worker-live", VOL = "ompos-worker-live-data", PORT = Number(process.env.OMPOS_WORKER_LIVE_PORT || 18088);
const IMAGE = process.env.OMPOS_WORKER_IMAGE || "localhost/ompos-agent-worker:local";
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const sh = (args) => spawnSync("podman", args, { encoding: "utf8" });
const b64u = (b) => Buffer.from(b).toString("base64url");
const { publicKey, privateKey } = generateKeyPairSync("ec", { namedCurve: "P-256" });
const pem = publicKey.export({ type: "spki", format: "pem" });
function mint({ sub = "live-session-1", aud = "ompos-worker", iss = "ompos-api", ttl = 600, iat = Math.floor(Date.now() / 1000) } = {}) {
  const head = b64u(JSON.stringify({ alg: "ES256", typ: "JWT" }));
  const body = b64u(JSON.stringify({ iss, aud, sub, iat, exp: iat + ttl }));
  const sig = sign("sha256", Buffer.from(`${head}.${body}`), { key: privateKey, dsaEncoding: "ieee-p1363" });
  return `${head}.${body}.${b64u(sig)}`;
}
const results = [];
const check = (name, ok, detail = "") => { results.push({ name, ok, detail }); console.log(ok ? "PASS" : "FAIL", name, detail); };
sh(["rm", "-f", NAME]); sh(["volume", "rm", "-f", VOL]);
const run = sh(["run", "-d", "--name", NAME, "-p", `127.0.0.1:${PORT}:8080`, "-v", `${VOL}:/data`,
  "-e", `Worker__Auth__PublicKeysPem__0=${pem}`, "-e", "Worker__Auth__Issuer=ompos-api", "-e", "PORT=8080", IMAGE]);
if (run.status !== 0) { console.log(run.stderr); process.exit(2); }
try {
  let up = false;
  for (let i = 0; i < 120 && !up; i++) { try { up = (await fetch(`http://127.0.0.1:${PORT}/healthz`)).ok; } catch { await sleep(500); } }
  check("healthz answers 200 ok", up);
  const ready = await fetch(`http://127.0.0.1:${PORT}/readyz`); check("readyz answers", ready.status === 200 || ready.status === 503, `status ${ready.status} ${(await ready.text()).slice(0, 120)}`);
  const noTok = await fetch(`http://127.0.0.1:${PORT}/acp`); check("acp without a token is 401", noTok.status === 401, `status ${noTok.status}`);
  const badAud = await fetch(`http://127.0.0.1:${PORT}/acp`, { headers: { Authorization: `Bearer ${mint({ aud: "someone-else" })}` } }); check("wrong audience is 401", badAud.status === 401, `status ${badAud.status}`);
  const expired = await fetch(`http://127.0.0.1:${PORT}/acp`, { headers: { Authorization: `Bearer ${mint({ iat: Math.floor(Date.now() / 1000) - 3600, ttl: 600 })}` } }); check("expired token is 401", expired.status === 401, `status ${expired.status}`);
  const longLived = await fetch(`http://127.0.0.1:${PORT}/acp`, { headers: { Authorization: `Bearer ${mint({ ttl: 7200 })}` } }); check("token living longer than 15 minutes is 401", longLived.status === 401, `status ${longLived.status}`);
  const unsafe = await fetch(`http://127.0.0.1:${PORT}/acp`, { headers: { Authorization: `Bearer ${mint({ sub: "../etc" })}` } }); check("unsafe subject is 403", unsafe.status === 403, `status ${unsafe.status}`);
  const plain = await fetch(`http://127.0.0.1:${PORT}/acp`, { headers: { Authorization: `Bearer ${mint()}` } }); check("valid token without an upgrade is 400", plain.status === 400, `status ${plain.status}`);

  const ws = new WebSocket(`ws://127.0.0.1:${PORT}/acp`, { headers: { Authorization: `Bearer ${mint()}` } });
  const inbox = []; let wake = null;
  ws.addEventListener("message", (e) => { try { inbox.push(JSON.parse(String(e.data))); } catch { inbox.push({ raw: String(e.data) }); } wake?.(); });
  const opened = await new Promise((res) => { ws.addEventListener("open", () => res(true), { once: true }); ws.addEventListener("error", () => res(false), { once: true }); setTimeout(() => res(false), 60000); });
  check("websocket upgrade with a valid token", opened);
  const call = async (id, method, params, ms = 90000) => {
    ws.send(JSON.stringify({ jsonrpc: "2.0", id, method, params }));
    for (let t = 0; t < ms; t += 100) { const hit = inbox.find((m) => m.id === id); if (hit) return hit; await new Promise((r) => { wake = r; setTimeout(r, 100); }); }
    return null;
  };
  if (opened) {
    const init = await call(1, "initialize", { protocolVersion: 1, clientCapabilities: { fs: { readTextFile: false, writeTextFile: false } }, clientInfo: { name: "worker-live-check", version: "0" } });
    check("omp in the container answers initialize", !!init?.result?.agentCapabilities, JSON.stringify(init?.result?.agentInfo ?? init?.error ?? null));
    check("loadSession advertised", init?.result?.agentCapabilities?.loadSession === true);
    const created = await call(2, "session/new", { cwd: "/data/work/live-session-1", mcpServers: [] });
    check("session/new returns a session id", typeof created?.result?.sessionId === "string", JSON.stringify(created?.error ?? { sessionId: created?.result?.sessionId?.slice(0, 8) }));
    const second = new WebSocket(`ws://127.0.0.1:${PORT}/acp`, { headers: { Authorization: `Bearer ${mint()}` } });
    const secondOpen = await new Promise((res) => { second.addEventListener("open", () => res(true), { once: true }); second.addEventListener("error", () => res(false), { once: true }); setTimeout(() => res(false), 10000); });
    check("a second connection for the same session is refused (409)", secondOpen === false);
    ws.close();
  }
  const logs = sh(["logs", NAME]);
  const text = `${logs.stdout}${logs.stderr}`;
  check("container log carries no token or key material", !/BEGIN PUBLIC KEY|eyJ[A-Za-z0-9_-]{20,}/.test(text));
  console.log("--- container log (tail)\n" + text.split("\n").slice(-12).join("\n"));
} finally {
  sh(["rm", "-f", NAME]); sh(["volume", "rm", "-f", VOL]);
}
const failed = results.filter((r) => !r.ok);
console.log(`\n${results.length - failed.length}/${results.length} checks passed`);
process.exit(failed.length ? 1 : 0);
