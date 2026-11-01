---
phase: 68-data-plane-names-schema-and-resilience
plan: 01
requirements: [AG-68-8]
status: complete
---

# 68-01 Summary: Autonogrammer.Resilience (AG-68-8)

Hand-written L1 library `Autonogrammer.Resilience`: deadline, decorrelated-jitter retry, consecutive-and-ratio breaker, bulkhead, all on an injected `TimeProvider`. BCL plus `Microsoft.Extensions.Logging.Abstractions` only; no Polly.

## Commits

| Hash | Message |
| --- | --- |
| `ae647893` | chore(68-01): reference Microsoft.Extensions.Logging.Abstractions (csproj plus Resilience and Resilience.Tests locks) |
| `75cd3622` | test(68-01): add failing resilience contract tests |
| `d8b65f68` | chore(68-01): lock Resilience Logging.Abstractions edge in its dependents (6 dependent locks, only the hunk for the `autonogrammer.resilience` project entry) |
| `786c8fe9` | feat(68-01): implement hand-written resilience pipeline (library, plus analyzer fixes to the test files) |

## Public surface (for 68-02, 68-03, 68-04, 68-05)

- `ResiliencePipeline(ResiliencePolicy, TimeProvider, Random? = null, ILogger<ResiliencePipeline>? = null)`
  - `ExecuteAsync<T>(OperationClass, bool idempotent, Func<CancellationToken, Task<T>>, CancellationToken)` and a result-less overload.
  - `ExecuteTransactionAsync<T>(OperationClass, Func<CancellationToken, Task<T>>, CancellationToken)`: one whole-transaction retry, only when `policy.IsSerializationFailure` matches.
  - `Component`, `State` (`DependencyState`).
- `OperationClass { Interactive, Background }`, `DependencyState { Healthy, Degraded, Open }`.
- `DataPlaneTimeoutException { Component, Reason }`, `DataPlaneTimeoutReason { Timeout, CircuitOpen, BulkheadFull }`. No inner exception, never a connection string.
- `ResiliencePolicy` (record) built by `PostgresResilienceOptions.ToPolicy(isTransient, isSerializationFailure)`, `DragonflyResilienceOptions.ToPolicy(isTransient)`, `LedgerResilienceOptions.ToPolicy(isTransient)`. Consumers pass the driver predicates (for example `NpgsqlException.IsTransient`, SQLSTATE 40001); the default predicate knows only BCL timeout/IO/socket/HTTP failures.
- Section names (68-04 validators): `DataPlane:Postgres:Resilience`, `DataPlane:Dragonfly:Resilience`, `DataPlane:Ledger:Resilience` (a `Resilience` sub-section so they cannot collide with the 68-04 `DataPlane:Postgres:*` / `DataPlane:Ledger:*` keys).
- `ResilienceTelemetry.MeterName = "Autonogrammer.Resilience"`; instruments `autonogrammer.dataplane.call.duration` (ms histogram), `.retry.count`, `.breaker.transitions`, `.bulkhead.rejected`; tags only `component`, `operation_class`, `result`, `reason`. 68-04 adds `.AddMeter(ResilienceTelemetry.MeterName)`.

## D-41 constants as built

| | PostgreSQL | Dragonfly | Ledger |
| --- | --- | --- | --- |
| attempt timeout | 5 s interactive, 30 s background | 250 ms | 10 s |
| retries | 2, delay 100 ms to 2 s, budget 8 s | 1, delay 20 to 80 ms, budget derived (2 x 250 ms + 80 ms) | 0 in the pipeline (outbox row is the retry state) |
| bulkhead | 16 interactive, 4 background | 64 shared | 2 shared |
| breaker open | 15 s | 10 s | 60 s |

Breaker (all components): opens at 5 consecutive failures, or at 50 % failures once the last 20 calls (all younger than 30 s) are in the window; exactly one half-open probe; a probe success closes it, a probe failure reopens it for a full window. Ledger option properties `MaxAttempts 8`, `FirstDelaySeconds 5`, `MaxDelaySeconds 600` are carried for the 68-05 pump; the pipeline does not apply them.

## Behaviour decisions made while implementing (not spelled out in the plan)

- Each attempt has its own timeout; the call has a total deadline of `Budget` (or derived), never shorter than the class timeout. No retry starts when `elapsed + next delay >= total`; then the last failure surfaces unchanged. A timed-out call ends as `DataPlaneTimeoutException(Timeout)`.
- Caller cancellation: the original exception reaches the caller, is not retried and is not a breaker failure (D-42). Tests prove it before and during the operation.
- Breaker verdicts: timeouts and failures the policy calls transient are failures; any other exception (constraint violation, serialisation failure) counts as the dependency having answered, so it does not open the breaker. Bulkhead and breaker rejections are never retried and never counted as dependency failures.
- The ratio rule is evaluated on failures only and needs 20 samples inside the 30 s window ([INFERENCE] reading of "50 % of the last 20 calls within 30 s"; fewer samples would let 1 failure of 2 calls trip it).
- `DependencyState` is public, not internal as the plan text says, because the Api status service (68-04, another assembly) maps it.
- Open breaker and full bulkhead surface as `DataPlaneTimeoutException` with a `Reason`, because the library has no other exception type and 68-04 needs `CircuitOpen` versus `Timeout`; the plan's file list is otherwise kept (plus `OperationClass.cs`, `DataPlaneTimeoutReason.cs`, `BreakerPermit.cs`, `ResiliencePolicy.cs` and the three options files, one type per file for MA0048).
- Time is read through `TimeProvider.GetUtcNow()` (the manual clock does not override `GetTimestamp`).

