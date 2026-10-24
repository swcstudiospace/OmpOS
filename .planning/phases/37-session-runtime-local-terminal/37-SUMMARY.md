# Phase 37 Summary: Session Runtime, Streaming and Local Terminal

Status: criteria 1 to 4 met with evidence; criterion 5 open, blocked on the owner's durability decision (verbatim limit below). No product code under `src/` or `services/` changed. Two test files were added, and the gate projects they belong to pass.

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Lifecycle through the herdr contract; detach never terminates; id, provider and last-known status persist locally; restart reattaches by stored id. | Met | Fake contract tests: `FakeHerdrSessionClientTests.PauseAndResumeMoveBetweenRunningAndPaused`, `TerminatedSessionCannotChangeAgainAndSurvivesARestartAsTerminated`, `DetachLeavesTheSessionAliveAndFindable`. Service: `HerdrSessionServiceTests.CreatePersistsTheIdProviderAndLastKnownStatusAcrossReopen`, `DetachKeepsTheSessionAliveAndDoesNotRecordItAsEnded`, `RestoreReattachesALiveSessionByItsStoredId`. New, closes gap G1: `HerdrLifecyclePersistenceTests.PauseResumeAndTerminatePersistTheLastKnownStatusAcrossReopen`. |
| 2 | Heartbeat every 15 s; stale after three missed beats (45 s of simulated time); gate test with a fake clock. | Met | `HerdrHeartbeatMonitorTests.StaleIsReportedAtFortyFiveSecondsAndNotAtFortyFourPointNineSeconds`, `TheStaleWindowIsThreeMissedBeatsOfFifteenSeconds`, `ABeatRestartsTheSilenceCount`; `HerdrSessionServiceTests.StaleIsReportedAfterFortyFiveSecondsOfFakeClockSilence`, `ABeatFromTheStreamRestartsTheStaleCount`. The 15 s cadence is a constant contract. Nothing here enforces the server's cadence (`HerdrHeartbeatMonitor` doc comment). |
| 3 | Sequence-numbered stream; replay from the last acknowledged sequence yields no duplicate blocks; cancel leaves the session running. | Met | Agent: `OmpEventLogTests.ReplayFromTheLastAcknowledgedSequenceResumesAfterIt`; `OmpEventStreamTests.ReplayFromEveryStartPositionYieldsEachLaterEventExactlyOnce`, `ReceiverAcceptsEachSequenceOnceUnderDuplicateAndOutOfOrderDelivery`; `RecordedTranscriptReplayTests.CancelTurnLeavesTheSessionRunningAndAcceptsTheNextTurn`. New, closes gap G2 at the projection, through the repository replay boundary: `ReplayFromAcknowledgedSequenceTests.ResumingAfterEveryAcknowledgedSequenceYieldsTheSameStateAsAFullReplay`, `ResumingAtTheAcknowledgedSequenceItselfRedeliversThatBlock`, `CancelledResponseDoesNotEndTheStream`. Transport, existing and not re-run in this pass: `tests/OmpOS.Api.IntegrationTests/SseResumeTests.Resume_WithLastEventId_DeliversRemainingExactlyOnce`; `tests/OmpOS.Web.Tests/SseEventStreamTests.DuplicateSeqIsDropped`. |
| 4 | Local shell spawns, echoes input and exits with its code on Linux in the gate. Windows and macOS recorded as not run; runbook entries for them. | Met for Linux; Windows and macOS recorded as NOT RUN | `LinuxLocalPtyBackendTests.EchoesTypedInputAndDeliversTheMarkerAndTheExitCode` (spawns `/bin/sh`, writes `typed`, reads `MARK<typed>`, exit code 7). Also `ASizeSetBeforeTheInputIsTheSizeTheCommandSees`, `StartsTheCommandInTheExplicitWorkingDirectory`, `DoesNotPassHostVariablesOutsideTheAllowlistToTheCommand`. Runbook entries: below. |
| 5 | The real herdr endpoint stays blocked until the owner decides the restart semantics recorded in STATE. | Met (open by design) | STATE.md line 135 holds the decision and options (a), (b), (c), default (a). No production `IHerdrSessionClient` exists; only `tests/OmpOS.Harness.Herdr.Tests/FakeHerdrSessionClient.cs` implements it. |

## Criterion 5, open item (verbatim limit)

The real herdr endpoint is blocked until the owner decides the restart semantics. STATE.md, line 135, records the options (quoted, not chosen here):

> (a) accept the herdr limit; after a restart or reboot, sessions resume from the stored native omp session reference with the transcript restored (default); (b) require process survival, which herdr v0.9.5 does not provide, so it needs an upstream change or another runtime; (c) defer cloud terminals until (b) is available.

Owner action: choose (a), (b) or (c). Until then no herdr endpoint is built.

## Runbook entries: local terminal, other platforms (NOT RUN)

These entries sit in this record, not in `docs/runbooks/`, because this phase does not edit `docs/`. The parent should move them into a runbook if it wants them there.

