---
phase: 01-foundation
plan: 01
subsystem: infra
tags: [dotnet, msbuild, cpm, blazor, bootstrap, governance]

# Dependency graph
requires: []
provides:
  - Governed monorepo skeleton (SDK pin, strictness flags, CPM, lock files, formatter)
  - Full 15-project solution graph with compiling placeholders
  - Blazor WASM shell project with Contracts/UI-only references
  - One-command bootstrap script
affects: [02-core, 03-platform, 04-surfaces, 05-verify]

# Actuals (#2632) — pairs with the plan's `estimate` to calibrate future estimates.
actuals:
  tokens: 12600
  tasks: 4
  commits: 4

# Tech tracking
tech-stack:
  added: []
  patterns: ["Directory.Build.props central strictness", "CPM with transitive pinning", "AssemblyMarker placeholders", "slnx plus slnf solution layout"]

key-files:
  created: [global.json, Directory.Build.props, Directory.Packages.props, .editorconfig, BannedSymbols.txt, BannedSymbols.NoProcess.txt, .gitignore, .gitattributes, OmpOS.slnx, OmpOS.Linux.slnf, bootstrap.sh, .env.example]
  modified: []

key-decisions:
  - "Blazor package versions (Components.Web/WebAssembly/DevServer 10.0.13) live in CPM, matching the template-resolved versions"
  - "Web Program.cs uses ConfigureAwait(false) to satisfy CA2007/MA0004 under latest-all"
  - "bootstrap.sh keeps the dotnet test step as-is despite exit 1 on the empty test set (plan-directed); first test projects in plan 01-02 resolve it"
  - "Vendored Bootstrap dist TODO comments are upstream code, not authored TODOs"

patterns-established:
  - "Placeholder projects contain exactly one XML-documented AssemblyMarker type, no TODO"
  - "Generated csproj files are stripped of every property Directory.Build.props owns"
  - "Solution filter OmpOS.Linux.slnf lists every Linux-buildable project; apps/OmpOS.App stays absent until Phase 4"

requirements-completed: [FOUND-01, FOUND-02, FOUND-03, FOUND-04]

# Metrics
duration: 12min
completed: 2026-10-20
status: complete
---

# Phase 1 Plan 01: Governance, Skeleton, Placeholders, Bootstrap Summary

**Governed 15-project .NET 10 monorepo that restores locked, builds warning-free, formats clean, and bootstraps with one command**

## Performance

- **Duration:** 12 min (approx)
- **Started:** 2026-10-20T05:40:00Z (approx)
- **Completed:** 2026-10-20T05:52:00Z
- **Tasks:** 4
- **Files modified:** 114 (incl. vendored Blazor static assets)

## Accomplishments
- SDK 10.0.402 pinned in global.json with MTP test runner; strictness enforced from Directory.Build.props
- CPM with transitive pinning, committed packages.lock.json per project, locked restore green
- Full D-01 project graph (15 projects) compiling warning-free with AssemblyMarker placeholders
- Blazor WASM shell referencing only Contracts + UI, CompressionEnabled false
- bootstrap.sh one-command build/test/format pipeline plus .env.example

## Task Commits

Each task was committed atomically:

1. **Task 1: Tracer: governance files plus solution skeleton builds green** - `af530a5` (feat)
2. **Task 2: Expansion A: L1, harness, and Sessions placeholders (14 files)** - `9fd3ea8` (feat)
3. **Task 3: Expansion B: GenUi, Sandbox, Workflows, UI, services, and Web shell (16 files)** - `3cc0c12` (feat)
4. **Task 4: bootstrap.sh plus .env.example and first full bootstrap run** - `d0e1545` (feat)

## Files Created/Modified
- `global.json` - SDK 10.0.402 pin, latestPatch rollForward, MTP test runner
- `Directory.Build.props` - net10.0, nullable, TWAE, latest-all, deterministic, CPM, lock files, audit, test-project and BannedSymbols wiring
- `Directory.Packages.props` - Central test/library/Blazor/analyzer versions
- `BannedSymbols.txt` / `BannedSymbols.NoProcess.txt` - Newtonsoft.Json, blocking-call, and process-spawning bans
- `.editorconfig` - Template plus OmpOS overrides (LF, final newline, IDE0005 warning, CA1303 off, test section)
- `OmpOS.slnx` / `OmpOS.Linux.slnf` - Solution and Linux filter with 15 projects
- `src/*/AssemblyMarker.cs` (13 projects) - XML-documented placeholder markers
- `apps/OmpOS.Web/` - Blazor WASM shell with minimal router, trimmed imports
- `bootstrap.sh` - One-command bootstrap, executable
- `.env.example` - OMPOS_ENV with Phase 1 no-variables note

