---
phase: 03-platform
verified: 2026-10-21T00:00:00Z
status: gaps_found
score: 8/8 requirements verified, 3/4 criteria fully met (1 partial)
suite_counts:
  sandbox_test_cases: 70
  workflows_test_cases: 20
  evidence: "/tmp/bootstrap-final3.log:63-64,70-71 (both suites passed, 527 total, 0 failed)"
---

# Phase 3 (Platform) Verification Report

**Phase goal** (`.planning/ROADMAP.md:59-61`): a session run can be placed inside an
operator-controlled isolation boundary and executed durably — fake + local podman-compose
providers work for real, every vendor boundary is an honest interface-conforming stub, and the
provision → run → collect → teardown workflow runs in-process by default or on a Temporal
worker behind a flag.

**Method:** read-only static verification (`grep`/`read` only; no builds, no test runs — those are
forbidden to this verifier). Every status below cites a file path with a line number and a test
class/method name confirmed to exist by `grep`. Suite-green evidence is quoted from the
pre-existing gate log `/tmp/bootstrap-final3.log` (Release build, 527 tests, 0 failed), not from
a run performed here.

**Test-count arithmetic (confirmed by `grep`, not by execution):**

- `tests/OmpOS.Sandbox.Tests`: 64 `[Fact]`/`[Theory]` attributes across 14 files, zero
  `[Theory]` data rows. 6 of the 64 sit on the abstract
  `SandboxProviderContractTests<TProvider>` (not executed directly) and run once per concrete
  subclass (`FakeSandboxProviderContractTests`, `LocalPodmanComposeProviderContractTests`):
  64 − 6 + 12 = **70 test cases**.
- `tests/OmpOS.Workflows.Tests`: 19 attributes — 6 on abstract
  `SessionRunWorkflowContractTests<T>` (run once via `DirectRunnerTests`), 2
  `TemporalReplayTests`, 3 `TemporalTeardownGuardTests`, 7 `[Fact]` + 1 `[Theory]` with 2
  `[InlineData]` rows in `WorkerHostTests` (`WorkerHostTests.cs:143-146`) — i.e.
  6 + 2 + 3 + 8 + 1 = **20 test cases**.

## Requirement table

