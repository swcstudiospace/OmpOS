# Phase 73 Plan 07: Production wiring gap closure + gate + SOTA

Status: blocked — not executed. This plan is `autonomous: false` and its tracer requires production call sites in `AgentHead.cs` / `CoworkHead.cs` registering the computer provider into `CoworkToolHost` (73-07-PLAN.md:95-143). That wiring does not exist in the tree: the 73-08 tracer that attempted the same seam "discarded composition results and did not wire a functional indicator", its additions were removed, and the five Native files were restored to the `5ce514eb` baseline (73-08-SUMMARY.md:35,54; 73-VERIFICATION.md:59,63 gap 1). No task was run and no product code was changed.

## What was checked (tree at c04095ba, 2026-11-21)

- `apps/Autonogrammer.Native/Agents/AgentHead.cs` contains no call to `ComputerUseHeadComposition.Build` and no `ComputerUseToolsComposition.AddTools`; the only Computer Use reference is the 73-06 settings-factory lambda `ComputerUseHeadComposition.SettingsFactory?.Invoke()` at lines 130 and 261. `CoworkHead.cs` likewise registers no computer provider. Task 1's tracer artifact is absent; `ShouldRegisterFor` (73-VERIFICATION.md:50) is never called from a running head.
- Task 2's SOTA overlay docs already exist in the tree (`73-SOTA-STATES.md`, `73-SOTA-A11Y.md`, `73-SOTA-SECURITY.md`, elevated by 73-08, 73-08-SUMMARY.md:61), but they document states/mitigations of a path that is not wired into the head; this plan did not re-audit or update them.
- Task 3 (full gate + VERIFICATION/ROADMAP update): not run by this plan. Criterion-level results are reported in the Phase 73 VERIFICATION re-run with actual outcomes; this plan claims none of them. The 73-VERIFICATION.md gap-1 grep the plan requires (`grep -c 'gap 1'` = 0) would fail: gap 1 is still recorded at 73-VERIFICATION.md:63.

## Blockers

- Production wiring gap (73-VERIFICATION.md gap 1): no provider registration, no functional kill indicator, no passing gate evidence at HEAD. The 73-08 attempt proved the seam is not trivially closable; this plan was never executed to close it.
- `autonomous: false` — requires explicit orchestration/human gate, which was not given for this plan.

## Not run

- The plan's `<verify>` commands (`ComputerUseHeadComposition`/`ComputerUseWiring` Native tests, Native build, wiring grep) — not run as plan evidence; the wiring they verify does not exist. The pre-existing `ComputerUseHeadCompositionTests` (14/14 per 73-VERIFICATION.md:15) test `Build` in isolation, not production registration, and are covered by the Phase 73 VERIFICATION re-run.
