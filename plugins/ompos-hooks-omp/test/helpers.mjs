import { createHookBridge } from "../src/bridge.mjs";

export const URL_OK = "http://127.0.0.1:4555/v1/hook";
export const TOKEN = "tok-SECRET-123";

/** Fake clock + timers: timers only fire when `fire()` is called. */
export function fakeClock(start = 1_000_000) {
  let t = start;
  const timers = new Map();
  let id = 1;
  return {
    now: () => t,
    advance: (ms) => {
      t += ms;
    },
    setTimeout: (fn, ms) => {
      timers.set(id, { fn, ms });
      return id++;
    },
    clearTimeout: (h) => timers.delete(h),
    pendingTimers: () => [...timers.values()],
    fire: () => {
      for (const [k, v] of [...timers]) {
        timers.delete(k);
        v.fn();
      }
    },
  };
}

/**
 * Fake fetch. `respond(body, init)` returns a decision object, a Response-like spec
 * `{ status, text }`, throws, or returns the string "hang" to wait for abort.
 */
export function fakeFetch(respond = () => ({})) {
  const calls = [];
  const fn = async (url, init) => {
    const body = JSON.parse(init.body);
    calls.push({ url, init, body });
    const out = await respond(body, init);
    if (out === "hang") {
      return new Promise((_, reject) => {
        init.signal.addEventListener("abort", () => reject(new Error("aborted")));
      });
    }
    if (out && typeof out === "object" && "status" in out) {
      return { status: out.status, json: async () => JSON.parse(out.text) };
    }
    return { status: 200, json: async () => out };
  };
  fn.calls = calls;
  fn.events = () => calls.map((c) => c.body.event);
  return fn;
}

export function startupFile(extra = {}) {
  return JSON.stringify({ version: 1, failClosedEvents: [], activeEvents: null, activeOmpEvents: [], ...extra });
}

export function makeBridge({ respond, startup, env = {}, files = {}, getAllTools, clock = fakeClock() } = {}) {
  const fetch = fakeFetch(respond);
  const fullEnv = { OMPOS_HOOKS_URL: URL_OK, OMPOS_HOOKS_TOKEN: TOKEN, ...env };
  if (startup !== undefined) fullEnv.OMPOS_HOOKS_STARTUP_FILE = "/startup.json";
  const reads = [];
  const bridge = createHookBridge({
    env: fullEnv,
    fetch,
    now: clock.now,
    setTimeout: clock.setTimeout,
    clearTimeout: clock.clearTimeout,
    getAllTools,
    readFile: (path) => {
      reads.push(path);
      if (path === "/startup.json") {
        if (startup instanceof Error) throw startup;
        return startup;
      }
      if (path in files) return files[path];
      throw new Error("ENOENT");
    },
  });
  return { bridge, fetch, clock, reads };
}

export function makeCtx(over = {}) {
  const notes = [];
  return {
    cwd: "/work",
    hasUI: true,
    agent: { kind: "main" },
    sessionManager: { getSessionId: () => "sess-1" },
    ui: { notify: (m, l) => notes.push([m, l]) },
    getContextUsage: () => ({ tokens: 100, contextWindow: 1000, percent: 10 }),
    notes,
    ...over,
  };
}
