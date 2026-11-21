---
phase: 75-cowork-parity-projects-memory-schedules
plan: 75-11
subsystem: cowork
tags: [cowork, schedules, automations]
status: not_shipped
---

# 75-11 Summary

Plan `75-11-PLAN.md` (scheduler tick routes Cowork to a project-aware Cowork host with correct context /
memory policy; code scheduling retains `HeadAutomationSessions`) is **not shipped** on `origin/main` @
`c04095ba`.

## Missing (plan's own artifacts, absent on `main`)

- `apps/Autonogrammer.Native/Cowork/ScheduledCoworkSessionStarter.cs` — **absent**.
- `tests/Autonogrammer.Native.Tests/Cowork/ScheduledCoworkSessionStarterTests.cs` — **absent**
  (`tests/Autonogrammer.Native.Tests/Cowork/` has no such file).
- `apps/Autonogrammer.Native/Composition/Automations/AutomationsHost.cs` — not confirmed present as the
  plan's scheduling entry point in this verification; `AgentHead.cs` / `CoworkSessionHost.cs` exist but
  do not themselves show the scheduled-tick routing the plan requires.

`docs/reference/cowork-projects-memory.md` and `CHANGELOG.md` are on the tree, but they document the
present Cowork surfaces; they do not evidence a scheduler starter that does not exist.

## Verified in this session (no product-code change)

- `dotnet restore Autonogrammer.Linux.slnf --locked-mode` — ok.
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` — 0 warnings, 0 errors.
- `dotnet format Autonogrammer.Linux.slnf --verify-no-changes --no-restore` — clean.
- `dotnet test tests/Autonogrammer.Presentation.Tests` — 2646/2646 (non-live).

## Next step for the owning lane

Implement the scheduled starter + tests, or mark 75-11 out of scope and update the phase counts
(75 currently 6/13 summaries). Route to A05. This doc lane made no code change.
