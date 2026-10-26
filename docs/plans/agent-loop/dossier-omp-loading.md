# Dossier: omp (v18.12.5) loading of sibling plugins under `omp acp`, hermetic launch

Date 2026-10-26. Author: w1-probe-1. No model call was made; `session/prompt` was never sent. All writes were under `/tmp/omp-probe/`.
Probe: `/tmp/omp-probe/probe.mjs` (Node, ACP v1 NDJSON). Wrapper: `/tmp/omp-probe/run.sh <label> [--settle ms] -- <omp acp flags>` (runs the probe under `strace -f`, then prints `sum.mjs` + `scan.mjs`). Raw per-run output: `/tmp/omp-probe/out.<label>.json`, `strace.<label>.txt`. Ctx probes: `ctxprobe.ts`, `ctxprobe2.ts`. Env overrides: `PROBE_CWD` (session cwd), `PROBE_TRACE_EXTRA` (extra strace syscalls), `PROBE_NO_STRACE=1`.

## 1. Hermetic isolation (what makes omp ignore user-global config)

Source: `pi-utils/src/dirs.ts`, `config/settings.ts`, `discovery/*`; confirmed by strace (no `~/.omp|.config|.agents|.claude|.gsd|.cursor` read in ANY run) and by trap files.

- `HOME=<sandbox>/home` is REQUIRED. omp resolves `os.homedir()` from it: `~/.omp/plugins` (npm/linked plugins), `~/.omp/logs`, `~/.omp/cache`, `~/.agents/skills` (GSD skills!), `~/.claude`, `~/.codex`, `~/.bun` cache.
- `PI_CODING_AGENT_DIR=<sandbox>/agent` is REQUIRED. It replaces `~/.omp/agent` (config.yml `extensions:`, `extensions/`, `agents/`, `commands/`, `agent.db` credentials/settings, `sessions/`, `models.db`). Documented in `omp --help` ("Session storage directory").
- `PI_CONFIG_DIR` renames `.omp` (unused); `PI_CONFIG_FILES` (path-delimited) and `--config <overlay.yml>` add settings overlays (not needed).
- Trap experiment (inside the sandbox): a skill in `$HOME/.agents/skills/trapg` and an extension in `$PI_CODING_AGENT_DIR/extensions/trapg.ts` DO load (`skill:trapg`, `/trapglobalext`) => those sandbox paths are exactly where user-global config would be; pointing the two env vars at an empty dir therefore removes the user's.
- `--no-extensions` is NOT what makes the run hermetic w.r.t. user config (the empty sandbox does that). What it adds: it also blocks PROJECT-level extensions (`<cwd>/.omp/extensions/*.ts`, project `.omp/config.yml extensions:`; trap `trapext` loaded without the flag, dropped with it) and the bundled built-in extension commands `/annotate /green /review` (observed removed in every `--no-extensions` run, also with `--trusted-extension`). It does NOT block project skills (`<cwd>/.omp/skills`, trap `trapskill`) or project `.claude/commands` (trap `trapcmd`); `--no-skills` kills all skills including `-e` package skills (`l1`: ultrathink extension commands load, 0 skills).
- `-e` still works with `--no-extensions` (explicit-only mode, source `main.ts` 1747-1858, `omp-extension-roots.ts`): runs i2/i4/j3.
- A project's own `.omp/config.yml` is honored when cwd is inside it: `~/src/repos/claude-ultrathink/.omp/config.yml` declares `extensions: [~/src/repos/agent-swarm/omp, /var/tmp/ut-verify/wt-swarm/omp]`. Never launch with cwd inside that repo without `--no-extensions` (would double-load swarm).

## 2. Flag semantics under `omp acp` (source + experiment)

