---
phase: 69-surface-switch-and-cowork-sessions
plan: 69-06
subsystem: testing
tags: [architecture-tests, archunit, reflection, il-scan, kanban, cowork, isolation, docs]

requires:
  - phase: 69-01
    provides: CoworkSessionRegistry, ICoworkSessionCatalog, CoworkSessionSummary
  - phase: 69-02
    provides: IDockPanel and the Cowork presentation models
  - phase: 69-03
    provides: SurfaceHost and the Native shell that hosts the Code and Cowork surfaces
  - phase: 69-05
    provides: Api.Cowork routes and CodeBoardSource
provides:
  - Five build-breaking Cowork isolation rules (ADR-0032 D2), each with a negative control
  - Behavioural Kanban gates: three Code cards stay three across Cowork churn, and the two files, caps and recovery passes are independent
  - Per-phase prose for architecture, the Unreleased changelog and the accessibility focus note
affects: [70, 74, 75]

actuals:
  tokens: 29549
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - Architecture rules that scan compiled IL decode each instruction by operand size and resolve call targets by name
    - Every isolation rule is paired with a fixture that must violate it, so an empty selector cannot pass silently

key-files:
  created:
    - tests/Autonogrammer.ArchitectureTests/CoworkSurfaceIsolationTests.cs
    - tests/Autonogrammer.ArchitectureTests/CoworkFixtures/SessionRegistry.cs
    - tests/Autonogrammer.ArchitectureTests/CoworkFixtures/CoworkRegistryConsumer.cs
    - tests/Autonogrammer.Native.Tests/Cowork/CoworkKanbanIsolationTests.cs
  modified:
    - docs/architecture.md
    - CHANGELOG.md
    - docs/accessibility/README.md

key-decisions:
  - "The construction-site rule allows the AgentHead build path (BuildAsync and BuildModelsAsync, with their state machines and lambdas) and the WebWorkbench constructor. KanbanBoardModel is built in BuildModelsAsync at HEAD, so the name Build is matched by its family."
  - "The Cowork rule covers the namespace subtree Autonogrammer.Cowork (which includes Cowork.Artifacts and Cowork.Memory) as well as Contracts.Cowork and Presentation.Cowork, so a Kanban type cannot reach any Cowork namespace."
  - "Closing a Cowork task in the Kanban test is SetStatus(Completed), because CoworkSessionRegistry exposes no Close."
  - "The architecture rules are verified by negative controls, not by editing production code. No production file was changed."

patterns-established:
  - "Pattern: a pure checker takes any type list, so a fixture type can be fed to it and the rule must report it."
  - "Pattern: the Cowork account folder key comes from CoworkAccountFolder.KeyFromSubject; a raw subject is rejected as a folder name."

requirements-completed: [AG-69-10]

