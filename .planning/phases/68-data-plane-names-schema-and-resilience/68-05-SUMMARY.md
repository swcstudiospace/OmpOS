---
phase: 68-data-plane-names-schema-and-resilience
plan: 05
requirements: [AG-68-11]
status: complete
---

# 68-05 Summary: audit ledger sink (AG-68-11)

Optional, default-off notarisation path. `IAuditLedger` is `NullAuditLedger` unless `DataPlane:Ledger:Enabled=true`, then `SubstrateAuditLedger`. The request path only enqueues. The hosted `LedgerOutboxPump` is the only caller of `SubstrateGateway.EmitEventAsync`. No Solana client, key, transaction or RPC URL.

## Registration

One line in `DataPlaneServiceCollectionExtensions.AddDataPlane` (68-04-owned): `LedgerSinkServiceCollectionExtensions.AddLedgerSink(services);`. `AddDataPlane` has no `IConfiguration` parameter and `Program.cs` is not this plan's file, so the call is not `AddLedgerSink(configuration)`. Options are already bound; the factory reads `IOptionsMonitor<DataPlaneOptions>` when the ledger is first resolved, so the gateway is built only when `Enabled` is true. No csproj edit.

## Behaviour

- Closed `AuditLedgerEvent`: `Action` (always `autonogrammer.audit.v1`), `Item`, `Outcome`, `Summary`, `TenantHash`. Kind is always `note`. The field-lock test fails if a public instance property is added. JSON is source-generated (`AuditJsonContext`).
- `RecordAsync` on the null ledger does not touch `ledger_outbox`. On the substrate ledger it enqueues in the caller's transaction (savepoint, so a table failure does not abort the state change) and does not emit.
- The pump uses `LedgerResilienceOptions.ToPolicy` (10 s, no pipeline retry, 2 in flight, breaker open 60 s) and spaces outbox retries at 5 s doubling to 10 min, 8 attempts, then parks the row. Stopping-token cancellation aborts an in-flight emit and returns. Failures are not thrown to callers of `RecordAsync`.
- Bearer token is read by the gateway token provider from `DataPlane:Ledger:Token` on each request. It is not logged or interpolated.
- Tenant hash is sent as `GraphId` because `SubstrateEventPayload` has no tenant field. Surface stays null (the token's surface).
- `ILedgerStatusSource` is the pump. Depth is a cached count. The pump also creates `autonogrammer.ledger.outbox.depth` on the `Autonogrammer.Api` meter with no account, device or audit tag. `DataPlaneStatusService` (68-04) already publishes the same instrument from `ILedgerStatusSource`; both read the same cached depth.
- A host whose session store is not Postgres uses an in-memory outbox so design-time OpenAPI startup does not resolve `NpgsqlDataSource`.

## Verification

`/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*AuditLedgerEventFieldLockTests*" --filter-class "*LedgerSinkTests*" --filter-class "*SubstrateLiveRefusalTests*"`: **5 passed, 1 skipped, 0 failed**. The live test skipped (no `AUTONOGRAMMER_LIVE_SUBSTRATE=1`). No connection to port 7410.

## Suppressions (68-07 owns the SUP row, D-67)

- `CA2000` on `LedgerSinkTests`: the service provider owns the scripted handler. No `#pragma warning disable`.

## Files

`services/Autonogrammer.Api/Audit/` (`IAuditLedger`, `NullAuditLedger`, `SubstrateAuditLedger`, `AuditLedgerEvent`, `AuditJsonContext`, `LedgerOutboxPump`, `LedgerSinkServiceCollectionExtensions`, plus `LedgerOutboxItem`, `ILedgerOutboxStore`, `MemoryLedgerOutboxStore`, `PostgresLedgerOutboxStore` because MA0048 requires one type per file). One line in `DataPlane/DataPlaneServiceCollectionExtensions.cs`. Tests: `LedgerSinkTests`, `AuditLedgerEventFieldLockTests`, `SubstrateLiveRefusalTests`.
