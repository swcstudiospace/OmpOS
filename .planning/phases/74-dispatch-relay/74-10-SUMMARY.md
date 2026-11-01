---
phase: 74-dispatch-relay
plan: 74-10
subsystem: coordination
tags: [dispatch, dragonfly, presence, nonce, signals]

requires:
  - 68-02 DragonflyKeyspace and DragonflyResilience
provides:
  - IDispatchSignals, IDispatchPresence and IDispatchAuthNonces
  - In-memory and Dragonfly implementations registered on both coordination methods
affects: [74-05, 74-07]

actuals:
  tokens: 0
  tasks: 2
  commits: []

tech-stack:
  added: []
  patterns:
    - Dispatch signals publish a device id only on DragonflyKeyspace.SignalDispatch and fail open
    - Nonces are SET NX PX and GETDEL, fail closed with DataPlaneTimeoutException
    - Presence is one key with PX in the same command; refresh reuses the lease compare-and-set script; unreachable reads are Unknown

key-files:
  created:
    - src/Autonogrammer.Coordination/IDispatchSignals.cs
    - src/Autonogrammer.Coordination/IDispatchPresence.cs
    - src/Autonogrammer.Coordination/IDispatchAuthNonces.cs
    - src/Autonogrammer.Coordination/DispatchPresence.cs
    - src/Autonogrammer.Coordination.Dragonfly/DragonflyDispatchSignals.cs
    - src/Autonogrammer.Coordination.Dragonfly/DragonflyDispatchPresence.cs
    - src/Autonogrammer.Coordination.Dragonfly/DragonflyDispatchAuthNonces.cs
    - tests/Autonogrammer.Coordination.Tests/InMemoryDispatchCoordinationTests.cs
    - tests/Autonogrammer.Coordination.Tests/DragonflyDispatchCoordinationTests.cs
    - tests/Autonogrammer.Coordination.Tests/DispatchSignalsRegistrationTests.cs
  modified:
    - src/Autonogrammer.Coordination/InMemoryCoordinationServiceCollectionExtensions.cs
    - src/Autonogrammer.Coordination.Dragonfly/DragonflyCoordinationServiceCollectionExtensions.cs
    - tests/Autonogrammer.Coordination.Tests/InMemoryCoordinationRegistrationTests.cs

key-decisions:
  - "DispatchPresence is its own file so the snapshot and its status enum are not a second top-level type inside the interface file."
  - "Presence refresh reuses DragonflyScripts.RenewLease, which already declares KEYS[1] and extends only when the stored holder matches."
  - "Nonce consume is GETDEL with idempotent false, so a lost reply is not retried into a false replay."

requirements-completed: [AG-74-4, AG-74-14]

# Phase 74 plan 10 summary

Public dispatch coordination seams: a device-id signal hint, single-use hello nonces, and presence that fails open as Unknown.

## Tests

- `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Coordination.Tests -c Release -- --filter-class "*Registration*"` passed, 2 tests.
- `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Coordination.Tests -c Release -- --filter-class "*InMemoryDispatch*"` passed, 5 tests.
- `source "$(bash scripts/test-services.sh up)" && /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Coordination.Tests -c Release -- --filter-class "*DragonflyDispatch*"` passed, 4 tests. `bash scripts/test-services.sh down` ran afterwards.

## Gaps

None in this plan. `ITenantHasher` is a constructor dependency of `DragonflyDispatchPresence` and is not registered by `AddDragonflyCoordination`; the composition root that already registers the hasher must do so before the presence singleton is resolved.
---
