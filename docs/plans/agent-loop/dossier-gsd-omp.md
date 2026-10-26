# Dossier: GSD-Core source + omp plugin packaging (w0-discovery-4)

Date 2026-10-26. All facts observed unless marked `UNKNOWN: <cmd>`. Nothing modified except this file.

## 1. GSD-Core source of truth

### 1.1 Identity
- Installed tree: `~/.agents/gsd-core` — NOT a git checkout (`git -C ~/.agents/gsd-core remote -v` -> `fatal: not a git repository`).
- `node ~/.agents/gsd-core/bin/gsd-tools.cjs runtime-identity --raw` ->
  `{"packageName":"@opengsd/gsd-core","version":"1.18.0"}`
- `~/.agents/gsd-core/VERSION` = `1.18.0` (no trailing newline); `~/.agents/gsd-core/.gsd-runtime` = `kimi`.
- Package: `@opengsd/gsd-core` 1.18.0 (npm `latest`; dist-tag `next` = 1.7.0-rc.6). `npm view` ->
  `repository.url = 'git+https://github.com/open-gsd/gsd-core.git'`, `homepage = https://github.com/open-gsd/gsd-core`,
  `license = 'MIT'`, bugs `https://github.com/open-gsd/gsd-core/issues`, maintainers trek-e, solvely-colin, jeremymcs.
  `bin`: `gsd-core` -> `bin/install.js`, `gsd-tools` -> `gsd-core/bin/gsd-tools.cjs`, `gsd_run` -> `gsd-core/bin/gsd_run`, `gsd-mcp-server` -> `bin/gsd-mcp-server.js`. `engines`: node >=24, npm >=10 (host node = v24.18.0).
- GitHub (gh api): `open-gsd/gsd-core`, public, default branch **`next`** (also `main`), license MIT, last release/tag **v1.18.0** (2026-10-19). Tag v1.18.0 sha `35f5a452239b897b8d6352c973e1e4efd683370f`; `main` head `77682909449b8f4fbeee37a57b98ce1dd260c46b` (2026-10-26 read).
- Local clones: none under `~/src/repos` (`ls ~/src/repos | grep -i -E 'gsd|get-shit'` empty). `find / -maxdepth 4 -name 'gsd-core*'` finds only per-runtime install copies: `~/.agents/gsd-core`, `~/.agents/gsd-local-patches/gsd-core`, `~/.augment/`, `~/.claude/`, `~/.cline/`, `~/.codebuddy/`, `~/.codeium/windsurf/`, `~/.codex/`, `~/.config/kilo/`, `~/.config/opencode/` (+ more). The omp-relevant one is `~/.agents/gsd-core` (Kimi-flavoured install; `.gsd-runtime`=kimi).
- Upstream repo root listing: `agents bin capabilities commands docs gsd-core hooks pi scripts skills src tests vscode .claude-plugin .opencode … package.json LICENSE`. `package.json` has `"pi": {"extensions": ["./pi/gsd.cjs"]}` (pi.dev host, `@earendil-works/pi-coding-agent` API; install = copy to `~/.pi/agent/extensions/gsd.js`) and `files` incl. `pi`, `agents`, `skills`, `commands`. **There is no omp/oh-my-pi runtime capability** (`capabilities/` has: claude, codex, cursor, kimi, kimi-code, opencode, pi, qwen, windsurf, zcode, … no `omp`). Installer `npx @opengsd/gsd-core --help` runtimes: Claude Code, OpenCode, Kimi CLI, Kilo, Codex, Copilot, Antigravity, Cursor, Windsurf, Augment, Trae, Qwen, Hermes, Cline, CodeBuddy, ZCode, pi (`--pi`), Gemini.

