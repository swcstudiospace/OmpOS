import assert from "node:assert/strict";
import * as realFs from "node:fs";
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { after, describe, test } from "node:test";
import { HEARTBEAT_MS, MAX_FILES_PER_PASS, POLL_MS, createSteerInbox } from "../src/steer-inbox.mjs";

const temps = [];
after(() => {
  for (const dir of temps) rmSync(dir, { recursive: true, force: true });
});

/** A manual clock: timers only fire inside `advance`. */
export function fakeClock(start = Date.parse("2026-10-26T10:00:00.000Z")) {
  let now = start;
  let nextId = 1;
  const timers = new Map();
  return {
    now: () => now,
    setInterval: (fn, ms) => {
      const id = nextId++;
      timers.set(id, { fn, ms, due: now + ms, repeat: true });
      return id;
    },
    clearInterval: (id) => void timers.delete(id),
    setTimeout: (fn, ms) => {
      const id = nextId++;
      timers.set(id, { fn, ms, due: now + ms, repeat: false });
      return id;
    },
    clearTimeout: (id) => void timers.delete(id),
    timerCount: () => timers.size,
    advance(ms) {
      const target = now + ms;
      for (;;) {
        let next = null;
        for (const [id, timer] of timers) if (timer.due <= target && (next === null || timer.due < next.timer.due)) next = { id, timer };
        if (next === null) break;
        now = Math.max(now, next.timer.due);
        if (next.timer.repeat) next.timer.due += next.timer.ms;
        else timers.delete(next.id);
        next.timer.fn();
      }
      now = target;
    },
  };
}

