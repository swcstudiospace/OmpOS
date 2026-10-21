---
phase: 3-platform
reviewed: 2026-10-20T00:00:00Z
depth: standard
files_reviewed: 48
findings:
  critical: 4
  warning: 12
  info: 4
  total: 20
status: findings
---

# Phase 3 (platform): Code Review Report

**Reviewed:** 2026-10-20
**Depth:** standard (per-file, language-aware; cross-file tracing for lifecycle/provider/workflow paths)
**Files Reviewed:** 48 across `src/OmpOS.Sandbox.Abstractions`, `src/OmpOS.Sandbox`,
`src/OmpOS.Workflows`, `services/OmpOS.Workflows.Worker`, `tests/OmpOS.Sandbox.Tests`,
`tests/OmpOS.Workflows.Tests`, plus `docs/decisions/ADR-0003-workflow-routing.md`
**Status:** findings

## Summary

Reviewed the platform slice against the six assigned priorities. Real strengths:
argv-only process invocation (`ProcessSpec.Argv`, `UseShellExecute=false`),
source-generated compose serialization, default-deny network (`"none"` + internal bridge),
`landlock: hard_requirement` with empty `network_policies`, retry policies plus heartbeat
on all Temporal activities, a real-export replay fixture, and the worker's early `return 0`
when disabled. The findings below are the gaps that survive underneath those strengths:
two resource-cleanup holes on the provision/teardown path, one unbounded pipe drain in the
test runner that defeats its own timeout, and one unguarded `finally` teardown in the
Temporal workflow that contradicts its own result-contract. Warnings cover filter/label
injection adjacencies, dead isolation options, host-path leakage into descriptors,
and composition gaps (no production `IProcessRunner`, fake activities in prod DI).

## Critical Issues

### CR-01: Typed provision failures skip compose-file cleanup, leaking temp files and half-provisioned containers

**File:** `src/OmpOS.Sandbox/LocalPodmanComposeProvider.cs:109-112`
**Issue:** `ProvisionAsync` deletes the generated compose file only in
`catch (Exception ex) when (ex is not SandboxException)`. But the dominant failure modes
*are* `SandboxException`s: `BringUpAsync` throws `ProvisionTimeoutException` on non-zero
exit (line 303) and on timeout (line 306), and `LookupContainerIdAsync` failures surface
after the file is written. In all those cases the compose file remains in `Path.GetTempPath()`
and any half-started containers from `podman-compose up` are never torn down (the sandbox
is never registered, so neither the lifecycle reaper nor `TeardownAsync` can reach it).
**Why it matters:** Every failed `up` accumulates a temp file and orphaned containers;
over time this exhausts disk/container capacity on the host and contradicts the
"failed provision leaves nothing behind" guarantee.
**Fix:**
```csharp
catch (Exception ex) when (ex is not SandboxException)
{
    DeleteComposeFile(composeFile);
    throw new ProvisionTimeoutException($"Provisioning failed: {ex.Message}", ex);
}
// becomes: delete on ALL failure paths, rethrow typed failures untouched
catch (SandboxException)
{
    DeleteComposeFile(composeFile);
    try { await TeardownPartialAsync(projectName, composeFile, cancellationToken).ConfigureAwait(false); } catch { /* best effort */ }
    throw;
}
catch (Exception ex)
{
    DeleteComposeFile(composeFile);
    throw new ProvisionTimeoutException($"Provisioning failed: {ex.Message}", ex);
}
```

### CR-02: Provider `TeardownAsync` ignores the process result, so teardown failures are silent and lifecycle retries never fire

**File:** `src/OmpOS.Sandbox/LocalPodmanComposeProvider.cs:226-244`
**Issue:** `TeardownAsync` awaits `_processRunner.RunAsync(...)` for
`podman-compose down -v` and discards the `ProcessResult` entirely — non-zero exit and
`TimedOut: true` both fall through to `DeleteComposeFile` + `_sandboxes.TryRemove` in the
`finally`, reporting success. `SandboxLifecycle.TeardownAsync` retries only on thrown
exceptions, so its jittered-retry guarantee is dead code end-to-end against this provider:
the exact failure mode retries exist for (transient `down` failure) can never trigger one.
**Why it matters:** Containers/volumes survive while the registry claims destruction; the
reaper counts them reaped. Resource leak plus false success signal.
**Fix:**
```csharp
var result = await _processRunner.RunAsync(new ProcessSpec { ... }, cancellationToken).ConfigureAwait(false);
if (result.TimedOut)
{
    throw new TeardownFailedException(sandboxId, 1, "podman-compose down timed out");
}
if (result.ExitCode != 0)
{
    throw new TeardownFailedException(sandboxId, 1, $"podman-compose down failed (exit {result.ExitCode}): {result.Stderr}");
}
```