## Verification (what was actually run)

- `ag-dotnet test --project tests/Autonogrammer.Resilience.Tests -c Release -- --filter-not-trait "Category=Live"`: **61 passed, 0 failed, 0 skipped**, no container.
- Build gate `ag-dotnet build src/Autonogrammer.Resilience/... -c Release --no-restore -warnaserror -nodeReuse:false -m:1`: see the line at the end of this file.
- Source assertions run: no `Polly`, no `DateTime.UtcNow`/`DateTimeOffset.UtcNow`, no `Coordination` or `Sessions.Postgres` text in `src/Autonogrammer.Resilience`; the csproj holds exactly one `PackageReference`; `git diff` of `Directory.Packages.props`, `Autonogrammer.slnx`, `Autonogrammer.Linux.slnf`, `LayerMap.cs` is empty; no `Thread.Sleep` or `Task.Delay` in the test project.
- RED phase: the RED commit (`75cd3622`) references types that did not exist yet, so the test project did not compile at that commit; the RED verify command was **not executed** (the implementation files already existed on disk, uncommitted, while the host's single dotnet lock queue took 20 to 50 minutes per call).

## Locks

- Resilience and Resilience.Tests locks: Direct `Microsoft.Extensions.Logging.Abstractions 10.0.13` plus CentralTransitive `Microsoft.Extensions.DependencyInjection.Abstractions 10.0.13` (not a new reference; it is the dependency of Logging.Abstractions under central transitive pinning).
- Dependents that gained only the `autonogrammer.resilience` project dependency line: Sessions.Postgres, Sessions.Postgres.Tests, Sessions.Postgres.Probe, Coordination.Tests, Api.IntegrationTests, ArchitectureTests (committed in `d8b65f68`, only my hunk of each, because other leads' lock edits were also in the working tree of the shared files).
- `Coordination.Dragonfly` and `Api` need no entry (both reference `Microsoft.AspNetCore.App`, which carries Logging.Abstractions).
- **Not done:** a clean `CI=true restore Autonogrammer.Linux.slnf --locked-mode` pass. The run I made failed (NU1004) on projects whose locks other leads were changing at that moment (Accounts, Harness.Secrets.Os, Harness.Workspace and their dependents); the lock files of those projects, and the other-lead hunks in the `Api`, `Api.IntegrationTests`, `ArchitectureTests` locks, are in the working tree uncommitted and belong to 66/67a. The final phase gate must run `restore --force-evaluate` on the slnf, commit the resulting locks, then the locked restore.

## Suppressions added (68-07 owns the SUP rows, D-67)

- One `SuppressMessage` `CA5394` (insecure randomness) on `RetryPolicy.NextDelay` in `src/Autonogrammer.Resilience/RetryPolicy.cs`, same justification as SUP-042/043 (`SandboxLifecycle`): backoff jitter, the `Random` is injected. Needs one kept SUP row. No `#pragma warning disable`.

## Files changed

Source: `src/Autonogrammer.Resilience/` `Autonogrammer.Resilience.csproj`, `packages.lock.json`, `ResiliencePipeline.cs`, `ResiliencePolicy.cs`, `RetryPolicy.cs`, `CircuitBreaker.cs`, `BreakerPermit.cs`, `Bulkhead.cs`, `DependencyState.cs`, `OperationClass.cs`, `DataPlaneTimeoutException.cs`, `DataPlaneTimeoutReason.cs`, `ResilienceOptions.cs`, `PostgresResilienceOptions.cs`, `DragonflyResilienceOptions.cs`, `LedgerResilienceOptions.cs`, `ResilienceTelemetry.cs`.
Tests: `tests/Autonogrammer.Resilience.Tests/` `packages.lock.json`, `ResiliencePipelineTests.cs`, `RetryPolicyTests.cs`, `CircuitBreakerTests.cs`, `BulkheadTests.cs`, `Support/ManualTimeProvider.cs`, `Support/ClockDriverExtensions.cs`, `Support/CapturingLogger.cs`, `Support/TransientFault.cs`, `Support/SerializationFault.cs`, `Support/TestPolicies.cs`.
Locks (dependents): `src/Autonogrammer.Sessions.Postgres`, `tests/Autonogrammer.{Sessions.Postgres.Tests, Sessions.Postgres.Probe, Coordination.Tests, Api.IntegrationTests, ArchitectureTests}` `packages.lock.json`.

## Build gate result

`ag-dotnet build src/Autonogrammer.Resilience/Autonogrammer.Resilience.csproj -c Release --no-restore -warnaserror -nodeReuse:false -m:1`: Build succeeded, 0 warnings, 0 errors.
