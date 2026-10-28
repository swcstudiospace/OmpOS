import assert from "node:assert/strict";
import { describe, test } from "node:test";
import { cursorToolName, parseMcpName, safeCopy, truncateUtf8 } from "../src/bridge.mjs";
import ompHooksExtension from "../src/index.mjs";
import { TOKEN, makeBridge, makeCtx, startupFile } from "./helpers.mjs";

const call = (bridge, name, event, ctx = makeCtx()) => bridge.handle(name, event, ctx);

describe("tool name mapping", () => {
  test("maps omp tools to Cursor names", () => {
    assert.equal(cursorToolName("bash"), "Shell");
    assert.equal(cursorToolName("read"), "Read");
    for (const n of ["edit", "write", "ast_edit"]) assert.equal(cursorToolName(n), "Write");
    for (const n of ["grep", "ast_grep", "find", "glob"]) assert.equal(cursorToolName(n), "Grep");
    assert.equal(cursorToolName("task"), "Task");
    assert.equal(cursorToolName("web_search"), "WebSearch");
    assert.equal(cursorToolName("mcp__linear_save_issue"), "MCP:save_issue");
    assert.equal(cursorToolName("eval"), "eval");
    assert.deepEqual(parseMcpName("mcp__linear_save_issue"), { server: "linear", tool: "save_issue" });
  });
});

describe("registration", () => {
  test("registers every event without touching fs/network at load", () => {
    const names = [];
    ompHooksExtension({ on: (n, h) => names.push([n, typeof h]) });
    assert.deepEqual(
      names.map((n) => n[0]).sort(),
      [
        "agent_end", "auto_compaction_start", "before_subagent_spawn", "input", "message_end",
        "session_before_compact", "session_shutdown", "session_start", "session_stop", "tool_approval_requested",
        "tool_approval_resolved", "tool_call", "tool_result", "turn_end",
      ],
    );
    assert.ok(names.every((n) => n[1] === "function"));
  });
});