### 1.2 How it is installed here
- Install state `~/.agents/gsd-file-manifest.json`: `"manifestVersion":2,"version":"1.18.0","timestamp":"2026-10-21T23:17:17.646Z","mode":"full","runtime":"kimi","scope":"global"`; `~/.agents/.gsd-profile` = `full`; `~/.agents/gsd-install-state.json` (applied migrations incl. `2026-06-02-rename-get-shit-done-to-gsd-core`).
- Installer = `@opengsd/gsd-core` with `--kimi --global` (`capabilities/kimi/capability.json`: configHome kind `generic-agents-root`, probes `~/.config/agents` then `~/.agents` for an existing `skills/`, env `KIMI_CONFIG_DIR`). Exact original command: `UNKNOWN: grep -rn "gsd-core" ~/.bash_history ~/.omp/agent/history.db` (likely `npx @opengsd/gsd-core --kimi --global` or `-c ~/.agents`).
- What it put in `~/.agents` (layout):
  - `gsd-core/` — engine: `bin/gsd-tools.cjs` (289 KB CLI), `bin/lib/*.cjs`, `bin/gsd_run`, `bin/check-latest-version.cjs`, `bin/ensure-runtime-build.cjs`, `bin/verify-reapply-patches.cjs`; `agents/` (authoritative agent profiles, 64 files = 35 agents + 29 `*.compact.md` variants); `commands/gsd/*.md` (72 Claude-style command sources); `workflows/` (109 files); `references/`, `templates/`, `contexts/`, `VERSION`, `.gsd-runtime`. No LICENSE file is shipped in the installed tree.
  - `skills/gsd-*/SKILL.md` — **72 skills** (of 135 entries in `~/.agents/skills`). Format = Kimi wrapper: frontmatter `name: gsd-fast` / `description: "…"`; body "Invoke this Kimi skill with `/skill:gsd-fast`." + `<arguments>$ARGUMENTS</arguments>` + `<execution_context>@$HOME/.agents/gsd-core/workflows/fast.md …</execution_context>`. They reference workflows by `@$HOME/.agents/gsd-core/...` path; workflows say "Agent() call for gsd-phase-researcher, gsd-planner, gsd-plan-checker" (omp equivalent is the `task` tool with `agent: "<name>"`).
  - `agents/` — Kimi mirror: `agents/gsd.yaml`, `agents/subagents/gsd.md|gsd.yaml`, and per-agent `<name>.md` + `<name>.yaml` under `agents/subagents/` (the .md there is **not** omp-compatible: skill gsd-omp-agent-registration says it can lack required frontmatter). `init.manager` reports `agents_dir=~/.agents/agents`, `agent_runtime=kimi`, `agents_installed=true`, `missing_agents=[]` — i.e. GSD's own agent check is about the Kimi mirror, NOT omp registration.
  - `AGENTS.md` (6 lines of GSD guidance, written for Cline wording), `.skill-lock.json` (unrelated skillshare lock).
  - Global GSD config: `~/.gsd/defaults.json` (`runtime: "codex"`, model_profile inherit, workflow flags…); project config `.planning/config.json`. `GSD_HOME` env only relocates the *consent home* (`capability-command-router.cjs:76,129`), NOT the install root; no env var relocates `~/.agents/gsd-core`.
- Counts: **72 gsd skills; 35 agents (+29 compact variants); 72 commands/gsd sources; 109 workflows.**

### 1.3 Agents registered in omp (important gap)
- Authoritative: `~/.agents/gsd-core/agents/<name>.md` (frontmatter `name, description, tools: Read, Write, Edit, Bash, Glob, Grep, Skill, WebFetch, mcp__context7__*…, color`; no `model`). `*.compact.md` share the SAME `name:` as the full file (e.g. gsd-integration-checker.compact.md has `name: gsd-integration-checker`) -> must be excluded or they collide (first-wins dedup by exact name).
- `~/.omp/agent/agents/` currently holds 21 gsd-* files (copies; diff vs authoritative for gsd-planner: one added `model: muse-code/muse-spark-1.3-contributor` line and one removed commented hook line).
- **Missing from omp (14 of 35)**: gsd-advisor-researcher, gsd-assumptions-analyzer, gsd-codebase-mapper, gsd-debug-session-manager, gsd-debugger, gsd-doc-classifier, gsd-doc-synthesizer, gsd-doc-verifier, gsd-doc-writer, gsd-dom-verifier, gsd-eval-auditor, gsd-intel-updater, gsd-mempalace-curator, gsd-user-profiler. (Commands comm/diff computed from `ls`.) Workflows map-codebase, docs-update, debug, intel, profile-user spawn these -> `task` rejects them as unknown.
- Why registration is needed: omp's `task` tool only resolves agent names found by task discovery (`omp://task-agent-discovery.md`), precedence: (1) nearest project `.omp/agents`, (2) user `~/.omp/agent/agents`, (3) `<root>/agents/` of every enabled OMP extension package (explicit `-e`/`--plugin-dir`, settings `extensions:`, installed npm/link plugins), (4) Claude marketplace plugin roots, (5) bundled. `~/.agents/gsd-core/agents` and `~/.agents/agents` are NOT on that list, so GSD agents are invisible until a copy/link exists in 1-3. Skill `gsd-omp-agent-registration` (read; `~/.omp/agent/managed-skills/gsd-omp-agent-registration/SKILL.md`): locate the authoritative profile `~/.agents/gsd-core/agents/<name>.md` (NOT the `agents/subagents` mirror), register unchanged at `~/.omp/agent/agents/<name>.md` only if absent, never overwrite a user profile, never strip `model`, retry; "OMP discovered the new file on the next Task call without restart"; a spawned role is not a passing verdict.
- Parse rules (`parseAgentFields`): `name` + `description` required; `main`/`sub` reserved; `tools` CSV or array (legacy aliases normalised, `Read/Write/...` accepted — the 21 registered copies already work); `model` optional (Claude-dialect plugin manifests discard `model`; OMP-native keep it).

