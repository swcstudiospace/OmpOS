# Phase 73 Plan 10: Dispatch eligibility, local consent and honest verification

Status: blocked — not executed. The plan's precondition is that 73-09, phase 74's actual Dispatch task authorization / closed operation set / local command-approval integration, and 75-11's completed scheduler-to-Cowork-host path and its prerequisites are available; "Otherwise stop and report the missing dependency without asserting closure" (73-10-PLAN.md:45). None of the three is satisfied: 73-09 is unexecuted (73-09-SUMMARY.md), phase 74 has no 74-11 SUMMARY and no Dispatch command-approval integration usable here, and 75-11 is unexecuted. No task was run and no product code was changed.

## What was checked (tree at c04095ba, 2026-11-21)

- Task 1 artifacts absent: no gateway test exercises local vs Dispatch vs scheduled session eligibility, no separate local command approval before per-app/action approvals, and no Dispatch grant lifetime/revocation proof. `CoworkHead.cs` registers no computer provider at all (73-07-SUMMARY.md), so there is no registration condition to distinguish. `ComputerToolProviderTests.cs` asserts the 15 descriptors in isolation (73-03); it does not prove Dispatch/local eligibility from the running head.
- Task 2 artifacts exist only in isolation: `tests/Autonogrammer.Native.Tests/Computer/IndicatorWindowViewTests.cs` and `ApprovalDialogViewTests.cs` exist (73-06), but they test the composed views, not the actually mounted controls bound to a session coordinator, and they do not cover the plan's matrix (unavailable platform, Wayland, hidden/minimized/fullscreen-occluded indicator, unknown visibility, unregistered hotkey, audit failure, gate exceptions). No real-desktop smoke (indicator visibility, local approval, emergency Stop) was performed or claimed; per the plan a headless run is not proof of topmost behavior across real displays (73-10-PLAN.md:55).
- The plan's phase-criteria re-run (offline core, private-Xvfb live, Windows/macOS builds, architecture, licence/notices) was not executed by this plan. Criterion-level outcomes are reported in the Phase 73 VERIFICATION re-run with actual results; this plan claims none.

## Blockers (unchanged from the plan)

- 73-09 local head/gateway/indicator/kill wiring: unexecuted.
- Phase 74 Dispatch task authorization + local command approval integration: not available to this plan.
- 75-11 scheduler-to-Cowork-host path + prerequisites: not integrated.
- Live external provider, physical multi-display, Windows/macOS behavior: unverified absent actual evidence (73-10-PLAN.md:68).

## Not run

- `ComputerUseHeadCompositionTests`, `ComputerToolProviderTests`, `IndicatorWindowViewTests`, `ApprovalDialogViewTests` and the offline core suite — not run as plan evidence; the eligibility/consent behavior they would need to prove does not exist in the running head. Isolated pre-existing cases are reported in the Phase 73 VERIFICATION re-run.
