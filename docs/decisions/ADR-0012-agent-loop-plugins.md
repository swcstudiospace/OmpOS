# ADR-0012: Agent loop plugins

- Status: Accepted 2026-10-26
- See Update 2026-10-26 (at the end of this record).
- Date: 2026-10-26
- Owner: ompos-maintainers
- Deciders: ompos-maintainers
- Scope: how the Ultrathink, Swarm, Substrate and GSD-Core plugins are obtained, launched, observed and degraded in the OmpOS agent loop (milestone v0.14.0, phases 44 to 46). The full design is [agent-loop-design.md](../plans/agent-loop/agent-loop-design.md); the evidence is in [docs/plans/agent-loop/](../plans/agent-loop/README.md).

## Context

OmpOS drives omp over ACP. Three sibling projects already ship as omp plugins: `claude-ultrathink` (prompt planning), `agent-swarm` (a 30-agent subagent orchestrator) and `agent-substrate` (cross-session state, steering record, ledger). GSD-Core is installed on the host but has no omp runtime upstream. The program goal is a default Agent Panel that shows these stages without OmpOS owning their code.

Observed facts that constrain the design:

- Under `omp acp`, extensions see `ctx.mode` `rpc` with a no-op UI context: widgets mount nowhere and `ui.notify` never reaches the client.
- omp silently ignores a nonexistent `-e` or `--plugin-dir` path and unknown flags.
- omp fails `session/new` if any MCP server in the request is unreachable.
- Substrate on its default branch has no live steering delivery.
- The substrate repository is private; two of the three siblings have no LICENSE file and the third is AGPL-3.0-or-later.

## Decision

- **D1. The plugins run inside omp.** OmpOS resolves them from pinned submodules, launches `omp acp` with explicit loading flags, observes them through files and HTTP, and renders them. No sibling source is copied or edited. Evidence: `dossier-omp-loading.md` sections 1 to 5, `dossier-ultrathink.md`.
- **D2. Layout.** `plugins/claude-ultrathink`, `plugins/agent-swarm` and `plugins/agent-substrate` are git submodules with remote https URLs and `branch = main`, pinned to commits that exist on the remotes. `plugins/gsd-core-omp` is a tracked OmpOS-owned thin plugin, because GSD-Core upstream has no omp runtime. Evidence: `dossier-git-ci.md`, `dossier-gsd-omp.md` section 3.
- **D3. Steering is OmpOS-owned.** Operator text is queued over ACP (queue after the turn, through the existing run input scheduler). OmpOS only records a steering action in Substrate (`events_emit`, kind `note`, `payload.action` `steer`) when it is reachable, and never claims Substrate delivered it. Evidence: `dossier-substrate.md`, `dossier-ompos-seams.md`.
- **D4. Stage events are data in `OmpOS.Contracts.Agents`.** No `StreamEvent` kind is added and `ContractVersion` does not change. Evidence: `dossier-ompos-seams.md` section 2.4, option A.
- **D5. One `omp acp` child per session.** `InProcessEngineHost` binds one session; the native head owns N engines through `SessionEngineSet`. Evidence: `dossier-ompos-seams.md`.
- **D6. Substrate is reached twice.** The omp child reaches it through an HTTP entry in `session/new.mcpServers`; the host reaches it through `SubstrateGateway` (`GET /healthz`, `POST /brief`, `POST /mcp`, `POST /events/query`). The host health-checks first and omits an unhealthy Substrate, surfacing a Degraded stage. Tokens travel only as environment variable names. Tests never target the live server on `127.0.0.1:7410`. Evidence: `dossier-substrate.md`.
- **D7. Launch is hermetic and explicit.** `omp acp --no-extensions`, then one `-e` per extension in composition order (Ultrathink directory, Swarm `omp/`, the two substrate projector files, `plugins/gsd-core-omp`). `-e` only, never `--plugin-dir`. OmpOS validates every path and tests assert exact argv and per-session environment. Evidence: `dossier-omp-loading.md` sections 2, 3 and 5.
- **D8. Degradation is a stage event, never a crash.** Ultrathink off: original prompt flows. Swarm missing: single-agent run. Substrate unavailable: no steering record, no shared memory. GSD missing: phase unknown. Uninitialised submodule: a typed exception naming the plugin, path and fix. Evidence: [agent-loop-design.md](../plans/agent-loop/agent-loop-design.md) section 1.
- **D9. The panel shows only what exists.** There is no live Ultrathink progress: that stage is Running from prompt send until its session file appears. Swarm state comes from `live.json` and `events.jsonl`. Substrate panels show only timestamp, surface, kind, actor and `payload` action, item and outcome, never ledger summary or payload text. Memory and lesson bodies are untrusted plain text, length capped. Evidence: `dossier-ultrathink.md` sections 3 and 5, `dossier-swarm.md` section 4, `dossier-substrate.md`.

