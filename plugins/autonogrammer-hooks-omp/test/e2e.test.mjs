import assert from "node:assert/strict";
import { createServer } from "node:http";
import { after, before, describe, test } from "node:test";
import { createHookBridge } from "../src/bridge.mjs";

describe("loopback end to end", () => {
  const seen = [];
  let server;
  let url;
  let mode = "ok";

  before(async () => {
    server = createServer((req, res) => {
      let body = "";
      req.on("data", (c) => (body += c));
      req.on("end", () => {
        seen.push({ method: req.method, url: req.url, auth: req.headers.authorization, length: req.headers["content-length"], body });
        if (mode === "deny") {
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ permission: "deny", agent_message: "blocked by host" }));
        } else if (mode === "500") {
          res.writeHead(500);
          res.end("{}");
        } else {
          res.writeHead(200, { "Content-Type": "application/json" });
          res.end(JSON.stringify({ additional_context: "from host" }));
        }
      });
    });
    await new Promise((r) => server.listen(0, "127.0.0.1", r));
    url = `http://127.0.0.1:${server.address().port}/v1/hook`;
  });
  after(() => new Promise((r) => server.close(r)));

  const ctx = { cwd: "/w", agent: { kind: "main" }, sessionManager: { getSessionId: () => "s" } };
  const mk = (env = {}) => createHookBridge({ env: { AUTONOGRAMMER_HOOKS_URL: url, AUTONOGRAMMER_HOOKS_TOKEN: "real-token", ...env } });

  test("real fetch POSTs JSON with bearer token and parses the decision", async () => {
    mode = "ok";
    seen.length = 0;
    const out = await mk().handle("tool_call", { toolName: "grep", toolCallId: "c1", input: { q: 1 } }, ctx);
    assert.deepEqual(out, { additionalContext: "from host" });
    assert.equal(seen.length, 1);
    assert.equal(seen[0].method, "POST");
    assert.equal(seen[0].url, "/v1/hook");
    assert.equal(seen[0].auth, "Bearer real-token");
    assert.equal(Number(seen[0].length), Buffer.byteLength(seen[0].body));
    const body = JSON.parse(seen[0].body);
    assert.equal(body.event, "preToolUse");
    assert.equal(body.kind, "cursor");
    assert.equal(body.sessionId, "s");
  });

  test("deny and fail-closed over real HTTP", async () => {
    mode = "deny";
    assert.deepEqual(await mk().handle("tool_call", { toolName: "grep", toolCallId: "c", input: {} }, ctx), {
      block: true, reason: "blocked by host",
    });
    mode = "500";
    const files = JSON.stringify({ failClosedEvents: ["preToolUse"], activeEvents: ["preToolUse"], activeOmpEvents: [] });
    const bridge = createHookBridge({
      env: { AUTONOGRAMMER_HOOKS_URL: url, AUTONOGRAMMER_HOOKS_TOKEN: "t", AUTONOGRAMMER_HOOKS_STARTUP_FILE: "/s" },
      readFile: () => files,
    });
    const out = await bridge.handle("tool_call", { toolName: "grep", toolCallId: "c", input: {} }, ctx);
    assert.equal(out.block, true);
  });
});
