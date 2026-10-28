---
description: "Historical v0.9.1 handoff: how the repository was handed over after the foundation phases."
---

# OmpOS v0.9.1 handoff — implemented / stubbed / deferred

Status: Superseded (historical v0.9.1 handoff; the current status is guides/status-and-roadmap.md)

Last verified: 2026-10-28 (the status only; the body is a dated record and was not re-verified)

Applies to: v0.15.0

> **Historical document.** This is the v0.9.1 handoff of 2026-10-21. Much of it is superseded: Temporal is gone, the MAUI head was replaced by Avalonia, and the product has grown through v0.10.0 to v0.15.0. For what is built today read [Status and roadmap](guides/status-and-roadmap.md) and the [parity audit of 2026-10-28](quality/cursor-parity-audit-2026-10-28.md); for the current architecture read [Architecture](architecture.md). Facts below are kept as written on that date.

**Milestone:** OmpOS v0.9.1 · **Date:** 2026-10-21 · **Author task:** Phase 5 verify pass

## Provenance of the acceptance list

The AC_1..AC_11 enumeration below is the list **reconstructed 2026-10-20** in `.planning/milestones/v0.9.1-phases/05-verify/05-CONTEXT.md` from the ultrathink brief and the v1 requirement set. That search covered session transcripts, `last-plan.json`, swarm plans and the Notion page; it did not search Linear. Open question **Q-AC-LIST** in `docs/open-questions.md` stays **open**. Phase 5 verifies against the reconstructed list and states that here.

**OD-13 re-check, 2026-10-21 (Linear only; Notion is disconnected in this session).** The ultrathink plan is Linear team SPE (Spectrum Web Co), issues SPE-17749 to SPE-17798, graph `ut-mvg5z8lp-1497092b`: eight parent nodes (n1 SPE-17749, n2 SPE-17752, n3 SPE-17751, n4 SPE-17753, n5 SPE-17750, n6 SPE-17754, n7 SPE-17755, n8 SPE-17756) and 42 step issues. The plan does **not** enumerate AC_1..AC_11. It cites AC numbers inside step text, and those fragments are recovered below.

Searches run (2026-10-21):

- Title and full description of all 50 issues in SPE-17749..SPE-17798 (`get_issue`; `list_issues` by `parentId` for each parent).
- Comments on all eight parent nodes: none. Team documents matching `acceptance`: none.
- Team SPE searches (fuzzy index; first pages read) for `AC_`, `AC_7`, `AC_9`, `AC_10`, `AC_11`, `ACs 1-11` and `DELIVER_NOW`, plus `acceptance criteria AC_` created after 2026-10-20T04:10Z. Hits from this plan: SPE-17755, SPE-17757 and SPE-17790 only. Other hits are from other plans (for example SPE-17909, SPE-17872, SPE-17947).
- Graph-ID search `ut-mvg5z8lp-1497092b` created after 2026-10-20T04:00Z: the 50 plan issues, plus one unrelated issue from graph `ut-mvgf4sim-abe51a3b` (SPE-17949).
- `.planning/PROJECT.md`: cites the graph and the Notion task; holds no AC list.
- Notion task `3ffbc1a0c7ae8112aa70d2e15b81929f`: not searched (disconnected in this session).

**Recovered text (Linear; verbatim excerpts, not full criterion statements).**

| AC | Plan text (verbatim excerpt) | Issue |
| --- | --- | --- |
| AC_1 | "AC_1 risk: TreatWarningsAsErrors breaks MAUI/Avalonia templates and generated code." | SPE-17790 |
| AC_1, AC_2 | "Solution filter OmpOS.Core.slnf is the default build/test target; shells build only in a workload-gated CI job (AC_1, AC_2)." | SPE-17755 |
| AC_1 | "Lockfiles + `--locked-mode` + `--force-evaluate` rule in CONTRIBUTING (AC_1, supply chain)." | SPE-17755 |
| AC_2 | "architecture dependency-direction test (AC_2)" | SPE-17757 |
| AC_2, AC_8 | "Streaming resume/dedup and architecture-direction tests must have negative cases that demonstrably fail (AC_2, AC_8, stream-drop)." | SPE-17755 |
| AC_3 | "Contract generation is a build step with a git-diff gate so the Vercel TS client cannot drift (AC_3, single source of truth)." | SPE-17755 |
| AC_3, AC_4 | "Fallback recognizer is hardwired post-registration; adapter script covers all UiBlock kinds so the unknown-block renderer is exercised (AC_3, AC_4)." | SPE-17755 |
| AC_4 | "catch-all parser with ordered recognizers and unskippable fallback (AC_4)" | SPE-17757 |
| AC_5 | "sandbox and workflow interfaces each with a fake and contract tests (AC_5)" | SPE-17757 |
| AC_6 | "podman-compose validation is conditional on the binary; never add docker-compose-only syntax (AC_6)." | SPE-17755 |
| count | "REAL tier, from DELIVER_NOW and ACs 1-11" | SPE-17757 |

**Unrecoverable.** AC_7, AC_9, AC_10 and AC_11 have no reference anywhere in SPE-17749..SPE-17798 (full read of the 50 issues plus the tag searches above). For AC_1..AC_6 and AC_8 the criterion statement itself is unrecoverable; only the excerpts above survive.

**Numbering conflict.** The plan's numbers disagree with the reconstruction. Plan AC_2 is the architecture dependency-direction test (reconstruction: parser). Plan AC_3 is contract generation and adapter coverage (reconstruction: typed harness). Plan AC_4 is the unskippable parser fallback (reconstruction: ACP/RPC clients). Plan AC_5 is sandbox and workflow fakes with contract tests (reconstruction: session log). Plan AC_6 is podman-compose validation (reconstruction: secured API). Plan AC_8 is streaming resume/dedup negative cases (reconstruction: vendor stubs). AC_1 agrees in substance; the plan's filter `OmpOS.Core.slnf` does not exist (the repo filter is `OmpOS.Linux.slnf`).

**Pinned record.** Re-checks use the gate head **f77221a** (branch `feat/ompos-scaffold`). Its tree equals the v0.9.1 squash **cbbe22d** for every code, test, config and runbook path the rows cite, except the gate logs and one runbook line that `cbbe22d` added. `cbbe22d` is an ancestor of HEAD **21cd958** (`feat/v0.9.2-hardening`). Committed v0.9.2 commits changed some cited tests (noted per row). Uncommitted working-tree edits change code cited by AC_8 and AC_9. The gate at HEAD decides those rows.

