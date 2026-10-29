#!/usr/bin/env node
// Hosted end-to-end check of an Autonogrammer API over HTTP, driven by the scripted fake model (see README.md).
//
//   AUTONOGRAMMER_E2E_TOKEN=<owner token> node scripts/hosted/e2e-api.mjs --api http://127.0.0.1:5187
//
// Steps (each can be skipped with --skip <name>[,<name>...]):
//   auth-config      GET /auth/config: anonymous, no-store, only {mode, authority, audience}
//   unauthenticated  a request with no credential is 401                      (skipped in mode "development")
//   bad-token        a wrong bearer token is 401 with a ProblemDetails body   (skipped in mode "development")
//   text-turn        create a session, stream a prompt: ids, text deltas, completed, session settles
//   tool-turn        "@tool" prompt: the permission request is decided through the tool-approval endpoints
//   resume           second prompt, forced disconnect, resume with Last-Event-ID: no gap, no duplicate, log matches
//   rate-limit       hammer POST /sessions until 429 with Retry-After          (last: it drains the creation bucket)
//
// The routes, payloads and status codes are the ones in services/Autonogrammer.Api/Endpoints/*.cs. The token is read
// from AUTONOGRAMMER_E2E_TOKEN (preferred) or --token, is sent only as an Authorization header, and is redacted from
// every message this script prints. Progress goes to stderr; a JSON report goes to stdout (and to --out when given).
// Exit status: 0 all steps passed, 1 a step failed, 2 bad usage.

import { randomBytes } from "node:crypto";
import { writeFileSync } from "node:fs";
import { parseSse } from "./sse.mjs";

const STEP_NAMES = ["auth-config", "unauthenticated", "bad-token", "text-turn", "tool-turn", "resume", "rate-limit"];
const MODES = ["owner-token", "oidc", "development"];
const APPROVAL_EXPECTATIONS = ["auto", "approved", "denied", "policy-denied", "none"];

class Usage extends Error {}

function parseArgs(argv) {
  const options = {
    api: process.env.AUTONOGRAMMER_API_URL ?? "",
    token: process.env.AUTONOGRAMMER_E2E_TOKEN ?? "",
    skip: new Set(),
    requestTimeoutMs: 30_000,
    turnTimeoutMs: 120_000,
    toolName: "bash",
    toolArgs: '{"command":"echo autonogrammer-e2e"}',
    approvalExpect: "auto",
    burst: 200,
    out: "",
    allowInsecure: false,
    lenient401: false,
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
    if (arg === "--api") options.api = value();
    else if (arg === "--token") options.token = value();
    else if (arg === "--skip") {
      for (const name of value().split(",").filter(Boolean)) {
        if (!STEP_NAMES.includes(name)) throw new Usage(`--skip: unknown step ${name} (steps: ${STEP_NAMES.join(", ")})`);
        options.skip.add(name);
      }
    } else if (arg === "--request-timeout-ms") options.requestTimeoutMs = integer();
    else if (arg === "--turn-timeout-ms") options.turnTimeoutMs = integer();
    else if (arg === "--tool-name") options.toolName = value();
    else if (arg === "--tool-args") options.toolArgs = value();
    else if (arg === "--approval-expect") {
      options.approvalExpect = value();
      if (!APPROVAL_EXPECTATIONS.includes(options.approvalExpect)) throw new Usage(`--approval-expect must be one of ${APPROVAL_EXPECTATIONS.join(", ")}`);
    } else if (arg === "--rate-limit-burst") options.burst = integer();
    else if (arg === "--out") options.out = value();
    else if (arg === "--allow-insecure") options.allowInsecure = true;
    else if (arg === "--lenient-401-body") options.lenient401 = true;
    else if (arg === "--help" || arg === "-h") throw new Usage("help");
    else throw new Usage(`unknown argument ${arg}`);
  }
  if (!options.api) throw new Usage("the API base URL is required: --api <url> or AUTONOGRAMMER_API_URL");
  let url;
  try {
    url = new URL(options.api);
  } catch {
    throw new Usage(`--api is not a URL: ${options.api}`);
  }
  if (!["http:", "https:"].includes(url.protocol)) throw new Usage("--api must be an http(s) URL");
  const loopback = ["127.0.0.1", "localhost", "[::1]"].includes(url.hostname);
  if (url.protocol === "http:" && !loopback && !options.allowInsecure) {
    throw new Usage("refusing to send a credential over plain http to a non-loopback host; use https or --allow-insecure");
  }
  options.api = url.origin + url.pathname.replace(/\/+$/, "");
  return options;
}

