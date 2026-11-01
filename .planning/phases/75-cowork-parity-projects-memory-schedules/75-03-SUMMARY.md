---
phase: 75-cowork-parity-projects-memory-schedules
plan: 75-03
subsystem: automations
tags: [scheduling, session-target, missed-run, approval]
requires: []
provides:
  - AutomationTargetKind.Session with preset cron, missed-run policy and isolation gating
  - Unattended approval wait that cancels and never approves
  - IScheduledSessionStarter seam with plain data only
affects: [75-06]
tech-stack:
  added: []
  patterns:
    - Session runs start only through IScheduledSessionStarter
    - Non-session targets keep the five-minute misfire rule
key-files:
  created:
    - src/Autonogrammer.Harness.Automations/Scheduling/IScheduledSessionStarter.cs
    - src/Autonogrammer.Harness.Automations/AutomationOptions.cs
    - src/Autonogrammer.Contracts/Cowork/Scheduling/SchedulingContracts.cs
  modified:
    - src/Autonogrammer.Harness.Automations/Hosted/AutomationScheduler.cs
    - src/Autonogrammer.Harness.Automations/Hosted/AutomationRunner.cs
key-decisions:
  - "RunOnce folding applies only to session targets so existing cloud misfire tests stay unchanged."
  - "schedule.run, schedule.late and schedule.waiting_for_approval are recorded on the automation run summary. No cowork_events sink exists in this plan's files."
requirements-completed: [AG-75-8, AG-75-9]
duration: 0
completed: 2026-10-30
status: complete
coverage:
  - id: D1
    description: Session targets validate with presets, API refusal and Skip refusal without filesystem isolation.
    requirement: AG-75-8
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.Automations.Tests/Scheduling/SchedulePresetTests.cs
        status: pass
      - kind: unit
        ref: tests/Autonogrammer.Harness.Automations.Tests/Scheduling/SessionTargetTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Missed runs fold into one late run inside 24 hours and unattended approval cancels after 30 minutes.
    requirement: AG-75-9
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.Automations.Tests/Scheduling/MissedRunTests.cs
        status: pass
      - kind: unit
        ref: tests/Autonogrammer.Harness.Automations.Tests/Scheduling/UnattendedApprovalTests.cs
        status: pass
    human_judgment: false
---

# Phase 75 Plan 03: Scheduled tasks Summary

Session targets, preset cron, missed-run folding and an unattended approval wait that cancels instead of approving.

## Accomplishments

- `AutomationTargetKind.Session` carries surface, project, trusted folders, approval mode, preset and missed-run policy. The validator refuses session targets on the API host and refuses Skip unless the session is filesystem-isolated or a per-session acknowledgement is recorded.
- `RunOnce` folds missed session occurrences into one late run inside 24 hours. Older than that is skipped. `Skip` keeps the five-minute rule. A disabled task does not catch up. Cloud targets are unchanged.
- An unattended Manual or Auto run waits in `waiting_for_approval` and is cancelled after 30 minutes. Nothing approves itself. The runner starts a session only through `IScheduledSessionStarter` and re-checks Skip at start.

## Verification

- `SchedulePresetTests`, `SessionTargetTests`, `MissedRunTests` and `UnattendedApprovalTests` passed.
- `/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.Automations.Tests -c Release --filter-not-trait "Category=Live"` passed: 404 succeeded, 0 failed.
- Engine and contracts Release builds with `-warnaserror` succeeded with 0 warnings.

## Deviations

- Schedule event kinds are stored on the run summary. This plan's file list does not include a cowork_events writer, and no phase-68 sink is referenced by the engine.
- The engine surface enum matches the phase-68 names but does not reference `SessionSurface`, because Harness.Automations does not reference Contracts and the plan forbids a new project reference.

## Self-Check: PASSED
