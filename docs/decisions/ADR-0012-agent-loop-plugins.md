# ADR-0012: Agent loop plugins

- Status: Accepted 2026-10-26
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
