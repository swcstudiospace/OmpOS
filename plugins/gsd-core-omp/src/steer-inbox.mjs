/**
 * Autonogrammer steering inbox: delivers directives that Autonogrammer drops into a directory into the RUNNING omp session.
 *
 * omp's ACP agent cancels a running turn when a new `session/prompt` arrives, so live steering cannot travel as a
 * prompt. An extension can instead call `pi.sendUserMessage(text, { deliverAs })`, which queues the text into the
 * running session (omp 18.12: `deliverAs` is "steer" | "followUp" | "aside"; idle sessions start a turn).
 *
 * Layout under `AUTONOGRAMMER_STEER_DIR` (absolute path; unset or relative = inactive, no fs call at all):
 *   inbox/<id>.json   written by Autonogrammer (tmp + rename); only `*.json` regular files are read
 *   acks/<id>.json    written here, atomically (mode 0600)
 *   done/<id>.json    a handled directive is moved here BEFORE delivery, so a crash can lose but never repeat it
 *   alive.json        heartbeat, rewritten every 5 s
 *
 * Silence rule: this module never writes to stdout or stderr and never throws to its caller.
 */
import { constants as fsConstants } from "node:fs";
import { basename, isAbsolute, join } from "node:path";

/** Maximum directive file size in bytes. */
export const MAX_FILE_BYTES = 16 * 1024;
/** Maximum UTF-8 size of a directive text. */
export const MAX_TEXT_BYTES = 8192;
/** Maximum regular files examined per pass; the rest wait for the next pass. */
export const MAX_FILES_PER_PASS = 64;
/** Heartbeat period in milliseconds. */
export const HEARTBEAT_MS = 5000;
/** Poll fallback period in milliseconds. */
export const POLL_MS = 500;

const ID_PATTERN = /^[A-Za-z0-9_-]{1,64}$/u;
const ISO_PATTERN = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$/u;
const MODES = new Set(["steer", "followUp", "aside"]);

/**
 * @typedef {object} Clock
 * @property {() => number} now epoch milliseconds
 * @property {(fn: () => void, ms: number) => unknown} setInterval
 * @property {(handle: unknown) => void} clearInterval
 * @property {(fn: () => void, ms: number) => unknown} setTimeout
 * @property {(handle: unknown) => void} clearTimeout
 */

/** The real clock; every timer handle is unref'd so it never keeps omp alive. */
export const systemClock = {
  now: () => Date.now(),
  setInterval: (fn, ms) => {
    const handle = setInterval(fn, ms);
    handle.unref?.();
    return handle;
  },
  clearInterval: (handle) => clearInterval(handle),
  setTimeout: (fn, ms) => {
    const handle = setTimeout(fn, ms);
    handle.unref?.();
    return handle;
  },
  clearTimeout: (handle) => clearTimeout(handle),
};

let tmpSequence = 0;

/**
 * Writes JSON atomically: a sibling `.tmp` file (mode 0600) then a rename, so no reader ever sees a partial file.
 * @param {any} fs
 * @param {string} file
 * @param {unknown} value
 * @returns {boolean} whether the file is in place
 */
export function writeJsonAtomic(fs, file, value) {
  tmpSequence += 1;
  const tmp = `${file}.${process.pid}.${tmpSequence}.tmp`;
  try {
    fs.writeFileSync(tmp, `${JSON.stringify(value)}\n`, { mode: 0o600 });
    fs.renameSync(tmp, file);
    return true;
  } catch {
    try {
      fs.unlinkSync(tmp);
    } catch {
      // nothing to clean up
    }
    return false;
  }
}

/**
 * Reads at most `MAX_FILE_BYTES` of a regular file without following a symlink.
 * @returns {{ kind: "skip" } | { kind: "too-large" } | { kind: "ok", text: string }}
 */
