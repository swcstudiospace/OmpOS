import { closeSync, fstatSync, openSync, readSync } from "node:fs";
import { resolve as resolvePath } from "node:path";

export const POST_TIMEOUT_MS = 5000;
export const SHUTDOWN_TIMEOUT_MS = 2000;
export const DEFAULT_REQUEST_TIMEOUT_MS = 130000;
export const MIN_REQUEST_TIMEOUT_MS = 1000;
export const MAX_REQUEST_TIMEOUT_MS = 900000;
export const MAX_TEXT_BYTES = 256 * 1024;
const MAX_SUMMARY_BYTES = 4 * 1024;
const MAX_STARTUP_BYTES = 1024 * 1024;
const MAX_OMP_STRING = 64 * 1024;
const MAX_OMP_ARRAY = 50;
const MAX_OMP_DEPTH = 6;

const BLOCKABLE = new Set([
  "preToolUse",
  "beforeShellExecution",
  "beforeMCPExecution",
  "beforeReadFile",
  "subagentStart",
  "beforeSubmitPrompt",
]);
const LOOPBACK_HOSTS = new Set(["127.0.0.1", "localhost", "[::1]"]);
const EDIT_TOOLS = new Set(["edit", "write", "ast_edit"]);
const CURSOR_TOOL_NAMES = {
  bash: "Shell",
  read: "Read",
  edit: "Write",
  write: "Write",
  ast_edit: "Write",
  grep: "Grep",
  ast_grep: "Grep",
  find: "Grep",
  glob: "Grep",
  task: "Task",
  web_search: "WebSearch",
};

/** Every omp event the extension subscribes to. */
export const EVENT_NAMES = [
  "tool_call",
  "tool_result",
  "input",
  "session_start",
  "session_shutdown",
  "session_stop",
  "before_subagent_spawn",
  "session_before_compact",
  "message_end",
  "auto_compaction_start",
  "agent_end",
  "turn_end",
  "tool_approval_requested",
  "tool_approval_resolved",
];

const FIRE_AND_FORGET = new Set([
  "session_start",
  "message_end",
  "auto_compaction_start",
  "agent_end",
  "turn_end",
  "tool_approval_requested",
  "tool_approval_resolved",
]);

/**
 * Truncate a string to at most `maxBytes` UTF-8 bytes.
 * @param {string} text
 * @param {number} maxBytes
 */
export function truncateUtf8(text, maxBytes) {
  const buf = Buffer.from(text, "utf8");
  if (buf.length <= maxBytes) return text;
  return buf.subarray(0, maxBytes).toString("utf8").replace(/\uFFFD+$/u, "");
}

/** @param {unknown} v */
function parseRequestTimeout(v) {
  if (typeof v !== "number" || !Number.isInteger(v)) return DEFAULT_REQUEST_TIMEOUT_MS;
  return Math.min(MAX_REQUEST_TIMEOUT_MS, Math.max(MIN_REQUEST_TIMEOUT_MS, v));
}

/** @param {string} path @param {number} maxBytes */
function defaultReadFile(path, maxBytes) {
  const fd = openSync(path, "r");
  try {
    if (!fstatSync(fd).isFile()) throw new Error("not a regular file");
    const buf = Buffer.alloc(maxBytes);
    const n = readSync(fd, buf, 0, maxBytes, 0);
    return buf.subarray(0, n).toString("utf8");
  } finally {
    closeSync(fd);
  }
}

/** @param {unknown} v */
const isObject = (v) => typeof v === "object" && v !== null && !Array.isArray(v);

/** @param {string} url @returns {string | null} */
export function checkLoopbackUrl(url) {
  try {
    const u = new URL(url);
    if (u.protocol !== "http:") return null;
    if (!LOOPBACK_HOSTS.has(u.hostname)) return null;
    return u.href;
  } catch {
    return null;
  }
}

/**
 * JSON-safe copy without functions, signals, or giant arrays/strings.
 * @param {unknown} value
 * @param {number} [depth]
 * @returns {unknown}
 */