`commands/acp.ts` parses the same flag table as the root command and forces `mode=acp`; so all launch flags apply.
- `-e/--extension <path>` (also `--hook`): an OMP extension PACKAGE ROOT or a module file. For a dir it reads `package.json` `omp.extensions`, runs the factory per session, AND wires the package's `skills/`, `commands/`, `hooks/`, `tools/`, `agents/` (task agents), `rules/`, `prompts/`, `.mcp.json` (`discovery/omp-plugins.ts`, `task/discovery.ts` l.113-119).
- `--plugin-dir <dir>`: Claude-plugin-layout root only (`injectPluginDirRoots`, `discovery/helpers.ts` 1508): provides skills, commands (namespaced `<plugin>:<cmd>`), agents. It does NOT run `omp.extensions` factories (no `/swarm`, no `swarm_*` tools, no `/ultrathink-swarm|ui`). It is a distinct mechanism from `-e`.
- `--trusted-extension <FILE>`: exact module FILES only (dirs rejected), disables discovery, no sibling skills/commands/agents; any load error is fatal. Useful for the two substrate projector files only if skill/agent wiring is not wanted.
- `--no-extensions`: see above. `--no-skills`: kills all skill discovery. `--config`: extra settings overlay.
- Nonexistent paths: `-e /nope` and `--plugin-dir /nope` are SILENTLY IGNORED: initialize and session/new succeed, no stderr, no notification (runs j1/j2/j3). `--trusted-extension /nope` throws at startup (source). OmpOS must pre-validate every path itself.
- Path of a missing extension FILE passed to `-e` was not separately tested (only a missing dir/path) - covered by j2 (same silent behavior).

## 3. Results matrix (cwd `/tmp/omp-probe/work`, fresh sandbox; commands = names from `available_commands_update`; base = 51 commands)

| run | flags | session/new | new slash commands | new skills | notes |
|---|---|---|---|---|---|
| a | (none) | ok | - (51 built-ins) | 0 | hermetic baseline; no ext. 0 files outside sandbox |
| b | `-e claude-ultrathink` | ok | ultrathink-off/on/quick/skip/status/track, **ultrathink-swarm, ultrathink-ui** | 6 (`skill:ultrathink-{cursor,kickoff,plan,ship,sync,teach}`) | extension factory ran (swarm/ui are registered only by `src/host/omp.ts` l.1993/2016) |
| c | `--plugin-dir claude-ultrathink` | ok | `ultrathink:ultrathink-{off,on,quick,skip,status,track}` (namespaced, from `commands/*.md`) | same 6 | no extension; no ultrathink-swarm/ui |
| d | b + c | ok | both un-namespaced AND `ultrathink:`-namespaced copies (71 total) | 6 | DOUBLE registration of 6 commands (no warning, no stderr) - do not combine |
| e | `-e agent-swarm/omp` | ok | `swarm` | 407 (`skill:a01-orchestrator`..`a30-custody-compliance` + `--` sub-skills, `swarm-*`) | extension ran: 8 `swarm_*` tools (swarm_status/plan/assignment/ingest/transition/gate/catalog/tool_call); 30 agents a01..a30 in `task` tool description (ctxprobe2, run k1) |
| f | `--plugin-dir agent-swarm/omp` | ok | none | 407 | agents registered (30, ctxprobe2 run k2) BUT no `/swarm`, no `swarm_*` tools |
| g | e + f | ok | `swarm` | 407, no dup | no warning (dedup by path) |
| h | `-e substrate-status.ts -e substrate-skills.ts` | ok | `substrate` | 0 | substrate-status polls `127.0.0.1:7410` (observed `connect`) |
| i1 | `-e U -e W -e P/status -e P/skills` | ok | substrate, swarm, 8 ultrathink-* (474 total) | 413 (387 are `aNN-*` agent skills), 0 duplicate names | RECOMMENDED |
| i2 | `--no-extensions` + i1 | ok | same 10 (471 total) | 413 | built-ins annotate/green/review removed |
| i3/i4 | i1 / i2 with cwd = trap project `work2` | ok | i3 adds `trapext`,`trapcmd`; i4 adds only `trapcmd` | +1 (`trapskill`) | `--no-extensions` blocks project extension, not project commands/skills |
| j1 | `--plugin-dir /nope` | ok | - | - | silently ignored |
| j2 | `-e /nope` | ok | - | - | silently ignored |
| j3 | i2 + `--plugin-dir /nope` | ok | same as i2 | 413 | silently ignored |
| j4 | `--trusted-extension substrate-status.ts` | ok | `substrate`; built-ins annotate/green/review removed | 0 | exact-file allowlist |

Not in the ACP command list: swarm agents (a01..a30) and ultrathink hooks. Agents verified via the extension API (`pi.getAllTools()` -> `task` description lists 30 agents). `omp agents` only supports `unpack` of bundled agents (no list), so it is not a probe. Skills appear in ACP as `skill:<name>`; plugin slash commands appear WITHOUT `/`.

Startup: `initialize` ~1-2 s, `session/new` returns after ~7-12 s total per run under strace (extension-bearing runs ~10-13 s).

## 4. ctx.mode / ctx.hasUI seen by extensions under `omp acp`

