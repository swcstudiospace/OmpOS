# Plugins

OmpOS runs four plugins inside the omp engine. Three are sibling projects pinned as git submodules under `plugins/`; one is first-party and tracked in this repository. All four are omp extensions: OmpOS launches omp with them, observes what they write, and shows what they expose in the default Cursor-style Agent Panel (session rail, thread, composer, and the one-line plugin connection row under the thread header). The panel has no stage panels or inspector of its own (DR-1, [ADR-0012](decisions/ADR-0012-agent-loop-plugins.md#update-2026-10-26)); task context and steering history belong on the Kanban tab card (Phase 39). The target is that the plugins are connected to every omp process the application starts, engine sessions over ACP and omp terminals, as described in "Plugins in omp terminals" below. OmpOS never copies, forks or edits their source. Decision record: [ADR-0012](decisions/ADR-0012-agent-loop-plugins.md). Evidence: [docs/plans/agent-loop/](plans/agent-loop/README.md).

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
| `SWARM_GSD_STATE_FILE` | empty | always |
| `SWARM_CLAUDE_STATE_DIR` | `<session>/claude-state` | always |
| `SUBSTRATE_OMP_PENDING_DIR` | `<session>/substrate-pending` | always (its default is a hard-coded real-home path) |
| `SUBSTRATE_TOKENS_DIR` | configured directory | only when configured |
| `SUBSTRATE_URL` | the Substrate endpoint | only when Substrate is enabled and healthy |
| `ULTRATHINK_UPLIFT` | `0` | only when planning is disabled |

`SWARM_AGENT` and `SWARM_TASK_ID` are never set. Real runs inherit `HOME` and `PI_CODING_AGENT_DIR`, because the user's omp credentials live there. Only tests and an explicit isolated-home setting override them. The profile `Inherit` (default) lets the user's own Ultrathink, Linear and Notion configuration apply, as in the omp TUI; `Isolated` adds the neutralising variables listed in the dossiers. Substrate tokens are passed by environment variable name only (`SUBSTRATE_TOKEN_OMP`, `SUBSTRATE_TOKEN_FILE`, `SUBSTRATE_TOKENS_DIR`) and never appear in argv, logs, persisted state or exception messages.

`OMPOS_PLUGINS_ROOT` overrides the plugins directory. Otherwise OmpOS uses `plugins/` next to the repository root, found from the executable or the current directory.

## Plugins in omp terminals

This section states a target, not a result. No recorded run shows it working or failing yet; it is verified in Phase 48 (criteria in [.planning/ROADMAP.md](../.planning/ROADMAP.md), decision D44-8 in `.planning/phases/44-plugin-submodules-launch-contract/44-CONTEXT.md`).

Target behaviour (DR-2):

- The launch planner plans an interactive omp (terminal) launch as well as `omp acp`, with the same plugin set, per-session state directory and environment as in the Launch contract above.
- The embedded terminal starts omp through the planner (argument array, project directory). A missing omp, a missing plugin or an uninitialised submodule shows its fix text while the rest of the application keeps working.
- Each plugin is registered exactly once in an omp process the application starts, also when the user's own omp configuration is present (global `config.yml` extensions, extension symlinks). If omp cannot be made to load each plugin once, the fallback is recorded in an open question ([OQ-82](open-questions.md)).
- Each of Ultrathink, Substrate, Swarm and GSD-Core is registered with omp, active in the app-started engine session and in the app-started omp terminal, and observable in the session UI to the extent the plugin exposes it. A load or connect failure is shown with its fix text.
- Plugins are not connected to an omp terminal the user starts outside the application (the default of [OQ-82](open-questions.md)).

What closes it: one recorded run per plugin, one recorded end-to-end run without a model turn, and a degraded-path test per plugin. The checklist is in the [agent loop runbook](runbooks/agent-loop.md#evidence-to-record).

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

OmpOS owns steering. Operator text is queued over ACP and delivered after the current turn. This is the fallback: omp's ACP agent cancels a running turn when a new `session/prompt` arrives, so a prompt cannot deliver live, and live delivery into a running session is the requirement (DR-4, Phase 49). The delivery mechanism is open ([OQ-81](open-questions.md)). When Substrate is reachable, OmpOS also records the action as an `events_emit` note with `payload.action` `steer`; the text itself is never sent, only its length and a 12-hex digest prefix. Substrate does not deliver the steer: live delivery (substrate Phase 53) is unmerged ([OQ-66](open-questions.md)).

## Licence facts

These are facts, not legal advice. The owner decision is open: [OQ-62](open-questions.md).

- OmpOS has no LICENSE file.
- `claude-ultrathink` is AGPL-3.0-or-later. `agent-swarm` and `agent-substrate` have no LICENSE file.
- The plugins are separate programs launched as omp extensions. They are pinned by reference. Their source is not copied or linked into OmpOS binaries, and the container build context excludes `plugins/`.

## Tests

Tests are hermetic. Never point a test at a live substrate on `127.0.0.1:7410`: use fakes, scripted ACP transports, temporary directories and fixture files. A development host may run a real substrate on that port; tests must neither read from nor write to it.