export function safeCopy(value, depth = 0) {
  if (value === null || value === undefined) return value ?? null;
  if (typeof value === "string") return truncateUtf8(value, MAX_OMP_STRING);
  if (typeof value === "number" || typeof value === "boolean") return value;
  if (typeof value !== "object") return undefined;
  if (typeof AbortSignal !== "undefined" && value instanceof AbortSignal) return undefined;
  if (depth >= MAX_OMP_DEPTH) return "[truncated]";
  if (Array.isArray(value)) {
    return value.slice(0, MAX_OMP_ARRAY).map((v) => safeCopy(v, depth + 1) ?? null);
  }
  /** @type {Record<string, unknown>} */
  const out = {};
  for (const [k, v] of Object.entries(value)) {
    if (typeof v === "function" || v === undefined) continue;
    const c = safeCopy(v, depth + 1);
    if (c !== undefined) out[k] = c;
  }
  return out;
}

/** @param {any} content @returns {string[]} */
function textBlocks(content) {
  if (typeof content === "string") return [content];
  if (!Array.isArray(content)) return [];
  return content.filter((b) => b && b.type === "text" && typeof b.text === "string").map((b) => b.text);
}

/**
 * Split `mcp__<server>_<tool>`. Names are minted by omp as `mcp__${server}_${tool}`; a server name
 * containing `_` is ambiguous, so the first `_` after the prefix is taken as the separator.
 * @param {string} name
 */
export function parseMcpName(name) {
  const rest = name.slice("mcp__".length);
  const i = rest.indexOf("_");
  if (i <= 0) return { server: rest, tool: rest };
  return { server: rest.slice(0, i), tool: rest.slice(i + 1) };
}

/** @param {string} name */
export function cursorToolName(name) {
  if (name.startsWith("mcp__")) return `MCP:${parseMcpName(name).tool}`;
  return CURSOR_TOOL_NAMES[name] ?? name;
}

/**
 * @typedef {object} BridgeOptions
 * @property {Record<string, string | undefined>} [env]
 * @property {typeof fetch} [fetch]
 * @property {() => number} [now]
 * @property {(path: string, maxBytes: number) => string} [readFile]
 * @property {(fn: () => void, ms: number) => unknown} [setTimeout]
 * @property {(handle: unknown) => void} [clearTimeout]
 * @property {() => any[]} [getAllTools]
 */

/**
 * Create the bridge. Nothing is read or sent until the first event.
 * @param {BridgeOptions} [options]
 */
