/**
 * OmpOS connection report: a raw snapshot of what omp actually has loaded, so the host (C#) can tell which plugins
 * are connected. The report lists names only; the host owns the expected-name table per plugin.
 *
 * Environment: `OMPOS_CONNECTION_FILE` (absolute file path; unset = inactive, no fs call at all).
 *
 * The parent directory is created (recursive, mode 0700) when it is missing.
 *
 * Report v1 (atomic write, mode 0600):
 *   { "v":1, "at":ISO, "pid":number, "mode":string|null, "omp":{"version":string|null},
 *     "tools":[sorted names], "commands":[sorted names], "commandsAvailable":boolean }
 *
 * Silence rule: nothing here writes to stdout or stderr, and nothing throws to the caller.
 */
import { dirname, isAbsolute } from "node:path";
import { writeJsonAtomic } from "./steer-inbox.mjs";

/** Minimum gap between two rewrites of the same report, in milliseconds. */
export const REWRITE_GAP_MS = 2000;

function sortedNames(items) {
  if (!Array.isArray(items)) return [];
  const names = [];
  for (const item of items) {
    if (item !== null && typeof item === "object" && typeof item.name === "string") names.push(item.name);
  }
  return [...new Set(names)].sort();
}

/**
 * Builds the report body from the live extension API.
 * @param {{ clock: { now: () => number }, pi: any, mode: string | null, version: string | null }} options
 */
export function buildConnectionReport({ clock, pi, mode, version }) {
  let tools = [];
  try {
    tools = sortedNames(pi?.getAllTools?.());
  } catch {
    tools = [];
  }
  let commands = [];
  let commandsAvailable = false;
  if (typeof pi?.getCommands === "function") {
    try {
      commands = sortedNames(pi.getCommands());
      commandsAvailable = true;
    } catch {
      commands = [];
    }
  }
  return {
    v: 1,
    at: new Date(clock.now()).toISOString(),
    pid: process.pid,
    mode: typeof mode === "string" ? mode : null,
    omp: { version: typeof version === "string" ? version : null },
    tools,
    commands,
    commandsAvailable,
  };
}

/**
 * Writes the report once, unconditionally.
 * @param {{ fs: any, clock: { now: () => number }, file: string | undefined, pi: any, mode: string | null, version: string | null }} options
 * @returns {boolean} whether a report is in place
 */
export function writeConnectionReport({ fs, clock, file, pi, mode, version }) {
  if (typeof file !== "string" || !isAbsolute(file)) return false;
  try {
    // The host names a file inside a `connection/` directory it may not have created yet.
    fs.mkdirSync(dirname(file), { recursive: true, mode: 0o700 });
    return writeJsonAtomic(fs, file, buildConnectionReport({ clock, pi, mode, version }));
  } catch {
    return false;
  }
}

/**
 * Creates the stateful reporter used by the extension: the first `notify()` writes; later calls rewrite only when the
 * tool or command list changed since the last write, and at most once per `REWRITE_GAP_MS` (a change inside the gap is
 * rewritten by a one-shot timer when the gap ends).
 *
 * @param {{ fs: any, clock: any, file: string | undefined, pi: any, getMode?: () => string | null, version: string | null }} options
 * @returns {{ notify: () => void, stop: () => void }}
 */
export function createConnectionReporter({ fs, clock, file, pi, getMode, version }) {
  const active = typeof file === "string" && isAbsolute(file);
  let lastKey = null;
  let lastWriteAt = 0;
  let retry = null;
  let stopped = false;

  function signature() {
    const probe = buildConnectionReport({ clock, pi, mode: null, version: null });
    return `${probe.tools.join("\n")}\u0000${probe.commands.join("\n")}`;
  }

  function notify() {
    if (!active || stopped) return;
    try {
      const key = signature();
      if (key === lastKey) return;
      const wait = lastWriteAt + REWRITE_GAP_MS - clock.now();
      if (lastKey !== null && wait > 0) {
        if (retry === null) {
          retry = clock.setTimeout(() => {
            retry = null;
            notify();
          }, wait);
        }
        return;
      }
      let mode = null;
      try {
        mode = getMode?.() ?? null;
      } catch {
        mode = null;
      }
      if (writeConnectionReport({ fs, clock, file, pi, mode, version })) {
        lastKey = key;
        lastWriteAt = clock.now();
      }
    } catch {
      // never throw into omp
    }
  }

  function stop() {
    stopped = true;
    if (retry !== null) clock.clearTimeout(retry);
    retry = null;
  }

  return { notify, stop };
}
