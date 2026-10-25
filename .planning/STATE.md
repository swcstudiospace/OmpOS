---
gsd_state_version: "1.0"
milestone: v0.10.0
milestone_name: Cross-platform UI
status: paused
last_updated: "2026-10-24"
last_activity: 2026-10-24
progress:
  total_phases: 6
  completed_phases: 2
  total_plans: 5
  completed_plans: 5
  percent: 33
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-10-21)

**Core value:** A prompt typed on any surface becomes an omp session whose streamed events render as live Generative UI — reliably, resumably, and inside an isolation boundary the operator controls.
**Current focus:** Milestone v0.10.0 — Cross-platform UI (active). Phases 20 and 21 complete. Paused at the owner's decision on Phase 22, which is blocked on external hosts.

## Current Position

Phase: 21 complete (5 of 5 criteria, verified twice: 21-VERIFICATION.md passed 5/5). Phase 20 complete (7 of 7, 20-VERIFICATION.md passed).
Plan: 21-01 complete. Phase 22 partial: criterion 1 met on the XWayland path (2026-10-24), criterion 2 met on X11 (2026-10-22), criteria 3 to 5 blocked on external hosts (Windows 11 24H2 runner; Mac channel). Phases 23 to 25 partial or blocked (see queue).
Status: Paused for one owner decision: how to proceed while Phase 22 criteria 3 to 5 remain blocked. The repo gate is green on the uncommitted working tree (bootstrap OK, 2240 passed, 0 failed, 0 skipped). Nothing is committed.
Last activity: 2026-10-24 - autonomous run: gate baseline repaired, lock files regenerated, Phase 20 verified, Phase 21 completed with owner decisions F1 to F3 implemented and independently reviewed, headless Wayland probe run for Phase 22 criterion 1, records reconciled.

## Autonomous Run 2026-10-24 (ultrathink graph ut-mvlj2cdc-609d7b5b)

Scope: the active milestone is v0.10.0. The STATE frontmatter and ROADMAP agreed on it. Phases 20 to 25 are the milestone's phases. Phases 20 and 21 are complete. Phase 22 is blocked on external hosts, and the rest are partial or blocked, as the queue records.

Reconciliation. Before this run, STATE named v0.9.2 as current focus, said Phase 20 was complete, and ROADMAP showed Phase 20 in progress. Resolved from the artifacts: v0.10.0 is active, and Phase 20 is complete (20-04-SUMMARY and 20-VERIFICATION). ROADMAP is updated to match.

Baseline gate: red. Two defects in the uncommitted Terminal PTY tests (tests/OmpOS.Harness.Terminal.Tests/LinuxLocalPtyBackendTests.cs) caused it. `DisposeEndsARunningProcessAndReturns` quoted its shell `PID<$$>` wrongly, so `/bin/sh` failed and the PID wait polled forever (a 59-minute hang). `RefusesANullArgument` asserted a property name as the ParamName. Both are test defects; production code is unchanged. Fixed in the test file.

Lock files: the `OmpOS.Sessions` project reference in apps/OmpOS.Native/OmpOS.Native.csproj made the locked restore fail (NU1004). Regenerated with `dotnet restore OmpOS.Linux.slnf --force-evaluate`. `dotnet restore OmpOS.Linux.slnf --locked-mode` passes.

Phase 20: complete. gsd-verifier verdict passed, 7 of 7 (20-VERIFICATION.md). OQ-39 stays open. It is not a criterion failure.

Phase 21: complete. Verified twice, passed 5 of 5 (21-VERIFICATION.md). Implementation:
- Production approvals: `Program.ProductionAllowedKinds` = edit, delete, move, execute, read, search. The owner confirmed the list (the parent first proposed the four write and execute kinds, and the owner then added read and search). fetch, think, switch_mode and other are denied automatically.
- Prompt echo: a user-message event in the shared contract. The sent prompt appears in the transcript, and each queued message appears when its turn starts.
- Steering: steer and follow-up messages are held in send order and sent as their own turns after the active turn ends. Stop cancels the turn and sends nothing further.
- An independent review (a09-reviewer) returned CHANGES_REQUESTED. All findings were fixed, each with a named test. The fixes include the replay fold throwing on the new kinds, the SSE name for diagnostics, the composer copy, the fault-path test, the fault ordering, the window-close disposal, and the held-steer count.

