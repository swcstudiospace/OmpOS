---
phase: 01-foundation
plan: 02
subsystem: contracts
tags: [system-text-json, source-generation, polymorphism, contracts, xunit-v3, shouldly]

# Dependency graph
requires:
  - phase: 01-foundation plan 01
    provides: Build governance (Directory.Build.props with reflection disabled, CPM versions, slnx/slnf) the contracts compile under
provides:
  - OmpOS.Contracts L0 surface (Session, Message, IntentEnvelope, nine UiBlock variants, StreamEvent with five payload kinds, SandboxDescriptor, WorkflowRun, OmpOSError, ContractVersion) with unknown-kind-safe STJ serialization
  - OmpOsJsonContext source-generation context plus UiBlockConverter and StreamEventPayloadConverter
  - Green OmpOS.Contracts.Tests suite (26 tests) wired into OmpOS.slnx and OmpOS.Linux.slnf
affects: [02-core, 03-platform, 04-surfaces, architecture-tests]

# Actuals (#2632) — pairs with the plan's `estimate` to calibrate future estimates.
actuals:
  tokens: 12500
  tasks: 3
  commits: 2

# Tech tracking
tech-stack:
  added: []
  patterns: [unknown-kind-safe JsonConverter over source-generated derived TypeInfo, kind-first write via JsonObject.Insert, verbatim Raw re-serialization for unknown kinds]

key-files:
  created:
    - src/OmpOS.Contracts/Serialization/OmpOsJsonContext.cs
    - src/OmpOS.Contracts/Ui/UiBlockConverter.cs
    - src/OmpOS.Contracts/Streaming/StreamEventPayloadConverter.cs
    - src/OmpOS.Contracts/Streaming/StreamEventSeqComparer.cs
    - src/OmpOS.Contracts/ContractVersion.cs
    - tests/OmpOS.Contracts.Tests/OmpOS.Contracts.Tests.csproj
  modified:
    - OmpOS.slnx
    - OmpOS.Linux.slnf

key-decisions:
  - "A2uiSurfaceBlock kind string is a2ui-surface (not a2ui), so bare a2ui stays an unknown kind per the plan's unknown-kind test"
  - "OmpOSError is a sealed record (Code plus Detail), not Exception-derived, avoiding custom-exception analyzer noise under latest-all"
  - "Contract records expose IReadOnlyList and IReadOnlyDictionary members for a CA2227-clean warnings-as-errors build"
  - "Semver assertion uses Version.TryParse three-part check, not regex — Meziantou MA0009 flags the regex under TWAE"
  - "Test-project packages.lock.json committed with the RED commit so locked restore reaches the compiler for the RED gate"

patterns-established:
  - "Converter-backed polymorphism: abstract record plus JsonConverter attribute holding an Ordinal kind-to-Type map, resolving known kinds via options.GetTypeInfo and degrading everything else to a typed Unknown record"
  - "TDD gate evidence: test(01-02) RED commit with CS0246 failures, feat(01-02) GREEN commit with 26 of 26 passing, no refactor commit when format is already clean"

requirements-completed: [FOUND-06]

