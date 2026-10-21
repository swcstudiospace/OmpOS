---
phase: 01-foundation
plan: 03
subsystem: infra
tags: [archunitnet, github-actions, podman-compose, temporal, docs, adrs]

# Dependency graph
requires:
  - phase: 01-foundation plans 01-01 and 01-02
    provides: [monorepo layout with placeholder projects, contracts with STJ source generation, bootstrap.sh, build governance]
provides:
  - ArchitectureTests suite proving layer direction with live-gate proofs
  - CI pipeline mirroring bootstrap plus audit, secret scan, and compose gates
  - Validated podman-compose stack with the Temporal dev server
  - Eight-file docs skeleton with ADR-0001, ADR-0002, and a 26-row owned open-questions seed
affects: [phase-2-core, phase-3-platform, phase-4-surfaces, phase-5-verify]

# Tech tracking
tech-stack:
  added: [TngTech.ArchUnitNET.xUnitV3 0.13.4, podman-compose 1.6.0 (CI only), temporalio/temporal 1.9.1 (compose image)]
  patterns: [layer map as data with one-line project onboarding, csproj-graph test complementing ArchUnitNET, hygiene tests over repo files]

key-files:
  created: [tests/OmpOS.ArchitectureTests/OmpOS.ArchitectureTests.csproj, tests/OmpOS.ArchitectureTests/LayerMap.cs, tests/OmpOS.ArchitectureTests/ProjectGraphChecker.cs, tests/OmpOS.ArchitectureTests/ProjectGraphTests.cs, tests/OmpOS.ArchitectureTests/ArchUnitLayerTests.cs, tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs, .github/workflows/ci.yml, deploy/compose.yaml, README.md, CONTRIBUTING.md, docs/architecture.md, docs/decisions/ADR-0001-monorepo-layout.md, docs/decisions/ADR-0002-surface-technology.md, docs/open-questions.md, docs/runbooks/local-dev.md, docs/runbooks/sandbox.md]
  modified: [OmpOS.slnx, OmpOS.Linux.slnf]

key-decisions:
  - "ArchUnitNET rules take System.Reflection.Assembly objects, not short names: the string overload of ResideInAssembly compares full assembly names, so short names match nothing (correction to 01-RESEARCH FS-9/VF-3)"
  - "Hygiene docker matching uses ordinal word scans with an explicit prose allowlist instead of regex (Meziantou MA0009/MA0023 fire even on source-generated regex)"
  - "open-questions.md seeded with 26 rows: 14 research gaps plus 4 research extras plus the 8 ultrathink ambiguities transcribed from the dispatch context (Q1-Q8 text is not locatable in the repo)"
  - "actions/checkout@v7 appears twice in ci.yml (both jobs), so the plan's boilerplate grep-count of exactly 1 does not apply; every acceptance literal is present"

patterns-established:
  - "New project onboarding: slnx plus slnf entry, one-line LayerMap entry, ProjectReference from ArchitectureTests, --force-evaluate relock"
  - "Live-gate proofs: every mechanical gate ships a synthetic-violation test plus a manually verified real violation (reverted before commit)"

requirements-completed: [FOUND-05, FOUND-07, FOUND-08, FOUND-09]

# Metrics
duration: 13min
completed: 2026-10-20
status: complete
---

# Phase 1 Plan 03: Architecture gates, CI, compose, docs skeleton Summary

**Layer-direction tests with live-gate proofs, a CI pipeline mirroring bootstrap plus audit/scan/compose gates, a validated Temporal compose stack, and the eight-file docs skeleton — bootstrap green over the finished tree.**

## Performance

- **Duration:** 13 min
- **Started:** 2026-10-20T06:12:53Z
- **Completed:** 2026-10-20T06:25:57Z
- **Tasks:** 3
- **Files modified:** 18

## Accomplishments

