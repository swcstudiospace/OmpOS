---
phase: "3"
slug: "platform"
status: partial
nyquist_compliant: false
wave_0_complete: true
created: "2026-10-21"
---

# Phase 3 — Validation Strategy

Scope: the 8 requirements that ROADMAP.md (Phase 3 `Requirements:` line) and the REQUIREMENTS.md traceability table assign to Phase 3: SBOX-01..05, WFLW-01..03.

## Compliance

- `nyquist_compliant: false`. 5 of 8 requirements are COVERED. 3 are PARTIAL; each uncovered clause is listed in the Gap Register below.
- A requirement is COVERED only when every clause of its text has an automated command and each named test exists in `tests/`.
- Test names were confirmed by grep of `tests/**/*.cs`. Commands were **not executed** in this audit. A filter that matches zero tests must be treated as a failure, not a pass.

## Requirement Coverage

| Req | Status | Automated command | Confirmed test names (grep-verified) |
|---|---|---|---|
| SBOX-01 | COVERED | `dotnet test tests/OmpOS.Sandbox.Tests --filter "FullyQualifiedName~FakeSandboxProviderContractTests\|FullyQualifiedName~LocalPodmanComposeProviderContractTests"` | Inherited from `SandboxProviderContractTests<T>`, run by `FakeSandboxProviderContractTests` and `LocalPodmanComposeProviderContractTests`: `SandboxProviderContractTests.Provision_Attach_Exec_Logs_Teardown_HappyPath`, `IdempotencyKey_DedupesConcurrentProvisions` (idempotency key), `Teardown_IsIdempotent`, `StatusTransitions_CoverEnum` (`SandboxStatus` enum), `Timeout_IsHonoured` (explicit timeouts), `FailedProvision_NeverLeavesSessionRunning` |
| SBOX-02 | COVERED | `dotnet test tests/OmpOS.Sandbox.Tests --filter "FullyQualifiedName~SandboxLifecycleTests\|FullyQualifiedName~SandboxLifecycleRegressionTests"` | `SandboxLifecycleTests.TtlExpiry_TriggersReaper`, `OrphanReaper_SweepsOrphanedSandboxes` (orphan-reaper hook), `Teardown_RetriedWithJitteredBackoff`, `FailedProvision_NeverLeavesSessionRunning`, `Provision_FailsFast_WhenSessionNotRunning`, `Teardown_Idempotent_NotFoundIsSuccess`; `SandboxLifecycleRegressionTests.ReapOnce_ReapsSandboxExactlyAtTtl`, `Provision_SessionStoreFailure_WrappedAsTyped`, `EqualJitterDelay_StaysWithinHalfToFullBase`, `EqualJitterDelay_CappedAtThirtySeconds` |
| SBOX-03 | COVERED | `dotnet test tests/OmpOS.Sandbox.Tests --filter "FullyQualifiedName~FakeSandboxProviderContractTests\|FullyQualifiedName~LocalPodmanComposeProviderContractTests\|FullyQualifiedName~ProvisionCleanupRegressionTests\|FullyQualifiedName~TeardownFailureTests\|FullyQualifiedName~ProcessRunnerDrainTests\|FullyQualifiedName~ProcessRunnerOutputCapTests"` | `FakeSandboxProviderContractTests.FailProvision_InjectsFailures`, `FailTeardown_InjectsFailures`, `FailAfterPartial_LeavesNoRunningSandbox`; `LocalPodmanComposeProviderContractTests.Provision_GeneratesComposeFileAndRunsUp`, `Exec_RunsPodmanExec`, `Teardown_RunsPodmanComposeDown` (fake `IProcessRunner`), `RealPodmanComposeConfig_SmokeTest` (`Assert.SkipUnless` podman and podman-compose on PATH); `ProvisionCleanupRegressionTests.TypedUpFailure_DeletesComposeFileAndAttemptsDown`, `GenericRunnerFailure_DeletesComposeFileAndAttemptsDown`; `TeardownFailureTests.NonZeroDownExit_ThrowsTypedWithoutRawStderr`, `TimedOutDown_ThrowsTyped`; `ProcessRunnerDrainTests.GrandchildHoldingPipe_DrainIsBounded`; `ProcessRunnerOutputCapTests.LargeOutput_IsCappedAtOneMiBPerStreamAndMarkedTruncated` |
| SBOX-04 | PARTIAL | `dotnet test tests/OmpOS.Sandbox.Tests --filter "FullyQualifiedName~VendorStubTests\|FullyQualifiedName~GoldenPolicyTests\|FullyQualifiedName~PolicyYamlRegressionTests"` | `VendorStubTests`: `OpenShellProvider_Capabilities_AreHonest`, `OpenShellProvider_AllOperations_ThrowNotAvailable`, `OpenShellProvider_CheckPrerequisites_ReturnsFailureForPodman493`, `AgentOsProvider_Capabilities_AreHonest`, `AgentOsProvider_AllOperations_ThrowNotAvailable`, `RailwaySwarmProvider_Capabilities_AreHonest`, `RailwaySwarmProvider_AllOperations_ThrowNotAvailable`, `AttestedEnclaveProvider_Capabilities_AreHonest`, `AttestedEnclaveProvider_GetQuoteAsync_ReturnsUnverified`, `AttestedEnclaveProvider_WrapsInnerProvider`; `GoldenPolicyTests.OpenShellProvider_GatewayToml_MatchesGolden`, `OpenShellProvider_PolicyYaml_MatchesGolden`, `OpenShellPolicyModel_Version1_HardRequirementLandlock`, `OpenShellGatewayModel_Version2_PodmanDriver`; `PolicyYamlRegressionTests.NonEmptyNetworkPolicies_RenderAsYamlMappings`, `RuleToString_QuotesYamlSignificantValues`. Gap: see Gap Register |
| SBOX-05 | COVERED | `dotnet test tests/OmpOS.Sandbox.Tests --filter "FullyQualifiedName~IsolationDefaultsTests\|FullyQualifiedName~ProvisionCleanupRegressionTests.SecretRefs_RejectedWhenHostSecretsDisallowed\|FullyQualifiedName~ProvisionCleanupRegressionTests.DuplicateSecretTarget_RejectedWhenPerSandboxCredentials"` | `IsolationDefaultsTests`: `FakeProvider_ProvisionedDescriptor_NoHostPathsOrSecrets`, `PodmanProvider_ProvisionedDescriptor_NoHostPathsOrSecrets`, `PodmanProvider_HostMountRejected_WhenAllowHostMountsFalse`, `PodmanProvider_HostMountAllowed_WhenAllowHostMountsTrue`, `PodmanProvider_BindMountWithAbsoluteSource_RejectedAsHostMount`, `PodmanProvider_VolumeMountWithAbsoluteSource_RejectedAsHostMount`, `PodmanProvider_TmpfsMountWithAbsoluteSource_RejectedAsHostMount`, `PodmanProvider_NamedVolumeWithoutPathCharacters_AllowedWhenHostMountsDenied`, `NetworkPolicy_DefaultDeny_Enforced`, `FakeProvider_Logs_NoSecretValues`, `PodmanProvider_Exec_NoSecretValuesInOutput`; `ProvisionCleanupRegressionTests.SecretRefs_RejectedWhenHostSecretsDisallowed`, `DuplicateSecretTarget_RejectedWhenPerSandboxCredentials`. Per-sandbox credential issuance is not tested (see Notes) |
| WFLW-01 | PARTIAL | `dotnet test tests/OmpOS.Workflows.Tests --filter "FullyQualifiedName~DirectRunnerTests"` | Inherited from `SessionRunWorkflowContractTests<TFixture>` via `DirectRunnerTests`: `HappyPath_ProvisionsRunsCollectsTearsDownInOrder`, `PromptFailure_StillTearsDown`, `TeardownFailure_RecordedAsFailedStatus`, `ProvisionFailure_SessionNotRunning_NoTeardown`, `HeartbeatEvents_ObservedDuringPrompt`, `Cancellation_ResultsInCancelledStatusWithTeardown`. Gap: default in-process composition is not tested (see Gap Register) |
| WFLW-02 | PARTIAL | `dotnet test tests/OmpOS.Workflows.Tests --filter "FullyQualifiedName~WorkerHostTests\|FullyQualifiedName~TemporalReplayTests\|FullyQualifiedName~TemporalTeardownGuardTests\|FullyQualifiedName~DirectRunnerTests.HeartbeatEvents_ObservedDuringPrompt"` | `WorkerHostTests`: `WorkerMain_Disabled_ReturnsZero`, `WorkerHost_Disabled_ConfigureServicesRefusesRegistration`, `WorkerHost_Enabled_RegistersTemporalServices` (starts only when enabled), `WorkerOptions_ReadFromConfiguration`, `WorkerOptions_Defaults_Disabled`, `WorkerOptions_Validation_FailsWhenEnabledWithoutAddress`, `WorkerHost_Enabled_RegistersFailLoudGuardByDefault`, `WorkerHost_EnabledFakeActivities_RegistersFakes`; `TemporalReplayTests.SessionRunWorkflow_ReplaysDeterministically` (replay test), `HistoryFixture_IsWellFormed`; `TemporalTeardownGuardTests.TeardownFailure_AfterCompleted_RecordedAsFailed`, `TeardownFailure_AfterFailed_PreservesOriginalFailure`, `TeardownFailure_AfterCancelled_PreservesCancelled`; heartbeat via `DirectRunnerTests.HeartbeatEvents_ObservedDuringPrompt`. Gap: retry-policy values untested (see Gap Register) |
| WFLW-03 | COVERED | `dotnet test tests/OmpOS.Workflows.Tests --filter "FullyQualifiedName~DirectRunnerTests"` | Abstract `SessionRunWorkflowContractTests<TFixture>` executed against the direct runner by `DirectRunnerTests` (6 methods, listed under WFLW-01). The contract is reusable: no Temporal fixture subclasses it yet, and Temporal behaviour is covered separately by `TemporalReplayTests` and `TemporalTeardownGuardTests` |