describe("tool_call", () => {
  test("bash: preToolUse then beforeShellExecution with Cursor payloads", async () => {
    const { bridge, fetch } = makeBridge();
    const out = await call(bridge, "tool_call", { toolName: "bash", toolCallId: "c1", input: { command: "ls" } });
    assert.equal(out, undefined);
    assert.deepEqual(fetch.events(), ["preToolUse", "beforeShellExecution"]);
    const [pre, shell] = fetch.calls.map((c) => c.body);
    assert.equal(pre.kind, "cursor");
    assert.equal(pre.sessionId, "sess-1");
    assert.equal(pre.toolCallId, "c1");
    assert.deepEqual(pre.payload, { tool_name: "Shell", tool_input: { command: "ls" }, tool_use_id: "c1", cwd: "/work", omp_tool_name: "bash" });
    assert.deepEqual(shell.payload, { command: "ls", cwd: "/work", sandbox: false });
    const init = fetch.calls[0].init;
    assert.equal(init.headers.Authorization, `Bearer ${TOKEN}`);
    assert.equal(init.headers["Content-Type"], "application/json");
    assert.equal(init.method, "POST");
  });

  test("MCP: beforeMCPExecution carries JSON string input and server", async () => {
    const { bridge, fetch } = makeBridge();
    await call(bridge, "tool_call", { toolName: "mcp__linear_save_issue", toolCallId: "c2", input: { a: 1 } });
    assert.deepEqual(fetch.events(), ["preToolUse", "beforeMCPExecution"]);
    assert.equal(fetch.calls[0].body.payload.tool_name, "MCP:save_issue");
    assert.deepEqual(fetch.calls[1].body.payload, { tool_name: "save_issue", tool_input: '{"a":1}', mcp_server_name: "linear" });
  });

  test("MCP detection via getAllTools sourceInfo", async () => {
    const { bridge, fetch } = makeBridge({ getAllTools: () => [{ name: "weird", sourceInfo: { source: "mcp" } }] });
    await call(bridge, "tool_call", { toolName: "weird", toolCallId: "c", input: {} });
    assert.deepEqual(fetch.events(), ["preToolUse", "beforeMCPExecution"]);
  });

  test("read: beforeReadFile resolves path and includes file content", async () => {
    const { bridge, fetch } = makeBridge({ files: { "/work/a.txt": "hello" } });
    await call(bridge, "tool_call", { toolName: "read", toolCallId: "c3", input: { path: "a.txt" } });
    assert.deepEqual(fetch.calls[1].body.payload, { attachments: [], file_path: "/work/a.txt", content: "hello" });
  });

  test("read: unreadable file omits content", async () => {
    const { bridge, fetch } = makeBridge();
    await call(bridge, "tool_call", { toolName: "read", toolCallId: "c3", input: { path: "/x/nope" } });
    assert.deepEqual(fetch.calls[1].body.payload, { attachments: [], file_path: "/x/nope" });
  });

  test("other tools only post preToolUse", async () => {
    const { bridge, fetch } = makeBridge();
    await call(bridge, "tool_call", { toolName: "grep", toolCallId: "c", input: {} });
    assert.deepEqual(fetch.events(), ["preToolUse"]);
  });

  test("deny on preToolUse short-circuits and maps reason", async () => {
    const { bridge, fetch } = makeBridge({ respond: () => ({ permission: "deny", agent_message: "no", user_message: "u" }) });
    const out = await call(bridge, "tool_call", { toolName: "bash", toolCallId: "c", input: { command: "rm" } });
    assert.deepEqual(out, { block: true, reason: "no" });
    assert.deepEqual(fetch.events(), ["preToolUse"]);
  });

  test("deny falls back to user_message then default", async () => {
    let r = { permission: "deny", user_message: "um" };
    const { bridge } = makeBridge({ respond: () => r });
    const ev = { toolName: "grep", toolCallId: "c", input: {} };
    assert.equal((await call(bridge, "tool_call", ev)).reason, "um");
    r = { permission: "deny" };
    assert.equal((await call(bridge, "tool_call", ev)).reason, "Blocked by an Autonogrammer hook");
  });

  test("deny on beforeShellExecution after allow on preToolUse", async () => {
    const { bridge, fetch } = makeBridge({
      respond: (b) => (b.event === "beforeShellExecution" ? { permission: "deny", agent_message: "shell no" } : { permission: "allow" }),
    });
    const out = await call(bridge, "tool_call", { toolName: "bash", toolCallId: "c", input: { command: "x" } });
    assert.deepEqual(out, { block: true, reason: "shell no" });
    assert.equal(fetch.calls.length, 2);
  });

  test("updated_input: last wins, later payloads see it, additional_context joined", async () => {
    const { bridge, fetch } = makeBridge({
      respond: (b) =>
        b.event === "preToolUse"
          ? { updated_input: { command: "one" }, additional_context: "ctx1" }
          : { updated_input: { command: "two" }, additional_context: "ctx2" },
    });
    const out = await call(bridge, "tool_call", { toolName: "bash", toolCallId: "c", input: { command: "zero" } });
    assert.deepEqual(out, { input: { command: "two" }, additionalContext: "ctx1\nctx2" });
    assert.equal(fetch.calls[1].body.payload.command, "one");
  });

  test("omp section: posts kind omp only when active; omp_result merged, block sticky", async () => {
    const { bridge, fetch } = makeBridge({
      startup: startupFile({ activeOmpEvents: ["tool_call"] }),
      respond: (b) =>
        b.kind === "omp" ? { omp_result: { block: false, reason: "r", additionalContext: "x" } } : { permission: "deny", agent_message: "c" },
    });
    // cursor deny short-circuits before omp
    assert.deepEqual(await call(bridge, "tool_call", { toolName: "grep", toolCallId: "c", input: {} }), { block: true, reason: "c" });
    assert.deepEqual(fetch.events(), ["preToolUse"]);

    const b2 = makeBridge({
      startup: startupFile({ activeOmpEvents: ["tool_call"] }),
      respond: (b) => (b.kind === "omp" ? { omp_result: { block: true, reason: "omp says" } } : {}),
    });
    const out = await call(b2.bridge, "tool_call", { toolName: "grep", toolCallId: "c", input: {}, fn() {} });
    assert.deepEqual(out, { block: true, reason: "omp says" });
    const ompCall = b2.fetch.calls.find((c) => c.body.kind === "omp");
    assert.equal(ompCall.body.event, "tool_call");
    assert.equal(ompCall.body.payload.fn, undefined);
    assert.equal(ompCall.body.payload.toolName, "grep");
  });

  test("omp events are not posted when not in activeOmpEvents", async () => {
    const { bridge, fetch } = makeBridge({ startup: startupFile({ activeOmpEvents: ["turn_end"] }) });
    await call(bridge, "tool_call", { toolName: "grep", toolCallId: "c", input: {} });
    assert.ok(fetch.calls.every((c) => c.body.kind === "cursor"));
  });
});

