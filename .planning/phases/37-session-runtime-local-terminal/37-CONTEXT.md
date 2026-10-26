# Phase 37 Context: Session Runtime, Streaming and Local Terminal

Scope: ROADMAP.md Phase 37 success criteria 1 to 5 and requirements VX-06, VX-07, VX-08.
Depends on: Phase 36 (complete and verified).
Out of scope: a real herdr endpoint (criterion 5 keeps it blocked), Windows and macOS local terminal backends (no runner exists), mobile and web terminals (Phase 43), the chat window (Phase 38).

## Criteria status

| # | Criterion (ROADMAP, verbatim summary) | Status | Source | Test that meets it |
|---|---------------------------------------|--------|--------|--------------------|
| 1 | Session service creates, attaches, detaches, pauses, resumes and terminates through the herdr contract; detach never terminates; id, provider and last-known status persist locally; a restart reattaches by stored id. Gate lifecycle tests over the fake. | Met | `src/OmpOS.Harness.Herdr/HerdrSessionService.cs`; `HerdrSessionStore.cs`; `IHerdrSessionClient.cs` | Fake contract: `FakeHerdrSessionClientTests.PauseAndResumeMoveBetweenRunningAndPaused`, `TerminatedSessionCannotChangeAgainAndSurvivesARestartAsTerminated`, `DetachLeavesTheSessionAliveAndFindable`. Service: `HerdrSessionServiceTests.CreatePersistsTheIdProviderAndLastKnownStatusAcrossReopen`, `DetachKeepsTheSessionAliveAndDoesNotRecordItAsEnded`, `RestoreReattachesALiveSessionByItsStoredId`. Added this phase: `HerdrLifecyclePersistenceTests.PauseResumeAndTerminatePersistTheLastKnownStatusAcrossReopen`. |
| 2 | Heartbeat every 15 seconds; stale after three missed heartbeats (45 seconds of simulated time). Gate test with a fake clock. | Met | `HerdrHeartbeatMonitor.cs` (`HeartbeatInterval` 15 s, `MissedBeatsBeforeStale` 3, `StaleAfter` 45 s, on an injected `TimeProvider`) | `HerdrHeartbeatMonitorTests.StaleIsReportedAtFortyFiveSecondsAndNotAtFortyFourPointNineSeconds`, `TheStaleWindowIsThreeMissedBeatsOfFifteenSeconds`, `ABeatRestartsTheSilenceCount`; `HerdrSessionServiceTests.StaleIsReportedAfterFortyFiveSecondsOfFakeClockSilence`, `ABeatFromTheStreamRestartsTheStaleCount`. |
| 3 | Streamed output sequence-numbered; replay from the last acknowledged sequence yields no duplicate blocks; cancelling a response leaves the session running. Gate transcript replay test. | Met (gap closed this phase) | `src/OmpOS.Harness.Agent/OmpEventLog.cs` (sequence from 1, `Acknowledge`, `ReadAfter`, `FollowAsync`); `OmpEventReceiver.cs` (accepts only sequences above the highest accepted); `services/OmpOS.Api/Endpoints/EventStreamEndpoints.cs` (`Last-Event-ID` resumes at `last + 1`); `src/OmpOS.Sessions/HarnessEventProjection.cs` (block ids derive from the fold, so a duplicate changes the state) | Agent: `OmpEventLogTests.ReplayFromTheLastAcknowledgedSequenceResumesAfterIt`; `OmpEventStreamTests.ReplayFromEveryStartPositionYieldsEachLaterEventExactlyOnce`, `ReceiverAcceptsEachSequenceOnceUnderDuplicateAndOutOfOrderDelivery`; `RecordedTranscriptReplayTests.CancelTurnLeavesTheSessionRunningAndAcceptsTheNextTurn`. Projection, added this phase: `ReplayFromAcknowledgedSequenceTests.ResumingAfterEveryAcknowledgedSequenceYieldsTheSameStateAsAFullReplay`, `ResumingAtTheAcknowledgedSequenceItselfRedeliversThatBlock`, `CancelledResponseDoesNotEndTheStream`. Transport, existing and not re-run here: `tests/OmpOS.Api.IntegrationTests/SseResumeTests.Resume_WithLastEventId_DeliversRemainingExactlyOnce`; `tests/OmpOS.Web.Tests/SseEventStreamTests.DuplicateSeqIsDropped`. |
| 4 | A local shell spawns, echoes input and exits with its code on Linux in the gate. Windows and macOS recorded as not run until their runners exist. Linux gate test and runbook entries for the other platforms. | Met for Linux; Windows and macOS recorded as not run | `src/OmpOS.Harness.Terminal/LinuxLocalPtyBackend.cs`; `PtyTerminalSession.cs`; `ITerminalBackend.cs` | `LinuxLocalPtyBackendTests.EchoesTypedInputAndDeliversTheMarkerAndTheExitCode` (spawns `/bin/sh`, writes `typed`, reads `MARK<typed>`, exit code 7). Also `ASizeSetBeforeTheInputIsTheSizeTheCommandSees`, `StartsTheCommandInTheExplicitWorkingDirectory`, `DoesNotPassHostVariablesOutsideTheAllowlistToTheCommand`. Runbook entries: see 37-SUMMARY.md (docs/ was not edited this phase). |
| 5 | The real herdr endpoint stays blocked until the owner decides the restart semantics recorded in STATE. STATE entry. | Met (blocked by design) | `.planning/STATE.md` line 135 (PRODUCT BEHAVIOUR); line 81 (Later owner decisions); no production `IHerdrSessionClient` exists, only `tests/OmpOS.Harness.Herdr.Tests/FakeHerdrSessionClient.cs` | Verified by search: no class outside the fake implements `IHerdrSessionClient`. |

