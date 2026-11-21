---
phase: 75-cowork-parity-projects-memory-schedules
plan: 75-08
subsystem: cowork
tags: [cowork, launch, overlay, standing-context]
status: shipped
---

# 75-08 Summary

Plan `75-08-PLAN.md` (contributions retain all fields through actual launch; one overlay includes
`memory.backend: off` for Cowork only; first prompt carries the 2 KiB-bounded paragraph) is
**substantially shipped** on `origin/main` @ `c04095ba`.

## Present on the tree

- `src/Autonogrammer.Harness.AgentLoop/Launch/OmpLaunchPlan.cs` and `OmpLaunchPlanner.cs` —
  launch-plan contribution types.
- `src/Autonogrammer.Cowork/Chat/CoworkSessionProfile.cs`,
  `src/Autonogrammer.Cowork/Chat/StandingContextContributor.cs`,
  `src/Autonogrammer.Cowork/Instructions/StandingContextBuilder.cs` — standing-context build.
- `apps/Autonogrammer.Native/Cowork/CoworkEngineComposer.cs`, `CoworkEngineRequest.cs`,
  `CoworkSessionHost.cs` — Native composition consuming the contributions.
- Tests: `tests/Autonogrammer.Harness.AgentLoop.Tests/Launch/LaunchContributionTests.cs`,
  `tests/Autonogrammer.Cowork.Tests/Chat/StandingContextContributorTests.cs`,
  `tests/Autonogrammer.Cowork.Tests/Instructions/StandingContextBuilderTests.cs`,
  `tests/Autonogrammer.Native.Tests/Cowork/CoworkEngineComposerTests.cs` — all present.

## Caveat (retained from plan)

Whether omp itself honours `memory.backend: off` is not observed by this repo — that needs a live omp
run (OD-07). The plan's artifact requirement is met; the live-consumption half is an honest gap, not a
false pass.

## Verified in this session (no product-code change)

- `dotnet restore Autonogrammer.Linux.slnf --locked-mode` — ok.
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` — 0 warnings, 0 errors.
- `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore` — clean.
- `dotnet test tests/Autonogrammer.Presentation.Tests` — 2646/2646 (non-live).

This doc lane made no code change; no new tests were executed beyond the suites above.