function readDirective(fs, file) {
  const lst = fs.lstatSync(file);
  if (!lst.isFile()) return { kind: "skip" };
  let fd;
  try {
    fd = fs.openSync(file, fsConstants.O_RDONLY | (fsConstants.O_NOFOLLOW ?? 0));
  } catch {
    return { kind: "skip" };
  }
  try {
    const st = fs.fstatSync(fd);
    if (!st.isFile()) return { kind: "skip" };
    if (st.size > MAX_FILE_BYTES) return { kind: "too-large" };
    const buffer = Buffer.alloc(st.size);
    let offset = 0;
    while (offset < st.size) {
      const read = fs.readSync(fd, buffer, offset, st.size - offset, offset);
      if (read <= 0) break;
      offset += read;
    }
    return { kind: "ok", text: buffer.subarray(0, offset).toString("utf8") };
  } finally {
    try {
      fs.closeSync(fd);
    } catch {
      // already gone
    }
  }
}

/**
 * Validates a parsed directive for the file stem `id`.
 * @returns {{ ok: true, mode: string, text: string } | { ok: false, reason: string, mode: string | null }}
 */
function validate(parsed, id) {
  const fail = (reason, mode = null) => ({ ok: false, reason, mode });
  if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed)) return fail("invalid-schema");
  const mode = typeof parsed.mode === "string" && MODES.has(parsed.mode) ? parsed.mode : null;
  if (typeof parsed.id !== "string" || !ID_PATTERN.test(parsed.id) || parsed.id !== id) return fail("bad-id", mode);
  if (parsed.v !== 1 || mode === null || parsed.source !== "autonogrammer") return fail("invalid-schema", mode);
  if (typeof parsed.createdAt !== "string" || !ISO_PATTERN.test(parsed.createdAt) || Number.isNaN(Date.parse(parsed.createdAt))) {
    return fail("invalid-schema", mode);
  }
  if (typeof parsed.text !== "string") return fail("invalid-schema", mode);
  if (parsed.text.trim().length === 0) return fail("blank-text", mode);
  if (Buffer.byteLength(parsed.text, "utf8") > MAX_TEXT_BYTES) return fail("too-large", mode);
  return { ok: true, mode, text: parsed.text };
}

/**
 * Creates the steering inbox.
 *
 * @param {object} options
 * @param {any} options.fs node:fs-compatible subset: mkdirSync, readdirSync, lstatSync, openSync, fstatSync, readSync,
 *   closeSync, writeFileSync, renameSync, unlinkSync, existsSync, watch
 * @param {Clock} options.clock
 * @param {(text: string, mode: "steer" | "followUp" | "aside") => ({ idle?: boolean | null } | void)} options.deliver
 *   hands the text to omp; may report whether the session was idle
 * @param {string | undefined} options.dir `AUTONOGRAMMER_STEER_DIR`; inactive unless absolute
 * @param {() => string | null} [options.getMode] omp run mode for the heartbeat
 * @returns {{ active: boolean, start: () => boolean, stop: () => void, poll: () => void }}
 */