## 2. omp plugin/extension/skills/agents packaging contract (omp v18.12.5)

Sources read: `omp --help`, `omp plugin --help`, `omp://plugin-manager-installer-plumbing.md`, `omp://extension-loading.md`, `omp://extensions.md`, `omp://task-agent-discovery.md`, `omp://skills.md`, `omp://cli-reference.md`, `omp://slash-command-internals.md`.

- **CLI**: `omp --plugin-dir <path>` "Load plugin from directory (repeatable)" (cli-reference.md:157: "Add a local plugin directory to discovery"); `--no-extensions`, `--no-skills`, `--skills <globs>`, `-e/--extension <file>`, `--hook`, `--trusted-extension <abs-path>` (exclusive allowlist; incompatible with -e/--hook). `omp plugin [install|uninstall|list|link|doctor|features|config|enable|disable|marketplace|discover|upgrade] [--json] [--fix] [--force] [--dry-run] [-l/--local] [--scope user|project]`. `omp plugin discover` is a MARKETPLACE command (`omp plugin discover --help` just prints the generic plugin help); not a local-dir tool.
- **`omp plugin link <path>`** -> `PluginManager.link()`: creates symlink `~/.omp/plugins/node_modules/<package.json name>` -> path and lock entry in `~/.omp/plugins/omp-plugins.lock.json` (`{"plugins":{"<name>":{"version","enabledFeatures":null,"enabled":true}},"settings":{}}`). Observed: `ultrathink -> ~/src/repos/claude-ultrathink`, `omp-token-opt -> ~/src/repos/claude-ultrathink/plugins/omp-token-opt`. `omp plugin install <local path>` routes to the same link. Project scope: `--scope project` (nearest project plugin root). `omp plugin list --json` returns `{"npm":[{name,version,path,manifest:{extensions:[...],version},enabledFeatures,enabled}],"marketplace":[...]}`; `omp plugin doctor` prints "✔ plugin:<name>: vX - desc" lines, summary "5 ok, 0 warnings, 0 errors" (works as a machine-ish health check; `--fix` available).
- **Manifest** (`package.json`): `"omp": {"extensions": ["./src/index.ts"]}`; legacy mirror `"pi": {"extensions": [...]}` still accepted (all siblings ship both). Entries may be `.ts/.js/.mjs/.cjs` for installed-plugin manifests. Manifest also supports `omp.tools`, hooks/commands path resolvers (exported but runtime hook/slash discovery uses conventional dirs instead). Feature flags: `omp plugin features`.
- **Extension entry**: default-export factory `(pi: ExtensionAPI) => void|Promise`; load phase allows `registerTool/registerCommand/registerFlag/on(...)`, `exec`, `getFlag`, but session actions throw until initialised; handlers get `ctx` (`ctx.ui.notify(msg,"info"|"warn"…)`, `ctx.cwd`). Unsandboxed; factory runs once per session incl. subagents -> agent-swarm's convention: factory registers only, no fs/spawn at load; do work inside `session_start`. Events: session_start, session_shutdown, before_agent_start, tool_call (30 s fail-closed), tool_result, etc. `resources_discover` exists but is NOT invoked by AgentSession (extensions.md:602-605) -> an extension CANNOT dynamically add skills/agents at runtime.
- **Conventional dirs scanned under an enabled npm/link plugin root (and `--plugin-dir` roots)** by `omp-plugins` capability provider: `skills/<name>/SKILL.md` (one level only; `description` REQUIRED), `hooks/pre|post/`, `tools/`, `commands/*.md`, `rules/`, `prompts/`, `.mcp.json`/`mcp.json`; task discovery scans `agents/*.md`. `--plugin-dir` roots are injected first in precedence. A root `plugin.json` targeting Agent Plugins 1.0.0 switches skills/MCP to the strict portable layout (avoid). Skills dup rules: identical body+frontmatter => silently dropped; differing same name => higher-precedence keeps bare name, other gets `<namespace>/<name>` (namespace = plugin package name for omp-plugins).
- **Skills user-level discovery**: the `agents` provider scans `~/.agents/skills` and `.agents/skills` (toggles `enableAgentsUser/enableAgentsProject`, independent of Claude/Codex toggles; default on). Evidence it works here: all 72 `gsd-*` skills appear in this session's skill list and `skill://gsd-omp-agent-registration` resolves. => **GSD skills need NO plugin work**; `~/.agents/commands/*.md` is likewise scanned for slash commands (none exist for GSD; the Kimi install exposes everything as skills, `/skill:gsd-*`).
- **omp global config** `~/.omp/agent/config.yml` already has `extensions: [~/src/repos/agent-swarm/omp]` (settings-array extension root) and `skills.customDirectories: [~/agent-skills]`; `~/.omp/agent/extensions/` holds `substrate-status.ts`, `substrate-skills.ts` (symlinks into agent-substrate), `rtk.ts` etc. **Symlinked extension files demonstrably load**, so symlink-following is established for extensions; for `agents/*.md` symlinks: `UNKNOWN: mkdir /tmp/gsdp/agents && ln -s ~/.agents/gsd-core/agents/gsd-debugger.md /tmp/gsdp/agents/ && printf '{"name":"x","private":true}' >/tmp/gsdp/package.json && omp --plugin-dir /tmp/gsdp -p "List the task agents named gsd-debugger" --no-session`.
- **Sibling comparison**
  - claude-ultrathink (`~/src/repos/claude-ultrathink/package.json`): `"omp":{"extensions":["./src/host/omp.ts"]}` + `"pi":{…same}`; ships `skills/ultrathink-{cursor,kickoff,plan,ship,sync,teach}/`, `commands/ultrathink-{off,on,quick,skip,status,track}.md` (conventional dirs, no manifest listing); linked as `~/.omp/plugins/node_modules/ultrathink`.
  - `plugins/omp-token-opt/package.json`: name `omp-token-opt` 0.1.0, `omp.extensions ["./src/index.ts"]`, `pi` mirror, `type: module`, bun>=1.2.9, dep gpt-tokenizer; no skills/agents; linked.
  - agent-swarm `omp/` : `package.json` `{"name":"agent-swarm-omp","private":true,"type":"module","omp":{"extensions":["./src/index.ts"]}}`; `omp/agents/a01..a30*.md` (frontmatter `name: "a05-backend"`, `description`, `tools: read, grep, glob, bash, write, edit, swarm_catalog, swarm_tool_call`, `spawns`, `autoloadSkills`, `output` JSON schema) and `omp/skills/`; loaded via config.yml `extensions:` (directory root -> `agents/` auto-scanned). Entry convention: `createSwarmExtension({bridge})` factory registers tools/commands/`pi.on` only.