const HELP = `usage: node scripts/hosted/e2e-api.mjs --api <url> [--token <t>] [--skip steps] [--approval-expect ${APPROVAL_EXPECTATIONS.join("|")}]
       [--tool-name bash] [--tool-args '{"command":"..."}'] [--rate-limit-burst 200] [--turn-timeout-ms 120000]
       [--request-timeout-ms 30000] [--out report.json] [--allow-insecure] [--lenient-401-body]
environment: AUTONOGRAMMER_API_URL, AUTONOGRAMMER_E2E_TOKEN
steps: ${STEP_NAMES.join(", ")}`;

let options;
try {
  options = parseArgs(process.argv.slice(2));
} catch (error) {
  if (!(error instanceof Usage)) throw error;
  process.stderr.write(error.message === "help" ? `${HELP}\n` : `e2e-api: ${error.message}\n${HELP}\n`);
  process.exit(error.message === "help" ? 0 : 2);
}

const badToken = randomBytes(32).toString("base64url");
const secrets = [options.token, badToken].filter((secret) => secret.length >= 8);
const redact = (text) => secrets.reduce((acc, secret) => acc.split(secret).join("[redacted]"), String(text));
const note = (message) => process.stderr.write(`${new Date().toISOString()} ${redact(message)}\n`);
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

class Failure extends Error {}
function check(condition, message) {
  if (!condition) throw new Failure(message);
}

// ---- HTTP ---------------------------------------------------------------------------------------------------

async function call(method, path, { token = options.token, headers = {}, body, signal } = {}) {
  const requestHeaders = { ...headers };
  if (token) requestHeaders.authorization = `Bearer ${token}`;
  let payload;
  if (body !== undefined) {
    requestHeaders["content-type"] = "application/json";
    payload = JSON.stringify(body);
  }
  try {
    return await fetch(options.api + path, { method, headers: requestHeaders, body: payload, redirect: "manual", signal: signal ?? AbortSignal.timeout(options.requestTimeoutMs) });
  } catch (error) {
    if (error?.name === "AbortError" || error?.name === "TimeoutError") throw error;
    throw new Failure(`${method} ${path} failed: ${redact(error?.cause?.code ?? error?.message ?? error)}`);
  }
}

async function readJson(response) {
  const text = await response.text();
  try {
    return { text, json: text ? JSON.parse(text) : null };
  } catch {
    return { text, json: null };
  }
}

async function expectStatus(response, status, what) {
  if (response.status === status) return readJson(response);
  const { text } = await readJson(response);
  throw new Failure(`${what}: expected HTTP ${status}, got ${response.status}${text ? `: ${redact(text).slice(0, 300)}` : ""}`);
}

const statusIs = (json, ...names) => typeof json?.status === "string" && names.some((name) => json.status.toLowerCase() === name.toLowerCase());

// ---- session helpers ----------------------------------------------------------------------------------------

async function createSession() {
  const response = await call("POST", "/sessions", { body: {} });
  const { json } = await expectStatus(response, 201, "POST /sessions");
  check(typeof json?.id === "string" && json.id.length > 0, "POST /sessions: the body has no session id");
  check((response.headers.get("location") ?? "").endsWith(`/sessions/${json.id}`), "POST /sessions: Location does not point at the session");
  return json.id;
}

async function getSession(id) {
  const { json } = await expectStatus(await call("GET", `/sessions/${id}`), 200, `GET /sessions/${id}`);
  return json;
}

async function waitForStatus(id, wanted, ms = 15_000) {
  const deadline = Date.now() + ms;
  let last = null;
  for (;;) {
    last = await getSession(id);
    if (statusIs(last, ...wanted)) return last;
    if (Date.now() > deadline) throw new Failure(`session ${id} did not reach ${wanted.join("/")} within ${ms} ms (status ${last?.status})`);
    await sleep(100);
  }
}