describe("tool_result", () => {
  const started = (bridge, name, id, input = {}) => call(bridge, "tool_call", { toolName: name, toolCallId: id, input });

  test("postToolUse with tool_output JSON and duration", async () => {
    const { bridge, fetch, clock } = makeBridge();
    await started(bridge, "grep", "r1", { q: 1 });
    fetch.calls.length = 0;
    clock.advance(250);
    const out = await call(bridge, "tool_result", {
      toolName: "grep", toolCallId: "r1", input: { q: 1 }, isError: false, content: [{ type: "text", text: "hit" }, { type: "image" }],
    });
    assert.equal(out, undefined);
    assert.deepEqual(fetch.events(), ["postToolUse"]);
    assert.deepEqual(fetch.calls[0].body.payload, {
      tool_name: "Grep", tool_input: { q: 1 }, tool_use_id: "r1", cwd: "/work", omp_tool_name: "grep",
      tool_output: '{"content":["hit"],"isError":false}', duration: 250,
    });
  });

  test("error result -> postToolUseFailure", async () => {
    const { bridge, fetch } = makeBridge();
    await call(bridge, "tool_result", { toolName: "grep", toolCallId: "r", input: {}, isError: true, content: [{ type: "text", text: "boom" }] });
    assert.deepEqual(fetch.events(), ["postToolUseFailure"]);
    const p = fetch.calls[0].body.payload;
    assert.equal(p.error_message, "boom");
    assert.equal(p.failure_type, "error");
    assert.equal(p.is_interrupt, false);
    assert.equal("duration" in p, false);
  });

  test("bash -> afterShellExecution", async () => {
    const { bridge, fetch } = makeBridge();
    await started(bridge, "bash", "b", { command: "ls" });
    fetch.calls.length = 0;
    await call(bridge, "tool_result", { toolName: "bash", toolCallId: "b", input: { command: "ls" }, isError: false, content: [{ type: "text", text: "out" }] });
    assert.deepEqual(fetch.events(), ["postToolUse", "afterShellExecution"]);
    assert.deepEqual(fetch.calls[1].body.payload, { command: "ls", output: "out", sandbox: false, duration: 0 });
  });

  test("MCP -> afterMCPExecution; updated_mcp_tool_output replaces content; details win for names", async () => {
    const { bridge, fetch } = makeBridge({
      respond: (b) => (b.event === "afterMCPExecution" ? { updated_mcp_tool_output: { ok: 1 } } : { additional_context: "c" }),
    });
    const out = await call(bridge, "tool_result", {
      toolName: "mcp__my_srv_tool", toolCallId: "m", input: { a: 1 }, isError: false,
      details: { serverName: "my_srv", mcpToolName: "tool" }, content: [{ type: "text", text: "r" }],
    });
    assert.deepEqual(out, { additionalContext: "c", content: [{ type: "text", text: '{"ok":1}' }] });
    const p = fetch.calls[1].body.payload;
    assert.equal(p.mcp_server_name, "my_srv");
    assert.equal(p.tool_name, "tool");
    assert.equal(p.tool_input, '{"a":1}');
    assert.equal(p.result_json, '{"content":["r"],"isError":false}');
  });

  test("updated_mcp_tool_output ignored for non-MCP tools", async () => {
    const { bridge } = makeBridge({ respond: () => ({ updated_mcp_tool_output: { x: 1 } }) });
    const out = await call(bridge, "tool_result", { toolName: "grep", toolCallId: "r", input: {}, isError: false, content: [] });
    assert.equal(out, undefined);
  });

  test("edit/write/ast_edit success -> afterFileEdit; error does not", async () => {
    const { bridge, fetch } = makeBridge();
    await call(bridge, "tool_result", { toolName: "edit", toolCallId: "e", input: { path: "f.ts", old_string: "a", new_string: "b" }, isError: false, content: [] });
    assert.deepEqual(fetch.events(), ["postToolUse", "afterFileEdit"]);
    assert.deepEqual(fetch.calls[1].body.payload, { edits: [{ old_string: "a", new_string: "b" }], file_path: "/work/f.ts" });
    fetch.calls.length = 0;
    await call(bridge, "tool_result", { toolName: "write", toolCallId: "e", input: { path: "f.ts" }, isError: false, content: [] });
    assert.deepEqual(fetch.calls[1].body.payload, { edits: [], file_path: "/work/f.ts" });
    fetch.calls.length = 0;
    await call(bridge, "tool_result", { toolName: "write", toolCallId: "e", input: { path: "f.ts" }, isError: true, content: [] });
    assert.deepEqual(fetch.events(), ["postToolUseFailure"]);
  });

  test("task -> subagentStop with followup mapped to additionalContext", async () => {
    const { bridge, fetch } = makeBridge({ respond: (b) => (b.event === "subagentStop" ? { followup_message: "next" } : {}) });
    await started(bridge, "task", "t", { agent: "explore", task: "look" });
    fetch.calls.length = 0;
    const out = await call(bridge, "tool_result", {
      toolName: "task", toolCallId: "t", input: { agent: "explore", task: "look" }, isError: false,
      content: [{ type: "text", text: "x".repeat(5000) }],
    });
    assert.deepEqual(out, { additionalContext: "next" });
    const p = fetch.calls[1].body.payload;
    assert.equal(p.subagent_type, "explore");
    assert.equal(p.status, "completed");
    assert.equal(p.task, "look");
    assert.equal(p.summary.length, 4096);
    assert.equal(p.duration_ms, 0);
    assert.equal(p.loop_count, 0);
    assert.deepEqual(p.modified_files, []);
  });

  test("task without agent defaults to generalPurpose and error status", async () => {
    const { bridge, fetch } = makeBridge();
    await call(bridge, "tool_result", { toolName: "task", toolCallId: "t", input: {}, isError: true, content: [] });
    const p = fetch.calls.find((c) => c.body.event === "subagentStop").body.payload;
    assert.equal(p.subagent_type, "generalPurpose");
    assert.equal(p.status, "error");
  });

  test("tool_output and output truncated to 256 KiB", async () => {
    const { bridge, fetch } = makeBridge();
    const big = "a".repeat(400 * 1024);
    await call(bridge, "tool_result", { toolName: "bash", toolCallId: "b", input: { command: "c" }, isError: false, content: [{ type: "text", text: big }] });
    assert.equal(Buffer.byteLength(fetch.calls[0].body.payload.tool_output), 256 * 1024);
    assert.equal(Buffer.byteLength(fetch.calls[1].body.payload.output), 256 * 1024);
  });

  test("truncateUtf8 never splits multibyte characters", () => {
    const s = "é".repeat(10);
    const t = truncateUtf8(s, 5);
    assert.equal(t, "éé");
    assert.equal(truncateUtf8("abc", 10), "abc");
  });

  test("omp_result merged into tool_result", async () => {
    const { bridge } = makeBridge({
      startup: startupFile({ activeOmpEvents: ["tool_result"] }),
      respond: (b) => (b.kind === "omp" ? { omp_result: { isError: true, details: { d: 1 } } } : {}),
    });
    const out = await call(bridge, "tool_result", { toolName: "grep", toolCallId: "r", input: {}, isError: false, content: [] });
    assert.deepEqual(out, { isError: true, details: { d: 1 } });
  });
});

