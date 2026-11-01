---
phase: 73-cowork-computer-use
plan: 73-01
subsystem: cowork
tags: [computer-use, contracts, coordinate-map, denial-codes]
requires: []
provides:
  - IComputerUseBackend, IComputerActionGate, denial catalog, options reader and CoordinateMap
affects: [73-02, 73-03, 73-06]
actuals:
  tokens: 28000
  tasks: 3
  commits: 1
tech-stack:
  added: []
  patterns:
    - One public type per file (MA0048)
    - Plain options validator and environment reader, no Microsoft.Extensions
key-files:
  created:
    - src/Autonogrammer.Harness.ComputerUse.Abstractions/IComputerUseBackend.cs
    - src/Autonogrammer.Harness.ComputerUse.Abstractions/IComputerActionGate.cs
    - src/Autonogrammer.Harness.ComputerUse.Abstractions/ComputerUseOptions.cs
    - src/Autonogrammer.Harness.ComputerUse.Abstractions/CoordinateMap.cs
    - src/Autonogrammer.Harness.ComputerUse.Abstractions/DenialCode.cs
    - src/Autonogrammer.Harness.ComputerUse.Abstractions/ComputerUsePorts.cs
  modified: []
key-decisions:
  - "MA0048 forced one top-level type per file; public names stay unqualified."
  - "IComputerUseAccessFacts stays the gate's account seam. Landed ICoworkAccountGate has IsSignedIn and AccountFolderKey only; the head still supplies IsCoworkSession."
patterns-established:
  - "Gate step order is DenialCodeCatalog.GateStepOrder (ADR-0035 D3 / D-06)."
  - "CoordinateMap is the only scale conversion; MidpointRounding.ToEven."
requirements-completed: [AG-73-1, AG-73-3, AG-73-10, AG-73-18]
coverage:
  - id: D1
    description: Backend, gate, ports and availability remediation ids
    requirement: AG-73-1
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.ComputerUse.Tests/Abstractions/DenialCodeCatalogTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Denial codes each have one message id and the D-06 step order
    requirement: AG-73-3
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.ComputerUse.Tests/Abstractions/DenialCodeCatalogTests.cs#EveryDenialCodeHasADistinctMessageId
        status: pass
    human_judgment: false
  - id: D3
    description: Options default off, validator names keys, reader maps Cowork__ComputerUse__ variables
    requirement: AG-73-10
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.ComputerUse.Tests/Abstractions/ComputerUseOptionsValidatorTests.cs
        status: pass
    human_judgment: false
  - id: D4
    description: Wayland and platform-less availability states have remediation ids
    requirement: AG-73-18
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.ComputerUse.Tests/Abstractions/DenialCodeCatalogTests.cs#EveryAvailabilityMapsToOneRemediationId
        status: pass
    human_judgment: false
duration: 45min
completed: 2026-10-30
status: complete
---

# Phase 73 Plan 01 Summary

**Computer Use L1 contracts: backend, gate ticket, denial catalog, off-by-default options and CoordinateMap**

## Performance

- **Duration:** 45 min
- **Started:** 2026-10-30
- **Completed:** 2026-10-30
- **Tasks:** 3
- **Files modified:** contracts and four test classes

## Accomplishments

- `IComputerUseBackend` exposes availability, permissions, displays, bottom-to-top windows, pointer, BGRA capture, input primitives, release-all and clipboard text as `ValueTask` methods with `CancellationToken` last.
- `IComputerActionGate` returns a single-use, hash-bound ticket valid for 2 seconds, or a `DenialCode` plus message id. `DenialCodeCatalog.GateStepOrder` matches D-06.
- `ComputerUseOptions` defaults `Enabled=false`. The plain validator names `Cowork:ComputerUse:` keys and never values. The reader maps `Cowork__ComputerUse__` variables.
- `CoordinateMap` is the only scale conversion (negative origins, two displays, macOS scale 2, round trip within 1 px).
- Wayland and unsupported-platform states use `computer.unavailable.wayland` and `computer.unavailable.platform`.

## Task Commits

1. **Task 1-3: contracts, CoordinateMap, options** - this commit (`feat`)

## Files Created/Modified

- `src/Autonogrammer.Harness.ComputerUse.Abstractions/` - one public type per file
- `tests/Autonogrammer.Harness.ComputerUse.Tests/Abstractions/` - CoordinateMap, validator, reader and catalog tests

## Decisions Made

- Extra files exist because MA0048 rejects multiple top-level types in one file. Names are unchanged.
- `ComputerPermission.cs` was not kept: a type ending in `Permission` fails CA1711. Kinds and statuses are `ComputerPermissionKind` and `ComputerPermissionStatus`.
- `JsonSerializerIsReflectionEnabledByDefault` is already false in `Directory.Build.props`. `ComputerUseJsonContext` is the source-generated context. No reflection serialize calls.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Split types for MA0048**
- **Found during:** Task 1
- **Issue:** The plan grouped several public types per file. Meziantou MA0048 is an error.
- **Fix:** One top-level type per file. `ComputerUsePorts` remains the port index.
- **Files modified:** Abstractions sources
- **Verification:** Abstractions Release build, 0 warnings
- **Committed in:** this commit

**Total deviations:** 1 auto-fixed
**Impact on plan:** Required to compile. No behaviour change.

## Issues Encountered

- Test build initially failed on a sibling `IDispatchStore` CA1716. That parameter was renamed outside this plan. Tests then passed.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- 73-02 can compile the gate against these types.
- Re-read `ICoworkAccountGate` (now `IsSignedIn` plus `AccountFolderKey` only). `IComputerUseAccessFacts.IsCoworkSession` is still the head's job. Do not add a ProjectReference from Abstractions to Cowork.
- Do not drive a live desktop from this unit.

---
*Phase: 73-cowork-computer-use*
*Completed: 2026-10-30*
