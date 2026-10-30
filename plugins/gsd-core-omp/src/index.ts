/**
 * gsd-core-omp extension entry (`package.json` -> `omp.extensions`).
 *
 * omp calls this factory once per session, subagents included, so it registers only: no fs access, no spawn and
 * no module-scope state at load time. All work happens inside `session_start` and the command handler, and none
 * of it can throw into omp.
 *
 * The plugin never copies GSD-Core files. It symlinks the installed GSD agent profiles that omp's task tool
 * cannot see yet into its own gitignored `agents/` directory (see `src/gsd-home.mjs`).
 *
 * It also hosts two Autonogrammer host features, both inactive (no fs call) unless their environment variable is set, both
 * started for the top-level session only (never a subagent) and both silent (the TUI owns the screen):
 * - the steering inbox (`AUTONOGRAMMER_STEER_DIR`, `src/steer-inbox.mjs`): delivers directive files into the running session
 *   with `pi.sendUserMessage(text, { deliverAs })`, which omp's ACP `session/prompt` cannot do without cancelling the turn;
 * - the connection report (`AUTONOGRAMMER_CONNECTION_FILE`, `src/connection-report.mjs`): writes the raw tool and command names
 *   omp has loaded. omp emits `session_start` after every extension factory has run, so the first report is written
 *   there; `before_agent_start` and a 1 s one-shot timer re-check it because MCP tools can register later, and the
 *   reporter rewrites only when the lists changed (at most once per 2 s).
 */
import { createConnectionReporter } from "./connection-report.mjs";
import { probe, syncAgents } from "./gsd-home.mjs";
import { createSteerInbox, systemClock } from "./steer-inbox.mjs";
import * as nodeFs from "node:fs";

/** The slice of omp's extension context this plugin uses. */
interface NotifyContext {
  ui?: { notify?: (message: string, level?: "info" | "warn" | "error") => void };
  mode?: string;
  agent?: { kind?: string };
  isIdle?: () => boolean;
}

/** The slice of omp's `ExtensionAPI` this plugin uses. */
interface PluginApi {
  on(event: "session_start" | "session_shutdown" | "before_agent_start", handler: (event: unknown, ctx: NotifyContext) => void | Promise<void>): void;
  registerCommand(
    name: string,
    options: { description: string; handler: (args: string, ctx: NotifyContext) => void | Promise<void> },
  ): void;
  sendUserMessage(content: string, options?: { deliverAs?: "steer" | "followUp" | "aside" }): void;
  getAllTools(): { name: string }[];
  getCommands(): { name: string }[];
}

function notify(ctx: NotifyContext | undefined, message: string, level: "info" | "warn" | "error"): void {
  try {
    ctx?.ui?.notify?.(message, level);
  } catch {
    // a failing UI must never break a session
  }
}

/** Starts the Autonogrammer steering inbox and connection report for the top-level session; inert without their env vars. */
function registerHostFeatures(pi: PluginApi): void {
  let ctxRef: NotifyContext | undefined;
  let inbox: { stop: () => void } | null = null;
  let reporter: { notify: () => void; stop: () => void } | null = null;
  let oneShot: unknown = null;

  const stopAll = (): void => {
    try {
      inbox?.stop();
      reporter?.stop();
      if (oneShot !== null) systemClock.clearTimeout(oneShot);
    } catch {
      // never throw into omp
    }
    inbox = null;
    reporter = null;
    oneShot = null;
  };
  const mode = (): string | null => (typeof ctxRef?.mode === "string" ? ctxRef.mode : null);

  pi.on("session_start", (_event, ctx) => {
    try {
      stopAll();
      ctxRef = ctx;
      if (ctx?.agent?.kind === "sub") return;
      const env = process.env;
      if (env.AUTONOGRAMMER_STEER_DIR) {
        const created = createSteerInbox({
          fs: nodeFs,
          clock: systemClock,
          dir: env.AUTONOGRAMMER_STEER_DIR,
          getMode: mode,
          deliver: (text: string, deliverAs: "steer" | "followUp" | "aside") => {
            let idle: boolean | null = null;
            try {
              idle = typeof ctxRef?.isIdle === "function" ? ctxRef.isIdle() : null;
            } catch {
              idle = null;
            }
            pi.sendUserMessage(text, { deliverAs });
            return { idle };
          },
        });
        created.start();
        inbox = created;
      }
      if (env.AUTONOGRAMMER_CONNECTION_FILE) {
        const created = createConnectionReporter({
          fs: nodeFs,
          clock: systemClock,
          file: env.AUTONOGRAMMER_CONNECTION_FILE,
          pi,
          getMode: mode,
          version: null, // omp exposes no version to extensions
        });
        reporter = created;
        created.notify();
        oneShot = systemClock.setTimeout(() => created.notify(), 1000);
      }
    } catch {
      // never throw into omp
    }
  });

  pi.on("before_agent_start", () => {
    try {
      reporter?.notify();
    } catch {
      // never throw into omp
    }
  });

  pi.on("session_shutdown", () => stopAll());
}

export default function gsdCoreOmp(pi: PluginApi): void {
  pi.on("session_start", (_event, ctx) => {
    try {
      const { probe: state, link } = syncAgents();
      if (state.status === "not-installed") {
        notify(
          ctx,
          `GSD-Core not found at ${state.gsdHome}. Install: npx @opengsd/gsd-core --kimi --global (or set GSD_CORE_HOME)`,
          "warn",
        );
        return;
      }
      if (link !== null && link.errors.length > 0) {
        notify(ctx, `gsd-core-omp: ${link.errors.length} agent link problem(s); run /gsd-core for details`, "warn");
      }
    } catch {
      // never throw into omp
    }
  });

  pi.registerCommand("gsd-core", {
    description: "GSD-Core binding status (version, agents linked/missing)",
    handler: (_args, ctx) => {
      try {
        notify(ctx, JSON.stringify(probe({ spawn: true })), "info");
      } catch (error) {
        notify(ctx, `gsd-core-omp: probe failed: ${error instanceof Error ? error.message : String(error)}`, "error");
      }
    },
  });

  registerHostFeatures(pi);
}
