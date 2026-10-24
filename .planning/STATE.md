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