Phase 22 (partial, blocked): the owner approved a headless Wayland session. weston 13.0.0 (headless backend with Xwayland 23.2.6) was installed. The probe in docs/runbooks/evidence/phase-22-wayland-2026-10-24/ started the head with default platform detection. The head stayed alive for 30 seconds. Its mapped backend libraries were Avalonia.X11.dll and libX11, with no libwayland-client, and an `OmpOS` window (960x640) existed on the Xwayland display. Criterion 1 is met on the XWayland path. The native Wayland backend (Avalonia.Wayland with UseWayland()) is not added: it needs a package addition, and native Wayland has no AT-SPI support, so that is a separate accessibility decision. Criteria 3 (Windows runner) and 4 and 5 (Mac channel) remain blocked.

Quality: the CA1031 suppression added by an earlier Phase 21 unit is removed. No new suppressions remain in the touched code.

Autonomous decisions, using the plan's clarification defaults:
- q1 commits: none. Every change is uncommitted, and the user's uncommitted work is preserved.
- q2 tooling: no new dependency added. The headless Wayland packages (weston, xwayland, x11-utils) are host tools installed for the probe, approved by the owner.
- q3 gap handling: Phase 22 is held blocked, not closed with a gap.
- Approval policy: deny-by-default is kept for every kind not named.

Tracking (ultrathink): the Notion Task row is set to Implementing. The planner created the Notion rows. The Linear rows were not created, because the workspace has reached its free issue limit (`save_issue` refused). The substrate `graph_register` call is deferred until the Linear rows exist.

Uncommitted changes from this run (review before any commit):
- Phase records: .planning/phases/20-avalonia-foundation/20-VERIFICATION.md (new); .planning/phases/21-parity-surface/21-CONTEXT.md, 21-01-PLAN.md, 21-VERIFICATION.md (new) and 21-SUMMARY.md (rewritten); .planning/phases/22-desktop-heads/22-SUMMARY.md (rewritten); .planning/ROADMAP.md; .planning/STATE.md.
- Phase 21 code: apps/OmpOS.Native (Program.cs, App.axaml.cs, MainWindow.axaml.cs, Engine/ including new EnginePromptChannel.cs and EngineRunInput.cs, Views/ComposerView.axaml.cs, OmpOS.Native.csproj); shared contracts and presentation (src/OmpOS.Contracts, src/OmpOS.Presentation/Runs, src/OmpOS.Sessions/HarnessEventProjection.cs, src/OmpOS.GenUi/UiFolder.cs, src/OmpOS.EngineHost/RemoteEngineHost.cs); services/OmpOS.Api/Sse/StreamEventKind.cs; docs/parity/session-chat-parity.md; tests under tests/OmpOS.Native.Tests/Send/, tests/OmpOS.Native.Tests/RunControls/, tests/OmpOS.Presentation.Tests/, tests/OmpOS.Contracts.Tests/, tests/OmpOS.Sessions.Tests/, tests/OmpOS.UI.Tests/.
- Phase 22 evidence: docs/runbooks/phase-22-desktop-heads.md (rows XP-02 Wayland updated); docs/runbooks/evidence/phase-22-wayland-2026-10-24/ (probe.sh, probe-output.txt).
- Lock files: about ten packages.lock.json files regenerated.
- Terminal fix: tests/OmpOS.Harness.Terminal.Tests/LinuxLocalPtyBackendTests.cs.

## Owner Decisions

Answered 2026-10-24:
- F1, approval kinds: named kind set, confirmed as edit, delete, move, execute, read and search. The owner did not name the kinds at first; the parent proposed the first four from the ACP spec, and the owner added read and search.
- F2, prompt echo: contract payload (a user-message event in the shared contracts).
- F3, steering over ACP: queue after the turn. Steer and follow-up are delivered after the active turn ends, in send order.
- Phase 22 blocker: try a headless Wayland session here. Done; criterion 1 met on the XWayland path. Criteria 3 to 5 remain blocked.

Open (ask this turn, answer pending):
- How to proceed while Phase 22 criteria 3 to 5 remain blocked on external hosts: skip Phase 22 and continue with the phases that do not depend on it (recommended), or stop until the Windows runner and the Mac channel exist. Phase 36 (v0.13) depends only on Phases 20 and 21, so skipping Phase 22 lets it proceed.

Later owner decisions, not blocking now:
- Durability across a herdr server restart or machine reboot (Phase 37, VX-06). Default: resume from the stored native omp session reference.
- Railway written confirmation for remote desktops (Phase 42).
- OD-03 identity authority configuration (Phases 12, 13 and 26).
- Greptile render sign-off (Phase 36, VX-01).
- Cursor parity sign-off (Phases 38 and 40).
- Pets appearance sign-off (Phase 41).
- Native Wayland opt-in (Avalonia.Wayland, UseWayland): a package addition with an accessibility gap, to decide before it is added.