coverage:
  - id: D1
    description: No type implements both ICoworkSessionCatalog and ISessionCatalog
    requirement: AG-69-10
    verification:
      - kind: unit
        ref: tests/Autonogrammer.ArchitectureTests/CoworkSurfaceIsolationTests.cs#NoTypeImplementsBothCatalogInterfaces
        status: pass
      - kind: unit
        ref: tests/Autonogrammer.ArchitectureTests/CoworkSurfaceIsolationTests.cs#DualCatalogFixtureIsReported
        status: pass
    human_judgment: false
  - id: D2
    description: Presentation.Kanban and Harness.AgentLoop.Sessions reference no Cowork namespace
    requirement: AG-69-10
    verification:
      - kind: unit
        ref: tests/Autonogrammer.ArchitectureTests/CoworkSurfaceIsolationTests.cs#KanbanAndAgentLoopSessionsNeverReferenceCowork
        status: pass
      - kind: unit
        ref: tests/Autonogrammer.ArchitectureTests/CoworkSurfaceIsolationTests.cs#KanbanConsumerOfCoworkRegistryIsReported
        status: pass
    human_judgment: false
  - id: D3
    description: KanbanBoardModel is constructed only on the AgentHead build path and in the WebWorkbench constructor
    requirement: AG-69-10
    verification:
      - kind: unit
        ref: tests/Autonogrammer.ArchitectureTests/CoworkSurfaceIsolationTests.cs#KanbanBoardModelIsConstructedOnlyInTheHeadBuildPaths
        status: pass
      - kind: unit
        ref: tests/Autonogrammer.ArchitectureTests/CoworkSurfaceIsolationTests.cs#KanbanConstructionOutsideTheBuildPathIsReported
        status: pass
    human_judgment: false
  - id: D4
    description: ISessionRepository listings of Session take a SessionSurface; GetAsync and ListSessionIdsByStatusAsync are exempt
    requirement: AG-69-10
    verification:
      - kind: unit
        ref: tests/Autonogrammer.ArchitectureTests/CoworkSurfaceIsolationTests.cs#RepositoryListingsNameTheSurface
        status: pass
      - kind: unit
        ref: tests/Autonogrammer.ArchitectureTests/CoworkSurfaceIsolationTests.cs#UnfilteredRepositoryListingFixtureIsReported
        status: pass
    human_judgment: false
  - id: D5
    description: Api.Cowork uses neither SessionRegistry nor CatalogProjector (by type name)
    requirement: AG-69-10
    verification:
      - kind: unit
        ref: tests/Autonogrammer.ArchitectureTests/CoworkSurfaceIsolationTests.cs#ApiCoworkUsesNeitherSessionRegistryNorCatalogProjector
        status: pass
      - kind: unit
        ref: tests/Autonogrammer.ArchitectureTests/CoworkSurfaceIsolationTests.cs#ApiCoworkConsumerOfSessionRegistryIsReported
        status: pass
    human_judgment: false
  - id: D6
    description: A head with three Code sessions keeps exactly three cards across the create, update and close of Cowork tasks
    requirement: AG-69-10
    verification:
      - kind: integration
        ref: tests/Autonogrammer.Native.Tests/Cowork/CoworkKanbanIsolationTests.cs#ACoworkTaskNeverChangesTheBoardOfThreeCodeSessions
        status: pass
    human_judgment: false
  - id: D7
    description: The Code file and the Cowork file are distinct; an automation run never lands in the Cowork registry
    requirement: AG-69-7
    verification:
      - kind: integration
        ref: tests/Autonogrammer.Native.Tests/Cowork/CoworkKanbanIsolationTests.cs#TheCoworkFileIsNotTheCodeFileAndNeitherHoldsTheOther
        status: pass
      - kind: integration
        ref: tests/Autonogrammer.Native.Tests/Cowork/CoworkKanbanIsolationTests.cs#AnAutomationRunNeverLandsInTheCoworkRegistry
        status: pass
    human_judgment: false
  - id: D8
    description: The two caps and the two RecoverAfterRestart passes are independent in both directions
    requirement: AG-69-10
    verification:
      - kind: integration
        ref: tests/Autonogrammer.Native.Tests/Cowork/CoworkKanbanIsolationTests.cs#AFullCodeRegistryLeavesCoworkWritable
        status: pass
      - kind: integration
        ref: tests/Autonogrammer.Native.Tests/Cowork/CoworkKanbanIsolationTests.cs#AFullCoworkRegistryLeavesCodeWritable
        status: pass
      - kind: integration
        ref: tests/Autonogrammer.Native.Tests/Cowork/CoworkKanbanIsolationTests.cs#EachRecoverAfterRestartPassTouchesOnlyItsOwnRows
        status: pass
    human_judgment: false
  - id: D9
    description: Per-phase prose (architecture paragraph, Unreleased changelog entry, accessibility focus sentence) passes the docs and changelog gates
    verification:
      - kind: other
        ref: bash scripts/ci/check-changelog.sh
        status: pass
      - kind: other
        ref: bash scripts/ci/check-docs.sh
        status: pass
    human_judgment: false

duration: 75min
completed: 2026-11-02
status: complete
---

# Phase 69 Plan 06: Cowork isolation gates and phase prose Summary

**The Cowork Kanban rule now fails the build: five architecture rules and a behavioural board test hold it, and the phase is recorded in architecture, the changelog and the accessibility notes.**

