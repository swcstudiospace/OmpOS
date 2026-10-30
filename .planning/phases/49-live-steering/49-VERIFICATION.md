# Phase 49 Verification: Live Steering

How to re-run each proof and what it proves. Run from the repository root with the .NET environment set as in `.planning/phases/44-plugin-submodules-launch-contract/44-VERIFICATION.md`. Tests not tagged Live are hermetic: a scripted omp peer, fake steering plugins, a stub Substrate handler, temporary directories. Live tests run the real omp in a sandbox, send no prompt, type no message and use no credential.

Results recorded on 2026-10-26 are in `49-SUMMARY.md`. The whole-solution run after the native steering work passed 5409 tests with 0 failed and 10 skipped (`Category=Live`) (F7).

## Gate tests (no omp needed)

| Criterion | Command | What it proves |
|-----------|---------|----------------|
| 1 (wire) | `dotnet test tests/Autonogrammer.Harness.Acp.Tests -c Release -- --filter-class "*AcpSteeringTests*"` and `dotnet test tests/Autonogrammer.EngineHost.Tests -c Release -- --filter-class "*HostSessionControlTests*"` | A steer is one text block without meta; a follow-up carries the follow-up queue meta; `promptRequired`, `failed` with a reason and an unknown outcome are mapped; nothing is sent without the capability, for an idle or closed session, or for a blank or over-8-KiB text; the host wrapper maps outcomes and queues. |
| 1 (composer) | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*AgentSessionRuntimeNativeSteeringTests*" --filter-class "*AgentSessionRuntimeSteeringTests*"` and `dotnet test tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*SteeringLiveDeliveryTests*" --filter-class "*SteeringNativeDeliveryTextTests*" --filter-class "*SteeringInputModelTests*"` | An injected steer is delivered into the turn, an injected follow-up is queued behind it, `promptRequired` runs the text as the next turn, a failure keeps the text and says why, the capability-less omp and a terminal use the inbox, every delivery status has its own sentence and the panel never claims more than the channel reported. |
| 1 (card) | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*NativeSteeringHeadTests*" --filter-class "*CardSteeringTests*" --filter-class "*LiveSteeringWiringTests*" --filter-class "*KanbanSteerFlyoutTests*"` and `dotnet test tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*KanbanCardSteeringTests*"` | The head authenticates with `agent`; a running chat takes the card message through omp's steering; omp's answer becomes the plain message of the card; the inbox path reports delivered, queued, refused, or sent but not acknowledged; the card's steer box keeps the text on a failure. |
| inbox | `dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*FileSteeringChannelTests*"` | The directive file schema and ordering, refusal on a stale heartbeat, the 64-directive queue, the 8192-byte limit and acknowledgement handling (including the `idle` flag). |
| 2 | `SteeringInputModelTests` (`AFollowUpWaitsForTheRunToComplete`, `ASteerBehindAFollowUpKeepsSendOrder`), `AgentSessionRuntimeNativeSteeringTests.AnInjectedFollowUpIsQueuedBehindTheRunningTurn`, `AgentSessionRuntimeSteeringTests.AFollowUpIsSentAsAFollowUpDirective` | A follow-up waits for the end of the run and keeps send order; with native steering it goes to omp's follow-up queue and Autonogrammer holds none. |
| 3 | `CardSteeringTests`, `NativeSteeringHeadTests`, `AgentSessionRuntimeNativeSteeringTests`, `AgentSessionRuntimeSteeringTests`, `SteeringLiveDeliveryTests`, `KanbanCardSteeringTests` (classes above) | A closed, lost, unknown or idle session fails with a plain message, queues nothing and changes nothing; a failed send keeps the text in the composer or the card. |
| 4 | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*CardSteeringTests*" --filter-class "*SteeringRecorderAdapterTests*"` | The Substrate request is a `note` with `payload.action` `steer`, the mode, the length and a hash prefix, never the text and never a delivery claim; without a healthy Substrate the steer is still sent and nothing is recorded. |
| 5 | `./bootstrap.sh`, or `dotnet build Autonogrammer.Linux.slnf -c Release` then `dotnet test Autonogrammer.Linux.slnf -c Release --no-build -- --filter-not-trait "Category=Live"` | Build with warnings as errors, analyzers and every non-live test. |
| auth | `dotnet test tests/Autonogrammer.Harness.Acp.Tests -c Release -- --filter-class "*AcpAuthenticationTests*"`, `dotnet test tests/Autonogrammer.Api.IntegrationTests -c Release -- --filter-class "*AcpAuthenticationHandshakeTests*"` and `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*EngineCompositionAuthenticationTests*"` | The client authenticates with the method the omp lists and refuses a non-empty `authMethods` without a configured method; the API composition does the same. |

## Live tests (real omp, no model turn)

Need `omp` on `PATH`, Linux and `AUTONOGRAMMER_LIVE_OMP=1`. They are excluded from the gate by the trait.

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 (wire contract) | `AUTONOGRAMMER_LIVE_OMP=1 dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests -c Release --no-build -- --filter-trait "Category=Live"` (`LiveOmpSteeringContractTests`) | `initialize` advertises `_meta.steering.supported` and `authMethods` with `agent`; `authenticate` and `session/new` succeed; `_session/steering` on the idle session answers `promptRequired` (queue `steering`); an unknown session answers -32602. | Passed on 2026-10-26 against omp 18.12.6, together with the earlier handshake test; recorded in `acp-auth-and-steering.md` and OQ-81. The run output itself is not stored in the evidence folder. |
| chips | `AUTONOGRAMMER_LIVE_OMP=1 dotnet test tests/Autonogrammer.Native.Tests -c Release --no-build -- --filter-trait "Category=Live"` (`LiveOmpOptionChipsTests`) | The composed engine signs in with `agent`, opens a session without a model turn and fills the mode and thinking chips from the real omp. | Passed on 2026-10-26 after the handshake timeout was raised; it failed once on a loaded host with the 10 s default. |
| 1 (running turn) | none | An `injected` outcome into a running turn and its visible effect. | Not verified: no model turn was approved (OD-07). |

## Evidence files

| File | What it shows |
|------|---------------|
| `docs/runbooks/evidence/phase-48-omp-plugins-2026-10-26/acp-auth-and-steering.md` | omp's ACP `initialize` answer (`authMethods` `agent`, `_meta.steering.supported`), the `_session/steering` outcomes read from the omp source, the consequences for Autonogrammer, and the live results above. |
| `docs/runbooks/evidence/phase-48-omp-plugins-2026-10-26/acp-fix-1-new-agent-composer.png`, `acp-fix-2-model-chip-lists-omp-models.png` | The real application under Xvfb after the authentication fix: a chat session opened with its composer and the model chip, and the chip's flyout listing omp's models grouped by provider. |

## Known limits of this verification

- No model turn (OD-07): nothing shows a steered turn taking the message into account, an `injected` outcome, or a follow-up delivered after the turn by omp.
- The inbox was proved with a fake plugin in tests and, against the real omp, only through the heartbeat and the connection report (Phase 48).
- The steering history entries are tested in the model; no view lists them.
- Linux only. Windows, macOS, Android and hosted deployments were not run.
- No real Substrate server; the ledger record was asserted against a stub.