## Queue (status from each phase's SUMMARY; depends-on from ROADMAP)

| Milestone | Phase | Depends on | Status and blocker or decision |
|-----------|-------|------------|--------------------------------|
| v0.10.0 | 22 Desktop heads | 20, 21 | Partial. Criteria 1 (XWayland path) and 2 (X11) met. Criteria 3 (Windows 11 24H2 runner) and 4 and 5 (Mac channel) blocked. Owner decision: skip or stop |
| v0.10.0 | 23 Web head | 20, 21 | Partial. Criterion 1 met. Criteria 2 and 3 blocked (OMPOS_API_ORIGIN unset; Railway deploys paused). Criterion 4 needs a repository command that reproduces G-WEB (the spike project is not in the repo). Does not depend on 22 |
| v0.10.0 | 24 iOS and Android heads | 20, 21 | Blocked. iOS needs the Mac channel. Android needs SDK API 36 (ANDROID_HOME unset). Does not depend on 22 |
| v0.10.0 | 25 Cutover | 21, 22, 23, 24 | Blocked on 22 and 24 |
| v0.11.0 | 26 Agents, worktrees, cloud, remote | 21, 22 | Partial. Blocked by 22. Cloud criterion needs v0.9.2 Phase 13 (Railway pause, OD-03) |
| v0.11.0 | 27 Editor and review | 21, 22 | Partial. Blocked by 22. Native editor view not built |
| v0.11.0 | 28 Terminal, MCP, rules, search | 21, 22 | Partial. Blocked by 22. Terminal control and PTY not chosen |
| v0.11.0 | 29 Browser pane, completion, export | 26, 27 | Partial. Browser pane not started (ADR R-3 open) |
| v0.11.0 | 30 Compatibility and admin | 20, 26, 29 | Partial. ADE-22 deferred in writing. ADE-21 needs owner scope |
| v0.12.0 | 31 Plugin manifests and hooks | 21, 28 | Planned. Blocked by 28 |
| v0.12.0 | 32 Automations | 26, 31 | Planned |
| v0.12.0 | 33 Omp CLI terminal and shared registry | 28 | Planned |
| v0.12.0 | 34 Remote development | not stated | Blocked: OD-06 (no verified remote omp endpoint) |
| v0.12.0 | 35 Code quality bar | 20 to 33 | Planned. Runs last, audits the final tree |
| v0.13.0 | 36 Contracts and theme foundation | 20, 21 | UNBLOCKED: its dependencies are met. Uncommitted code already exists (Harness.Providers, Harness.Secrets, Harness.Herdr, Harness.Agent, Presentation Kanban and Commands, theme font assets, and their tests, all green in the gate). It needs a CONTEXT, PLAN and VERIFICATION against the Phase 36 criteria. Does not depend on 22 |
| v0.13.0 | 37 Session runtime and local terminal | 36 | Planned. Durability decision (VX-06) |
| v0.13.0 | 38 Chat window and slash commands | 36, 37 | Planned. Cursor parity sign-off |
| v0.13.0 | 39 Kanban board | 36, 37 | Planned |
| v0.13.0 | 40 Settings and model providers | 36, 37 | Planned. Live validation needs owner credentials per provider |
| v0.13.0 | 41 Pets companion | 36, 39 | Planned. Appearance sign-off |
| v0.13.0 | 42 Cloud terminals on Railway | 37, 40; Phase 13 | Prototype only. Railway written confirmation; deploys paused |
| v0.13.0 | 43 Mobile and web terminals | 36, 37, 40 | Planned. iOS needs the Mac channel |

v0.9.2 Phases 6, 12, 13 and 14 remain blocked on external criteria and the owner's OD-03 sign-in decision, as recorded in ROADMAP.

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

Open pauses (carried):
- PRODUCT BEHAVIOUR (owner decision): durability across a herdr server restart or machine reboot. Options: (a) accept the herdr limit; after a restart or reboot, sessions resume from the stored native omp session reference with the transcript restored (default); (b) require process survival, which herdr v0.9.5 does not provide, so it needs an upstream change or another runtime; (c) defer cloud terminals until (b) is available.
- EXTERNAL: Railway written confirmation for remote desktops; Railway account and deploy access; production API origin.
- VALIDATION: Greptile render sign-off; Pets appearance sign-off; Cursor parity sign-off.
- CREDENTIALS: live provider credentials per provider; herdr endpoint and credentials; omp launch-time provider configuration (unverified).