Source: `modes/acp/acp-agent.ts` 2584-2664 passes `"rpc"` to `extensionRunner.initialize(..., uiContext, "rpc")` and the real `createAcpExtensionUiContext` (not `noOpUIContext`), so `hasUI()` (= `uiContext !== noOpUIContext`) is true. Experiment (ctxprobe, session_start): `{mode:"rpc", hasUI:true, ui.supportsEditor:false}`, `ctx.ui` has every method.
- `ui.setWidget/setStatus/setFooter/setHeader/setTitle/setWorkingMessage/custom/setEditorText` are NO-OPS; `ui.notify` only does `logger.debug` (NEVER reaches the ACP client); `select/confirm/input/askDialog` work only if the client advertises `elicitation.form`, else return undefined/false.
- Consequences: swarm bar mode (`swarm_bar.ts` 2575) = `hasUI===true && mode==="tui"` -> "ui"; `hasUI===false && mode in {print,json}` -> "headless"; ACP is neither -> "off" (bar never mounts). Ultrathink TUI insight/bar require `ctx.mode==="tui"` (omp.ts 1546, 1681) -> never mount. Ultrathink's subagent heuristic (omp.ts 842) `hasUI !== false -> not a subagent`. Swarm `notify` guarded by `ctx.hasUI` is a silent no-op, and `holdTimeoutMs` takes the UI branch. => OmpOS cannot rely on widgets or `notify` for stage visibility under ACP; project stages from tool calls/`session/update` and from files/events the plugins write (swarm `.swarm/events.jsonl`, ultrathink state dir) or pi.events bus channels (`substrate:status`, `token-opt:savings`).
- Session lifecycle: `session_start` fires in `session/new` after extension binding; `session_shutdown` fires when the process gets SIGTERM (observed in ctx.log).

## 5. RECOMMENDED argv + env

argv (per-launch; absolute paths must exist - validate first because omp ignores missing ones silently):

```
omp acp --no-extensions \
  -e ~/src/repos/claude-ultrathink \
  -e ~/src/repos/agent-swarm/omp \
  -e ~/src/repos/agent-substrate/packages/projector/omp/substrate-status.ts \
  -e ~/src/repos/agent-substrate/packages/projector/omp/substrate-skills.ts
```
(in OmpOS the three repo roots come from the pinned submodule paths.) Do NOT add `--plugin-dir` for these three (adds nothing with `-e`, and double-registers ultrathink commands). `--no-extensions` is recommended only if the workspace cwd may contain project extensions or sits in claude-ultrathink; it costs `/annotate /green /review`. Without it everything else is identical (i1 vs i2). Do not pass `--no-skills`.

env (names; * = per-session/per-workspace value, rest per-OmpOS-installation):
- `HOME=<omp-managed>/home` (REQUIRED, isolation)
- `PI_CODING_AGENT_DIR=<omp-managed>/agent` (REQUIRED, isolation; holds agent.db credentials, sessions)
- `ULTRATHINK_UPLIFT=0` (disable prompt rewrite; or `1` when OmpOS wants the uplift stage on) - per session*
- `ULTRATHINK_STATE_DIR=<omp-managed>/ultrathink` (otherwise derived from agent dir/HOME; override ignored if it is a `.planning` path)
- `SWARM_DIR=<workspace-or-managed>/swarm`* (event log/`results`; default is `<git toplevel>/.swarm`, so a real workspace gets `.swarm/` created)
- `SWARM_ROOT` (optional, workspace root for swarm python bridge)*
- `SWARM_GSD_STATE_FILE=` (empty), `SWARM_CLAUDE_STATE_DIR=<managed>/claude-state`
- `SUBSTRATE_OMP_PENDING_DIR=<managed>/substrate-pending` (REQUIRED: substrate-skills default is hardcoded `~/.omp/agent/substrate-pending`, a real-home write path)
- `SUBSTRATE_TOKENS_DIR=<managed>/substrate-tokens` (default `/etc/substrate/tokens` is read by both substrate extensions even when URL is unset)
- `SUBSTRATE_URL`: if UNSET, substrate-status still polls the default `http://127.0.0.1:7410` (observed `connect`); it fails soft ("unreachable"). Set it to the OmpOS-managed substrate URL when one exists.
- `SUBSTRATE_TOKEN` / `SUBSTRATE_TOKEN_FILE` only when OmpOS runs a substrate server.
- Provider credentials (e.g. `ANTHROPIC_API_KEY`) via env or `omp login` against the managed agent dir: NOT tested (see UNKNOWNs).
- Optional: `XDG_CONFIG_HOME|XDG_DATA_HOME|XDG_CACHE_HOME|XDG_STATE_HOME` under the managed home (I set them; omp derived paths from HOME anyway, not shown to be needed).
- Leave unset: Linear/Notion/Greptile/Hindsight/RAGFlow tokens, `OLLAMA_HOST`/`OLLAMA_BASE_URL`.

