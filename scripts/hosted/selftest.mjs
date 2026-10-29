#!/usr/bin/env node
// Self-test of the hosted end-to-end harness: the SSE reader and the scripted fake model server are exercised over
// loopback with fetch. It needs neither omp nor the API nor the .NET build, and it is not wired into CI.
//
//   node scripts/hosted/selftest.mjs

import assert from "node:assert/strict";
import { messageText, parseScenario, planReply, startFakeModel, textChunks } from "./fake-model-server.mjs";
import { parseSse } from "./sse.mjs";

const results = [];
async function test(name, fn) {
  try {
    await fn();
    results.push({ name, ok: true });
    process.stdout.write(`PASS ${name}\n`);
  } catch (error) {
    results.push({ name, ok: false });
    process.stdout.write(`FAIL ${name}\n  ${String(error?.stack ?? error).split("\n").join("\n  ")}\n`);
  }
}

const bytes = (text) => new TextEncoder().encode(text);
async function* chunked(...parts) {
  for (const part of parts) yield typeof part === "string" ? bytes(part) : part;
}
async function collect(iterable) {
  const out = [];
  for await (const item of iterable) out.push(item);
  return out;
}

// ---- SSE reader -----------------------------------------------------------------------------------------------

await test("sse: id, event and data fields; blank line dispatches", async () => {
  const events = await collect(parseSse(chunked("id: 7\nevent: text_delta\ndata: {\"a\":1}\n\nid: 8\ndata: x\n\n")));
  assert.deepEqual(events, [
    { id: "7", event: "text_delta", data: '{"a":1}', retry: null },
    { id: "8", event: "message", data: "x", retry: null },
  ]);
});

await test("sse: CRLF, lone CR, comments, multi-line data and retry", async () => {
  const events = await collect(parseSse(chunked(": keep-alive\r\nretry: 15000\r\nid: 1\r\ndata: a\r\ndata: b\r\n\r\ndata: c\r\r")));
  assert.deepEqual(events, [
    { id: "1", event: "message", data: "a\nb", retry: 15000 },
    { id: null, event: "message", data: "c", retry: null },
  ]);
});

await test("sse: a CRLF split across chunks and a multi-byte character split across chunks", async () => {
  const whole = bytes("id: 3\r\ndata: h\u00e9llo\r\n\r\n");
  const cut = whole.indexOf(0xc3) + 1;
  const events = await collect(parseSse(chunked(whole.slice(0, cut), whole.slice(cut, cut + 3), whole.slice(cut + 3))));
  assert.deepEqual(events, [{ id: "3", event: "message", data: "h\u00e9llo", retry: null }]);
  const split = await collect(parseSse(chunked("id: 4\r", "\ndata: z\r", "\n\r", "\n")));
  assert.deepEqual(split, [{ id: "4", event: "message", data: "z", retry: null }]);
});

await test("sse: an event still open when the body ends is dropped", async () => {
  const events = await collect(parseSse(chunked("id: 1\ndata: done\n\nid: 2\ndata: open")));
  assert.deepEqual(events.map((event) => event.id), ["1"]);
});

// ---- scenario DSL ---------------------------------------------------------------------------------------------

await test("dsl: every directive and the default", () => {
  assert.deepEqual(parseScenario("hello"), { delayMs: 0, kind: "echo" });
  assert.deepEqual(parseScenario("@say  hi there "), { delayMs: 0, kind: "say", text: "hi there" });
  assert.deepEqual(parseScenario('@tool bash {"command": "echo hi"}'), { delayMs: 0, kind: "tool", name: "bash", args: '{"command":"echo hi"}' });
  assert.deepEqual(parseScenario("@tool read"), { delayMs: 0, kind: "tool", name: "read", args: "{}" });
  assert.deepEqual(parseScenario("@error 503"), { delayMs: 0, kind: "error", status: 503 });
  assert.deepEqual(parseScenario("@slow 25\n@say hi"), { delayMs: 25, kind: "say", text: "hi" });
  assert.deepEqual(parseScenario("preface\n@say second line"), { delayMs: 0, kind: "say", text: "second line" });
  assert.equal(parseScenario("@say a\n@say b").text, "a", "the first non-@slow directive wins");
});

await test("dsl: invalid directives are reported, never thrown", () => {
  for (const text of ["@error 200", "@error nope", "@slow -1", "@slow 99999999", "@tool", "@tool bash not-json", "@tool bash [1]"]) {
    assert.equal(parseScenario(text).kind, "invalid", text);
  }
});

