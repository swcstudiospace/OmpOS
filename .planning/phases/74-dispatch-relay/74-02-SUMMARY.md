---
phase: 74-dispatch-relay
plan: 74-02
subsystem: sessions-postgres
tags: [dispatch, postgres, migration, store]

requires:
  - 74-01 IDispatchStore and DispatchStoreContractTests
provides:
  - M0010 dispatch_device_state columns and unique (id, account_id)
  - PostgresDispatchStore over M0005 plus M0010
affects: [74-05]

actuals:
  tokens: 0
  tasks: 3
  commits: []

tech-stack:
  added: []
  patterns:
    - Sequence allocation is UPDATE dispatch_devices SET command_seq = command_seq + 1 RETURNING under the device row lock
    - Feed ids come from accounts.feed_seq under the account row lock
    - Tenant hash is a constructor delegate so Sessions.Postgres does not reference Coordination

key-files:
  created:
    - src/Autonogrammer.Sessions.Postgres/Migrations/M0010_DispatchDeviceState.cs
    - src/Autonogrammer.Sessions.Postgres/PostgresDispatchStore.cs
    - tests/Autonogrammer.Sessions.Postgres.Tests/Dispatch/PostgresDispatchStoreTests.cs
  modified:
    - src/Autonogrammer.Sessions.Postgres/Migrations/MigrationRegistry.cs
    - tests/Autonogrammer.Sessions.Postgres.Tests/Autonogrammer.Sessions.Postgres.Tests.csproj

key-decisions:
  - "Fingerprint, capability, issue time, failure reason and result text live in dispatch_commands.result. M0005 has no columns for them and M0010 only adds the eight device columns."
  - "OQ-423 default: body and result text are cleared together seven days after a terminal state. The result document keeps the fingerprint so a later read still has it."
  - "Feed prune uses the injected clock against occurred_at text. A one-tick advance is below timestamptz precision, so a SQL cutoff comparison would not match the shared oracle."

requirements-completed: [AG-74-6, AG-74-12, AG-74-13]

# Phase 74 plan 02 summary

Postgres dispatch store: atomic per-device sequence and queue cap, composite owner key, same-transaction audit, and account feed ids from `accounts.feed_seq`.

## Tests

`source "$(bash scripts/test-services.sh up)" && /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Sessions.Postgres.Tests -c Release -- --filter-class "*Dispatch*"`

13 passed, 0 failed.

## Gaps

None in this plan. `last_source_prefix` is persisted only when a later hub writes it; this store has no writer for that column because `IDispatchStore` does not carry it.
