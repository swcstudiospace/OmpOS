---
phase: 75-cowork-parity-projects-memory-schedules
plan: 75-13
subsystem: cowork
tags: [cowork, native, projects, sessions]
status: partial_not_shipped
---

# 75-13 Summary

Plan `75-13-PLAN.md` (project selection in the Cowork rail creates a linked task; no selection creates an
unlinked task; linked metadata/folders/memory-mode persisted before session open; missing local folder
reported, not fatal) is **partially shipped** on `origin/main` @ `c04095ba`.

## Present on the tree

- `src/Autonogrammer.Presentation/Cowork/Projects/ProjectListModel.cs` — project list model.
- `src/Autonogrammer.Native.Views/Shell/CoworkSurfaceServices.cs`,
  `src/Autonogrammer.Native.Views/Views/Cowork/CoworkRailView.axaml(.cs)` — rail surface.
- `apps/Autonogrammer.Native/Cowork/CoworkSurfaceServicesFactory.cs`,
  `apps/Autonogrammer.Native/Cowork/CoworkSessionHost.cs` — session open path.

## Missing (plan's own artifacts, absent on `main`)

- `tests/Autonogrammer.Native.Tests/Cowork/CoworkProjectSessionSelectionTests.cs` — **absent**.
- `tests/Autonogrammer.Presentation.Tests/Cowork/CoworkProjectSessionSelectionTests.cs` — **absent**
  (`tests/Autonogrammer.Presentation.Tests/Cowork/` was checked for a selection suite; none is present
  under that name).

The linked/unlinked task-creation wiring exists in the surface/host sources, but the selection behaviour
itself (selection → linked task, no selection → unlinked task, missing-folder non-fatal reporting) has
**no dedicated test on `main`** and is recorded here as unevidenced, not passing.

## Verified in this session (no product-code change)

- `dotnet restore Autonogrammer.Linux.slnf --locked-mode` — ok.
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` — 0 warnings, 0 errors.
- `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore` — clean.
- `dotnet test tests/Autonogrammer.Presentation.Tests` — 2646/2646 (non-live).

## Next step for the owning lane

Add the selection tests, or downgrade the plan's truths to "candidates present, behaviour untested".
Route to A06. This doc lane made no code change.
