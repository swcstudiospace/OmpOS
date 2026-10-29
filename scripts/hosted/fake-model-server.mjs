#!/usr/bin/env node
// Scripted OpenAI-compatible model server for the hosted end-to-end harness.
//
// It implements the subset of the Chat Completions API that `omp` uses for a custom provider declared in
// models.yml with `api: openai-completions` (see scripts/hosted/README.md):
//   GET  /v1/models              the model list (one model)
//   POST /v1/chat/completions    SSE streaming ("stream": true) and the non-streaming fallback
// Everything else answers a JSON error. No credential is checked and nothing leaves the host: it listens on
// 127.0.0.1 unless --host says otherwise, and it makes no outbound request.
//
// Scenarios are chosen by the text of the LAST user message. A line of that message that starts with one of
// these directives selects the scenario (the first non-@slow directive wins; @slow only adds a delay):
//   @say <text>          reply with <text>
//   @tool <name> <json>  reply with a call to tool <name> with the JSON arguments. When the next request ends in
//                        the tool result, reply "done: <length of the result text>". If the request does not offer
//                        a tool of that name, reply with a text that says so (a title or compaction request has
//                        no tools, and must not break).
//   @slow <ms>           wait <ms> before every streamed chunk (0..60000); the reply is the default echo unless
//                        another directive is present
//   @error <status>      answer HTTP <status> (400..599) with an OpenAI-style error body
//   (none)               reply "You said: <the user message>"
//
// Determinism: the reply is a pure function of the request body. Ids are a hash of the body, `created` is a
// constant, usage counts are derived from lengths, and the only timer is @slow.

import { createHash } from "node:crypto";
import { createServer } from "node:http";
import { pathToFileURL } from "node:url";

export const DEFAULT_MODEL_ID = "fake-model";
const CREATED = 1_700_000_000;
const MAX_BODY_BYTES = 8 * 1024 * 1024;
const MAX_RECORDED_REQUESTS = 1000;
const MAX_SLOW_MS = 60_000;
const DIRECTIVE = /^\s*@(say|tool|slow|error)(?:[ \t]+(.*?))?\s*$/;
const TOOL_NAME = /^[A-Za-z0-9_.-]{1,64}$/;

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/**
 * Reads the text of one chat message whose content is a string or an array of content parts.
 * @param {unknown} message A chat message.
 * @returns {string} The concatenated text, or "" when the message has none.
 */
export function messageText(message) {
  const content = message && typeof message === "object" ? message.content : undefined;
  if (typeof content === "string") return content;
  if (Array.isArray(content)) {
    return content
      .map((part) => (typeof part === "string" ? part : part && typeof part.text === "string" ? part.text : ""))
      .join("");
  }
  return "";
}

/**
 * Parses the scenario DSL out of the text of the last user message.
 * @param {string} text The message text.
 * @returns {{kind: "echo" | "say" | "tool" | "error" | "invalid", delayMs: number, text?: string, name?: string, args?: string, status?: number, reason?: string}} The scenario.
 */
export function parseScenario(text) {
  let delayMs = 0;
  let main = null;
  for (const line of String(text).split(/\r?\n/)) {
    const match = DIRECTIVE.exec(line);
    if (!match) continue;
    const [, directive, rest = ""] = match;
    if (directive === "slow") {
      if (!/^\d+$/.test(rest) || Number(rest) > MAX_SLOW_MS) {
        return { kind: "invalid", delayMs, reason: `@slow needs an integer 0..${MAX_SLOW_MS}, got "${rest}"` };
      }
      delayMs = Number(rest);
      continue;
    }
    if (main) continue;
    if (directive === "say") {
      main = { kind: "say", text: rest };
    } else if (directive === "error") {
      const status = /^\d{3}$/.test(rest) ? Number(rest) : 0;
      if (status < 400 || status > 599) return { kind: "invalid", delayMs, reason: `@error needs a status 400..599, got "${rest}"` };
      main = { kind: "error", status };
    } else {
      const split = /^(\S+)(?:\s+([\s\S]*))?$/.exec(rest);
      const name = split?.[1] ?? "";
      const rawArgs = (split?.[2] ?? "").trim() || "{}";
      if (!TOOL_NAME.test(name)) return { kind: "invalid", delayMs, reason: `@tool needs a tool name, got "${name}"` };
      let args;
      try {
        const parsed = JSON.parse(rawArgs);
        if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed)) throw new Error("not an object");
        args = JSON.stringify(parsed);
      } catch {
        return { kind: "invalid", delayMs, reason: `@tool arguments must be a JSON object, got "${rawArgs}"` };
      }
      main = { kind: "tool", name, args };
    }
  }
  return { delayMs, ...(main ?? { kind: "echo" }) };
}

/**
 * Decides the reply to one chat-completions request body.
 * @param {any} body The parsed request body.
 * @returns {{type: "text", text: string, delayMs: number, scenario: string} | {type: "tool", name: string, args: string, delayMs: number, scenario: string} | {type: "error", status: number, message: string, scenario: string}} The plan.
 */
