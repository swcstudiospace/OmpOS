---
phase: 74-dispatch-relay
plan: "11"
subsystem: coordination
tags: [dispatch, idempotency, postgres, readiness]
status: not_shipped_blocked
---

# 74-11 Summary

Plan `74-11-PLAN.md` (gap-closure, wave 1, AG-74-3/6/7/12/13/14/17) was re-verified against the current tree on `origin/main` @ `c04095ba`. **This plan has not shipped.** The blockers below were observed in the shipped code, not inferred.

## Current tree evidence (2026-11-21)

- `src/Autonogrammer.Sessions.Postgres/PostgresDispatchStore.cs` `InsertCommandAsync` still projects
  `request.IdempotencyKey ?? request.CommandId` into `idempotency_key`. The plan's core truth — "a new
  unkeyed command never occupies a caller-key identity or projects its command ID as a caller key" — is
  therefore **not met** by the shipped store: an unkeyed command's `CommandId` can be read back by
  `ReadIdempotentAsync` as if it were a caller key, and a keyed row whose value equals a later command ID
  collides with it. `ReadIdempotentAsync` (fingerprint check → `IdempotencyKeyReused`/`Replayed`) exists,
  but the insert side still conflates the two namespaces the plan requires separated.
- `services/Autonogrammer.Api/Dispatch/Options/DispatchOptionsValidator.cs` now requires, outside
  Development, `Sessions:Store=Postgres`, a `HubOrigin`, and durable coordination for `ReplicaMode=Multi`.
  This is **stricter** than the plan's assumption (validator refused enabled Dispatch outside Development).
  The plan's readiness work must be re-scoped against this validator: enabled-production readiness now
  depends on `DispatchModule` composition plus checked Postgres/Dragonfly readiness, which the current
  `DispatchModule.cs` wires through `IDispatchStore` — the validator, not the module, is the gate.
- Migration assumption is stale: plan says "M0010 remains unmodified and unregistered". On the current
  tree `M0010_DispatchDeviceState.cs` exists and is registered (`MigrationRegistry.cs` carries M0010–M0015,
  contiguous M0001–M0015). **No production migration and no M0010 registry change was made by this doc
  lane**; the plan's migration text must be rewritten against M0010–M0015 before any execution lane.

## What did not happen

- `tests/Autonogrammer.Sessions.Postgres.Tests/Dispatch/PostgresDispatchStoreTests.cs` (real-PostgreSQL
  collision/legacy/concurrency cases) was not executed — `scripts/test-services.sh` throwaway services were
  not started by this doc lane, and the DB-backed suites require them. Recorded as an open verification
  gap, not a pass.
- `tests/Autonogrammer.Api.IntegrationTests/Dispatch/DispatchPipelineTests.cs` and
  `DispatchOptionsValidatorTests.cs` exist on the tree, but no current implementation lane was run for
  this plan; their present contents exercise the pre-plan store/validator behavior described above.

## Verified in this session (no product-code change)

- `dotnet restore Autonogrammer.Linux.slnf --locked-mode` — ok.
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` — 0 warnings, 0 errors.
- `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore` — clean.
- `dotnet test tests/Autonogrammer.Contracts.Tests --filter-class "*Dispatch*"` — 27 succeeded, 0 failed.
- `dotnet test tests/Autonogrammer.Dispatch.Desktop.Tests` — 80/80.
- `dotnet test tests/Autonogrammer.Dispatch.Client.Tests` — 1/1.
- `dotnet test tests/Autonogrammer.Presentation.Tests` — 2646/2646.

## Next step for the owning lane

Separate keyed vs unkeyed identity in `PostgresDispatchStore.InsertCommandAsync` (nullable `idempotency_key`,
no `?? CommandId` projection; ambiguous legacy rows fail rather than replay), add the plan's regression
cases, run the throwaway-services DB gate, and rewrite the plan's M0010/migration section against the
registered M0010–M0015 registry. Changes are L2 product code — route to A05/A07, not this doc lane.