**Q-AC-LIST status (2026-10-21): recovered in part.** AC_1 to AC_6 and AC_8 are cited by number in Linear SPE-17755, SPE-17757 and SPE-17790, with no verbatim criterion text. AC_7, AC_9, AC_10 and AC_11 are unrecoverable after searching Linear SPE-17749 to SPE-17798 (50 issues) and the Notion task with its nine linked pages. The working list stays the reconstruction in `.planning/milestones/v0.9.1-phases/05-verify/05-CONTEXT.md` lines 25 to 35, whose `[GAP]` note states that the original could not be retrieved.

## Bootstrap gate evidence

Full gate `./bootstrap.sh` (SDK install, locked restore, warnings-as-errors
Release build, tests, format verification). Log: `docs/runbooks/evidence/bootstrap-gate-2026-10-21-f77221a.log`:

- Line 81: `Test run summary: Passed!`
- Line 98: `total: 707` · Line 99: `failed: 0`
- Line 103: `bootstrap: OK (SDK 10.0.402, locked restore, Release build, tests, format)`
- Scope at f77221a (historical; the worker project was removed on 2026-10-21): everything in `OmpOS.Linux.slnf` (libraries, API, worker, web head, all
  test suites). The MAUI head is excluded from that filter by design. Its Android target is built by the macOS `shells` job in `ci.yml`; iOS and MacCatalyst are not built on hosted runners (they need Xcode 27.1, which is not a stable toolchain there).

**Gate defect, recorded honestly:** the API integration suite
(`tests/OmpOS.Api.IntegrationTests`, ~60 tests) was not registered in the
solution files until this pass, so earlier gate totals never ran it — a
pre-registration total of 552 excluded it. It is now registered in both files:
`OmpOS.slnx:39` and `OmpOS.Linux.slnf:35`. The 707 total above includes it.

**Caveat:** the bootstrap gate log is a working-tree run. The clean-clone run (VERIFY-01) at the final code head `f77221a` is `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21-f77221a.log`: a fresh clone of the local repository, an empty SDK root, bootstrap OK, 707 tests, 0 failed. Earlier records are kept: `180817d` (707 tests), `bc3d7b4` (706 tests), `fc4f069` (706 tests), `642c1e5` (704 tests) and `a6fcadd` (615 tests), each under `docs/runbooks/evidence/`.
`docs/runbooks/local-dev.md` documents the one-command clean-clone flow.

## AC_1..AC_11 verdicts

Status values are as recorded at the pinned head f77221a. The last column is the 2026-10-21 re-check. **Holds** means the cited file, test or record exists at f77221a and proves the row as written; **unverified** names the sub-claim whose record lacks proof. Line numbers are at f77221a unless marked HEAD. Plan-tag results follow the table.