## Performance

- **Duration:** about 75 min (most of it in dotnet builds on a shared runner)
- **Started:** 2026-11-02 (plan start at HEAD 9e3d42d9)
- **Completed:** 2026-11-02T06:16:41Z
- **Tasks:** 3 of 3
- **Files modified:** 7 (4 created, 3 modified), plus this summary

## Accomplishments

- `CoworkSurfaceIsolationTests` (ArchitectureTests) asserts the five D-31 rules over the built product assemblies. Each rule has a negative control, a fixture that violates it, and the test asserts the rule reports it.
- `CoworkKanbanIsolationTests` (Native.Tests/Cowork) drives a real `AgentHead` over a temporary tree. Three Code sessions stay three cards while five Cowork tasks are created, one is updated to Running and then closed. The Code and Cowork files differ and neither changes when the other is written. An automation run gets a card and no Cowork row. A full Code registry leaves Cowork writable, and a full Cowork registry leaves Code writable. Each `RecoverAfterRestart` pass touches only its own file's rows.
- Phase prose: an architecture paragraph (`SurfaceHost`, the per-account registry file, `CodeBoardSource`, `Api.Cowork` routes, and the gates), one Unreleased changelog entry, and one sentence in the accessibility focus paragraph.

## Task Commits

1. **Task 1: CoworkSurfaceIsolationTests architecture gates**: `33bb8a28` (test)
2. **Task 2: CoworkKanbanIsolationTests behavioural gates**: `fcd33cc0` (test)
3. **Task 3: Per-phase docs prose**: `eb2a6135` (docs)

**Plan metadata:** this summary is committed as `docs(69-06): summary` (the commit that adds this file). STATE.md and ROADMAP.md were not edited, as instructed.

## Files Created/Modified

- `tests/Autonogrammer.ArchitectureTests/CoworkSurfaceIsolationTests.cs` - the five rules, their negative controls, and the IL and signature scanners.
- `tests/Autonogrammer.ArchitectureTests/CoworkFixtures/SessionRegistry.cs` - negative-control stand-in named `SessionRegistry` for rule 5.
- `tests/Autonogrammer.ArchitectureTests/CoworkFixtures/CoworkRegistryConsumer.cs` - negative-control consumer for rules 2 and 5.
- `tests/Autonogrammer.Native.Tests/Cowork/CoworkKanbanIsolationTests.cs` - the behavioural gates.
- `docs/architecture.md` - one paragraph after the Cowork chat-core paragraph.
- `CHANGELOG.md` - one Unreleased entry at the top of Added.
- `docs/accessibility/README.md` - one sentence at line 43.

## Decisions Made

- The construction-site rule allows the `AgentHead` build path (`BuildAsync`, `BuildModelsAsync`, with their compiler-generated state machines and lambdas) and the `WebWorkbench` constructor. At HEAD the board is built in `BuildModelsAsync`, not in a member named `Build`.
- The Cowork reference rule uses the namespace subtree `Autonogrammer.Cowork`, which includes `Cowork.Artifacts` and `Cowork.Memory`, plus `Contracts.Cowork` and `Presentation.Cowork`.
- In the Kanban test "closing" a Cowork task is `SetStatus(Completed)`, because the Cowork registry has no `Close`.
- No production file was edited. Each rule is verified by its negative control rather than by a temporary production mutation.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] ArchUnitNET 0.13.4 has no namespace-subtree overload**
- **Found during:** Task 1 build
- **Issue:** `ResideInNamespace(ns, true)` does not compile against this ArchUnitNET version.
- **Fix:** Used `ResideInNamespaceMatching` with an escaped subtree pattern, `^<escaped ns>(\..+)?$`.
- **Files modified:** `CoworkSurfaceIsolationTests.cs`
- **Verification:** the five rules and their negative controls pass (10/10).
- **Committed in:** `33bb8a28`

