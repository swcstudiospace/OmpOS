# Phase 73 Plan 09: Real local head, gateway, indicator and kill wiring

Status: blocked — not executed. The plan's precondition is that 75-07 is implemented with its authenticated Native account gate and sign-out lifecycle available, with shared files re-read after 75-07 and serialized against 75-11 (73-09-PLAN.md:58). That prerequisite is absent: `.planning/phases/75-cowork-parity-projects-memory-schedules/` contains no 75-07 SUMMARY (only 75-07-PLAN.md). The plan also states "The restored 5ce514eb Native baseline is not evidence of safety" and the 73-08 tracer's restored baseline remains the current Native code. No task was run and no product code was changed.

## What was checked (tree at c04095ba, 2026-11-21)

- Task 1 artifacts absent: no AgentHead-to-Cowork gateway integration test exists that drives the mounted local opt-in control (`ComputerSettingsPage` Accept → `ComputerOptInModel` → `ComputerActionGate.OptIn(sessionId)` for the active session only). `AgentHead.cs` still has no `ComputerUseHeadComposition.Build` / `AddTools` call (see 73-07-SUMMARY.md), so there is no real composed host, no provider discovery, no durable write-ahead audit record, and no negative-case injection proof. `ComputerUseHeadCompositionTests.cs` calls `Build` directly in isolation (73-06); it does not drive the production gateway.
- Task 2 artifacts absent: the baseline indicator callback is not a verified kill control (73-VERIFICATION.md:63), there is no platform-observed visibility/occlusion evidence before actuation, and no test proves the visible indicator Stop, hotkey, session Stop and tray Stop all trip the same coordinator that gates injection. `KillSwitchCoordinatorTests` (3/3 per 73-VERIFICATION.md:15) test the coordinator in isolation, not through the composed session.
- Consent cannot be exercised end-to-end because the local opt-in → session association it would drive is not connected to a running Cowork session.

## Blockers (unchanged from the plan)

- 75-07 authenticated Native account gate + sign-out lifecycle: not implemented (no SUMMARY, no gate in tree).
- 75-11 shared-file serialization (AgentHead.cs, CoworkSessionHost.cs): not integrated.
- Human desktop proof is not implied by headless tests even if executed (73-09-PLAN.md:82).

## Not run

- `ComputerUseHeadCompositionTests` (Native.Tests) and `KillSwitchCoordinatorTests` (Harness.ComputerUse.Tests) — not run as plan evidence; the head/gateway/indicator behavior they would need to prove does not exist. The isolated pre-existing cases are reported in the Phase 73 VERIFICATION re-run.