## Decisions Made
- Blazor package versions (Microsoft.AspNetCore.Components.Web, WebAssembly, WebAssembly.DevServer, all 10.0.13) declared in Directory.Packages.props under a "Blazor shells" group; template-inline Version attributes removed as CPM requires.
- `await builder.Build().RunAsync().ConfigureAwait(false)` in Web Program.cs: the template line trips CA2007 + MA0004 under latest-all; ConfigureAwait preserves template semantics at the entry point.
- bootstrap.sh `dotnet test` step kept as-is per plan direction even though it exits 1 with "No test projects were found" on the current zero-test tree; plan 01-02 (first test projects) resolves it. No `|| true` masking added.
- TODO strings inside `wwwroot/lib/bootstrap/**` are upstream Bootstrap distribution comments, not authored TODOs; authored sources are TODO-free.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Web Program.cs failed CA2007/MA0004 under latest-all**
- **Found during:** Task 3 (Expansion B build)
- **Issue:** The `dotnet new blazorwasm` template line `await builder.Build().RunAsync();` violates CA2007 and MA0004 once the locked analyzer set applies.
- **Fix:** Appended `.ConfigureAwait(false)`; template semantics unchanged.
- **Files modified:** apps/OmpOS.Web/Program.cs
- **Verification:** `dotnet build OmpOS.Linux.slnf -c Release --no-restore -warnaserror` → 16 projects, 0 warnings.
- **Committed in:** 3cc0c12 (Task 3 commit)

**2. [Rule 3 - Blocking] Template PackageReference versions conflict with CPM**
- **Found during:** Task 3 (Expansion B restore)
- **Issue:** Generated Web/UI csproj files carry inline `Version="10.0.13"` attributes, which CPM forbids.
- **Fix:** Removed inline versions, added the three Blazor PackageVersion rows to Directory.Packages.props.
- **Files modified:** apps/OmpOS.Web/OmpOS.Web.csproj, src/OmpOS.UI/OmpOS.UI.csproj, Directory.Packages.props
- **Verification:** `dotnet restore OmpOS.Linux.slnf --force-evaluate` exits 0.
- **Committed in:** 3cc0c12 (Task 3 commit)

**3. [Rule 3 - Blocking] Directory.Packages.props edit orphaned the Libraries group opener**
- **Found during:** Task 3 (Blazor versions insert)
- **Issue:** A range-replace of the Libraries ItemGroup opener left its PackageVersion rows outside any ItemGroup and a duplicated closing tag.
- **Fix:** Re-added the opener, removed the duplicate closer; verified full file reads clean.
- **Files modified:** Directory.Packages.props
- **Verification:** Full-file read plus successful restore/build after the fix.
- **Committed in:** 3cc0c12 (Task 3 commit)

---

**Total deviations:** 3 auto-fixed (1 bug, 2 blocking)
**Impact on plan:** All required for a warning-free locked build. No scope creep; no architectural changes.

## Issues Encountered
- `dotnet test OmpOS.Linux.slnf -c Release --no-build` exits 1 with "No test projects were found" because the filter contains zero test projects. Per the plan's explicit direction the step stays as-is (no masking flags); `./bootstrap.sh` therefore currently exits 1 at the test step. Plan 01-02 creates the first test projects and resolves this. Restore, build, and format steps all pass.
- The orchestrator-supplied `<project_root_pin>` guard script trips `pin-unbound` on a correctly bound pin: the `{PINNED_ROOT}` sentinel in its `case` arm was substituted with the real path along with the assignment, so the bound value matches the "unexpanded" arm. Verified the substantive property directly (actual toplevel equals pinned toplevel via git on both sides) with a corrected guard before every write/commit. Orchestrator should substitute only the assignment line in future prompts.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Ready for plan 01-02 (contracts, tests, architecture gate): the props, CPM versions, editorconfig, and solution layout it builds on are green.
- Watch item: bootstrap stays red at the test step until the first test projects land.

---
*Phase: 01-foundation*
*Completed: 2026-10-20*

## Self-Check: PASSED
All 12 key files found on disk; all 4 task commits (af530a5, 9fd3ea8, 3cc0c12, d0e1545) present in git log.