| ID | Status | Implementation (file:line) | Proving test (class.method, file:line) |
|----|--------|----------------------------|----------------------------------------|
| SBOX-01 | VERIFIED | `src/OmpOS.Sandbox.Abstractions/ISandboxProvider.cs:22,30,40,50,59,67` (provision/attach/exec/stream-logs/snapshot/teardown); `src/OmpOS.Sandbox.Abstractions/Models/ProvisionRequest.cs:13` (`required IdempotencyKey`), `:23` (`required Timeout`) | `SandboxProviderContractTests.Timeout_IsHonoured` (`tests/OmpOS.Sandbox.Tests/SandboxProviderContractTests.cs:136`, 1 ms timeout, accepts `ProvisionTimeoutException`); `.IdempotencyKey_DedupesConcurrentProvisions` (`:74`); `.StatusTransitions_CoverEnum` (`:116`); `.Provision_Attach_Exec_Logs_Teardown_HappyPath` (`:26`); `.FailedProvision_NeverLeavesSessionRunning` (`:164`). Executed ×2 via `FakeSandboxProviderContractTests.cs:10` and `LocalPodmanComposeProviderContractTests.cs:12`. |
| SBOX-02 | VERIFIED | `src/OmpOS.Sandbox/SandboxLifecycle.cs:232` (`ReapOnceAsync`), `:241,257` (expiry/TTL + `IsOrphanedAsync`), `:199-217` (teardown retry loop over `TeardownRetryCount` with `ComputeJitteredDelay`, `:284`) | `SandboxLifecycleTests.TtlExpiry_TriggersReaper` (`tests/OmpOS.Sandbox.Tests/SandboxLifecycleTests.cs:17`); `.OrphanReaper_SweepsOrphanedSandboxes` (`:54`); `.Teardown_RetriedWithJitteredBackoff` (`:92`); `.FailedProvision_NeverLeavesSessionRunning` (`:134`); `.Provision_FailsFast_WhenSessionNotRunning` (`:164`); `.Teardown_Idempotent_NotFoundIsSuccess` (`:185`). |
| SBOX-03 | VERIFIED | `src/OmpOS.Sandbox/FakeSandboxProvider.cs` (in-memory); `src/OmpOS.Sandbox/LocalPodmanComposeProvider.cs:52` (`podman-compose` argv asserted), injectable `IProcessRunner` (`LocalPodmanComposeProviderContractTests.cs:17-31` wires `FakeProcessRunner`) | Contract ×2 (see SBOX-01) plus `LocalPodmanComposeProviderContractTests.Provision_GeneratesComposeFileAndRunsUp` (`:40`), `.Exec_RunsPodmanExec` (`:64`), `.Teardown_RunsPodmanComposeDown` (`:102`), and `RealPodmanComposeConfig_SmokeTest` (`:136-137`: `Assert.SkipUnless` podman AND podman-compose exist — real `podman-compose --profile workflows config` invocation skipped when podman absent, config-only, never starts containers). |
| SBOX-04 | VERIFIED | `src/OmpOS.Sandbox/OpenShellProvider.cs:13` (+`:44-68` all ops `throw NotAvailableException`; `:96,124` `GenerateGatewayToml`/`GeneratePolicyYaml` from typed `OpenShellPolicy`); `AgentOsProvider.cs:10` (`:35-47` throw); `RailwaySwarmProvider.cs:10` (throws); `AttestedEnclaveProvider.cs:15` (delegates, `:156-163` `GetQuoteAsync` → boundary verdict `Unverified`, `AttestationVerdict.cs:9`) | `VendorStubTests.OpenShellProvider_AllOperations_ThrowNotAvailable` (`tests/OmpOS.Sandbox.Tests/VendorStubTests.cs:30`); `.OpenShellProvider_Capabilities_AreHonest` (`:17`); `.OpenShellProvider_CheckPrerequisites_ReturnsFailureForPodman493` (`:46`); `.AgentOsProvider_AllOperations_ThrowNotAvailable` (`:79`); `.RailwaySwarmProvider_AllOperations_ThrowNotAvailable` (`:104`); `.AttestedEnclaveProvider_GetQuoteAsync_ReturnsUnverified` (`:131`); `.AttestedEnclaveProvider_WrapsInnerProvider` (`:157`); golden-file `GoldenPolicyTests.OpenShellProvider_GatewayToml_MatchesGolden` (`:24`) + `.OpenShellProvider_PolicyYaml_MatchesGolden` (`:35`). Open questions filed: OQ-24 (vendor APIs must not be invented, decided), OQ-06 (OpenShell gateway proto), OQ-10 (Podman 5.x), Q-SB-AGENTOS / Q-SB-RAILWAY / Q-SB-ENCLAVE (`docs/open-questions.md:35-37`). No vendor API invented — stubs return typed `NotAvailable`/`Unverified`. |
| SBOX-05 | VERIFIED | `src/OmpOS.Sandbox.Abstractions/SandboxProviderOptions.cs:11` (`AllowHostMounts`, default false), `:16` (`AllowHostSecrets`), `:21` (`PerSandboxCredentials=true`), `:26` (`OutboundNetworkDefaultDeny=true`); enforcement in `src/OmpOS.Sandbox/LocalPodmanComposeProvider.cs:423,429` (host-mount denial), `:434,436` (secret denial), `:439` (duplicate-target rejection), `:452` (default-deny), `:520,545` (network none/bridge) | `IsolationDefaultsTests.PodmanProvider_BindMountWithAbsoluteSource_RejectedAsHostMount` (`tests/OmpOS.Sandbox.Tests/IsolationDefaultsTests.cs:125`); `.PodmanProvider_VolumeMountWithAbsoluteSource_RejectedAsHostMount` (`:144`); `.PodmanProvider_TmpfsMountWithAbsoluteSource_RejectedAsHostMount` (`:163`); `.PodmanProvider_HostMountRejected_WhenAllowHostMountsFalse` (`:82`); `.PodmanProvider_ProvisionedDescriptor_NoHostPathsOrSecrets` (`:42`); `.FakeProvider_ProvisionedDescriptor_NoHostPathsOrSecrets` (`:20`); `.NetworkPolicy_DefaultDeny_Enforced` (`:237`); `.PodmanProvider_NamedVolumeWithoutPathCharacters_AllowedWhenHostMountsDenied` (`:182`); plus secret-redaction `.FakeProvider_Logs_NoSecretValues` (`:268`), `.PodmanProvider_Exec_NoSecretValuesInOutput` (`:293`) and `ProvisionCleanupRegressionTests.SecretRefs_RejectedWhenHostSecretsDisallowed` (`:157`), `.DuplicateSecretTarget_RejectedWhenPerSandboxCredentials` (`:177`), `.FilterInjectionSessionId_Rejected` (`:141`), `.TypedUpFailure_/` `.GenericRunnerFailure_/` `.EmptyLookupResult_DeletesComposeFileAndAttemptsDown` (`:58,82,105`), `.MultiLineLookupResult_FailsClosed` (`:126`); output-cap `ProcessRunnerOutputCapTests.LargeOutput_IsCappedAtOneMiBPerStreamAndMarkedTruncated` (`tests/OmpOS.Sandbox.Tests/ProcessRunnerOutputCapTests.cs:26`) + `.SmallOutput_PassesThroughUntruncated` (`:49`); drain `ProcessRunnerDrainTests.GrandchildHoldingPipe_DrainIsBounded` (`:27`). |
| WFLW-01 | VERIFIED | `src/OmpOS.Workflows/ISessionRunWorkflow.cs` + `ISessionRunActivities.cs` (single contract); `src/OmpOS.Workflows/DirectSessionRunner.cs:65` (teardown in `finally`; doc `:10` cites `Workflows:Enabled=false` default) | `DirectRunnerTests` (`tests/OmpOS.Workflows.Tests/DirectRunnerTests.cs:6`) inherits all 6 `SessionRunWorkflowContractTests` facts: `.HappyPath_ProvisionsRunsCollectsTearsDownInOrder` (`SessionRunWorkflowContractTests.cs:53`), `.PromptFailure_StillTearsDown` (`:101`), `.TeardownFailure_RecordedAsFailedStatus` (`:147`), `.ProvisionFailure_SessionNotRunning_NoTeardown` (`:177`), `.HeartbeatEvents_ObservedDuringPrompt` (`:219`), `.Cancellation_ResultsInCancelledStatusWithTeardown` (`:250`) — all execute against `DirectRunnerFixture` with no Temporal server. |
| WFLW-02 | VERIFIED with one partial (see Gaps) | `services/OmpOS.Workflows.Worker/Program.cs:22-25` (exits 0 before any Temporal registration when disabled); `TemporalSessionRunWorkflow.cs:81` (teardown in `finally`), `:150-179` (`RetryPolicy` per activity: provision/collect/teardown retry, prompt `MaximumAttempts = 1`), `TemporalSessionRunActivities.cs:52,78-82` (`ReportHeartbeat` → ambient `ActivityExecutionContext`); routing criteria in `docs/decisions/ADR-0003-workflow-routing.md` Decision section (Temporal ONLY for restart-survival/durable retries; everything else direct) | `WorkerHostTests.WorkerMain_Disabled_ReturnsZero` (`tests/OmpOS.Workflows.Tests/WorkerHostTests.cs:28`); `.WorkerHost_Disabled_ConfigureServicesRefusesRegistration` (`:55`); `.WorkerHost_Enabled_RegistersTemporalServices` (`:72`); `.WorkerOptions_Validation_FailsWhenEnabledWithoutAddress` (`:146`, 2 data rows); `.WorkerHost_Enabled_RegistersFailLoudGuardByDefault` (`:161`); `TemporalReplayTests.SessionRunWorkflow_ReplaysDeterministically` (`TemporalReplayTests.cs:30`) + `.HistoryFixture_IsWellFormed` (`:48`); `TemporalTeardownGuardTests.TeardownFailure_AfterCompleted_RecordedAsFailed` (`:19`), `.AfterFailed_PreservesOriginalFailure` (`:33`), `.AfterCancelled_PreservesCancelled` (`:46`). |
| WFLW-03 | VERIFIED | `tests/OmpOS.Workflows.Tests/SessionRunWorkflowContractTests.cs:19` (abstract `SessionRunWorkflowContractTests<TFixture>`, reusable — Temporal impl can bind the same fixture) | Same 6 facts as WFLW-01 run through `DirectRunnerFixture`; abstraction is proven reusable by construction (generic over `ISessionRunWorkflowFixture`, no direct-runner types in the contract file). |