## 3. Recommended packaging

### 3.1 Submodule or thin plugin? -> (b) OmpOS-owned thin plugin
(a) is technically possible (GSD-Core has its own remote `github.com/open-gsd/gsd-core`, pin tag v1.18.0 `35f5a45…`), but NOT recommended and should not be a 4th submodule:
1. Upstream has no omp manifest; its `package.json` only has `pi.extensions: ["./pi/gsd.cjs"]` (pi.dev API, subprocess-reuse of `gsd-tools.cjs`, installs to `~/.pi/agent/extensions/gsd.js`); hosting an omp manifest would need an upstream PR (not ours; MIT, third party).
2. Linking the upstream checkout would expose root `skills/` (Claude/pi-flavoured, 72 names identical to the installed Kimi ones but with different bodies -> every skill gets a `<namespace>/<name>` alias + collision warnings) and root `agents/` incl. 29 `.compact.md` files with duplicate `name:` values (silent first-wins ambiguity). Net negative.
3. The runtime the workflows actually use is the INSTALLED tree (`~/.agents/gsd-core`, version-stamped by the installer with local patches in `~/.agents/gsd-local-patches`), not a source checkout; a submodule would duplicate/lag it and need Node>=24 build tooling.
4. Brief rule "never copy sibling source" is satisfied by (b) (reference only).
(The upstream remote is still useful as the *install source*: `npx @opengsd/gsd-core@1.18.0 --kimi --global`.)