export function createHookBridge(options = {}) {
  const env = options.env ?? process.env;
  const doFetch = options.fetch ?? ((...a) => globalThis.fetch(...a));
  const now = options.now ?? Date.now;
  const readFile = options.readFile ?? defaultReadFile;
  const setT = options.setTimeout ?? ((fn, ms) => setTimeout(fn, ms));
  const clearT = options.clearTimeout ?? ((h) => clearTimeout(/** @type {any} */ (h)));
  const getAllTools = options.getAllTools ?? (() => []);

  /** @type {{ activeEvents: Set<string> | null, activeOmp: Set<string> | null, failClosed: Set<string>, requestTimeoutMs: number } | null} */
  let startup = null;
  /** @type {string | null | undefined} */
  let endpoint;
  /** @type {Map<string, number>} */
  const toolStarts = new Map();
  /** @type {Set<Promise<unknown>>} */
  const pending = new Set();
  let sessionStartedAt = null;
  let autoCompactionPending = false;

  const enabled = () => {
    if (!env.AUTONOGRAMMER_HOOKS_URL || !env.AUTONOGRAMMER_HOOKS_TOKEN) return false;
    if (endpoint === undefined) endpoint = checkLoopbackUrl(env.AUTONOGRAMMER_HOOKS_URL);
    return endpoint !== null;
  };

  const loadStartup = () => {
    if (startup) return startup;
    startup = { activeEvents: null, activeOmp: null, failClosed: new Set(), requestTimeoutMs: DEFAULT_REQUEST_TIMEOUT_MS };
    const file = env.AUTONOGRAMMER_HOOKS_STARTUP_FILE;
    if (!file) return startup;
    try {
      const parsed = JSON.parse(readFile(file, MAX_STARTUP_BYTES));
      if (!isObject(parsed)) return startup;
      const strs = (v) => (Array.isArray(v) ? v.filter((s) => typeof s === "string") : null);
      const active = strs(parsed.activeEvents);
      const activeOmp = strs(parsed.activeOmpEvents);
      const closed = strs(parsed.failClosedEvents);
      startup = {
        activeEvents: active ? new Set(active) : null,
        activeOmp: activeOmp ? new Set(activeOmp) : null,
        failClosed: new Set(closed ?? []),
        requestTimeoutMs: parseRequestTimeout(parsed.requestTimeoutMs),
      };
    } catch {
      startup = { activeEvents: null, activeOmp: null, failClosed: new Set(), requestTimeoutMs: DEFAULT_REQUEST_TIMEOUT_MS };
    }
    return startup;
  };

  /** POST timeout for responses that are awaited and applied. */
  const reqMs = () => loadStartup().requestTimeoutMs;

  const cursorActive = (name) => {
    const s = loadStartup();
    return s.activeEvents === null || s.activeEvents.has(name);
  };
  const ompActive = (name) => {
    const s = loadStartup();
    return s.activeOmp !== null && s.activeOmp.has(name);
  };

  /**
   * POST one event. Never throws. `ok:false` means failed (unreachable, timeout, non-200, bad JSON).
   * @returns {Promise<{ ok: boolean, decision: Record<string, any> }>}
   */
  const post = async (event, kind, payload, ctx, toolCallId, timeoutMs = POST_TIMEOUT_MS) => {
    const failed = { ok: false, decision: {} };
    let timer;
    try {
      const body = { event, kind, payload };
      if (toolCallId) body.toolCallId = String(toolCallId);
      const sid = sessionIdOf(ctx);
      if (sid) body.sessionId = sid;
      const controller = new AbortController();
      let timedOut = false;
      const timeout = new Promise((_, reject) => {
        timer = setT(() => {
          timedOut = true;
          controller.abort();
          reject(new Error("timeout"));
        }, timeoutMs);
      });
      const request = (async () => {
        const res = await doFetch(/** @type {string} */ (endpoint), {
          method: "POST",
          headers: {
            Authorization: `Bearer ${env.AUTONOGRAMMER_HOOKS_TOKEN}`,
            "Content-Type": "application/json",
          },
          body: JSON.stringify(body),
          signal: controller.signal,
        });
        if (res.status !== 200) return failed;
        const json = await res.json();
        return isObject(json) ? { ok: true, decision: json } : failed;
      })();
      request.catch(() => {});
      const result = await Promise.race([request, timeout]);
      return timedOut ? failed : result;
    } catch {
      return failed;
    } finally {
      if (timer !== undefined) clearT(timer);
    }
  };

  const sessionIdOf = (ctx) => {
    try {
      const id = ctx?.sessionManager?.getSessionId?.();
      return typeof id === "string" && id ? id : undefined;
    } catch {
      return undefined;
    }
  };

  const notify = (ctx, message) => {
    try {
      if (ctx?.hasUI && typeof message === "string" && message) ctx.ui.notify(message, "warn");
    } catch {
      // never throw into omp
    }
  };

  const failClosedDeny = (name, res) =>
    !res.ok && BLOCKABLE.has(name) && loadStartup().failClosed.has(name);
  const denyReason = (name) => `Autonogrammer hook host unreachable; ${name} is configured fail-closed`;
  const isDeny = (d) => d.permission === "deny";
  const denyText = (d) => d.agent_message || d.user_message || "Blocked by an Autonogrammer hook";

  const isMcpTool = (name) => {
    if (name.startsWith("mcp__")) return true;
    try {
      const info = getAllTools().find((t) => t?.name === name);
      return info?.sourceInfo?.source === "mcp";
    } catch {
      return false;
    }
  };

  /** Cursor event helper: returns null when inactive. */
  const cursor = async (name, payload, ctx, toolCallId, timeoutMs) => {
    if (!cursorActive(name)) return null;
    return post(name, "cursor", payload, ctx, toolCallId, timeoutMs);
  };

  /** omp-kind event: returns the omp_result object or null. */
  const omp = async (name, event, ctx, extra, timeoutMs) => {
    if (!ompActive(name)) return null;
    const payload = safeCopy({ ...event, ...extra });
    const res = await post(name, "omp", payload, ctx, event?.toolCallId, timeoutMs);
    if (!res.ok) return null;
    return res.decision;
  };

  const mergeOmp = (result, decision) => {
    const r = decision && isObject(decision.omp_result) ? decision.omp_result : null;
    if (!r) return result;
    const block = result.block === true || r.block === true;
    const merged = { ...result, ...r };
    if (block) merged.block = true;
    if (block && result.reason && !r.reason) merged.reason = result.reason;
    return merged;
  };

  const addContext = (result, text) => {
    if (typeof text !== "string" || !text) return;
    result.additionalContext = result.additionalContext ? `${result.additionalContext}\n${text}` : text;
  };

  // ---------------------------------------------------------------- tool_call
  const onToolCall = async (event, ctx) => {
    const toolName = String(event.toolName ?? "");
    const toolCallId = event.toolCallId;
    toolStarts.set(toolCallId, now());
    const cwd = ctx?.cwd;
    let input = isObject(event.input) ? event.input : {};
    /** @type {Record<string, any>} */
    let result = {};
    let changedInput = false;
    const mcp = isMcpTool(toolName);

    /** @type {Array<[string, () => Record<string, any>]>} */
    const steps = [
      [
        "preToolUse",
        () => ({
          tool_name: cursorToolName(toolName),
          tool_input: input,
          tool_use_id: toolCallId,
          cwd,
          omp_tool_name: toolName,
        }),
      ],
    ];
    if (toolName === "bash") {
      steps.push([
        "beforeShellExecution",
        () => ({ command: String(input.command ?? ""), cwd, sandbox: false }),
      ]);
    } else if (mcp) {
      steps.push([
        "beforeMCPExecution",
        () => ({
          tool_name: parseMcpName(toolName).tool,
          tool_input: JSON.stringify(input),
          mcp_server_name: parseMcpName(toolName).server,
        }),
      ]);
    } else if (toolName === "read") {
      steps.push(["beforeReadFile", () => readFilePayload(input, cwd)]);
    }

    for (const [name, build] of steps) {
      if (!cursorActive(name)) continue;
      const res = await post(name, "cursor", build(), ctx, toolCallId, reqMs());
      if (failClosedDeny(name, res)) return { block: true, reason: denyReason(name) };
      const d = res.decision;
      if (isDeny(d)) return { block: true, reason: denyText(d) };
      if (isObject(d.updated_input)) {
        input = d.updated_input;
        changedInput = true;
      }
      addContext(result, d.additional_context);
    }
    if (changedInput) result.input = input;

    const od = await omp("tool_call", event, ctx, undefined, reqMs());
    result = mergeOmp(result, od);
    return Object.keys(result).length ? result : undefined;
  };

  const readFilePayload = (input, cwd) => {
    const raw = input.path ?? input.file_path ?? input.filePath;
    const payload = { attachments: [] };
    if (typeof raw !== "string" || !raw) return payload;
    const abs = resolvePath(typeof cwd === "string" && cwd ? cwd : process.cwd(), raw);
    payload.file_path = abs;
    try {
      payload.content = readFile(abs, MAX_TEXT_BYTES);
    } catch {
      // unreadable or not a regular file: content omitted
    }
    return payload;
  };

  // -------------------------------------------------------------- tool_result
  const onToolResult = async (event, ctx) => {
    const toolName = String(event.toolName ?? "");
    const toolCallId = event.toolCallId;
    const startedAt = toolStarts.get(toolCallId);
    toolStarts.delete(toolCallId);
    const duration = startedAt === undefined ? undefined : Math.max(0, now() - startedAt);
    const input = isObject(event.input) ? event.input : {};
    const cwd = ctx?.cwd;
    const isError = event.isError === true;
    const texts = textBlocks(event.content);
    const mcp = isMcpTool(toolName);
    const outputJson = truncateUtf8(JSON.stringify({ content: texts, isError }), MAX_TEXT_BYTES);
    const base = {
      tool_name: cursorToolName(toolName),
      tool_input: input,
      tool_use_id: toolCallId,
      cwd,
      omp_tool_name: toolName,
    };
    const withDuration = (o) => (duration === undefined ? o : { ...o, duration });

    /** @type {Array<[string, Record<string, any>]>} */
    const steps = [];
    if (isError) {
      steps.push([
        "postToolUseFailure",
        withDuration({
          ...base,
          error_message: truncateUtf8(texts.join("\n"), MAX_TEXT_BYTES),
          failure_type: "error",
          is_interrupt: false,
        }),
      ]);
    } else {
      steps.push(["postToolUse", withDuration({ ...base, tool_output: outputJson })]);
    }
    if (toolName === "bash") {
      steps.push([
        "afterShellExecution",
        withDuration({
          command: String(input.command ?? ""),
          output: truncateUtf8(texts.join("\n"), MAX_TEXT_BYTES),
          sandbox: false,
        }),
      ]);
    } else if (mcp) {
      const d = isObject(event.details) ? event.details : {};
      const parsed = parseMcpName(toolName);
      steps.push([
        "afterMCPExecution",
        withDuration({
          tool_name: typeof d.mcpToolName === "string" ? d.mcpToolName : parsed.tool,
          tool_input: JSON.stringify(input),
          mcp_server_name: typeof d.serverName === "string" ? d.serverName : parsed.server,
          result_json: outputJson,
        }),
      ]);
    } else if (EDIT_TOOLS.has(toolName) && !isError) {
      steps.push(["afterFileEdit", editPayload(input, cwd)]);
    } else if (toolName === "task") {
      const text = texts.join("\n");
      steps.push([
        "subagentStop",
        ({
          subagent_type: typeof input.agent === "string" && input.agent ? input.agent : "generalPurpose",
          status: isError ? "error" : "completed",
          task: String(input.task ?? input.assignment ?? ""),
          summary: truncateUtf8(text, MAX_SUMMARY_BYTES),
          loop_count: 0,
          modified_files: [],
          ...(duration === undefined ? {} : { duration_ms: duration }),
        }),
      ]);
    }

    /** @type {Record<string, any>} */
    let result = {};
    for (const [name, payload] of steps) {
      const res = await cursor(name, payload, ctx, toolCallId, name === "postToolUse" || name === "subagentStop" ? reqMs() : undefined);
      if (!res || !res.ok) continue;
      const d = res.decision;
      addContext(result, d.additional_context);
      addContext(result, d.followup_message);
      if (mcp && d.updated_mcp_tool_output !== undefined && d.updated_mcp_tool_output !== null) {
        result.content = [{ type: "text", text: JSON.stringify(d.updated_mcp_tool_output) }];
      }
    }
    const od = await omp("tool_result", event, ctx, undefined, reqMs());
    result = mergeOmp(result, od);
    return Object.keys(result).length ? result : undefined;
  };

  const editPayload = (input, cwd) => {
    const raw = input.path ?? input.file_path ?? input.filePath;
    const payload = { edits: [] };
    if (typeof raw === "string" && raw) {
      payload.file_path = resolvePath(typeof cwd === "string" && cwd ? cwd : process.cwd(), raw);
    }
    const oldS = input.old_string ?? input.oldText ?? input.old_text;
    const newS = input.new_string ?? input.newText ?? input.new_text ?? input.content;
    if (typeof newS === "string" || typeof oldS === "string") {
      payload.edits.push({ old_string: typeof oldS === "string" ? oldS : "", new_string: typeof newS === "string" ? newS : "" });
    }
    return payload;
  };

  // -------------------------------------------------------------------- input
  const onInput = async (event, ctx) => {
    const name = "beforeSubmitPrompt";
    /** @type {Record<string, any>} */
    let result = {};
    let handled = false;
    if (cursorActive(name)) {
      const res = await post(name, "cursor", { prompt: String(event.text ?? ""), attachments: [] }, ctx, undefined, reqMs());
      if (failClosedDeny(name, res)) {
        notify(ctx, denyReason(name));
        handled = true;
      } else if (isDeny(res.decision) || res.decision.continue === false) {
        notify(ctx, res.decision.user_message);
        handled = true;
      }
    }
    if (handled) result.handled = true;
    const od = await omp("input", event, ctx, undefined, reqMs());
    result = mergeOmp(result, od);
    return Object.keys(result).length ? result : undefined;
  };

  // --------------------------------------------------------- session lifecycle
  const onSessionStart = async (event, ctx) => {
    sessionStartedAt = now();
    await cursor("sessionStart", { session_id: sessionIdOf(ctx), is_background_agent: false, composer_mode: "agent" }, ctx);
    await omp("session_start", event, ctx);
  };

  const onSessionShutdown = async (event, ctx) => {
    const payload = {
      session_id: sessionIdOf(ctx),
      reason: "user_close",
      is_background_agent: false,
      final_status: "completed",
    };
    if (sessionStartedAt !== null) payload.duration_ms = Math.max(0, now() - sessionStartedAt);
    await cursor("sessionEnd", payload, ctx, undefined, SHUTDOWN_TIMEOUT_MS);
    await omp("session_shutdown", event, ctx, undefined, SHUTDOWN_TIMEOUT_MS);
  };

  const onSessionStop = async (event, ctx) => {
    /** @type {Record<string, any>} */
    let result = {};
    const res = await cursor("stop", { status: "completed", loop_count: 0 }, ctx, undefined, reqMs());
    const follow = res?.ok ? res.decision.followup_message : undefined;
    if (typeof follow === "string" && follow) {
      result = { continue: true, additionalContext: follow };
    }
    const od = await omp("session_stop", { ...event, messages: undefined, last_assistant_message: undefined }, ctx, undefined, reqMs());
    result = mergeOmp(result, od);
    return Object.keys(result).length ? result : undefined;
  };

  const onSubagentSpawn = async (event, ctx) => {
    const name = "subagentStart";
    /** @type {Record<string, any>} */
    let result = {};
    if (cursorActive(name)) {
      const payload = {
        subagent_type: event.agent,
        task: "",
        is_parallel_worker: false,
      };
      if (Array.isArray(event.patterns) && typeof event.patterns[0] === "string") {
        payload.subagent_model = event.patterns[0];
      }
      if (event.spawnKey) payload.tool_call_id = String(event.spawnKey);
      const res = await post(name, "cursor", payload, ctx, undefined, reqMs());
      if (failClosedDeny(name, res)) return { block: true, reason: denyReason(name) };
      if (isDeny(res.decision)) return { block: true, reason: denyText(res.decision) };
    }
    const od = await omp("before_subagent_spawn", event, ctx, undefined, reqMs());
    result = mergeOmp(result, od);
    return Object.keys(result).length ? result : undefined;
  };

  const compactPayload = (trigger, event, ctx) => {
    const p = { trigger };
    try {
      const u = ctx?.getContextUsage?.();
      if (u) {
        if (Number.isFinite(u.tokens)) p.context_tokens = u.tokens;
        if (Number.isFinite(u.contextWindow)) p.context_window_size = u.contextWindow;
        if (Number.isFinite(u.percent)) p.context_usage_percent = u.percent;
      }
    } catch {
      // usage unavailable
    }
    const prep = event?.preparation;
    if (prep) {
      if (Array.isArray(prep.messagesToSummarize)) {
        p.messages_to_compact = prep.messagesToSummarize.length;
        const recent = Array.isArray(prep.recentMessages) ? prep.recentMessages.length : 0;
        p.message_count = prep.messagesToSummarize.length + recent;
      }
      if (typeof prep.previousSummary === "string") p.is_first_compaction = false;
      else if ("previousSummary" in prep || Array.isArray(prep.messagesToSummarize)) p.is_first_compaction = true;
    }
    return p;
  };

  const onBeforeCompact = async (event, ctx) => {
    const trigger = autoCompactionPending ? "auto" : "manual";
    autoCompactionPending = false;
    const res = await cursor("preCompact", compactPayload(trigger, event, ctx), ctx, undefined, reqMs());
    if (res?.ok) notify(ctx, res.decision.user_message);
    const od = await omp("session_before_compact", { type: event.type, customInstructions: event.customInstructions }, ctx, undefined, reqMs());
    const r = od && isObject(od.omp_result) ? od.omp_result : null;
    return r && r.cancel === true ? { cancel: true } : undefined;
  };

  const onAutoCompaction = async (event, ctx) => {
    autoCompactionPending = true;
    await cursor("preCompact", compactPayload("auto", undefined, ctx), ctx);
    await omp("auto_compaction_start", event, ctx);
  };

  const onMessageEnd = async (event, ctx) => {
    const message = event.message;
    if (message?.role === "assistant" && Array.isArray(message.content)) {
      const text = message.content.filter((b) => b?.type === "text" && typeof b.text === "string").map((b) => b.text).join("");
      const thought = message.content
        .filter((b) => b?.type === "thinking" && typeof b.thinking === "string")
        .map((b) => b.thinking)
        .join("");
      if (text) await cursor("afterAgentResponse", { text: truncateUtf8(text, MAX_TEXT_BYTES) }, ctx);
      if (thought) await cursor("afterAgentThought", { text: truncateUtf8(thought, MAX_TEXT_BYTES) }, ctx);
    }
    await omp("message_end", { type: event.type, role: message?.role }, ctx);
  };

  const onObserve = (name) => async (event, ctx) => {
    await omp(name, event, ctx);
  };

  /** @type {Record<string, (event: any, ctx: any) => Promise<any>>} */
  const handlers = {
    tool_call: onToolCall,
    tool_result: onToolResult,
    input: onInput,
    session_start: onSessionStart,
    session_shutdown: onSessionShutdown,
    session_stop: onSessionStop,
    before_subagent_spawn: onSubagentSpawn,
    session_before_compact: onBeforeCompact,
    message_end: onMessageEnd,
    auto_compaction_start: onAutoCompaction,
    agent_end: onObserve("agent_end"),
    turn_end: onObserve("turn_end"),
    tool_approval_requested: onObserve("tool_approval_requested"),
    tool_approval_resolved: onObserve("tool_approval_resolved"),
  };

  /**
   * omp handler entry. Never throws. Observe-only events return undefined immediately.
   * @param {string} name
   * @param {any} event
   * @param {any} ctx
   */
  const handle = (name, event, ctx) => {
    try {
      if (!enabled()) return undefined;
      if (ctx?.agent?.kind === "sub") return undefined;
      const fn = handlers[name];
      if (!fn) return undefined;
      const run = fn(event ?? {}, ctx).catch(() => undefined);
      if (FIRE_AND_FORGET.has(name)) {
        const p = run.finally(() => pending.delete(p));
        pending.add(p);
        return undefined;
      }
      return run;
    } catch {
      return undefined;
    }
  };

  return {
    eventNames: EVENT_NAMES,
    handle,
    /** Resolves once every fire-and-forget POST has settled. */
    idle: async () => {
      while (pending.size) await Promise.allSettled([...pending]);
    },
  };
}
