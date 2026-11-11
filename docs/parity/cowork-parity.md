---
description: "Row-by-row audit of Autonogrammer Cowork parity A (projects, memory, schedules) against the v0.16.0 inventory in docs/plans/v0.16.0-research/briefs/75-cowork-parity-a.md and ADR-0037 D2."
---

# Cowork parity audit (projects, memory, schedules) - Phase 75-06

Status: Integration complete for listed rows (written after 75-01..05 and 75-06 tasks; the parity file is the evidence record for AG-75-11 and D-30).

Last verified: 2026-11-10

Applies to: v0.16.0 (phase 75)

This audit records the state of Cowork parity A after the integration plan. Rows are graded MET only when a named passing test class exists. omp-dependent rows (C7, C9) stay PARTIAL until live omp consumption is recorded.

## 1. Audited tree and method

| Item | Value |
|---|---|
| Audited tree | The tree after 75-06 commits (launch seam, contributor, starter, registration, parity file, openapi, docs) |
| Method | Read plan, CONTEXT, RESEARCH, the named test classes from 75-01..05 and this plan; grep for class names; actual dotnet test runs with PATH=/root/.dotnet:$PATH for 75 classes (Project*, Standing*, SessionTarget*, MissedRun*, Unattended*, Guard, Architecture); no live omp run for this audit. Actual runs: export PATH=/root/.dotnet:$PATH; dotnet --version (10.0.402); targeted dotnet test/build invocations on Cowork.Tests, ArchitectureTests, Api.IntegrationTests (runner mismatches and obj path errors observed as real execution output, not static read). |

## 2. Status vocabulary

| Word | Meaning |
|---|---|
| MET | Implemented, reachable, covered by named test class |
| PARTIAL | Partial; gap named |
| NOT-STARTED | No product code |
| DEFERRED | Blocked by named external (e.g. live services, device-off) |
| OMITTED-BY-DECISION | Omitted per ADR-0037 D2 (no org tenancy, hosted inference etc.) |

## 3. Summary

| Family | MET | PARTIAL | NOT-STARTED | DEFERRED | OMITTED-BY-DECISION | Total |
|---|---|---|---|---|---|---|
| Cowork core (C1-C31) | 0 | 17 | 9 | 1 | 4 | 31 |
| Knowledge/scheduled (K01-K20) | 0 | 6 | 10 | 2 | 2 | 20 |
| **All** | **0** | **23** | **19** | **3** | **6** | **51** |

## 4. C rows (Cowork inventory C1-C31)

Source inventory from docs/plans/v0.16.0-research/06-cowork-parity-and-references.md section 3 and ADR-0037 D2. Grades remain provisional until current-tree evidence is available.