cwd = the user's workspace (per session `session/new { cwd }`); omp keys sessions under `$PI_CODING_AGENT_DIR/sessions/<cwd-slug>/`.

## 6. Side effects observed

- Outside `/tmp/omp-probe`: NONE written or read in any of ~45 runs (strace of openat/mkdir/rename/unlink/symlink/connect, children included; real-home reads list empty). `find ~/.omp ...-newer` was polluted by sibling omp processes (parent session, other agents write logs/sessions/token-opt there), so strace is the proof, not find.
- Inside the sandbox: `home/.omp/{logs,cache/legacy-pi-extension-cache.db,run/daemons,plugins(dir read)}`, `home/.cache/bun` (transpile cache, `.pile`), `agent/{agent.db,models.db,skill-descriptions.db}`, one empty session jsonl per `session/new` under `agent/sessions/-tmp-omp-probe-work/`. Swarm/claude-state/substrate dirs stayed empty after session_start (no eager writes).
- Network (every run, even baseline): loopback model-discovery probes to `127.0.0.1:11434` (ollama), `:8080` (llama.cpp), `:1234` (lm-studio); they log `model discovery failed` warnings into `home/.omp/logs`.
- Network with substrate-status loaded: `127.0.0.1:7410` connect on `session_start`/poll.
- Network UNEXPLAINED: in 2 of ~45 runs (t2 `--no-extensions` in work2, l1 `--no-skills -e ultrathink`) a TCP/443 connection to Cloudflare addresses (172.67.75.165, 104.26.10.114/11.114) occurred in the omp main process. Not reproduced in 13 reruns (hunt1..10, l1x..l3x). No tracker env was set. See UNKNOWN-1.
- No double-registration warnings, no stderr lines in any run; d (`-e` + `--plugin-dir` on the same package) double-registers commands silently.

## 7. UNKNOWNs (with resolving commands)

1. UNKNOWN: host of the intermittent outbound HTTPS connection. Resolve: loop `PROBE_TRACE_EXTRA=sendto,sendmmsg bash /tmp/omp-probe/hunt.sh` (stops at first `htons(443)`, prints the DNS `sendto` payload); or run under `bwrap --unshare-net` to prove nothing but the LLM needs network. Hypothesis: background model catalog refresh (`modelRegistry.refreshInBackground()` for non-interactive modes, `main.ts` ~2330) with an empty `models.db`.
2. UNKNOWN: whether `session/prompt` works with injected credentials and what `session/new` then reports for `models`. Resolve (needs approval, uses a credential): `HOME=... PI_CODING_AGENT_DIR=... omp login` or env `<PROVIDER>_API_KEY`, then rerun `node /tmp/omp-probe/probe.mjs x --` and check `sessionNew.models > 0`. Without credentials `initialize` returns `authMethods:["agent"]` and `session/new` still succeeds with 0 models, which is enough to enumerate commands.
3. UNKNOWN: behavior of slash commands `/ultrathink-status`, `/swarm`, `/substrate` when executed (they require `session/prompt`); only their registration was verified. Resolve via an approved prompt run.
4. UNKNOWN: ultrathink `before_agent_start` hook behavior (`ULTRATHINK_UPLIFT=0` honored?) and whether ultrathink `hooks/` JSON (Claude-style) are wired by `-e`. Resolve: read `src/host/omp.ts` l.2289 and run an approved prompt with `ULTRATHINK_UPLIFT=0`.
5. UNKNOWN: can the loopback model-discovery probes be disabled (settings key)? Resolve: `grep -n "ollama\|llama.cpp\|lm-studio" ~/.local/share/reflex/bun/install/global/node_modules/@oh-my-pi/pi-coding-agent/src/config/model-discovery.ts` then a `--config overlay.yml` run.
6. UNKNOWN: `omp plugin link` path (settled decision q1b) interplay: not exercised because it writes to `~/.omp/plugins` (inside the sandbox it would be `home/.omp/plugins`); `-e` made it unnecessary.
7. UNKNOWN: whether multiple concurrent ACP processes share `agent.db` safely (sandbox sqlite WAL); not stress-tested.