describe("input", () => {
  test("beforeSubmitPrompt payload; allow returns nothing", async () => {
    const { bridge, fetch } = makeBridge();
    assert.equal(await call(bridge, "input", { text: "hi", source: "interactive" }), undefined);
    assert.deepEqual(fetch.calls[0].body.payload, { prompt: "hi", attachments: [] });
  });

  test("continue:false or deny -> handled with notify", async () => {
    const ctx = makeCtx();
    const { bridge } = makeBridge({ respond: () => ({ continue: false, user_message: "stopped" }) });
    assert.deepEqual(await call(bridge, "input", { text: "hi" }, ctx), { handled: true });
    assert.deepEqual(ctx.notes, [["stopped", "warn"]]);
    const { bridge: b2 } = makeBridge({ respond: () => ({ permission: "deny" }) });
    const noUi = makeCtx({ hasUI: false });
    assert.deepEqual(await call(b2, "input", { text: "hi" }, noUi), { handled: true });
    assert.deepEqual(noUi.notes, []);
  });
});

describe("session events", () => {
  test("session_start/session_shutdown payloads and duration", async () => {
    const { bridge, fetch, clock } = makeBridge();
    await call(bridge, "session_start", { type: "session_start" });
    await bridge.idle();
    clock.advance(1234);
    await call(bridge, "session_shutdown", { type: "session_shutdown" });
    assert.deepEqual(fetch.calls[0].body.payload, { session_id: "sess-1", is_background_agent: false, composer_mode: "agent" });
    assert.deepEqual(fetch.calls[1].body.payload, {
      session_id: "sess-1", reason: "user_close", is_background_agent: false, final_status: "completed", duration_ms: 1234,
    });
    assert.deepEqual(fetch.events(), ["sessionStart", "sessionEnd"]);
  });

  test("session_start is fire-and-forget; shutdown uses 2 s timeout", async () => {
    const { bridge, clock } = makeBridge({ respond: () => "hang" });
    assert.equal(call(bridge, "session_start", {}), undefined);
    assert.equal(clock.pendingTimers()[0].ms, 5000);
    clock.fire();
    await bridge.idle();
    const p = call(bridge, "session_shutdown", {});
    assert.equal(clock.pendingTimers()[0].ms, 2000);
    clock.fire();
    assert.equal(await p, undefined);
  });

  test("session_stop: followup_message -> continue + additionalContext", async () => {
    const { bridge, fetch } = makeBridge({ respond: () => ({ followup_message: "keep going" }) });
    const out = await call(bridge, "session_stop", { type: "session_stop", messages: [{ big: 1 }], turn_id: 3 });
    assert.deepEqual(out, { continue: true, additionalContext: "keep going" });
    assert.deepEqual(fetch.calls[0].body.payload, { status: "completed", loop_count: 0 });
  });

  test("session_stop: empty followup -> nothing", async () => {
    const { bridge } = makeBridge({ respond: () => ({ followup_message: "" }) });
    assert.equal(await call(bridge, "session_stop", {}), undefined);
  });

  test("before_subagent_spawn payload and deny", async () => {
    const { bridge, fetch } = makeBridge({ respond: () => ({ permission: "deny", agent_message: "nope" }) });
    const out = await call(bridge, "before_subagent_spawn", { agent: "explore", patterns: ["m1", "m2"], spawnKey: "k" });
    assert.deepEqual(out, { block: true, reason: "nope" });
    assert.deepEqual(fetch.calls[0].body.payload, {
      subagent_type: "explore", task: "", is_parallel_worker: false, subagent_model: "m1", tool_call_id: "k",
    });
  });

  test("before_subagent_spawn omp_result passes model/note", async () => {
    const { bridge } = makeBridge({
      startup: startupFile({ activeOmpEvents: ["before_subagent_spawn"] }),
      respond: (b) => (b.kind === "omp" ? { omp_result: { model: "x", note: "n" } } : {}),
    });
    assert.deepEqual(await call(bridge, "before_subagent_spawn", { agent: "a", patterns: [] }), { model: "x", note: "n" });
  });

  test("session_before_compact: preCompact numbers, observe-only, cancel from omp_result", async () => {
    const ctx = makeCtx();
    const { bridge, fetch } = makeBridge({
      startup: startupFile({ activeOmpEvents: ["session_before_compact"] }),
      respond: (b) => (b.kind === "omp" ? { omp_result: { cancel: true, other: 1 } } : { user_message: "heads up" }),
    });
    const out = await call(bridge, "session_before_compact", {
      type: "session_before_compact",
      preparation: { messagesToSummarize: [1, 2, 3], recentMessages: [4], previousSummary: "s" },
    }, ctx);
    assert.deepEqual(out, { cancel: true });
    assert.deepEqual(fetch.calls[0].body.payload, {
      trigger: "manual", context_tokens: 100, context_window_size: 1000, context_usage_percent: 10,
      messages_to_compact: 3, message_count: 4, is_first_compaction: false,
    });
    assert.deepEqual(ctx.notes, [["heads up", "warn"]]);
  });

  test("compaction without cancel returns nothing; auto_compaction_start sets trigger auto", async () => {
    const { bridge, fetch } = makeBridge();
    call(bridge, "auto_compaction_start", { reason: "threshold" });
    await bridge.idle();
    assert.equal(fetch.calls[0].body.payload.trigger, "auto");
    assert.equal(await call(bridge, "session_before_compact", { preparation: { messagesToSummarize: [], recentMessages: [] } }), undefined);
    assert.equal(fetch.calls[1].body.payload.trigger, "auto");
    assert.equal(fetch.calls[1].body.payload.is_first_compaction, true);
  });

  test("message_end: assistant text and thinking", async () => {
    const { bridge, fetch } = makeBridge();
    call(bridge, "message_end", {
      message: { role: "assistant", content: [{ type: "text", text: "a" }, { type: "thinking", thinking: "t1" }, { type: "text", text: "b" }, { type: "thinking", thinking: "t2" }] },
    });
    await bridge.idle();
    assert.deepEqual(fetch.events(), ["afterAgentResponse", "afterAgentThought"]);
    assert.equal(fetch.calls[0].body.payload.text, "ab");
    assert.equal(fetch.calls[1].body.payload.text, "t1t2");
  });

  test("message_end for user role posts nothing", async () => {
    const { bridge, fetch } = makeBridge();
    call(bridge, "message_end", { message: { role: "user", content: [{ type: "text", text: "a" }] } });
    await bridge.idle();
    assert.equal(fetch.calls.length, 0);
  });

  test("omp-only observers post kind omp when active", async () => {
    const { bridge, fetch } = makeBridge({ startup: startupFile({ activeOmpEvents: ["agent_end", "turn_end", "tool_approval_requested", "tool_approval_resolved"] }) });
    for (const n of ["agent_end", "turn_end", "tool_approval_requested", "tool_approval_resolved"]) {
      assert.equal(call(bridge, n, { type: n, toolCallId: "z", signal: new AbortController().signal }), undefined);
    }
    await bridge.idle();
    assert.deepEqual(fetch.events(), ["agent_end", "turn_end", "tool_approval_requested", "tool_approval_resolved"]);
    assert.ok(fetch.calls.every((c) => c.body.kind === "omp" && c.body.payload.signal === undefined));
  });

  test("safeCopy bounds depth, arrays and strings", () => {
    const c = safeCopy({ a: new Array(500).fill(1), s: "x".repeat(100000), f() {}, n: { n: { n: { n: { n: { n: { n: 1 } } } } } } });
    assert.equal(c.a.length, 50);
    assert.equal(c.s.length, 65536);
    assert.equal("f" in c, false);
  });
});