| Row | Feature | Status | Evidence | Gap or blocker |
|-----|---------|--------|----------|----------------|
| C1 | Core task agent | NOT-STARTED | Covered by prior ADR-0032 (omp + session chat); this phase adds no new agent core (see 75-CONTEXT OmpLaunchPlanner generic apply only). | Phase 75 focuses on parity seams only. |
| C2 | Cloud execution | DEFERRED | D1 of report 06; needs funded hosted inference (Railway sandboxes + short-lived tokens per inventory). Local only for 75. | Per ADR-0037 D1. |
| C3 | Local isolation | PARTIAL | Phase 76-04; bwrap/Seatbelt + proxy (sandbox-runtime ref S24); no network fence yet (C3 inventory). | Sandbox in later phase. |
| C4 | Folder and file access, storage folder | PARTIAL | Phase 76-03/04; trusted folders + access mode; CoworkProjectStore + folders (device-specific never to API) per 75-06. | Per D-03/D-04. |
| C5 | Deletion protection | PARTIAL | Phase 76-03; file-tool deletes enforced; baseline approval in tools. | Tool hardening later. |
| C6 | Approval modes and connector matrix | PARTIAL | Phase 76-02/03; Manual/Auto(rules)/Skip; no classifier (D3); matrix planned. | Auto classifier deferred. |
| C7 | Global and folder instructions | PARTIAL | global and folder instructions; omp consumption unverified (OQ-440); StandingContextBuilderTests + StandingContextContributorTests (byte-stable 2KiB para, memory.backend off) | Actual: StandingContextContributorTests.CoworkSurfaceGetsParagraphAtMost2KiBAndFirstPromptBlockAndMemoryBackendOff asserts <=2KiB, <untrusted> block, overlay "memory.backend: off"; Code path empty. omp live consumption (OQ-440) unverified. |
| C8 | Projects | PARTIAL | ProjectValidatorTests, ProjectStoreMigrationTests, ProjectAccountSyncTests, ProjectsApiTests and CoworkPresentationTests cover project behavior in source. | Passing current-tree test runs have not been established. |
| C9 | Memory | PARTIAL | agent-side use depends on the tool host reaching omp | MemoryContentPolicyTests, MemoryToolProviderTests, Postgres store tests; scoped + policy + tool provider. Agent-side omp dep per verif. |
| C10 | Scheduled tasks | PARTIAL | runs while the desktop is awake, device-off deferred | SessionTargetTests, MissedRunTests, UnattendedApprovalTests (24h catchup, 30m cancel, isolation check) |
| C11 | Plugins | PARTIAL | Phase 76-01; hooks in bundles refused (inventory ADAPT to omp). | Per 76 plan. |
| C12 | Skills | PARTIAL | Phase 76-01; SKILL.md consume (Agent Skills std S26). | Per 76. |
| C13 | Connectors and MCP | PARTIAL | Phase 76-02; MCP + broker (C13). | Per 76. |
| C14 | Built-in browser | NOT-STARTED | Prior ADR-0034; embedded + CDP + per-site (C14/C16 from S5). | Prior phase scope. |
| C15 | Claude in Chrome | OMITTED-BY-DECISION | Per ADR-0037 D2 (D6); extension + Anthropic classifiers not reusable. | OMIT per D6. |
| C16 | Browser use with vision | NOT-STARTED | Prior ADR-0034; hybrid DOM+screenshot grounding (C16 S28). | Prior. |
| C17 | Computer use | NOT-STARTED | Prior ADR-0035; Cua Driver / native tiers (default off, per-app); Linux no (C17 S10/S22). | Prior. |
| C18 | Dispatch | NOT-STARTED | Prior ADR-0036; outbound relay + device creds (C18 S23). | Prior. |
| C19 | Artifacts | NOT-STARTED | Prior ADR-0033; reuse A2UI (C19; hosted templates OMIT). | Prior. |
| C20 | Live artifacts | OMITTED-BY-DECISION | Deprecated per ADR-0037 (D6); frozen post-2026-08-19. | OMIT per D6. |
| C21 | File outputs | NOT-STARTED | Prior (C21); xlsx/pptx planned, Office add-ins OMIT. | Prior. |
| C22 | Custom visuals in chat | NOT-STARTED | Prior (C22); GenUi/A2UI. | Prior. |
| C23 | Sub-agents | PARTIAL | Phase 76-05; omp parallel workstreams (C23 S25). | Per 76. |
| C24 | Notifications | PARTIAL | Phase 76-05; phone + desktop notifs. | Per 76. |
| C25 | Cross-surface continuity | NOT-STARTED | Prior (C25); API session store. | Prior. |
| C26 | Usage and plans | PARTIAL | Phase 76-05 (local meter); billing OMIT per D6. | Per 76 + D6. |
| C27 | Monitoring and compliance | PARTIAL | ADR-0031 + local audit (OTel to ledger). | Per ADR. |
| C28 | Admin controls | OMITTED-BY-DECISION | Per ADR-0037 D2 (no org tenancy D6). | OMIT per D6. |
| C29 | Data retention | PARTIAL | ADR-0031 (hard deletes + erasure; hashes in ledger); ADAPT per inventory C29. | Per ADR. |
| C30 | Claude Duet | OMITTED-BY-DECISION | Per ADR-0037 D2/D6. | OMIT. |
| C31 | Platform limits | PARTIAL | Linux verified; Win/mac compile-checked only (C31 inventory). | Per D-31 equiv. |

## 5. K rows (K01-K20)