# Coverage metadata (#1602)
coverage:
  - id: D1
    description: "Nine UiBlock variants round-trip with documented kind strings; unknown or missing kinds degrade to UnknownBlock with byte-identical re-serialization"
    requirement: "FOUND-06"
    verification:
      - kind: unit
        ref: "tests/OmpOS.Contracts.Tests/Ui/UiBlockJsonTests.cs"
        status: pass
    human_judgment: false
  - id: D2
    description: "StreamEvent round-trips Seq, SessionId, CorrelationId, timestamp and all five payload kinds; unknown payload kinds degrade to UnknownPayload; Seq comparer orders ascending"
    requirement: "FOUND-06"
    verification:
      - kind: unit
        ref: "tests/OmpOS.Contracts.Tests/Streaming/StreamEventJsonTests.cs"
        status: pass
      - kind: unit
        ref: "tests/OmpOS.Contracts.Tests/Streaming/StreamEventSeqComparerTests.cs"
        status: pass
    human_judgment: false
  - id: D3
    description: "Reflection-based serialization provably throws InvalidOperationException"
    requirement: "FOUND-06"
    verification:
      - kind: unit
        ref: "tests/OmpOS.Contracts.Tests/Serialization/ReflectionDisabledTests.cs"
        status: pass
    human_judgment: false
  - id: D4
    description: "ContractVersion.Current is three-part semver and HeaderName is X-Ompos-Contract-Version"
    requirement: "FOUND-06"
    verification:
      - kind: unit
        ref: "tests/OmpOS.Contracts.Tests/ContractVersionTests.cs"
        status: pass
    human_judgment: false
  - id: D5
    description: "Full D-03 surface compiles package-free with zero warnings under latest-all warnings-as-errors"
    requirement: "FOUND-06"
    verification:
      - kind: other
        ref: "dotnet build OmpOS.Linux.slnf -c Release --no-restore -warnaserror (17 projects, 0 warnings, 0 errors)"
        status: pass
    human_judgment: false

# Metrics
duration: 11min
completed: 2026-10-20
status: complete
---

# Phase 1 Plan 02: L0 Contracts with Unknown-Kind-Safe STJ Summary

**OmpOS.Contracts owns the full D-03 surface with two kind converters and a source-generated JSON context; 26 of 26 tests pass with zero warnings.**

## Performance

- **Duration:** 11 min
- **Started:** 2026-10-20T05:57:32Z
- **Completed:** 2026-10-20T06:08:33Z
- **Tasks:** 3
- **Files modified:** 45 (42 created, 3 modified)

## Accomplishments
- RED-first contract tests: nine-block Theory, unknown/missing-kind degradation, required-member throws, payload round-trips, reflection-disabled proof, Seq ordering, ContractVersion
- Implemented all 36 D-03 contract files: sessions, intents, nine UiBlock variants, StreamEvent with five payload kinds, sandboxes, workflows, errors, OmpOsJsonContext, ContractVersion
- Unknown-kind-safe converters resolve known kinds through source-generated TypeInfo and never throw on future kinds
- Test project wired into OmpOS.slnx and OmpOS.Linux.slnf exactly once each; locked restore, TWAE build, and format verify all green

## Task Commits

Each task was committed atomically:

1. **Task 1: RED failing contract tests** - `08519d1` (test)
2. **Task 2: GREEN implement contracts** - `aeda598` (feat)
3. **Task 3: REFACTOR format and lockfiles** - no commit (dotnet format made zero changes; AssemblyMarker kept since no analyzer fires on it)

_Note: TDD tasks may have multiple commits (test → feat → refactor)_

## Files Created/Modified
- `src/OmpOS.Contracts/Sessions/` - Session, Message, SessionStatus
- `src/OmpOS.Contracts/Intents/` - IntentEnvelope, IntentKind, Diagnostic
- `src/OmpOS.Contracts/Ui/` - UiBlock base plus nine sealed variants plus UiBlockConverter
- `src/OmpOS.Contracts/Streaming/` - StreamEvent, payload base plus five variants, StreamEventPayloadConverter, StreamEventSeqComparer
- `src/OmpOS.Contracts/Sandboxes/` - SandboxDescriptor, SandboxStatus, Isolation, Attestation
- `src/OmpOS.Contracts/Workflows/` - WorkflowRun, WorkflowRunStatus
- `src/OmpOS.Contracts/Errors/` - OmpOSError, ErrorCode
- `src/OmpOS.Contracts/Serialization/OmpOsJsonContext.cs` - Source-generation context registering every variant
- `src/OmpOS.Contracts/ContractVersion.cs` - Current 1.0.0 plus X-Ompos-Contract-Version header name
- `tests/OmpOS.Contracts.Tests/` - Hand-written MTP csproj plus five test classes (26 tests)
- `OmpOS.slnx`, `OmpOS.Linux.slnf` - Test project wired in exactly once each