await test("planReply: content arrays, the last user message wins, and a tool result ends the turn", () => {
  assert.equal(messageText({ content: [{ type: "text", text: "a" }, { type: "text", text: "b" }] }), "ab");
  const plan = planReply({ messages: [{ role: "user", content: "@say old" }, { role: "assistant", content: "old" }, { role: "user", content: [{ type: "text", text: "again" }] }] });
  assert.equal(plan.text, "You said: again");
  const done = planReply({
    tools: [{ type: "function", function: { name: "bash" } }],
    messages: [{ role: "user", content: '@tool bash {"command":"x"}' }, { role: "assistant", content: null, tool_calls: [] }, { role: "tool", tool_call_id: "c", content: "12345" }],
  });
  assert.equal(done.text, "done: 5");
  assert.deepEqual(textChunks("a b  c").join(""), "a b  c");
});

await test("planReply: the system reminder omp puts before the prompt is not part of what the user said", () => {
  const reminder = "<system-reminder>\nToday: 2026-10-29; current working directory: '/tmp/work'. Do not repeat this information in your reply.\n</system-reminder>";
  const say = planReply({ messages: [{ role: "user", content: `${reminder}@say hello there` }] });
  assert.deepEqual([say.scenario, say.text], ["say", "hello there"]);
  const tool = planReply({ tools: [{ type: "function", function: { name: "bash" } }], messages: [{ role: "user", content: `${reminder}@tool bash {"command":"echo x"}` }] });
  assert.deepEqual([tool.type, tool.name], ["tool", "bash"]);
  assert.equal(planReply({ messages: [{ role: "user", content: `${reminder}plain words` }] }).text, "You said: plain words");
});

// ---- the server over HTTP -------------------------------------------------------------------------------------

const fake = await startFakeModel({ port: 0 });
const post = (body, headers = {}) =>
  fetch(`${fake.baseUrl}/chat/completions`, { method: "POST", headers: { "content-type": "application/json", ...headers }, body: JSON.stringify(body) });
const user = (content) => ({ model: "fake-model", stream: true, messages: [{ role: "user", content }] });

async function streamOf(response) {
  const chunks = [];
  let done = false;
  for await (const event of parseSse(response.body)) {
    if (event.data === "[DONE]") {
      done = true;
      continue;
    }
    assert.equal(done, false, "nothing follows [DONE]");
    chunks.push(JSON.parse(event.data));
  }
  assert.equal(done, true, "the stream ends with [DONE]");
  return chunks;
}

const textOf = (chunks) => chunks.map((chunk) => chunk.choices[0]?.delta?.content ?? "").join("");

