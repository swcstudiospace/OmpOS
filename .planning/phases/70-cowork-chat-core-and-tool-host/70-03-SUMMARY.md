---
phase: 70-cowork-chat-core-and-tool-host
plan: 70-03
subsystem: cowork
tags: [permissions, omp-launch, path-boundary, overlay]

requires:
  - phase: 70-01
    provides: Cowork project and session stores
provides:
  - CoworkSessionProfile launch flags, provider-key environment and cowork MCP entry
  - CoworkSystemPrompt budgets and owner-only prompt file
  - CoworkPathBoundary and CoworkPermissionResolver
  - ConfigOverlayWriter.WriteYaml
affects: [70-04, 76-permissions]

actuals:
  tokens: 18000
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns: [owner-only prompt and overlay, ask-user resolver seam, provider-key name filter]

key-files:
  created:
    - src/Autonogrammer.Cowork/Chat/CoworkSessionProfile.cs
    - src/Autonogrammer.Cowork/Chat/CoworkPermissionResolver.cs
    - src/Autonogrammer.Cowork/Chat/CoworkPathBoundary.cs
  modified:
    - src/Autonogrammer.Harness.AgentLoop/Launch/ConfigOverlayWriter.cs
    - src/Autonogrammer.Cowork/Autonogrammer.Cowork.csproj

key-decisions:
  - "Reused Tools.CoworkPermissionMode; Chat/CoworkPermissionMode holds the setting key, default and Skip availability."
  - "WriteYaml writes the caller YAML exactly; the profile does not call the private planner overlay helper."
  - "Catalog MCP servers ask in a tainted session even in Skip; mcp__cowork_* stays allowed."

patterns-established:
  - "Cowork launch arguments are built by CoworkSessionProfile, never RunModeLaunchMapper."
  - "A prompt that cannot be written refuses the start with CoworkPromptUnavailableException."

requirements-completed: [AG-70-1, AG-70-2, AG-70-13, AG-70-14]

coverage:
  - id: D1
    description: Manual, Auto and Skip launch with no extensions, the asking approval mode, one add-dir per folder, the absolute prompt path and the overlay path.
    requirement: AG-70-1
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Cowork.Tests/Chat/CoworkSessionProfileTests.cs#EachModeLaunchesWithoutExtensionsAndNeverYolo
        status: pass
    human_judgment: false
  - id: D2
    description: The embedded prompt names the six rules and stays inside the locked budgets, written owner-only.
    requirement: AG-70-1
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Cowork.Tests/Chat/CoworkSystemPromptTests.cs#TheBaseNamesTheSixRulesAndStaysInsideFourKibibytes
        status: pass
    human_judgment: false
  - id: D3
    description: WriteYaml writes owner-only caller YAML and deletes it on dispose, without an mcp or workspace block.
    requirement: AG-70-1
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.AgentLoop.Tests/Launch/ConfigOverlayWriterTests.cs#WriteYamlWritesExactlyTheCallersYamlAndDeletesItOnDispose
        status: pass
    human_judgment: false
  - id: D4
    description: The resolver denies outside folders, always asks before delete, allows mcp__cowork_*, and follows Manual, Auto and Skip including taint.
    requirement: AG-70-2
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Cowork.Tests/Chat/CoworkPermissionResolverTests.cs
        status: pass
    human_judgment: false
  - id: D5
    description: The boundary refuses secrets, omp, ssh and account folders, including symlink and dot-dot escapes, plus UNC and drive roots.
    requirement: AG-70-14
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Cowork.Tests/Chat/CoworkPathBoundaryTests.cs
        status: pass
    human_judgment: false
  - id: D6
    description: The profile environment keeps only AUTONOGRAMMER_PROVIDER_*_KEY names and refuses Substrate, hook, API and account tokens.
    requirement: AG-70-14
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Cowork.Tests/Chat/CoworkSessionProfileTests.cs#TheEnvironmentKeepsProviderKeysAndRefusesEverythingElse
        status: pass
    human_judgment: false
  - id: D7
    description: A tainted session asks for catalog MCP calls even in Skip, and treats edit, delete, move and execute as Manual.
    requirement: AG-70-13
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Cowork.Tests/Chat/CoworkPermissionResolverTests.cs#CoworkToolsAreAllowedAndCatalogMcpAsksWhenTainted
        status: pass
    human_judgment: false

duration: 40min
completed: 2026-10-30
status: complete
---

# Phase 70 Plan 03: Cowork profile and permissions Summary

**Cowork launch flags, owner-only prompt and overlay, folder boundary, and the permission resolver including taint**

## Performance

- **Duration:** 40 min
- **Started:** 2026-10-30T00:00:00Z
- **Completed:** 2026-10-30T00:40:00Z
- **Tasks:** 3
- **Files modified:** 16

## Accomplishments
- CoworkSessionProfile emits `--no-extensions`, an asking approval mode, one `--add-dir` per folder, the absolute prompt path and `--config`, plus a cowork MCP entry and provider keys only.
- CoworkSystemPrompt writes the embedded six-rule prompt owner-only and refuses a start it cannot write, with section and total budgets.
- CoworkPathBoundary and CoworkPermissionResolver deny escapes and outside paths, always ask before delete, allow `mcp__cowork_*`, and demote Skip when the session is tainted or unacknowledged.
- ConfigOverlayWriter.WriteYaml writes the caller's YAML exactly and deletes it on dispose.

## Task Commits

1. **Task 1-3: profile, boundary, resolver, environment** - see commit (feat)

## Files Created/Modified
- `src/Autonogrammer.Cowork/Chat/CoworkSessionProfile.cs` - launch plan and provider-key filter
- `src/Autonogrammer.Cowork/Chat/CoworkSystemPrompt.cs` - embedded prompt writer and budgets
- `src/Autonogrammer.Cowork/Chat/cowork-system-prompt.md` - six-rule base prompt
- `src/Autonogrammer.Cowork/Chat/CoworkPermissionResolver.cs` - mode, delete, folder and taint decisions
- `src/Autonogrammer.Cowork/Chat/CoworkPathBoundary.cs` - secrets, omp, ssh, account, symlink and dot-dot refusals
- `src/Autonogrammer.Harness.AgentLoop/Launch/ConfigOverlayWriter.cs` - WriteYaml
- `src/Autonogrammer.Cowork/Autonogrammer.Cowork.csproj` - EmbeddedResource item

## Decisions Made
- The existing `Tools.CoworkPermissionMode` enum is the value type. `Chat/CoworkPermissionMode` stores `cowork.permissionMode`, the Manual default, the approval-mode argument and the Skip availability rule.
- Auto follows Manual until the ADR-0037 rule engine exists. Skip allows in-folder reads and searches, still asks for deletes, and asks for edit, delete, move, execute and catalog MCP when tainted.
- The profile carries no catalog servers. An empty MCP list is used when no gateway URL and bearer are supplied.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Exception types live in their own files**
- **Found during:** Task 1
- **Issue:** A second public type in the profile and prompt files failed MA0048, and CA1032 required the standard constructors.
- **Fix:** Added `CoworkEnvironmentRefusedException` and `CoworkPromptUnavailableException` under `Chat/`.
- **Files modified:** those two files
- **Verification:** targeted tests compile and pass
- **Committed in:** the plan commit

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** File split required by the analyzer. No behaviour change.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
70-04 can compose `CoworkSessionProfile`, pass `ConfigOverlayWriter.WriteYaml` the overlay constant, and install `CoworkPermissionResolver` through `PermissionResolverFactory`.

---
*Phase: 70-cowork-chat-core-and-tool-host*
*Completed: 2026-10-30*
