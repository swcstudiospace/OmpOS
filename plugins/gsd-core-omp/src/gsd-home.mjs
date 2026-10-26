/**
 * Shared logic of the gsd-core-omp plugin.
 *
 * Plain ESM with JSDoc types so that the omp extension entry (`index.ts`, loaded by Bun) and the
 * `bin/*.mjs` scripts (plain Node >= 24) run the very same code. Everything here is synchronous,
 * local-filesystem only, never opens a network connection, and NEVER throws to its caller: failures are
 * reported in the returned objects.
 *
 * The only process this module may spawn is `node <gsdHome>/bin/gsd-tools.cjs runtime-identity --raw`
 * (5 s timeout), and only when `spawn` is requested.
 */
import { spawnSync } from "node:child_process";
import {
  existsSync,
  lstatSync,
  mkdirSync,
  readdirSync,
  readFileSync,
  readlinkSync,
  realpathSync,
  statSync,
  symlinkSync,
} from "node:fs";
import { homedir } from "node:os";
import { basename, dirname, join, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

/** Package name of this plugin (key in omp's plugin lock file and `node_modules`). */
export const PLUGIN_NAME = "gsd-core-omp";

/** Version of the probe JSON schema. */
export const PROBE_SCHEMA = 1;

/** Exit codes of `bin/gsd-omp-probe.mjs`. */
export const EXIT = Object.freeze({ healthy: 0, degraded: 2, notInstalled: 3 });

/** Timeout for the single process the probe may spawn. */
export const IDENTITY_TIMEOUT_MS = 5000;

const SAFE_AGENT_FILE = /^[A-Za-z0-9][A-Za-z0-9._-]*\.md$/u;
const COMPACT_SUFFIX = ".compact.md";

/**
 * @typedef {object} GsdOptions Every field is optional; defaults come from the live process.
 * @property {Record<string, string | undefined>} [env] Environment to read (default `process.env`).
 * @property {string} [home] Home directory (default `os.homedir()`).
 * @property {string} [userAgentsDir] omp user agents dir (default `<PI_CODING_AGENT_DIR or ~/.omp/agent>/agents`).
 * @property {string} [pluginRoot] Root of this plugin (default: the directory above `src/`).
 * @property {string} [pluginAgentsDir] Where symlinks are created (default `<pluginRoot>/agents`).
 * @property {string} [ompPluginsDir] omp plugin manager dir (default `~/.omp/plugins`).
 * @property {string} [nodeBin] Node executable used for the identity spawn (default `node`).
 * @property {number} [timeoutMs] Identity spawn timeout (default 5000).
 * @property {boolean} [spawn] Spawn `gsd-tools runtime-identity` (default false).
 * @property {boolean} [dryRun] Link only: report without touching the filesystem.
 */

/**
 * @typedef {object} GsdHome
 * @property {string} path Resolved GSD-Core install root (directory that holds `bin/gsd-tools.cjs`).
 * @property {"GSD_CORE_HOME" | "KIMI_CONFIG_DIR" | "~/.config/agents" | "~/.agents"} source Which rule selected it.
 * @property {boolean} exists Whether `path` is an existing directory.
 * @property {{ source: string, path: string }[]} candidates Every candidate in precedence order.
 */

/**
 * @typedef {object} AgentEntry
 * @property {string} name Agent name (file name without `.md`).
 * @property {string} file Absolute path of the authoritative profile inside `<gsdHome>/agents`.
 */

/**
 * @typedef {object} LinkIssue
 * @property {string} name Agent name or `*` for a directory-level problem.
 * @property {"path-escape" | "conflict" | "io" | "unreadable"} code Machine readable reason.
 * @property {string} message Human readable reason.
 */

/**
 * @typedef {object} LinkResult
 * @property {boolean} dryRun Whether the filesystem was left untouched.
 * @property {string[]} created Names linked (or that would be linked in dry-run mode).
 * @property {string[]} unchanged Names whose correct symlink already existed.
 * @property {string[]} skippedUser Names left alone because the user's omp agents dir already has them.
 * @property {LinkIssue[]} errors Problems; a non-empty list never aborts the other agents.
 * @property {{ candidates: number, created: number, unchanged: number, skippedUser: number, errors: number }} counts
 * @property {string} pluginAgentsDir Directory the links live in.
 */

/**
 * @param {unknown} error
 * @returns {string}
 */
function messageOf(error) {
  return error instanceof Error ? error.message : String(error);
}

/**
 * @param {string} path
 * @returns {boolean}
 */
function isDirectory(path) {
  try {
    return statSync(path).isDirectory();
  } catch {
    return false;
  }
}

/**
 * @param {string} path
 * @returns {boolean}
 */
function isFile(path) {
  try {
    return statSync(path).isFile();
  } catch {
    return false;
  }
}

/**
 * @param {string} child Absolute real path.
 * @param {string} parent Absolute real directory path.
 * @returns {boolean} True when `child` is strictly inside `parent`.
 */
function isInside(child, parent) {
  const prefix = parent.endsWith(sep) ? parent : parent + sep;
  return child.startsWith(prefix);
}

/**
 * @param {string} path
 * @returns {string | undefined}
 */
function readTrimmed(path) {
  try {
    return readFileSync(path, "utf8").trim() || undefined;
  } catch {
    return undefined;
  }
}

/**
 * @param {string | undefined} value
 * @returns {string | undefined} The value, or undefined when unset or blank.
 */
function nonBlank(value) {
  return value !== undefined && value.trim() !== "" ? value : undefined;
}

/**
 * Fills in every default of {@link GsdOptions}.
 *
 * @param {GsdOptions} [options]
 */
export function resolvePaths(options = {}) {
  const env = options.env ?? process.env;
  const home = options.home ?? homedir();
  const pluginRoot = resolve(options.pluginRoot ?? fileURLToPath(new URL("..", import.meta.url)));
  const agentDir = nonBlank(env.PI_CODING_AGENT_DIR) ?? join(home, ".omp", "agent");
  return {
    env,
    home,
    pluginRoot,
    userAgentsDir: resolve(options.userAgentsDir ?? join(agentDir, "agents")),
    pluginAgentsDir: resolve(options.pluginAgentsDir ?? join(pluginRoot, "agents")),
    ompPluginsDir: resolve(options.ompPluginsDir ?? join(home, ".omp", "plugins")),
    nodeBin: options.nodeBin ?? "node",
    timeoutMs: options.timeoutMs ?? IDENTITY_TIMEOUT_MS,
  };
}

/**
 * Resolves the GSD-Core install root. Precedence: `GSD_CORE_HOME` (explicit override, always wins, even when
 * it does not exist so the probe reports that exact path), `$KIMI_CONFIG_DIR/gsd-core`, `~/.config/agents/gsd-core`,
 * `~/.agents/gsd-core`. Among the non-override candidates the first holding `bin/gsd-tools.cjs` wins, then the
 * first existing directory, then `~/.agents/gsd-core`.
 *
 * @param {GsdOptions} [options]
 * @returns {GsdHome}
 */
export function resolveGsdHome(options = {}) {
  const { env, home } = resolvePaths(options);
  /** @type {{ source: GsdHome["source"], path: string }[]} */
  const candidates = [];
  const override = nonBlank(env.GSD_CORE_HOME);
  if (override !== undefined) candidates.push({ source: "GSD_CORE_HOME", path: resolve(override) });
  const kimi = nonBlank(env.KIMI_CONFIG_DIR);
  if (kimi !== undefined) candidates.push({ source: "KIMI_CONFIG_DIR", path: resolve(kimi, "gsd-core") });
  candidates.push({ source: "~/.config/agents", path: join(home, ".config", "agents", "gsd-core") });
  candidates.push({ source: "~/.agents", path: join(home, ".agents", "gsd-core") });

  const pick =
    (override !== undefined ? candidates[0] : undefined) ??
    candidates.find((c) => isFile(join(c.path, "bin", "gsd-tools.cjs"))) ??
    candidates.find((c) => isDirectory(c.path)) ??
    candidates[candidates.length - 1];
  return { path: pick.path, source: pick.source, exists: isDirectory(pick.path), candidates };
}

/**
 * Scans `<gsdHome>/agents` for authoritative profiles (`*.md`, never `*.compact.md`, which reuse the full
 * profile's `name:`). A file whose real path is not inside the real `<gsdHome>/agents` is refused.
 *
 * @param {string} gsdHome
 * @param {LinkIssue[]} [errors] Collector for refused or unreadable entries.
 * @returns {AgentEntry[]} Sorted by name.
 */
export function scanAuthoritativeAgents(gsdHome, errors = []) {
  const agentsDir = join(gsdHome, "agents");
  /** @type {string} */
  let realAgentsDir;
  /** @type {string[]} */
  let names;
  try {
    realAgentsDir = realpathSync(agentsDir);
    names = readdirSync(agentsDir);
  } catch (error) {
    if (existsSync(gsdHome)) {
      errors.push({ name: "*", code: "unreadable", message: `cannot read ${agentsDir}: ${messageOf(error)}` });
    }
    return [];
  }
  /** @type {AgentEntry[]} */
  const entries = [];
  for (const file of names.sort()) {
    if (!SAFE_AGENT_FILE.test(file) || file.endsWith(COMPACT_SUFFIX)) continue;
    const full = join(agentsDir, file);
    try {
      const real = realpathSync(full);
      if (!isInside(real, realAgentsDir)) {
        errors.push({ name: basename(file, ".md"), code: "path-escape", message: `${full} resolves outside ${realAgentsDir}` });
        continue;
      }
      if (!statSync(real).isFile()) continue;
    } catch (error) {
      errors.push({ name: basename(file, ".md"), code: "unreadable", message: `${full}: ${messageOf(error)}` });
      continue;
    }
    entries.push({ name: basename(file, ".md"), file: full });
  }
  return entries;
}

/**
 * Authoritative agents that the user's omp agents directory does not already provide.
 *
 * @param {string} gsdHome
 * @param {GsdOptions & { errors?: LinkIssue[] }} [options]
 * @returns {AgentEntry[]}
 */
export function listAuthoritativeAgents(gsdHome, options = {}) {
  const { userAgentsDir } = resolvePaths(options);
  return scanAuthoritativeAgents(gsdHome, options.errors).filter((a) => !existsSyncLink(join(userAgentsDir, `${a.name}.md`)));
}

/**
 * @param {string} path
 * @returns {boolean} True for anything on disk at `path`, including a dangling symlink.
 */
function existsSyncLink(path) {
  try {
    lstatSync(path);
    return true;
  } catch {
    return false;
  }
}

/**
 * Creates absolute symlinks `<pluginAgentsDir>/<name>.md -> <gsdHome>/agents/<name>.md` for every authoritative
 * agent the user does not already have. Idempotent, never overwrites or removes anything (a differing existing
 * entry is reported as a `conflict`), never follows a source outside `<gsdHome>/agents`, never throws.
 *
 * @param {string} gsdHome
 * @param {GsdOptions} [options]
 * @returns {LinkResult}
 */
export function linkAgents(gsdHome, options = {}) {
  const { userAgentsDir, pluginAgentsDir } = resolvePaths(options);
  const dryRun = options.dryRun === true;
  /** @type {LinkIssue[]} */
  const errors = [];
  /** @type {string[]} */
  const created = [];
  /** @type {string[]} */
  const unchanged = [];
  /** @type {string[]} */
  const skippedUser = [];
  let candidates = 0;
  try {
    const all = scanAuthoritativeAgents(gsdHome, errors);
    candidates = all.length;
    const sourceDir = join(gsdHome, "agents");
    if (resolve(sourceDir) === pluginAgentsDir) {
      errors.push({ name: "*", code: "conflict", message: "plugin agents dir equals the GSD agents dir" });
    } else if (all.length > 0) {
      if (!dryRun) mkdirSync(pluginAgentsDir, { recursive: true });
      for (const agent of all) {
        if (existsSyncLink(join(userAgentsDir, `${agent.name}.md`))) {
          skippedUser.push(agent.name);
          continue;
        }
        const dest = join(pluginAgentsDir, `${agent.name}.md`);
        const target = resolve(agent.file);
        try {
          if (existsSyncLink(dest)) {
            if (lstatSync(dest).isSymbolicLink() && readlinkSync(dest) === target) unchanged.push(agent.name);
            else errors.push({ name: agent.name, code: "conflict", message: `${dest} exists and is not the expected link to ${target}; left untouched` });
            continue;
          }
          if (!dryRun) symlinkSync(target, dest, "file");
          created.push(agent.name);
        } catch (error) {
          errors.push({ name: agent.name, code: "io", message: messageOf(error) });
        }
      }
    }
  } catch (error) {
    errors.push({ name: "*", code: "io", message: messageOf(error) });
  }
  return {
    dryRun,
    created,
    unchanged,
    skippedUser,
    errors,
    counts: {
      candidates,
      created: created.length,
      unchanged: unchanged.length,
      skippedUser: skippedUser.length,
      errors: errors.length,
    },
    pluginAgentsDir,
  };
}

/**
 * @param {string} dir
 * @returns {number} Number of `gsd-*` skill directories holding a SKILL.md.
 */
function countGsdSkills(dir) {
  try {
    return readdirSync(dir).filter((n) => n.startsWith("gsd-") && isFile(join(dir, n, "SKILL.md"))).length;
  } catch {
    return 0;
  }
}

/**
 * @param {string} gsdHome
 * @param {string} pluginAgentsDir
 * @param {string[]} authoritative
 * @returns {Set<string>} Authoritative names that already have a working plugin symlink.
 */
function validPluginLinks(gsdHome, pluginAgentsDir, authoritative) {
  const linked = new Set();
  const agentsDir = resolve(gsdHome, "agents");
  for (const name of authoritative) {
    const dest = join(pluginAgentsDir, `${name}.md`);
    try {
      if (!lstatSync(dest).isSymbolicLink()) continue;
      const target = resolve(dirname(dest), readlinkSync(dest));
      if (dirname(target) === agentsDir && isFile(target)) linked.add(name);
    } catch {
      // absent or unreadable: not linked
    }
  }
  return linked;
}

/**
 * @param {string} ompPluginsDir
 * @param {string} pluginRoot
 * @returns {{ pluginLinked: boolean, enabled: boolean }}
 */
function ompPluginState(ompPluginsDir, pluginRoot) {
  let pluginLinked = false;
  try {
    pluginLinked = realpathSync(join(ompPluginsDir, "node_modules", PLUGIN_NAME)) === realpathSync(pluginRoot);
  } catch {
    pluginLinked = false;
  }
  let enabled = false;
  if (pluginLinked) {
    try {
      const lock = JSON.parse(readFileSync(join(ompPluginsDir, "omp-plugins.lock.json"), "utf8"));
      enabled = lock?.plugins?.[PLUGIN_NAME]?.enabled === true;
    } catch {
      enabled = false;
    }
  }
  return { pluginLinked, enabled };
}

/**
 * Runs `node <gsdHome>/bin/gsd-tools.cjs runtime-identity --raw`.
 *
 * @param {string} gsdHome
 * @param {string} nodeBin
 * @param {number} timeoutMs
 * @returns {{ packageName: string, version: string } | { error: string }}
 */
function readIdentity(gsdHome, nodeBin, timeoutMs) {
  const result = spawnSync(nodeBin, [join(gsdHome, "bin", "gsd-tools.cjs"), "runtime-identity", "--raw"], {
    cwd: gsdHome,
    encoding: "utf8",
    timeout: timeoutMs,
    maxBuffer: 1024 * 1024,
    stdio: ["ignore", "pipe", "pipe"],
    windowsHide: true,
  });
  if (result.error) return { error: `runtime-identity failed: ${result.error.message}` };
  if (result.status !== 0) return { error: `runtime-identity exited with ${result.status ?? result.signal}` };
  try {
    const parsed = JSON.parse(result.stdout);
    if (typeof parsed?.packageName === "string" && typeof parsed?.version === "string") {
      return { packageName: parsed.packageName, version: parsed.version };
    }
    return { error: "runtime-identity output lacks packageName/version" };
  } catch {
    return { error: "runtime-identity output is not JSON" };
  }
}

/**
 * Health probe, schema 1. `status` is `healthy` (exit 0), `degraded` (exit 2) or `not-installed` (exit 3).
 * Never throws and never touches the network.
 *
 * @param {GsdOptions} [options] `spawn: true` additionally runs `gsd-tools runtime-identity --raw`.
 */
export function probe(options = {}) {
  const problems = [];
  const paths = resolvePaths(options);
  const home = resolveGsdHome(options);
  const base = {
    schema: PROBE_SCHEMA,
    ok: false,
    status: /** @type {"healthy" | "degraded" | "not-installed"} */ ("not-installed"),
    gsdHome: home.path,
    source: home.source,
    packageName: /** @type {string | null} */ (null),
    version: /** @type {string | null} */ (null),
    runtime: /** @type {string | null} */ (null),
    node: process.version,
    gsdTools: false,
    skills: { dir: join(dirname(home.path), "skills"), gsdCount: 0 },
    agents: { authoritative: 0, userRegistered: 0, pluginLinked: 0, missing: /** @type {string[]} */ ([]) },
    omp: { pluginLinked: false, enabled: false },
    problems,
  };
  try {
    base.omp = ompPluginState(paths.ompPluginsDir, paths.pluginRoot);
    if (!home.exists) {
      problems.push(`GSD-Core not found at ${home.path} (source ${home.source}); install with npx @opengsd/gsd-core --kimi --global or set GSD_CORE_HOME`);
      return base;
    }
    base.status = "degraded";
    base.runtime = readTrimmed(join(home.path, ".gsd-runtime")) ?? null;
    base.version = readTrimmed(join(home.path, "VERSION")) ?? null;
    base.gsdTools = isFile(join(home.path, "bin", "gsd-tools.cjs"));
    if (!base.gsdTools) problems.push(`missing ${join(home.path, "bin", "gsd-tools.cjs")}`);
    if (options.spawn === true && base.gsdTools) {
      const identity = readIdentity(home.path, paths.nodeBin, paths.timeoutMs);
      if ("error" in identity) problems.push(identity.error);
      else {
        base.packageName = identity.packageName;
        base.version = identity.version;
      }
    }
    base.skills.gsdCount = countGsdSkills(base.skills.dir);
    if (base.skills.gsdCount === 0) problems.push(`no gsd-* skills under ${base.skills.dir}`);

    const scanErrors = [];
    const all = scanAuthoritativeAgents(home.path, scanErrors);
    for (const issue of scanErrors) problems.push(`${issue.name}: ${issue.message}`);
    const names = all.map((a) => a.name);
    const user = names.filter((n) => existsSyncLink(join(paths.userAgentsDir, `${n}.md`)));
    const linked = validPluginLinks(home.path, paths.pluginAgentsDir, names);
    base.agents = {
      authoritative: names.length,
      userRegistered: user.length,
      pluginLinked: linked.size,
      missing: names.filter((n) => !user.includes(n) && !linked.has(n)),
    };
    if (names.length === 0) problems.push(`no agent profiles under ${join(home.path, "agents")}`);
    if (base.agents.missing.length > 0) {
      problems.push(`${base.agents.missing.length} agent(s) not visible to omp yet; run bin/gsd-omp-link.mjs or start omp with the plugin`);
    }
    if (problems.length === 0) {
      base.status = "healthy";
      base.ok = true;
    }
  } catch (error) {
    problems.push(`probe failed: ${messageOf(error)}`);
    if (base.status === "not-installed" && home.exists) base.status = "degraded";
  }
  return base;
}

/**
 * @param {{ status: string }} result A {@link probe} result.
 * @returns {number} The process exit code for the probe CLI.
 */
export function exitCodeFor(result) {
  if (result.status === "healthy") return EXIT.healthy;
  return result.status === "not-installed" ? EXIT.notInstalled : EXIT.degraded;
}

/**
 * The single code path shared by `session_start` and `bin/gsd-omp-link.mjs`: probe without spawning, then link
 * when GSD-Core is installed. Never throws.
 *
 * @param {GsdOptions} [options]
 * @returns {{ probe: ReturnType<typeof probe>, link: LinkResult | null }}
 */
export function syncAgents(options = {}) {
  const state = probe({ ...options, spawn: false });
  if (state.status === "not-installed") return { probe: state, link: null };
  return { probe: state, link: linkAgents(state.gsdHome, options) };
}
