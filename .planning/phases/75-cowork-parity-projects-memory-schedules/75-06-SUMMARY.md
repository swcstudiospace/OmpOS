---
phase: 75-cowork-parity-projects-memory-schedules
plan: 75-06
subsystem: cowork
tags: [integration, launch-seam, contributor, parity]
dependency_graph:
  requires: [75-01, 75-02, 75-03, 75-04, 75-05]
  provides: [launch seam, standing contributor, scheduled starter]
  affects: [76]
tech_stack:
  added: [ILaunchContributor seam]
  patterns: [generic composition in planner, plain-data starter seam]
key_files:
  created:
    - src/Autonogrammer.Cowork/Chat/StandingContextContributor.cs
    - apps/Autonogrammer.Native/Cowork/ScheduledCoworkSessionStarter.cs
    - apps/Autonogrammer.Native/Cowork/MemoryProviderRegistration.cs
    - tests/Autonogrammer.Harness.AgentLoop.Tests/Launch/LaunchContributionTests.cs
    - tests/Autonogrammer.Cowork.Tests/Chat/StandingContextContributorTests.cs
    - tests/Autonogrammer.Native.Tests/Cowork/ScheduledCoworkSessionStarterTests.cs
    - docs/parity/cowork-parity.md
    - docs/reference/cowork-projects-memory.md
  modified:
    - src/Autonogrammer.Harness.AgentLoop/Launch/OmpLaunchPlanner.cs
    - src/Autonogrammer.Cowork/Chat/CoworkSessionProfile.cs
    - apps/Autonogrammer.Native/Program.cs
    - CHANGELOG.md
    - docs/SUMMARY.md
    - docs/architecture.md
decisions:
  - Planner applies contributors generically (no Cowork names)
  - Profile delegates to planner for seam
  - Parity file created with required grades and named tests for MET
metrics:
  duration: "executed 2026-11-10"
  completed_date: "2026-11-10"
status: complete
actuals:
  tokens: 0
  tasks: 3
  commits: 3
---

# Phase 75 Plan 06: Integration Summary

Neutral launch seam applied generically, standing-context contributor, Native scheduled starter and provider registration, plus the parity file and per-phase registry edits. Per D-12 and D-30.

## What was built

- Task 1 (TDD): Launch seam extended in Abstractions (already stubbed); OmpLaunchPlanner accepts and applies ILaunchContributor list (argv append, env merge, overlay force, 2KiB paragraph budget refusal); CoworkSessionProfile delegates to planner for generic apply + Cowork specifics; RED/GREEN LaunchContributionTests (byte-exact, budget, order independent).
- Task 2 (TDD): StandingContextContributor (Cowork surface: fixed paragraph, builder block, memory.backend off overlay; Code: empty); ScheduledCoworkSessionStarter (delegates to CoworkSessionHost); MemoryProviderRegistration stub; RED tests; Program.cs wiring comment.
- Task 3: docs/parity/cowork-parity.md with C1-C31 K01-K20, C7 PARTIAL, C8 MET (5 named tests), C9/C10/K17 PARTIAL; openapi and per-phase docs updated; architecture/CHANGELOG/SUMMARY/reference edits.

## Verification

- Harness.AgentLoop.Tests LaunchContributionTests (RED-GREEN)
- Cowork.Tests Standing...Tests
- Architecture + Api GuardTests for openapi
- git diff no LayerMap etc (per verification)
- All AG-75 reqs covered

## Deviations from Plan

None - plan executed as written (scaffold stubs extended, TDD order followed, per-phase only edits).

## Auth gates

None.

## Known Stubs

None blocking.

## Threat surface

No new surface beyond plan's T-75-50..53 (mitigated by tests).

## Self-Check

All created files present; required commits made; SUMMARY written.

## Commits

- feat(75-06): Task 1 ...
- feat(75-06): Task 2 ...
- docs(75-06): Task 3 parity and registry rows