try {
  await test("server: models, health and unknown routes", async () => {
    const models = await (await fetch(`${fake.baseUrl}/models`)).json();
    assert.equal(models.object, "list");
    assert.deepEqual(models.data.map((model) => model.id), ["fake-model"]);
    assert.equal((await fetch(`${fake.url}/healthz`)).status, 200);
    const missing = await fetch(`${fake.url}/v2/nothing`);
    assert.equal(missing.status, 404);
    assert.equal((await missing.json()).error.type, "not_found_error");
    assert.equal((await fetch(`${fake.baseUrl}/chat/completions`)).status, 405);
  });

  await test("server: default echo streams word chunks, a stop finish and usage in the last chunk", async () => {
    const response = await post(user("Hello from the harness"), { authorization: "Bearer dummy" });
    assert.equal(response.status, 200);
    assert.match(response.headers.get("content-type"), /^text\/event-stream/);
    const chunks = await streamOf(response);
    assert.equal(chunks[0].choices[0].delta.role, "assistant");
    assert.equal(textOf(chunks), "You said: Hello from the harness");
    assert.ok(chunks.filter((chunk) => (chunk.choices[0]?.delta?.content ?? "") !== "").length > 3, "several content chunks");
    const finish = chunks.filter((chunk) => chunk.choices[0]?.finish_reason);
    assert.equal(finish.length, 1);
    assert.equal(finish[0].choices[0].finish_reason, "stop");
    const last = chunks.at(-1);
    assert.deepEqual(last.choices, []);
    assert.ok(last.usage.prompt_tokens > 0 && last.usage.completion_tokens > 0);
    assert.equal(last.usage.total_tokens, last.usage.prompt_tokens + last.usage.completion_tokens);
    assert.equal(fake.requests.at(-1).hasAuthorization, true);
    assert.equal(fake.requests.at(-1).scenario, "echo");
  });

  await test("server: @say and @slow", async () => {
    assert.equal(textOf(await streamOf(await post(user("@say one two three")))), "one two three");
    assert.equal(textOf(await streamOf(await post(user("@slow 5\n@say slow ok")))), "slow ok");
    assert.equal(textOf(await streamOf(await post(user("@slow 5")))), "You said: @slow 5");
  });

  await test("server: @tool streams a call with fragmented arguments, then answers done: <length> after the result", async () => {
    const tools = [{ type: "function", function: { name: "bash", description: "run", parameters: { type: "object" } } }];
    const args = '{"command":"echo autonogrammer-e2e-0123456789"}';
    const first = await streamOf(await post({ ...user(`@tool bash ${args}`), tools }));
    const calls = first.flatMap((chunk) => chunk.choices[0]?.delta?.tool_calls ?? []);
    assert.ok(calls.length > 1, "arguments arrive in fragments");
    assert.equal(calls[0].function.name, "bash");
    assert.equal(calls[0].index, 0);
    assert.match(calls[0].id, /^call_fake_/);
    assert.equal(calls.map((call) => call.function.arguments).join(""), args);
    assert.equal(first.filter((chunk) => chunk.choices[0]?.finish_reason).at(0).choices[0].finish_reason, "tool_calls");
    assert.equal(textOf(first), "");

    const result = "autonogrammer-e2e-0123456789\n";
    const second = await streamOf(
      await post({
        ...user(`@tool bash ${args}`),
        tools,
        messages: [
          { role: "user", content: `@tool bash ${args}` },
          { role: "assistant", content: null, tool_calls: [{ id: calls[0].id, type: "function", function: { name: "bash", arguments: args } }] },
          { role: "tool", tool_call_id: calls[0].id, content: result },
        ],
      }),
    );
    assert.equal(textOf(second), `done: ${result.length}`);
    assert.equal(second.flatMap((chunk) => chunk.choices[0]?.delta?.tool_calls ?? []).length, 0);
  });

  await test("server: @tool without that tool offered answers with text, so title requests do not break", async () => {
    const chunks = await streamOf(await post(user('@tool bash {"command":"x"}')));
    assert.match(textOf(chunks), /tool "bash" is not offered/);
  });

  await test("server: non-streaming fallback for text and for a tool call", async () => {
    const text = await (await post({ ...user("plain"), stream: false })).json();
    assert.equal(text.object, "chat.completion");
    assert.equal(text.choices[0].message.content, "You said: plain");
    assert.equal(text.choices[0].finish_reason, "stop");
    assert.ok(text.usage.total_tokens > 0);
    const tool = await (await post({ ...user('@tool read {"path":"a"}'), stream: false, tools: [{ type: "function", function: { name: "read" } }] })).json();
    assert.equal(tool.choices[0].finish_reason, "tool_calls");
    assert.equal(tool.choices[0].message.tool_calls[0].function.name, "read");
    assert.equal(tool.choices[0].message.tool_calls[0].function.arguments, '{"path":"a"}');
  });

  await test("server: @error answers the status with an OpenAI-style error body; bad input answers 400", async () => {
    const failed = await post(user("@error 503"));
    assert.equal(failed.status, 503);
    const body = await failed.json();
    assert.equal(body.error.type, "server_error");
    assert.match(body.error.message, /503/);
    assert.equal((await post(user("@error 429"))).status, 429);
    const badJson = await fetch(`${fake.baseUrl}/chat/completions`, { method: "POST", body: "{nope" });
    assert.equal(badJson.status, 400);
    assert.equal((await post({ model: "m" })).status, 400);
  });

  await test("server: an invalid directive replies with a readable text", async () => {
    assert.match(textOf(await streamOf(await post(user("@error 200")))), /@error needs a status 400\.\.599/);
  });

  await test("server: the reply is a pure function of the request (byte-identical, no randomness)", async () => {
    const body = user("determinism check");
    const [a, b] = await Promise.all([post(body).then((r) => r.text()), post(body).then((r) => r.text())]);
    assert.equal(a, b);
    assert.match(a, /chatcmpl-fake-[0-9a-f]{24}/);
  });

  await test("server: the request log never holds a credential", async () => {
    await post(user("secret check"), { authorization: "Bearer super-secret-value" });
    assert.equal(JSON.stringify(fake.requests).includes("super-secret-value"), false);
  });
} finally {
  await fake.close();
}

const failed = results.filter((result) => !result.ok);
process.stdout.write(`\n${results.length - failed.length}/${results.length} passed\n`);
process.exit(failed.length === 0 ? 0 : 1);
