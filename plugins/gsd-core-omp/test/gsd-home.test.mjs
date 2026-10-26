import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { existsSync, lstatSync, mkdirSync, mkdtempSync, readdirSync, readlinkSync, rmSync, symlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { isAbsolute, join } from "node:path";
import { after, describe, test } from "node:test";
import { fileURLToPath } from "node:url";
import { EXIT, exitCodeFor, linkAgents, listAuthoritativeAgents, probe, resolveGsdHome, syncAgents } from "../src/gsd-home.mjs";

const pluginRoot = fileURLToPath(new URL("..", import.meta.url));
const temps = [];
after(() => {
  for (const dir of temps) rmSync(dir, { recursive: true, force: true });
});

const IDENTITY_OK = `process.stdout.write(JSON.stringify({ packageName: "@opengsd/gsd-core", version: "9.9.9" }));`;

/** Builds an isolated world: fake home, GSD install, user agents dir and plugin agents dir. */
function world({ agents = ["gsd-planner", "gsd-debugger", "gsd-codebase-mapper"], tools = IDENTITY_OK, install = true } = {}) {
  const root = mkdtempSync(join(tmpdir(), "gsd-omp-test-"));
  temps.push(root);
  const home = join(root, "home");
  const gsdHome = join(home, ".agents", "gsd-core");
  const userAgentsDir = join(home, ".omp", "agent", "agents");
  const pluginAgentsDir = join(root, "plugin", "agents");
  mkdirSync(userAgentsDir, { recursive: true });
  if (install) {
    mkdirSync(join(gsdHome, "bin"), { recursive: true });
    mkdirSync(join(gsdHome, "agents"), { recursive: true });
    writeFileSync(join(gsdHome, "bin", "gsd-tools.cjs"), tools);
    writeFileSync(join(gsdHome, "VERSION"), "9.9.9");
    writeFileSync(join(gsdHome, ".gsd-runtime"), "kimi");
    for (const name of agents) {
      writeFileSync(join(gsdHome, "agents", `${name}.md`), `---\nname: ${name}\ndescription: d\n---\n`);
      writeFileSync(join(gsdHome, "agents", `${name}.compact.md`), `---\nname: ${name}\ndescription: d\n---\n`);
    }
    mkdirSync(join(home, ".agents", "skills", "gsd-fast"), { recursive: true });
    writeFileSync(join(home, ".agents", "skills", "gsd-fast", "SKILL.md"), "---\nname: gsd-fast\n---\n");
  }
  const options = { env: {}, home, userAgentsDir, pluginAgentsDir, pluginRoot: join(root, "plugin"), ompPluginsDir: join(root, "omp-plugins") };
  return { root, home, gsdHome, userAgentsDir, pluginAgentsDir, options };
}

describe("resolveGsdHome", () => {
  test("honours GSD_CORE_HOME > KIMI_CONFIG_DIR > ~/.config/agents > ~/.agents", () => {
    const w = world();
    const mk = (dir) => {
      mkdirSync(join(dir, "bin"), { recursive: true });
      writeFileSync(join(dir, "bin", "gsd-tools.cjs"), "");
    };
    const override = join(w.root, "override");
    const kimi = join(w.root, "kimi");
    const xdg = join(w.home, ".config", "agents", "gsd-core");
    mk(override);
    mk(join(kimi, "gsd-core"));
    mk(xdg);

    const env = { GSD_CORE_HOME: override, KIMI_CONFIG_DIR: kimi };
    assert.deepEqual(pick(resolveGsdHome({ home: w.home, env })), [override, "GSD_CORE_HOME"]);
    assert.deepEqual(pick(resolveGsdHome({ home: w.home, env: { KIMI_CONFIG_DIR: kimi } })), [join(kimi, "gsd-core"), "KIMI_CONFIG_DIR"]);
    assert.deepEqual(pick(resolveGsdHome({ home: w.home, env: {} })), [xdg, "~/.config/agents"]);
    rmSync(xdg, { recursive: true });
    assert.deepEqual(pick(resolveGsdHome({ home: w.home, env: {} })), [w.gsdHome, "~/.agents"]);
    assert.equal(resolveGsdHome({ home: w.home, env: {} }).exists, true);
  });

  test("GSD_CORE_HOME wins even when it does not exist, and blank values are ignored", () => {
    const w = world();
    const missing = join(w.root, "nope");
    const r = resolveGsdHome({ home: w.home, env: { GSD_CORE_HOME: missing } });
    assert.equal(r.path, missing);
    assert.equal(r.exists, false);
    assert.equal(resolveGsdHome({ home: w.home, env: { GSD_CORE_HOME: "  ", KIMI_CONFIG_DIR: "" } }).path, w.gsdHome);
  });

  test("falls back to ~/.agents/gsd-core when nothing is installed", () => {
    const w = world({ install: false });
    const r = resolveGsdHome({ home: w.home, env: {} });
    assert.equal(r.path, join(w.home, ".agents", "gsd-core"));
    assert.equal(r.exists, false);
  });
});

function pick(r) {
  return [r.path, r.source];
}

describe("listAuthoritativeAgents", () => {
  test("excludes *.compact.md and names already present in the user's agents dir", () => {
    const w = world();
    writeFileSync(join(w.userAgentsDir, "gsd-planner.md"), "user copy");
    const names = listAuthoritativeAgents(w.gsdHome, w.options).map((a) => a.name);
    assert.deepEqual(names, ["gsd-codebase-mapper", "gsd-debugger"]);
  });

  test("a dangling user symlink still counts as present", () => {
    const w = world();
    symlinkSync(join(w.root, "gone"), join(w.userAgentsDir, "gsd-debugger.md"));
    const names = listAuthoritativeAgents(w.gsdHome, w.options).map((a) => a.name);
    assert.ok(!names.includes("gsd-debugger"));
  });
});

describe("linkAgents", () => {
  test("creates absolute symlinks to the authoritative profiles only", () => {
    const w = world();
    writeFileSync(join(w.userAgentsDir, "gsd-planner.md"), "user copy");
    const r = linkAgents(w.gsdHome, w.options);
    assert.deepEqual(r.created, ["gsd-codebase-mapper", "gsd-debugger"]);
    assert.deepEqual(r.skippedUser, ["gsd-planner"]);
    assert.deepEqual(r.errors, []);
    assert.equal(r.counts.candidates, 3);
    assert.deepEqual(readdirSync(w.pluginAgentsDir).sort(), ["gsd-codebase-mapper.md", "gsd-debugger.md"]);
    for (const name of r.created) {
      const dest = join(w.pluginAgentsDir, `${name}.md`);
      assert.ok(lstatSync(dest).isSymbolicLink());
      const target = readlinkSync(dest);
      assert.ok(isAbsolute(target));
      assert.equal(target, join(w.gsdHome, "agents", `${name}.md`));
    }
  });

  test("is idempotent", () => {
    const w = world();
    linkAgents(w.gsdHome, w.options);
    const again = linkAgents(w.gsdHome, w.options);
    assert.deepEqual(again.created, []);
    assert.equal(again.unchanged.length, 3);
    assert.deepEqual(again.errors, []);
  });

  test("never overwrites an existing file or a symlink to elsewhere", () => {
    const w = world();
    mkdirSync(w.pluginAgentsDir, { recursive: true });
    writeFileSync(join(w.pluginAgentsDir, "gsd-debugger.md"), "hand written");
    const elsewhere = join(w.root, "elsewhere.md");
    writeFileSync(elsewhere, "x");
    symlinkSync(elsewhere, join(w.pluginAgentsDir, "gsd-planner.md"));

    const r = linkAgents(w.gsdHome, w.options);
    assert.deepEqual(r.created, ["gsd-codebase-mapper"]);
    assert.deepEqual(r.errors.map((e) => [e.name, e.code]).sort(), [["gsd-debugger", "conflict"], ["gsd-planner", "conflict"]]);
    assert.equal(lstatSync(join(w.pluginAgentsDir, "gsd-debugger.md")).isSymbolicLink(), false);
    assert.equal(readlinkSync(join(w.pluginAgentsDir, "gsd-planner.md")), elsewhere);
  });

  test("refuses a profile whose real path escapes gsdHome/agents", () => {
    const w = world();
    const outside = join(w.root, "outside.md");
    writeFileSync(outside, "---\nname: evil\n---\n");
    symlinkSync(outside, join(w.gsdHome, "agents", "evil.md"));

    const r = linkAgents(w.gsdHome, w.options);
    assert.ok(r.errors.some((e) => e.name === "evil" && e.code === "path-escape"));
    assert.ok(!r.created.includes("evil"));
    assert.equal(existsSync(join(w.pluginAgentsDir, "evil.md")), false);
    assert.equal(r.created.length, 3);
  });

  test("refuses unsafe file names", () => {
    const w = world({ agents: [] });
    writeFileSync(join(w.gsdHome, "agents", "-rf.md"), "x");
    writeFileSync(join(w.gsdHome, "agents", "..hidden.md"), "x");
    const r = linkAgents(w.gsdHome, w.options);
    assert.deepEqual(r.created, []);
  });

  test("dry-run changes nothing", () => {
    const w = world();
    const r = linkAgents(w.gsdHome, { ...w.options, dryRun: true });
    assert.equal(r.dryRun, true);
    assert.equal(r.created.length, 3);
    assert.equal(existsSync(w.pluginAgentsDir), false);
  });

  test("reports a conflict instead of linking when the plugin dir is the GSD agents dir", () => {
    const w = world();
    const r = linkAgents(w.gsdHome, { ...w.options, pluginAgentsDir: join(w.gsdHome, "agents") });
    assert.equal(r.created.length, 0);
    assert.equal(r.errors[0].code, "conflict");
  });

  test("never throws when gsdHome is missing", () => {
    const w = world({ install: false });
    const r = linkAgents(join(w.root, "absent"), w.options);
    assert.deepEqual(r.created, []);
    assert.equal(r.counts.candidates, 0);
    assert.equal(existsSync(w.pluginAgentsDir), false);
  });
});

describe("probe", () => {
  test("not installed: exit code 3, never throws", () => {
    const w = world({ install: false });
    const p = probe({ ...w.options, spawn: true });
    assert.equal(p.schema, 1);
    assert.equal(p.ok, false);
    assert.equal(p.status, "not-installed");
    assert.equal(exitCodeFor(p), EXIT.notInstalled);
    assert.equal(EXIT.notInstalled, 3);
    assert.ok(p.problems[0].includes("GSD_CORE_HOME"));
  });

  test("degraded before linking (agents missing): exit code 2; healthy after linking: exit code 0", () => {
    const w = world();
    const before = probe({ ...w.options, spawn: true });
    assert.equal(before.status, "degraded");
    assert.equal(exitCodeFor(before), 2);
    assert.deepEqual(before.agents.missing, ["gsd-codebase-mapper", "gsd-debugger", "gsd-planner"]);

    writeFileSync(join(w.userAgentsDir, "gsd-planner.md"), "user copy");
    linkAgents(w.gsdHome, w.options);
    const after = probe({ ...w.options, spawn: true });
    assert.equal(after.ok, true, JSON.stringify(after.problems));
    assert.equal(exitCodeFor(after), 0);
    assert.equal(after.version, "9.9.9");
    assert.equal(after.packageName, "@opengsd/gsd-core");
    assert.equal(after.runtime, "kimi");
    assert.deepEqual(after.agents, { authoritative: 3, userRegistered: 1, pluginLinked: 2, missing: [] });
    assert.equal(after.skills.gsdCount, 1);
    assert.deepEqual(after.omp, { pluginLinked: false, enabled: false });
  });

  test("degraded when gsd-tools is missing", () => {
    const w = world();
    rmSync(join(w.gsdHome, "bin"), { recursive: true });
    const p = probe({ ...w.options, spawn: true });
    assert.equal(p.status, "degraded");
    assert.equal(p.gsdTools, false);
    assert.equal(exitCodeFor(p), 2);
  });

  test("degraded when runtime-identity fails or prints garbage", () => {
    for (const tools of [`process.exit(1);`, `process.stdout.write("not json");`, `process.stdout.write("{}");`]) {
      const w = world({ tools });
      const p = probe({ ...w.options, spawn: true });
      assert.equal(p.status, "degraded", tools);
      assert.ok(p.problems.some((m) => m.startsWith("runtime-identity")), tools);
      assert.equal(p.packageName, null);
      assert.equal(p.version, "9.9.9", "falls back to the VERSION file");
    }
  });

  test("a hanging runtime-identity is cut off by the timeout", () => {
    const w = world({ tools: `setInterval(() => {}, 1000);` });
    const started = Date.now();
    const p = probe({ ...w.options, spawn: true, timeoutMs: 300 });
    assert.ok(Date.now() - started < 4000);
    assert.equal(p.status, "degraded");
    assert.ok(p.problems.some((m) => m.startsWith("runtime-identity failed")));
  });

  test("without spawn it does not run gsd-tools", () => {
    const w = world({ tools: `require("node:fs").writeFileSync(${JSON.stringify(join(tmpdir(), "gsd-omp-should-not-exist"))}, "x");` });
    const p = probe({ ...w.options, spawn: false });
    assert.equal(existsSync(join(tmpdir(), "gsd-omp-should-not-exist")), false);
    assert.equal(p.packageName, null);
    assert.equal(p.version, "9.9.9");
  });

  test("reports the omp link state from the plugin manager directory", () => {
    const w = world();
    const nm = join(w.options.ompPluginsDir, "node_modules");
    mkdirSync(nm, { recursive: true });
    mkdirSync(w.options.pluginRoot, { recursive: true });
    symlinkSync(w.options.pluginRoot, join(nm, "gsd-core-omp"));
    writeFileSync(join(w.options.ompPluginsDir, "omp-plugins.lock.json"), JSON.stringify({ plugins: { "gsd-core-omp": { enabled: true } } }));
    assert.deepEqual(probe(w.options).omp, { pluginLinked: true, enabled: true });
  });
});

describe("syncAgents", () => {
  test("links when installed and returns link=null when not, without throwing", () => {
    const w = world();
    const ok = syncAgents(w.options);
    assert.equal(ok.link.created.length, 3);
    const none = syncAgents({ ...w.options, env: { GSD_CORE_HOME: join(w.root, "absent") } });
    assert.equal(none.link, null);
    assert.equal(none.probe.status, "not-installed");
  });
});

describe("bin scripts", () => {
  const run = (script, args, env) =>
    spawnSync(process.execPath, [join(pluginRoot, "bin", script), ...args], {
      encoding: "utf8",
      env: { PATH: process.env.PATH, ...env },
      timeout: 20000,
    });

  test("probe prints one JSON object and exits 3 when GSD-Core is absent", () => {
    const w = world({ install: false });
    const r = run("gsd-omp-probe.mjs", ["--json"], { HOME: w.home, GSD_CORE_HOME: join(w.root, "absent") });
    assert.equal(r.status, 3, r.stderr);
    assert.equal(r.stdout.trim().split("\n").length, 1);
    assert.equal(JSON.parse(r.stdout).status, "not-installed");
  });

  test("probe exits 2 when degraded and reports the gsd-tools identity", () => {
    const w = world();
    const r = run("gsd-omp-probe.mjs", [], { HOME: w.home, GSD_CORE_HOME: w.gsdHome });
    assert.equal(r.status, 2, r.stderr);
    const json = JSON.parse(r.stdout);
    assert.equal(json.packageName, "@opengsd/gsd-core");
    assert.equal(json.status, "degraded");
  });

  test("link --dry-run reports and writes nothing; unknown flags exit 64", () => {
    const w = world();
    const r = run("gsd-omp-link.mjs", ["--dry-run"], { HOME: w.home, GSD_CORE_HOME: w.gsdHome });
    assert.equal(r.status, 0, r.stderr);
    const json = JSON.parse(r.stdout);
    assert.equal(json.link.dryRun, true);
    assert.equal(json.link.created.length, 3);
    assert.deepEqual(readdirSync(join(pluginRoot, "agents")).filter((f) => f.endsWith(".md")), []);
    assert.equal(run("gsd-omp-link.mjs", ["--wat"], {}).status, 64);
  });

  test("link exits 3 when GSD-Core is not installed", () => {
    const w = world({ install: false });
    const r = run("gsd-omp-link.mjs", ["--dry-run"], { HOME: w.home, GSD_CORE_HOME: join(w.root, "absent") });
    assert.equal(r.status, 3);
    assert.equal(JSON.parse(r.stdout).link, null);
  });
});