### CR-03: `RealProcessRunner` pipe drain is unbounded — a lingering child pipe hangs the suite despite `spec.Timeout`

**File:** `tests/OmpOS.Sandbox.Tests/RealProcessRunner.cs:44-45`
**Issue:** The timeout only guards `WaitForExitAsync` (lines 36-42). After the process
exits, `await stdoutTask` / `await stderrTask` wait unconditionally. If the child spawned
a grandchild that inherits stdout/stderr (daemons, `podman-compose up -d --wait` helpers,
log tailers), `ReadToEndAsync` never completes and the awaiting test hangs forever —
`spec.Timeout` no longer applies. The priority for this file is explicitly "must enforce
its timeout so the suite cannot hang"; the current shape enforces it only on process exit,
not on output drain, which is the classic pipe-deadlock hang.
**Why it matters:** One misbehaving subprocess wedges the whole test run with no timeout,
no kill, and no diagnostic.
**Fix:**
```csharp
using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
drainCts.CancelAfter(TimeSpan.FromSeconds(30)); // or a fraction of spec.Timeout
var stdout = await stdoutTask.WaitAsync(drainCts.Token).ConfigureAwait(false);
var stderr = await stderrTask.WaitAsync(drainCts.Token).ConfigureAwait(false);
```
(on timeout: kill tree if still alive, return `TimedOut: true`). Also observe/abandon the
tasks on the timeout path so pipe-reader exceptions never go unobserved.

### CR-04: Temporal workflow `finally` teardown is unguarded — a teardown failure escapes as a thrown exception instead of a result status

**File:** `services/OmpOS.Workflows.Worker/TemporalSessionRunWorkflow.cs:83-87`
**Issue:** The `finally` block awaits `TeardownSandboxAsync` with no `try/catch`. After
its 5-attempt retry policy is exhausted the activity throws, the exception escapes the
`finally`, masks whatever `Completed`/`Failed` status was already computed, and fails the
workflow task — which the Temporal server will retry as a whole, re-executing provision
against the same idempotency key. The class's own contract ("failures surface as
`SessionRunResult` statuses rather than thrown exceptions") and the sibling
`DirectSessionRunner.TryTeardownAsync` (catch-and-record) both say the opposite.
**Why it matters:** Behavioral divergence between the two runners; worst case is
duplicate provisioning side effects plus loss of the original failure detail.
**Fix:** Mirror the direct runner:
```csharp
finally
{
    if (provisioned)
    {
        try
        {
            await Workflow.ExecuteActivityAsync<TemporalSessionRunActivities, bool>(
                activities => activities.TeardownSandboxAsync(input),
                TeardownOptions());
        }
        catch (Exception teardownEx)
        {
            if (status == WorkflowRunStatus.Completed)
            {
                status = WorkflowRunStatus.Failed;
            }
            failureDetail = $"Teardown failed: {teardownEx.Message}";
        }
    }
}
```
(note: no `ConfigureAwait(false)` inside workflow code — keep continuations on the
workflow scheduler).

## Warnings

### WR-01: `LookupContainerIdAsync` trusts raw `ps` output — empty or multi-line output registers a garbage container id

**File:** `src/OmpOS.Sandbox/LocalPodmanComposeProvider.cs:322`
**Issue:** `return psResult.Stdout.Trim();` with no validation. Zero matches yield an
empty container id (sandbox registered, later `podman exec "" ...` fails obscurely);
multiple matches (stale projects, label collision) yield an embedded-newline blob used as
a single argv element. Exit code of `ps` is never checked either.
**Fix:** Check `psResult.ExitCode == 0`, take the first non-empty line, and
`throw new ProvisionTimeoutException(...)` unless exactly one id is found.

