import assert from "node:assert/strict";
import * as realFs from "node:fs";
import { mkdtempSync, rmSync, statSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { after, describe, test } from "node:test";
import { REWRITE_GAP_MS, buildConnectionReport, createConnectionReporter, writeConnectionReport } from "../src/connection-report.mjs";

const temps = [];
after(() => {
  for (const dir of temps) rmSync(dir, { recursive: true, force: true });
});

/** Manual clock: one-shot timers fire only inside `advance`. */
function fakeClock(start = Date.parse("2026-10-26T10:00:00.000Z")) {
  let now = start;
  let nextId = 1;
  const timers = new Map();
  return {
    now: () => now,
    setTimeout: (fn, ms) => {
      const id = nextId++;
      timers.set(id, { fn, due: now + ms });
      return id;
    },
    clearTimeout: (id) => void timers.delete(id),
    timerCount: () => timers.size,
    advance(ms) {
      now += ms;
      for (const [id, timer] of [...timers]) {
        if (timer.due <= now) {
          timers.delete(id);
          timer.fn();
        }
      }
    },
  };
}

function recordingFs() {
  const calls = [];
  const wrapped = {};
  for (const [name, value] of Object.entries(realFs)) {
    wrapped[name] =
      typeof value === "function"
        ? (...args) => {
            calls.push({ op: name, args });
            return value(...args);
          }
        : value;
  }
  return { fs: wrapped, calls };
}

function fakePi({ tools = ["read", "bash"], commands = ["help"], withCommands = true } = {}) {
  const state = { tools: tools.map((name) => ({ name })), commands: commands.map((name) => ({ name })) };
  const pi = { getAllTools: () => state.tools };
  if (withCommands) pi.getCommands = () => state.commands;
  return { pi, state };
}

function setup(options = {}) {
  const root = mkdtempSync(join(tmpdir(), "autonogrammer-conn-"));
  temps.push(root);
  const file = join(root, "connection.json");
  const clock = fakeClock();
  const { fs, calls } = recordingFs();
  const { pi, state } = fakePi(options);
  const read = () => JSON.parse(realFs.readFileSync(file, "utf8"));
  return { root, file, clock, fs, calls, pi, state, read };
}

describe("connection report content", () => {
  test("lists sorted raw tool and command names with mode, pid and version", () => {
    const w = setup({ tools: ["write", "bash", "read", "bash"], commands: ["zeta", "alpha"] });
    assert.equal(writeConnectionReport({ fs: w.fs, clock: w.clock, file: w.file, pi: w.pi, mode: "rpc", version: "18.12.5" }), true);
    assert.deepEqual(w.read(), {
      v: 1,
      at: "2026-10-26T10:00:00.000Z",
      pid: process.pid,
      mode: "rpc",
      omp: { version: "18.12.5" },
      tools: ["bash", "read", "write"],
      commands: ["alpha", "zeta"],
      commandsAvailable: true,
    });
  });

  test("commandsAvailable is false and commands empty when the API has no command lister", () => {
    const w = setup({ withCommands: false });
    writeConnectionReport({ fs: w.fs, clock: w.clock, file: w.file, pi: w.pi, mode: null, version: null });
    const report = w.read();
    assert.equal(report.commandsAvailable, false);
    assert.deepEqual(report.commands, []);
    assert.equal(report.mode, null);
    assert.equal(report.omp.version, null);
  });

  test("a throwing tool or command lister degrades to empty lists", () => {
    const pi = {
      getAllTools: () => {
        throw new Error("no tools yet");
      },
      getCommands: () => {
        throw new Error("no commands yet");
      },
    };
    const body = buildConnectionReport({ clock: fakeClock(), pi, mode: "tui", version: null });
    assert.deepEqual(body.tools, []);
    assert.deepEqual(body.commands, []);
    assert.equal(body.commandsAvailable, false);
  });

  test("the write is atomic (tmp then rename) with mode 0600", () => {
    const w = setup();
    writeConnectionReport({ fs: w.fs, clock: w.clock, file: w.file, pi: w.pi, mode: null, version: null });
    const writes = w.calls.filter((c) => c.op === "writeFileSync");
    assert.equal(writes.length, 1);
    assert.match(String(writes[0].args[0]), /\.tmp$/u);
    assert.equal(writes[0].args[2].mode, 0o600);
    const renames = w.calls.filter((c) => c.op === "renameSync");
    assert.equal(renames.length, 1);
    assert.equal(renames[0].args[1], w.file);
  });

  test("creates a missing parent directory (mode 0700) before the atomic write", () => {
    const w = setup();
    const file = join(w.root, "connection", "nested", "connection.json");
    assert.equal(writeConnectionReport({ fs: w.fs, clock: w.clock, file, pi: w.pi, mode: null, version: null }), true);
    const report = JSON.parse(realFs.readFileSync(file, "utf8"));
    assert.deepEqual(report.tools, ["bash", "read"]);
    assert.equal(statSync(join(w.root, "connection", "nested")).mode & 0o777, 0o700);
    assert.equal(statSync(file).mode & 0o777, 0o600);
  });

  test("a reporter whose directory is missing still produces the report on its first notify", () => {
    const w = setup();
    const file = join(w.root, "connection", "connection.json");
    const reporter = createConnectionReporter({ fs: w.fs, clock: w.clock, file, pi: w.pi, version: "1.0.0" });
    reporter.notify();
    assert.equal(JSON.parse(realFs.readFileSync(file, "utf8")).omp.version, "1.0.0");
    reporter.stop();
  });

  test("never throws when the fs fails", () => {
    const w = setup();
    const broken = new Proxy(realFs, {
      get() {
        return () => {
          throw new Error("disk gone");
        };
      },
    });
    assert.equal(writeConnectionReport({ fs: broken, clock: w.clock, file: w.file, pi: w.pi, mode: null, version: null }), false);
    const reporter = createConnectionReporter({ fs: broken, clock: w.clock, file: w.file, pi: w.pi, version: null });
    assert.doesNotThrow(() => reporter.notify());
    assert.doesNotThrow(() => reporter.stop());
  });
});

describe("connection reporter", () => {
  test("inactive without an absolute file: no fs call and no timer", () => {
    for (const file of [undefined, "", "relative.json"]) {
      const w = setup();
      const reporter = createConnectionReporter({ fs: w.fs, clock: w.clock, file, pi: w.pi, version: null });
      reporter.notify();
      reporter.stop();
      assert.deepEqual(w.calls, []);
      assert.equal(w.clock.timerCount(), 0);
    }
  });

  test("the first notify writes; an unchanged list is not rewritten", () => {
    const w = setup();
    const reporter = createConnectionReporter({ fs: w.fs, clock: w.clock, file: w.file, pi: w.pi, getMode: () => "tui", version: null });
    reporter.notify();
    assert.equal(w.read().mode, "tui");
    w.calls.length = 0;
    w.clock.advance(REWRITE_GAP_MS * 3);
    reporter.notify();
    assert.deepEqual(w.calls, []);
  });

  test("a changed tool list is rewritten after the 2 s gap, not before", () => {
    const w = setup();
    const reporter = createConnectionReporter({ fs: w.fs, clock: w.clock, file: w.file, pi: w.pi, version: null });
    reporter.notify();
    w.state.tools = [...w.state.tools, { name: "mcp_late_tool" }];
    w.clock.advance(1000);
    reporter.notify();
    assert.deepEqual(w.read().tools, ["bash", "read"]);
    assert.equal(w.clock.timerCount(), 1);
    w.clock.advance(1000);
    assert.deepEqual(w.read().tools, ["bash", "mcp_late_tool", "read"]);
    assert.equal(w.read().at, "2026-10-26T10:00:02.000Z");
    assert.equal(w.clock.timerCount(), 0);
  });

  test("a change after the gap is rewritten immediately", () => {
    const w = setup();
    const reporter = createConnectionReporter({ fs: w.fs, clock: w.clock, file: w.file, pi: w.pi, version: null });
    reporter.notify();
    w.clock.advance(REWRITE_GAP_MS);
    w.state.commands = [...w.state.commands, { name: "swarm" }];
    reporter.notify();
    assert.deepEqual(w.read().commands, ["help", "swarm"]);
  });

  test("stop cancels the pending rewrite", () => {
    const w = setup();
    const reporter = createConnectionReporter({ fs: w.fs, clock: w.clock, file: w.file, pi: w.pi, version: null });
    reporter.notify();
    w.state.tools = [];
    reporter.notify();
    assert.equal(w.clock.timerCount(), 1);
    reporter.stop();
    assert.equal(w.clock.timerCount(), 0);
    w.clock.advance(REWRITE_GAP_MS * 2);
    assert.deepEqual(w.read().tools, ["bash", "read"]);
    reporter.notify();
    assert.deepEqual(w.read().tools, ["bash", "read"]);
  });
});