- **Windows**: NOT RUN. No local terminal backend exists for Windows, and no Windows runner exists. The Windows 11 24H2 runner is an external host (owner item). When a runner exists, the backend and its tests must be written first; the Linux `LinuxLocalPtyBackendTests` do not apply to Windows.
- **macOS**: NOT RUN. No macOS local PTY backend exists in this phase. The macOS backend runs on the Mac channel and is listed under XP-11.
- **Linux**: run in the gate. `dotnet test tests/OmpOS.Harness.Terminal.Tests --filter-not-trait "Category=Live"`.

## Files

| File | Change |
|------|--------|
| `tests/OmpOS.Harness.Herdr.Tests/HerdrLifecyclePersistenceTests.cs` | New. One test, G1. |
| `tests/OmpOS.Sessions.Tests/ReplayFromAcknowledgedSequenceTests.cs` | New. Three tests, G2. |
| `.planning/phases/37-session-runtime-local-terminal/37-CONTEXT.md` | New. |
| `.planning/phases/37-session-runtime-local-terminal/37-01-PLAN.md` | New. |
| `.planning/phases/37-session-runtime-local-terminal/37-SUMMARY.md` | New (this file). |

Pre-existing uncommitted work in the tree was left as found. This phase edited no existing file. `STATE.md` was not edited: its entry for criterion 5 already exists. The VERIFICATION file was not written, as instructed.

## Commands and results

Environment: `export DOTNET_ROOT=/root/.dotnet DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1; export PATH=/root/.dotnet:$PATH`

Baseline before any change (`dotnet test <project> --filter-not-trait "Category=Live"`):

| Project | Exit | Total | Failed | Succeeded | Skipped |
|---------|------|-------|--------|-----------|---------|
| `OmpOS.Harness.Herdr.Tests` | 0 | 78 | 0 | 78 | 0 |
| `OmpOS.Sessions.Tests` | 0 | 94 | 0 | 94 | 0 |
| `OmpOS.Harness.Terminal.Tests` | 0 | 50 | 0 | 50 | 0 |
| `OmpOS.Harness.Agent.Tests` | 0 | 23 | 0 | 23 | 0 |

Targeted builds after the change (`dotnet build <project> -nologo`, grep for error or warning empty):

| Project | Exit | Warnings | Errors |
|---------|------|----------|--------|
| `OmpOS.Harness.Herdr.Tests` | 0 | 0 | 0 |
| `OmpOS.Sessions.Tests` | 0 | 0 | 0 |

The first build of each new file failed with Meziantou analyzer errors (MA0004 on the un-configured awaits; MA0002 and MA0006 on string comparison). Both were fixed in the source before the passing run below. The fix was in the test code, not in the analyzer settings.

New suites (`--filter-class`):

| Command | Exit | Total | Failed | Succeeded |
|---------|------|-------|--------|-----------|
| `dotnet test tests/OmpOS.Harness.Herdr.Tests --no-build --filter-not-trait "Category=Live" --filter-class "*HerdrLifecyclePersistenceTests*"` | 0 | 1 | 0 | 1 |
| `dotnet test tests/OmpOS.Sessions.Tests --no-build --filter-not-trait "Category=Live" --filter-class "*ReplayFromAcknowledgedSequenceTests*"` | 0 | 3 | 0 | 3 |

Full touched projects, and evidence projects, after the change:

| Command | Exit | Total | Failed | Succeeded | Skipped |
|---------|------|-------|--------|-----------|---------|
| `dotnet test tests/OmpOS.Harness.Herdr.Tests --no-build --filter-not-trait "Category=Live"` | 0 | 79 | 0 | 79 | 0 |
| `dotnet test tests/OmpOS.Sessions.Tests --no-build --filter-not-trait "Category=Live"` | 0 | 97 | 0 | 97 | 0 |
| `dotnet test tests/OmpOS.Harness.Terminal.Tests --no-build --filter-not-trait "Category=Live"` | 0 | 50 | 0 | 50 | 0 |
| `dotnet test tests/OmpOS.Harness.Agent.Tests --no-build --filter-not-trait "Category=Live"` | 0 | 23 | 0 | 23 | 0 |

The gate-wide baseline was not re-run. The full gate was not run for this phase.

## Checks not performed

- No mutation check was run. The claim that the new tests fail when the behavior is removed comes from the code path, not from a run. The path for G1 is `HerdrSessionService.ApplyAsync` → `PersistAsync`, the only write of `LastKnownState` for pause, resume and terminate. The path for G2 is the inclusive `ListFromSeqAsync` bound, which `ResumingAtTheAcknowledgedSequenceItselfRedeliversThatBlock` pins.
- The Api and Web SSE tests cited for criterion 3 were not re-run in this pass.
- No real herdr endpoint was called.

## Open items

1. Criterion 5: owner decides option (a), (b) or (c) in STATE.md (above).
2. Windows and macOS local terminal backends: not built, not run (no runner; macOS on the Mac channel, XP-11).
3. Runbook location: the entries sit here because `docs/` was out of scope for this phase. Parent to decide whether to move them.
4. `VERIFICATION` file: written by the verifier next, not by this phase.
