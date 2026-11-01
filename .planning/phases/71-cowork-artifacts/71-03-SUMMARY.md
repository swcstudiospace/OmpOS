---
phase: 71-cowork-artifacts
plan: 03
subsystem: database
tags: [artifacts, postgres, file-store, sync, npgsql]

requires:
  - phase: 71-01
    provides: IArtifactStore and ArtifactFailure
  - phase: 68
    provides: M0006 cowork artifact tables and MigrationRunner schema argument
provides:
  - Authoritative desktop FileArtifactStore with write-once version files
  - PostgresArtifactStore over M0006 with account_id filters and no migration
  - Off-by-default ArtifactSyncService and HttpArtifactRemote
  - ArtifactWireContract shared with 71-04
affects: [71-04, 71-06]

actuals:
  tokens: 0
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns:
    - "Desktop versions are CreateNew files under a SHA-256 tenant folder; head is the highest version file"
    - "Postgres schema comes from NpgsqlDataSource SearchPath, never concatenated SQL"
    - "Content over 5242880 bytes throws ArtifactQuotaException whose Failure is ArtifactFailure.QuotaExceeded"

key-files:
  created:
    - src/Autonogrammer.Cowork.Artifacts.Stores/FileArtifactStore.cs
    - src/Autonogrammer.Cowork.Artifacts.Stores/PostgresArtifactStore.cs
    - src/Autonogrammer.Cowork.Artifacts.Stores/ArtifactSyncService.cs
    - src/Autonogrammer.Cowork.Artifacts.Stores/HttpArtifactRemote.cs
    - src/Autonogrammer.Cowork.Artifacts.Stores/ArtifactWireContract.cs
    - src/Autonogrammer.Cowork.Artifacts.Stores/ArtifactQuotaException.cs
  modified:
    - src/Autonogrammer.Cowork.Artifacts.Stores/Autonogrammer.Cowork.Artifacts.Stores.csproj
    - tests/Autonogrammer.Cowork.Artifacts.Stores.Tests/packages.lock.json

key-decisions:
  - "Grant persistence stays internal and is visible to Autonogrammer.Api through InternalsVisibleTo, because a public nested Grant fails CA1034"
  - "The 5242880-byte store cap throws ArtifactQuotaException, not InvalidOperationException"

patterns-established:
  - "Sync default is off; SyncOffText is the string 71-06 shows"
  - "409 divergence forks the desktop copy to a new artifact and does not merge"

requirements-completed: [AG-71-4, AG-71-9, AG-71-19]

coverage:
  - id: D1
    description: Desktop file store writes version files atomically, isolates accounts, and refuses tampered bytes
    requirement: AG-71-4
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Cowork.Artifacts.Stores.Tests/FileArtifactStoreTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Postgres store round-trips M0006 with account isolation and the 5242880 cap
    requirement: AG-71-9
    verification:
      - kind: integration
        ref: tests/Autonogrammer.Cowork.Artifacts.Stores.Tests/PostgresArtifactStoreTests.cs
        status: pass
    human_judgment: false
  - id: D3
    description: Sync is metadata-only until enabled, idempotent by hash, and forks on divergence
    requirement: AG-71-19
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Cowork.Artifacts.Stores.Tests/ArtifactSyncServiceTests.cs
        status: pass
    human_judgment: false

duration: 90min
completed: 2026-10-30
status: complete
---

# Phase 71 Plan 03 Summary

**Desktop file store, M0006 Postgres store, and off-by-default hash-idempotent sync, with a typed quota refusal at 5242880 bytes.**

## Performance

- **Duration:** 90 min
- **Started:** 2026-10-30
- **Completed:** 2026-10-30
- **Tasks:** 3
- **Files modified:** 16

## Accomplishments

- FileArtifactStore writes vNNNN.bin and vNNNN.json once, under a per-account tenant folder, and re-checks hash and parent on read.
- PostgresArtifactStore uses unqualified M0006 names, filters every query by account_id, and adds no migration.
- ArtifactSyncService stays off until enabled, sends metadata only while off, and forks the desktop copy on 409.

## Task Commits

1. **Task 1-3: stores and sync** - this commit (feat)

## Files Created/Modified

- `src/Autonogrammer.Cowork.Artifacts.Stores/FileArtifactStore.cs` - authoritative desktop store
- `src/Autonogrammer.Cowork.Artifacts.Stores/PostgresArtifactStore.cs` - M0006 account store
- `src/Autonogrammer.Cowork.Artifacts.Stores/ArtifactSyncService.cs` - off-by-default sync
- `src/Autonogrammer.Cowork.Artifacts.Stores/HttpArtifactRemote.cs` - sole network path
- `src/Autonogrammer.Cowork.Artifacts.Stores/ArtifactWireContract.cs` - client and server wire constants
- `src/Autonogrammer.Cowork.Artifacts.Stores/ArtifactQuotaException.cs` - typed quota refusal
- `src/Autonogrammer.Cowork.Artifacts.Stores/StoresServiceCollectionExtensions.cs` - host-overridable registration

## Decisions Made

- Grant methods stay internal. `InternalsVisibleTo` names `Autonogrammer.Api` so 71-04 can call them without a public nested `Grant` (CA1034).
- Content over 5242880 bytes throws `ArtifactQuotaException` whose `Failure` is `ArtifactFailure.QuotaExceeded`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Typed quota exception**
- **Found during:** Task 2
- **Issue:** The store threw `InvalidOperationException` with the code string.
- **Fix:** `ArtifactQuotaException.Failure` is `ArtifactFailure.QuotaExceeded`.
- **Files modified:** ArtifactQuotaException.cs, both stores, PostgresArtifactStoreTests.cs
- **Verification:** Postgres cap test asserts the failure value.
- **Committed in:** this commit

**2. [Rule 3 - Blocking] InternalsVisibleTo for the API**
- **Found during:** Task 2
- **Issue:** 71-04 grant routes need `SaveGrantAsync` / `GrantsAsync`, and making `Grant` public nested fails CA1034.
- **Fix:** InternalsVisibleTo `Autonogrammer.Api`. No new project reference.
- **Files modified:** Autonogrammer.Cowork.Artifacts.Stores.csproj
- **Verification:** Stores Release build, 0 warnings.
- **Committed in:** this commit

---

**Total deviations:** 2 auto-fixed
**Impact on plan:** Both are required for the typed cap and for 71-04 to call grant methods.

## Issues Encountered

None in the stores suite. Sibling projects were mid-edit, so verification built this project with existing dependency assemblies.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- 71-04 can consume `ArtifactWireContract` and the internal grant methods.
- `ArtifactSyncService.SyncOffText` is `sync is off` for 71-06.
- This plan adds no migration and does not edit ContractVersion.

---
*Phase: 71-cowork-artifacts*
*Completed: 2026-10-30*