## Criteria table (roadmap Phase 3 success criteria)

| # | Criterion | Status | Proven by |
|---|-----------|--------|-----------|
| 1 | Fake + local podman-compose providers pass `SandboxProviderContractTests<T>` (provision/attach/exec/stream-logs/snapshot/teardown, timeouts, idempotency keys, every `SandboxStatus`); real invocation skipped when podman absent | VERIFIED | Unit tests only: contract fixtures ×2 providers + `RealPodmanComposeConfig_SmokeTest` skip gate (`LocalPodmanComposeProviderContractTests.cs:136-139`). No live podman run in this environment. |
| 2 | `SandboxLifecycle` TTL/reaper/jittered-retry/no-Running-on-failure; isolation defaults explicit documented settings | VERIFIED | Unit tests only: `SandboxLifecycleTests` (6 facts) + `IsolationDefaultsTests` (11 facts) + `ProvisionCleanupRegressionTests` (7 facts); settings explicit in `SandboxProviderOptions.cs:11-26`. |
| 3 | Four vendor stubs compile against `ISandboxProvider`, return typed `NotAvailable`/`Unverified`, owner-tagged open questions, no invented APIs | VERIFIED | Unit tests only: `VendorStubTests` (10 facts) + `GoldenPolicyTests` (4 facts); questions OQ-06/OQ-10/OQ-24 + Q-SB-AGENTOS/Q-SB-RAILWAY/Q-SB-ENCLAVE. |
| 4a | `Workflows:Enabled=false` → direct in-process run with teardown in `finally`, passing contract tests, no Temporal server | VERIFIED | Unit tests only (`DirectRunnerTests`, 6 facts). |
| 4b | `Workflows:Enabled=true` → worker hosts deterministic `SessionRunWorkflow` with retrying/heartbeating activities; replay test passes; routing criteria documented | VERIFIED | **Real Temporal run for the fixture, unit test for the assertion:** `tests/OmpOS.Workflows.Tests/TestData/histories/session-run.json:1-43` is a genuine dev-server export — `EVENT_TYPE_WORKFLOW_EXECUTION_STARTED`, workflow type `TemporalSessionRunWorkflow`, task queue `ompos-session-runs`, workflow id `session-run-001`, `"identity": "temporal-cli:temporal@…"`. Provenance (export commands: `temporal workflow start … --workflow-id session-run-001`, wait to `COMPLETED`, `temporal workflow show --output json` saved with no hand-editing; image `docker.io/temporalio/temporal:1.9.1` per `deploy/compose.yaml:6`, run under podman) is recorded in `docs/decisions/ADR-0003-workflow-routing.md` ("Dev Server" provenance section). Note: the task brief cites `docs/decisions/ADR-0003.md`; the actual file is `docs/decisions/ADR-0003-workflow-routing.md`. `TemporalReplayTests.SessionRunWorkflow_ReplaysDeterministically` (`TemporalReplayTests.cs:30`) replays that fixture; retry/heartbeat paths are unit-covered (`TemporalSessionRunWorkflow.cs:150-179`, `TemporalSessionRunActivities.cs:78-82`, `TemporalTeardownGuardTests`). |
| 4c | `/health/ready` reflects Temporal connectivity | PARTIAL — see Gaps | `services/OmpOS.Api/Health/TemporalCheck.cs:12-40` reports Healthy/degraded from **configuration only** (`Workflows:Enabled` + endpoint non-empty); no live gRPC probe. Its own doc comment calls it a placeholder and says "Phase 3 fills the real connectivity check" — that fill did not land. |

