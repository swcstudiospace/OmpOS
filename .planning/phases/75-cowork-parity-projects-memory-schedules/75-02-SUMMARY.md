---
phase: 75-cowork-parity-projects-memory-schedules
plan: 75-02
subsystem: database
tags: [memory, postgres, policy, cowork]

requires:
  - phase: 68-data-plane-names-schema-and-resilience
    provides: M0009 cowork_memory_entries GIN expression and ON DELETE CASCADE
  - phase: 70-cowork-chat-core-and-tool-host
    provides: ICoworkToolProvider and ADR-0032 D5 descriptor rule
provides:
  - Policy-gated IMemoryStore with in-memory, HTTP and Postgres implementations
  - MemoryMode in Contracts and the memory tool provider
  - 4 KiB untrusted memory brief
affects: [75-04, 75-05, 75-06]

actuals:
  tokens: 0
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns:
    - "MemoryContentPolicy runs before every write and raises a notice"
    - "Search SQL uses the M0009 to_tsvector simple expression"

key-files:
  created:
    - src/Autonogrammer.Cowork.Memory/MemoryContentPolicy.cs
    - src/Autonogrammer.Cowork.Memory/MemoryToolProvider.cs
    - src/Autonogrammer.Cowork.Memory.Postgres/PostgresMemoryStore.cs
  modified: []

key-decisions:
  - "MemoryMode lives only in Contracts/Cowork/Memory so Cowork and Cowork.Memory share it without an L2-L2 edge"
  - "HttpMemoryStore is the desktop client over /cowork/memory; the loopback in the contract tests is not the API"

patterns-established:
  - "Pattern: every store filters by account_id and refuses before write"
  - "Pattern: brief text is fenced as information saved earlier, not instructions"

requirements-completed: [AG-75-4, AG-75-5, AG-75-6, AG-75-7]

coverage:
  - id: D1
    description: Never-saved and sensitive classes are refused before a write, with a sentence and named caps
    requirement: AG-75-5
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Cowork.Memory.Tests/MemoryContentPolicyTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Scope, pause, reset, delete, export and caps hold for the in-memory and HTTP stores
    requirement: AG-75-4
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Cowork.Memory.Tests/MemoryStoreContractTests.cs
        status: pass
    human_judgment: false
  - id: D3
    description: Postgres isolation, cascade, GIN EXPLAIN and the log canary
    requirement: AG-75-6
    verification:
      - kind: integration
        ref: tests/Autonogrammer.Cowork.Memory.Postgres.Tests/PostgresMemoryStoreTests.cs
        status: pass
    human_judgment: false
  - id: D4
    description: memory_recall, memory_save and memory_forget follow the ADR-0032 name rule and unavailable reasons
    requirement: AG-75-7
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Cowork.Memory.Tests/MemoryToolProviderTests.cs
        status: pass
    human_judgment: false

duration: 90min
completed: 2026-10-30
status: complete
---

# Phase 75 Plan 02: Account memory stores and tool provider Summary

**Policy-gated memory stores, a 4 KiB untrusted brief, and the memory tool provider with Manual/Auto asks via ByPolicy.**

## Performance

- **Duration:** 90 min
- **Started:** 2026-10-30
- **Completed:** 2026-10-30
- **Tasks:** 3
- **Files modified:** 20

## Accomplishments

- MemoryContentPolicy refuses government IDs, criminal history, financial numbers and immigration status always, and health, race, religion, politics and gender identity unless include_sensitive is on.
- InMemoryMemoryStore, HttpMemoryStore and PostgresMemoryStore share IMemoryStore: scope, 1,000-entry and 20-per-hour caps, pause, reset, delete and JSON export.
- MemoryToolProvider offers memory_recall, memory_save and memory_forget. GetStateAsync names signed_out, offline, memory_disabled, memory_off_for_task, memory_paused and ToolHostUnavailable. The save preview marks the text agent-proposed.

## Task Commits

1. **Task 1-3: memory policy, stores and tool provider** - this commit (feat)

## Files Created/Modified

- `src/Autonogrammer.Contracts/Cowork/Memory/MemoryMode.cs` - On/Off, the only home of the enum
- `src/Autonogrammer.Cowork.Memory/MemoryContentPolicy.cs` - Luhn, IBAN mod-97, national-id and lexicon detectors
- `src/Autonogrammer.Cowork.Memory.Postgres/PostgresMemoryStore.cs` - DML-only store; search expression matches M0009
- `src/Autonogrammer.Cowork.Memory/MemoryToolProvider.cs` - the only agent path

## Decisions Made

- MemoryMode stays in Contracts so both layers consume it without an L2-L2 reference.
- The HTTP contract tests use a loopback over the same routes 75-04 will map. This plan does not add the API.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Test projects reference the migration runner and the shared contract**

- **Found during:** Task 2
- **Issue:** Live tests must apply M0009 through MigrationRunner, and the Postgres project must inherit the shared contract. Those references are not product L2-L2 edges.
- **Fix:** Added test ProjectReferences and refreshed the lock files. No package and no migration were added.
- **Files modified:** the two memory test csproj files and their lock files
- **Verification:** Postgres tests applied M0009 and passed, including EXPLAIN
- **Committed in:** this commit

**Total deviations:** 1 auto-fixed
**Impact on plan:** Required for the live runner and the shared contract. No product layer change.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- 75-04 can bind Cowork:Memory and the Contracts/Cowork/Memory DTOs on `/cowork/memory`.
- The brief string is ready for the standing-context builder. This plan does not inject it.

---
*Phase: 75-cowork-parity-projects-memory-schedules*
*Completed: 2026-10-30*