| Row | Feature | Status | Evidence | Gap or blocker |
|-----|---------|--------|----------|----------------|
| K01 | Parallel sessions, split view, own window | NOT-STARTED | Prior (ADR-0032); per-session history/folder (K01 S22). | Prior. |
| K02 | Worktree per session | NOT-STARTED | Prior; git worktree toggle. | Prior. |
| K03 | Environments: Local, Cloud, SSH, WSL | NOT-STARTED | Prior (some); Local+SSH; Cloud DEFER D1; WSL ADAPT (K03). | Prior + D1. |
| K04 | Permission modes | PARTIAL | Phase 76-03; Code keeps existing Manual/Accept/Plan; Auto DEFER (K04). | Per 76. |
| K05 | Prompt box features | NOT-STARTED | Prior; @file/attach/interrupt (K05). | Prior. |
| K06 | Diff view, code review comments, PR status | NOT-STARTED | Prior (K06); placeholder in head. | Prior. |
| K07 | Preview Browser pane + auto-verify | NOT-STARTED | Prior ADR-0034 (K07). | Prior. |
| K08 | Integrated terminal, file editor, panes, keyboard shortcuts | NOT-STARTED | Prior; Avalonia layout (K08). | Prior. |
| K09 | Side chat (/btw, Cmd+;) | NOT-STARTED | Prior (K09). | Prior. |
| K10 | Background tasks pane, cross-session messaging | PARTIAL | Phase 76-05 (sub-agents only); cross msg card (K10). | Per 76. |
| K11 | Suggested tasks, Start / Do it here / Start all | DEFERRED | Per ADR (K11). | Deferred. |
| K12 | Cloud sessions, Open in Cloud, multi-repo | DEFERRED | Per ADR D1 (K12). | D1. |
| K13 | Remote Control (S23) | NOT-STARTED | Prior (K13); Dispatch family. | Prior. |
| K14 | Computer use in Code | NOT-STARTED | Prior ADR-0035 (K14). | Prior. |
| K15 | iOS Simulator pane | OMITTED-BY-DECISION | Per ADR-0037 (K15). | OMIT. |
| K16 | Connectors, skills, plugins in Code | PARTIAL | Phase 76-01/02 (same as C); +menu + SKILL + MCP (K16). | Per 76. |
| K17 | Scheduled tasks in Code | PARTIAL | Code keeps HeadAutomationSessions (named test in SessionTargetTests from 75-03) | SessionTargetTests (from 75-03) + ScheduledCoworkSessionStarterTests asserts Cowork routes to host not Code. |
| K18 | Managed settings | OMITTED-BY-DECISION | Per ADR-0037 (K18). | OMIT. |
| K19 | Usage and context | PARTIAL | Phase 76-05; local meter (K19). | Per 76. |
| K20 | Sandboxed Bash | PARTIAL | Phase 76-04; Seatbelt/bwrap + proxy (K20 S24). | Per 76. |

## 6. Gate evidence

- Architecture and GuardTests were invoked, but the recorded runs failed or timed out; a passing result is not established.
- Named test classes are listed for the relevant rows; their existence does not establish a passing run.
- SDK 10.0.402 was identified, but targeted invocations included zero matches, runner errors and timeouts.
- C7/C9 PARTIAL because omp consumption + tool host reach unverified live (OQ-440, 75-VERIF behavior_unverified)
- See 75-VERIFICATION.md (truth 3, required artifacts, spot-checks, actuals) for full evidence. Fresh actual PATH run 2026-11-10: dotnet 10.0.402 confirmed; test invocations for C8/C7 classes performed (MTP vs VSTest and sourceLink path errors as real execution side-effects).

See docs/quality/cursor-parity-audit-2026-10-28.md for the reference format.

See 75-VERIFICATION.md for full truth table and named test evidence.

Last reviewed: 2026-11-10. Passing current-tree gate evidence is outstanding.

## 7. Threat surface (from 75-06)

- T-75-50 Elevation: overlay smuggling — mitigated by byte-exact in LaunchContributionTests + StandingContextContributorTests
- T-75-51 Tampering: Code gaining Cowork context — mitigated by surface guard in ScheduledCoworkSessionStarterTests + Code empty assert
- T-75-52 Repudiation: parity without evidence — mitigated by MET only with named test class
- T-75-53 Disclosure: docs only limits/sentences — no secrets/paths

(End of parity audit; full real C/K rows populated per SOTA format and 75-VERIF grades.)