## Decisions Made
- A2uiSurfaceBlock kind string is `a2ui-surface` (the plan's RED action names it so); bare `a2ui` stays an unknown kind, which is what the unknown-kind test asserts.
- OmpOSError is a sealed record carrying ErrorCode plus Detail rather than Exception-derived, avoiding SYSLIB0051/custom-exception analyzer friction under `latest-all`.
- Contract records expose `IReadOnlyList`/`IReadOnlyDictionary` members so the warnings-as-errors build stays CA2227-clean.
- Semver assertion uses `Version.TryParse` with a three-part check instead of a regex (Meziantou MA0009 flags the regex under TWAE); equivalent to `^\d+\.\d+\.\d+$`.
- Test-project `packages.lock.json` committed with the RED commit so the locked restore reaches the compiler for the RED gate (CS0246, exit 1).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] A2uiSurfaceBlock kind string corrected to a2ui-surface**
- **Found during:** Task 2 GREEN test run (UnknownKind test threw JsonException for missing surfaceId/payload)
- **Issue:** Converter mapped `a2ui` to A2uiSurfaceBlock, but the plan's unknown-kind test requires bare `a2ui` to degrade to UnknownBlock; the RED action names the variant kind `a2ui-surface`
- **Fix:** KindToType maps `a2ui-surface` to A2uiSurfaceBlock; Theory row updated to match
- **Files modified:** src/OmpOS.Contracts/Ui/UiBlockConverter.cs, tests/OmpOS.Contracts.Tests/Ui/UiBlockJsonTests.cs
- **Verification:** Full suite 26 of 26 pass
- **Committed in:** aeda598 (Task 2 commit)

**2. [Rule 1 - Bug] Semver regex replaced with Version.TryParse**
- **Found during:** Task 2 GREEN build (MA0009 error under TWAE)
- **Issue:** `^\d+\.\d+\.\d+$` trips Meziantou MA0009 (ReDoS); `using System.Text.RegularExpressions` then trips IDE0005 once removed
- **Fix:** `Version.TryParse` plus Build >= 0 and Revision == -1 (exactly three numeric parts); dropped both usings made redundant
- **Files modified:** tests/OmpOS.Contracts.Tests/ContractVersionTests.cs
- **Verification:** ContractVersionTests pass; slnf build 0 warnings
- **Committed in:** aeda598 (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (2 bugs)
**Impact on plan:** Both required for the suite to pass under the locked analyzer set. No scope creep; test intent unchanged.

## Issues Encountered
- One line-anchored edit landed on the wrong map row during the kind-string fix (duplicated A2uiSurfaceBlock entry, dropped tool_call). Caught immediately by the next test run (TypeInitializationException, duplicate key), repaired with a full-range rewrite, suite green after. No lasting effect.
- CS0419 ambiguous `List{T}.Sort` cref in comparer docs; replaced with plain text.

## TDD Gate Compliance
- RED gate commit `test(01-02)`: present (`08519d1`); RED run exited 1 with CS0246/CS0234 type-missing errors.
- GREEN gate commit `feat(01-02)`: present (`aeda598`); suite prints `Passed!` with 26 passed, 0 failed, 0 skipped.
- REFACTOR commit: absent by protocol (format produced zero changes); no behavior changed after GREEN.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- FOUND-06 met: every layer can compile against the L0 shapes with source-generated serialization.
- Ready for 01-03 (ArchitectureTests layer-direction gate, which consumes the new test project wiring).
- Assumption recorded on StreamEventSeqComparer: List.Sort is not stable; Phase 2 code needing stable order must sort by (Seq, insertion index).

---
*Phase: 01-foundation*
*Completed: 2026-10-20*

## Self-Check: PASSED
