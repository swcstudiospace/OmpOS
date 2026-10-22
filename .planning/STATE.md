---
gsd_state_version: "1.0"
milestone: v0.10.0
milestone_name: Cross-platform UI
status: executing
last_updated: "2026-10-22"
last_activity: 2026-10-22
progress:
  total_phases: 6
  completed_phases: 1
  total_plans: 4
  completed_plans: 4
  percent: 17
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-10-21)

**Core value:** A prompt typed on any surface becomes an omp session whose streamed events render as live Generative UI — reliably, resumably, and inside an isolation boundary the operator controls.
**Current focus:** Milestone v0.9.2 — Hardening and hosted API (requirements defined; roadmap pending)

## Current Position

Phase: 20 complete at the gate (HEAD 9e5913d). Phases 21 to 23 partially complete. Phases 24 and 25 blocked. Phases 26 to 29 partially complete (v0.11.0). Phase 30 partially complete: ADE-22 deferred; ADE-21 needs owner scope. v0.12.0 (phases 31 to 35) planned.
Plan: 20-04 complete (wave 4 records)
Status: Executing v0.10.0 (active). v0.9.2 is tech_debt, awaiting owner completion.
Last activity: 2026-10-22 - phase SUMMARY records written for phases 20 to 30; STATE updated

## Plan Decisions and Assumptions (2026-10-22, ultrathink graph ut-mvjvg4gj-fb21d80f)

Owner decision (answered 2026-10-22): cloud model is a persistent container per session with idle sleep, restoring the transcript from a volume on reattach.

Defaults applied, not yet confirmed (non-blocking, kickoff step 4): providers, verified only (q2); Pets, 2D anime companion first with 3D gated by a per-target spike (q3); Cursor reference, current public docs with version and date recorded in CONTEXT, functional parity with no visual or asset copying (q4).

Numeric defaults recorded as assumptions: heartbeat every 15 seconds, stale after three missed beats (45 seconds); server scrollback 10,000 lines; client scrollback 5,000 lines; input queue 50 messages while herdr is unreachable; attach token expiry 15 minutes; idle stop after 24 hours; completed-session retention 7 days.

Verified facts (research, opened primary pages, 2026-10-22; files in docs/plans/vision-research/):
- herdr: herdrdev/herdr, Apache-2.0, v0.9.5. Detaching leaves panes running. A herdr server restart or machine reboot does not keep processes; only the layout returns, and conversations resume only from native session references. Multi-day process durability is not supported by herdr.
- Greptile fonts: DM Sans, Anybody and Space Mono under SIL OFL 1.1; Nanum Pen Script is loaded but unused on the homepage.
- Railway: no desktop, VNC or browser-streaming product. Sandboxes and per-session Docker services are the viable paths. Trust and Safety text prohibits remote desktops for personal or professional browsing; written confirmation is required before production.
- Pets: Blazor WebAssembly is feasible with three.js (MIT) and @pixiv/three-vrm (MIT). Avalonia desktop has no .NET VRM renderer, so desktop 3D needs a custom renderer. The 2D fallback is feasible on every head.
- Cursor slash commands: 86 tokens, 69 verified and 22 unverified (changelog only). Cursor settings: 34 rows, 30 verified and 4 unverified.
- Providers, local and US: 24 of 25 rows verified; OpenAI and Groq pricing pages were unreadable (403 and redirect). Providers, EU and CN: 22 researched; 4 verified, 14 partial, 4 unverified.

Open pauses:
- PRODUCT BEHAVIOUR (new, owner decision): durability across a herdr server restart or machine reboot. Options: (a) accept the herdr limit; after a restart or reboot, sessions resume from the stored native omp session reference with the transcript restored (default); (b) require process survival, which herdr v0.9.5 does not provide, so it needs an upstream change or another runtime; (c) defer cloud terminals until (b) is available.
- EXTERNAL: Railway written confirmation for remote desktops; Railway account and deploy access; production API origin.
- VALIDATION: Greptile render sign-off; Pets appearance sign-off; Cursor parity sign-off.
- CREDENTIALS: live provider credentials per provider; herdr endpoint and credentials; omp launch-time provider configuration (unverified).

