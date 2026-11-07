---
phase: 68-data-plane-names-schema-and-resilience
plan: 02
requirements: [AG-68-1, AG-68-2, AG-68-9]
status: complete
---

# 68-02 Summary: Dragonfly keyspace v1, tenant hash, fail modes, erasure

## Commits

| Hash | Message |
| --- | --- |
| `ae6b5954` | test(68-02): keyspace, tenant hasher, fail-mode, erasure and ACL probe tests |
| `47975369` | feat(68-02): DragonflyKeyspace v1, keyed tenant hasher, resilience fail modes, tenant erasure |
| (this commit) | fix(68-02): ACL probe split (MA0051), report file, comment wording, SUMMARY |

The RED commit does not compile on its own (it names types that the feat commit adds); the RED verify command was not run.

## What was built

- `DragonflyKeyspace` (internal, `InternalsVisibleTo Autonogrammer.Coordination.Tests`): `Prefix = "autonogrammer:v1:"`; `LeaseRun`, `Idempotency` (SHA-256 hex of scope, newline, key), `Presence`, `RateLimit`, `AuthNonce`, `SignalSession`/`SignalDispatch` channels, `TenantScanPattern`, `RequireTimeToLive` (null, zero, negative or above 24 h throws `ArgumentOutOfRangeException`), `RequireTenant` (32 lowercase hex). Session, device, policy and window ids are charset-checked (letters, digits, `_`, `-`, at most 128). `DragonflyKeys.cs` deleted; every caller moved; no alias.
- Every write is `SET ... PX` or a Lua script with `PX` in the same command; no SET-then-EXPIRE. Lease and renew TTLs go through `RequireTimeToLive`. Channel renamed to `autonogrammer:v1:sig:session`.
- `ITenantHasher` (Coordination, L1) and `TenantHasher` plus `TenantHasherOptions { Pepper }` (Dragonfly, L2): first 16 bytes of HMAC-SHA256 as 32 lowercase hex; blank pepper or account is refused; email never special-cased; pepper never logged.
- `DragonflyResilience` (public wrapper of one `ResiliencePipeline` with the Dragonfly outage predicate) shared by all stores over a connection. `ClosedAsync` turns `RedisConnectionException`/`RedisTimeoutException`/BCL transient failures and attempt timeouts into `DataPlaneTimeoutException`; breaker/bulkhead refusals keep their `Reason`; server answers (for example a script error) pass through unchanged. `TryOpenAsync` reports an outage as `false`.
  - Closed: lease acquire, renew, `IsHeld`; idempotency claim, complete, release; health ping. `SET NX` claims (lease, idem) run with `idempotent:false` (never retried); reads and scripts are retried once (20-80 ms) via the pipeline.
  - Open: signal publish (dropped, logged Warning) and subscribe (returns an inert handle; the poll is the safety net); lease release on dispose is best-effort (the lease expires by TTL).
- `DragonflyCoordinationHealthCheck` pings through the pipeline and carries `Data["dependencyState"]`; also exposes `State`.
- `InProcessRateLimiter` (Coordination, L1): one bucket of `limit` tokens per policy, tenant and window, dropped at window end plus 5 s; non-positive limit or window throws, so never unlimited.
- `ITenantKeyEraser` + `NoOpTenantKeyEraser` (L1); `DragonflyTenantErasure` runs `SCAN MATCH autonogrammer:v1:*:{tenant}:*` (via `IServer.KeysAsync`, page 500) and deletes in batches through the pipeline; a malformed tenant throws `ArgumentException` before any command. `AddDragonflyCoordination` registers `DragonflyResilience`, `ITenantKeyEraser`, and applies `OpMs` as the client `AsyncTimeout` and `ConnectSeconds` as `ConnectTimeout` (options optionally read from `IOptions<DragonflyResilienceOptions>`).
- Not registered here (68-04): `ITenantHasher`/`TenantHasher`, `NoOpTenantKeyEraser` fallback, the `DataPlane:Dragonfly:Resilience` binding.

## Deviations and facts to know