/** Real fs on a temp dir behind a recorder, so tests can assert the exact write protocol. */
export function recordingFs() {
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

function world(options = {}) {
  const root = mkdtempSync(join(tmpdir(), "autonogrammer-steer-"));
  temps.push(root);
  const dir = join(root, "steer");
  const clock = fakeClock();
  const { fs, calls } = recordingFs();
  const delivered = [];
  const deliver = options.deliver ?? ((text, mode) => {
    delivered.push({ text, mode });
    return { idle: true };
  });
  const inbox = createSteerInbox({ fs, clock, deliver, dir: "dir" in options ? options.dir : dir, getMode: () => "rpc" });
  const put = (name, body) => realFs.writeFileSync(join(dir, "inbox", name), typeof body === "string" ? body : JSON.stringify(body));
  const directive = (id, extra = {}) => ({ v: 1, id, mode: "steer", text: `text ${id}`, createdAt: "2026-10-26T09:59:00Z", source: "autonogrammer", ...extra });
  const ack = (id) => JSON.parse(realFs.readFileSync(join(dir, "acks", `${id}.json`), "utf8"));
  const exists = (...parts) => realFs.existsSync(join(dir, ...parts));
  return { root, dir, clock, fs, calls, delivered, inbox, put, directive, ack, exists };
}

describe("steering inbox delivery", () => {
  for (const mode of ["steer", "followUp", "aside"]) {
    test(`mode ${mode} reaches deliver and writes a delivered ack`, () => {
      const w = world();
      w.inbox.start();
      w.put("a1.json", w.directive("a1", { mode, text: "hello ünï" }));
      w.inbox.poll();
      assert.deepEqual(w.delivered, [{ text: "hello ünï", mode }]);
      assert.deepEqual(w.ack("a1"), { v: 1, id: "a1", status: "delivered", reason: null, mode, at: "2026-10-26T10:00:00.000Z", idle: true });
      assert.equal(w.exists("inbox", "a1.json"), false);
      assert.equal(w.exists("done", "a1.json"), true);
      w.inbox.stop();
    });
  }

  const rejected = [
    ["invalid-json", "a1.json", "{not json", null],
    ["invalid-schema", "a1.json", (w) => w.directive("a1", { v: 2 }), "steer"],
    ["invalid-schema", "a1.json", (w) => w.directive("a1", { source: "other" }), "steer"],
    ["invalid-schema", "a1.json", (w) => w.directive("a1", { createdAt: "yesterday" }), "steer"],
    ["invalid-schema", "a1.json", (w) => w.directive("a1", { mode: "interrupt" }), null],
    ["invalid-schema", "a1.json", (w) => w.directive("a1", { text: 5 }), "steer"],
    ["invalid-schema", "a1.json", "[1,2]", null],
    ["bad-id", "a1.json", (w) => w.directive("other"), "steer"],
    ["bad-id", "a1.json", (w) => w.directive("../x"), "steer"],
    ["blank-text", "a1.json", (w) => w.directive("a1", { text: " \n\t " }), "steer"],
    ["too-large", "a1.json", (w) => w.directive("a1", { text: "x".repeat(8193) }), "steer"],
    ["too-large", "a1.json", (w) => w.directive("a1", { text: "é".repeat(4097) }), "steer"],
    ["too-large", "a1.json", (w) => w.directive("a1", { text: "x".repeat(17000) }), null],
  ];
  for (const [reason, name, body, mode] of rejected) {
    test(`rejects ${reason} (${typeof body === "string" ? body.slice(0, 12) : "schema case"})`, () => {
      const w = world();
      w.inbox.start();
      w.put(name, typeof body === "function" ? body(w) : body);
      w.inbox.poll();
      assert.deepEqual(w.delivered, []);
      const a = w.ack("a1");
      assert.equal(a.status, "rejected");
      assert.equal(a.reason, reason);
      assert.equal(a.mode, mode);
      assert.equal(a.idle, null);
      assert.equal(w.exists("inbox", "a1.json"), false);
      w.inbox.stop();
    });
  }

  test("a duplicate id is never delivered twice and the first ack survives", () => {
    const w = world();
    w.inbox.start();
    w.put("a1.json", w.directive("a1"));
    w.inbox.poll();
    w.put("a1.json", w.directive("a1", { text: "again" }));
    w.inbox.poll();
    assert.equal(w.delivered.length, 1);
    assert.equal(w.ack("a1").status, "delivered");
    assert.equal(w.exists("inbox", "a1.json"), false);
  });

  test("a duplicate of a done directive without an ack is acked rejected/duplicate", () => {
    const w = world();
    w.inbox.start();
    realFs.writeFileSync(join(w.dir, "done", "a1.json"), "{}");
    w.put("a1.json", w.directive("a1"));
    w.inbox.poll();
    assert.deepEqual(w.delivered, []);
    const a = w.ack("a1");
    assert.equal(a.status, "rejected");
    assert.equal(a.reason, "duplicate");
  });

  test("a file name that cannot carry an ack is quarantined without an ack", () => {
    const w = world();
    w.inbox.start();
    w.put("bad name.json", w.directive("a1"));
    w.inbox.poll();
    assert.deepEqual(w.delivered, []);
    assert.equal(w.exists("inbox", "bad name.json"), false);
    assert.deepEqual(realFs.readdirSync(join(w.dir, "acks")), []);
  });

  test("ignores *.tmp files and symlinks", () => {
    const w = world();
    w.inbox.start();
    w.put("a1.json.tmp", w.directive("a1"));
    w.put("real.json", w.directive("real"));
    realFs.symlinkSync(join(w.dir, "inbox", "real.json"), join(w.dir, "inbox", "link.json"));
    realFs.symlinkSync("/etc/hostname", join(w.dir, "inbox", "etc.json"));
    w.inbox.poll();
    assert.deepEqual(w.delivered.map((d) => d.text), ["text real"]);
    assert.equal(w.exists("inbox", "a1.json.tmp"), true);
    assert.equal(realFs.lstatSync(join(w.dir, "inbox", "link.json")).isSymbolicLink(), true);
    assert.equal(w.exists("acks", "link.json"), false);
    assert.equal(w.exists("acks", "etc.json"), false);
  });

  test("delivers in file-name order", () => {
    const w = world();
    w.inbox.start();
    for (const id of ["c3", "a1", "b2", "a10"]) w.put(`${id}.json`, w.directive(id));
    w.inbox.poll();
    assert.deepEqual(w.delivered.map((d) => d.text), ["text a1", "text a10", "text b2", "text c3"]);
  });

  test("examines at most 64 files per pass; the rest wait for the next pass", () => {
    const w = world();
    w.inbox.start();
    for (let i = 0; i < 70; i += 1) {
      const id = `m${String(i).padStart(3, "0")}`;
      w.put(`${id}.json`, w.directive(id));
    }
    w.inbox.poll();
    assert.equal(w.delivered.length, MAX_FILES_PER_PASS);
    assert.equal(realFs.readdirSync(join(w.dir, "inbox")).length, 6);
    w.clock.advance(POLL_MS);
    assert.equal(w.delivered.length, 70);
  });

  test("acks and the heartbeat are atomic: only .tmp files are written, then renamed over the final name", () => {
    const w = world();
    w.inbox.start();
    w.put("a1.json", w.directive("a1"));
    w.inbox.poll();
    const writes = w.calls.filter((c) => c.op === "writeFileSync");
    assert.ok(writes.length >= 2);
    for (const write of writes) {
      assert.match(String(write.args[0]), /\.tmp$/u);
      assert.equal(write.args[2].mode, 0o600);
    }
    const renames = w.calls.filter((c) => c.op === "renameSync").map((c) => String(c.args[1]));
    assert.ok(renames.includes(join(w.dir, "acks", "a1.json")));
    assert.ok(renames.includes(join(w.dir, "alive.json")));
    const mkdirs = w.calls.filter((c) => c.op === "mkdirSync");
    assert.ok(mkdirs.length >= 4);
    for (const mk of mkdirs) assert.equal(mk.args[1].mode, 0o700);
  });

  test("a throwing deliver becomes a failed ack and the inbox keeps working", () => {
    let calls = 0;
    const delivered = [];
    const w = world({
      deliver: (text) => {
        calls += 1;
        if (calls === 1) throw new Error("boom");
        delivered.push(text);
      },
    });
    w.inbox.start();
    w.put("a1.json", w.directive("a1"));
    w.put("a2.json", w.directive("a2"));
    w.inbox.poll();
    const failed = w.ack("a1");
    assert.equal(failed.status, "failed");
    assert.equal(failed.reason, "deliver-error");
    assert.equal(w.ack("a2").status, "delivered");
    assert.equal(w.ack("a2").idle, null);
    assert.deepEqual(delivered, ["text a2"]);
  });

  test("a failing fs never throws out of start, poll or stop", () => {
    const w = world();
    const broken = new Proxy(realFs, {
      get() {
        return () => {
          throw new Error("disk gone");
        };
      },
    });
    const inbox = createSteerInbox({ fs: broken, clock: w.clock, deliver: () => {}, dir: w.dir });
    assert.doesNotThrow(() => inbox.start());
    assert.doesNotThrow(() => inbox.poll());
    assert.doesNotThrow(() => w.clock.advance(HEARTBEAT_MS * 2));
    assert.doesNotThrow(() => inbox.stop());
  });
});

describe("steering inbox lifecycle", () => {
  test("heartbeat is written at start and refreshed every 5 s by the clock", () => {
    const w = world();
    w.inbox.start();
    const read = () => JSON.parse(realFs.readFileSync(join(w.dir, "alive.json"), "utf8"));
    const first = read();
    assert.equal(first.v, 1);
    assert.equal(first.pid, process.pid);
    assert.equal(first.startedAt, "2026-10-26T10:00:00.000Z");
    assert.equal(first.lastSeen, "2026-10-26T10:00:00.000Z");
    assert.equal(first.mode, "rpc");
    w.clock.advance(HEARTBEAT_MS);
    const second = read();
    assert.equal(second.lastSeen, "2026-10-26T10:00:05.000Z");
    assert.equal(second.startedAt, "2026-10-26T10:00:00.000Z");
    w.inbox.stop();
  });

  test("the poll timer delivers files that arrive later", () => {
    const w = world();
    w.inbox.start();
    w.put("a1.json", w.directive("a1"));
    assert.equal(w.delivered.length, 0);
    w.clock.advance(POLL_MS);
    assert.equal(w.delivered.length, 1);
    w.inbox.stop();
  });

  test("stop clears timers and stops delivering and heartbeating", () => {
    const w = world();
    w.inbox.start();
    assert.equal(w.clock.timerCount(), 2);
    w.inbox.stop();
    assert.equal(w.clock.timerCount(), 0);
    w.put("a1.json", w.directive("a1"));
    w.inbox.poll();
    w.clock.advance(HEARTBEAT_MS * 3);
    assert.equal(w.delivered.length, 0);
    assert.equal(JSON.parse(realFs.readFileSync(join(w.dir, "alive.json"), "utf8")).lastSeen, "2026-10-26T10:00:00.000Z");
  });

  test("start is idempotent", () => {
    const w = world();
    assert.equal(w.inbox.start(), true);
    assert.equal(w.inbox.start(), false);
    assert.equal(w.clock.timerCount(), 2);
    w.inbox.stop();
  });

  for (const dir of [undefined, "", "relative/steer"]) {
    test(`inactive for dir ${JSON.stringify(dir)}: no fs call at all`, () => {
      const w = world({ dir });
      assert.equal(w.inbox.active, false);
      assert.equal(w.inbox.start(), false);
      w.inbox.poll();
      w.inbox.stop();
      w.clock.advance(HEARTBEAT_MS * 2);
      assert.deepEqual(w.calls, []);
      assert.equal(w.clock.timerCount(), 0);
    });
  }
});