export function createSteerInbox({ fs, clock, deliver, dir, getMode }) {
  const active = typeof dir === "string" && isAbsolute(dir);
  const inboxDir = active ? join(dir, "inbox") : "";
  const acksDir = active ? join(dir, "acks") : "";
  const doneDir = active ? join(dir, "done") : "";
  const aliveFile = active ? join(dir, "alive.json") : "";
  const handled = new Set();
  let watcher = null;
  let pollTimer = null;
  let heartbeatTimer = null;
  let startedAt = null;
  let running = false;
  let rejectedSequence = 0;

  const iso = () => new Date(clock.now()).toISOString();

  function ack(id, status, reason, mode, idle) {
    writeJsonAtomic(fs, join(acksDir, `${id}.json`), { v: 1, id, status, reason, mode, at: iso(), idle });
  }

  function moveToDone(file, id) {
    try {
      fs.renameSync(file, join(doneDir, `${id}.json`));
      return true;
    } catch {
      return false;
    }
  }

  function discard(file) {
    try {
      fs.unlinkSync(file);
    } catch {
      // the in-memory handled set still prevents a second pass
    }
  }

  /** Quarantines a file whose name cannot carry an ack. */
  function quarantine(file) {
    rejectedSequence += 1;
    if (!moveToDone(file, `invalid-${clock.now()}-${rejectedSequence}`)) discard(file);
  }

  function reject(file, id, reason, mode) {
    ack(id, "rejected", reason, mode, null);
    if (!moveToDone(file, id)) discard(file);
  }

  function handleFile(name) {
    const file = join(inboxDir, name);
    const id = basename(name, ".json");
    if (!ID_PATTERN.test(id)) {
      quarantine(file);
      return;
    }
    const ackFile = join(acksDir, `${id}.json`);
    if (handled.has(id) || fs.existsSync(ackFile) || fs.existsSync(join(doneDir, `${id}.json`))) {
      handled.add(id);
      // the first outcome stays authoritative: an existing ack is never overwritten
      if (!fs.existsSync(ackFile)) ack(id, "rejected", "duplicate", null, null);
      discard(file);
      return;
    }
    handled.add(id);
    const read = readDirective(fs, file);
    if (read.kind === "too-large") {
      reject(file, id, "too-large", null);
      return;
    }
    if (read.kind === "skip") {
      handled.delete(id);
      return;
    }
    let parsed;
    try {
      parsed = JSON.parse(read.text);
    } catch {
      reject(file, id, "invalid-json", null);
      return;
    }
    const verdict = validate(parsed, id);
    if (!verdict.ok) {
      reject(file, id, verdict.reason, verdict.mode);
      return;
    }
    if (!moveToDone(file, id)) {
      ack(id, "failed", "deliver-error", verdict.mode, null);
      discard(file);
      return;
    }
    let idle = null;
    try {
      const outcome = deliver(verdict.text, verdict.mode);
      if (outcome && typeof outcome.idle === "boolean") idle = outcome.idle;
    } catch {
      ack(id, "failed", "deliver-error", verdict.mode, null);
      return;
    }
    ack(id, "delivered", null, verdict.mode, idle);
  }

  function poll() {
    if (!running) return;
    try {
      /** @type {string[]} */
      const names = fs.readdirSync(inboxDir).filter((name) => name.endsWith(".json")).sort();
      let examined = 0;
      for (const name of names) {
        if (!running || examined >= MAX_FILES_PER_PASS) break;
        try {
          const lst = fs.lstatSync(join(inboxDir, name));
          if (!lst.isFile()) continue;
        } catch {
          continue;
        }
        examined += 1;
        try {
          handleFile(name);
        } catch {
          // one broken file never stops the inbox
        }
      }
    } catch {
      // inbox missing or unreadable: try again on the next tick
    }
  }

  function heartbeat() {
    if (!running) return;
    let mode = null;
    try {
      mode = getMode?.() ?? null;
    } catch {
      mode = null;
    }
    writeJsonAtomic(fs, aliveFile, { v: 1, pid: process.pid, startedAt, lastSeen: iso(), mode });
  }

  function start() {
    if (!active || running) return false;
    running = true;
    startedAt = iso();
    try {
      for (const path of [dir, inboxDir, acksDir, doneDir]) fs.mkdirSync(path, { recursive: true, mode: 0o700 });
    } catch {
      // the poll keeps retrying reads; writes fail soft
    }
    heartbeat();
    try {
      watcher = fs.watch(inboxDir, () => poll());
      watcher.on?.("error", () => {});
      watcher.unref?.();
    } catch {
      watcher = null;
    }
    pollTimer = clock.setInterval(poll, POLL_MS);
    heartbeatTimer = clock.setInterval(heartbeat, HEARTBEAT_MS);
    poll();
    return true;
  }

  function stop() {
    running = false;
    if (pollTimer !== null) clock.clearInterval(pollTimer);
    if (heartbeatTimer !== null) clock.clearInterval(heartbeatTimer);
    pollTimer = null;
    heartbeatTimer = null;
    try {
      watcher?.close();
    } catch {
      // already closed
    }
    watcher = null;
  }

  return { active, start, stop, poll };
}