Blocked, carried from v0.10.0 and earlier: Phase 22 criteria 3 to 5 (Windows runner, Mac channel); Phase 24 (Mac channel, Android SDK API 36); Phase 25 (on 22 and 24); Phase 34 (OD-06); Phases 12 to 14 (deploys paused).

Execution assumption, corrected 2026-10-24: v0.13.0 Phase 36 depends only on Phases 20 and 21, and both are complete. It does not depend on Phase 22, so it can proceed without the blocked host items.

Next action (superseded 2026-10-24): see Autonomous Run 2026-10-24, Owner Decisions and Queue above.

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
- [2026-10-24, F1]: Production approvals are deny-by-default except for the owner-confirmed kinds edit, delete, move, execute, read and search (ACP tool kinds). fetch, think, switch_mode and other stay denied automatically.
- [2026-10-24, F2]: The sent prompt is a user-message event in the shared contract, echoed into both heads.
- [2026-10-24, F3]: Steer and follow-up messages are queued and delivered after the active turn ends, in send order. ACP v1 has no steer.
- [2026-10-24, Phase 22]: Criterion 1 is met on the XWayland path using a headless weston session. The native Wayland backend is not added (package addition and no AT-SPI support).

### Pending Todos

- Owner answer: how to proceed while Phase 22 criteria 3 to 5 remain blocked (skip Phase 22 or stop).
- Linear rows for the ultrathink graph, after the Linear workspace limit is resolved; then re-run `ultrathink-mcp track complete` and the substrate `graph_register`.
- Phase 36 CONTEXT, PLAN and VERIFICATION, when the run resumes (if the owner chooses to skip Phase 22).
- Native Wayland opt-in decision (accessibility gap) before any Avalonia.Wayland change.

### Blockers/Concerns

- [Owner decision]: Phase 22 handling (skip or stop), while criteria 3 to 5 remain blocked.
- [Owner decision]: ADE-21 scope (which VS Code extension APIs come first) is needed before work starts (Phase 30).
- [Owner decision]: OD-03 sign-in decision gates hosted exposure, ADE-07 and ADE-08 (Phase 26).
- [Owner decision]: OD-06 remote omp endpoint is unverified; remote protocol and host-key store not decided (Phase 34, v0.12.0).
- [Open decision]: editor control (ADR R-2), terminal control and PTY (ADR-0006 R-1) and browser pane (ADR R-3) are not chosen.
- [External, Mac channel]: macOS 26 build, accessibility dump, signing and notarization (Phase 22); iOS build, accessibility dump and signing (Phase 24). Phase 25 cutover waits on both.
- [External, host]: no Windows 11 24H2 runner (Phase 22 criterion 3); no Android SDK API 36, ANDROID_HOME unset (Phase 24).
- [Accessibility gap]: native Wayland (Avalonia.Wayland) has no AT-SPI support, so a native-Wayland head would have no screen-reader path; the XWayland path keeps AT-SPI through X11.
- [External, deploy]: Railway deploys paused blocks the Phase 26 cloud live run, the Phase 23 Vercel smoke (OMPOS_API_ORIGIN unset) and ADE-22.
- [External, tracking]: Linear workspace free issue limit reached; the ultrathink Linear rows were not created.
- [Known limit, Phase 21]: no live omp turn was exercised; OnClosed still has an unobserved MCP disposal (pre-existing).

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

Last session: 2026-10-24
Stopped at: the owner decision on Phase 22 handling (skip Phase 22 and continue, or stop). The gate is green on the working tree. Nothing is committed.
Resume file: None
Ship: ultrathink-ship is enabled with autoMerge on. It has NOT run. Its gate rules block shipping while roadmap phases are incomplete, and the working tree is uncommitted, so no PR was opened.

## Operator Next Steps

- Decide Phase 22 handling. Skip it and continue with Phase 36 (v0.13), or stop until the Windows runner and the Mac channel exist.
- Linear: upgrade the workspace or accept Notion-only tracking. Then re-run `ultrathink-mcp track complete` and the substrate `graph_register` call.
- Host items (owner): Mac channel for Phase 22 macOS and Phase 24 iOS; a Windows 11 24H2 runner; Android SDK API 36; OMPOS_API_ORIGIN and Railway deploys before the Phase 23 smoke.
- Review and commit the uncommitted working tree when ready. The autonomous run made no commits.

## Run Discovery 2026-10-24 (gsd-autonomous, graph ut-mvm12su0-1fae51f6)

This section supersedes the stale statements above it. Gate count is 2245 (not 2240). Phase 36 is complete (36-VERIFICATION passed 6 of 6). Phase 37 is partial (criteria 1 to 4 verified, criterion 5 open by design). The Phase 22 owner question is answered: skip Phase 22 and continue.