describe("inactive / inert", () => {
  test("skips events not in activeEvents", async () => {
    const { bridge, fetch } = makeBridge({ startup: startupFile({ activeEvents: ["beforeShellExecution"] }) });
    await call(bridge, "tool_call", { toolName: "bash", toolCallId: "c", input: { command: "x" } });
    assert.deepEqual(fetch.events(), ["beforeShellExecution"]);
    await call(bridge, "tool_result", { toolName: "bash", toolCallId: "c", input: {}, isError: false, content: [] });
    assert.deepEqual(fetch.events(), ["beforeShellExecution"]);
  });

  test("inert without URL or token: no reads, no posts", async () => {
    for (const env of [{ AUTONOGRAMMER_HOOKS_URL: "" }, { AUTONOGRAMMER_HOOKS_TOKEN: "" }]) {
      const { bridge, fetch, reads } = makeBridge({ env, startup: startupFile() });
      assert.equal(await call(bridge, "tool_call", { toolName: "bash", toolCallId: "c", input: {} }), undefined);
      assert.equal(await call(bridge, "session_shutdown", {}), undefined);
      assert.equal(fetch.calls.length, 0);
      assert.equal(reads.length, 0);
    }
  });

  test("no env and no filesystem or network access at load / without events", () => {
    const { reads, fetch } = makeBridge({ startup: startupFile() });
    assert.equal(reads.length, 0);
    assert.equal(fetch.calls.length, 0);
  });

  test("subagent contexts never post", async () => {
    const { bridge, fetch } = makeBridge();
    assert.equal(await call(bridge, "tool_call", { toolName: "bash", toolCallId: "c", input: {} }, makeCtx({ agent: { kind: "sub" } })), undefined);
    assert.equal(fetch.calls.length, 0);
  });

  test("startup file read lazily once and cached", async () => {
    const { bridge, reads } = makeBridge({ startup: startupFile() });
    await call(bridge, "tool_call", { toolName: "grep", toolCallId: "1", input: {} });
    await call(bridge, "tool_call", { toolName: "grep", toolCallId: "2", input: {} });
    assert.deepEqual(reads, ["/startup.json"]);
  });
});

