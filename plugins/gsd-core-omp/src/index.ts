/**
 * gsd-core-omp extension entry (`package.json` -> `omp.extensions`).
 *
 * omp calls this factory once per session, subagents included, so it registers only: no fs access, no spawn and
 * no module-scope state at load time. All work happens inside `session_start` and the command handler, and none
 * of it can throw into omp.
 *
 * The plugin never copies GSD-Core files. It symlinks the installed GSD agent profiles that omp's task tool
 * cannot see yet into its own gitignored `agents/` directory (see `src/gsd-home.mjs`).
 */
import { probe, syncAgents } from "./gsd-home.mjs";

/** The slice of omp's extension context this plugin uses. */
interface NotifyContext {
  ui?: { notify?: (message: string, level?: "info" | "warn" | "error") => void };
}

/** The slice of omp's `ExtensionAPI` this plugin uses. */
interface PluginApi {
  on(event: "session_start", handler: (event: unknown, ctx: NotifyContext) => void | Promise<void>): void;
  registerCommand(
    name: string,
    options: { description: string; handler: (args: string, ctx: NotifyContext) => void | Promise<void> },
  ): void;
}

function notify(ctx: NotifyContext | undefined, message: string, level: "info" | "warn" | "error"): void {
  try {
    ctx?.ui?.notify?.(message, level);
  } catch {
    // a failing UI must never break a session
  }
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
}