## Gaps

1. **`/health/ready` does not probe Temporal connectivity (criterion 4c).**
   `services/OmpOS.Api/Health/TemporalCheck.cs:26-38`: disabled → Healthy; enabled with a
   non-empty endpoint → Healthy ("Temporal endpoint is configured") without opening any
   connection. A worker pointing at a dead/misaddressed Temporal server still reports ready.
   Fix: probe the `TemporalClient` (or reuse the worker's fail-loud connect path,
   `WorkerHostConfiguration.cs:99`) inside `TemporalCheck.CheckHealthAsync`, or record an
   explicit decision that configuration-level readiness is sufficient and update the criterion
   wording plus the stale "Phase 3 fills the real connectivity check" comment.

## Human verification

1. **Re-validate the replay fixture against a live dev server when the Temporal SDK or image
   changes.** Start `podman-compose --profile workflows up`, run the ADR-0003 export commands,
   diff against `tests/OmpOS.Workflows.Tests/TestData/histories/session-run.json`, re-run
   `TemporalReplayTests`. Why human: requires podman + network + a long-lived dev server; this
   verifier is read-only and the current environment has no podman daemon asserted.
2. **Run `RealPodmanComposeConfig_SmokeTest` on a podman host.**
   (`tests/OmpOS.Sandbox.Tests/LocalPodmanComposeProviderContractTests.cs:136` skips cleanly
   here.) Why human: needs podman/podman-compose binaries plus the repo compose file; asserts
   `podman-compose config` renders and contains "temporal".
