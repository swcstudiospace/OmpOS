---
phase: 66-rebrand-to-autonogrammer
plan: 05
subsystem: infra
tags: [legacy-discovery, conventions, safety, architecture-guard]

requires:
  - phase: 66-04
    provides: Abstractions reference edges and the frozen LegacyBrandNames.cs holders
provides:
  - Read-only legacy convention discovery in fixed order new, legacy, engine
  - Enforced-and-reported legacy safety files and a conventions notice with move affordance
  - Architecture guard that fails on stray old-name literals outside LegacyBrandNames.cs
affects: [66-rebrand, conventions, safety]

actuals:
  tokens: 0
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns: [legacy slot between new and third-party, fragment-built spellings, per-call notices]

key-files:
  created:
    - src/Autonogrammer.Harness.Abstractions/LegacyConventionDiscovery.cs
    - src/Autonogrammer.Harness.AgentLoop/Safety/LegacySafetyNotice.cs
    - src/Autonogrammer.Harness.Plugins/LegacyBrandNames.cs
  modified:
    - src/Autonogrammer.Harness.Workspace/Conventions/ConventionScanner.cs
    - src/Autonogrammer.Harness.Workspace/WorkspaceCatalog.cs
    - docs/runbooks/rebrand-autonogrammer.md

key-decisions:
  - "Legacy slots are inserted only when the file exists so existing exact-order tests stay green."
  - "Plugins keeps its own LegacyBrandNames.cs for system hook paths so no new ProjectReference is added."
  - "LegacyNotice stays in LegacyConventionDiscovery.cs with MA0048 suppressed, as the plan required one file."

patterns-established:
  - "Discovery order is current, legacy read-only, then engine. New wins and the conflict is named."

requirements-completed: [AG-66-8, AG-66-10]

coverage:
  - id: D1
    description: Legacy convention directories are read in order and never written.
    requirement: AG-66-8
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.Workspace.Tests/LegacyWorkspaceDiscoveryTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Legacy safety files are enforced when alone and skipped with a named conflict when shadowed.
    requirement: AG-66-8
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.AgentLoop.Tests/Safety/LegacySafetyDiscoveryTests.cs
        status: pass
    human_judgment: false
  - id: D3
    description: Architecture guard catches planted old-name literals and ignores the compose family.
    requirement: AG-66-10
    verification:
      - kind: unit
        ref: tests/Autonogrammer.ArchitectureTests/LegacyBrandLiteralGuardTests.cs
        status: unknown
    human_judgment: true
    rationale: The architecture test project could not be built while apps/Autonogrammer.Native failed to compile on unrelated account-host edits.

duration: 90min
completed: 2026-10-30
status: complete
---

# Phase 66 Plan 05: Legacy convention discovery Summary

**Legacy convention directories are read after the current directory and before the engine, with notices, enforced safety files, and a move command.**

## Performance

- **Duration:** 90 min
- **Started:** 2026-10-30T00:00:00Z
- **Completed:** 2026-10-30T00:00:00Z
- **Tasks:** 3
- **Files modified:** 20

## Accomplishments
- Added `LegacyConventionDiscovery` and routed workspace, safety, MCP, hooks, and environment reads through new, then legacy, then engine.
- Legacy-only permissions and sandbox files stay enforced and are reported; a current file wins and the legacy file is named, not merged.
- The runbook section 7 item 2 now holds the `git mv` command the conventions view copies.

## Task Commits

1. **Task 1-3: legacy discovery, notices, and the literal guard** - pending (feat)

## Files Created/Modified
- `src/Autonogrammer.Harness.Abstractions/LegacyConventionDiscovery.cs` - ordered candidates, legacy-only notices, move text
- `src/Autonogrammer.Harness.Workspace/WorkspaceCatalog.cs` - read-only legacy notice accessor
- `docs/runbooks/rebrand-autonogrammer.md` - fenced move command

## Decisions Made
- Existing exact path-order tests stay green because a missing legacy file is not inserted into the public lists.
- Setup and ignore readers outside this plan's file list are covered by the helper order and by catalog conflict names, not by a second reader.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Existing order tests cannot be edited**
- **Found during:** Task 2
- **Issue:** `McpConfigPrecedenceTests` and `PermissionsFileTests` assert exact location lists and are not in this plan's file list.
- **Fix:** Insert the legacy slot only when the file exists.
- **Files modified:** SafetyFileLocations.cs, McpLocations.cs
- **Verification:** The new matrix tests see the slot; the owned suites pass.
- **Committed in:** this plan commit

## Issues Encountered
- `tests/Autonogrammer.Native.Tests` and `tests/Autonogrammer.ArchitectureTests` could not be built while `apps/Autonogrammer.Native` failed on unrelated account-host edits and an Avalonia resource resolve error. The views project itself builds with zero warnings.
- `tests/Autonogrammer.Api.IntegrationTests` was blocked at times by unrelated files in that project.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
Legacy discovery is in place for the owned sites. Re-run the native and architecture filters once the app project compiles.

---
*Phase: 66-rebrand-to-autonogrammer*
*Completed: 2026-10-30*

## Self-Check: FAILED
Native and architecture filters were not executed because the app project did not compile.