- ArchitectureTests suite (8 tests) enforces D-02 layers via the csproj graph and ArchUnitNET type rules, with synthetic and real live-gate proofs
- `ci.yml` runs locked restore, TWAE build, TRX/coverage tests, format verify, a JSON-parsed vulnerability gate, gitleaks v3, and podman-compose validation, plus a guarded macOS MAUI job
- `deploy/compose.yaml` serves the Temporal 1.9.1 dev server under the `workflows` profile with a CLI healthcheck
- Docs skeleton complete: README, CONTRIBUTING, architecture, ADR-0001, ADR-0002, 26-row open questions, two runbooks

## Task Commits

Each task was committed atomically:

1. **Task 1: Architecture tests** - `cbea25d` (feat)
2. **Task 2: CI workflow plus compose stack** - `fc1a609` (feat)
3. **Task 3: Docs skeleton with ADRs, open questions, runbooks** - `ab5c368` (docs)

**Plan metadata:** committed separately via `gsd_run query commit` (docs: complete plan)

## Files Created/Modified

- `tests/OmpOS.ArchitectureTests/` - LayerMap, ProjectGraphChecker, graph/ArchUnit/hygiene tests, lock file
- `OmpOS.slnx`, `OmpOS.Linux.slnf` - ArchitectureTests wired into both (exactly once each)
- `.github/workflows/ci.yml` - build-test plus guarded macOS shells jobs
- `deploy/compose.yaml` - temporal dev server, workflows profile
- `README.md`, `CONTRIBUTING.md` - bootstrap/run/test, conventions, TODO policy
- `docs/architecture.md` - mermaid layers, components, data flow
- `docs/decisions/ADR-0001-monorepo-layout.md` - layout, layers, ownership, `/home/temporal` deviation
- `docs/decisions/ADR-0002-surface-technology.md` - Blazor, MAUI, SSE, harness, Temporal flag, sandbox layering
- `docs/open-questions.md` - 26 owned rows
- `docs/runbooks/local-dev.md`, `docs/runbooks/sandbox.md` - bootstrap, workflows profile, troubleshooting, prerequisites

## Decisions Made

- ArchUnitNET rules take `System.Reflection.Assembly` objects: probing proved the string overload of `ResideInAssembly` compares full assembly names, so short names match nothing (and an empty object set fails the rule, which can masquerade as a passing negative test). Both the positive layer rules and the negative rule-fires proof now evaluate over real object sets.
- Hygiene docker matching uses ordinal word scans with an explicit prose allowlist (`never installs docker`, `docker is not used/unsupported`, `docker.io/` prefix) instead of regex, because Meziantou MA0009/MA0023 fire even on `[GeneratedRegex]` patterns under TWAE.
- `open-questions.md` carries 26 rows: the 14 research gaps, 4 research extras (volume ownership, nc fallback, MAUI workload, gitleaks licence), and the 8 ultrathink ambiguities transcribed from the dispatch context since Q1–Q8 text is not locatable in the repo. Every row is owned by `ompos-maintainers`.
- `actions/checkout@v7` appears twice (build-test and shells jobs, as the plan's action text mandates), so the verify boilerplate's grep-count of exactly 1 applies only to the Temporal image literal. All acceptance literals verified present.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] ArchUnitNET string predicate matches nothing**
- **Found during:** Task 1 (first green run: 6/7, `ArchUnit_LowerLayersDoNotDependOnHigher` failed with "requires positive evaluation")
- **Issue:** Research FS-9/VF-3 implied `ResideInAssembly(shortName)` works; probing proved the string overload compares full assembly names, and an empty object set fails — which also meant the negative test could pass vacuously
- **Fix:** Rules take `System.Reflection.Assembly` objects loaded from the test output (uniform `LoadFrom` by LayerMap name, App skipped via on-disk csproj check); verified both name hypotheses fail and the object approach passes before committing
- **Files modified:** tests/OmpOS.ArchitectureTests/ArchUnitLayerTests.cs
- **Verification:** 8/8 suite green; temporary probe files deleted before commit
- **Committed in:** cbea25d (Task 1 commit)