Kickoff clarifications, answered 2026-10-24:
- Branch: current branch feat/v0.9.2-hardening, local commits only. No push and no pull request.
- Uncommitted work: committed as baseline 8e0c5e2 (229 paths). A secret-shaped scan of the staged set found no matches. BASE for this run is 8e0c5e2.
- System installs: Android SDK API 36 and the .NET Android workload only. Podman 5.x and gitleaks are not authorized.
- Caller identity: record the code's behavior. Callers are the JWT subject. local-dev is a Development-only principal behind Api:Auth:AllowDevelopmentPrincipal. An OwnerId in a request body is refused with 400. The Greptile knowledge-base answer is stale, and its cited docs/api-service.md is not in this tree.
- Non-blocking defaults applied: scope is the ROADMAP phases plus behavior-neutral safe fixes (q1); CI is recommend-only (q3).
- Phase commands: Skill() is not a tool in this environment. Each phase command runs by loading its skill document and executing it as the single active command. Recorded deviation.

Discrepancy list (on-disk evidence wins):
1. Phase 22 criterion 1 is overstated in 22-SUMMARY and in this file ("met on the XWayland path"). ROADMAP criterion 1 requires the start "with the native backend opt-in". Corrected to partial: X11 and XWayland starts are recorded, and the native Wayland run is not done.
2. Phase 23 criterion 4 is recorded here as blocked, saying the spike project is not in the repo. Commit e598e5e re-measured G-WEB from the repository, and tools/gweb/ is tracked. Corrected: see the 2026-10-24 re-run under QUALITY_BASELINE.
3. Phase 36 is listed as unblocked and awaiting records. It is complete: 36-CONTEXT, 36-01-PLAN, 36-SUMMARY and 36-VERIFICATION all exist.
4. Phase 37 is listed as planned. It is partial: criteria 1 to 4 verified in 37-VERIFICATION, and criterion 5 is open by design.
5. The Phase 22 question above is answered (skip and continue). The "answer pending" wording is stale.
6. v0.11.0 and v0.12.0 phase statuses do not match commits 90d1296, 377d96f, 68f78c7 and 8a89149, which implement parts of Phases 31 to 33 and 26. Not reconciled in this pass. Recorded for MILESTONE_AUDIT.

QUALITY_BASELINE (BASE 8e0c5e2, 2026-10-24):
- Stack: .NET SDK 10.0.402 (global.json). C# throughout: an Avalonia 12.1.4 native head and a Blazor WebAssembly web head. Manifests: OmpOS.Linux.slnf (54 projects, each with packages.lock.json) and Directory.Packages.props (central package versions).
- Gate commands, verbatim from bootstrap.sh:
  - restore: dotnet restore OmpOS.Linux.slnf --locked-mode
  - build: dotnet build OmpOS.Linux.slnf -c Release --no-restore -warnaserror
  - test: dotnet test OmpOS.Linux.slnf -c Release --no-build -- --filter-not-trait "Category=Live"
  - format: dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore
  - full gate: ./bootstrap.sh exits 0 and prints "bootstrap: OK". Result: total 2245, failed 0, skipped 0, test duration 48.9 s, wall time 265.8 s.
- Outside the gate, CI only: gitleaks secret scan (ci.yml, pinned 8.30.1), the vulnerable-package list, and compose validation.
- Pre-existing gate failures: none.
- Known limits: the open-questions register (KL-01 to KL-08 and the open OQ rows).

Entry state (n1 step 8):
- v0.10.0 is RUNNING.
- Phases 20 and 21: COMPLETE (verified).
- Phase 22: BLOCKED. Criteria 3 to 5 need external hosts. Criterion 1 is partial. Owner decision: skip and continue.
- Phase 23: REMAINING, partial. Criteria 2 and 3 need OMPOS_API_ORIGIN and the Vercel and Railway deploys. Criteria 1, 4 and 5 are to be verified this run.
- Phase 24: BLOCKED. iOS needs the Mac channel. Criterion 5 needs a device or emulator, and /dev/kvm is absent. Criterion 4 needs the Android SDK (authorized for this run) and a CI run.
- Phase 25: BLOCKED on Phases 22 and 24.

Remaining phases in ROADMAP order, v0.10.0: 22 (BLOCKED), 23 (REMAINING), 24 (BLOCKED), 25 (BLOCKED).
Milestone closeout cannot run until v0.10.0 is complete or the owner decides otherwise. Later milestones follow only after that.