### WR-02: Caller-controlled `SessionId` interpolated into podman label-filter values without validation

**File:** `src/OmpOS.Sandbox/LocalPodmanComposeProvider.cs:315-316`
**Issue:** `$"label=ompos.session={sessionId}"` builds a `--filter` argv element from
request data. There is no shell (argv-only — good), but podman filter syntax gives `=`
and `,` structural meaning, so a crafted `sessionId` can broaden the filter and match a
*foreign* container, whose id is then trusted for `exec`. `sandboxId` is provider-generated
(safe); `sessionId` is not.
**Fix:** Validate `request.SessionId` (and `IdempotencyKey`) at provision entry against
`^[A-Za-z0-9_.-]+$` and reject otherwise; independently verify the looked-up container
carries the expected `ompos.sandbox` label before trusting it.

### WR-03: `AllowHostSecrets` and `PerSandboxCredentials` are dead options — never enforced

**File:** `src/OmpOS.Sandbox/LocalPodmanComposeProvider.cs:340-356`
**Issue:** `ValidateIsolationDefaults` enforces `AllowHostMounts` and
`OutboundNetworkDefaultDeny` but never reads `AllowHostSecrets` (default `false`) or
`PerSandboxCredentials` (default `true`). Operators setting the documented safe defaults
get no enforcement; a future caller passing secret values via `Spec.Environment` (which
`GenerateComposeFile` serializes verbatim into the on-disk compose file) would sail through.
**Fix:** Either enforce — reject `SecretRefs`/secret-bearing environment when the flags
forbid it — or delete the options so the config surface does not promise what the code
does not do.

### WR-04: Sandbox descriptor labels embed a host temp path (`ompos.compose_file`)

**File:** `src/OmpOS.Sandbox/LocalPodmanComposeProvider.cs:279-280`
**Issue:** `CreateDescriptor` copies the absolute compose-file path (and raw container id)
into `Descriptor.Labels`. Descriptors are serialized, logged, and streamed (the isolation
canary tests only assert absence of specific fixture strings, not of temp paths), so a
host filesystem layout leaks into descriptors/logs/history — exactly the class of leak
priority (2) forbids.
**Fix:** Drop `ompos.compose_file` (and `ompos.container_id`) from descriptor labels;
keep them in the internal `PodmanSandbox` record only.

### WR-05: Raw tool stderr is embedded in exception messages that can carry environment content

**File:** `src/OmpOS.Sandbox/LocalPodmanComposeProvider.cs:303`
**Issue:** `$"podman-compose up failed: {upResult.Stderr}"` copies tool output into the
exception message. The compose file under test contains `Spec.Environment` verbatim, and
compose tooling echoes file content Fragments in error diagnostics — so a failed `up`
can propagate environment values into exception messages, logs, and (via the workflow
`FailureDetail`) session history.
**Fix:** Return a generic message (`"podman-compose up failed (exit N); see debug log"`),
and log stderr at Debug with redaction, or scrub known secret values before embedding.

### WR-06: TTL expiry boundary is off by one — exactly-at-TTL sandboxes are never reaped by that tick

**File:** `src/OmpOS.Sandbox/SandboxLifecycle.cs:231`
**Issue:** `now - entry.ProvisionedAt > entry.Ttl` uses strict `>`. A sandbox whose age
equals `Ttl` exactly is not expired; with coarse reaper ticks this can defer reaping by a
full sweep interval.
**Fix:** `>=`.

### WR-07: Full-jitter backoff can collapse to ~0ms, turning retries into a busy loop

**File:** `src/OmpOS.Sandbox/SandboxLifecycle.cs:277-278`
**Issue:** `ComputeJitteredDelay` returns `NextDouble() * baseDelay` — uniform over
`[0, base)`. Draws near zero are common, so consecutive teardown attempts can fire
back-to-back against an already-failing provider (thundering herd in miniature), and the
30s `Math.Min` cap applies to the jitter draw rather than the total delay.
**Fix:** Equal jitter — `base/2 + U[0, base/2)` — and cap the total:
```csharp
var delay = baseDelayMilliseconds / 2 + _random.NextDouble() * baseDelayMilliseconds / 2;
return TimeSpan.FromMilliseconds(Math.Min(delay, 30000));
```