/**
 * Reads the session event stream until a terminal event, the server closing it, or onEvent returning "stop".
 * Validates the framing of every event: a numeric id that grows, an event name equal to the payload kind.
 */
async function readEvents(id, { lastEventId = 0, onEvent, signal }) {
  const headers = { accept: "text/event-stream" };
  if (lastEventId > 0) headers["last-event-id"] = String(lastEventId);
  const controller = new AbortController();
  const link = signal ? AbortSignal.any([signal, controller.signal]) : controller.signal;
  const response = await call("GET", `/sessions/${id}/events`, { headers, signal: link });
  if (response.status !== 200) await expectStatus(response, 200, `GET /sessions/${id}/events`);
  check((response.headers.get("content-type") ?? "").startsWith("text/event-stream"), `GET /sessions/${id}/events: content type is ${response.headers.get("content-type")}`);

  const events = [];
  let previous = lastEventId;
  let stopped = false;
  let terminal = null;
  try {
    for await (const frame of parseSse(response.body)) {
      check(frame.id !== null && /^\d+$/.test(frame.id), `an event arrived without a numeric id (event ${frame.event})`);
      const seq = Number(frame.id);
      check(seq > previous, `event ids must grow: ${seq} followed ${previous}`);
      if (lastEventId > 0 && events.length === 0) check(seq === lastEventId + 1, `resume with Last-Event-ID ${lastEventId} must continue at ${lastEventId + 1}, got ${seq}`);
      if (events.length > 0) check(seq === previous + 1, `gap in the event ids: ${seq} followed ${previous}`);
      let payload;
      try {
        payload = JSON.parse(frame.data);
      } catch {
        throw new Failure(`event ${seq} (${frame.event}) carries data that is not JSON`);
      }
      check(payload?.kind === frame.event, `event ${seq}: SSE event name "${frame.event}" differs from payload kind "${payload?.kind}"`);
      previous = seq;
      const event = { seq, event: frame.event, payload };
      events.push(event);
      if (frame.event === "completed" || frame.event === "error") {
        terminal = frame.event;
        break;
      }
      if (onEvent && (await onEvent(event)) === "stop") {
        stopped = true;
        break;
      }
    }
  } catch (error) {
    if (error instanceof Failure) throw error;
    if (error?.name === "AbortError" || error?.name === "TimeoutError") throw new Failure("the event stream was aborted (turn timeout)");
    throw error;
  } finally {
    controller.abort();
  }
  return { events, stopped, terminal };
}

/** Starts reading the stream, submits the prompt, and returns what the stream delivered. */
async function runTurn(id, prompt, { lastEventId = 0, onEvent } = {}) {
  const controller = new AbortController();
  const deadline = AbortSignal.timeout(options.turnTimeoutMs);
  const reading = readEvents(id, { lastEventId, onEvent, signal: AbortSignal.any([controller.signal, deadline]) });
  reading.catch(() => {});
  try {
    const response = await call("POST", `/sessions/${id}/prompts`, { body: { prompt } });
    const { json } = await expectStatus(response, 202, `POST /sessions/${id}/prompts`);
    check(statusIs(json, "accepted") && json.sessionId === id, "the prompt acceptance body is not {sessionId, status: accepted}");
  } catch (error) {
    controller.abort();
    throw error;
  }
  try {
    return await reading;
  } catch (error) {
    if (deadline.aborted) throw new Failure(`the turn did not finish within ${options.turnTimeoutMs} ms`);
    throw error;
  }
}

const deltaText = (events) => events.filter((event) => event.event === "text_delta").map((event) => event.payload.delta).join("");
const kinds = (events) => [...new Set(events.map((event) => event.event))].join(",");

// ---- steps --------------------------------------------------------------------------------------------------

const report = { api: options.api, startedAt: new Date().toISOString(), mode: "unknown", steps: [] };
let session = null;
let lastSeq = 0;