### 3.2 Layout: `plugins/gsd-core-omp/` in the OmpOS repo (does not exist yet; ompos has no `plugins/` dir and no `.gitmodules`)
```
plugins/gsd-core-omp/
  package.json
  README.md                  # install + probe docs (short)
  .gitignore                 # agents/*.md   (generated symlinks; keep .gitkeep)
  agents/.gitkeep
  src/index.ts               # extension entry (registration only)
  src/gsd-home.ts            # resolveGsdHome(), listAuthoritativeAgents(), linkAgents()
  bin/gsd-omp-probe.mjs      # health probe CLI (JSON on stdout)
  bin/gsd-omp-link.mjs       # idempotent symlink maintainer (same code path as session_start)
```
No skills/ dir (provided natively by `~/.agents/skills`), no commands/ dir (GSD is skill-driven), no copies of any GSD file.

`package.json`:
```json
{
  "name": "gsd-core-omp",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "description": "omp binding for the installed GSD-Core (@opengsd/gsd-core): registers its agents with the task tool and reports health",
  "license": "AGPL-3.0-or-later",
  "engines": { "bun": ">=1.2", "node": ">=24" },
  "omp": { "extensions": ["./src/index.ts"] },
  "pi":  { "extensions": ["./src/index.ts"] },
  "scripts": { "probe": "node bin/gsd-omp-probe.mjs --json", "link-agents": "node bin/gsd-omp-link.mjs" }
}
```
(License choice = OmpOS repo's own; `UNKNOWN: cat ~/src/repos/ompos/LICENSE` — none at repo root listing above; align with whatever OmpOS decides.)

`src/index.ts` sketch (convention of agent-swarm/omp: factory registers only; fs only inside handlers):
```ts
import type { ExtensionAPI } from "@oh-my-pi/pi-coding-agent";
import { resolveGsdHome, probe, linkAgents } from "./gsd-home.ts";

export default function gsdCoreOmp(pi: ExtensionAPI): void {
  pi.on("session_start", async (_e, ctx) => {
    const p = probe();                       // sync fs + VERSION read, no spawn
    if (!p.ok) { ctx.ui.notify(`GSD-Core not found at ${p.gsdHome}. Install: npx @opengsd/gsd-core --kimi --global (or set GSD_CORE_HOME)`, "warn"); return; }
    const r = linkAgents(p);                 // symlink missing authoritative agents into <plugin>/agents; never overwrites; skips *.compact.md and names already in ~/.omp/agent/agents
    if (r.errors.length) ctx.ui.notify(`gsd-core-omp: ${r.errors.length} agent link errors`, "warn");
  });
  pi.registerCommand("gsd-core", {
    description: "GSD-Core binding status (version, agents linked/missing)",
    handler: async (_args, ctx) => ctx.ui.notify(JSON.stringify(probe()), "info"),
  });
}
```
Rationale for symlinks in the plugin's own gitignored `agents/`: `resources_discover` is not wired (cannot add agents dynamically); task discovery scans `<plugin-root>/agents/` of enabled plugin roots at Task-call time (skill notes new files are picked up without restart); user-level files in `~/.omp/agent/agents` (21 copies, with injected `model:`) outrank plugin agents, so the plugin only adds the 14 missing ones and never disturbs existing ones. Symlink (not copy) keeps the authoritative profile the single source and tracks `npx @opengsd/gsd-core` upgrades. Symlink target must be absolute and machine-local => therefore generated, never committed. `UNKNOWN` whether omp's agent reader follows symlinks (see test command in §2); fallback if not: have `linkAgents` hard-copy is forbidden by the "reference not copy" rule -> instead register via `omp --plugin-dir` + generated `agents/` files written with `fs.copyFile` ONLY as documented fallback decided by the orchestrator (record as OQ).

### 3.3 Paths the plugin must resolve
Resolution order for the GSD install root (`gsdHome`, dir containing `bin/gsd-tools.cjs`, `agents/`, `VERSION`):
1. env `GSD_CORE_HOME` (OmpOS-defined override; GSD itself defines none — `GSD_HOME` means something else, do not reuse),
2. `$KIMI_CONFIG_DIR/gsd-core` (the Kimi installer's own override env),
3. `~/.config/agents/gsd-core`, then 4. `~/.agents/gsd-core` (the installer's probe order; present here).
Derived: `skillsDir = dirname(gsdHome)/skills` (only for the report), `toolsCli = gsdHome/bin/gsd-tools.cjs`, `agentsAuthoritative = gsdHome/agents/*.md` minus `*.compact.md`, `ompAgentsUser = ~/.omp/agent/agents` (honour `PI_CODING_AGENT_DIR`/`--profile`: `UNKNOWN: omp config path for agent dir under --profile`).
Degradation: missing gsdHome => `probe().ok=false`, notify once with install command, register no agents, never throw (extension errors are swallowed per handler but a throw in the factory logs a load error).

### 3.4 Health probe (machine readable)
`node plugins/gsd-core-omp/bin/gsd-omp-probe.mjs --json` (exit 0 healthy, 2 degraded, 3 not installed) -> stdout, one JSON object (proposed schema):
```json
{"schema":1,"ok":true,"gsdHome":"~/.agents/gsd-core","packageName":"@opengsd/gsd-core","version":"1.18.0","runtime":"kimi",
 "node":"v24.18.0","gsdTools":true,
 "skills":{"dir":"~/.agents/skills","gsdCount":72},
 "agents":{"authoritative":35,"userRegistered":21,"pluginLinked":14,"missing":[]},
 "omp":{"pluginLinked":true,"enabled":true}}
```
Building blocks that already exist and can be used verbatim (verified):
- `node ~/.agents/gsd-core/bin/gsd-tools.cjs runtime-identity --raw` -> `{"packageName":"@opengsd/gsd-core","version":"1.18.0"}` (pure local read, ~0.3 s).
- `omp plugin list --json` -> filter `.npm[]|select(.name=="gsd-core-omp")` (`enabled:true`, `manifest.extensions`).
- `omp plugin doctor` -> line `✔ plugin:gsd-core-omp: v0.1.0 - …`.
- ompos-side install: `omp plugin link /abs/path/plugins/gsd-core-omp` (persistent) or per-launch `omp acp --plugin-dir /abs/path/plugins/gsd-core-omp` (no `~/.omp` mutation; `--plugin-dir` roots rank first for agents/skills and are exempt from the claude-plugins opt-in). For OmpOS's ACP launch prefer `--plugin-dir` so the plugin is pinned to the repo path.

## 4. Reading GSD state from C# (Kanban task context) — all run in ~/src/repos/ompos
Invocation: `node ~/.agents/gsd-core/bin/gsd-tools.cjs query <name> [args] [--raw] [--pick <field>] [--project-dir <root>] [--json-errors]`. Each call ≈ 0.35–0.40 s wall (node cold start; no daemon), JSON on **stdout**, warnings on **stderr** (harmless, exit 0): `gsd-tools: warning: unknown config key(s) in .planning/config.json: quick_branch_template …` and `…~/.gsd/defaults.json sets model_profile, commit_docs, … (#3532)`. Errors: exit 1 + `{"ok":false,"reason":"sdk_unknown_command","message":"Unknown command: …"}` (with `--json-errors`, on stderr). Verified read-only: STATE.md/ROADMAP.md mtimes unchanged after running the queries below (`state.json` query synthesises `last_updated` = now; do not diff on it).

### 4.1 Direct file reads (cheapest, stable)
- `.planning/STATE.md` YAML frontmatter (observed):
```yaml
gsd_state_version: "1.0"
milestone: v0.10.0
milestone_name: Cross-platform UI
status: paused            # free-text lifecycle word
last_updated: "2026-10-25"
last_activity: 2026-10-25
progress: {total_phases: 6, completed_phases: 3, total_plans: 8, completed_plans: 8, percent: 50}
```
  Body has `## Project Reference`, `**Current focus:**`, `## Current Position` (`Phase:`, `Plan:`, `Status:`, `Last activity:`) — free text, long (672 lines / 73 KB), parse frontmatter only.
- `.planning/ROADMAP.md` (872 lines): phase checklist lines `^- \[( |x)\] \*\*Phase (\d+(\.\d+)?): (.+?)\*\* - (.*)$` (e.g. `- [x] **Phase 20: Avalonia Foundation** - …`, `- [ ] **Phase 22: Desktop Heads** - …`), per-phase `### Phase N: Name` sections with `**Plans**:` and plan checklists `- [x] 20-01-PLAN.md: Wave 1, …`. NOTE the checklist spans ALL milestones (phases 1–38); STATE frontmatter is v0.10.0-scoped.
- `.planning/phases/NN-slug/NN-CONTEXT.md | NN-MM-PLAN.md | NN-SUMMARY.md | NN-VERIFICATION.md` (e.g. `phases/21-parity-surface/{21-01-PLAN.md,21-CONTEXT.md,21-SUMMARY.md,21-VERIFICATION.md}`); requirements `.planning/REQUIREMENTS.md` (`- [ ] **XP-01**: …` + traceability table `| XP-01 | 20, 25 | v0.10.0 … |`).
- **TRAP**: `.planning/state.json` is a STALE separate file (`{"contract":"1.0.0","flavor":"core","milestone":"v0.9.2","phases":[],"next":{"command":"/gsd:discuss-phase",…},"updated_at":"2026-10-21T10:22:28.527Z"}`). Do NOT read it; use `query state.json` (computed) or STATE.md.

### 4.2 Query commands (JSON shapes, trimmed)
1. `query roadmap.analyze` (9.6 KB; stable, cheap, best for the board). Top keys: `milestones[{heading,version}]`, `phases[]`, `phase_count`(15), `completed_phases`(2), `total_plans`(13), `total_summaries`(9), `progress_percent`(69), `progress_scope`("complete"), `current_phase`("23"), `next_phase`("22"), `checkbox_conflict[{number,roadmap_complete,disk_status,plan_count,summary_count}]`, `missing_phase_details[]` (phases 1–19 have no detail in active milestone view), `scope`. Phase element: `{number,name,goal,mode,depends_on,plan_count,summary_count,has_context,has_research,disk_status("complete"|"discussed"|"planned"|"executed"),roadmap_complete,context_read_error,context_scope}`.
2. `query init.manager` (largest; dashboard payload): `milestone_version, milestone_name, phase_count, completed_count, in_progress_count, recommended_actions[{phase,phase_name,action:"plan"|"execute"|"verify",reason,command:"/gsd-plan-phase 22"}], waiting_signal(null), all_complete, project_exists, roadmap_exists, state_exists, manager_flags{discuss,plan,execute}, state_path, roadmap_path, archive_dir, project_root, agents_installed, missing_agents[], agents_dir, agent_runtime("kimi"), project_title, phases[]`. Phase element adds to the roadmap.analyze shape: `implementation_complete, verification_status("passed"|…), verification_passed, phase_complete, completion_status, verification_next_action, verification_next_command, verification_route, verification_stale_check_indeterminate, last_activity(ISO), is_active, display_name, deps_satisfied, dep_phases[], deps_display, is_next_to_discuss`. Best single call for Kanban column placement (`recommended_actions` gives next command per phase).
3. `query progress` (compact): `{milestone_version, milestone_name, phases:[{number,name(slugged lowercase),plans,summaries,status:"Complete"|"Pending"|"Planned"|"Executed"}], total_plans, total_summaries, percent, phase_scope}`.
4. `query init.progress`: like progress but with `directory`, `status`, `has_research`, verification fields, `executor_model`, `planner_model`, `commit_docs`.
5. `query state.json` : `{gsd_state_version, milestone, milestone_name, current_phase:"21", current_phase_name (⚠ parsed from STATE body line — here it is a sentence "5 of 5 criteria, verified twice…", unreliable), status:"paused", stopped_at, last_updated(now), last_activity, last_activity_desc, state_head(git sha), progress:{total_phases:15,completed_phases:11,total_plans:13,completed_plans:9,percent:69}, … plus body text}`. Cross-check: STATE frontmatter says percent 50/total_phases 6, query says 69/15 — computations differ (frontmatter is stale/hand-written); prefer queries for numbers.
6. `query state-snapshot`: `{current_phase, current_phase_name, total_phases:null, current_plan:null, total_plans_in_phase:null, status, progress_percent:null, last_activity, last_activity_desc, decisions:[], blockers:[], paused_at:null, session:{last_date,stopped_at,resume_file}}` — body-parse based, many nulls; use for `blockers`/`decisions`/`session.stopped_at` only.
7. `query state.get status` -> `{"status":"paused"}` (single field).
8. `query roadmap.get-phase 21` -> `{found,phase_number,phase_name,goal,mode,success_criteria:[string…],…}` (criteria text incl. "Verify:" clauses) — good for card detail.
9. `query find-phase 21` -> `{found,directory:".planning/phases/21-parity-surface",phase_number,phase_name,plans:["21-01-PLAN.md"],summaries:["21-SUMMARY.md"],plan_count,summary_count,plan_count_all}`.
10. `query phase-plan-index 21` -> `{phase, plans:[{id:"21-01",wave,depends_on[],autonomous,gap_closure,objective,files_modified[],files_deleted[],agent_hint,task_count,has_summary,halted,blocked_by[],ready}], waves:{"1":["21-01"]}, incomplete[], runnable[], ready_plans[], has_checkpoints}` — per-plan Kanban cards.
11. `query init.phase-op 21` -> paths bundle `{phase_found,phase_dir,phase_number,phase_name,phase_slug,padded_phase,has_research,has_context,context_scope,has_plans,has_verification,has_reviews,plan_count,state_path,roadmap_path,requirements_path,context_path,verification_path,project_root,agents_installed,missing_agents,agents_dir,agent_runtime,project_title,commit_docs,…search flags}`.
- Other families advertised by `gsd-tools` usage: `milestone, phase, phases, progress, roadmap, state, requirements, list-todos, list-seeds, learnings, intel, graphify, history-digest, audit-open, audit-uat, loop, worktree, workstream, verify, validate`. `UNKNOWN: node ~/.agents/gsd-core/bin/gsd-tools.cjs query learnings.list` (not run) for the "lessons/shared memory" surface; `.planning/intel/` and `.planning/research/` exist as directories.
- Project flags: `--ws <name>`/`GSD_WORKSTREAM` workstreams; `--project-dir /abs/root` skips ancestor walk-up (REQUIRED for C# since `--cwd` must contain `.planning/` and OmpOS may run from elsewhere).
- Stability ranking for C#: (1) STATE.md frontmatter + ROADMAP checklist regex = zero-process, stable keys but may disagree with computed values; (2) `query roadmap.analyze` / `progress` / `phase-plan-index` = stable, small, cheap; (3) `init.manager` = richest, versioned by gsd-core (shape may change across releases: bind with unknown-tolerant source-generated JSON, `[JsonExtensionData]`, and guard on `runtime-identity` version `1.18.x`); (4) `state.json`/`state-snapshot` text-derived fields are heuristic. Always set 5–10 s timeout, read stdout only, treat non-zero exit as "GSD unavailable", cache per `.planning` mtime to avoid 0.4 s spawns.

## 5. Licence
Upstream `LICENSE` (gh api `open-gsd/gsd-core/contents/LICENSE`):
```
MIT License

Copyright (c) 2026 Open GSD
```
`npm view @opengsd/gsd-core license` = `MIT`; GitHub SPDX `MIT`. The installed `~/.agents/gsd-core` tree ships no LICENSE file. MIT is compatible with OmpOS referencing it (no source copied; plugin only symlinks/reads).

## 6. Risks / UNKNOWNs for the C# OmpOS host
- UNKNOWN: omp agent loader follows symlinked `agents/*.md` (test command §2); fallback is an orchestrator decision (copy forbidden by brief).
- GSD workflows are written for `Agent()`; under omp that is the `task` tool — the 14 missing agents break map-codebase/docs-update/debug/intel/user-profile flows until linked.
- OmpOS launching omp with an isolated `--profile`/custom agent dir may hide `~/.omp/agent/agents` (21 copies) and possibly alter `~/.agents` skill visibility: `UNKNOWN: omp --profile ompos -p "list skills" --no-session`.
- GSD agents installed with the Kimi layout ⇒ any GSD reinstall (`npx @opengsd/gsd-core --kimi`) rewrites `~/.agents/gsd-core`; plugin symlinks stay valid (same path), versions via `runtime-identity`.
- Query numbers disagree (frontmatter 50%/6 phases vs query 69%/15 phases; roadmap.analyze `current_phase` 23 vs STATE current 21); show both with provenance or pick `roadmap.analyze` consistently.
- Node >=24 required by gsd-tools (host has 24.18.0); OmpOS must locate `node` explicitly (no podman/docker involvement).
- `.planning/` in ompos currently has uncommitted modifications (STATE.md, ROADMAP.md, phase summaries) — read-only queries do not change them.