### WR-08: Non-empty `network_policies` render as CLR type names — policy YAML generation is broken beyond the golden empty case

**File:** `src/OmpOS.Sandbox/OpenShellProvider.cs:221-228`
**Issue:** `FormatYamlList` stringifies items with `ToString()`, and `NetworkPolicyRule`
(`src/OmpOS.Sandbox/NetworkPolicyRule.cs`) does not override it. The golden test only
covers the empty list (`network_policies: []`), so the first real rule would emit
`- OmpOS.Sandbox.NetworkPolicyRule` — a silently invalid policy for the one mechanism
(priority 4) that must default-deny network.
**Fix:** Give `NetworkPolicyRule` a structured mapping (destination/port/protocol/action
lines) or override `ToString()` to emit valid YAML, and add a non-empty golden test.

### WR-09: No production `IProcessRunner` exists — providers are unwirable outside tests

**File:** `tests/OmpOS.Sandbox.Tests/RealProcessRunner.cs:1-13`
**Issue:** The only `IProcessRunner` implementation in the repo is `internal` to the test
assembly. `LocalPodmanComposeProvider` and `OpenShellProvider` both require one via DI,
so there is no production composition that can actually run them. The argv-only safety
story (priority 2) therefore rests entirely on a test double.
**Fix:** Ship a production runner (the `RealProcessRunner` logic, hardened per
CR-03/WR-11, with `ArgumentList` and secret-redacting logs) in `src/OmpOS.Sandbox` and
register it in DI; or document explicitly that providers are not production-wired yet.

### WR-10: Production worker DI registers stub activities that silently succeed with canned data

**File:** `services/OmpOS.Workflows.Worker/WorkerHostConfiguration.cs:86`
**Issue:** `services.AddSingleton<ISessionRunActivities>(new FakeSessionRunActivities())`
means an operator who flips `Workflows:Enabled=true` gets a worker that provisions fake
descriptors, reports `FinalSeq = 1`, and collects zero artifacts — all reported as success.
The placeholder remark exists in code comments, but nothing at runtime distinguishes
"stubbed" from "working".
**Fix:** Until real activities land, fail fast or log at `Critical` on first use
(`throw new InvalidOperationException("real session-run activities not registered")`),
so the worker cannot silently fabricate successful runs.

### WR-11: `RealProcessRunner` hand-rolls argv quoting instead of using `ArgumentList`, and abandons pipe readers on timeout

**File:** `tests/OmpOS.Sandbox.Tests/RealProcessRunner.cs:19,116-124`
**Issue:** `Arguments = string.Join(" ", Argv.Skip(1).Select(EscapeArg))` re-implements
quoting that only handles space/quote/apostrophe. Tabs, newlines, backslashes, and
non-printables split or corrupt argv on POSIX (where .NET re-parses the string). Separately,
`KillOnTimeout` never `WaitForExit`s after `Kill(entireProcessTree: true)` and the timeout
path abandons `stdoutTask`/`stderrTask`, whose later exceptions go unobserved while `using`
disposes the process underneath them.
**Fix:** Use `ProcessStartInfo.ArgumentList` (no manual quoting at all); after kill,
`WaitForExit` bounded, then drain-or-abandon with observed continuations.

### WR-12: Session-store failures in `ProvisionAsync` propagate raw and untyped

**File:** `src/OmpOS.Sandbox/SandboxLifecycle.cs:80`
**Issue:** `sessionStatusProvider` is awaited *before* the `try` block, so a session-store
outage throws whatever the callback throws (I/O, timeout, custom) instead of the typed
`ProvisionTimeoutException` the post-check path produces. Callers handling typed sandbox
failures see an inconsistent contract depending on where the failure happened.
**Fix:** Move the status check inside the `try`, or wrap callback exceptions in
`ProvisionTimeoutException` (preserving the `InvalidOperationException` fail-fast for
non-Running sessions).

## Info

### IN-01: `DeleteComposeFile` catches only two exception types, so exotic failures mask the original error