3. **Decide the 4c readiness semantics** (live probe vs configuration-level) with the
   operator, then either accept the gap fix or amend roadmap criterion 4. Why human: product
   decision, not a code fact.

## Requirement impact of known open questions (not defects)

- **Q-SB-AGENTOS / Q-SB-RAILWAY / Q-SB-ENCLAVE** (`docs/open-questions.md:35-37`) + OQ-24/OQ-06:
  cover SBOX-04 exactly as specified — the requirement *asks for* interface-conforming stubs
  with owner-tagged questions, which is what ships. No requirement impact beyond the intended
  stub boundary.
- **Q-WF-RETRY** (`:38`): documents that the direct path (WFLW-01) fails fast with teardown in
  `finally` while only the Temporal path retries. Matches the verified code
  (`DirectSessionRunner.cs:65` vs `TemporalSessionRunWorkflow.cs:150-179`); no requirement
  violated, but callers must route retry-needing runs to Temporal per ADR-0003.
- **Q-SB-CREDENTIALS** (`:39`): SBOX-05 "per-sandbox credentials" is currently
  duplicate-target rejection only (`LocalPodmanComposeProvider.cs:439`); issuance is deferred.
  Requirement text is satisfied at the settings level, but operators must know issuance is not
  implemented.
- **Q-SB-WORKER-COMPOSE** (`:40`): only `IProcessRunner` is DI-registered and fake activities
  are opt-in; real provider/activity composition into the worker is future work. WFLW-02 is met
  for the worker host + contract seam, not for a production-composed worker.
- **Q-HARN-PERMISSION / Q-HARN-WS / Q-HARN-UPDATE / Q-AC-LIST / Q-WEB-REFLECTION**: Phase 2/4/5
  scope; no Phase 3 requirement depends on them. The macOS MAUI build deferral is out of scope
  for Phase 3 (Linux-only providers verified here).

## Suite-green reference (not executed by this verifier)

`/tmp/bootstrap-final3.log:63-64,70-71`: `OmpOS.Sandbox.Tests` and `OmpOS.Workflows.Tests`
Release DLLs both report `passed`; `:70-71` summary `total: 527, failed: 0, succeeded: 527,
skipped: 0`. Per-suite case counts (70/20) are derived above by attribute arithmetic, not read
from the log (the log does not print per-suite counts).

## Post-verification fixes

Fixes landed after the verification above; each cites a grep-confirmed test:

- Attested enclave stub honesty: `VendorStubTests` proves `AttestedEnclaveProvider` throws `NotAvailable` for every sandbox operation and its attestation verdict stays `Unverified` (`AttestedEnclaveProvider_GetQuoteAsync_ReturnsUnverified`).
- Real readiness probe (closes criterion-4c gap): `services/OmpOS.Api/Health/TemporalCheck.cs` now performs a real TCP reachability check against the configured Temporal endpoint instead of reporting Healthy from configuration alone; `TemporalReadinessTests` (`Enabled_WithReachableEndpoint_ReturnsHealthy`, `Enabled_WithClosedPort_ReturnsUnhealthy`, `Enabled_WithUnparsableEndpoint_ReturnsUnhealthy`) pins reachable/unreachable/unparsable behavior.
- WFLW-01 API wiring: `services/OmpOS.Api/Workflows/ApiSessionRunActivities.cs` is the in-process `ISessionRunActivities` the API prompt path executes through `DirectSessionRunner` when `Workflows:Enabled=false`; `SessionRunWorkflowTests` covers the composed session-run path.
