# OmpOS v0.9.1 handoff — implemented / stubbed / deferred

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

**Q-AC-LIST status (2026-10-21): open.** Partly recovered: tagged excerpts for AC_1..AC_6 and AC_8. Unrecoverable: the full AC_1..AC_11 statements, and any reference to AC_7, AC_9, AC_10 and AC_11.

## Bootstrap gate evidence

Full gate `./bootstrap.sh` (SDK install, locked restore, warnings-as-errors
Release build, tests, format verification). Log: `docs/runbooks/evidence/bootstrap-gate-2026-10-21-f77221a.log`:

- Line 81: `Test run summary: Passed!`
- Line 98: `total: 707` · Line 99: `failed: 0`
- Line 103: `bootstrap: OK (SDK 10.0.402, locked restore, Release build, tests, format)`
- Scope: everything in `OmpOS.Linux.slnf` (libraries, API, worker, web head, all
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

| AC | Criterion (reconstructed) | Status | Evidence | Re-check (2026-10-21) |
| --- | --- | --- | --- | --- |
| AC_1 | Monorepo scaffold boots green from one command (`bootstrap.sh`) with pinned SDK, locked restore, warnings-as-errors | Implemented | `docs/runbooks/evidence/bootstrap-gate-2026-10-21-f77221a.log` (exit path OK, 707/0); `OmpOS.Linux.slnf`, `global.json`, `Directory.Build.props`, `Directory.Packages.props` | **Holds.** Gate log `:81`, `:98`, `:99`, `:101`, `:103` (passed; 707 total; 0 failed; 0 skipped; `bootstrap: OK (SDK 10.0.402, locked restore, Release build, tests, format)`); clean-clone log `:139`–`:145`; `Directory.Build.props:7` (`TreatWarningsAsErrors`) and `:16` (`RestorePackagesWithLockFile`); `global.json` pins 10.0.402. The plan's name `OmpOS.Core.slnf` does not exist; see the plan table. |
| AC_2 | Every prompt string yields exactly one `IntentEnvelope`; catch-all fallback is unremovable; omp keyword rules honoured (PARSE-01..04) | Implemented | `PromptParserTests`, `MagicKeywordBoundaryTests`, `RecognizerOrderingTests` (`tests/OmpOS.Parser.Tests/`) | **Holds** for this text: `RecognizerOrderingTests.FallbackRunsLastEvenWhenRegisteredWithOrderZero` (`:64`) and `FallbackRegistrationCanBeRemovedAndParserStillTerminates` (`:45`) prove the unremovable fallback. **Numbering conflict:** the plan's AC_2 is the architecture test (plan table). |
| AC_3 | Typed harness with explicit state machine; scripted in-memory adapter drives UI work without omp; six failure modes covered (HARN-01..03, HARN-06) | Implemented (remote transport stubbed) | `HarnessStateMachineTests` (`tests/OmpOS.Harness.Abstractions.Tests/`), `InMemoryAdapterTests`, `FailureModeTests` (`tests/OmpOS.Harness.InMemory.Tests/`); remote WebSocket transport is a bounded stub — `WebSocketOmpTransportStubTests`, open question **Q-HARN-WS** | **Holds**, with one **unverified** sub-claim: the plan's "adapter script covers all UiBlock kinds" has no test (no `UiBlockKind` reference in tests at f77221a or HEAD). `FailureModeTests` states six scripted modes; `WebSocketOmpTransportStubTests` covers Q-HARN-WS. Path corrected for `HarnessStateMachineTests`. |
| AC_4 | Real ACP v1 and omp RPC JSONL clients pass replay + live smoke against installed omp, no model spend (HARN-04, HARN-05) | Implemented (interactive approval deferred) | `AcpFrameReplayTests`, `AcpStdioTransportTests`, `AcpAgentSessionTests` (`tests/OmpOS.Harness.Acp.Tests/`, incl. authenticate + deny-by-default `fs/read_text_file` handlers); `RpcFrameReplayTests`, `RpcChunkReassemblyTests`, `RpcCommandCorrelationTests`, `RpcPromptCompletionTests`, `RpcLiveSmokeTests`; approval answers deny-by-default — interactive resolution deferred, **Q-HARN-PERMISSION** | **Holds at count level, with one unverified sub-detail.** The live smokes did not skip: gate `:101` and clean-clone `:142` read `skipped: 0`. **Unverified:** no record names the omp binary or version used for the live run. The live smokes send no prompt (`AcpLiveSmokeTests` `:7`; `RpcLiveSmokeTests` `:9`–`:10`). `authenticate` is checked by `AcpAgentSessionTests.AuthMethodsWithConfiguredIdSendsOneAuthenticateBeforeSessionNew` (`:222`); deny-by-default by `AcpApprovalPolicyTests`. |
| AC_5 | Append-only sequence-numbered session log; one-active-prompt enforcement; replay-equal projection (SESS-01..03) | Implemented | `SessionServiceTests`, `ProjectionReplayTests`, `SessionRepositoryContractTests` (`tests/OmpOS.Sessions.Tests/`) | **Holds.** `SessionServiceTests.SecondPromptWhileRunningIsRejectedWithPromptInFlight` (`:53`) and `ConcurrentSubmissionsAdmitExactlyOnePrompt` (`:88`); `ProjectionReplayTests.ReplayingSerializedLogReproducesIdenticalUiState`; `SessionRepositoryContractTests` runs through `InMemoryRepositoryFixture` (`:4`). |
| AC_6 | Secured observable API: validated endpoints, per-session rate limit, locked CORS, committed OpenAPI 3.1, SSE resume without gaps/duplicates, health, fail-fast config, correlation, redaction, auth boundary (API-01..03, OPS-01..04) | Implemented (auth is dev-only — deploy blocker) | `SessionEndpointTests`, `SseResumeTests`, `GuardTests`, `ParserPromptTests`, `SecurityMitigationsTests`, `OperationsTelemetryTests`, `AuthBoundaryTests` (`tests/OmpOS.Api.IntegrationTests/`); `docs/api/openapi.json` (committed contract). Auth is a no-op single-dev-principal boundary (OQ-26) — see deploy blockers | **Holds** at f77221a; each named capability has a test: validation `SessionEndpointTests` (`:130`, `:144`) and `SecurityMitigationsTests.ProblemDetails_BadRequest_HidesInternals` (`:98`); per-session limit `SessionEndpointTests` (`:186`–`:197`, 429); global limits `ReviewFindingsTests` (`:44`–`:72`); locked CORS `SecurityMitigationsTests.Cors_DefaultDeny_BlocksSimpleAndPreflight` (`:21`) and `SessionEndpointTests.Cors_DisallowedOriginGetsNoHeaders` (`:206`); committed OpenAPI `GuardTests` (`:93`, served document equals `docs/api/openapi.json`); SSE resume `SseResumeTests` (`:18`); health `GuardTests` (`:46`, `:59`); correlation `SessionEndpointTests.CorrelationId_RoundTrips` (`:225`). **Drift:** v0.9.2 commits changed `SessionEndpointTests` and `SecurityMitigationsTests` after `cbbe22d`; the gate at HEAD confirms. |
| AC_7 | Sandbox provider abstraction: fake + local podman-compose providers under contract tests; explicit isolation defaults (SBOX-01..03, SBOX-05) | Implemented (per-sandbox credential issuance deferred) | `FakeSandboxProviderContractTests`, `LocalPodmanComposeProviderContractTests`, `SandboxLifecycleTests`, `IsolationDefaultsTests` (`tests/OmpOS.Sandbox.Tests/`); `deploy/compose.yaml`; per-sandbox credential issuance deferred — **Q-SB-CREDENTIALS** | **Holds** at f77221a: the four test classes (`:10`, `:12`, `:14`, `:17`), `deploy/compose.yaml`, and the Q-SB-CREDENTIALS row. **Drift:** v0.9.2 commits changed `IsolationDefaultsTests`, `LocalPodmanComposeProviderContractTests` and `LocalPodmanComposeProvider.cs` after `cbbe22d`; the gate at HEAD confirms. |
| AC_8 | Vendor sandbox stubs (OpenShell, agentOS, Railway, dstack-shaped enclave) return typed NotAvailable/Unverified with owner-tagged open questions; no invented vendor APIs (SBOX-04) | Stubbed by design | `VendorStubTests` (`tests/OmpOS.Sandbox.Tests/`): all four stubs throw `NotAvailable` for every operation; the enclave attestation verdict is `Unverified`; **Q-SB-AGENTOS**, **Q-SB-RAILWAY**, **Q-SB-ENCLAVE** | **Holds** at f77221a and committed HEAD (`OpenShellProvider.cs`, `AgentOsProvider.cs` and `RailwaySwarmProvider.cs` are unchanged since `cbbe22d`): `VendorStubTests.OpenShellProvider_AllOperations_ThrowNotAvailable` (`:29`), `AgentOsProvider_AllOperations_ThrowNotAvailable` (`:78`), `RailwaySwarmProvider_AllOperations_ThrowNotAvailable` (`:103`), `AttestedEnclaveProvider_AllOperations_ThrowNotAvailable` (`:140`), `AttestedEnclaveProvider_GetQuoteAsync_ReturnsUnverified` (`:129`). **Drift (uncommitted only):** the working tree implements `AgentOsProvider` over HTTP (+359 lines vs f77221a) and changes `RailwaySwarmProvider` (+157) and `OpenShellProvider` (+21); "stubbed by design" does not hold there. |
| AC_9 | Durable session-run workflow behind `Workflows:Enabled` with deterministic replay test; direct runner is the default path (WFLW-01..03) | Implemented (worker composition + direct-path retry deferred) | With `Workflows:Enabled=false` the API prompt path executes through `DirectSessionRunner` provisioned via `ISandboxProvider` (`FakeSandboxProvider` by default) with teardown in `finally` (`src/OmpOS.Workflows/DirectSessionRunner.cs:65`); DI registrations in `services/OmpOS.Api/Program.cs:67-69` (`services/OmpOS.Api/Workflows/ApiSessionRunActivities.cs`); `DirectRunnerTests`, `TemporalReplayTests`, `WorkerHostTests` (`tests/OmpOS.Workflows.Tests/`); `SessionRunWorkflowTests` (`tests/OmpOS.Api.IntegrationTests/`). The Temporal worker is **not** wired into the API — **Q-SB-WORKER-COMPOSE** stands; no retry on the direct path — **Q-WF-RETRY** | **Holds** at f77221a: `DirectSessionRunner.cs:65` (`finally`) and `:69` (`TryTeardownAsync`); `Program.cs:67`–`:69`; `DirectRunnerTests` (`:6`), `TemporalReplayTests` (`:22`), `WorkerHostTests` (`:22`); `SessionRunWorkflowTests` (`:22`) is in `tests/OmpOS.Api.IntegrationTests/` (path corrected here). **Drift (uncommitted only):** the working tree adds retry wrappers to `DirectSessionRunner.cs` and edits `Program.cs`; "no retry on the direct path" does not hold there. |
| AC_10 | Generative UI: all UiBlock variants + unknown-fallback render as a pure fold over the event log; Blazor WASM web head deploys statically to Vercel and completes the vertical slice in a browser; MAUI head shares the RCL — desktop in-process, mobile remote-only (UI-01..04, WEB-01..02, DESK-01, MOB-01, SHELL-01) | Implemented for RCL + web head; Deferred for MAUI binaries | `UiBlockRendererTests`, `ReplayTreeTests`, `StreamingStateTests`, `AccessibilityTests`, `SkipLinkIsFirstFocusableAndTargetsConversation` (`tests/OmpOS.UI.Tests/`); `NewConversationButtonTests` (`tests/OmpOS.Web.Tests/`); `SseParserTests`, `SseEventStreamTests`, `ApiBaseUrlResolverTests` (apiBaseUrl rejection), `VercelConfigTests` (CSP `connect-src` property), `PublishSmokeTests` (`tests/OmpOS.Web.Tests/`); `EngineHostTests` (`tests/OmpOS.EngineHost.Tests/`). Web head browser-verified: `docs/runbooks/local-dev.md` (Vertical slice section) + `docs/runbooks/evidence/web-vertical-slice-smoke.webp`; axe-core reports zero violations (same runbook section). MAUI binaries deferred — macOS human build outstanding | **Holds** at f77221a: `UiBlockRendererTests` (`:11`), `ReplayTreeTests` (`:16`), `StreamingStateTests` (`:15`), `AccessibilityTests` (`:11`) and `SkipLinkIsFirstFocusableAndTargetsConversation` (`:93`); `NewConversationButtonTests` (`:12`) is in `tests/OmpOS.Web.Tests/` (path corrected here); `vercel.json:31` (`connect-src 'self' http://127.0.0.1:5187`); the runbook "Accessibility run (UI-03)" (`docs/runbooks/local-dev.md:94`) records 0 violations in both states. MAUI binaries are not built (decision 5). |
| AC_11 | Milestone governance: architecture/security/format/vulnerability gates green, docs (architecture, ADRs, runbooks, handoff) reflect the final tree with implemented/stubbed/deferred fidelity (VERIFY-01..03) | Implemented (TODO-linkage test: `RepoHygieneTests.TodoMarkersLinkToOpenQuestion`) | Bootstrap gate in `docs/runbooks/evidence/bootstrap-gate-2026-10-21-f77221a.log` (707/0); clean-clone run in `docs/runbooks/local-dev.md` (section Clean-clone bootstrap) and `docs/runbooks/evidence/clean-clone-bootstrap-2026-10-21-f77221a.log`; `ArchUnitLayerTests`, `ProjectGraphTests`, `RepoHygieneTests` (`tests/OmpOS.ArchitectureTests/`); `docs/architecture.md`, `docs/decisions/` (ADR-0001..0003 + index), `docs/runbooks/`, this file; VERIFY-02 sweep recorded in `.planning/milestones/v0.9.1-phases/05-verify/05-01-SUMMARY.md` | **Holds** at f77221a: the gate and clean-clone records (row AC_1); `local-dev.md:106` "Clean-clone bootstrap (VERIFY-01)" lists the f77221a re-run (707 total, 0 failed; added by `cbbe22d`); `ArchUnitLayerTests` (`:16`), `ProjectGraphTests` (`:10`), `RepoHygieneTests.TodoMarkersLinkToOpenQuestion` (`:155` at f77221a, `:115` at HEAD); ADR-0001..0003 and `docs/decisions/README.md`; `05-01-SUMMARY.md` present at f77221a and HEAD. **Drift:** v0.9.2 commits changed `RepoHygieneTests.cs` and added `TodoMarkerScanner.cs` after `cbbe22d`; the gate at HEAD confirms. |

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
| 1 | WFLW-01 is wired: with `Workflows:Enabled=false` the API prompt path executes through `DirectSessionRunner`, provisioning via `ISandboxProvider` (`FakeSandboxProvider` by default) and tearing down in `finally`; the Temporal worker is not wired into the API | **Q-SB-WORKER-COMPOSE** stands; AC_9 reads Implemented with that deferral |
| 2 | `SessionService.RequestToolApprovalAsync` and `ResolveToolApprovalAsync` are `internal` (`InternalsVisibleTo` `Sessions.Tests` only); interactive approval stays deferred | **Q-HARN-PERMISSION** stands; AC_4 reads Implemented with that deferral |
| 3 | The markdown `MarkupString` sink is accepted: its only input is `MarkdownRenderer` output, produced with raw HTML disabled and a link/image scheme allowlist, under a CSP without inline script | No finding; recorded here as an accepted sink |
| 4 | The API has no authentication — a single local dev principal (OQ-26); it must not be exposed publicly until an auth provider exists | Deploy blocker (see below) |
| 5 | The macOS MAUI build is partly verified: the CI `shells` job builds the Android target on a hosted macOS runner; iOS and MacCatalyst need Xcode 27.1, which hosted runners do not provide as a stable toolchain | DESK-01 / MOB-01 / SHELL-01 stay Partial; AC_10 stays split |
| 6 | Remote repository: `https://github.com/swcstudiospace/OmpOS` (currently empty and public; local `git remote -v` is unconfigured) | origin is `https://github.com/swcstudiospace/OmpOS.git`; `main` and `feat/ompos-scaffold` were pushed on 2026-10-21; PR #1 is open |
| 7 | Tech debt from the milestone audit (43 items) is accepted and tracked in the backlog, not fixed in v0.9.1 | Recorded in the archived audit (`.planning/milestones/v0.9.1-MILESTONE-AUDIT.md`) |
| 8 | The human-only items are carried as tracked follow-ups: podman-compose validation, podman smoke test, live Temporal replay check, TCP readiness sign-off (4c), iOS and MacCatalyst builds (Xcode 27.1), live Vercel deploy, screen-reader pass, clean-clone re-record at a later HEAD | Each has its file reference in the audit; none blocks the archive |
| 9 | The API host runs only the in-memory harness for v0.9.1 (`services/OmpOS.Api/Program.cs`); wiring the real ACP harness is the first item of the next milestone | Acknowledged scope limit for the vertical slice |
| 10 | Ship: push `main` and `feat/ompos-scaffold` to `https://github.com/swcstudiospace/OmpOS` and open a PR into `main`. The PR is not merged by the orchestrator, but the user's later ship instruction supersedes this: the PR merges through ultrathink-ship only after a completed Greptile review scores 5 with no open threads and CI passes | Superseded by the user's ship instruction on 2026-10-21; this row is kept as history |

## Known deferrals

All open-question IDs below were confirmed present in `docs/open-questions.md` on 2026-10-21:

- **Interactive approval** — no UI-to-harness resolution path (**Q-HARN-PERMISSION**).
- **Remote WebSocket transport** — faulting stub; SSE remains the client transport (**Q-HARN-WS**).
- **Unseen `session/update` variants** — fixtures frozen from recorded/scripted turns; unknown-fallback path until verified against a live omp (**Q-HARN-UPDATE**).
- **Sandbox vendor stubs** — OpenShell, agentOS, Railway Swarm, attested enclave (**Q-SB-AGENTOS**, **Q-SB-RAILWAY**, **Q-SB-ENCLAVE**).
- **Per-sandbox credential issuance** — duplicate-target rejection only (**Q-SB-CREDENTIALS**).
- **Real provider/activity composition into the Temporal worker** — opt-in fake activities only (**Q-SB-WORKER-COMPOSE**).
- **Direct-path retry policy** — fail fast on the direct path; retries live on the Temporal path (**Q-WF-RETRY**).
- **Blazor reflection switch override** for the web/MAUI heads (**Q-WEB-REFLECTION**).
- **Authoritative AC enumeration** — verifying against the reconstructed list until the original is retrieved (**Q-AC-LIST**).
- **iOS and MacCatalyst builds (human)** — need Xcode 27.1; the macOS `shells` job builds only the Android target, and the other targets are unverified.
- **Live Vercel deploy** — needs a production API origin, which must also be added to `connect-src` (`vercel.json:31` currently allows only `'self'` + loopback).
- **Auth provider** — deploy blocker (decision 4 above).
- **Accepted MarkupString sink** — decision 3 above; not a deferral, recorded for audit continuity.
- **Remote repository** — decision 6 above; first push and remote CI run outstanding.

## Accessibility evidence (UI-03)

axe-core 4.14.0, WCAG 2.0 and 2.1 levels A and AA, run 2026-10-21 against the
published Release head in an owned headless Chrome (`docs/runbooks/local-dev.md`,
Accessibility run section):

- Empty state and an active conversation with a streamed reply: **0 violations** in both states.
- First-run findings, fixed: missing skip link and the New-button accessible-name mismatch (`AccessibilityTests.SkipLinkIsFirstFocusableAndTargetsConversation`, `NewConversationButtonTests`).
- Remaining `incomplete` items, both on the icon-only send control: axe cannot evaluate a pictogram's label or colour automatically. Measured live: foreground `rgb(0, 0, 0)` on `rgb(239, 239, 239)`, contrast **18.26:1** (WCAG 1.4.11 needs 3:1 for UI components; text needs 4.5:1).
- One unexplained screenshot (skip link visible immediately after the full axe run; hidden in every probe before, during stream/send, and in a later same-state screenshot) — cause not isolated, recorded in the runbook.
- Not verified here: the MAUI head's runtime behaviour (its Android target compiles in CI; nothing runs on a device) and assistive-technology testing with a screen reader.

## Before any public deployment (human verification)

1. **Auth provider (blocker).** The API accepts unauthenticated localhost callers as the single dev principal (`AuthBoundaryTests`, `OmpOS.Api.IntegrationTests`). Do not expose it publicly until a real auth provider fronts it. Verify: unauthenticated cross-origin requests are denied by the locked CORS default, and every session carries the expected owner once auth lands.
2. **Production API origin in `connect-src`.** `vercel.json:31` allows only `'self'` + loopback. Before deploying the static head, add the production API origin to `connect-src` (and point the head at it via `Api:BaseUrl` / `?apiBaseUrl=`); otherwise the browser blocks every API call. Verify: load the deployed head, send a prompt, observe the streamed reply with no CSP violations in the console.
3. **iOS and MacCatalyst builds (human).** With Xcode 27.1 on a macOS host, build `apps/OmpOS.App` for `net10.0-ios` and `net10.0-maccatalyst` with warnings as errors (the `shells` job builds only `net10.0-android`), then spot-check desktop against the remote backend and mobile remote-only resume. Verify: warning-free build; desktop streams; mobile never spawns a process.
4. **Live Vercel deploy.** Deploy the static output once the origin above exists and re-run the runbook browser smoke against it. Verify: same observations as `docs/runbooks/local-dev.md` (strong 1, img 0, no script, no error bar).
5. **Clean-clone bootstrap record (VERIFY-01).** Fresh `git clone` into a temp dir → `./bootstrap.sh` → record date, host, SDK version, duration, result in `docs/runbooks/local-dev.md`.