describe("loopback guard", () => {
  for (const url of ["http://example.com/v1/hook", "http://10.0.0.5:80/v1/hook", "https://127.0.0.1/v1/hook", "not a url", "http://127.0.0.1.evil.com/v1/hook"]) {
    test(`refuses ${url}`, async () => {
      const { bridge, fetch } = makeBridge({ env: { AUTONOGRAMMER_HOOKS_URL: url } });
      assert.equal(await call(bridge, "tool_call", { toolName: "bash", toolCallId: "c", input: {} }), undefined);
      assert.equal(fetch.calls.length, 0);
    });
  }
  for (const url of ["http://localhost:1/v1/hook", "http://[::1]:1/v1/hook", "http://127.0.0.1:1/v1/hook"]) {
    test(`accepts ${url}`, async () => {
      const { bridge, fetch } = makeBridge({ env: { AUTONOGRAMMER_HOOKS_URL: url } });
      await call(bridge, "tool_call", { toolName: "grep", toolCallId: "c", input: {} });
      assert.equal(fetch.calls.length, 1);
    });
  }
});

describe("fail policy", () => {
  const closed = startupFile({ failClosedEvents: ["preToolUse", "beforeShellExecution", "beforeMCPExecution", "beforeReadFile", "subagentStart", "beforeSubmitPrompt", "stop"] });
  const failures = {
    unreachable: () => {
      throw new Error("ECONNREFUSED");
    },
    "500": () => ({ status: 500, text: "{}" }),
    "401": () => ({ status: 401, text: "" }),
    "bad json": () => ({ status: 200, text: "not json" }),
    "non-object json": () => ({ status: 200, text: "[1]" }),
  };

  for (const [label, respond] of Object.entries(failures)) {
    test(`fail-closed deny on ${label}`, async () => {
      const { bridge } = makeBridge({ startup: closed, respond });
      assert.deepEqual(await call(bridge, "tool_call", { toolName: "bash", toolCallId: "c", input: {} }), {
        block: true, reason: "Autonogrammer hook host unreachable; preToolUse is configured fail-closed",
      });
      assert.deepEqual(await call(bridge, "before_subagent_spawn", { agent: "a", patterns: [] }), {
        block: true, reason: "Autonogrammer hook host unreachable; subagentStart is configured fail-closed",
      });
      assert.deepEqual(await call(bridge, "input", { text: "x" }, makeCtx({ hasUI: false })), { handled: true });
    });

    test(`fail-open on ${label} when not configured fail-closed`, async () => {
      const { bridge } = makeBridge({ startup: startupFile(), respond });
      assert.equal(await call(bridge, "tool_call", { toolName: "bash", toolCallId: "c", input: {} }), undefined);
      assert.equal(await call(bridge, "before_subagent_spawn", { agent: "a", patterns: [] }), undefined);
      assert.equal(await call(bridge, "input", { text: "x" }), undefined);
    });
  }

  test("beforeShellExecution fail-closed only for that event", async () => {
    const { bridge } = makeBridge({
      startup: startupFile({ failClosedEvents: ["beforeShellExecution"] }),
      respond: (b) => {
        if (b.event === "beforeShellExecution") throw new Error("down");
        return {};
      },
    });
    assert.deepEqual(await call(bridge, "tool_call", { toolName: "bash", toolCallId: "c", input: {} }), {
      block: true, reason: "Autonogrammer hook host unreachable; beforeShellExecution is configured fail-closed",
    });
  });

  test("non-blockable events in failClosedEvents never deny (stop, post events)", async () => {
    const { bridge } = makeBridge({ startup: closed, respond: () => { throw new Error("down"); } });
    assert.equal(await call(bridge, "session_stop", {}), undefined);
    assert.equal(await call(bridge, "tool_result", { toolName: "grep", toolCallId: "c", input: {}, isError: false, content: [] }), undefined);
  });

  test("blockable event is still waiting after 6 s and aborts only at requestTimeoutMs (default 130000)", async () => {
    const { bridge, clock, fetch } = makeBridge({ startup: closed, respond: () => "hang" });
    const p = call(bridge, "tool_call", { toolName: "grep", toolCallId: "c", input: {} });
    await new Promise((r) => setImmediate(r));
    assert.equal(clock.pendingTimers().length, 1);
    assert.equal(clock.pendingTimers()[0].ms, 130000);
    clock.advance(6000);
    let settled = false;
    p.then(() => (settled = true));
    await new Promise((r) => setImmediate(r));
    assert.equal(settled, false);
    assert.equal(fetch.calls[0].init.signal.aborted, false);
    clock.fire();
    assert.deepEqual(await p, { block: true, reason: "Autonogrammer hook host unreachable; preToolUse is configured fail-closed" });
    assert.equal(fetch.calls[0].init.signal.aborted, true);
    assert.equal(clock.pendingTimers().length, 0);
  });

  test("requestTimeoutMs from startup file is used and clamped to 1000..900000", async () => {
    const ms = async (value) => {
      const { bridge, clock } = makeBridge({ startup: startupFile({ requestTimeoutMs: value }), respond: () => "hang" });
      const p = call(bridge, "tool_call", { toolName: "grep", toolCallId: "c", input: {} });
      await new Promise((r) => setImmediate(r));
      const t = clock.pendingTimers()[0].ms;
      clock.fire();
      await p;
      return t;
    };
    assert.equal(await ms(40000), 40000);
    assert.equal(await ms(10), 1000);
    assert.equal(await ms(99999999), 900000);
    assert.equal(await ms("x"), 130000);
    assert.equal(await ms(1.5), 130000);
  });

  test("timeout per event: awaited ones use requestTimeoutMs, observers keep 5 s", async () => {
    const timeouts = async (name, event, startup = startupFile({ requestTimeoutMs: 50000, activeOmpEvents: [name] })) => {
      const { bridge, clock, fetch } = makeBridge({ startup, respond: () => "hang" });
      const out = call(bridge, name, event);
      for (let i = 0; i < 20 && !clock.pendingTimers().length; i++) await new Promise((r) => setImmediate(r));
      const seen = [];
      for (let i = 0; i < 6; i++) {
        seen.push(...clock.pendingTimers().map((t) => t.ms));
        clock.fire();
        await new Promise((r) => setImmediate(r));
      }
      await out;
      await bridge.idle();
      return [...new Set(seen)];
    };
    const tr = { toolName: "grep", toolCallId: "r", input: {}, isError: false, content: [] };
    assert.deepEqual(await timeouts("input", { text: "x" }), [50000]);
    assert.deepEqual(await timeouts("session_stop", {}), [50000]);
    assert.deepEqual(await timeouts("before_subagent_spawn", { agent: "a", patterns: [] }), [50000]);
    assert.deepEqual(await timeouts("session_before_compact", { preparation: {} }), [50000]);
    assert.deepEqual(await timeouts("tool_result", tr), [50000]);
    assert.deepEqual(await timeouts("tool_result", { ...tr, isError: true }, startupFile({ requestTimeoutMs: 50000, activeEvents: ["postToolUseFailure"] })), [5000]);
    assert.deepEqual(await timeouts("tool_result", { ...tr, toolName: "bash", input: { command: "c" } }, startupFile({ requestTimeoutMs: 50000, activeEvents: ["afterShellExecution"] })), [5000]);
    assert.deepEqual(await timeouts("session_start", {}), [5000]);
    assert.deepEqual(await timeouts("message_end", { message: { role: "assistant", content: [{ type: "text", text: "a" }] } }), [5000]);
    assert.deepEqual(await timeouts("turn_end", {}), [5000]);
    assert.deepEqual(await timeouts("auto_compaction_start", {}), [5000]);
  });

  test("timer is cleared after a successful response", async () => {
    const { bridge, clock } = makeBridge();
    await call(bridge, "tool_call", { toolName: "grep", toolCallId: "c", input: {} });
    assert.equal(clock.pendingTimers().length, 0);
  });

  for (const [label, startup] of [
    ["missing", new Error("ENOENT")],
    ["malformed", "{not json"],
    ["wrong shape", "[1,2]"],
  ]) {
    test(`startup file ${label}: all events active, nothing fail-closed`, async () => {
      const { bridge, fetch } = makeBridge({ startup, respond: () => { throw new Error("down"); } });
      assert.equal(await call(bridge, "tool_call", { toolName: "bash", toolCallId: "c", input: {} }), undefined);
      assert.deepEqual(fetch.events(), ["preToolUse", "beforeShellExecution"]);
    });
  }

  test("unset startup env var behaves like a missing file", async () => {
    const { bridge, fetch } = makeBridge();
    await call(bridge, "tool_call", { toolName: "bash", toolCallId: "c", input: {} });
    assert.equal(fetch.calls.length, 2);
  });
});