Blocked, carried from v0.10.0 and earlier: Phases 22, 24 and 25 (Mac channel, Windows runner, Wayland session, Android SDK and device); Phase 34 (OD-06); Phases 12 to 14 (deploys paused).

Execution assumption: v0.13.0 Phases 36 to 38 do not depend on the blocked items and proceed in parallel with them. No blocked item is closed by this plan.

Next action: dispatch the Phase 36 units through the AgentSwarm (a01-orchestrator), run the gate, commit by phase, then ask the owner the durability question.

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
- [2026-10-22, q1]: MCP access goes through a host-owned gateway (user answer 2026-10-22). Phase 33 implements it.
- [2026-10-22, q2]: Cloud means the OmpOS hosted API (OIDC bearer per OD-03). No third-party cloud provider is chosen.
- [2026-10-22, q3]: Remote development is deferred (OD-06).
- [2026-10-22, q4]: omp is an external install located by configuration or PATH (Harness:Acp:OmpPath), consistent with the API image shipping a pinned omp.

### Pending Todos

None yet.

### Blockers/Concerns

- [Owner decision]: ADE-21 scope (which VS Code extension APIs come first) is needed before work starts (Phase 30).
- [Owner decision]: OD-03 sign-in decision gates hosted exposure, ADE-07 and ADE-08 (Phase 26).
- [Owner decision]: OD-06 remote omp endpoint is unverified; remote protocol and host-key store not decided (Phase 34, v0.12.0).
- [Open decision]: editor control (ADR R-2), terminal control and PTY (ADR-0006 R-1) and browser pane (ADR R-3) are not chosen.
- [External, Mac channel]: macOS 26 build, accessibility dump, signing and notarization (Phase 22); iOS build, accessibility dump and signing (Phase 24). Phase 25 cutover waits on both.
- [External, host]: no Windows 11 24H2 runner and no Wayland session (Phase 22); no Android SDK API 36, ANDROID_HOME unset (Phase 24).
- [External, deploy]: Railway deploys paused blocks the Phase 26 cloud live run, the Phase 23 Vercel smoke (OMPOS_API_ORIGIN unset) and ADE-22.
- [Engineering gap]: production approval and steering path not connected to the engine (Phase 21 criteria 4 and 5). Terminal setup commands need the approval path (ADR-0005 risk 1).

## Deferred Items

Items acknowledged and deferred at milestone close, most recent first:

| Category | Item | Status | Deferred At | Milestone |
|----------|------|--------|-------------|-----------|
| Scope | ADE-22 admin and analytics | Deferred in writing: criterion 2 unmet (Phase 26 criterion 4 cloud runs not live on the hosted API) | 2026-10-22 | v0.11.0 |
| Scope | ADE-15 browser pane | Not started: ADR R-3 open (candidate Avalonia.Controls.WebView 12.1.1, MIT) | 2026-10-22 | v0.11.0 |
| Scope | ADE-07 cloud runs | Not started: needs a contract change request for run status and diff; live run blocked (Railway deploys paused) | 2026-10-22 | v0.11.0 |
| Scope | ADE-08 remote control | Not started: OD-03 gated; authority not configured | 2026-10-22 | v0.11.0 |
| Scope | Phase 34 remote development | Blocked: OD-06, no verified remote omp endpoint; no Cursor primary SSH docs page | 2026-10-22 | v0.12.0 |
| Verification | Remote CI run green (push + PR jobs on GitHub; local gates pass, no remote configured yet). Resume: verify on first ship PR run | deferred-human | 2026-10-20 | v0.9.1 |
| Verification | macOS MAUI head build job (excluded from Linux slnf; local host is Linux). Resume: verify on first CI run on a macOS runner | deferred-human | 2026-10-20 | v0.9.1 |

## Session Continuity

Last session: 2026-10-22
Stopped at: wave 4 records written (20-04 summary and phase 21 to 30 SUMMARY files). Ultrathink tracking complete. Ship is enabled but has NOT run: the done-gate fails while roadmap phases are incomplete, so no PR was opened
Resume file: None

## Operator Next Steps

- Create the v0.9.2 roadmap from .planning/REQUIREMENTS.md (gsd-roadmapper), then plan the first phase
