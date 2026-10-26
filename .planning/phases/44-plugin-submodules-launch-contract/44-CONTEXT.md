# Phase 44 Context: Plugin Submodules and Launch Contract

Milestone v0.14.0. Requirements AL-01 to AL-04. Decision record: `docs/decisions/ADR-0012-agent-loop-plugins.md`. Evidence: `docs/plans/agent-loop/` (eight redacted discovery dossiers and the design). Guide: `docs/plugins.md`. Runbook: `docs/runbooks/agent-loop.md`.

## What this phase does

The user asked for three sibling projects to join the agent loop as pinned git submodules and omp plugins, and for GSD-Core to be an omp plugin too:

- `claude-ultrathink` (prompt uplift, Graph and Chain of Thought, decisions),
- `agent-swarm` (subagent orchestrator, runs after Ultrathink),
- `agent-substrate` (live session steering, memory, lessons across Kanban tasks),
- GSD-Core ("Get Shit Done").

## Decisions

| ID | Decision | Evidence |
|----|----------|----------|
| D44-1 | Plugins run inside the omp engine. OmpOS resolves them from the submodules, launches omp with them, observes their side channels (files and HTTP) and projects stage events. | Discovery dossiers `dossier-ultrathink.md`, `dossier-swarm.md`, `dossier-substrate.md`; user answer q1 settled from repo evidence |
| D44-2 | Launch contract (confirmed by a live probe, no model turn): `omp acp --no-extensions -e <ultrathink dir> -e <swarm>/omp -e substrate-status.ts -e substrate-skills.ts -e plugins/gsd-core-omp`. Use `-e` only: `--plugin-dir` loads no extension code and the two together register twice. omp silently ignores a bad path or flag, so OmpOS validates every path and the tests assert the exact argv. | `dossier-omp-loading.md` |
| D44-3 | Per-session state directories hold the plugins' files: `ULTRATHINK_STATE_DIR`, `SWARM_DIR`, `SWARM_CLAUDE_STATE_DIR`, `SUBSTRATE_OMP_PENDING_DIR`. `SWARM_GSD_STATE_FILE` is set empty so the swarm never writes `swarm:` lines into the OmpOS `.planning/STATE.md`. `SWARM_AGENT` and `SWARM_TASK_ID` are never set. Tokens travel only by environment variable name, never in argv. | `dossier-swarm.md` |
| D44-4 | GSD-Core is a thin OmpOS-owned plugin (`plugins/gsd-core-omp`), not a fourth submodule: upstream (open-gsd/gsd-core, MIT) has no omp runtime. The plugin reports health as JSON (exit 0 healthy, 2 degraded, 3 not installed) and degrades without GSD-Core. | `dossier-gsd-omp.md` |
| D44-5 | Submodule pins are the tips of each remote's default branch, verified on the remotes (the local sibling checkouts are unpushed or on feature branches). The upstream repositories were renamed; `.gitmodules` carries the canonical URLs. | `dossier-git-ci.md` |
| D44-6 | The private `agent-substrate` repository needs a token in CI: the `SUBMODULE_TOKEN` secret is an owner action (OQ-63). Public submodules always check out. Without the token CI continues with the substrate plugin reported as not initialised. | `ci.yml` step "Plugin submodules" |
| D44-7 | Third-party content under `plugins/` is never edited, scanned for TODO markers or shipped in the container build context. | `TodoMarkerScanner`, `.containerignore` |
| D44-8 | Terminals: the same planner plans the omp terminal launch (Phase 48). The launch planner plans an interactive omp (terminal) launch as well as `omp acp`, with the same plugin set, per-session state directory and environment, so that the plugins are connected to every omp process the application starts and loaded exactly once (DR-2). Added 2026-10-26; this is a verification target for Phase 48, not a recorded fact. | User direction 2026-10-26 (DR-2); `docs/decisions/ADR-0012-agent-loop-plugins.md`, Update 2026-10-26 |

## Open items for the owner

OQ-62 (licence of OmpOS and AGPL-3.0-or-later of ultrathink), OQ-63 (`SUBMODULE_TOKEN`), OQ-64 (omp symlinked agent loading), OQ-65 (intermittent outbound connection of `omp acp`), OQ-66 (Substrate Phase 53 live steering), OQ-67 (live Ultrathink progress under ACP). See `docs/open-questions.md`.
