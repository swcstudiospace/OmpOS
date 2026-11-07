---
phase: 74-dispatch-relay
plan: 74-01
subsystem: contracts
tags: [dispatch, canonical, store, frames]

requires: []
provides:
  - Closed Dispatch frames, enums, REST DTOs and DispatchJsonContext
  - DispatchCanonical plus the shared vector file
  - IDispatchStore, transition table, IJitterSource and InMemoryDispatchStore
affects: [74-02, 74-04, 74-05, 74-07, 74-09]

actuals:
  tokens: 0
  tasks: 3
  commits: []

tech-stack:
  added: []
  patterns:
    - One type per file (MA0048); wire parsers sit beside the enums
    - Source-generated DispatchJsonContext; no reflection fallback
    - Shared store oracle linked into Api.IntegrationTests

key-files:
  created:
    - src/Autonogrammer.Contracts/Dispatch/DispatchCanonical.cs
    - src/Autonogrammer.Contracts/Dispatch/IDispatchStore.cs
    - services/Autonogrammer.Api/Dispatch/Store/InMemoryDispatchStore.cs
    - tests/Autonogrammer.Contracts.Tests/Dispatch/TestData/dispatch-canonical-vectors.json
  modified:
    - tests/Autonogrammer.Api.IntegrationTests/Autonogrammer.Api.IntegrationTests.csproj

key-decisions:
  - "received to awaiting_approval is allowed so the approval_pending state is reachable (ADR-0036 D5). The plan's parenthetical edge list omitted it."
  - "HubMayApply refuses every edge into completed. The store still accepts a device-reported running to completed compare-and-set."
  - "Extra files beyond the plan list exist only because MA0048 requires one type per file. SystemJitterSource uses RandomNumberGenerator so CA5394 stays clear."

requirements-completed: [AG-74-4, AG-74-5, AG-74-6, AG-74-7, AG-74-9]

# Phase 74 plan 01 summary

Closed relay contracts, canonical command bytes, the transition table and an in-memory store that the Postgres store will share.

## Tests

- `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Contracts.Tests -c Release -- --filter-class '*Dispatch*'` passed, 15 tests.
- In-memory store contract: 8 tests passed via the built IntegrationTests host (`--filter-class '*InMemoryDispatchStoreTests'`). The plan's `*Dispatch*Store*` filter is rejected by xunit (wildcard in the middle). A full IntegrationTests rebuild is blocked by sibling Cowork.Abstractions MA0048 and unused usings in account tests.

## Gaps

- `received → awaiting_approval` is an extra allowed edge, required for that state to be reachable.
- Official IntegrationTests command could not rebuild while Cowork.Abstractions is mid-edit. Store tests were run against an API build that did not rebuild that project.
---