async function step(name, fn) {
  if (options.skip.has(name)) {
    report.steps.push({ name, status: "skipped", detail: "skipped by --skip" });
    note(`SKIP ${name} (--skip)`);
    return true;
  }
  const started = Date.now();
  try {
    const result = await fn();
    if (result?.skipped) {
      report.steps.push({ name, status: "skipped", detail: result.skipped });
      note(`SKIP ${name}: ${result.skipped}`);
      return true;
    }
    report.steps.push({ name, status: "passed", ms: Date.now() - started, ...(result ?? {}) });
    note(`PASS ${name} (${Date.now() - started} ms)`);
    return true;
  } catch (error) {
    const message = redact(error instanceof Failure ? error.message : `${error?.name ?? "Error"}: ${error?.message ?? error}`);
    report.steps.push({ name, status: "failed", ms: Date.now() - started, detail: message });
    note(`FAIL ${name}: ${message}`);
    return false;
  }
}

await step("auth-config", async () => {
  const response = await call("GET", "/auth/config", { token: "" });
  const { json } = await expectStatus(response, 200, "GET /auth/config");
  check(/\bno-store\b/i.test(response.headers.get("cache-control") ?? ""), `GET /auth/config: Cache-Control is "${response.headers.get("cache-control")}", expected no-store`);
  check(json !== null && typeof json === "object" && !Array.isArray(json), "GET /auth/config: the body is not an object");
  const extra = Object.keys(json).filter((key) => !["mode", "authority", "audience"].includes(key));
  check(extra.length === 0, `GET /auth/config: unexpected members ${extra.join(", ")} (the contract is mode, authority, audience)`);
  check(MODES.includes(json.mode), `GET /auth/config: mode "${json.mode}" is not one of ${MODES.join(", ")}`);
  for (const key of ["authority", "audience"]) check(json[key] === null || typeof json[key] === "string", `GET /auth/config: ${key} must be a string or null`);
  report.mode = json.mode;
  return { mode: json.mode };
});

if (report.steps.some((entry) => entry.name === "auth-config" && entry.status === "failed" && /^GET \/auth\/config failed:/.test(entry.detail))) {
  note(`the API at ${options.api} is not reachable`);
  finish(1);
}

if (report.mode !== "development" && report.mode !== "unknown" && !options.token) {
  note("a credential is required by this deployment: set AUTONOGRAMMER_E2E_TOKEN");
  report.steps.push({ name: "credential", status: "failed", detail: "no token: set AUTONOGRAMMER_E2E_TOKEN (or --token)" });
  finish(2);
}

await step("unauthenticated", async () => {
  if (report.mode === "development") return { skipped: "mode is development: no credential is needed" };
  const response = await call("GET", "/sessions/e2e-probe", { token: "" });
  await expectStatus(response, 401, "an unauthenticated request");
  return { wwwAuthenticate: response.headers.get("www-authenticate") ?? null };
});

await step("bad-token", async () => {
  if (report.mode === "development") return { skipped: "mode is development: no credential is needed" };
  const response = await call("GET", "/sessions/e2e-probe", { token: badToken });
  const { text, json } = await readJson(response);
  check(response.status === 401, `a wrong token: expected HTTP 401, got ${response.status}`);
  check(!text.includes(badToken), "the 401 body echoes the presented token");
  if (!options.lenient401) {
    const type = response.headers.get("content-type") ?? "";
    check(/^application\/problem\+json/i.test(type), `the 401 content type is "${type}", expected application/problem+json (the shared ProblemDetails shape)`);
    check(json !== null && json.status === 401 && typeof json.title === "string" && json.title.length > 0, `the 401 body is not ProblemDetails with status 401 and a title: ${text.slice(0, 200)}`);
  }
  return { contentType: response.headers.get("content-type") ?? null };
});

const haveSession = await step("text-turn", async () => {
  session = await createSession();
  const fresh = await getSession(session);
  check(fresh.id === session && Array.isArray(fresh.events), "GET /sessions/{id}: the session body has no events array");
  const result = await runTurn(session, "@say one two three four five six seven eight");
  lastSeq = result.events.at(-1)?.seq ?? 0;
  check(result.terminal === "completed", `the turn ended with "${result.terminal ?? "no terminal event"}" instead of completed (events: ${kinds(result.events)})`);
  const text = deltaText(result.events);
  check(result.events.some((event) => event.event === "text_delta"), `no text_delta event arrived (events: ${kinds(result.events)})`);
  check(text.includes("one two three four five six seven eight"), `the assistant text is "${text.slice(0, 200)}", expected the scripted reply`);
  await waitForStatus(session, ["completed"]);
  return { sessionId: session, events: result.events.length, eventKinds: kinds(result.events), firstSeq: result.events[0]?.seq, lastSeq, userMessageEvent: result.events.some((event) => event.event === "user_message") };
});