**2. [Rule 3 - Blocking] Analyzer errors in new test code under latest-all plus TWAE**
- **Found during:** Task 1 (initial build: 10 errors)
- **Issue:** CA1515 (public helper types), MA0002 (missing comparers), MA0009/MA0023 (regex), CA1859 (interface return)
- **Fix:** Helpers internal; explicit `StringComparer.Ordinal`; regex replaced with ordinal word scans; concrete `Dictionary` return type
- **Files modified:** LayerMap.cs, ProjectGraphChecker.cs, ProjectGraphTests.cs, RepoHygieneTests.cs, ArchUnitLayerTests.cs
- **Verification:** Warning-free Release build
- **Committed in:** cbea25d (Task 1 commit)

**3. [Rule 3 - Blocking] Hygiene test could not require CI/compose files that arrive in Task 2**
- **Found during:** Task 1
- **Issue:** `RepoHygiene_NoDockerTooling` scans `ci.yml`/`compose.yaml`, which do not exist until Task 2; asserting existence would fail the Task 1 verify
- **Fix:** Absent files contribute no lines (documented in the test comment); Task 2's files are fully scanned once present, proven by the Task 3 full-suite run
- **Files modified:** tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs
- **Verification:** Task 1 suite green without the files; Tasks 2–3 runs green with them
- **Committed in:** cbea25d (Task 1 commit)

---

**Total deviations:** 3 auto-fixed (3 blocking)
**Impact on plan:** All three were required to reach the plan's own green gates; no scope creep. Deviation 1 additionally corrects a research finding future phases rely on.

## Issues Encountered

- `grep -c 'actions/checkout@v7'` returns 2, not 1: both CI jobs pin checkout v7 exactly as the plan's action text mandates. The verify boilerplate's exactly-1 expectation predates that reading; all acceptance literals confirmed present, so this is a plan-text inconsistency, not a defect.
- Two `edit`-tool patches mis-anchored (one duplicated lines, one misplaced a method signature); both repaired immediately and verified by rebuild. No trace in the tree.

## Authentication Gates

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Foundation complete after this plan: monorepo, contracts, guardrails, CI, compose, docs all green via `./bootstrap.sh` (34/34 tests).
- Phase 2 (Core) can start: parser, harness, sessions, and API build on the enforced layer map; the scripted in-memory adapter fixtures still need the Phase 2 research spike on real `sessionUpdate` variants (OQ-02).

## Threat Flags

None - no new security-relevant surface beyond the plan's threat model. CI permissions are `contents: read`, gitleaks covers committed secrets with full-history checkout, and the vulnerability gate parses JSON because the bare list exits 0 (T-3-01 through T-3-03 mitigated as planned).

## Self-Check

- LayerMap contains all 17 D-02 assembly names including OmpOS.App: verified in test (`ProjectGraph_RejectsUpwardReference` asserts the only map entry without a csproj is OmpOS.App).
- All eight named tests present and passing: `dotnet test` reports 8/8 succeeded.
- `OmpOS.slnx` and `OmpOS.Linux.slnf` each contain the ArchitectureTests path exactly once: grep counts verified 1 and 1.
- Manual Parser-to-Api reference check fired naming `(OmpOS.Parser, OmpOS.Api)`, then reverted with lock files restored clean.
- Both YAML files parse; all acceptance literals present; no `version:` key; no TODOs.
- All eight docs exist, each exceeds 400 bytes; README has the four required `## ` sections; open-questions header exact with 26 owned rows (awk count 28 including header and separator).
- `./bootstrap.sh` exits 0 with `bootstrap: OK` over the finished tree.

## Self-Check: PASSED

---
*Phase: 01-foundation*
*Completed: 2026-10-20*