## Consequences

- Cloning needs `--recurse-submodules`. The private substrate submodule needs read access locally and the `SUBMODULE_TOKEN` secret in CI ([OQ-63](../open-questions.md)). Without it the substrate plugin is Unavailable and the build still passes.
- A pin bump is a commit in OmpOS. Sibling changes land in the sibling repository first ([plugins guide](../plugins.md)).
- The architecture layer map gains `OmpOS.Harness.AgentLoop` (L2) and the Contracts `Agents` types ([architecture](../architecture.md)).
- Stage visibility depends on files and HTTP the plugins write. A plugin that changes those formats degrades its stage until the observer is updated; unknown fields are ignored and malformed files yield a Degraded event.
- Open items: licence of OmpOS and AGPL interplay ([OQ-62](../open-questions.md)), symlinked agents ([OQ-64](../open-questions.md)), intermittent outbound connection ([OQ-65](../open-questions.md)), Substrate Phase 53 adoption ([OQ-66](../open-questions.md)), live Ultrathink progress ([OQ-67](../open-questions.md)).
- Note on evidence: the design file records an earlier Ultrathink pin; the pin in the OmpOS tree and in [plugins.md](../plugins.md) is authoritative.

## Alternatives considered and rejected

- **Copy the plugin code into OmpOS.** Rejected: it forks three projects, drops upstream fixes and mixes an AGPL-3.0-or-later codebase into the repository. Pinned submodules keep one source of truth.
- **Reimplement the plugins on the host side.** Rejected: the behaviour (planning, 30 agents, ledger) already exists as omp plugins and would diverge from the originals.
- **Load with `--plugin-dir`.** Rejected: it provides skills, commands and agents but runs no extension code (no `/swarm`, no `swarm_*` tools), and combined with `-e` it double-registers commands. Evidence: `dossier-omp-loading.md` section 3, runs c, d, f.
- **A fourth submodule for GSD-Core.** Rejected: upstream GSD-Core has no omp runtime, and the installed copy under `~/.agents` is what omp skills already use. A thin tracked plugin that copies nothing is enough.
- **Substrate-delivered steering.** Rejected for now: live delivery (Phase 53) is unmerged on the pinned commit. OmpOS delivers over ACP and records only. Revisit under [OQ-66](../open-questions.md).

## Update 2026-10-26

Source: the user's direction of 2026-10-26 (decisions DR-1, DR-2, DR-4, DR-5 and DR-6 of the TODO revision). The accepted text above is unchanged. Where this section and the accepted text disagree, this section governs; the affected sentences are named below.