## Owner decision: restart semantics (OPEN, not chosen here)

STATE.md, line 135, records this owner decision, quoted verbatim:

> PRODUCT BEHAVIOUR (owner decision): durability across a herdr server restart or machine reboot. Options: (a) accept the herdr limit; after a restart or reboot, sessions resume from the stored native omp session reference with the transcript restored (default); (b) require process survival, which herdr v0.9.5 does not provide, so it needs an upstream change or another runtime; (c) defer cloud terminals until (b) is available.

Recorded default: (a). This phase does not choose. The code path that (a) describes exists and is tested: a lost session is never reported as running and resumes only through `HerdrSessionService.ResumeFromNativeReferenceAsync`, covered by `ALostSessionIsNotRunningUntilItResumesFromItsNativeReference`. Options (b) and (c) need no code in this phase. The real herdr endpoint stays unbuilt until the owner picks one.

## Decisions recorded for this phase

| ID | Decision | Where |
|----|----------|-------|
| D37-1 | Heartbeat cadence is a constant contract (15 s, 3 beats, 45 s stale), not a client-driven timer. The client consumes the server's beat stream and measures silence on the injected clock. Nothing here enforces the server's cadence; the monitor's doc comment says so. | `HerdrHeartbeatMonitor.cs` |
| D37-2 | Replay bounds are exclusive at the client boundary. `Last-Event-ID` N resumes at N + 1. The repository's `ListFromSeqAsync` is inclusive, so a caller that passes the acknowledged sequence itself re-delivers that event. | `EventStreamEndpoints.cs`; `ISessionRepository.cs` |
| D37-3 | The projection has no sequence de-duplication of its own. De-duplication happens at the receiver (`OmpEventReceiver`, the web SSE client) and at the transport bound. The projection test documents this rather than hiding it. | `HarnessEventProjection.cs`; `ReplayFromAcknowledgedSequenceTests.cs` |
| D37-4 | The Linux PTY backend is the only local terminal backend. No Windows or macOS backend is written in this phase, since there is no runner. | `src/OmpOS.Harness.Terminal/` |
| D37-5 | No change to any `docs/` file. The three records named for this phase are the only `.planning` files written, and the runbook entries go into 37-SUMMARY.md. | this file; 37-SUMMARY.md |

## Known limits

- Criterion 5 is blocked on the owner's durability decision in STATE (quoted above). Stated verbatim as a limit.
- The Windows and macOS local terminal backends are not run: no runner exists (XP-11 covers macOS on the Mac channel; the Windows runner is an external host). Stated verbatim as a limit.
- Mobile and web terminals are not in this phase; they belong to Phase 43.
- The "cloud environment is a persistent container with idle sleep" part of VX-08 is not a Phase 37 criterion and has no code here.
- `ListFromSeqAsync` and `Last-Event-ID` disagree on bound inclusivity (D37-2). The transport is correct today; the projection test pins the boundary so a change to either side fails a test.

## Verified facts

- The herdr contract is `ContractVersion = 1`. Lifecycle states: `Running`, `Paused`, `Terminated`, `Lost`. A lost session keeps its native reference.
- herdr v0.9.5: detaching leaves panes running; a server restart or machine reboot keeps no processes (STATE, verified facts, 2026-10-22).
- The gate baseline before this phase: `OmpOS.Harness.Herdr.Tests` 78 passed, `OmpOS.Sessions.Tests` 94 passed, `OmpOS.Harness.Terminal.Tests` 50 passed, `OmpOS.Harness.Agent.Tests` 23 passed, all with `--filter-not-trait "Category=Live"`.
