# Phase 3: Platform - Context

**Gathered:** 2026-10-20
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss); decisions LOCKED from the ultrathink specification (nodes n4.6 SPE-17785, n4.7-n4.8 SPE-17787/SPE-17788, n5.5 SPE-17776, n5.6 SPE-17778), `.planning/research/ARCHITECTURE.md` (sandbox and Temporal topology), PITFALLS 5 and 12, and Phase 2's shipped Session and Harness contracts.

<domain>
## Phase Boundary

Sandbox provisioning and durable session runs. Deliverables: `OmpOS.Sandbox.Abstractions` (L1), `OmpOS.Sandbox` (fake, local podman-compose, OpenShell, agentOS, Railway, enclave stubs; lifecycle; reaper), `OmpOS.Workflows` (direct runner, workflow contracts, activities), `services/OmpOS.Workflows.Worker` (Temporal host, started only behind config), contract fixtures in tests. No live vendor calls, no Railway or Vercel deployment.
</domain>

<decisions>

### D-01 Sandbox abstraction (SBOX-01) - LOCKED
- `ISandboxProvider`: `ProvisionAsync(ProvisionRequest, ct)`, `AttachAsync(SandboxId, ct)`, `ExecAsync(SandboxId, ExecRequest, ct)` returning `IAsyncEnumerable<ExecEvent>`, `StreamLogsAsync(SandboxId, LogCursor?, ct)`, `SnapshotAsync(SandboxId, ct)` (NotSupported for providers without snapshots), `TeardownAsync(SandboxId, ct)`, and `Capabilities`.
- `ProvisionRequest`: required `IdempotencyKey` (same key returns the same sandbox), `SessionId`, required bounded `Timeout` (maximum 10 minutes), requested isolation tier, `NetworkPolicy` (default deny), `Mounts` (default empty), `SecretRefs` (references only, never values).
- `SandboxStatus` (Contracts, Phase 1) transitions are a static table with tests; invalid transitions throw.
- Typed errors: `SandboxException` tree (`ProvisionTimeout`, `PrerequisiteMissing`, `NotAvailable`, `PolicyDenied`, `TeardownFailed`) mapped to `OmpOsError` codes.

### D-02 Lifecycle safety (SBOX-02) - LOCKED
- `SandboxLifecycle` wraps any provider: TTL (default 30 minutes, `Sandbox:TtlMinutes`), an in-memory `SandboxRegistry` keyed by `SandboxId` with `SessionId`, `IdempotencyKey` and labels `ompos.session=<id>`, `ompos.idempotency=<key>`.
- `ISandboxReaper.ReapOnceAsync(now)` tears down expired sandboxes and orphans (registry entry whose session is Completed, Failed or Cancelled, or missing). A hosted service runs it every 60 seconds only when `Sandbox:Reaper:Enabled=true`.
- Teardown retries with full-jitter exponential backoff (base 500 ms, factor 2, maximum 5 attempts), then reports `TeardownFailed`. Teardown is idempotent; NotFound counts as success.
- A failed provision never leaves the session `Running`: the session goes to `Failed`, and partial resources are reaped within the same call (tested with a provider that fails after partial provisioning).
- Provision retries are bounded (default 3 attempts, jittered) and apply only to retryable errors (`ProvisionTimeout`, transient `NotAvailable`). `PrerequisiteMissing` and `PolicyDenied` are never retried.

### D-03 Providers (SBOX-03, SBOX-04, SBOX-05) - LOCKED
- `FakeSandboxProvider`: deterministic in-memory sandboxes with failure injection (`FailProvision`, `FailAfterPartial`, `HangTeardown`, `FailTeardownOnce`); records calls; passes `SandboxProviderContractTests<T>`.
- `LocalPodmanComposeProvider`: generates a per-sandbox compose file from a typed model (no host mounts, read-only root, `network_mode: none` unless policy allows). Runs through an injectable `IProcessRunner` using argument arrays only (never shell strings), project name `ompos-<sandboxId>`, `podman-compose -p <project> -f <file> up -d --wait` and `down -v`. Unit tests assert argv exactly with a fake runner. A live test runs only when `podman` and `podman-compose` are on PATH and `podman info` succeeds, otherwise it is skipped with an explicit reason. Tests never pull images.
- `OpenShellProvider`: typed `OpenShellPolicy` model generates policy YAML v1 (`version: 1`, `filesystem_policy`, `landlock`, `network_policies` empty = default deny) and gateway TOML (`[openshell]` version 2, `[openshell.gateway] compute_driver = "podman"`, `[openshell.drivers.podman]` socket path). Both are golden-tested. `ProvisionAsync` checks prerequisites (podman 5.x or later, cgroups v2, user socket) and returns `PrerequisiteMissing` naming the failed check. The local apt podman is 4.9.3, so the test asserts `PrerequisiteMissing` here. When prerequisites pass it invokes `openshell sandbox create --name <name> --policy <file> --no-auto-providers`, the verb documented in `omega-prime/omega_prime/hosting/openshell/sandbox-policy.yaml`. Other OpenShell verbs stay `NotAvailable` until verified and are recorded as open questions.
- `AgentOsProvider`: stub. Capabilities `NativeBinaries=false`. Every operation returns `NotAvailable` with reason "omp native addons not verified inside agentOS" (open question). No vendor calls.
- `RailwaySwarmProvider`: stub. Every operation returns `NotAvailable` ("Railway Cloud Swarm interface unknown"). No HTTP calls.
- `AttestedEnclaveProvider`: wraps an inner provider and an `IAttestationBoundary { Task<AttestationReport> AttestAsync(ReadOnlyMemory<byte> nonce, ct) }` shaped on the dstack guest API v1 (Attest, Info, Version). The default implementation returns verdict `Unverified`. `SandboxDescriptor.Attestation` may be `Quote` only when a verifier has validated it; otherwise `None`.
- Provider selection: `Sandbox:Provider` = `Fake` | `LocalPodman` | `OpenShell` | `AgentOs` | `Railway` | `Enclave` (default `Fake`). `Enclave` wraps the configured inner provider.
- Isolation defaults (SBOX-05): no host paths or host secrets mounted (`Mounts` rejected by policy unless `Sandbox:AllowHostMounts=true`, default false). Network default deny. Per-sandbox credentials only through `SecretRef`, resolved inside activities. A canary test proves a `SecretRef` value never appears in `SandboxDescriptor`, logs or workflow payloads.