Pauses raised: none for grey areas. BLOCKERs for Phase 22 criteria 3 to 5 (Windows runner, Mac channel), Phase 23 criteria 2 and 3 (OMPOS_API_ORIGIN, Vercel, Railway deploys paused), and Phase 24 criteria 1 to 3 and 5 (Mac channel, no device or emulator).



## Run Progress 2026-10-24 (after the baseline)

Baseline: committed as 8e0c5e2. This is BASE for the run. The uncommitted tree is now in history, and the working tree was clean before the first run edit.

QUALITY_BASELINE gate on 8e0c5e2: ./bootstrap.sh exit 0, prints "bootstrap: OK", total 2245, failed 0, skipped 0.

Install record (authorized: Android SDK and .NET Android workload):
- Android SDK at /root/Android/Sdk, present before this run: platforms android-34 and android-36, platform-tools, ndk 26.3.11579264, build-tools 34.0.0, and cmdline-tools latest and latest-2. sdkmanager is deprecated, and the android CLI sits in the same directory.
- Installed this run: build-tools 36.0.0 through sdkmanager (exit 0). The warning that cmdline-tools;latest sits in latest-2 is informational and was left alone.
- Installed this run: the .NET workload android 36.1.118 (manifest 10.0.100, SDK band 10.0.400), exit 0.
- Classification: development tooling for Phase 24. No repository file references it yet. Its first user is the Phase 24 Android head project.
- Licences: the Android SDK terms were accepted under this run's authorization of the Android SDK install. The SDK licenses directory existed before the run.
- ANDROID_HOME is not set globally. Set ANDROID_HOME=/root/Android/Sdk for each build.
- Not installed: Podman 5.x and gitleaks. Not authorized.

G-WEB re-measurement (Phase 23 criterion 4): tools/gweb/measure.sh on 2026-10-24 gives roleNodes 4, textboxNodes 0 and namedTextboxNodes 0 for stock and named. This matches 2026-10-22. Evidence: docs/runbooks/evidence/gweb-remeasure-2026-10-24/.

Phase status, v0.10.0:
- 20: COMPLETE (verified).
- 21: COMPLETE (verified).
- 22: BLOCKED. Criterion 1 is partial (the native Wayland opt-in run is not done). Criteria 3 to 5 are external. Owner decision: skip and continue.
- 23: BLOCKED. Criteria 1, 4 and 5 are met. Criteria 2 and 3 are external.
- 24: BLOCKED. Criterion 4 is partial: local prerequisites are installed, no head project exists, and no CI run has happened. Criteria 1 to 3 and 5 are external.
- 25: BLOCKED on Phases 22 and 24.

Corrections recorded in this run: 22-SUMMARY (criterion 1 is partial), 23-SUMMARY (criterion 4 is met, and the spike-project claim was wrong), and 24-SUMMARY (the Android SDK is present). Each correction is marked in its file.

Pauses and payloads (the run stops at these):
- GREY_AREA, Phase 22 criterion 1: add Avalonia.Wayland and call UseWayland() for a native Wayland opt-in run? Default: no. Cost: a package addition, and native Wayland has no AT-SPI support, so the accessibility gap needs its own decision.
- BLOCKER, Phase 22 criteria 3 to 5: a Windows 11 24H2 runner and the Mac channel. Minimal action: provide a runner and a Mac host, or record these criteria as owner-blocked.
- BLOCKER, Phase 23 criteria 2 and 3: OMPOS_API_ORIGIN, the Vercel deploy, and Railway deploys that are paused. Minimal action: provide the production API origin and resume deploys, or confirm the deploy is out of scope.
- VALIDATION, Phase 24 criterion 4: a CI run needs a push of this branch. GIT_SAFETY allows a push only when a phase requires one, and this phase does. Decision needed: authorize the push, or leave criterion 4 open.
- BLOCKER, Phase 24 criteria 1 to 3: iOS needs a Mac with Xcode. BLOCKER, Phase 24 criterion 5: an Android 16 device or emulator. /dev/kvm is absent on this host, so no emulator runs here.

Resume point: Phase 24 criterion 4, local part. Create the Android head project following CONTRIBUTING "Adding a project". Pin Avalonia.Android to the version the native head uses, after a NuGet check. Build it with ANDROID_HOME=/root/Android/Sdk and warnings as errors, and record the log. The CI part waits on the push decision.


## Discuss Step and Plan Pause (2026-10-24, graph ut-mvm12su0-1fae51f6)

Discuss records written, one CONTEXT.md per remaining v0.10.0 phase: 22-CONTEXT.md, 23-CONTEXT.md, 24-CONTEXT.md and 25-CONTEXT.md, each in its phase directory. Each lists verified paths, the criteria with their evidence, the grey areas with defaults, and its pause payload.