- **DR-1. There is no additional Agent Panel.** The default Cursor-style Agent Panel is the agent panel, and the work is to keep it a faithful Cursor clone, not to build a second surface. The accepted sentence "The program goal is a default Agent Panel that shows these stages" (Context) and D9 ("The panel shows only what exists") are read with this limit: the panel shows no stage panels of its own and has no inspector. Kept: the session rail (Cursor's Agents sidebar), the thread, the composer, the header chips and the one-line plugin connection row directly under the header. They belong to the default panel header, they are not a panel. The task context and the steering history that the design placed in an inspector move to the Kanban tab card (Phase 39). The facts in D9 about what the plugins expose (no live Ultrathink progress, Swarm files, Substrate fields, untrusted note bodies) still hold for every place that shows them.
- **DR-2. Plugins run in every omp process the application starts, exactly once.** D1 and D7 describe the launch of `omp acp`. The target now covers both the engine session behind the Agent Panel (over ACP) and the omp terminals the application starts. Each plugin is loaded from the pinned submodules once per process, even when the user's own omp configuration (global `config.yml` extensions, extension symlinks) is present. This is a verification target, not a recorded fact: it is closed only by one recorded run per plugin and one end-to-end run without a model turn (Phase 48). D44-8 of the Phase 44 context records that the same launch planner plans the terminal launch.
- **DR-4. Steering facts and the open delivery mechanism; D3 is amended.** D3 said operator text is queued over ACP after the turn. That stays as the fallback only. Live delivery into a running turn is the requirement. Facts that matter, each from the repository or the installed omp:
  - omp's ACP agent cancels a running turn when a new `session/prompt` arrives (`acp-agent.ts`, `prompt`), so a prompt cannot deliver live over ACP.
  - omp extensions can deliver a message into a running session with `sendUserMessage(text, { deliverAs: "steer" | "followUp" | "aside" })`.
  - omp RPC mode has `steer`, `follow_up` and `steer_subagent` commands. The OmpOS RPC client already carries the intent as `PromptOptions.Steer` (`src/OmpOS.Harness.Abstractions/PromptOptions.cs`, `src/OmpOS.Harness.Rpc/OmpRpcSession.cs`), and `RunInputScheduler` (`src/OmpOS.Presentation/Runs/RunInputScheduler.cs`) already separates steers from follow-ups. `AcpAgentSession.SubmitPromptAsync` throws `NotSupportedException` when `PromptOptions.Steer` is set ([OQ-59](../open-questions.md)).
  - Substrate on the pinned commit has no live delivery (draft PR #110, [OQ-66](../open-questions.md)). D3's rule stands: OmpOS records the steering action in Substrate and never attributes delivery to it.
  The delivery mechanism is therefore an open question, [OQ-81](../open-questions.md), with a recommended default: an omp extension inbox that calls `sendUserMessage` with `deliverAs`. Alternatives: switch the engine to omp RPC mode and use `steer` and `steer_subagent`, or adopt Substrate Phase 53 once merged. The steering requirement is written as an outcome (a running session takes the message into account before its turn ends; delivered, queued and failed are shown truthfully), not as a mechanism. Evidence: `docs/plans/agent-loop/dossier-substrate.md` and `dossier-ompos-seams.md` (the existing seams); the `acp-agent.ts` and `sendUserMessage` facts come from the installed omp and are to be re-confirmed in the Phase 49 recorded run.
- **DR-5. Defaults for three open product questions** (non-blocking; the user may change them): (a) the Kanban columns mirror the session lifecycle status, with the Phase 39 context defaults ([OQ-78](../open-questions.md)); (b) closing or ending a session moves its card to its terminal column (Completed, Failed or Archived) and never removes it silently ([OQ-79](../open-questions.md)); (c) the four plugins are always on, so no enable or disable settings item is added, while the launch planner keeps its per-plugin switches for operators and tests ([OQ-80](../open-questions.md)).
- **Scope of plugin registration** ([OQ-82](../open-questions.md)): the default is per-process `-e` flags for app-started omp. Writing the registration into omp's own configuration, for terminals the user starts elsewhere, is an optional later step blocked on a probe of how omp merges `extensions` overlays with the global configuration.
- **DR-6 (for reference).** The Settings window follows the Cursor Settings structure with per-provider fields in its Models section. Phase 40 carries it. It does not change this ADR.

Consequences: the Kanban tab (Phase 39) is the only product addition besides provider settings; Phase 48 verifies the plugins in app-started omp terminals; Phase 49 delivers live steering. No accepted decision other than D3 changes, and D3 keeps its queue-after-turn behaviour as the fallback.