### D-04 Durable workflows (WFLW-01..03) - LOCKED
- `OmpOS.Workflows` (L2; references Sandbox.Abstractions and Sessions, not Temporalio): `ISessionRunActivities` with `ProvisionSandboxAsync`, `RunPromptAsync` (appends StreamEvents to the session log; heartbeat payload carries `lastSeq`), `CollectArtifactsAsync`, `TeardownSandboxAsync`; `ISessionRunWorkflow.RunAsync(SessionRunInput)`; `DirectSessionRunner` executes the same activity classes in-process with teardown in `finally` and cancellation. `SessionRunInput` carries identifiers and cursors only (no transcripts, payloads under 64 KiB).
- `services/OmpOS.Workflows.Worker` (L3, `Microsoft.NET.Sdk.Worker`): Temporalio 1.20.0 with `Temporalio.Extensions.Hosting`. `[Workflow] SessionRunWorkflow` implements `ISessionRunWorkflow` through `Workflow.ExecuteActivityAsync`. Retry policies: provision 3 attempts; prompt 1 attempt (prompts are not idempotent); teardown retried with backoff within 10 minutes. Heartbeats on long activities. Cancellation via `Workflow.CancellationToken`, teardown in `finally` with `WaitCancellationCompleted`. Determinism: no `DateTime.Now`, `Guid.NewGuid`, `Random`, `Task.Run` or `Task.Delay` inside workflow code; use `Workflow.UtcNow`, `Workflow.NewGuid`, `Workflow.Random`, `Workflow.DelayAsync`.
- `Workflows:Enabled` defaults to false. When false the worker host does not start and `SessionService` uses `DirectSessionRunner`. `Workflows:Temporal:Address` (default `localhost:7233`), `Namespace` (default `default`), `TaskQueue` (default `ompos-session-runs`). Validation requires `Address` when enabled.
- Determinism proof without a server: `Temporalio` `WorkflowReplayer` over committed history fixtures recorded once from a test run. The replay test fails on any non-deterministic change.
- Routing criteria are recorded in `docs/decisions/ADR-0006-temporal-routing.md`: route through Temporal only when the run must survive process restarts, exceed the request lifetime, or need durable retries or timers. Otherwise `DirectSessionRunner`.

### D-05 Health and observability - LOCKED
- Readiness check `temporal` registered only when `Workflows:Enabled=true` (connect with a 2 second timeout). A `sandbox-provider` readiness check reports provider capabilities; stub providers report degraded, not failing.
- Histogram `ompos.sandbox.provision_ms` emitted on every provision (success and failure), tagged with provider and outcome.

### D-06 Tests - LOCKED
- `tests/OmpOS.Sandbox.ContractTests`: abstract `SandboxProviderContractTests<TFixture>` (provision idempotency, status transitions, teardown idempotency, TTL expiry triggers reap, `PolicyDenied` for host mounts, never `Running` after failed provision). Concrete subclasses for Fake and LocalPodman (fake runner), and typed `NotAvailable` checks for the stubs.
- `tests/OmpOS.Workflows.Tests`: `SessionRunWorkflowContractTests<TFixture>` run against `DirectSessionRunner`; replay test over recorded histories; a test that the Temporal worker does not start when `Workflows:Enabled=false`.
- Test projects for every new src or services project that contains code. Each is added to `OmpOS.slnx` and `OmpOS.Linux.slnf`.

### Claude's Discretion
Record names, folder layout inside projects, backoff constants within the stated bounds, and the choice to keep `SandboxRegistry` in memory (persistence is deferred and recorded as an open question).
</decisions>

<code_context>
Phase 2 delivers OmpOS.Sessions (SessionService, ISessionRepository, sequenced StreamEvent log), OmpOS.Harness.* (IOmpSession, HarnessEvent, IToolApprovalPolicy) and the OmpOS.Api host. Read the shipped code and docs/decisions/ADR-0003..0005 before planning and reuse names exactly. Toolchain: `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH`. podman 4.9.3 (apt) and podman-compose 1.6.0 are installed by the orchestrator. OpenShell requires podman 5.x, so OpenShell provisioning reports `PrerequisiteMissing` on this box.
</code_context>

<specifics>
- No Temporal package may be referenced by OmpOS.Web, OmpOS.UI or any project they reference (architecture test).
- The Temporal `[Workflow]` class lives only in `services/OmpOS.Workflows.Worker`.
- Every vendor fact not verified during research is recorded in docs/open-questions.md with owner `ompos-maintainers`.
</specifics>

<deferred>
- Persistent SandboxRegistry (PostgreSQL): v2 (PLAT-03).
- Live OpenShell provisioning on a Podman 5.x host: v2 (PLAT-01).
- Snapshot support: providers return NotSupported.
</deferred>