Naming convention (n1.4): phase directories are NN-slug/, and artifacts are NN-CONTEXT.md, NN-01-PLAN.md, NN-SUMMARY.md and NN-VERIFICATION.md. Examples: .planning/phases/21-parity-surface/21-CONTEXT.md and .planning/phases/21-parity-surface/21-01-PLAN.md.

Audit (n2.4, n2.6): dotnet list OmpOS.Linux.slnf package --vulnerable --include-transitive exited 0 and reported no vulnerable packages. Build output is ignored by .gitignore lines 30 and 31 (bin/ and obj/).

Grey areas awaiting one batched answer (GREY_AREA class):
- G22-1: add Avalonia.Wayland and call UseWayland() for a native Wayland run? Default: no.
- G22-2: accept that native Wayland has no AT-SPI support, so the criterion 2 Wayland variant stays unmet? Default: record the gap.
- G23-1: run the axe check as a gate step, which adds Node 22 or later and Chrome to the gate? Default: no. scripts/web-smoke stays the axe evidence.
- G24-1: Android head structure. (a) A new head project over a shared Avalonia library, with a behavior-neutral move of views out of apps/OmpOS.Native. (b) A repeated shell. Default: (a).
- G24-2: write the Android CI job now, and authorize a push for a CI run? Default: write the job, and do not push.
- G25-1: remove apps/OmpOS.App only after Phases 22 and 24 close and the parity inventory is green. Default: yes, and no deletion in this run.
- G-M1 (milestone order): continue to v0.11.0 while v0.10.0 items stay blocked? Default: no. Close each milestone before the next.
- G-S1 (process): Skill() is not a tool in this environment. Default: run each phase command by loading its skill document as the one active command, recorded as a deviation. Under the spec, this is a BLOCKER if it is not accepted.

BLOCKERs, external, and no decision clears them: Phase 22 criteria 3 to 5 (Windows 11 24H2 runner, Mac channel). Phase 23 criteria 2 and 3 (OMPOS_API_ORIGIN, Railway deploys). Phase 24 criteria 1 to 3 (Mac with Xcode and an iOS device) and criterion 5 (Android 16 device or KVM host). Phase 25 is blocked on Phases 22 and 24.

Plan (n8, rewritten from the wave 1 to 3 evidence; the waves run in sequence because the answers change the file set):
- Pause 1, now: the batched GREY_AREA answers above. Each answer becomes an accepted decision in its CONTEXT.md.
- Wave A, after the answers: Phase 24 criterion 4 local part (structure per G24-1; build with ANDROID_HOME; record the log; write the CI job per G24-2). Phase 22 native run if G22-1 is accepted. Phase 23 gate step if G23-1 is accepted.
- Wave B: n5 pattern convergence over the paths Wave A touches, counting the families and choosing the winner by the precedence rules. Then the gates, with one checkpoint commit per phase.
- Pause 2, external: the BLOCKERs above, in one payload.
- Closeout: blocked until every v0.10.0 phase is complete, or G-M1 is answered. Then audit, complete and cleanup, in that order.

Wave status: Wave 2 (n3 tooling findings and n4 scope records) is complete with these records. Wave 3: n6 and n7 are recorded, and n5 waits for Wave A's touched-file set. Wave 4 (n8) is this plan, and it depends on n5.

Ship: held. assess returned done=false (roadmap 2/6). No PR, no push, no merge.


## Run Start 2026-10-25 (gsd-autonomous, graph ut-mvmegw7f-4a7312d5)

Supersedes the 2026-10-24 run record (graph ut-mvm12su0-1fae51f6). Its Notion rows stay. Linear refuses new issues (free issue limit), so tracking is partial.

Discovery (Wave 1):
- Git: branch feat/v0.9.2-hardening, HEAD 63e8fcf, 90 commits ahead of main. Working tree clean (0 entries), 0 stashes, 0 unmerged paths. No merge, rebase, cherry-pick or revert in progress.
- Identity: configured. core.hooksPath unset. commit.gpgsign unset. .planning is tracked (124 files).
- Stack: .NET SDK 10.0.402 (global.json). Gate: ./bootstrap.sh (locked restore, warnings-as-errors build, tests, format, and from this run the axe web smoke).
- Skill() is not available. Each phase command is run by loading its skill document (G-S1, accepted 2026-10-25).
- Host: Node 24.18.0, /usr/bin/google-chrome, python3 3.12.3, npm 11.16.0, Android SDK at /root/Android/Sdk (android-34 and android-36), .NET workloads android 36.1.118 and wasm-tools 10.0.113.