- Component string is `"dragonfly"` (lowercase, from the 68-01 policy), not `"Dragonfly"` as the plan text says; 68-04 should map on that value.
- Erasure cannot reach lease, idempotency or nonce entries: the ADR key table (D-11) has no tenant segment in them (`lease:run:{sessionId}`, `idem:{sha}`, `auth:nonce:{sha}`), so `*:{tenant}:*` matches only presence and rate-limit keys. Those other entries hold reconstructible state and expire within 24 h (nonce 10 min). The plan text says erasure deletes "lease, idem, presence, rl and auth keys of that tenant"; that is not achievable without changing the D-11 key grammar. Left as the table says.
- `rl` and `presence` and `auth` have key builders only: no store exists for them on this base (consumers are phases 67/74), so their fail modes (rate limit degrading to `InProcessRateLimiter`; presence "unknown"; nonce closed) are not wired to a Dragonfly store and have no Dragonfly-side test beyond the limiter's own tests.
- A failed subscribe is not retried in the background; signals stay off for that subscription until the next subscribe (poll safety net covers it).
- `TenantHasherOptions` is its own file (MA0048), not inside `TenantHasher.cs`.
- Acceptance grep `grep -rn "DragonflyKeys" ...` is literally satisfied only with word matching: the substring is inside `DragonflyKeyspace`. `grep -w` over src, services, tests, apps finds it only in a stale `bin/` of the ArchitectureTests output.
- Live tests use `DragonflyTestEnvironment.LiveOptions()` (OpMs 10 s, connect 15 s) because the production 250 ms deadline failed 10 of the existing tests on this loaded host (first run, all `DataPlaneTimeoutException`/signal timeouts). Fail-mode tests use the defaults.
- Locks: no new package or project reference; no lock file touched.

## ACL probe result (v1.27.1 image, run once, gated)

`ACL SETUSER ... ~autonogrammer:v1:*:{tenant}:* &autonogrammer:v1:sig:* +@all` accepted; own-tenant key SET allowed, other-tenant key SET denied. Pattern isolation is enforced on this image. Per D-09 it stays defence in depth only; nothing in the product depends on it.

## Verification run

- `ag-dotnet test --project tests/Autonogrammer.Coordination.Tests -c Release --no-build -- --filter-not-trait "Category=Live"` inside the serialised container shape: 77 passed, 0 failed, 0 skipped (existing 31-ish plus new keyspace, hasher, limiter, fail-mode, erasure tests).
- Gated ACL test (`AUTONOGRAMMER_LIVE_DRAGONFLY_ACL=1`): 1 passed; without the variable: skipped (observed once).
- Source greps: only one `"autonogrammer:` literal (the Prefix); no EXPIRE sequence (only `PEXPIRE` in the renew script); no FLUSHDB/FLUSHALL/`KEYS `/`SELECT ` in `src/Autonogrammer.Coordination.Dragonfly`; no `podman stop|kill|restart` in the tests.
- Not run: ArchitectureTests (no reference or project added), the whole-solution build, the Api.IntegrationTests.

## Suppressions (for 68-07 SUP rows)

- Unchanged: `DragonflySessionSignals.cs` still carries SUP-019 (CA1031) on `PumpAsync`. No suppression added or removed.
- `Assert.Skip` marker added: `tests/Autonogrammer.Coordination.Tests/DragonflyAclTests.cs:24` (needs a SUP row).
- No `#pragma`.

## Files changed

Source: `src/Autonogrammer.Coordination/` `ITenantHasher.cs`, `ITenantKeyEraser.cs`, `NoOpTenantKeyEraser.cs`, `InProcessRateLimiter.cs`. `src/Autonogrammer.Coordination.Dragonfly/` `DragonflyKeyspace.cs`, `DragonflyKeys.cs` (deleted), `DragonflyResilience.cs`, `DragonflyTenantErasure.cs`, `NoSubscription.cs`, `TenantHasher.cs`, `TenantHasherOptions.cs`, `DragonflyIdempotencyStore.cs`, `DragonflySessionRunLease.cs`, `DragonflySessionRunLeases.cs`, `DragonflySessionSignals.cs`, `DragonflyCoordinationHealthCheck.cs`, `DragonflyCoordinationServiceCollectionExtensions.cs`, `DragonflyScripts.cs` (comment only), `Autonogrammer.Coordination.Dragonfly.csproj` (InternalsVisibleTo).
Tests: `tests/Autonogrammer.Coordination.Tests/` `DragonflyKeyspaceTests.cs`, `TenantHasherTests.cs`, `DragonflyFailModeTests.cs`, `DragonflyTenantErasureTests.cs`, `DragonflyAclTests.cs`, `InProcessRateLimiterTests.cs`, `UnreachableDragonfly.cs`, `DragonflyCoordinationTests.cs`, `DragonflySignalHandlerFailureTests.cs`, `DragonflyTestEnvironment.cs`.