**File:** `src/OmpOS.Sandbox/LocalPodmanComposeProvider.cs:325-340`
**Issue:** Only `IOException` and `UnauthorizedAccessException` are caught; `DeleteComposeFile`
runs inside `finally` (teardown) and `catch` (provision) blocks, where an
`ArgumentException`/`SecurityException`/`NotSupportedException` from a malformed path
would mask the in-flight failure.
**Fix:** Catch `Exception` here (cleanup is best-effort; log and continue) or validate the
path once at creation.

### IN-02: Disabled-worker test asserts a vacuous property

**File:** `tests/OmpOS.Workflows.Tests/WorkerHostTests.cs:52-68`
**Issue:** `WorkerHost_Disabled_NoTemporalServicesRegistered` builds a bare host that never
calls `ConfigureServices` and asserts it has no Temporal services — true by construction,
independent of production code. The real guarantee (early `return 0` in `Program.Main`)
is covered by `WorkerMain_Disabled_ReturnsZero`; this test would pass even if the disabled
path registered a full worker.
**Fix:** Assert on the disabled path itself (e.g., invoke the `Main`-equivalent branch and
verify the service collection is untouched), or fold into the `Main` exit-code test.

### IN-03: Direct runner has no activity retry; heartbeats are a no-op

**File:** `src/OmpOS.Workflows/DirectSessionRunner.cs:47-60`, `src/OmpOS.Workflows/DirectWorkflowContext.cs:30-33`
**Issue:** Each activity is attempted exactly once and `Heartbeat` discards details. This
matches the ADR (retries live at the Temporal call site), but it leaves the default
(`Enabled=false`) path — i.e., most runs — with strictly weaker resilience and no
progress observability on long prompts.
**Fix:** Document the trade-off in the ADR consequences, or add a small bounded retry for
idempotent activities (provision/collect/teardown) in the direct runner.

### IN-04: OpenShell YAML/TOML generation is string interpolation, and policy models are get-only

**File:** `src/OmpOS.Sandbox/OpenShellProvider.cs:200-218`, `src/OmpOS.Sandbox/OpenShellPolicy.cs:20`
**Issue:** Safe today only because every interpolated value is a code default. Any future
parameterization (custom socket paths, image names) becomes a TOML/YAML injection sink;
meanwhile `NetworkPolicies`/`ReadOnly`/`ReadWrite` are get-only `= []`, so round-tripping
policy YAML back through STJ cannot repopulate them.
**Fix:** Serialize via a TOML/YAML library or source-generated JSON with fixed keys, and
give the collections setters (or `[JsonInclude]`) if deserialization is ever needed.

---

## Verified compliant (no finding)

- **Argv-only invocation, no shell:** `ProcessSpec.Argv` contract plus `UseShellExecute=false`
  in the runner; no `/bin/sh`, `cmd.exe`, or `shell_exec` anywhere in scope.
- **Default-deny network:** `SandboxProviderOptions.OutboundNetworkDefaultDeny=true`,
  `NetworkPolicy.DefaultDeny`, compose `network_mode: "none"` + internal bridge,
  `PolicyDeniedException` on `OutboundDefault.Allow`.
- **OpenShell safe defaults:** `landlock.compatibility: hard_requirement`,
  `network_policies: []`, golden files match generators byte-for-byte.
- **Temporal determinism:** no `DateTime.Now`/`Random`/`Guid.NewGuid`/`Task.Run` in
  `TemporalSessionRunWorkflow.cs`; retries on all four activities (3/1/3/5 per ADR);
  `HeartbeatTimeout` + heartbeat forwarding on `RunPrompt`; no `ConfigureAwait` in
  workflow code; replay test runs against a real dev-server export.
- **Worker disabled behavior:** `Program.Main` returns 0 before `ConfigureServices`;
  options default `Enabled=false`; address validation names `Workflows:Address`.
- **Failed provision never leaves a session Running:** lifecycle registers only after
  provider success and fails fast on non-Running sessions (subject to CR-01 cleanup
  and WR-12 typing notes above).

_Reviewed: 2026-10-20_
_Reviewer: P3Review (gsd-code-review, adversarial)_
_Depth: standard_