export function planReply(body) {
  const messages = Array.isArray(body?.messages) ? body.messages : [];
  let userIndex = -1;
  for (let i = messages.length - 1; i >= 0; i--) {
    if (messages[i]?.role === "user") {
      userIndex = i;
      break;
    }
  }
  const userText = userIndex === -1 ? "" : messageText(messages[userIndex]);
  const scenario = parseScenario(userText);
  const last = messages.at(-1);

  if (last?.role === "tool") {
    return { type: "text", text: `done: ${messageText(last).length}`, delayMs: scenario.delayMs, scenario: scenario.kind };
  }
  switch (scenario.kind) {
    case "invalid":
      return { type: "text", text: `fake-model: ${scenario.reason}`, delayMs: scenario.delayMs, scenario: "invalid" };
    case "error":
      return { type: "error", status: scenario.status, message: `fake-model scripted error ${scenario.status}`, scenario: "error" };
    case "say":
      return { type: "text", text: scenario.text, delayMs: scenario.delayMs, scenario: "say" };
    case "tool": {
      if (!offeredToolNames(body).includes(scenario.name)) {
        return { type: "text", text: `fake-model: tool "${scenario.name}" is not offered in this request`, delayMs: scenario.delayMs, scenario: "tool-unavailable" };
      }
      return { type: "tool", name: scenario.name, args: scenario.args, delayMs: scenario.delayMs, scenario: "tool" };
    }
    default:
      return { type: "text", text: `You said: ${userText.trim()}`, delayMs: scenario.delayMs, scenario: "echo" };
  }
}

function offeredToolNames(body) {
  if (!Array.isArray(body?.tools)) return [];
  return body.tools.map((tool) => tool?.function?.name ?? tool?.name).filter((name) => typeof name === "string");
}

const tokens = (chars) => Math.ceil(chars / 4);

function buildUsage(body, plan) {
  const promptChars = JSON.stringify(body?.messages ?? []).length;
  const completionChars = plan.type === "text" ? plan.text.length : plan.name.length + plan.args.length;
  const prompt = tokens(promptChars);
  const completion = tokens(completionChars);
  return { prompt_tokens: prompt, completion_tokens: completion, total_tokens: prompt + completion };
}

/** Splits text into word chunks that concatenate back to the text. */
export function textChunks(text) {
  return text.match(/\S+\s*|\s+/g) ?? [];
}

function argumentFragments(args) {
  const out = [];
  for (let i = 0; i < args.length; i += 16) out.push(args.slice(i, i + 16));
  return out.length > 0 ? out : [""];
}

function streamChunks(plan, ids) {
  const base = { id: ids.completionId, object: "chat.completion.chunk", created: CREATED, model: ids.model };
  const chunk = (delta, finishReason = null) => ({ ...base, choices: [{ index: 0, delta, finish_reason: finishReason }] });
  const chunks = [chunk({ role: "assistant", content: "" })];
  if (plan.type === "text") {
    for (const piece of textChunks(plan.text)) chunks.push(chunk({ content: piece }));
    chunks.push(chunk({}, "stop"));
  } else {
    const fragments = argumentFragments(plan.args);
    chunks.push(chunk({ tool_calls: [{ index: 0, id: ids.toolCallId, type: "function", function: { name: plan.name, arguments: fragments[0] } }] }));
    for (const fragment of fragments.slice(1)) chunks.push(chunk({ tool_calls: [{ index: 0, function: { arguments: fragment } }] }));
    chunks.push(chunk({}, "tool_calls"));
  }
  // The usage chunk has no choices, exactly as OpenAI sends it when stream_options.include_usage is set.
  chunks.push({ ...base, choices: [], usage: ids.usage });
  return chunks;
}

function completionObject(plan, ids) {
  const message =
    plan.type === "text"
      ? { role: "assistant", content: plan.text }
      : { role: "assistant", content: null, tool_calls: [{ id: ids.toolCallId, type: "function", function: { name: plan.name, arguments: plan.args } }] };
  return {
    id: ids.completionId,
    object: "chat.completion",
    created: CREATED,
    model: ids.model,
    choices: [{ index: 0, message, finish_reason: plan.type === "text" ? "stop" : "tool_calls" }],
    usage: ids.usage,
  };
}

function sendJson(res, status, value, extraHeaders = {}) {
  const payload = JSON.stringify(value);
  res.writeHead(status, { "content-type": "application/json; charset=utf-8", "content-length": Buffer.byteLength(payload), ...extraHeaders });
  res.end(payload);
}

function sendError(res, status, message, type = "invalid_request_error") {
  sendJson(res, status, { error: { message, type, param: null, code: String(status) } });
}

async function readBody(req) {
  const parts = [];
  let size = 0;
  for await (const part of req) {
    size += part.length;
    if (size > MAX_BODY_BYTES) throw Object.assign(new Error("request body too large"), { status: 413 });
    parts.push(part);
  }
  return Buffer.concat(parts);
}

