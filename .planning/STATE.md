---
gsd_state_version: "1.0"
milestone: v0.9.2
milestone_name: Hardening and hosted API
status: planning
last_updated: "2026-10-21T10:22:28.524Z"
last_activity: 2026-10-21
progress:
  total_phases: 0
  completed_phases: 0
  total_plans: 0
  completed_plans: 0
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-10-21)

**Core value:** A prompt typed on any surface becomes an omp session whose streamed events render as live Generative UI — reliably, resumably, and inside an isolation boundary the operator controls.
**Current focus:** Milestone v0.9.2 — Hardening and hosted API (requirements defined; roadmap pending)

## Current Position

Phase: Not started (defining requirements)
Plan: —
Status: Defining requirements
Last activity: 2026-10-21 — Milestone v0.9.2 started

## Performance Metrics

**Velocity:**
- Total plans completed: 0
- Average duration: - min
- Total execution time: 0.0 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| - | - | - | - |

**Recent Trend:**
- Last 5 plans: -
- Trend: -

*Updated after each plan completion*
**Per-Plan Metrics:**

| Plan | Duration | Tasks | Files |
|------|----------|-------|-------|
| Phase 1 P01 | 12 min | 4 tasks | 114 files |
| Phase 1-foundation P02 | 11 min | 3 tasks | 45 files |
| Phase 1-foundation P03 | 13 min | 3 tasks | 18 files |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- [Roadmap]: Phase skeleton fixed to five phases by the ultrathink specification (Foundation → Core → Platform → Surfaces → Verify & Handoff); granularity coarse, mode mvp.
- [Roadmap]: Real `omp acp`/`omp --mode rpc` adapters (HARN-04/05) scheduled as the last Core plans so Surfaces can overlap against the scripted in-memory adapter + SSE API.
- [Foundation]: .NET 10 SDK 10.0.402 pinned in `global.json`; CPM with transitive pinning, lock files on app/test heads; STJ source-gen only (`JsonSerializerIsReflectionEnabledByDefault=false`); Podman/podman-compose only, Docker refused.
- [Core]: Append-only sequence-numbered `StreamEvent` log is the resume/dedup primitive (SSE `id:` = `Seq`); approval hook is deny-by-policy and may only deny earlier, never grant more.
- [Platform]: ~~Temporal optional behind `Workflows:Enabled` with `DirectSessionRunner` default~~ Superseded 2026-10-21: Temporal is removed and there is no worker service; session runs execute in-process through `DirectSessionRunner` (`docs/handoff.md`, "Temporal removal and Railway topology (2026-10-21)"). Vendor sandbox providers (OpenShell, agentOS, Railway Swarm, attested enclave) are interface-conforming stubs with owner-tagged open questions — no invented vendor calls.
- [Phase 1]: Blazor package versions in CPM; Web entry uses ConfigureAwait(false) under latest-all — Template-inline versions conflict with CPM; template await trips CA2007/MA0004
- [Phase 1]: bootstrap.sh keeps dotnet test step as-is despite exit 1 on empty test set — Plan-directed: no masking flags; plan 01-02 test projects resolve it
- [Phase 1]: 01-02: A2uiSurfaceBlock kind is a2ui-surface; bare a2ui stays unknown
- [Phase 1]: 01-02: OmpOSError is a sealed record; contract collections are read-only interfaces; semver asserted via Version.TryParse
- [Phase 1]: ArchUnitNET rules take Assembly objects: the string overload of ResideInAssembly compares full assembly names, so short names match nothing — Probed short and full names (both fail to match); object overloads evaluate over real object sets for both positive and negative rules
- [Phase 1]: open-questions.md seeded with 26 owned rows (14 research gaps, 4 research extras, 8 ultrathink ambiguities transcribed from dispatch context) — Q1-Q8 text is not locatable in the repo; dispatch context provided the eight items with resolved defaults

### Pending Todos

None yet.

### Blockers/Concerns

- [Phase 2]: Research flag — record every `sessionUpdate` variant a real `omp acp` turn emits as fixtures for the scripted adapter; empirically check Railway SSE buffering/HTTP-2 before freezing the resume design.
- [Phase 3]: Research flag — whether omp can run under agentOS (native addons), OpenShell gRPC contract, dstack attestation shape, Podman 5.x on Ubuntu 24.04 (apt ships 4.9.3).
- [Phase 4]: Research flag (Vercel sub-plan only) — `libicu`/`dotnet-install` caching, SPA rewrite vs `/_framework/*` precedence, Brotli negotiation for standalone Blazor WASM publish.
- [Phase 4]: MAUI head builds only in the macOS CI job; Linux verification covers the web head and RCL only.

## Deferred Items

Items acknowledged and deferred at milestone close, most recent first:

| Category | Item | Status | Deferred At | Milestone |
|----------|------|--------|-------------|-----------|
| Verification | Remote CI run green (push + PR jobs on GitHub; local gates pass, no remote configured yet). Resume: verify on first ship PR run | deferred-human | 2026-10-20 | v0.9.1 |
| Verification | macOS MAUI head build job (excluded from Linux slnf; local host is Linux). Resume: verify on first CI run on a macOS runner | deferred-human | 2026-10-20 | v0.9.1 |

## Session Continuity

Last session: 2026-10-20T06:26:48.224Z
Stopped at: Completed 1-03-PLAN.md
Resume file: None

## Operator Next Steps

- Create the v0.9.2 roadmap from .planning/REQUIREMENTS.md (gsd-roadmapper), then plan the first phase