let sessionReady = haveSession;
if (sessionReady && session === null) {
  sessionReady = await step("session", async () => {
    session = await createSession();
    return { sessionId: session };
  });
}

if (sessionReady) {
  await step("tool-turn", async () => {
    const outcome = { approval: null, policyDenied: false, toolCall: false, requestId: null };
    const onEvent = async (event) => {
      if (event.event === "tool_call" || event.event === "tool_call_update") outcome.toolCall = true;
      if (event.event === "diagnostic" && /Approval policy denied permission request/.test(String(event.payload.message))) outcome.policyDenied = true;
      if (event.event === "tool_approval_requested") {
        outcome.requestId = String(event.payload.requestId);
        outcome.approval = await decide(session, outcome.requestId);
      }
      return undefined;
    };
    const result = await runTurn(session, `@tool ${options.toolName} ${options.toolArgs}`, { lastEventId: lastSeq, onEvent });
    lastSeq = result.events.at(-1)?.seq ?? lastSeq;
    check(result.terminal === "completed", `the tool turn ended with "${result.terminal ?? "no terminal event"}" instead of completed (events: ${kinds(result.events)})`);

    const expectation = options.approvalExpect;
    const attempted = outcome.approval !== null || outcome.policyDenied;
    if (expectation === "auto") check(attempted, `omp asked for no permission for the scripted tool call (events: ${kinds(result.events)}; model reply: "${deltaText(result.events).slice(0, 120)}"); is tools.approvalMode "always-ask" in the agent config and is the tool "${options.toolName}" offered by omp? use --approval-expect none to accept it`);
    if (expectation === "approved") check(outcome.approval === "approved", `expected an approved request, got ${outcome.approval ?? (outcome.policyDenied ? "policy-denied" : "none")}`);
    if (expectation === "denied") check(outcome.approval === "denied-after-ceiling", `expected a denied request, got ${outcome.approval ?? (outcome.policyDenied ? "policy-denied" : "none")}`);
    if (expectation === "policy-denied") check(outcome.policyDenied, "expected the approval policy to deny the request without a decision, but no such diagnostic arrived");

    const text = deltaText(result.events);
    check(/done: \d+/.test(text), `the model's reply after the tool result is "${text.slice(0, 200)}", expected "done: <length>" (events: ${kinds(result.events)})`);
    await waitForStatus(session, ["completed"]);
    return {
      approval: outcome.approval ?? (outcome.policyDenied ? "policy-denied" : "none"),
      requestId: outcome.requestId,
      toolCallEvents: outcome.toolCall,
      reply: text.slice(0, 80),
      eventKinds: kinds(result.events),
    };
  });

  await step("resume", async () => {
    const prompt = "@slow 150\nresume after a forced disconnect with several words to stream";
    let first = null;
    const firstConnection = await runTurn(session, prompt, {
      lastEventId: lastSeq,
      onEvent: (event) => {
        if (event.event === "text_delta") {
          first = event.seq;
          return "stop";
        }
        return undefined;
      },
    });
    check(first !== null && firstConnection.stopped, `no text_delta arrived before the stream ended (events: ${kinds(firstConnection.events)})`);
    const lastSeen = firstConnection.events.at(-1).seq;

    // The first connection is now closed by the client. Resume from the last id it saw.
    const second = await readEvents(session, { lastEventId: lastSeen, signal: AbortSignal.timeout(options.turnTimeoutMs) });
    check(second.terminal === "completed", `the resumed stream ended with "${second.terminal ?? "no terminal event"}" instead of completed`);
    check(second.events.length > 0 && second.events[0].seq === lastSeen + 1, "the resumed stream did not continue right after the last id it had seen");

    const all = [...firstConnection.events, ...second.events];
    const text = deltaText(all);
    check(text.includes("resume after a forced disconnect with several words to stream"), `the stitched assistant text is "${text.slice(0, 200)}", expected the echo of the prompt`);

    const settled = await waitForStatus(session, ["completed"]);
    const log = new Map(settled.events.map((event) => [Number(event.seq), event.payload?.kind]));
    for (const event of all) {
      check(log.get(event.seq) === event.event, `event ${event.seq} (${event.event}) is not in the session log as that kind (log has ${log.get(event.seq)})`);
    }
    // A second reconnect from before the disconnect point replays the same ids again: resume is repeatable.
    const replay = await readEvents(session, { lastEventId: lastSeq, signal: AbortSignal.timeout(options.turnTimeoutMs) });
    check(replay.events.map((event) => event.seq).join() === all.map((event) => event.seq).join(), "replaying from the start of the turn did not return the same ids as the two connections together");
    lastSeq = all.at(-1).seq;
    return { firstConnectionEvents: firstConnection.events.length, resumedFrom: lastSeen, resumedEvents: second.events.length, sessionLogEvents: settled.events.length };
  });
} else {
  for (const name of ["tool-turn", "resume"]) {
    report.steps.push({ name, status: "skipped", detail: "no session: text-turn did not pass" });
    note(`SKIP ${name}: no session (text-turn did not pass)`);
  }
}

