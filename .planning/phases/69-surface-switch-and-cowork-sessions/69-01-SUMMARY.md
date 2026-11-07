---
phase: 69-surface-switch-and-cowork-sessions
plan: 69-01
subsystem: cowork
tags: [sessions, json, sync, account-folder]

requires:
  - phase: 66.1
    provides: Autonogrammer.Cowork and Cowork.Tests scaffolds
provides:
  - Cowork wire types and source-generated JSON context
  - Per-account Cowork registry wrapping AgentSessionRegistry
  - session-meta.json and projects.json stores
  - Debounced CoworkSessionSync over ICoworkSessionApi
affects: [69-03, 69-04, 69-05, 70]

actuals:
  tokens: 14000
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns:
    - CoworkSessionRegistry wraps AgentSessionRegistry at cowork/<account>
    - ICoworkAccountGate supplies the folder key and never the subject
    - Account stores keep unknown JSON members and refuse oversized files

key-files:
  created:
    - src/Autonogrammer.Contracts/Cowork/CoworkSessionSummary.cs
    - src/Autonogrammer.Cowork/Sessions/CoworkSessionRegistry.cs
    - src/Autonogrammer.Cowork/Sync/CoworkSessionSync.cs
  modified: []

key-decisions:
  - "Store size cap is 256 KiB; the plan stated a cap but no number."
  - "Offline retry starts at 1s and doubles to a 60s cap."
  - "Extra types are separate files so MA0048 holds."

patterns-established:
  - "Pattern: Cowork files live only under <stateRoot>/cowork/<account>."
  - "Pattern: ICoworkSessionCatalog is not implemented in this library."

requirements-completed: [AG-69-6, AG-69-7, AG-69-11]

coverage:
  - id: D1
    description: Cowork contracts stay off the Code catalog and refuse an unknown status string.
    requirement: AG-69-7
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Cowork.Tests/CatalogSeparationTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Per-account registry, 200-task message, Cowork-only recovery and quarantine banner.
    requirement: AG-69-6
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Cowork.Tests/CoworkSessionRegistryTests.cs
        status: pass
    human_judgment: false
  - id: D3
    description: Meta and project stores are atomic, owner-only, keep unknown members and enforce the size cap.
    requirement: AG-69-6
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Cowork.Tests/CoworkSessionMetaStoreTests.cs
        status: pass
    human_judgment: false
  - id: D4
    description: Sync debounces 5s, keeps the latest offline summary, caps backoff and marks 409 Not synced.
    requirement: AG-69-11
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Cowork.Tests/CoworkSessionSyncTests.cs
        status: pass
    human_judgment: false

duration: 45min
completed: 2026-10-30
status: complete
---

# Phase 69: Surface switch and Cowork sessions Summary

**Cowork sessions, metadata and project files live under a hashed account folder, with a 5s summary sync that never writes the Code registry.**

## Performance

- **Duration:** 45 min
- **Started:** 2026-10-30T00:00:00Z
- **Completed:** 2026-10-30T00:45:00Z
- **Tasks:** 3
- **Files modified:** 28

## Accomplishments

- Cowork wire types (`CoworkSessionSummary`, `CoworkSessionStatus`, `ICoworkSessionCatalog`, `CoworkJsonContext`) with a 200-character title and string statuses.
- `CoworkSessionRegistry` wraps `AgentSessionRegistry` at `<stateRoot>/cowork/<account>/sessions.json`. A 201st create throws the exact 200-task message. Recovery marks only that file's Running rows Lost. A corrupt file is kept under the quarantine name in the banner.
- `CoworkSessionMetaStore` and `CoworkProjectStore` write `session-meta.json` and `projects.json` atomically, mode 0600, keeping unknown members, refusing files over 256 KiB.
- `CoworkSessionSync` coalesces a burst into one PUT after 5s, retries offline with backoff capped at 60s, and sets a quiet "Not synced" mark on 409 or offline.

## Task Commits

1. **Task 1-3: contracts, registry, stores and sync** - this commit (`feat(69-01)`)

## Files Created/Modified

- `src/Autonogrammer.Contracts/Cowork/*` - wire types and JSON context
- `src/Autonogrammer.Cowork/Sessions/CoworkSessionRegistry.cs` - wrapped per-account registry
- `src/Autonogrammer.Cowork/CoworkAccountFolder.cs` - SHA-256 base32 folder key
- `src/Autonogrammer.Cowork/Sync/CoworkSessionSync.cs` - debounce and retry
- `tests/Autonogrammer.Cowork.Tests/*` - separation, registry, store and sync tests

## Decisions Made

- Size cap is 256 KiB because the plan required a cap and gave no number.
- Backoff is 1, 2, 4, 8, 16, 32, then 60 seconds.
- `ICoworkSessionCatalog` has no implementation here, matching the amendment that Native and Web own the adapters.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Split types so MA0048 passes**
- **Found during:** Task 2
- **Issue:** One file with two top-level types fails the repo analyzer.
- **Fix:** Extra records and JSON documents are their own files under the owned folders.
- **Files modified:** Sessions and Sync folders, `CoworkJsonFile.cs`, `ManualClock.cs`
- **Verification:** Release build of Autonogrammer.Cowork is warning-free
- **Committed in:** this commit

**Total deviations:** 1 auto-fixed
**Impact on plan:** Analyzer compliance only. No behaviour change.

## Issues Encountered

The official `--project tests/Autonogrammer.Cowork.Tests` form is rejected by this `dotnet test` (invalid extension). The same project was run with the `.csproj` path. Sibling Tools files blocked the test project until they compiled; they were not edited.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- 69-03 can implement `ICoworkAccountGate` and a catalog adapter over `CoworkSessionRegistry`.
- 69-05 can insert `CoworkJsonContext` and call `ICoworkSessionApi`.
- `CoworkSessionSync` is not constructed by a head in this plan.

---
*Phase: 69-surface-switch-and-cowork-sessions*
*Completed: 2026-10-30*