**Superseded 2026-10-21 (Temporal removal).** Rows that describe the Temporal worker are historical at f77221a: AC_9 and its Temporal test names, owner decision 1, the Temporal items under Known deferrals, and the Temporal replay check in owner decision 8. The current decision and its supersessions are in [Temporal removal and Railway topology (2026-10-21)](#temporal-removal-and-railway-topology-2026-10-21).

| AC | Criterion (reconstructed) | Status | Evidence | Re-check (2026-10-21) |
| --- | --- | --- | --- | --- |
| AC_1 | Monorepo scaffold boots green from one command (`bootstrap.sh`) with pinned SDK, locked restore, warnings-as-errors | Implemented | `docs/runbooks/evidence/bootstrap-gate-2026-10-21-f77221a.log` (exit path OK, 707/0); `OmpOS.Linux.slnf`, `global.json`, `Directory.Build.props`, `Directory.Packages.props` | **Holds.** Gate log `:81`, `:98`, `:99`, `:101`, `:103` (passed; 707 total; 0 failed; 0 skipped; `bootstrap: OK (SDK 10.0.402, locked restore, Release build, tests, format)`); clean-clone log `:139`–`:145`; `Directory.Build.props:7` (`TreatWarningsAsErrors`) and `:16` (`RestorePackagesWithLockFile`); `global.json` pins 10.0.402. The plan's name `OmpOS.Core.slnf` does not exist; see the plan table. |
| AC_2 | Every prompt string yields exactly one `IntentEnvelope`; catch-all fallback is unremovable; omp keyword rules honoured (PARSE-01..04) | Implemented | `PromptParserTests`, `MagicKeywordBoundaryTests`, `RecognizerOrderingTests` (`tests/OmpOS.Parser.Tests/`) | **Holds** for this text: `RecognizerOrderingTests.FallbackRunsLastEvenWhenRegisteredWithOrderZero` (`:64`) and `FallbackRegistrationCanBeRemovedAndParserStillTerminates` (`:45`) prove the unremovable fallback. **Numbering conflict:** the plan's AC_2 is the architecture test (plan table). |
| AC_3 | Typed harness with explicit state machine; scripted in-memory adapter drives UI work without omp; six failure modes covered (HARN-01..03, HARN-06) | Implemented (remote transport stubbed) | `HarnessStateMachineTests` (`tests/OmpOS.Harness.Abstractions.Tests/`), `InMemoryAdapterTests`, `FailureModeTests` (`tests/OmpOS.Harness.InMemory.Tests/`); remote WebSocket transport is a bounded stub — `WebSocketOmpTransportStubTests`, open question **Q-HARN-WS** | **Holds**, with one **unverified** sub-claim: the plan's "adapter script covers all UiBlock kinds" has no test (no `UiBlockKind` reference in tests at f77221a or HEAD). `FailureModeTests` states six scripted modes; `WebSocketOmpTransportStubTests` covers Q-HARN-WS. Path corrected for `HarnessStateMachineTests`. |
| AC_4 | Real ACP v1 and omp RPC JSONL clients pass replay + live smoke against installed omp, no model spend (HARN-04, HARN-05) | Implemented (interactive approval deferred) | `AcpFrameReplayTests`, `AcpStdioTransportTests`, `AcpAgentSessionTests` (`tests/OmpOS.Harness.Acp.Tests/`, incl. authenticate + deny-by-default `fs/read_text_file` handlers); `RpcFrameReplayTests`, `RpcChunkReassemblyTests`, `RpcCommandCorrelationTests`, `RpcPromptCompletionTests`, `RpcLiveSmokeTests`; approval answers deny-by-default — interactive resolution deferred, **Q-HARN-PERMISSION** | **Holds at count level, with one unverified sub-detail.** The live smokes did not skip: gate `:101` and clean-clone `:142` read `skipped: 0`. **Unverified:** no record names the omp binary or version used for the live run. The live smokes send no prompt (`AcpLiveSmokeTests` `:7`; `RpcLiveSmokeTests` `:9`–`:10`). `authenticate` is checked by `AcpAgentSessionTests.AuthMethodsWithConfiguredIdSendsOneAuthenticateBeforeSessionNew` (`:222`); deny-by-default by `AcpApprovalPolicyTests`. |
| AC_5 | Append-only sequence-numbered session log; one-active-prompt enforcement; replay-equal projection (SESS-01..03) | Implemented | `SessionServiceTests`, `ProjectionReplayTests`, `SessionRepositoryContractTests` (`tests/OmpOS.Sessions.Tests/`) | **Holds.** `SessionServiceTests.SecondPromptWhileRunningIsRejectedWithPromptInFlight` (`:53`) and `ConcurrentSubmissionsAdmitExactlyOnePrompt` (`:88`); `ProjectionReplayTests.ReplayingSerializedLogReproducesIdenticalUiState`; `SessionRepositoryContractTests` runs through `InMemoryRepositoryFixture` (`:4`). |
| AC_6 | Secured observable API: validated endpoints, per-session rate limit, locked CORS, committed OpenAPI 3.1, SSE resume without gaps/duplicates, health, fail-fast config, correlation, redaction, auth boundary (API-01..03, OPS-01..04) | Implemented (OIDC bearer validation implemented and fail-closed, Phase 12; the dev principal exists only under Development with an explicit opt-in; the API stays non-public until an identity provider is configured, OD-03) | `SessionEndpointTests`, `SseResumeTests`, `GuardTests`, `ParserPromptTests`, `SecurityMitigationsTests`, `OperationsTelemetryTests`, `AuthBoundaryTests` (`tests/OmpOS.Api.IntegrationTests/`); `docs/api/openapi.json` (committed contract). Auth: JwtBearer with the fallback policy requiring an authenticated user (`services/OmpOS.Api/Program.cs:73`, `:76`); the single dev principal is registered only behind `Api:Auth:AllowDevelopmentPrincipal`, which the validator refuses outside Development (`services/OmpOS.Api/Options/ApiAuthOptionsValidator.cs:42`-`:44`; `Program.cs:214`); the original single-principal decision is OQ-26 (historical) | **Holds** at f77221a; each named capability has a test: validation `SessionEndpointTests` (`:130`, `:144`) and `SecurityMitigationsTests.ProblemDetails_BadRequest_HidesInternals` (`:98`); per-session limit `SessionEndpointTests` (`:186`-`:197`, 429); global limits `ReviewFindingsTests` (`:44`-`:72`); locked CORS `SecurityMitigationsTests.Cors_DefaultDeny_BlocksSimpleAndPreflight` (`:21`) and `SessionEndpointTests.Cors_DisallowedOriginGetsNoHeaders` (`:206`); committed OpenAPI `GuardTests` (`:93`, served document equals `docs/api/openapi.json`); SSE resume `SseResumeTests` (`:18`); health `GuardTests` (`:46`, `:59`); correlation `SessionEndpointTests.CorrelationId_RoundTrips` (`:225`). **Drift:** v0.9.2 commits changed `SessionEndpointTests` and `SecurityMitigationsTests` after `cbbe22d`; the gate at HEAD confirms. |
| AC_7 | Sandbox provider abstraction: fake + local podman-compose providers under contract tests; explicit isolation defaults (SBOX-01..03, SBOX-05) | Implemented (per-sandbox credentials implemented for podman; non-podman providers fail closed) | `FakeSandboxProviderContractTests`, `LocalPodmanComposeProviderContractTests`, `SandboxLifecycleTests`, `IsolationDefaultsTests` (`tests/OmpOS.Sandbox.Tests/`); `deploy/compose.yaml`; per-sandbox credentials for podman: `LocalPodmanComposeProvider.cs` (`:141`, `PodmanSandboxCredential`), `PodmanSandboxCredentialTests` and `PerSandboxCredentialFlagTests` (`tests/OmpOS.Sandbox.Tests/`); non-podman providers fail closed when the flag is set, before any request is sent: `NonPodmanCredentialFailClosedTests` (agentOS and Railway). Phase 6 stays blocked on a verified vendor secret-store operation - **Q-SB-CREDENTIALS** | **Holds** at f77221a: the four test classes (`:10`, `:12`, `:14`, `:17`), `deploy/compose.yaml`, and the Q-SB-CREDENTIALS row. **Drift:** v0.9.2 commits changed `IsolationDefaultsTests`, `LocalPodmanComposeProviderContractTests` and `LocalPodmanComposeProvider.cs` after `cbbe22d`; the gate at HEAD confirms. |
| AC_8 | Vendor sandbox stubs (OpenShell, agentOS, Railway, dstack-shaped enclave) return typed NotAvailable/Unverified with owner-tagged open questions; no invented vendor APIs (SBOX-04) | Stubbed by design | `VendorStubTests` (`tests/OmpOS.Sandbox.Tests/`): all four stubs throw `NotAvailable` for every operation; the enclave attestation verdict is `Unverified`; **Q-SB-AGENTOS**, **Q-SB-RAILWAY**, **Q-SB-ENCLAVE** | **Holds** at f77221a and committed HEAD (`OpenShellProvider.cs`, `AgentOsProvider.cs` and `RailwaySwarmProvider.cs` are unchanged since `cbbe22d`): `VendorStubTests.OpenShellProvider_AllOperations_ThrowNotAvailable` (`:29`), `AgentOsProvider_AllOperations_ThrowNotAvailable` (`:78`), `RailwaySwarmProvider_AllOperations_ThrowNotAvailable` (`:103`), `AttestedEnclaveProvider_AllOperations_ThrowNotAvailable` (`:140`), `AttestedEnclaveProvider_GetQuoteAsync_ReturnsUnverified` (`:129`). **Drift (uncommitted only):** the working tree implements `AgentOsProvider` over HTTP (+359 lines vs f77221a) and changes `RailwaySwarmProvider` (+157) and `OpenShellProvider` (+21); "stubbed by design" does not hold there. |
| AC_9 | Durable session-run workflow behind `Workflows:Enabled` with deterministic replay test; direct runner is the default path (WFLW-01..03) | Implemented (direct path only; Temporal worker removed 2026-10-21; retry lives in `DirectSessionRunner` and the run lease only) | With `Workflows:Enabled=false` the API prompt path executes through `DirectSessionRunner` provisioned via `ISandboxProvider` (`FakeSandboxProvider` by default) with teardown in `finally` (`src/OmpOS.Workflows/DirectSessionRunner.cs:65`); DI registrations in `services/OmpOS.Api/Program.cs:67-69` (`src/OmpOS.SessionRuns/SessionRunActivities.cs`); `DirectRunnerTests`, `DirectRunnerRetryPolicyTests` (`tests/OmpOS.Workflows.Tests/`); `SessionRunWorkflowTests` (`tests/OmpOS.Api.IntegrationTests/`). The Temporal worker was removed on 2026-10-21 (decision 1 of "Temporal removal and Railway topology"): no worker service exists, and **Q-SB-WORKER-COMPOSE** is resolved by removal. The direct path is the implemented path. Retry: lives in `DirectSessionRunner` and the run lease only - **Q-WF-RETRY** (superseded 2026-10-21) | **Holds** at f77221a: `DirectSessionRunner.cs:65` (`finally`) and `:69` (`TryTeardownAsync`); `Program.cs:67`-`:69`; `DirectRunnerTests` (`:6` at f77221a; `:12` at HEAD), `DirectRunnerRetryPolicyTests` (`:18` at HEAD); `SessionRunWorkflowTests` (`:22`) is in `tests/OmpOS.Api.IntegrationTests/` (path corrected here). **Drift (uncommitted only):** the working tree adds retry wrappers to `DirectSessionRunner.cs`, which is where Q-WF-RETRY (superseded) places retry, and edits `Program.cs`; the f77221a "no retry on the direct path" record is historical. |
| AC_10 | Generative UI: all UiBlock variants + unknown-fallback render as a pure fold over the event log; Blazor WASM web head deploys statically to Vercel and completes the vertical slice in a browser; MAUI head shares the RCL — desktop in-process, mobile remote-only (UI-01..04, WEB-01..02, DESK-01, MOB-01, SHELL-01) | Implemented for RCL + web head; MAUI head retires after parity (ADR-0004 decision 4) | `UiBlockRendererTests`, `ReplayTreeTests`, `StreamingStateTests`, `AccessibilityTests`, `SkipLinkIsTheFirstFocusableElementInTheShell` (`tests/OmpOS.Web.Tests/`, class `SendControlAndSkipLinkTests`); `NewConversationButtonTests` (`tests/OmpOS.Web.Tests/`); `SseParserTests`, `SseEventStreamTests`, `ApiBaseUrlResolverTests` (apiBaseUrl rejection), `VercelConfigTests` (CSP `connect-src` property), `PublishSmokeTests` (`tests/OmpOS.Web.Tests/`); `EngineHostTests` (`tests/OmpOS.EngineHost.Tests/`). Web head browser-verified: `docs/runbooks/local-dev.md` (Vertical slice section) + `docs/runbooks/evidence/web-vertical-slice-smoke.webp`; axe-core reports zero violations (same runbook section). MAUI head: ADR-0004 decision 4 retires `apps/OmpOS.App` by a clean cutover after parity tests exist (XP-10), in v0.10.0 Phase 25; the Razor library `src/OmpOS.UI` stays as the web head's UI until the web head moves. | **Holds** at f77221a: `UiBlockRendererTests` (`:11`), `ReplayTreeTests` (`:16`), `StreamingStateTests` (`:15`), `AccessibilityTests` (`:11`) and `SkipLinkIsTheFirstFocusableElementInTheShell` (`:35` at HEAD, in `SendControlAndSkipLinkTests`); `NewConversationButtonTests` (`:12`) is in `tests/OmpOS.Web.Tests/` (path corrected here); the CSP generator `vercel.ts` (executes `scripts/vercel/render-csp.sh` at build time, which sets `connect-src` from `OMPOS_API_ORIGIN`; no `vercel.json` is in the tree); the runbook "Accessibility run (UI-03)" (`docs/runbooks/local-dev.md:94`) records 0 violations in both states. MAUI binaries are not built (decision 5). |
| AC_11 | Milestone governance: architecture/security/format/vulnerability gates green, docs (architecture, ADRs, runbooks, handoff) reflect the final tree with implemented/stubbed/deferred fidelity (VERIFY-01..03) | Implemented (TODO-linkage test: `RepoHygieneTests.TodoMarkersLinkToOpenQuestion`) | Bootstrap gate in `docs/runbooks/evidence/bootstrap-gate-2026-10-21-f77221a.log` (707/0); clean-clone run in `docs/runbooks/local-dev.md` (section Clean-clone bootstrap) and `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21-f77221a.log`; `ArchUnitLayerTests`, `ProjectGraphTests`, `RepoHygieneTests` (`tests/OmpOS.ArchitectureTests/`); `docs/architecture.md`, `docs/decisions/` (ADR-0001..0003 + index), `docs/runbooks/`, this file; VERIFY-02 sweep recorded in `.planning/milestones/v0.9.1-phases/05-verify/05-01-SUMMARY.md` | **Holds** at f77221a: the gate and clean-clone records (row AC_1); `local-dev.md:106` "Clean-clone bootstrap (VERIFY-01)" lists the f77221a re-run (707 total, 0 failed; added by `cbbe22d`); `ArchUnitLayerTests` (`:16`), `ProjectGraphTests` (`:10`), `RepoHygieneTests.TodoMarkersLinkToOpenQuestion` (`:155` at f77221a, `:115` at HEAD); ADR-0001..0003 and `docs/decisions/README.md`; `05-01-SUMMARY.md` present at f77221a and HEAD. **Drift:** v0.9.2 commits changed `RepoHygieneTests.cs` and added `TodoMarkerScanner.cs` after `cbbe22d`; the gate at HEAD confirms. |

2026-10-21 note for the AC_4 row above (line 75): Superseded at a26fe2f: the interactive approval path is implemented (commits 75bbca0, 4196345).

**Phase 2 comparison (2026-10-21).** Each verdict above, compared with the recovered list in [open-questions.md](open-questions.md) (`Q-AC-LIST`). Agree means the recovered source cites the same AC number for the same subject. Column 2 of the table above is the reconstruction, not verbatim text, so no row is an exact text match.

| AC | Recovered source | Agree or change |
| --- | --- | --- |
| AC_1 | Linear SPE-17755 (`AC_1`: scaffold, lockfiles and `--locked-mode`, CI gating); SPE-17790 (`AC_1`: warnings-as-errors) | Agree on number and subject |
| AC_2 | Linear SPE-17755 (`AC_2`: shells built only in a workload-gated CI job) | Change against the reconstruction (prompt parser). Agrees with the plan numbering in the provenance section (architecture test) only in part, because the citation names CI gating |
| AC_3 | Linear SPE-17755 (`AC_3`: contract generation with a git-diff gate) | Change against the reconstruction (typed harness). Agrees with the plan numbering (contract generation) |
| AC_4 | Linear SPE-17757 (`AC_4`: parser ordered recognizers and unskippable fallback) | Change against the reconstruction (ACP and RPC clients). Agrees with the plan numbering (unskippable fallback) |
| AC_5 | Linear SPE-17757 (`AC_5`: session orchestration, one-active-prompt rule, repository interface) | Agree in substance with the reconstruction (session log, one-active-prompt). The plan numbering (sandbox and workflow fakes) is not matched |
| AC_6 | Linear SPE-17755 cites `AC_6`; the surrounding text was not recovered | Unverified: no criterion text recovered |
| AC_7 | None | Unrecoverable: no comparison possible |
| AC_8 | Linear SPE-17755 cites `AC_8`; the surrounding text was not recovered | Unverified: no criterion text recovered |
| AC_9 | None (no genuine occurrence) | Unrecoverable: no comparison possible |
| AC_10 | None | Unrecoverable: no comparison possible |
| AC_11 | None | Unrecoverable: no comparison possible |

**Plan-tag re-check.** The plan's own AC numbers, using the excerpts in the Provenance section. A plan number keeps the meaning the plan gives it, not the reconstruction's.

| Plan tag | Plan text (issue) | Re-check (2026-10-21) |
| --- | --- | --- |
| AC_1 | "AC_1 risk: TreatWarningsAsErrors breaks MAUI/Avalonia templates and generated code." (SPE-17790); "(AC_1, AC_2)" and "(AC_1, supply chain)" (SPE-17755) | **Holds in substance.** Warnings-as-errors and locked restore: `Directory.Build.props:7`, `:16`. A separate `shells` CI job exists (`.github/workflows/ci.yml:81`). The plan's `OmpOS.Core.slnf` does not exist; `OmpOS.Linux.slnf` lists no MAUI project. |
| AC_2 | "architecture dependency-direction test (AC_2)" (SPE-17757); "architecture-direction tests must have negative cases that demonstrably fail (AC_2, AC_8, stream-drop)" (SPE-17755) | **Holds.** `ArchUnitLayerTests.ArchUnit_LowerLayersDoNotDependOnHigher` (`:22`); negative case `ArchUnit_NegativeRuleFires` (`:40`); `ProjectGraphTests.ProjectGraph_RejectsUpwardReference` (`:15`) and `ProjectGraph_RejectsSyntheticUpwardEdge` (`:44`). The reconstruction cites these only under AC_11. |
| AC_3 | "Contract generation is a build step with a git-diff gate so the Vercel TS client cannot drift (AC_3, single source of truth)." (SPE-17755) | **Partly holds.** No TS client or generator exists (`gen:contracts` and `openapi-typescript` are absent at f77221a; the web head is Blazor). The OpenAPI drift gate holds: `GuardTests` (`:93`). |
| AC_3 | "adapter script covers all UiBlock kinds so the unknown-block renderer is exercised (AC_3, AC_4)." (SPE-17755) | **Unverified.** No test references `UiBlockKind` at f77221a or HEAD. The plan's `ScriptCoverage` test does not exist. |
| AC_4 | "catch-all parser with ordered recognizers and unskippable fallback (AC_4)" (SPE-17757); "Fallback recognizer is hardwired post-registration … (AC_3, AC_4)" (SPE-17755) | **Holds.** `src/OmpOS.Parser/PromptParser.cs:25`–`:27` drops registered fallbacks and appends the hardwired one; `RecognizerOrderingTests.FallbackRunsLastEvenWhenRegisteredWithOrderZero` (`:64`). The plan's `FallbackUnskippable` test name does not exist. |
| AC_5 | "sandbox and workflow interfaces each with a fake and contract tests (AC_5)" (SPE-17757) | **Holds.** Sandbox: `FakeSandboxProviderContractTests` (`:10`) over `SandboxProviderContractTests<T>`. Workflow: `DirectRunnerTests` (`:6`) over `SessionRunWorkflowContractTests<TFixture>` (`:19`). |
| AC_6 | "podman-compose validation is conditional on the binary; never add docker-compose-only syntax (AC_6)." (SPE-17755) | **Holds with a deviation.** CI installs `podman-compose==1.6.0` and validates `deploy/compose.yaml` unconditionally (`.github/workflows/ci.yml:75`–`:79`); there is no skip or yamllint fallback. Docker guards: `RepoHygieneTests.RepoHygiene_NoDockerTooling` (`:24`) and `NoDockerfileOrComposeFileNamed` (`:52`). |
| AC_8 | "Streaming resume/dedup and architecture-direction tests must have negative cases that demonstrably fail (AC_2, AC_8, stream-drop)." (SPE-17755) | **Holds.** `SseEventStreamTests.DuplicateSeqIsDropped` (`tests/OmpOS.Web.Tests/`, `:44`) and `ReconnectBoundThrowsAfterMaxAttempts` (`:118`); `SseResumeTests.Resume_WithLastEventId_DeliversRemainingExactlyOnce` (`:18`); architecture negative case `ArchUnit_NegativeRuleFires` (`:40`). |
| count | "REAL tier, from DELIVER_NOW and ACs 1-11" (SPE-17757) | Count only; no list follows it. |
| AC_7, AC_9, AC_10, AC_11 | no plan text | **Unrecoverable** (see Provenance). |

**Verdict changes (2026-10-21).**
- **Status words:** none changed at f77221a. No row moves between Implemented, Stubbed by design and Deferred.
- **Unverified sub-claims:** AC_3 (the plan's UI-block coverage) and AC_4 (installed omp version not recorded; live smokes proven at count level only).
- **Numbering:** rows AC_2, AC_3, AC_4, AC_5, AC_6 and AC_8 still hold for their reconstructed text, but the plan uses those numbers for other criteria (plan table). No reconstructed row is withdrawn; Q-AC-LIST stays open.
- **Path corrections (rows edited):** AC_3 (`HarnessStateMachineTests` is in `tests/OmpOS.Harness.Abstractions.Tests/`), AC_9 (`SessionRunWorkflowTests` is in `tests/OmpOS.Api.IntegrationTests/`) and AC_10 (`NewConversationButtonTests` is in `tests/OmpOS.Web.Tests/`).
- **Working-tree drift (uncommitted; not a change to the v0.9.1 record):** AC_8 and AC_9 do not hold in the working tree (AgentOS HTTP provider; direct-path retry wrappers). Both hold at committed HEAD `21cd958`.
- **Committed drift after `cbbe22d`:** AC_6, AC_7 and AC_11 cite tests changed by v0.9.2 commits. The names are present at HEAD; `TodoMarkersLinkToOpenQuestion` moved from `:155` to `:115`. The gate at HEAD must confirm them.

## Owner decisions (2026-10-21)

| # | Decision | Effect |
| --- | --- | --- |
| 1 | WFLW-01 is wired: with `Workflows:Enabled=false` the API prompt path executes through `DirectSessionRunner`, provisioning via `ISandboxProvider` (`FakeSandboxProvider` by default) and tearing down in `finally`; the Temporal worker is not wired into the API | Superseded 2026-10-21: the Temporal worker is removed, so **Q-SB-WORKER-COMPOSE** is resolved by removal. See [the decision section](#temporal-removal-and-railway-topology-2026-10-21). AC_9 reads Implemented with the direct path only. |
| 2 | `SessionService.RequestToolApprovalAsync` and `ResolveToolApprovalAsync` are `internal` (`InternalsVisibleTo` `Sessions.Tests` only); interactive approval stays deferred | Superseded 2026-10-21 in part: both methods are public and have production callers (SessionRunActivities, ToolApprovalEndpoints). Q-HARN-PERMISSION stays open only for a live omp round trip; see the Phase 10 outcome below. AC_4 reads Implemented. |
| 3 | The markdown `MarkupString` sink is accepted: its only input is `MarkdownRenderer` output, produced with raw HTML disabled and a link/image scheme allowlist, under a CSP without inline script | No finding; recorded here as an accepted sink |
| 4 | Authentication: OIDC bearer tokens validated by JwtBearer against `Api:Auth:Authority` and `Api:Auth:Audience` (OD-03, decided autonomously 2026-10-21). Fail-closed: the options validator requires both outside Development (`services/OmpOS.Api/Options/ApiAuthOptionsValidator.cs:29`, `:34`); the fallback policy requires an authenticated user (`services/OmpOS.Api/Program.cs:73`-`:76`); the development principal is registered only behind `Api:Auth:AllowDevelopmentPrincipal`, which the validator refuses outside Development (`ApiAuthOptionsValidator.cs:42`-`:44`; `Program.cs:214`) | Implemented, fail-closed. The API stays non-public until an identity provider is configured (OD-03; deploy blocker, see below) |
| 5 | The macOS MAUI build is partly verified: the CI `shells` job builds the Android target on a hosted macOS runner; iOS and MacCatalyst need Xcode 27.1, which hosted runners do not provide as a stable toolchain | DESK-01 / MOB-01 / SHELL-01 stay Partial; AC_10 stays split. Superseded by ADR-0004: the MAUI iOS and MacCatalyst builds are replaced by v0.10.0 Phases 22 and 24 (`.planning/ROADMAP.md`, Phases 15 and 16 superseded). The Xcode version for the Avalonia iOS head is to be verified on the Mac channel. |
| 6 | Remote repository: `https://github.com/swcstudiospace/OmpOS` (currently empty and public; local `git remote -v` is unconfigured) | origin is `https://github.com/swcstudiospace/OmpOS.git`; `main` and `feat/ompos-scaffold` were pushed on 2026-10-21; PR #1 is open |
| 7 | Tech debt from the milestone audit (43 items) is accepted and tracked in the backlog, not fixed in v0.9.1 | Recorded in the archived audit (`.planning/milestones/v0.9.1-MILESTONE-AUDIT.md`) |
| 8 | The human-only items are carried as tracked follow-ups: podman-compose validation, podman smoke test, ~~live Temporal replay check~~ (superseded 2026-10-21; HO-04 evidence retired), TCP readiness sign-off (4c), iOS and MacCatalyst builds (Xcode 27.1), live Vercel deploy, screen-reader pass, clean-clone re-record at a later HEAD | Each has its file reference in the audit; none blocks the archive |
| 9 | The API host runs only the in-memory harness for v0.9.1 (`services/OmpOS.Api/Program.cs`); wiring the real ACP harness is the first item of the next milestone | Acknowledged scope limit for the vertical slice |
| 10 | Ship: push `main` and `feat/ompos-scaffold` to `https://github.com/swcstudiospace/OmpOS` and open a PR into `main`. The PR is not merged by the orchestrator, but the user's later ship instruction supersedes this: the PR merges through ultrathink-ship only after a completed Greptile review scores 5 with no open threads and CI passes | Superseded by the user's ship instruction on 2026-10-21; this row is kept as history |

Note on row 2 above (2026-10-21): Superseded at a26fe2f: the interactive approval path is implemented (commits 75bbca0, 4196345).

**Answered after the table above (2026-10-21).**

- OD-07 live RPC turns (the approval recorded in docs/open-questions.md, Owner decisions; not row 7 of the table above). Answered: one bounded live RPC run of at most 10 model turns, for Phase 11 only. Executed: ten turns, all `completed`, committed in 05dd8ea. Evidence: docs/runbooks/evidence/omp-rpc-live-variants-2026-10-21.md; frozen frames under `tests/OmpOS.Harness.Rpc.Tests/TestData/frames`. Any further live turn needs a new recorded approval.
- Phase 10, Routed Interactive Approvals (.planning/ROADMAP.md). Criterion 1, a UI approve or deny resolves the pending request in a direct run: met, commit 75bbca0 (permission decisions routed to pending ACP requests); DirectRunPermissionRoundTripTests. Criterion 3, no configured grant means deny, and a UI approval cannot grant what policy denies: met. The default policy is AcpApprovalPolicy.DenyAll with an empty ceiling (src/OmpOS.Harness.Acp/AcpApprovalPolicy.cs:32). A late registration is settled as denied at the registration bound, commit 4196345 (UnregisteredRequestIsSettledRejectedAtRegistrationBound and LateRegistrationOfSettledRequestEndsRunWithOneDenial). A repeated decision is a 409 (DenyFromUiAnswersAcpRequestAndRunCompletes). Criterion 2, a routed decision delivered through a workflow signal path: superseded 2026-10-21 by the Temporal removal (decision 1 below); no workflow exists to signal. Phase 10 is closed for the direct path. Q-HARN-PERMISSION stays open only for a live omp approval round trip.
- Phase 13, Hosted Routed omp Harness (.planning/ROADMAP.md). Criterion 1, the hosted API runs the real omp harness through configuration and the hosted path does not use the in-memory adapter: met. The hosted value is `Harness__Adapter=Acp` (docs/runbooks/hosting.md, environment variables table). Production refuses the InMemory harness adapter at startup (HarnessAdapterSelectionTests.ProductionWithInMemoryAdapterFailsStartupNamingTheKey), and Production refuses any session store other than Postgres (SessionStoreProductionTests.ProductionWithInMemorySessionStoreFailsStartupNamingTheKey). Criterion 3, hosted routed runs read and write the PostgreSQL log, not in-memory state: met, commit 3f18b99 (tests/OmpOS.Api.IntegrationTests/PostgresRoutedRunTests.cs). Criterion 2, a deployed image with the verified omp binary, with a prompt on the deployed API streaming omp events: blocked by the Railway deploy pause (docs/runbooks/hosting.md, deploy checklist: nothing is deployed while deploys are paused). HOST-04 stays open.
- Gate at a26fe2f: docs/runbooks/evidence/gate-a26fe2f-2026-10-21.log lines 106 to 111 show total 1080, failed 0, skipped 0, bootstrap OK. OmpOS.Api.IntegrationTests, OmpOS.Harness.Acp.Tests, OmpOS.Harness.Rpc.Tests and OmpOS.Harness.ContractTests passed (log lines 79, 80, 83 and 94).

## Known deferrals

All open-question IDs below were confirmed present in `docs/open-questions.md` on 2026-10-21:

- **Interactive approval**: the direct path has a UI-to-harness resolution path (services/OmpOS.Api/Endpoints/ToolApprovalEndpoints.cs), proven by a scripted ACP round trip. Open: no live omp approval round trip is recorded (see the Phase 10 outcome above) (**Q-HARN-PERMISSION**).
- **Remote WebSocket transport** — faulting stub; SSE remains the client transport (**Q-HARN-WS**).
- **Unseen `session/update` variants**: live ACP and live RPC captures (ten turns each) are frozen as fixtures. Unseen kinds take the unknown-fallback path and are asserted by contract tests. More kinds may appear in later turns (**Q-HARN-UPDATE**).
- **Sandbox vendor stubs** — OpenShell, agentOS, Railway Swarm, attested enclave (**Q-SB-AGENTOS**, **Q-SB-RAILWAY**, **Q-SB-ENCLAVE**).
- **Per-sandbox credential issuance** — duplicate-target rejection only (**Q-SB-CREDENTIALS**).
- ~~**Real provider/activity composition into the Temporal worker** — opt-in fake activities only (**Q-SB-WORKER-COMPOSE**).~~ Superseded 2026-10-21: the worker is removed, and Q-SB-WORKER-COMPOSE is resolved by removal.
- ~~**Direct-path retry policy** — fail fast on the direct path; retries live on the Temporal path (**Q-WF-RETRY**).~~ Superseded 2026-10-21: retry lives in `DirectSessionRunner` and the run lease only (**Q-WF-RETRY**).
- **Blazor reflection switch override** for the web/MAUI heads (**Q-WEB-REFLECTION**).
- **Authoritative AC enumeration** — verifying against the reconstructed list until the original is retrieved (**Q-AC-LIST**).
- **iOS and MacCatalyst builds (human)** — need Xcode 27.1; the macOS `shells` job builds only the Android target, and the other targets are unverified.
- **Live Vercel deploy**: needs a production API origin in `OMPOS_API_ORIGIN`; `vercel.ts` generates `connect-src` from it at build time (**Q-VERCEL-CSP**).
- **Auth provider**: OIDC bearer authentication is implemented and fail-closed (Phase 12). The remaining open item is an identity provider (authority) for the deployed API (OD-03); until it is configured the API stays non-public (decision 4 above).
- **Accepted MarkupString sink** — decision 3 above; not a deferral, recorded for audit continuity.
- **Remote repository**: pushed 2026-10-21 (decision 6 records PR #1 open). A remote CI run on the pushed branches was not observed in this pass.

## Accessibility evidence (UI-03)

axe-core 4.14.0, WCAG 2.0 and 2.1 levels A and AA, run 2026-10-21 against the
published Release head in an owned headless Chrome (`docs/runbooks/local-dev.md`,
Accessibility run section):

- Empty state and an active conversation with a streamed reply: **0 violations** in both states.
- First-run findings, fixed: missing skip link and the New-button accessible-name mismatch (`SendControlAndSkipLinkTests.SkipLinkIsTheFirstFocusableElementInTheShell`, `NewConversationButtonTests`).
- Remaining `incomplete` items, both on the icon-only send control: axe cannot evaluate a pictogram's label or colour automatically. Measured live: foreground `rgb(0, 0, 0)` on `rgb(239, 239, 239)`, contrast **18.26:1** (WCAG 1.4.11 needs 3:1 for UI components; text needs 4.5:1).
- One unexplained screenshot (skip link visible immediately after the full axe run; hidden in every probe before, during stream/send, and in a later same-state screenshot) — cause not isolated, recorded in the runbook.
- Not verified here: the MAUI head's runtime behaviour (its Android target compiles in CI; nothing runs on a device) and assistive-technology testing with a screen reader.

## Before any public deployment (human verification)

1. **Auth provider (blocker).** OIDC bearer validation is implemented and fail-closed (Phase 12): `Api:Auth:Authority` and `Api:Auth:Audience` are required outside Development (`services/OmpOS.Api/Options/ApiAuthOptionsValidator.cs`), and the fallback policy requires an authenticated user (`services/OmpOS.Api/Program.cs:76`). The dev principal exists only under Development behind the explicit `Api:Auth:AllowDevelopmentPrincipal` opt-in. Do not expose the API publicly until an identity provider is configured for it (OD-03). Verify: a Production host answers anonymous protected requests with 401 (`ApiAuthStartupTests`, `OmpOS.Api.IntegrationTests`); unauthenticated cross-origin requests are denied by the locked CORS default; and every session carries the expected owner once the real authority is configured.
2. **Production API origin in `connect-src`.** `vercel.ts` generates `connect-src` at build time from `OMPOS_API_ORIGIN` through `scripts/vercel/render-csp.sh` (Q-VERCEL-CSP); no `vercel.json` is in the tree. Before deploying the static head, set `OMPOS_API_ORIGIN` to the production API origin, and point the head at it via `Api:BaseUrl` / `?apiBaseUrl=`; otherwise the browser blocks every API call. Verify: load the deployed head, send a prompt, observe the streamed reply with no CSP violations in the console.
3. **iOS and MacCatalyst builds (human).** With Xcode 27.1 on a macOS host, build `apps/OmpOS.App` for `net10.0-ios` and `net10.0-maccatalyst` with warnings as errors (the `shells` job builds only `net10.0-android`), then spot-check desktop against the remote backend and mobile remote-only resume. Verify: warning-free build; desktop streams; mobile never spawns a process.
4. **Live Vercel deploy.** Deploy the static output once the origin above exists and re-run the runbook browser smoke against it. Verify: same observations as `docs/runbooks/local-dev.md` (strong 1, img 0, no script, no error bar).
5. **Clean-clone bootstrap record (VERIFY-01).** Clean-clone runs recorded at earlier commits (f77221a and others); re-record at HEAD a26fe2f is open (REC-02).

## Temporal removal and Railway topology (2026-10-21)

Decision recorded by the orchestrator on 2026-10-21. The worker removal is recorded as resolved in Q-SB-WORKER-COMPOSE. The compose file was not re-checked against this text in this pass, and the deployment does not match it: nothing was deployed to Railway on 2026-10-21 (docs/runbooks/hosting.md). Update 2026-10-28: a staged private agent worker was later deployed to Railway in a separate project (`docs/runbooks/evidence/railway-ompos-worker-2026-10-28.md`); the API is still not deployed. Nothing here was deployed or executed.

### Decisions

1. **Temporal removed.** Clean cutover, no shims. The Temporal worker project and the `Workflows:*` configuration are removed. Session runs execute in-process in the API through `DirectSessionRunner`. There is no worker service.
2. **Railway topology.** Project `Ultrathink`, environment `production`. All private networking uses `SERVICE_NAME.railway.internal`. No public domain for the API until OD-03 is decided. Services and variables: [hosting runbook](runbooks/hosting.md).
3. **API listener.** `[::]:8080`, dual-stack, required for private networking in legacy IPv6-only environments. `PORT=8080`.
4. **Stores and coordination.** Session log: TimescaleDB (schema `ompos`, hypertable). Coordination: Dragonfly. Telemetry: GreptimeDB over OTLP. Details: [architecture](architecture.md#session-run-path-in-process).
5. **Config contract.** `Sessions:Store` (`Postgres|InMemory`) defaults to `InMemory` in `appsettings.json` for Development only; Production refuses to start unless it is `Postgres`, and the startup error names `Sessions:Store`. `Sessions:Postgres:ConnectionString` (Npgsql; production uses `Ssl Mode=Require;Trust Server Certificate=true` because the template uses a self-signed certificate on the private network). `Coordination:Store` (`Dragonfly|InMemory`) defaults to `InMemory` in `appsettings.json`; production must set it explicitly, and a warning is logged in Production when it is `InMemory` (event 32). `Coordination:Dragonfly:ConnectionString` (StackExchange.Redis format; `abortConnect=false` is required). `Otel:Exporter` (`none|console|otlp`). OTLP endpoint, protocol, and headers come only from `OTEL_EXPORTER_OTLP_*`. The Authorization header is set only in the hosting environment and is never committed. The GreptimeDB pipeline header goes in `OTEL_EXPORTER_OTLP_HEADERS`, never in `OTEL_EXPORTER_OTLP_TRACES_HEADERS` (smoke evidence finding 1: [evidence](runbooks/evidence/smoke-backend-local-2026-10-21.md)).
6. **Run ownership.** Admission acquires the run lease (TTL 30 s, renewed every 10 s). Admission is synchronous: a conflict returns 409 before any 202. A reaper every 15 s settles a Running session older than 60 s whose lease is absent, with detail `run owner lost`. A lost lease settles the run with detail `run lease lost`. Terminal failure detail is persisted in the terminal event text.
7. **Idempotency.** Optional `Idempotency-Key` on prompt submission. Completed records last 24 h; a pending claim lasts 60 s. Replay returns the stored 202 body. A different body under the same key returns 409 `/problems/idempotency-key-reused`. A concurrent claim returns 409 `/problems/idempotency-in-progress`.
8. **Signals.** Published after commit and best-effort: failures are logged and swallowed. Polling (about 200 ms) remains the source of truth for SSE.
9. **Hosted runtime limits.** The hosted API is configured for the ACP harness (`Harness:Adapter=Acp`), and Production refuses the in-memory adapter at startup. No deployed API image contains omp (update 2026-10-28: the staged worker image on Railway does contain omp 18.12.5), no sandbox provider is wired to the hosted path, and no model credential is deployed. HOST-04 stays open.
10. **Vercel.** Hosts the Blazor WebAssembly web head unchanged. The CSP mechanism is decided and implemented (Q-VERCEL-CSP, `vercel.ts`); the deployed check waits for the production origin.

### Supersessions

| Item | Superseded by | Where recorded |
| --- | --- | --- |
| OD-02 routed worker composition | Worker removed. The session log is shared by the API only. | Q-SB-WORKER-COMPOSE; `.planning` (orchestrator) |
| HO-04 replay evidence | Temporal history fixture retired. The evidence file is kept and marked superseded. | `runbooks/evidence/ho-04-replay-revalidation-2026-10-21.md` |
| Q-SB-WORKER-COMPOSE | Resolved by removing the worker. | open-questions.md |
| Q-WF-RETRY | Retry lives in `DirectSessionRunner` and the run lease only. | open-questions.md |
| HOST-03 (Temporal Cloud requirement) | Railway services in decision 2. | `.planning` (orchestrator) |
| ADR-0003 workflow routing | Temporal removed; `DirectSessionRunner` only. | decisions/ADR-0003 |
| OQ-11, OQ-15, OQ-16, OQ-22 | Temporal removed. | open-questions.md |

### Open items

Owner decisions with a remaining blocker:

- **OD-03**: sign-in method. Decided autonomously 2026-10-21: OIDC bearer validation, fail-closed (`open-questions.md`, OD-03). Public exposure stays blocked until an identity provider is configured.

Decided, not open: worker placement is none. The Vercel CSP mechanism (Q-VERCEL-CSP) is decided and implemented. The approval adapter is ACP (Q-APPROVAL-ADAPTER). The plain Postgres service is kept; the owner may retire it (Q-PG-RETIRE).

New open questions, with owners in [open-questions.md](open-questions.md): Q-HOST-PRIVNET, Q-HOST-DFLY-PIN, Q-HOST-GREPTIME-AUTH, Q-DATA-TSDB-VERSION, Q-LOCAL-PARITY, Q-HOST-RUNTIME (HOST-04).