await step("rate-limit", async () => {
  let created = 0;
  for (let batch = 0; created < options.burst; batch++) {
    const size = Math.min(10, options.burst - created);
    const responses = await Promise.all(Array.from({ length: size }, () => call("POST", "/sessions", { body: {} })));
    for (const response of responses) {
      created += 1;
      const text = await response.text();
      if (response.status === 429) {
        const retryAfter = response.headers.get("retry-after");
        check(retryAfter !== null && /^\d+$/.test(retryAfter) && Number(retryAfter) > 0, `429 without a numeric Retry-After header (got ${retryAfter})`);
        await Promise.all(responses.map((other) => other.body?.cancel().catch(() => {})));
        return { requestsUntil429: created, retryAfterSeconds: Number(retryAfter) };
      }
      check(response.status === 201, `POST /sessions answered ${response.status} while hammering: ${redact(text).slice(0, 200)}`);
    }
  }
  throw new Failure(`no 429 after ${options.burst} session creations; the limit may be higher (Api:RateLimit:SessionsPerMinute) - raise --rate-limit-burst`);
});

// ---- decisions ----------------------------------------------------------------------------------------------

/** Decides a pending tool approval: allow first; when the ceiling forbids an allow (403) the request stays pending, so deny it. */
async function decide(id, requestId) {
  const listed = await expectStatus(await call("GET", `/sessions/${id}/tool-approvals`), 200, `GET /sessions/${id}/tool-approvals`);
  const pending = (listed.json?.approvals ?? []).find((approval) => approval.requestId === requestId);
  check(pending !== undefined, `the approval ${requestId} announced in the event stream is not listed as pending`);
  check(typeof pending.toolName === "string" && Array.isArray(pending.grantableTools), "a pending approval has no toolName / grantableTools");

  const path = `/sessions/${id}/tool-approvals/${encodeURIComponent(requestId)}`;
  const allow = await call("POST", path, { body: { decision: "allow" } });
  if (allow.status === 204) return "approved";
  const refusal = await readJson(allow);
  check(allow.status === 403, `allow: expected HTTP 204 or 403, got ${allow.status}: ${redact(refusal.text).slice(0, 200)}`);
  check(pending.grantableTools.length === 0 || !pending.grantableTools.includes(pending.toolName), "allow was refused (403) although the ceiling lists the tool");
  const deny = await call("POST", path, { body: { decision: "deny" } });
  await expectStatus(deny, 204, "deny after a refused allow");
  // A decided request is no longer pending.
  const again = await call("POST", path, { body: { decision: "deny" } });
  await expectStatus(again, 409, "deciding a request twice");
  return "denied-after-ceiling";
}

function finish(forcedCode) {
  report.finishedAt = new Date().toISOString();
  report.sessionId = session;
  const failed = report.steps.filter((entry) => entry.status === "failed");
  const code = forcedCode ?? (failed.length === 0 ? 0 : 1);
  report.result = code === 0 ? "passed" : "failed";
  const text = `${redact(JSON.stringify(report, null, 2))}\n`;
  if (options.out) writeFileSync(options.out, text);
  process.stdout.write(text);
  process.exit(code);
}

finish();