## Manual-Only Verifications

| Req | Reason |
|---|---|
| SBOX-03 (real podman) | `RealPodmanComposeConfig_SmokeTest` runs only when `podman` and `podman-compose` are on PATH and otherwise skips. A skip is not a pass. Host has podman 4.9.3 per 03-01-SUMMARY.md, so the real smoke test needs its own run record. |
| SBOX-04 ("no vendor API invented") | Review judgement. No test can assert that a vendor API was not invented. |
| WFLW-02 (decision criteria) | The routing decision criteria for Temporal must be documented. This is a documentation check and is not automated. |

## Gap Register (requirement clauses without an automated test; orchestrator to decide)

| Req | Missing | Evidence |
|---|---|---|
| SBOX-04 | `AttestedEnclaveProvider` does not return a typed `NotAvailable`. It wraps an inner provider and delegates provision, exec, logs and snapshot to it, which conflicts with the requirement's wording that all four vendor stubs return `NotAvailable`. The test `AttestedEnclaveProvider_WrapsInnerProvider` asserts the delegation. | `src/OmpOS.Sandbox/AttestedEnclaveProvider.cs:28-49, 49, 81, 99, 118, 132`; `tests/OmpOS.Sandbox.Tests/VendorStubTests.cs:157-169`. This is a spec-versus-implementation mismatch that the orchestrator must resolve. |
| SBOX-04 | The owner-tagged open questions (`Q-SB-OPENSHELL`, `Q-SB-RAILWAY`, `Q-SB-AGENTOS`, `Q-SB-ENCLAVE`) exist only in source doc comments. No test asserts the tags. | `src/OmpOS.Sandbox/OpenShellProvider.cs:11`, `RailwaySwarmProvider.cs:8`, `AgentOsProvider.cs:8`, `AttestedEnclaveProvider.cs:13`. |
| WFLW-01 | `DirectSessionRunner` is not registered in any host's DI container, so the `Workflows:Enabled=false` default path has no composition and no test. The API does not invoke a workflow at all. | Grep for `DirectSessionRunner` in `services/`, `src/` and `apps/` finds only its own definition (`src/OmpOS.Workflows/DirectSessionRunner.cs:14`) and doc comments (`services/OmpOS.Workflows.Worker/TemporalSessionRunWorkflow.cs:13,140`). No `AddSingleton`/`AddScoped`/`AddTransient` registration exists. `03-01-SUMMARY.md` records this as Q-SB-WORKER-COMPOSE. |
| WFLW-02 | The retry policies (`MaximumAttempts = 3` and the other values) and the heartbeat timeout in the Temporal workflow are not asserted by any test. Heartbeats are exercised only through the direct runner's fake context. | `services/OmpOS.Workflows.Worker/TemporalSessionRunWorkflow.cs:150-172`. |

## Notes

- Commands here were not run (no `dotnet test` in this audit). Before this file can move to `validated`, run each command and confirm each reports a non-zero match count and the named tests pass.
- Per-sandbox credentials (SBOX-05) are enforced only as duplicate-target rejection. Credential issuance is not implemented (`Q-SB-CREDENTIALS`, 03-01-SUMMARY.md), so the clause is covered only to the extent of the explicit setting and its enforcement.
- `nyquist_compliant` stays `false` until the three PARTIAL requirements in the Gap Register are closed or the orchestrator accepts them explicitly.