describe("no leaks", () => {
  test("nothing is written to console and the token never leaves the Authorization header", async () => {
    const written = [];
    const methods = ["log", "info", "warn", "error", "debug"];
    const originals = methods.map((m) => console[m]);
    for (const m of methods) console[m] = (...a) => written.push(a.join(" "));
    const stdout = process.stdout.write;
    const stderr = process.stderr.write;
    process.stdout.write = () => true;
    process.stderr.write = (c) => {
      written.push(String(c));
      return true;
    };
    try {
      const { bridge, fetch } = makeBridge({
        startup: startupFile({ failClosedEvents: ["preToolUse"], activeOmpEvents: ["tool_call"] }),
        respond: () => {
          throw new Error(`connect failed with PAYLOAD-SECRET-TEXT`);
        },
      });
      const out = await call(bridge, "tool_call", { toolName: "bash", toolCallId: "c", input: { command: "PAYLOAD-SECRET-TEXT" } });
      await call(bridge, "input", { text: "PAYLOAD-SECRET-TEXT" });
      assert.equal(out.block, true);
      assert.ok(!JSON.stringify(out).includes(TOKEN));
      assert.ok(!JSON.stringify(out).includes("PAYLOAD-SECRET-TEXT"));
      for (const c of fetch.calls) {
        assert.ok(!c.init.body.includes(TOKEN));
        assert.equal(c.init.headers.Authorization, `Bearer ${TOKEN}`);
      }
    } finally {
      methods.forEach((m, i) => (console[m] = originals[i]));
      process.stdout.write = stdout;
      process.stderr.write = stderr;
    }
    assert.deepEqual(written, []);
  });

  test("handlers never throw even for hostile events", async () => {
    const { bridge } = makeBridge();
    for (const name of bridge.eventNames) {
      await assert.doesNotReject(async () => bridge.handle(name, null, null));
      await assert.doesNotReject(async () => bridge.handle(name, { message: 5, input: 5, content: 5 }, {}));
    }
    await bridge.idle();
  });
});