/**
 * Starts the server.
 * @param {{host?: string, port?: number, modelId?: string, verbose?: boolean}} [options] Listen options; port 0 picks a free port.
 * @returns {Promise<{url: string, port: number, baseUrl: string, requests: object[], close: () => Promise<void>}>} The running server. `requests` records one entry per chat-completions request (never a credential).
 */
export async function startFakeModel({ host = "127.0.0.1", port = 0, modelId = DEFAULT_MODEL_ID, verbose = false } = {}) {
  const requests = [];

  const server = createServer(async (req, res) => {
    const path = new URL(req.url ?? "/", "http://fake").pathname;
    try {
      if (path === "/healthz") return sendJson(res, 200, { status: "ok" });
      if (path === "/v1/models") {
        if (req.method !== "GET") return sendError(res, 405, "use GET");
        return sendJson(res, 200, { object: "list", data: [{ id: modelId, object: "model", created: CREATED, owned_by: "fake" }] });
      }
      if (path === "/v1/chat/completions") {
        if (req.method !== "POST") return sendError(res, 405, "use POST");
        return await handleChat(req, res);
      }
      return sendError(res, 404, `no route ${req.method} ${path}`, "not_found_error");
    } catch (error) {
      if (res.headersSent) return res.destroy();
      return sendError(res, error?.status ?? 500, error?.message ?? "internal error", "server_error");
    }
  });

  async function handleChat(req, res) {
    const raw = await readBody(req);
    let body;
    try {
      body = JSON.parse(raw.toString("utf8"));
    } catch {
      return sendError(res, 400, "request body is not valid JSON");
    }
    if (body === null || typeof body !== "object" || !Array.isArray(body.messages)) {
      return sendError(res, 400, "'messages' must be an array");
    }

    const plan = planReply(body);
    const model = typeof body.model === "string" && body.model !== "" ? body.model : modelId;
    const digest = createHash("sha256").update(raw).digest("hex");
    const ids = { completionId: `chatcmpl-fake-${digest.slice(0, 24)}`, toolCallId: `call_fake_${digest.slice(0, 16)}`, model };
    ids.usage = plan.type === "error" ? undefined : buildUsage(body, plan);

    if (requests.length < MAX_RECORDED_REQUESTS) {
      requests.push({
        scenario: plan.scenario,
        stream: body.stream === true,
        model,
        messageCount: body.messages.length,
        toolNames: offeredToolNames(body),
        hasAuthorization: typeof req.headers.authorization === "string" && req.headers.authorization.length > 0,
      });
    }
    if (verbose) process.stderr.write(`fake-model: ${plan.scenario} stream=${body.stream === true} messages=${body.messages.length}\n`);

    if (plan.type === "error") return sendError(res, plan.status, plan.message, plan.status >= 500 ? "server_error" : "invalid_request_error");

    if (body.stream !== true) {
      if (plan.delayMs > 0) await sleep(plan.delayMs);
      return sendJson(res, 200, completionObject(plan, ids));
    }

    let closed = false;
    res.on("close", () => {
      closed = true;
    });
    res.writeHead(200, { "content-type": "text/event-stream; charset=utf-8", "cache-control": "no-cache", connection: "keep-alive" });
    for (const chunk of streamChunks(plan, ids)) {
      if (plan.delayMs > 0) await sleep(plan.delayMs);
      if (closed) return;
      res.write(`data: ${JSON.stringify(chunk)}\n\n`);
    }
    res.write("data: [DONE]\n\n");
    res.end();
  }

  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(port, host, resolve);
  });
  const address = server.address();
  const actualPort = typeof address === "object" && address ? address.port : port;
  const url = `http://${host}:${actualPort}`;
  return {
    url,
    port: actualPort,
    baseUrl: `${url}/v1`,
    requests,
    close: () =>
      new Promise((resolve) => {
        server.close(() => resolve());
        server.closeAllConnections();
      }),
  };
}

function parseArgs(argv) {
  const options = {
    host: process.env.FAKE_MODEL_HOST ?? "127.0.0.1",
    port: Number(process.env.FAKE_MODEL_PORT ?? 18080),
    modelId: process.env.FAKE_MODEL_ID ?? DEFAULT_MODEL_ID,
    verbose: false,
  };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    const value = () => {
      if (i + 1 >= argv.length) throw new Error(`${arg} needs a value`);
      return argv[++i];
    };
    if (arg === "--port") options.port = Number(value());
    else if (arg === "--host") options.host = value();
    else if (arg === "--model") options.modelId = value();
    else if (arg === "--verbose") options.verbose = true;
    else throw new Error(`unknown argument ${arg}`);
  }
  if (!Number.isInteger(options.port) || options.port < 0 || options.port > 65535) throw new Error("the port must be an integer 0..65535");
  return options;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const options = parseArgs(process.argv.slice(2));
    const running = await startFakeModel(options);
    process.stdout.write(`fake-model listening on ${running.baseUrl} (model ${options.modelId})\n`);
    const stop = () => running.close().then(() => process.exit(0));
    process.on("SIGINT", stop);
    process.on("SIGTERM", stop);
  } catch (error) {
    process.stderr.write(`fake-model-server: ${error.message}\n`);
    process.exit(1);
  }
}
