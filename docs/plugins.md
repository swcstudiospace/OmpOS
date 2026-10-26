# Plugins

OmpOS runs four plugins inside the omp engine. Three are sibling projects pinned as git submodules under `plugins/`; one is first-party and tracked in this repository. All four are omp extensions: OmpOS launches omp with them, observes what they write, and shows what they expose in the default Cursor-style Agent Panel (session rail, thread, composer, and the one-line plugin connection row under the thread header). The panel has no stage panels or inspector of its own (DR-1, [ADR-0012](decisions/ADR-0012-agent-loop-plugins.md#update-2026-10-26)); task context and steering history belong on the Kanban tab card (Phase 39). The plugins are connected to every omp process the application starts, engine sessions over ACP and omp terminals, as described in "Plugins in omp terminals" below (verified without a model turn on Linux; that section lists what is not verified). OmpOS never copies, forks or edits their source. Decision record: [ADR-0012](decisions/ADR-0012-agent-loop-plugins.md). Evidence: [docs/plans/agent-loop/](plans/agent-loop/README.md).

## The loop

```
prompt -> Ultrathink (plan) -> Swarm (subagents) -> Substrate (session state, steering record)
                      GSD-Core (phase and workflow context across all three)
```

| Plugin | Role in the loop |
| ------ | ---------------- |
| Ultrathink | Rewrites the prompt into an XML spec, a Graph of Thought and workflow waves before the agent starts. Runs first. Its session file appears when planning finishes; there is no progress before that. |
| Swarm | A 30-agent subagent orchestrator (agents `a01` to `a30`, tools `swarm_*`, command `/swarm`) with a Python runtime. Runs after Ultrathink. Writes `.swarm/live.json` and `.swarm/events.jsonl`, which OmpOS reads. |
| Substrate | Cross-session state: an MCP server (`POST /brief`, `POST /mcp`, `GET /healthz`), two omp projector extensions (`substrate-status.ts`, `substrate-skills.ts`), leases, handoffs, governed memory and an append-only event ledger keyed by a Graph ID `ut-<ts36>-<8hex>`. OmpOS reads shared memory and lessons and records steering actions. |
| GSD-Core | Phase and workflow context (`.planning/`). GSD-Core upstream has no omp runtime, so `plugins/gsd-core-omp` is a thin OmpOS-owned plugin that exposes the installed GSD-Core agent profiles to omp's `task` tool and reports health. It copies nothing from GSD-Core. |

## Where each plugin lives

| Path | Remote URL | Pin | Licence | Visibility |
| ---- | ---------- | --- | ------- | ---------- |
| `plugins/claude-ultrathink` | `https://github.com/swcstudiospace/ultrathink.git` | `71101ed069a66b3862fa5da5b01995a60c2160c2` (tracks `main`) | AGPL-3.0-or-later | public |
| `plugins/agent-swarm` | `https://github.com/swcstudiospace/swarm.git` | `c900a510db3e50de6e9f8b60453bdded6fdf72df` (tracks `main`) | no LICENSE file in the repository | public |
| `plugins/agent-substrate` | `https://github.com/swcstudiospace/substrate.git` | `754c51a17a9e6ffbc222d04bdaf4e783b4eca720` (tracks `main`) | no LICENSE file in the repository | private |
| `plugins/gsd-core-omp` | tracked in this repository, not a submodule | n/a | OmpOS-owned; no LICENSE file in OmpOS (see Licence facts) | n/a |

`.gitmodules` is the source of truth for URL and branch; the pin is the commit recorded in the OmpOS tree (`git submodule status`). The three repositories were renamed upstream: `claude-ultrathink` to `ultrathink`, `agent-swarm` to `swarm`, `agent-substrate` to `substrate`. The old URLs redirect. The directory names under `plugins/` keep the old names the team uses, so paths, descriptors and docs say `claude-ultrathink`, `agent-swarm` and `agent-substrate`.

## Cloning

```sh
git clone --recurse-submodules https://github.com/swcstudiospace/OmpOS.git
```

For an existing clone:

```sh
git submodule update --init --recursive
```

`./bootstrap.sh` runs the same command and only warns when it fails; the gate does not need the plugin contents. An uninitialised plugin is not an error at startup: the panel opens, the plugin's stage shows Unavailable, and the exception message names the plugin, the path and the fix `git submodule update --init --recursive plugins/<name>`.

### Access to the private substrate repository

`swcstudiospace/substrate` is private. A clone without read access fails on that one submodule. Fix: authenticate to GitHub (for example `gh auth login`) with an account that can read `swcstudiospace/substrate`, then run `git submodule update --init --recursive plugins/agent-substrate`. Without it the substrate plugin stays Unavailable and sessions run without shared memory, leases or a steering record.

In CI, the `build-test` job in `.github/workflows/ci.yml` checks out the two public submodules always, and checks out the private one only when the repository secret `SUBMODULE_TOKEN` exists. The secret holds a read-only token for `swcstudiospace/substrate`. It reaches git only through a masked `http.https://github.com/.extraheader` setting for that one command. When the secret is missing, the job prints a warning and continues. Who provisions the token is open: [OQ-63](open-questions.md). The container build context ignores `plugins/` (`.containerignore`), so no image contains plugin source.

## Bumping a pin

Run from the OmpOS repository root, for one plugin at a time:

```sh
cd plugins/<name>
git fetch origin
git checkout <commit>              # a commit that EXISTS on the sibling's remote
cd ../..
git ls-remote <url> <commit>       # must print the commit; empty output means it is not pushed
git add plugins/<name>
git commit -m "chore(plugins): bump <name> to <short-sha>"
```

To move to the tip of the tracked branch instead: `git submodule update --remote plugins/<name>`, then `git add plugins/<name>` and commit.

Rules:

1. Never edit sibling source inside OmpOS. A change belongs in the sibling repository: change it there, push it, then bump the pointer here.
2. Never pin a commit that is not on the sibling's remote. A pin that exists only in one working copy breaks every other clone and CI. Check with `git ls-remote <url> <sha>` before committing the bump. For the private substrate repository this needs read access.
3. `.gitmodules` keeps remote URLs, never local paths.
4. `plugins/gsd-core-omp` is ordinary tracked code: edit it in this repository, with tests (`npm test` inside it, which runs `node --test`).

## Launch contract

OmpOS starts one `omp acp` child per session with this argument order:

```
omp acp --no-extensions \
  -e plugins/claude-ultrathink \
  -e plugins/agent-swarm/omp \
  -e plugins/agent-substrate/packages/projector/omp/substrate-status.ts \
  -e plugins/agent-substrate/packages/projector/omp/substrate-skills.ts \
  -e plugins/gsd-core-omp
```

(OmpOS passes absolute paths.) Notes, each confirmed by a live probe without a model turn ([dossier-omp-loading.md](plans/agent-loop/dossier-omp-loading.md)):

- Use `-e` only. `--plugin-dir` provides skills and agents but runs no extension code, and `-e` plus `--plugin-dir` on the same package registers its commands twice.
- omp silently ignores a nonexistent `-e` or `--plugin-dir` path and silently ignores unknown flags. OmpOS therefore validates every path before launch and tests assert the exact argument list.
- `--no-extensions` blocks project-level extensions in the session working directory. It also removes the bundled `/annotate`, `/green` and `/review` commands.
- When Substrate is disabled or unhealthy, both substrate `-e` entries are omitted. If they were present with `SUBSTRATE_URL` unset, the status extension would still poll `127.0.0.1:7410`.

Per-session environment, from `SessionStateLayout`, with state under `<stateRoot>/sessions/<sessionId>/`:

| Variable | Value | Condition |
| -------- | ----- | --------- |
| `ULTRATHINK_STATE_DIR` | `<session>/ultrathink` | always |
| `SWARM_DIR` | `<session>/swarm` | always |
| `SWARM_GSD_STATE_FILE` | `<session>/swarm/gsd-state.md` | always; never empty, because the pseudo-terminal backend removes an empty variable and the swarm would then write into the workspace `.planning/STATE.md` |
| `SWARM_CLAUDE_STATE_DIR` | `<session>/claude-state` | always |
| `SUBSTRATE_OMP_PENDING_DIR` | `<session>/substrate-pending` | always (its default is a hard-coded real-home path) |
| `SUBSTRATE_TOKENS_DIR` | configured directory | only when configured |
| `OMPOS_STEER_DIR` | `<session>/steer` | always; the steering inbox of the `gsd-core-omp` plugin (see Steering) |
| `OMPOS_CONNECTION_FILE` | `<session>/connection/report.json` | always; the connection report of the `gsd-core-omp` plugin (see Plugins in omp terminals) |
| `SUBSTRATE_URL` | the Substrate endpoint | only when Substrate is enabled and healthy |
| `ULTRATHINK_UPLIFT` | `0` | only when planning is disabled |

`SWARM_AGENT` and `SWARM_TASK_ID` are never set. Real runs inherit `HOME` and `PI_CODING_AGENT_DIR`, because the user's omp credentials live there. Only tests and an explicit isolated-home setting override them. The profile `Inherit` (default) lets the user's own Ultrathink, Linear and Notion configuration apply, as in the omp TUI; `Isolated` adds the neutralising variables listed in the dossiers. Substrate tokens are passed by environment variable name only (`SUBSTRATE_TOKEN_OMP`, `SUBSTRATE_TOKEN_FILE`, `SUBSTRATE_TOKENS_DIR`) and never appear in argv, logs, persisted state or exception messages.

`OMPOS_PLUGINS_ROOT` overrides the plugins directory. Otherwise OmpOS uses `plugins/` next to the repository root, found from the executable or the current directory.

## Plugins in omp terminals

The plugins are connected to every omp process the application starts (DR-2): the engine session behind the Agent Panel (`omp acp`) and the omp terminals (an interactive omp in the embedded terminal). Built in Phase 48; the records are `.planning/phases/48-omp-terminals-with-the-four-plugins/48-SUMMARY.md` and `48-VERIFICATION.md`.

How it works:

- The launch planner derives the terminal launch from the same plan as the ACP launch (`OmpLaunchPlan.ForTerminal`): `--no-extensions` and the same `-e` arguments, never `acp`, never `--plugin-dir`, the same per-session environment (including the per-session `SWARM_GSD_STATE_FILE`), no MCP server (an interactive omp reads its own), an optional `--resume <omp session>` (never `--continue`) and an optional first prompt.
- The embedded terminal starts omp through the planner (argument array, project directory). A missing omp, a missing plugin or an uninitialised submodule shows its fix text while the rest of the application keeps working.
- Each plugin is registered exactly once. `--no-extensions` drops the global `config.yml` `extensions:` list and the `extensions/` directory, and each `-e` plugin loads once; without it a plugin that is both in the user's global configuration and passed with `-e` loads twice ([exactly-once-loading.md](runbooks/evidence/phase-48-omp-plugins-2026-10-26/exactly-once-loading.md)). Side effect: the owner's other global extensions do not load in app-started omp ([OQ-83](open-questions.md)).
- The PTY child no longer inherits the host environment. It gets `PATH`, `HOME`, `LANG`, `TERM`, `PI_CODING_AGENT_DIR` and `OMP_PROFILE` from the host plus the variables of the launch; every other host variable is unset by name through `/usr/bin/env -u` (Linux).
- Connection is observed. The `gsd-core-omp` plugin writes the sorted tool and command names omp has registered to `OMPOS_CONNECTION_FILE`; the host compares them with its table of expected names per plugin and shows each plugin's state (waiting, connected, missing names, failed) in the connection row. A plugin that is not loaded shows its fix text. A heartbeat file (`alive.json`, every 5 s) shows that omp is serving.
- The Substrate extensions load only when a Substrate endpoint is configured and healthy (see Degradation). Without one, the row shows Substrate off with its reason.
- Plugins are not connected to an omp terminal the user starts outside the application (the default of [OQ-82](open-questions.md)).

What is verified (2026-10-26, Linux, real omp 18.12.6, no model turn): the probes in `docs/runbooks/evidence/phase-48-omp-plugins-2026-10-26/` (exactly-once loading, the interactive omp with the planned arguments, the connection report and heartbeat in both modes); the live tests `LiveOmpHandshakeTests` (engine session: all four plugins connected, fresh heartbeat, mode `rpc`) and `LiveOmpTerminalTests` (interactive omp in the PTY: all four connected, fresh heartbeat, mode `tui`, closes on two Ctrl+C once the TUI is quiet), tagged `Category=Live` and excluded from the gate; the planner, terminal and degraded-path tests of the gate.

What is not verified: a live model turn (OD-07); one application run showing all four plugins connected in the row; a run against a real Substrate server; the user's real credentials with this launch recipe; Windows, macOS, Android and the hosted deployments. The checklist is in the [agent loop runbook](runbooks/agent-loop.md#evidence-to-record).

## Host requirements

| Plugin | Needs on the host |
| ------ | ----------------- |
| all | `omp` on `PATH` (Bun-based); `git` for the submodules |
| Ultrathink | the omp runtime only [INFERENCE: its extension is TypeScript run by omp; no separate install step was found] |
| Swarm | `python3` >= 3.12 for the Python runtime in `swarm/` |
| Substrate (omp extensions) | the omp runtime only; the extensions fail soft when no server is reachable |
| Substrate (server, only to run one) | Bun 1.4.0 and PostgreSQL 16 for the index (graphs, leases, memory); the event ledger (`events_*`, handoffs, drift) needs GreptimeDB over HTTP SQL. Without Postgres, `graph_claim` and `memory_write` fail. |
| GSD-Core (`gsd-core-omp`) | `node` >= 24 (`gsd-tools.cjs` requires it) and an installed GSD-Core (Kimi layout at `~/.agents/gsd-core`, or `GSD_CORE_HOME`) |

OmpOS does not start a Substrate server.

## Degradation

Every degradation is a stage event with status Degraded or Unavailable and a plain-text reason. None crashes the panel or the run.

| Plugin | When missing or failing |
| ------ | ----------------------- |
| Ultrathink | The original prompt flows unplanned. |
| Swarm | The run is single-agent. |
| Substrate | No steering record, no shared memory; the UI says so. An unhealthy server is omitted from `session/new.mcpServers`, because omp fails `session/new` when any MCP server is unreachable. |
| GSD-Core | Task context shows the phase as unknown. |
| Any submodule not initialised | `SubmoduleNotInitializedException` naming the plugin, the path and `git submodule update --init --recursive plugins/<name>`. |

## Steering

OmpOS owns steering, and the mechanism depends on the session. The requirement is live delivery into a running turn (DR-4, Phase 49).

- **Chat (ACP) sessions** use omp's native extension request `_session/steering` when omp advertises `_meta.steering.supported` in `initialize`. Outcomes: `injected` (a running turn takes the message at its next tool-batch or model-call boundary), `promptRequired` (the session is idle: nothing was consumed and OmpOS sends the text through `session/prompt`) and `failed`. A plain `session/prompt` cannot steer, because omp's ACP agent cancels a running turn when a new one arrives.
- **Omp terminals** have no ACP. They use the OmpOS inbox in `plugins/gsd-core-omp`: OmpOS writes a directive file under `OMPOS_STEER_DIR/inbox/`, the plugin hands the text to omp with `sendUserMessage(text, { deliverAs })` (`steer`, `followUp` or `aside`) and writes an acknowledgement. OmpOS refuses to write when the heartbeat is older than 15 s, and shows "delivered" only on an acknowledgement. The inbox is also the fallback for an omp whose `initialize` lacks the steering capability.
- **Queue after the turn** (the held message runs as its own turn) is the last fallback.
- **Substrate** records the action as an `events_emit` note with `payload.action` `steer` when it is reachable; the text itself is never sent, only its length and a 12-hex digest prefix. Substrate does not deliver the steer: live delivery (substrate Phase 53) is unmerged ([OQ-66](open-questions.md)).

Verified without a model turn: the inbox against the real omp in both modes (heartbeat and connection report), the native wire contract (an idle session answers `promptRequired`, recorded in [OQ-81](open-questions.md)), and every OmpOS-side path with fakes. Not verified: an `injected` delivery into a running real turn (needs a model turn, OD-07). Details: [ADR-0012 update](decisions/ADR-0012-agent-loop-plugins.md#update-2026-10-26-implementation-of-phases-44-to-49).

## Licence facts

These are facts, not legal advice. The owner decision is open: [OQ-62](open-questions.md).

- OmpOS has no LICENSE file.
- `claude-ultrathink` is AGPL-3.0-or-later. `agent-swarm` and `agent-substrate` have no LICENSE file.
- The plugins are separate programs launched as omp extensions. They are pinned by reference. Their source is not copied or linked into OmpOS binaries, and the container build context excludes `plugins/`.

## Tests

Tests are hermetic. Never point a test at a live substrate on `127.0.0.1:7410`: use fakes, scripted ACP transports, temporary directories and fixture files. A development host may run a real substrate on that port; tests must neither read from nor write to it.