Baseline (Wave 2), at HEAD 63e8fcf before any edit: ./bootstrap.sh exit 0, "bootstrap: OK", total 2245, failed 0, skipped 0, wall 304.95 s.

Phase map (Wave 3), v0.10.0 (Phases 20 to 25):
- 20 complete. 21 complete. 22 skipped by owner decision (2026-10-24).
- 23 partially complete: criterion 2's gate part met this run; the deploy part and criterion 3 blocked.
- 24 partial: no head project yet. Criterion 4 needs a CI run, which needs a push (G24-2 is "no push"). Criteria 1 to 3 need a Mac; criterion 5 needs an Android 16 device or emulator (no /dev/kvm).
- 25 not started. Criterion 1 is unmet: 24 parity rows carry "pending:" in the Avalonia column (docs/parity/session-chat-parity.md). The owner's waiver covers the phase dependencies on 22 and 24, not criterion 1.
- v0.11.0 and later: not started (G-M1 answered No).

Owner answers applied (GREY_AREA_ACCEPTANCE, 2026-10-25): phase25 waive the dependencies for MAUI removal; axe add to the gate; push write the job, no push; ship hold; structure new head over a shared Avalonia library; skill accept and record the deviation; m1 No.

Clarifications q1 to q3 are non-blocking. Defaults applied and stated: q1 public contracts stay backward compatible; q2 incremental within scope; q3 minimal local gates, plus the gate and CI additions the owner accepted (axe, and the Android job, not pushed).

Phase 23 record (2026-10-25): see 23-SUMMARY.md, run 2026-10-25. The axe smoke exposed a web-to-API contract defect (OwnerId sent on POST /sessions). It is repaired in the web head.

Ship: held by the owner's answer. ultrathink-ship not invoked. No PR, no push, no merge.

## Phase 24 run record 2026-10-25 (graph ut-mvmegw7f-4a7312d5)

Status: partially complete. The shared library split, the Android head and the android CI job are written; the Android build passes locally. The CI run needs a push, which is not authorized. Criteria 1 to 3 and 5 are blocked. Record: 24-SUMMARY.md, run 2026-10-25. Evidence: docs/runbooks/evidence/phase-24-android-2026-10-25/.
Gate after the split: `./bootstrap.sh` exit 0, total 2245, failed 0, skipped 0, web smoke exit 0. One flaky test is recorded (LinuxLocalPtyBackendTests.DisposeEndsARunningProcessAndReturns, passed on rerun; the terminal harness is not touched by this run).

## Run summary 2026-10-25 (gsd-autonomous, graph ut-mvmegw7f-4a7312d5, paused)

- v0.10.0 closeout: partial audit written (.planning/v0.10.0-MILESTONE-AUDIT.md). Milestone complete: not run (open phases). Cleanup: no run-created scratch remains (the checkpoint patches were not needed; commits are the checkpoints).
- Phase states: 20 complete; 21 complete; 22 skipped by owner decision; 23 blocked (criterion 2 deploy part and criterion 3); 24 blocked (CI push not authorized; criteria 1 to 3 Mac; criterion 5 device); 25 awaiting owner decision (criterion 1).
- Commits: cfa385d (Phase 23 checkpoint), fe2d398 (Phase 24 checkpoint), and the Phase 25 checkpoint with this run summary.
- Open pauses: Phase 25 GREY_AREA (questions 1 and 2 in 25-CONTEXT.md, run record 2026-10-25). Resume at Phase 25 Wave 8 after the answers. External blockers: Phase 22 (Windows and Mac runners), Phase 23 (OMPOS_API_ORIGIN, deploys), Phase 24 (push authorization for the CI run; Mac; Android 16 device).
- Ship: held by the owner's answer; ultrathink-ship not invoked; no PR, push or merge.
- v0.11.0 and later: not started (G-M1 answered No).
- Unmapped follow-ups: the flaky terminal test helper; a repository-wide TODO and FIXME check (touched paths were clean).

## Phase 25 decision 2026-10-25

Owner answers: implement the 13 Class C features with native tests; accept row 11 as by design. Phase 25 proceeds: criterion 1 first, then the removal of apps/OmpOS.App and its references (criterion 2), then the search evidence.

## Phase 25 complete 2026-10-25

Criteria 1 to 3 met with evidence in 25-SUMMARY.md (update 2026-10-25). The owner's waiver covers the dependencies on Phases 22 and 24. v0.10.0 is still open: Phase 22 skipped, Phases 23 and 24 blocked on external criteria.