**2. [Rule 2 - Missing critical] Analyzer-clean test code**
- **Found during:** Task 1 and Task 2 builds
- **Issue:** The repository's analyzers reject the first drafts: MA0048 (file name must match type), CA1812 and CA1515 (fixture visibility), MA0009 (regex without timeout), MA0002 and MA0006 (string comparers), CA1849 and VSTHRD103 (synchronous file reads), IDE0005 (unused using).
- **Fix:** Negative-control types are `internal abstract` or nested private, one type per file. The regex is replaced by string parsing. Comparisons are ordinal. File reads are async.
- **Files modified:** both test files and the two new fixture files.
- **Verification:** both builds are clean, and the test runs below pass.
- **Committed in:** `33bb8a28`, `fcd33cc0`

**3. [Scope - test-only] Fixture files in a new folder**
- **Found during:** Task 1
- **Issue:** The plan's `files_modified` lists two test files. The rule-5 negative control needs a type named `SessionRegistry` and a consumer in its own namespace, and MA0048 needs one type per file.
- **Fix:** Added `tests/Autonogrammer.ArchitectureTests/CoworkFixtures/` with two files. The other fixtures are nested in the test class.
- **Files modified:** two new files under `CoworkFixtures/`.
- **Committed in:** `33bb8a28`

**Total deviations:** 3 (one blocking, one analyzer cleanup, one test-only scope addition). None touches production code or the excluded registry files.

## Issues Encountered

- **Task 2 first run failed on my own fixture.** The Cowork registry requires a derived account folder key (`CoworkAccountFolder.KeyFromSubject`), not the raw subject. The fix is in the test; no production behaviour was involved.
- **No separate RED run before the GREEN change.** The plan's TDD order is RED, then GREEN. The rules and the behaviours already hold at HEAD (phases 69-01 to 69-05 are in place), so a RED run against the tree is not possible without editing production code, which the plan forbids. The failing direction is shown by the negative controls: each rule's violating fixture is reported. The one failing run on record is the account-key error above.
- **HEAD already includes phase 70 work.** `CoworkSessionHost`, `CoworkApprovalQueue`, `CoworkConversationModel` and the Native Cowork tests exist on this branch (commits `4cf49938`, `42d1ec1e`). The isolation rules still hold on this tree. The ordering is for the orchestrator to note.

## Verification (exact run summary lines)

- `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*CoworkSurfaceIsolationTests*"`: `Test run summary: Passed!`, total 10, failed 0, succeeded 10.
- `... --filter-class "*ProjectGraphTests*"`: `Passed!`, total 4, failed 0.
- `... --filter-class "*SuppressionLedgerTests*"`: `Passed!`, total 7, failed 0.
- `... --filter-class "*RepoHygieneTests*"`: `Passed!`, total 8, failed 0.
- `... --filter-class "*DeferredScopeTests*"`: `Passed!`, total 2, failed 0.
- `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*CoworkKanbanIsolationTests*"`: `Test run summary: Passed!`, total 7, failed 0, succeeded 7.
- `bash scripts/ci/check-changelog.sh`: `check-changelog: ... is valid (8 versions)`, exit 0.
- `bash scripts/ci/check-docs.sh`: `0 errors, 1 warnings`, exit 0. The warning is pre-existing: `docs/runbooks/hosting.md` links to `../../deploy/railway/README.md`. It is not from this plan.

The whole solution, the formatter and the full suite were not run, as instructed.

## Self-Check: PASSED

Created files and the three task commits were checked on disk and in history. The scope check showed no `src/`, `apps/` or `services/` production file changed.

- `docs/SUMMARY.md` is unchanged: this phase created no new docs page.
- No production file, `openapi.json`, `LayerMap`, slnx, slnf, lock, csproj, `ContractVersion` or `docs/parity/cowork-parity.md` was touched. Verified with `git diff --stat 9e3d42d9..HEAD`.
- `.claude/` (untracked, not ours) was not committed.

## Known Stubs

None.

## Threat Flags

None. T-69-15 is mitigated by the build-breaking rules and the behavioural test described above.

## Next Phase Readiness

Phase 70 can rely on the Kanban rule being a build gate. Phase 70 must keep these two test classes green, because they name the seams it adds to.
